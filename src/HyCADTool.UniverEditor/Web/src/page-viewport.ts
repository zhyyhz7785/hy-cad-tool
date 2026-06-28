import type { FUniver } from '@univerjs/core/facade';

import { SetZoomRatioCommand } from '@univerjs/sheets-ui';

import { DISPLAY_PX_PER_MM } from './mm-display';

import { getLayoutViewState, subscribeLayoutViewState } from './layout-view-state';

import { subscribeLayoutRibbonModel } from './layout-ribbon-model';

import { isLayoutTabActive, subscribeLayoutTabActive } from './layout-tab-inject';

import {

  applyPagePreviewScaleStep,

  getPagePreviewScalePxPerMm,

  resetPagePreviewScaleFromFit,

  setPagePreviewScalePxPerMm,

  subscribePagePreviewScale,

} from './page-preview-scale';

import { resolveSheetSizeMm } from './paper-sheet';

import { subscribeSnapshotDims } from './univer-bridge';

import { findScrollElement, installViewportProbe } from './univer-viewport';

/** 图纸（白底 sheetBox）与画布（gridHost 内容区）四边固定间距 mm */
export const CANVAS_SHEET_GAP_MM = 10;

/**
 * Word 式页面：
 *   U7 gridHost = 灰底画布
 *   U7a sheetBox = 图纸，左上角距画布四边各 CANVAS_SHEET_GAP_MM，缩放后不得超出画布
 */
const PAGE_HOST_CLASS = 'hycad-page-grid-host';
const PAGE_SHEET_CLASS = 'hycad-page-sheet-box';
const ZOOM_EPSILON = 0.004;
const MIN_PREVIEW_SCALE = 0.1;
const MAX_PREVIEW_SCALE = 5.0;

let lastZoom = 0;
let wheelInstalled = false;

interface PageNodes {
  gridHost: HTMLElement;
  sheetBox: HTMLElement;
  leftAsideW: number;
  rightAsideW: number;
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

  const leftAside = gridHost.querySelector(':scope > aside[data-u-comp="left-sidebar"]');
  const rightAside = gridHost.querySelector(':scope > aside[data-u-comp="right-sidebar"]');

  return {
    gridHost,
    sheetBox,
    leftAsideW: leftAside instanceof HTMLElement ? leftAside.offsetWidth : 0,
    rightAsideW: rightAside instanceof HTMLElement ? rightAside.offsetWidth : 0,
  };
}

function resolveCanvasContentSize(nodes: PageNodes): { width: number; height: number } {
  return {
    width: Math.max(0, nodes.gridHost.clientWidth - nodes.leftAsideW - nodes.rightAsideW),
    height: Math.max(0, nodes.gridHost.clientHeight),
  };
}

/** 在当前画布尺寸下，图纸四边各留 gapMm 时的最大 px/mm */
export function computeMaxPxPerMm(
  nodes: PageNodes,
  sheetWidthMm: number,
  sheetHeightMm: number,
  gapMm = CANVAS_SHEET_GAP_MM,
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
  box.removeProperty('justify-self');
  box.removeProperty('align-self');
  box.removeProperty('box-shadow');
  box.removeProperty('border');
  box.removeProperty('background');
  box.removeProperty('overflow');

  delete nodes.sheetBox.dataset.hycadPaperMm;
  delete nodes.sheetBox.dataset.hycadSheetMm;
  delete nodes.sheetBox.dataset.hycadGridCount;
}

function applyZoom(univerAPI: ReturnType<typeof FUniver.newAPI>, zoom: number): void {
  if (Math.abs(zoom - lastZoom) < ZOOM_EPSILON)
    return;

  const wb = univerAPI.getActiveWorkbook?.();
  const sheet = wb?.getActiveSheet?.();
  if (!wb || !sheet)
    return;

  lastZoom = zoom;
  void univerAPI.executeCommand(SetZoomRatioCommand.id, {
    unitId: wb.getId(),
    subUnitId: sheet.getSheetId(),
    zoomRatio: Math.round(zoom * 100) / 100,
  });
}

