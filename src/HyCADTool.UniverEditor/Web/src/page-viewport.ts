import type { FUniver } from '@univerjs/core/facade';

import { SetZoomRatioCommand } from '@univerjs/sheets-ui';

import { DISPLAY_PX_PER_MM, mmToColDisplayPx, mmToRowDisplayPx } from './mm-display';

import { getLayoutViewState, subscribeLayoutViewState } from './layout-view-state';

import { subscribeLayoutRibbonModel } from './layout-ribbon-model';

import { isLayoutTabActive, subscribeLayoutTabActive } from './layout-tab-inject';

import {

  applyPagePreviewScaleStep,

  getPagePreviewScalePxPerMm,

  resetPagePreviewScaleFromFit,

} from './page-preview-scale';

import { resolveSheetSizeMm } from './paper-sheet';

import { subscribeSnapshotDims } from './univer-bridge';

import { findScrollElement, readZoom } from './univer-viewport';
import { formatMarginDataset } from './page-margins';
import { DEFAULT_CANVAS_SHEET_GAP_MM } from './page-canvas-gap';
import { resetLayoutSheetScrollbars, setLayoutScrollLock } from './layout-sheet-scrollbars';
import {
  computeLayoutGeometry,
  resolveCanvasContentSize,
  resolveLayoutPageNodes,
  type LayoutGeometry,
  type LayoutPageNodes,
} from './layout-sheet-geometry';
import {
  applyGridFitStep,
  correctLastGridTracks,
  invalidateGridFit,
  restoreGridBaseline,
  type GridFitResult,
} from './layout-grid-fit';

/** @deprecated 用 layout-view-state.canvasSheetGapMm */
export const CANVAS_SHEET_GAP_MM = DEFAULT_CANVAS_SHEET_GAP_MM;

const PAGE_HOST_CLASS = 'hycad-page-grid-host';
const PAGE_SHEET_CLASS = 'hycad-page-sheet-box';
const PAGE_CELL_VIEWPORT_CLASS = 'hycad-cell-viewport';
const ZOOM_EPSILON = 0.004;
const MIN_PREVIEW_SCALE = 0.1;
const MAX_PREVIEW_SCALE = 5.0;
/** 进入布局/快照重建后的收敛重试上限（rAF 帧数），测量稳定即提前停止。 */
const SETTLE_MAX_FRAMES = 30;
/** cellViewport 裁切余量：网格恰好贴住 CellRegion 边缘时，1px 封口线不被 overflow 吃掉。 */
const CLIP_SLACK_PX = 2;

let lastZoom = 0;
let orchestrating = false;
/** 编排器正在写入 SetZoomRatio，用于区分 footer slider 等外部 zoom。 */
let orchestratorApplyingZoom = false;
let lastSheetBoxSig = '';
let wheelInstalled = false;
let pageModeActive = false;
let zoomBeforeLayout = 1;
let settleFrame = 0;
let lastSettleSize = '';

function resolveNodes(): LayoutPageNodes | null {
  return resolveLayoutPageNodes();
}

function resolveCanvasSheetGapMm(): number {
  return getLayoutViewState().canvasSheetGapMm;
}

export function computeMaxPxPerMm(
  nodes: LayoutPageNodes,
  sheetWidthMm: number,
  sheetHeightMm: number,
  gapMm = resolveCanvasSheetGapMm(),
): number {
  const canvas = resolveCanvasContentSize(nodes);
  if (canvas.width <= 0 || canvas.height <= 0 || sheetWidthMm <= 0 || sheetHeightMm <= 0)
    return DISPLAY_PX_PER_MM;

  const maxFromW = canvas.width / (sheetWidthMm + 2 * gapMm);
  const maxFromH = canvas.height / (sheetHeightMm + 2 * gapMm);
  return Math.max(MIN_PREVIEW_SCALE, Math.min(MAX_PREVIEW_SCALE, maxFromW, maxFromH));
}

export function clampPagePreviewScaleToCanvas(
  ppm: number,
  nodes: LayoutPageNodes,
  sheetWidthMm: number,
  sheetHeightMm: number,
): number {
  const maxPpm = computeMaxPxPerMm(nodes, sheetWidthMm, sheetHeightMm);
  return Math.max(MIN_PREVIEW_SCALE, Math.min(maxPpm, ppm));
}

