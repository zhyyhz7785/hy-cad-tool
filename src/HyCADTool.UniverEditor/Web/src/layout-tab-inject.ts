import { applyRibbonTabVisual } from './ribbon-tab-style';
import { createElement } from 'react';
import { createRoot, type Root } from 'react-dom/client';

import { autoFitColWidths, autoFitRowHeights, getLastSnapshotDims } from './univer-bridge';
import {
  patchLayoutRibbonModel,
  PAPER_PRESETS,
  TEMPLATE_OPTIONS,
  type LayoutRibbonModelState,
} from './layout-ribbon-model';
import { LayoutRibbonPanel, type LayoutRibbonActions } from './layout-ribbon-panel';
import {
  setShowHeaders,
  setShowPaperBoundary,
  setShowRulers,
  syncPaperFromLayoutInputs,
  updateLayoutViewFromViewport,
  type HyCadViewportPayload,
} from './layout-view-state';

export type { HyCadViewportPayload };

/**
 * 布局 Tab（012 结构优先 + 内容驱动，口径 B）。
 *
 * 注入到 Univer Ribbon 顶栏：「数据」Tab 右侧，工具条显示在 Ribbon 第二行。
 */

type PostHostMessage = (payload: Record<string, unknown>) => void;

const ROOT_SELECTOR = '[data-u-comp="ribbon-header-menu"]';
const LAYOUT_TAB_ROOT_ATTR = 'data-hycad-layout-tab-root';
const LAYOUT_PANEL_ID = 'hycad-layout-ribbon-panel';
const INJECTED_FLAG = 'data-hycad-layout-injected';

const LAYOUT_TOOLBAR_MODE_CLASS = 'hycad-layout-toolbar-mode';
const LAYOUT_TABLIT_CLASS = 'hycad-layout-tablit';

let post: PostHostMessage | null = null;
let layoutActive = false;
const layoutTabActiveListeners = new Set<(active: boolean) => void>();

let layoutTabButton: HTMLButtonElement | null = null;
let layoutRibbonHost: HTMLElement | null = null;
let layoutRibbonRoot: Root | null = null;

function emitLayoutTabActive(): void {
  for (const fn of layoutTabActiveListeners)
    fn(layoutActive);
}

/** 布局 Tab 是否处于激活态（仅此时启用 U7 页面视图）。 */
export function isLayoutTabActive(): boolean {
  return layoutActive;
}

export function subscribeLayoutTabActive(listener: (active: boolean) => void): () => void {
  layoutTabActiveListeners.add(listener);
  listener(layoutActive);
  return () => layoutTabActiveListeners.delete(listener);
}

function sendLayoutOp(op: string, value = 0): void {
  post?.({ type: 'hyCadLayout', op, value });
}

function sendFileAction(action: string): void {
  post?.({ type: 'hyCadFileAction', action });
}

function formatMm(value: number): number {
  if (!Number.isFinite(value))
    return 0;
  return Math.round(value * 100) / 100;
}

function createLayoutActions(): LayoutRibbonActions {
  return {
    sendLayoutOp,
    sendFileAction,
    autoFitRowHeights,
    autoFitColWidths,
    postError: (message) => post?.({ type: 'error', message }),
    setShowRulers,
    setShowHeaders,
    setShowPaperBoundary,
    syncPaperFromLayoutInputs,
  };
}

function mountLayoutRibbonPanel(host: HTMLElement): void {
  if (layoutRibbonRoot)
    return;

  layoutRibbonRoot = createRoot(host);
  layoutRibbonRoot.render(createElement(LayoutRibbonPanel, { actions: createLayoutActions() }));
}

function buildRibbonPanelHost(): HTMLElement {
  const host = document.createElement('div');
  host.className = 'hycad-layout-ribbon-host';
  mountLayoutRibbonPanel(host);
  return host;
}

function findTabByLabel(tablist: Element, label: string): HTMLElement | null {
  for (const tab of tablist.querySelectorAll('[role="tab"]')) {
    if (tab instanceof HTMLElement && tab.textContent?.trim() === label)
      return tab;
  }
  return null;
}

function findUniverToolbarRow(headerMenu: HTMLElement): HTMLElement | null {
  const header = headerMenu.closest('header');
  if (!header)
    return null;

  const toolbar = header.querySelector('[role="toolbar"]');
  return toolbar instanceof HTMLElement ? toolbar : null;
}

function setLayoutTabSelected(selected: boolean): void {
  if (layoutTabButton)
    applyRibbonTabVisual(layoutTabButton, selected);
}

/** 布局 Tab 激活时压制 Univer 原生 Tab 高亮（React 仍保留 activatedTab，需 CSS 互斥）。 */
function setLayoutTablistLit(active: boolean): void {
  const headerMenu = document.querySelector(ROOT_SELECTOR);
  if (headerMenu instanceof HTMLElement)
    headerMenu.classList.toggle(LAYOUT_TABLIT_CLASS, active);
}

function setRibbonToolbarHidden(hidden: boolean): void {
  const headerMenu = document.querySelector(ROOT_SELECTOR);
  if (!(headerMenu instanceof HTMLElement))
    return;

  const toolbar = findUniverToolbarRow(headerMenu);
  if (!toolbar)
    return;

  toolbar.classList.toggle(LAYOUT_TOOLBAR_MODE_CLASS, hidden);
}

function activateLayoutTab(): void {
  if (!layoutRibbonHost)
    return;

  layoutActive = true;
  setLayoutTabSelected(true);
  setLayoutTablistLit(true);

  setRibbonToolbarHidden(true);
  document.getElementById(LAYOUT_PANEL_ID)?.classList.add('hycad-layout-ribbon-panel--active');
  emitLayoutTabActive();
}

