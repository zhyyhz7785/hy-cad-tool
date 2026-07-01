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

  setPagePreviewScalePxPerMm,

} from './page-preview-scale';

import { resolveSheetSizeMm } from './paper-sheet';

import { subscribeSnapshotDims } from './univer-bridge';

import { findScrollElement, readZoom } from './univer-viewport';
import {
  formatMarginDataset,
  marginsToPaddingPx,
  type PageMarginsMm,
} from './page-margins';
import { DEFAULT_CANVAS_SHEET_GAP_MM } from './page-canvas-gap';
import { GRID_COL_HEADER_H, GRID_ROW_HEADER_W } from './sheet-headers';
import { resetLayoutSheetScrollbars, setLayoutScrollLock } from './layout-sheet-scrollbars';
import {
  applyGridFitStep,
  correctLastGridTracks,
  restoreGridBaseline,
} from './layout-grid-fit';

/** @deprecated 用 layout-view-state.canvasSheetGapMm */
export const CANVAS_SHEET_GAP_MM = DEFAULT_CANVAS_SHEET_GAP_MM;

const PAGE_HOST_CLASS = 'hycad-page-grid-host';
const PAGE_SHEET_CLASS = 'hycad-page-sheet-box';
const ZOOM_EPSILON = 0.004;
const MIN_PREVIEW_SCALE = 0.1;
const MAX_PREVIEW_SCALE = 5.0;

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

export interface SheetBoxLayout {
  boxW: number;
  boxH: number;
  maxBoxW: number;
  maxBoxH: number;
  gapPx: number;
  padLeft: number;
  padTop: number;
  padRight: number;
  padBottom: number;
  contentW: number;
  contentH: number;
  headerRowPx: number;
  headerColPx: number;
  /** 单元格区目标宽（content − 行头，zoom=1 基线 px 总和应 × zoom 贴齐） */
  cellAreaW: number;
  cellAreaH: number;
}

interface PageNodes {
  gridHost: HTMLElement;
  sheetBox: HTMLElement;
}

function resolveNodes(): PageNodes | null {
  const content = document.querySelector('#app [data-range-selector]');
  if (!(content instanceof HTMLElement))
    return null;

  const sheetBox = content.parentElement;
  if (!(sheetBox instanceof HTMLElement))
    return null;

  const gridHost = sheetBox.parentElement;
  if (!(gridHost instanceof HTMLElement))
    return null;

  return {
    gridHost,
    sheetBox,
  };
}

function resolveCanvasContentSize(nodes: PageNodes): { width: number; height: number } {
  return {
    width: Math.max(0, nodes.gridHost.clientWidth),
    height: Math.max(0, nodes.gridHost.clientHeight),
  };
}

function resolveCanvasSheetGapMm(): number {
  return getLayoutViewState().canvasSheetGapMm;
}

export function computeMaxPxPerMm(
  nodes: PageNodes,
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
  nodes: PageNodes,
  sheetWidthMm: number,
  sheetHeightMm: number,
): number {
  const maxPpm = computeMaxPxPerMm(nodes, sheetWidthMm, sheetHeightMm);
  return Math.max(MIN_PREVIEW_SCALE, Math.min(maxPpm, ppm));
}

function clearPageStyles(nodes: PageNodes): void {
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
}

function applyZoom(univerAPI: ReturnType<typeof FUniver.newAPI>, zoom: number, force = false): void {
  if (!force && Math.abs(zoom - lastZoom) < ZOOM_EPSILON)
    return;

  const wb = univerAPI.getActiveWorkbook?.();
  const sheet = wb?.getActiveSheet?.();
  if (!wb || !sheet)
    return;

  const quantized = Math.round(zoom * 100) / 100;
  if (force && Math.abs(quantized - lastZoom) < 0.0001)
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
  nodes: PageNodes,
  restoreZoom = false,
): void {
  clearPageStyles(nodes);
  nodes.gridHost.classList.remove(PAGE_HOST_CLASS);
  lastSheetBoxSig = '';

  if (restoreZoom) {
    lastZoom = 0;
    applyZoom(univerAPI, zoomBeforeLayout > 0 ? zoomBeforeLayout : 1);
    resetLayoutSheetScrollbars(univerAPI);
  }
}