function clearPageStyles(nodes: LayoutPageNodes): void {
  nodes.gridHost.classList.remove(PAGE_HOST_CLASS);
  nodes.gridHost.style.removeProperty('overflow');

  nodes.sheetBox.classList.remove(PAGE_SHEET_CLASS);

  const box = nodes.sheetBox.style;
  box.removeProperty('flex');
  box.removeProperty('width');
  box.removeProperty('height');
  box.removeProperty('max-width');
  box.removeProperty('max-height');
  box.removeProperty('margin-left');
  box.removeProperty('margin-top');
  box.removeProperty('padding');
  box.removeProperty('justify-self');
  box.removeProperty('align-self');
  box.removeProperty('box-shadow');
  box.removeProperty('border');
  box.removeProperty('background');
  box.removeProperty('overflow');

  delete nodes.sheetBox.dataset.hycadPaperMm;
  delete nodes.sheetBox.dataset.hycadSheetMm;
  delete nodes.sheetBox.dataset.hycadMarginPx;
  delete nodes.sheetBox.dataset.hycadGridCount;

  const cell = nodes.cellViewport;
  cell.classList.remove(PAGE_CELL_VIEWPORT_CLASS);
  cell.style.removeProperty('flex');
  cell.style.removeProperty('width');
  cell.style.removeProperty('height');
  cell.style.removeProperty('margin-left');
  cell.style.removeProperty('margin-top');
  cell.style.removeProperty('overflow');
}

function applyZoom(univerAPI: ReturnType<typeof FUniver.newAPI>, zoom: number, force = false): void {
  const wb = univerAPI.getActiveWorkbook?.();
  const sheet = wb?.getActiveSheet?.();
  if (!wb || !sheet)
    return;

  const quantized = Math.round(zoom * 100) / 100;
  if (quantized <= 0)
    return;

  // 与 Univer 真实 zoom 比较，而非模块缓存 lastZoom：快照加载会 dispose+createWorkbook,
  // 真实 zoom 被重置为 1 而缓存仍是旧值——按缓存比较会吞掉必要的缩放命令，
  // 网格以 zoom 1 渲染直接溢出纸张。getZoom 不可用时才退回 lastZoom。
  let actual = lastZoom;
  try {
    const z = (sheet as unknown as { getZoom?: () => number }).getZoom?.();
    if (typeof z === 'number' && z > 0)
      actual = z;
  } catch {
    // ignore
  }

  if (Math.abs(quantized - actual) < (force ? 0.0001 : ZOOM_EPSILON))
    return;

  lastZoom = quantized;
  orchestratorApplyingZoom = true;
  try {
    void univerAPI.executeCommand(SetZoomRatioCommand.id, {
      unitId: wb.getId(),
      subUnitId: sheet.getSheetId(),
      zoomRatio: quantized,
    });
  } finally {
    window.setTimeout(() => {
      orchestratorApplyingZoom = false;
    }, 0);
  }
}

function clearPageViewport(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
  nodes: LayoutPageNodes,
  restoreZoom = false,
): void {
  clearPageStyles(nodes);
  nodes.gridHost.classList.remove(PAGE_HOST_CLASS);
  lastSheetBoxSig = '';
  lastLayoutGeometrySig = '';

  if (restoreZoom) {
    lastZoom = 0;
    applyZoom(univerAPI, zoomBeforeLayout > 0 ? zoomBeforeLayout : 1);
    resetLayoutSheetScrollbars(univerAPI);
  }
}

function isLayoutPageViewActive(): boolean {
  return isLayoutTabActive() && getLayoutViewState().showPaperBoundary;
}

function computeFitPxPerMm(nodes: LayoutPageNodes, sheetWidthMm: number, sheetHeightMm: number): number {
  const maxPpm = computeMaxPxPerMm(nodes, sheetWidthMm, sheetHeightMm);
  return Math.min(DISPLAY_PX_PER_MM, maxPpm);
}

function resetSheetScroll(): void {
  const scroller = findScrollElement();
  if (!scroller)
    return;
  if (scroller.scrollLeft !== 0)
    scroller.scrollLeft = 0;
  if (scroller.scrollTop !== 0)
    scroller.scrollTop = 0;
}

