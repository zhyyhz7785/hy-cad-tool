import type { FUniver } from '@univerjs/core/facade';
import { IRenderManagerService, SHEET_VIEWPORT_KEY } from '@univerjs/engine-render';

import { findMainSheetCanvas } from './univer-viewport';

/** 布局页（纸张边界）隐藏 canvas 滚动条并锁定视口 scroll=0。 */

interface ScrollBarLike {
  enableHorizontal?: boolean;
  enableVertical?: boolean;
}

interface ViewportLike {
  getScrollBar?: () => ScrollBarLike | null;
  scrollToViewportPos?: (pos: { viewportScrollX?: number; viewportScrollY?: number }) => unknown;
  scrollToBarPos?: (pos: { x?: number; y?: number }) => unknown;
}

interface SceneLike {
  getViewport?: (key: string) => ViewportLike | null;
  makeDirty?: (dirty?: boolean) => void;
}

interface RenderUnitLike {
  scene?: SceneLike;
}

const APP_LAYOUT_FIT_CLASS = 'hycad-layout-grid-fit';
const RETRY_MS = 50;
const MAX_RETRIES = 20;

let scrollbarsHidden = false;
let scrollLocked = false;
let savedEnableHorizontal = true;
let savedEnableVertical = true;
let retryTimer: number | undefined;
let clampFrame = 0;
let wheelInstalled = false;

function getRenderManager(): IRenderManagerService | null {
  try {
    const univer = window.univer as { __getInjector?: () => { get: <T>(token: unknown) => T } } | undefined;
    const injector = univer?.__getInjector?.();
    if (!injector)
      return null;
    return injector.get(IRenderManagerService);
  }
  catch {
    return null;
  }
}

function resolveActiveRender(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
): RenderUnitLike | null {
  const unitId = univerAPI.getActiveWorkbook?.()?.getId?.();
  if (!unitId)
    return null;
  const render = getRenderManager()?.getRenderById(unitId);
  return (render as RenderUnitLike | undefined) ?? null;
}

function resolveMainViewport(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
): { viewport: ViewportLike; scene: SceneLike } | null {
  const render = resolveActiveRender(univerAPI);
  const scene = render?.scene;
  if (!scene?.getViewport)
    return null;

  const viewport = scene.getViewport(SHEET_VIEWPORT_KEY.VIEW_MAIN);
  if (!viewport)
    return null;

  return { viewport, scene };
}

function refreshAfterScrollChange(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
  scene: SceneLike,
): void {
  scene.makeDirty?.(true);
  const sheet = univerAPI.getActiveWorkbook?.()?.getActiveSheet?.() as { refreshCanvas?: () => void } | null | undefined;
  sheet?.refreshCanvas?.();
}

function clampViewportScrollOrigin(univerAPI: ReturnType<typeof FUniver.newAPI>): boolean {
  const resolved = resolveMainViewport(univerAPI);
  if (!resolved)
    return false;

  const { viewport, scene } = resolved;
  viewport.scrollToViewportPos?.({ viewportScrollX: 0, viewportScrollY: 0 });
  viewport.scrollToBarPos?.({ x: 0, y: 0 });
  refreshAfterScrollChange(univerAPI, scene);
  return true;
}

function applyScrollBarVisibility(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
  visible: boolean,
): boolean {
  const resolved = resolveMainViewport(univerAPI);
  if (!resolved)
    return false;

  const { viewport, scene } = resolved;
  const scrollBar = viewport.getScrollBar?.();
  if (!scrollBar)
    return false;

  if (!visible) {
    if (!scrollbarsHidden) {
      savedEnableHorizontal = scrollBar.enableHorizontal !== false;
      savedEnableVertical = scrollBar.enableVertical !== false;
      scrollbarsHidden = true;
    }
    scrollBar.enableHorizontal = false;
    scrollBar.enableVertical = false;
    clampViewportScrollOrigin(univerAPI);
  } else if (scrollbarsHidden) {
    scrollBar.enableHorizontal = savedEnableHorizontal;
    scrollBar.enableVertical = savedEnableVertical;
    scrollbarsHidden = false;
  }

  refreshAfterScrollChange(univerAPI, scene);
  return true;
}

function scheduleScrollBarVisibility(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
  visible: boolean,
  attempt = 0,
): void {
  window.clearTimeout(retryTimer);
  if (applyScrollBarVisibility(univerAPI, visible))
    return;
  if (visible || attempt >= MAX_RETRIES)
    return;
  retryTimer = window.setTimeout(
    () => scheduleScrollBarVisibility(univerAPI, visible, attempt + 1),
    RETRY_MS,
  );
}

function isLayoutScrollLocked(): boolean {
  return document.getElementById('app')?.classList.contains(APP_LAYOUT_FIT_CLASS) ?? false;
}

function installLayoutWheelBlock(): void {
  if (wheelInstalled)
    return;
  wheelInstalled = true;

  window.addEventListener('wheel', (ev: WheelEvent) => {
    if (!isLayoutScrollLocked())
      return;

    const host = document.querySelector('#app .hycad-page-grid-host');
    const canvas = findMainSheetCanvas();
    const target = ev.target;
    if (!(target instanceof Node))
      return;

    const inSheet = (canvas?.contains(target) ?? false)
      || (host?.contains(target) ?? false);
    if (!inSheet)
      return;

    // Ctrl+滚轮：让 page-viewport 处理整体视图缩放
    // 不阻止传播，让事件继续到 page-viewport 的监听器
    if (ev.ctrlKey) {
      // 只阻止 Univer 的默认缩放行为
      ev.preventDefault();
      // 不调用 stopPropagation()，让 page-viewport 能接收到事件
      return;
    }

    // 阻止普通滚轮滚动
    ev.preventDefault();
    ev.stopPropagation();
  }, { passive: false, capture: true });
}

function startScrollClampLoop(univerAPI: ReturnType<typeof FUniver.newAPI>): void {
  stopScrollClampLoop();
  const tick = (): void => {
    if (scrollLocked && isLayoutScrollLocked())
      clampViewportScrollOrigin(univerAPI);
    clampFrame = window.requestAnimationFrame(tick);
  };
  clampFrame = window.requestAnimationFrame(tick);
}

function stopScrollClampLoop(): void {
  if (clampFrame) {
    window.cancelAnimationFrame(clampFrame);
    clampFrame = 0;
  }
}

function setLayoutScrollLock(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
  lock: boolean,
): void {
  document.getElementById('app')?.classList.toggle(APP_LAYOUT_FIT_CLASS, lock);
  scrollLocked = lock;

  if (lock) {
    installLayoutWheelBlock();
    scheduleScrollBarVisibility(univerAPI, false);
    clampViewportScrollOrigin(univerAPI);
    startScrollClampLoop(univerAPI);
  } else {
    stopScrollClampLoop();
    scheduleScrollBarVisibility(univerAPI, true);
  }
}

/** @deprecated 用 setLayoutScrollLock */
export function setLayoutSheetScrollbarsVisible(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
  visible: boolean,
): void {
  setLayoutScrollLock(univerAPI, !visible);
}

export function resetLayoutSheetScrollbars(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
): void {
  window.clearTimeout(retryTimer);
  setLayoutScrollLock(univerAPI, false);
  scrollbarsHidden = false;
}

export { setLayoutScrollLock };