function isLayoutPageViewActive(): boolean {
  return isLayoutTabActive() && getLayoutViewState().showPaperBoundary;
}

function computeFitPxPerMm(nodes: PageNodes, sheetWidthMm: number, sheetHeightMm: number): number {
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

function computeSheetBoxLayout(
  nodes: PageNodes,
  sheetWidthMm: number,
  sheetHeightMm: number,
  ppm: number,
  pageMargins: PageMarginsMm,
  showHeaders: boolean,
): SheetBoxLayout | null {
  const canvas = resolveCanvasContentSize(nodes);
  const gapPx = Math.round(resolveCanvasSheetGapMm() * ppm);
  const maxBoxW = Math.max(0, canvas.width - 2 * gapPx);
  const maxBoxH = Math.max(0, canvas.height - 2 * gapPx);

  const boxW = Math.min(Math.round(sheetWidthMm * ppm), maxBoxW);
  const boxH = Math.min(Math.round(sheetHeightMm * ppm), maxBoxH);

  if (boxW <= 0 || boxH <= 0)
    return null;

  const pad = marginsToPaddingPx(pageMargins, ppm);
  const maxPadLeft = Math.max(0, Math.floor(boxW / 2) - 1);
  const maxPadRight = Math.max(0, Math.floor(boxW / 2) - 1);
  const maxPadTop = Math.max(0, Math.floor(boxH / 2) - 1);
  const maxPadBottom = Math.max(0, Math.floor(boxH / 2) - 1);

  const headerRowPx = showHeaders ? GRID_ROW_HEADER_W : 0;
  const headerColPx = showHeaders ? GRID_COL_HEADER_H : 0;

  const padLeft = Math.min(Math.max(0, Math.round(pad.padLeft - headerRowPx)), maxPadLeft);
  const padTop = Math.min(Math.max(0, Math.round(pad.padTop - headerColPx)), maxPadTop);
  const padRight = Math.min(pad.padRight, maxPadRight);
  const padBottom = Math.min(pad.padBottom, maxPadBottom);

  const contentW = Math.max(0, boxW - padLeft - padRight);
  const contentH = Math.max(0, boxH - padTop - padBottom);
  const cellAreaW = Math.max(1, contentW - headerRowPx);
  const cellAreaH = Math.max(1, contentH - headerColPx);

  return {
    boxW,
    boxH,
    maxBoxW,
    maxBoxH,
    gapPx,
    padLeft,
    padTop,
    padRight,
    padBottom,
    contentW,
    contentH,
    headerRowPx,
    headerColPx,
    cellAreaW,
    cellAreaH,
  };
}

function applySheetBoxSizing(
  nodes: PageNodes,
  layout: SheetBoxLayout,
  sheetWidthMm: number,
  sheetHeightMm: number,
): boolean {
  const sig = `${layout.boxW}x${layout.boxH}|${layout.gapPx}|${layout.padTop},${layout.padRight},${layout.padBottom},${layout.padLeft}|${Math.round(sheetWidthMm)}x${Math.round(sheetHeightMm)}`;
  if (sig === lastSheetBoxSig)
    return true;
  lastSheetBoxSig = sig;

  nodes.gridHost.classList.add(PAGE_HOST_CLASS);
  nodes.gridHost.style.overflow = 'hidden';

  nodes.sheetBox.classList.add(PAGE_SHEET_CLASS);
  nodes.sheetBox.dataset.hycadSheetMm = `${Math.round(sheetWidthMm)}x${Math.round(sheetHeightMm)}`;
  nodes.sheetBox.dataset.hycadPaperMm = nodes.sheetBox.dataset.hycadSheetMm;
  nodes.sheetBox.dataset.hycadMarginPx = formatMarginDataset({
    padLeft: layout.padLeft,
    padTop: layout.padTop,
    padRight: layout.padRight,
    padBottom: layout.padBottom,
  });

  const box = nodes.sheetBox.style;
  box.flex = 'none';
  box.width = `${layout.boxW}px`;
  box.height = `${layout.boxH}px`;
  box.maxWidth = `${layout.maxBoxW}px`;
  box.maxHeight = `${layout.maxBoxH}px`;
  box.marginLeft = `${layout.gapPx}px`;
  box.marginTop = `${layout.gapPx}px`;
  box.padding = `${layout.padTop}px ${layout.padRight}px ${layout.padBottom}px ${layout.padLeft}px`;
  box.justifySelf = 'start';
  box.alignSelf = 'start';
  box.background = '#ffffff';
  box.boxShadow = '0 2px 12px rgba(15, 23, 42, 0.18)';
  box.border = '1px solid #cbd5e1';
  box.overflow = 'hidden';

  return true;
}

function computeLayoutZoom(
  layout: SheetBoxLayout,
  colSumPx: number,
  rowSumPx: number,
): number {
  if (colSumPx <= 0 || rowSumPx <= 0)
    return 0.01;

  const zoomW = layout.cellAreaW / colSumPx;
  const zoomH = layout.cellAreaH / rowSumPx;
  let zoom = Math.min(zoomW, zoomH);
  zoom = Math.round(zoom * 100) / 100;
  if (zoom <= 0)
    zoom = 0.01;
  return zoom;
}

const SETTLE_MAX_FRAMES = 30;

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
    appliedPpm = clampPagePreviewScaleToCanvas(
      getPagePreviewScalePxPerMm(),
      nodes,
      sheet.widthMm,
      sheet.heightMm,
    );
    if (Math.abs(appliedPpm - getPagePreviewScalePxPerMm()) > 0.0001)
      setPagePreviewScalePxPerMm(appliedPpm);
  }

  const boxLayout = computeSheetBoxLayout(
    nodes,
    sheet.widthMm,
    sheet.heightMm,
    appliedPpm,
    state.pageMargins,
    state.showHeaders,
  );
  if (!boxLayout)
    return;

  if (!applySheetBoxSizing(nodes, boxLayout, sheet.widthMm, sheet.heightMm))
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

  let zoom = computeLayoutZoom(boxLayout, fit.colSumPx, fit.rowSumPx);

  const renderedColPx = fit.colSumPx * zoom;
  const renderedRowPx = fit.rowSumPx * zoom;
  if (
    (renderedColPx > boxLayout.cellAreaW + 1 || renderedRowPx > boxLayout.cellAreaH + 1)
    && zoom > 0.01
  ) {
    zoom = Math.max(0.01, Math.round((zoom - 0.01) * 100) / 100);
  }

  applyZoom(univerAPI, zoom, true);
  resetSheetScroll();
  setLayoutScrollLock(univerAPI, true);
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

  const unsub = univerAPI.onCommandExecuted?.((command) => {
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
  }) ?? (() => {});

  return () => {
    window.clearTimeout(debounceTimer);
    unsub();
  };
}

export function installPageViewport(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
): () => void {
  const schedule = (resetScale = false): void => applyPageViewport(univerAPI, resetScale);

  const unsubLayout = subscribeLayoutViewState(() => {
    if (isLayoutPageViewActive() || pageModeActive)
      schedule(true);
  });
  const unsubRibbon = subscribeLayoutRibbonModel(() => {
    if (isLayoutPageViewActive())
      schedule(true);
  });
  const unsubSnapshot = subscribeSnapshotDims(() => {
    if (isLayoutPageViewActive())
      schedule(false);
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