function applySheetBoxSizing(
  nodes: LayoutPageNodes,
  geometry: LayoutGeometry,
  sheetWidthMm: number,
  sheetHeightMm: number,
): boolean {
  const paper = geometry.paper;
  const bleed = geometry.headerBleed;
  const sig = `${paper.boxW}x${paper.boxH}|${paper.gapPx}|${paper.padTop},${paper.padRight},${paper.padBottom},${paper.padLeft}|${bleed.hostWidthPx}x${bleed.hostHeightPx}|${bleed.bleedLeftPx},${bleed.bleedTopPx}|${Math.round(sheetWidthMm)}x${Math.round(sheetHeightMm)}`;
  if (sig === lastSheetBoxSig)
    return true;
  lastSheetBoxSig = sig;

  nodes.gridHost.classList.add(PAGE_HOST_CLASS);
  nodes.gridHost.style.overflow = 'hidden';

  nodes.sheetBox.classList.add(PAGE_SHEET_CLASS);
  nodes.sheetBox.dataset.hycadSheetMm = `${Math.round(sheetWidthMm)}x${Math.round(sheetHeightMm)}`;
  nodes.sheetBox.dataset.hycadPaperMm = nodes.sheetBox.dataset.hycadSheetMm;
  nodes.sheetBox.dataset.hycadMarginPx = formatMarginDataset({
    padLeft: paper.padLeft,
    padTop: paper.padTop,
    padRight: paper.padRight,
    padBottom: paper.padBottom,
  });

  const box = nodes.sheetBox.style;
  box.flex = 'none';
  box.width = `${paper.boxW}px`;
  box.height = `${paper.boxH}px`;
  box.maxWidth = 'none';
  box.maxHeight = 'none';
  box.marginLeft = `${paper.gapPx}px`;
  box.marginTop = `${paper.gapPx}px`;
  box.padding = `${paper.padTop}px ${paper.padRight}px ${paper.padBottom}px ${paper.padLeft}px`;
  box.justifySelf = 'start';
  box.alignSelf = 'start';
  box.background = '#ffffff';
  box.boxShadow = '0 2px 12px rgba(15, 23, 42, 0.18)';
  box.border = '1px solid #cbd5e1';
  box.overflow = 'hidden';

  const viewport = nodes.cellViewport;
  viewport.classList.add(PAGE_CELL_VIEWPORT_CLASS);
  viewport.style.flex = 'none';
  viewport.style.width = `${bleed.hostWidthPx + CLIP_SLACK_PX}px`;
  viewport.style.height = `${bleed.hostHeightPx + CLIP_SLACK_PX}px`;
  viewport.style.marginLeft = `${-bleed.bleedLeftPx}px`;
  viewport.style.marginTop = `${-bleed.bleedTopPx}px`;
  viewport.style.overflow = 'hidden';

  return true;
}

/** 网格 zoom 严格跟随图纸 appliedPpm，不与 cellArea/colSum 反推脱钩。 */
function computePaperLockedZoom(appliedPpm: number): number {
  let zoom = appliedPpm / DISPLAY_PX_PER_MM;
  zoom = Math.round(zoom * 100) / 100;
  if (zoom <= 0)
    zoom = 0.01;
  return zoom;
}

/**
 * 量化 zoom 以"不超过 CellRegion"为准（floor）：ceil 会让网格溢出裁切框最多
 * colSum×0.01≈13px，最后一行/列被裁半、右/下封口线消失。floor 宁留 ≤1 个量化步
 * 的缝隙，配合 cellViewport 的 CLIP_SLACK_PX 余量，封口线始终可见。
 */
function snapZoomToGridEdge(
  zoomPaper: number,
  fit: GridFitResult,
  cellRegion: LayoutGeometry['cellRegion'],
): number {
  const fitZoom = Math.floor(Math.min(
    cellRegion.widthPx / fit.colSumPx,
    cellRegion.heightPx / fit.rowSumPx,
  ) * 100) / 100;
  return Math.max(0.01, Math.min(zoomPaper, fitZoom));
}

function clampPreviewScaleAbsolute(ppm: number): number {
  return Math.max(MIN_PREVIEW_SCALE, Math.min(MAX_PREVIEW_SCALE, ppm));
}

