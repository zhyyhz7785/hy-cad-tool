import type { FUniver } from '@univerjs/core/facade';

import {
  deriveGridCountsFromPaper,
  mmToColDisplayPx,
  mmToRowDisplayPx,
} from './mm-display';
import { getLayoutViewState } from './layout-view-state';
import { isLayoutTabActive } from './layout-tab-inject';
import {
  getLayoutRibbonModelState,
  patchLayoutRibbonModel,
} from './layout-ribbon-model';
import { resolveSheetSizeMm } from './paper-sheet';
import { getLastSnapshotDims } from './univer-bridge';
import { resetLayoutSheetScrollbars, setLayoutScrollLock } from './layout-sheet-scrollbars';

/**
 * 布局窗口「网格铺满纸面、完全固定不可滚动」。
 *
 * 初始行列数 = 可用宽/23.3、可用高/6.4 取整（回填 Ribbon）；
 * 用户改行列数后均分铺满纸面；切回其它 Tab 恢复 Univer 默认网格。
 */

type FitSheet = {
  getSheet?: () => { getConfig?: () => { defaultRowHeight?: number; defaultColumnWidth?: number } };
  getMaxColumns?: () => number;
  getMaxRows?: () => number;
  getColumnWidth?: (col: number) => number;
  getRowHeight?: (row: number) => number;
  setColumnCount?: (count: number) => unknown;
  setRowCount?: (count: number) => unknown;
  setColumnWidths?: (startColumn: number, numColumn: number, width: number) => unknown;
  setColumnWidth?: (column: number, width: number) => unknown;
  setRowHeightsForced?: (startRow: number, numRows: number, height: number) => unknown;
  setRowHeight?: (row: number, height: number) => unknown;
  refreshCanvas?: () => void;
};

interface SavedSheetBaseline {
  rowCount: number;
  colCount: number;
  defaultRowHeight: number;
  defaultColumnWidth: number;
}

/** Univer 写入粒度：0.01px */
const TRACK_PX_PRECISION = 2;

let lastSig = '';
let savedBaseline: SavedSheetBaseline | null = null;
let reseedingRibbon = false;
let lastRibbonCounts = { rowCount: 0, colCount: 0 };

function roundTrackPx(px: number): number {
  const factor = 10 ** TRACK_PX_PRECISION;
  return Math.round(px * factor) / factor;
}

/**
 * 高精度均分 totalPx；最后一格吸收余量，保证 sum === roundTrackPx(totalPx)。
 */
export function distributePx(totalPx: number, count: number): number[] {
  if (count < 1)
    return [];
  const target = roundTrackPx(totalPx);
  if (count === 1)
    return [target];

  const base = totalPx / count;
  const tracks: number[] = [];
  let sum = 0;
  for (let i = 0; i < count - 1; i++) {
    const w = roundTrackPx(base);
    tracks.push(w);
    sum += w;
  }
  tracks.push(roundTrackPx(target - sum));
  return tracks;
}

function shouldApplyLayoutFit(): boolean {
  return isLayoutTabActive() && getLayoutViewState().showPaperBoundary;
}

function resolveAvailableMm(): { widthMm: number; heightMm: number } {
  // CellRegion mm 口径（仅 margin 内区，与 HeaderBand 无关）
  const state = getLayoutViewState();
  const sheet = resolveSheetSizeMm(state.paperPresetIndex, state.orientation);
  const m = state.pageMargins;
  return {
    widthMm: Math.max(1, sheet.widthMm - m.left - m.right),
    heightMm: Math.max(1, sheet.heightMm - m.top - m.bottom),
  };
}

export function deriveLayoutGridCounts(): { rowCount: number; colCount: number } {
  const avail = resolveAvailableMm();
  return deriveGridCountsFromPaper(avail.widthMm, avail.heightMm);
}

function captureBaseline(sheet: FitSheet): SavedSheetBaseline {
  const config = sheet.getSheet?.()?.getConfig?.();
  return {
    rowCount: sheet.getMaxRows?.() ?? 200,
    colCount: sheet.getMaxColumns?.() ?? 26,
    defaultRowHeight: config?.defaultRowHeight ?? 24,
    defaultColumnWidth: config?.defaultColumnWidth ?? 88,
  };
}

function patchRibbonGridCounts(rowCount: number, colCount: number): void {
  reseedingRibbon = true;
  patchLayoutRibbonModel({ rowCount, colCount });
  lastRibbonCounts = { rowCount, colCount };
  reseedingRibbon = false;
}

/** 纸型/边距变化：按种子格距重算行列数并回填 Ribbon。 */
function reseedRibbonGridCounts(): void {
  const snapshot = getLastSnapshotDims();
  if (snapshot && snapshot.rowCount > 0 && snapshot.colCount > 0) {
    patchRibbonGridCounts(snapshot.rowCount, snapshot.colCount);
    return;
  }
  const derived = deriveLayoutGridCounts();
  patchRibbonGridCounts(derived.rowCount, derived.colCount);
}