function clearPageViewport(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
  nodes: PageNodes,
  restoreZoom = true,
): void {
  nodes.gridHost.classList.remove(PAGE_HOST_CLASS);
  clearPageStyles(nodes);

  if (restoreZoom) {
    lastZoom = 0;
    applyZoom(univerAPI, 1);
  }
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

function applySheetBoxSizing(
  nodes: PageNodes,
  sheetWidthMm: number,
  sheetHeightMm: number,
  ppm: number,
): boolean {
  const canvas = resolveCanvasContentSize(nodes);
  const gapPx = Math.round(CANVAS_SHEET_GAP_MM * ppm);
  const maxBoxW = Math.max(0, canvas.width - 2 * gapPx);
  const maxBoxH = Math.max(0, canvas.height - 2 * gapPx);

  const boxW = Math.min(Math.round(sheetWidthMm * ppm), maxBoxW);
  const boxH = Math.min(Math.round(sheetHeightMm * ppm), maxBoxH);

  if (boxW <= 0 || boxH <= 0)
    return false;

  nodes.gridHost.classList.add(PAGE_HOST_CLASS);
  nodes.gridHost.style.overflow = 'hidden';

  nodes.sheetBox.classList.add(PAGE_SHEET_CLASS);
  nodes.sheetBox.dataset.hycadSheetMm = `${Math.round(sheetWidthMm)}x${Math.round(sheetHeightMm)}`;
  nodes.sheetBox.dataset.hycadPaperMm = nodes.sheetBox.dataset.hycadSheetMm;

  const box = nodes.sheetBox.style;
  box.flex = 'none';
  box.width = `${boxW}px`;
  box.height = `${boxH}px`;
  box.maxWidth = `${maxBoxW}px`;
  box.maxHeight = `${maxBoxH}px`;
  box.marginLeft = `${gapPx}px`;
  box.marginTop = `${gapPx}px`;
  box.justifySelf = 'start';
  box.alignSelf = 'start';
  box.background = '#ffffff';
  box.boxShadow = '0 2px 12px rgba(15, 23, 42, 0.18)';
  box.border = '1px solid #cbd5e1';
  box.overflow = 'hidden';

  return true;
}

function applyPageViewport(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
  resetScale = false,
): void {
  const nodes = resolveNodes();
  if (!nodes)
    return;

  if (!isLayoutTabActive()) {
    clearPageViewport(univerAPI, nodes);
    return;
  }

  const state = getLayoutViewState();
  if (!state.showPaperBoundary) {
    clearPageViewport(univerAPI, nodes);
    return;
  }

  const sheet = resolveSheetSizeMm(state.paperPresetIndex, state.orientation);
  if (sheet.widthMm <= 0 || sheet.heightMm <= 0)
    return;

  if (resetScale || getPagePreviewScalePxPerMm() <= 0) {
    resetPagePreviewScaleFromFit(computeFitPxPerMm(nodes, sheet.widthMm, sheet.heightMm));
  }

  const clampedPpm = clampPagePreviewScaleToCanvas(
    getPagePreviewScalePxPerMm(),
    nodes,
    sheet.widthMm,
    sheet.heightMm,
  );
  if (Math.abs(clampedPpm - getPagePreviewScalePxPerMm()) > 0.0001)
    setPagePreviewScalePxPerMm(clampedPpm);

  if (!applySheetBoxSizing(nodes, sheet.widthMm, sheet.heightMm, clampedPpm))
    return;

  applyZoom(univerAPI, clampedPpm / DISPLAY_PX_PER_MM);
  resetSheetScroll();
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

export function installPageViewport(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
): () => void {
  const schedule = (resetScale = false): void => applyPageViewport(univerAPI, resetScale);

  const unsubLayout = subscribeLayoutViewState(() => schedule(true));
  const unsubRibbon = subscribeLayoutRibbonModel(() => schedule(true));
  const unsubSnapshot = subscribeSnapshotDims(() => schedule(false));
  const unsubTab = subscribeLayoutTabActive(() => schedule(true));
  const unsubScale = subscribePagePreviewScale(() => schedule(false));
  const unsubProbe = installViewportProbe(univerAPI, () => schedule(false));
  const unsubWheel = installPageWheelZoom(univerAPI);

  window.setTimeout(() => schedule(true), 500);
  window.setTimeout(() => schedule(true), 1500);

  return () => {
    unsubLayout();
    unsubRibbon();
    unsubSnapshot();
    unsubTab();
    unsubScale();
    unsubProbe();
    unsubWheel();

    const nodes = resolveNodes();
    if (nodes)
      clearPageViewport(univerAPI, nodes, false);
  };
}