function layoutGeometrySig(state: ReturnType<typeof getLayoutViewState>): string {
  const m = state.pageMargins;
  return [
    state.paperPresetIndex,
    state.orientation,
    Math.round(state.targetWidthMm),
    m.top,
    m.bottom,
    m.left,
    m.right,
    state.canvasSheetGapMm,
    state.showPaperBoundary,
  ].join('|');
}

let lastLayoutGeometrySig = '';

function startSettleConvergence(univerAPI: ReturnType<typeof FUniver.newAPI>): void {
  cancelSettleConvergence();
  lastSettleSize = '';
  let frame = 0;

  const tick = (): void => {
    settleFrame = 0;
    if (!isLayoutPageViewActive()) {
      cancelSettleConvergence();
      return;
    }

    const nodes = resolveNodes();
    const size = nodes ? `${nodes.gridHost.clientWidth}x${nodes.gridHost.clientHeight}` : '';
    const stable = size !== '' && size === lastSettleSize;
    lastSettleSize = size;

    applyPageViewport(univerAPI, true);

    frame++;
    if (!stable && frame < SETTLE_MAX_FRAMES)
      settleFrame = window.requestAnimationFrame(tick);
  };

  settleFrame = window.requestAnimationFrame(tick);
}

function cancelSettleConvergence(): void {
  if (settleFrame) {
    window.cancelAnimationFrame(settleFrame);
    settleFrame = 0;
  }
}

function applyPageViewport(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
  resetScale = false,
): void {
  if (orchestrating)
    return;
  orchestrating = true;
  try {
    applyPageViewportInner(univerAPI, resetScale);
  } finally {
    orchestrating = false;
  }
}

function applyPageViewportInner(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
  resetScale = false,
): void {
  const nodes = resolveNodes();
  if (!nodes)
    return;

  const layoutActive = isLayoutPageViewActive();

  if (!layoutActive) {
    if (pageModeActive) {
      cancelSettleConvergence();
      restoreGridBaseline(univerAPI);
      clearPageViewport(univerAPI, nodes, true);
      pageModeActive = false;
    } else if (nodes.gridHost.classList.contains(PAGE_HOST_CLASS)) {
      restoreGridBaseline(univerAPI);
      clearPageViewport(univerAPI, nodes, false);
    }
    return;
  }

  if (!pageModeActive) {
    zoomBeforeLayout = readZoom(univerAPI);
    pageModeActive = true;
    startSettleConvergence(univerAPI);
  }

  const state = getLayoutViewState();
  const sheet = resolveSheetSizeMm(state.paperPresetIndex, state.orientation);
  if (sheet.widthMm <= 0 || sheet.heightMm <= 0)
    return;

  let appliedPpm: number;
  if (resetScale || getPagePreviewScalePxPerMm() <= 0) {
    appliedPpm = computeFitPxPerMm(nodes, sheet.widthMm, sheet.heightMm);
    resetPagePreviewScaleFromFit(appliedPpm);
  } else {
    appliedPpm = clampPreviewScaleAbsolute(getPagePreviewScalePxPerMm());
  }

  const geometry = computeLayoutGeometry(
    nodes,
    sheet.widthMm,
    sheet.heightMm,
    appliedPpm,
    state.pageMargins,
    resolveCanvasSheetGapMm(),
    state.showHeaders,
  );
  if (!geometry)
    return;

  if (!applySheetBoxSizing(nodes, geometry, sheet.widthMm, sheet.heightMm))
    return;

  const gridReseed = resetScale;
  let fit = applyGridFitStep(univerAPI, { reseed: gridReseed });
  if (!fit)
    return;

  const m = state.pageMargins;
  const innerWmm = Math.max(1, sheet.widthMm - m.left - m.right);
  const innerHmm = Math.max(1, sheet.heightMm - m.top - m.bottom);
  const baselineColSumPx = mmToColDisplayPx(innerWmm);
  const baselineRowSumPx = mmToRowDisplayPx(innerHmm);

  if (
    gridReseed
    && (Math.abs(fit.colSumPx - baselineColSumPx) > 1 || Math.abs(fit.rowSumPx - baselineRowSumPx) > 1)
  ) {
    const corrected = correctLastGridTracks(univerAPI, {
      targetColSumPx: baselineColSumPx,
      targetRowSumPx: baselineRowSumPx,
    });
    if (corrected)
      fit = corrected;
  }

  const zoomPaper = computePaperLockedZoom(appliedPpm);
  const zoom = snapZoomToGridEdge(zoomPaper, fit, geometry.cellRegion);

  applyZoom(univerAPI, zoom, true);
  resetSheetScroll();
  setLayoutScrollLock(univerAPI, true);

  const activeSheet = univerAPI.getActiveWorkbook?.()?.getActiveSheet?.() as { refreshCanvas?: () => void } | null | undefined;
  activeSheet?.refreshCanvas?.();
}