function resolveTargetCounts(): { rowCount: number; colCount: number } {
  const model = getLayoutRibbonModelState();
  const rowCount = Math.max(1, Math.round(model.rowCount));
  const colCount = Math.max(1, Math.round(model.colCount));
  return { rowCount, colCount };
}

function applyColumnWidths(sheet: FitSheet, widths: number[]): void {
  const colCount = widths.length;
  if (colCount === 0)
    return;

  if (colCount > 1 && sheet.setColumnWidths) {
    sheet.setColumnWidths(0, colCount - 1, widths[0]);
    if (sheet.setColumnWidth)
      sheet.setColumnWidth(colCount - 1, widths[colCount - 1]);
    return;
  }

  if (sheet.setColumnWidth) {
    for (let c = 0; c < colCount; c++)
      sheet.setColumnWidth(c, widths[c]);
  }
  else if (sheet.setColumnWidths && colCount === 1) {
    sheet.setColumnWidths(0, 1, widths[0]);
  }
}

function applyRowHeights(sheet: FitSheet, heights: number[]): void {
  const rowCount = heights.length;
  if (rowCount === 0)
    return;

  if (rowCount > 1 && sheet.setRowHeightsForced) {
    sheet.setRowHeightsForced(0, rowCount - 1, heights[0]);
    if (sheet.setRowHeight)
      sheet.setRowHeight(rowCount - 1, heights[rowCount - 1]);
    return;
  }

  if (sheet.setRowHeightsForced && rowCount === 1) {
    sheet.setRowHeightsForced(0, 1, heights[0]);
    return;
  }

  if (sheet.setRowHeight) {
    for (let r = 0; r < rowCount; r++)
      sheet.setRowHeight(r, heights[r]);
  }
}

/** 从 sheet 读回真实列宽/行高总和（zoom=1 基线 px）。 */
export function measureGridSumPx(
  sheet: FitSheet,
  rowCount: number,
  colCount: number,
): { colSumPx: number; rowSumPx: number; colWidths: number[]; rowHeights: number[] } {
  const colWidths: number[] = [];
  const rowHeights: number[] = [];
  let colSumPx = 0;
  let rowSumPx = 0;

  const config = sheet.getSheet?.()?.getConfig?.();
  const defaultCol = config?.defaultColumnWidth ?? 88;
  const defaultRow = config?.defaultRowHeight ?? 24;

  for (let c = 0; c < colCount; c++) {
    const w = sheet.getColumnWidth?.(c);
    const px = (typeof w === 'number' && w > 0) ? w : defaultCol;
    colWidths.push(px);
    colSumPx += px;
  }

  for (let r = 0; r < rowCount; r++) {
    const h = sheet.getRowHeight?.(r);
    const px = (typeof h === 'number' && h > 0) ? h : defaultRow;
    rowHeights.push(px);
    rowSumPx += px;
  }

  return { colSumPx, rowSumPx, colWidths, rowHeights };
}

function restoreBaseline(univerAPI: ReturnType<typeof FUniver.newAPI>): void {
  if (!savedBaseline)
    return;

  const sheet = univerAPI.getActiveWorkbook?.()?.getActiveSheet?.() as FitSheet | null | undefined;
  if (!sheet)
    return;

  const { rowCount, colCount, defaultRowHeight, defaultColumnWidth } = savedBaseline;

  try {
    if ((sheet.getMaxColumns?.() ?? colCount) !== colCount)
      sheet.setColumnCount?.(colCount);
    if ((sheet.getMaxRows?.() ?? rowCount) !== rowCount)
      sheet.setRowCount?.(rowCount);

    if (sheet.setColumnWidths)
      sheet.setColumnWidths(0, colCount, defaultColumnWidth);
    else if (sheet.setColumnWidth)
      for (let c = 0; c < colCount; c++)
        sheet.setColumnWidth(c, defaultColumnWidth);

    if (sheet.setRowHeightsForced)
      sheet.setRowHeightsForced(0, rowCount, defaultRowHeight);
    else if (sheet.setRowHeight)
      for (let r = 0; r < rowCount; r++)
        sheet.setRowHeight(r, defaultRowHeight);

    lastSig = '';
    setLayoutScrollLock(univerAPI, false);
    sheet.refreshCanvas?.();
  } catch (error) {
    console.warn('[HyCAD] layout grid restore failed', error);
  }
}

export interface GridFitResult {
  colSumPx: number;
  rowSumPx: number;
  colWidths: number[];
  rowHeights: number[];
  rowCount: number;
  colCount: number;
}

/**
 * 有界二次校正：微调最后一列/行使 colSum/rowSum 贴近 target（zoom=1 基线 px）。
 * 返回校正后的测量结果；若无法校正则返回 null。
 */
