import type { FUniver } from '@univerjs/core/facade';
import { IRenderManagerService, SHEET_VIEWPORT_KEY } from '@univerjs/engine-render';

/** 布局页（纸张边界）隐藏 Univer 原生 canvas 滚动条；切回其它 Tab 恢复。 */

interface ScrollBarLike {
  enableHorizontal?: boolean;
  enableVertical?: boolean;
}

interface ViewportLike {
  getScrollBar?: () => ScrollBarLike | null;
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
let savedEnableHorizontal = true;
let savedEnableVertical = true;
let retryTimer: number | undefined;

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

function resolveMainScrollBar(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
): { scrollBar: ScrollBarLike; scene: SceneLike } | null {
  const render = resolveActiveRender(univerAPI);
  const scene = render?.scene;
  if (!scene?.getViewport)
    return null;

  const viewport = scene.getViewport(SHEET_VIEWPORT_KEY.VIEW_MAIN);
  const scrollBar = viewport?.getScrollBar?.();
  if (!scrollBar)
    return null;

  return { scrollBar, scene };
}

function refreshAfterScrollChange(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
  scene: SceneLike,
): void {
  scene.makeDirty?.(true);
  const sheet = univerAPI.getActiveWorkbook?.()?.getActiveSheet?.() as { refreshCanvas?: () => void } | null | undefined;
  sheet?.refreshCanvas?.();
}

function applyScrollBarVisibility(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
  visible: boolean,
): boolean {
  const resolved = resolveMainScrollBar(univerAPI);
  if (!resolved)
    return false;

  const { scrollBar, scene } = resolved;

  if (!visible) {
    if (!scrollbarsHidden) {
      savedEnableHorizontal = scrollBar.enableHorizontal !== false;
      savedEnableVertical = scrollBar.enableVertical !== false;
      scrollbarsHidden = true;
    }
    scrollBar.enableHorizontal = false;
    scrollBar.enableVertical = false;
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

/** 布局页隐藏/恢复 Univer 主视口滚动条（viewMain）。 */
export function setLayoutSheetScrollbarsVisible(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
  visible: boolean,
): void {
  document.getElementById('app')?.classList.toggle(APP_LAYOUT_FIT_CLASS, !visible);
  scheduleScrollBarVisibility(univerAPI, visible);
}

export function resetLayoutSheetScrollbars(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
): void {
  window.clearTimeout(retryTimer);
  document.getElementById('app')?.classList.remove(APP_LAYOUT_FIT_CLASS);
  applyScrollBarVisibility(univerAPI, true);
  scrollbarsHidden = false;
}