function installPageWheelZoom(univerAPI: ReturnType<typeof FUniver.newAPI>): () => void {
  if (wheelInstalled)
    return () => {};

  wheelInstalled = true;

  const onWheel = (ev: WheelEvent): void => {
    if (!ev.ctrlKey)
      return;
    if (!isLayoutTabActive() || !getLayoutViewState().showPaperBoundary)
      return;

    const host = document.querySelector('#app .hycad-page-grid-host');
    if (!host || !(ev.target instanceof Node) || !host.contains(ev.target))
      return;

    ev.preventDefault();
    const step = -(ev.deltaY || 0) / 100;
    applyPagePreviewScaleStep(step);
    applyPageViewport(univerAPI, false);
  };

  window.addEventListener('wheel', onWheel, { passive: false });

  return () => {
    wheelInstalled = false;
    window.removeEventListener('wheel', onWheel);
  };
}

function installLayoutZoomGuard(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
): () => void {
  let debounceTimer: number | undefined;

  // onCommandExecuted 返回 IDisposable（非函数），teardown 须调 dispose()
  const disposable = univerAPI.onCommandExecuted?.((command) => {
    if (command.id !== SetZoomRatioCommand.id)
      return;
    if (orchestratorApplyingZoom || orchestrating)
      return;
    if (!isLayoutPageViewActive())
      return;

    window.clearTimeout(debounceTimer);
    debounceTimer = window.setTimeout(() => {
      if (isLayoutPageViewActive())
        applyPageViewport(univerAPI, false);
    }, 0);
  });

  return () => {
    window.clearTimeout(debounceTimer);
    disposable?.dispose();
  };
}

export function installPageViewport(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
): () => void {
  const schedule = (resetScale = false): void => applyPageViewport(univerAPI, resetScale);

  const unsubLayout = subscribeLayoutViewState((view) => {
    if (!isLayoutPageViewActive() && !pageModeActive)
      return;
    const geomSig = layoutGeometrySig(view);
    const resetScale = geomSig !== lastLayoutGeometrySig;
    lastLayoutGeometrySig = geomSig;
    schedule(resetScale);
  });
  const unsubRibbon = subscribeLayoutRibbonModel(() => {
    if (isLayoutPageViewActive())
      schedule(true);
  });
  const unsubSnapshot = subscribeSnapshotDims(() => {
    // 工作簿已 dispose+重建：无条件作废 grid-fit 与纸盒样式缓存（含布局未激活时，
    // 否则稍后切回布局 Tab 会因 sig 陈旧匹配跳过重写，快照原始尺寸溢出纸张）。
    invalidateGridFit();
    lastSheetBoxSig = '';
    if (isLayoutPageViewActive()) {
      schedule(true); // reset：重算 fit ppm + 按快照 reseed 行列数 + 强制重写格子
      startSettleConvergence(univerAPI); // 重建后 DOM/canvas 可能异步挂载，rAF 收敛兜底
    }
  });
  const unsubTab = subscribeLayoutTabActive(() => schedule(true));
  const unsubWheel = installPageWheelZoom(univerAPI);
  const unsubZoomGuard = installLayoutZoomGuard(univerAPI);

  let resizeTimer: number | undefined;
  const onResize = (): void => {
    window.clearTimeout(resizeTimer);
    resizeTimer = window.setTimeout(() => {
      if (isLayoutPageViewActive())
        applyPageViewport(univerAPI, true);
    }, 120);
  };
  window.addEventListener('resize', onResize);

  return () => {
    unsubLayout();
    unsubRibbon();
    unsubSnapshot();
    unsubTab();
    unsubWheel();
    unsubZoomGuard();
    window.clearTimeout(resizeTimer);
    window.removeEventListener('resize', onResize);
    cancelSettleConvergence();

    const nodes = resolveNodes();
    if (nodes)
      clearPageViewport(univerAPI, nodes, false);
    pageModeActive = false;
  };
}