export function correctLastGridTracks(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
  targets: { targetColSumPx: number; targetRowSumPx: number },
): GridFitResult | null {
  const sheet = univerAPI.getActiveWorkbook?.()?.getActiveSheet?.() as FitSheet | null | undefined;
  if (!sheet)
    return null;

  const { rowCount, colCount } = resolveTargetCounts();
  if (rowCount < 1 || colCount < 1)
    return null;

  let measured = measureGridSumPx(sheet, rowCount, colCount);
  const colDelta = targets.targetColSumPx - measured.colSumPx;
  const rowDelta = targets.targetRowSumPx - measured.rowSumPx;

  if (Math.abs(colDelta) <= 1 && Math.abs(rowDelta) <= 1)
    return { ...measured, rowCount, colCount };

  try {
    if (Math.abs(colDelta) > 1 && colCount > 0 && measured.colWidths.length > 0) {
      const last = colCount - 1;
      const next = roundTrackPx(Math.max(0.01, measured.colWidths[last] + colDelta));
      sheet.setColumnWidth?.(last, next);
    }
    if (Math.abs(rowDelta) > 1 && rowCount > 0 && measured.rowHeights.length > 0) {
      const last = rowCount - 1;
      const next = roundTrackPx(Math.max(0.01, measured.rowHeights[last] + rowDelta));
      sheet.setRowHeight?.(last, next);
    }
    sheet.refreshCanvas?.();
    measured = measureGridSumPx(sheet, rowCount, colCount);
  } catch (error) {
    console.warn('[HyCAD] layout grid last-track correction failed', error);
  }

  return { ...measured, rowCount, colCount };
}

function applyGridFit(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
  force = false,
): GridFitResult | null {
  const sheet = univerAPI.getActiveWorkbook?.()?.getActiveSheet?.() as FitSheet | null | undefined;
  if (!sheet)
    return null;

  if (!savedBaseline)
    savedBaseline = captureBaseline(sheet);

  const avail = resolveAvailableMm();
  const { rowCount, colCount } = resolveTargetCounts();
  if (rowCount < 1 || colCount < 1)
    return null;

  const totalColPx = mmToColDisplayPx(avail.widthMm);
  const totalRowPx = mmToRowDisplayPx(avail.heightMm);
  const colWidths = distributePx(totalColPx, colCount);
  const rowHeights = distributePx(totalRowPx, rowCount);

  const sig = `${rowCount}x${colCount}@${colWidths.join(',')}@${rowHeights.join(',')}@${Math.round(avail.widthMm)}x${Math.round(avail.heightMm)}`;
  const skipWrite = !force && sig === lastSig;

  if (!skipWrite) {
    try {
      if ((sheet.getMaxColumns?.() ?? colCount) !== colCount)
        sheet.setColumnCount?.(colCount);
      if ((sheet.getMaxRows?.() ?? rowCount) !== rowCount)
        sheet.setRowCount?.(rowCount);

      applyColumnWidths(sheet, colWidths);
      applyRowHeights(sheet, rowHeights);

      lastSig = sig;
      sheet.refreshCanvas?.();
      setLayoutScrollLock(univerAPI, true);
      window.requestAnimationFrame(() => setLayoutScrollLock(univerAPI, true));
    } catch (error) {
      console.warn('[HyCAD] layout grid fit failed', error);
    }
  } else {
    setLayoutScrollLock(univerAPI, true);
  }

  const measured = measureGridSumPx(sheet, rowCount, colCount);
  return { ...measured, rowCount, colCount };
}

/**
 * 编排步骤（供 page-viewport 在固定顺序中调用）：可选 reseed 行列数 → 设置格子尺寸，
 * 返回读回的真实列宽/行高总和（zoom=1 基线 px）。不订阅事件、不设置 zoom。
 */
export function applyGridFitStep(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
  options: { reseed?: boolean; force?: boolean } = {},
): GridFitResult | null {
  if (!shouldApplyLayoutFit())
    return null;
  if (options.reseed)
    reseedRibbonGridCounts();
  return applyGridFit(univerAPI, options.reseed === true || options.force === true);
}

/** 快照重建工作簿后调用：sig 失配、baseline 属于已 dispose 的 unit，全部作废。 */
export function invalidateGridFit(): void {
  lastSig = '';
  savedBaseline = null;
}

/** 离开布局模式时恢复 Univer 默认网格（供 page-viewport 调用）。 */
export function restoreGridBaseline(univerAPI: ReturnType<typeof FUniver.newAPI>): void {
  restoreBaseline(univerAPI);
}

export function installLayoutGridFit(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
): () => void {
  return () => {
    restoreBaseline(univerAPI);
    resetLayoutSheetScrollbars(univerAPI);
  };
}