function deactivateLayoutTab(): void {
  if (!layoutActive)
    return;

  layoutActive = false;
  setLayoutTabSelected(false);
  setLayoutTablistLit(false);

  setRibbonToolbarHidden(false);
  document.getElementById(LAYOUT_PANEL_ID)?.classList.remove('hycad-layout-ribbon-panel--active');
  emitLayoutTabActive();
}

function createLayoutTabRoot(): HTMLElement {
  const root = document.createElement('div');
  root.className = 'hycad-layout-tab-root';
  root.setAttribute(LAYOUT_TAB_ROOT_ATTR, 'true');

  const button = document.createElement('button');
  button.type = 'button';
  button.role = 'tab';
  button.textContent = '布局';
  button.title = '纸张 L0 / 内容驱动 / 行列合并 / 落图拾取';
  applyRibbonTabVisual(button, false);
  button.addEventListener('click', (event) => {
    event.preventDefault();
    event.stopPropagation();
    activateLayoutTab();
  });

  layoutTabButton = button;
  root.appendChild(button);
  return root;
}

function injectLayoutTab(): boolean {
  const headerMenu = document.querySelector(ROOT_SELECTOR);
  if (!headerMenu || !(headerMenu instanceof HTMLElement))
    return false;
  if (headerMenu.getAttribute(INJECTED_FLAG) === 'true')
    return true;

  const tablist = headerMenu.querySelector('[role="tablist"]');
  if (!tablist)
    return false;

  const toolbarRow = findUniverToolbarRow(headerMenu);
  if (!toolbarRow)
    return false;

  layoutRibbonHost = buildRibbonPanelHost();
  toolbarRow.appendChild(layoutRibbonHost);

  const dataTab = findTabByLabel(tablist, '数据');
  const tabRoot = createLayoutTabRoot();
  if (dataTab?.parentElement === tablist)
    tablist.insertBefore(tabRoot, dataTab.nextSibling);
  else
    tablist.appendChild(tabRoot);

  tablist.addEventListener('click', (event) => {
    const target = event.target as HTMLElement | null;
    if (!target || target.closest(`[${LAYOUT_TAB_ROOT_ATTR}]`))
      return;
    if (target.closest('[role="tab"]') && layoutActive)
      deactivateLayoutTab();
  }, true);

  headerMenu.setAttribute(INJECTED_FLAG, 'true');
  return true;
}

/** 安装布局 Tab（幂等）。 */
export function installHyCadLayoutTab(postHostMessage: PostHostMessage): void {
  post = postHostMessage;
  if (document.getElementById(LAYOUT_PANEL_ID))
    return;

  if (injectLayoutTab())
    return;

  const observer = new MutationObserver(() => {
    if (injectLayoutTab())
      observer.disconnect();
  });

  observer.observe(document.body, { childList: true, subtree: true });

  window.setTimeout(() => {
    if (injectLayoutTab())
      observer.disconnect();
  }, 3000);
}

/** 由 main.ts 在 SelectionChanged 时调用：刷新行高/列宽 mm 框为当前选区真值。 */
export function updateLayoutTabSelection(
  startRow: number,
  startCol: number,
  endRow: number,
  endCol: number,
): void {
  const dims = getLastSnapshotDims();
  if (!dims)
    return;

  const r = Math.min(startRow, endRow);
  const c = Math.min(startCol, endCol);

  const patch: { rowHeightMm?: number; colWidthMm?: number } = {};
  if (dims.rowHeightsMm.length > r && r >= 0)
    patch.rowHeightMm = formatMm(dims.rowHeightsMm[r]);
  if (dims.colWidthsMm.length > c && c >= 0)
    patch.colWidthMm = formatMm(dims.colWidthsMm[c]);
  patchLayoutRibbonModel(patch);
}

/** 由 main.ts 在收到宿主 setScale 时调用：回填比例框。 */
export function updateLayoutTabScale(scale: number): void {
  if (!(scale > 0))
    return;
  patchLayoutRibbonModel({ scale: formatMm(scale) });
}

/** 由 main.ts 在收到宿主 setViewport 时调用：回填纸张/行列/模板初值。 */
export function updateLayoutTabViewport(payload: HyCadViewportPayload): void {
  const patch: Partial<LayoutRibbonModelState> = {};

  if (typeof payload.paperPresetIndex === 'number') {
    patch.paperPresetIndex = Math.max(0, Math.min(PAPER_PRESETS.length - 1, payload.paperPresetIndex));
  }
  if (typeof payload.orientation === 'number') {
    patch.orientation = payload.orientation > 0 ? 1 : 0;
  }
  if (typeof payload.targetWidthMm === 'number')
    patch.targetWidthMm = formatMm(payload.targetWidthMm);
  if (typeof payload.marginMm === 'number')
    patch.marginMm = formatMm(payload.marginMm);
  if (typeof payload.rowCount === 'number')
    patch.rowCount = Math.round(payload.rowCount);
  if (typeof payload.colCount === 'number')
    patch.colCount = Math.round(payload.colCount);
  if (typeof payload.templateIndex === 'number') {
    patch.templateIndex = Math.max(0, Math.min(TEMPLATE_OPTIONS.length - 1, payload.templateIndex));
  }
  if (typeof payload.structureMode === 'boolean')
    patch.structureMode = payload.structureMode;

  patchLayoutRibbonModel(patch);
  updateLayoutViewFromViewport(payload);
}

/** 由 main.ts 在收到宿主 setStructureMode 时调用。 */
export function updateLayoutTabStructureMode(on: boolean): void {
  patchLayoutRibbonModel({ structureMode: on });
}
