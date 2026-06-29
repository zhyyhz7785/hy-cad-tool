import type { FUniver } from '@univerjs/core/facade';

import {
  deriveGridCountsFromPaper,
  mmToColDisplayPx,
  mmToRowDisplayPx,
} from './mm-display';
import { getLayoutViewState, subscribeLayoutViewState } from './layout-view-state';
import { isLayoutTabActive, subscribeLayoutTabActive } from './layout-tab-inject';
import {
  getLayoutRibbonModelState,
  patchLayoutRibbonModel,
  subscribeLayoutRibbonModel,
} from './layout-ribbon-model';
import { resolveSheetSizeMm } from './paper-sheet';
import { getLastSnapshotDims, subscribeSnapshotDims } from './univer-bridge';
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

let lastSig = '';
let savedBaseline: SavedSheetBaseline | null = null;
let reseedingRibbon = false;
let lastRibbonCounts = { rowCount: 0, colCount: 0 };

function shouldApplyLayoutFit(): boolean {
  return isLayoutTabActive() && getLayoutViewState().showPaperBoundary;
}

function resolveAvailableMm(): { widthMm: number; heightMm: number } {
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

function applyGridFit(univerAPI: ReturnType<typeof FUniver.newAPI>, force = false): void {
  const sheet = univerAPI.getActiveWorkbook?.()?.getActiveSheet?.() as FitSheet | null | undefined;
  if (!sheet)
    return;

  if (!savedBaseline)
    savedBaseline = captureBaseline(sheet);

  const avail = resolveAvailableMm();
  const { rowCount, colCount } = resolveTargetCounts();
  if (rowCount < 1 || colCount < 1)
    return;

  const colWidthPx = mmToColDisplayPx(avail.widthMm / colCount);
  const rowHeightPx = mmToRowDisplayPx(avail.heightMm / rowCount);

  const sig = `${rowCount}x${colCount}@${colWidthPx}x${rowHeightPx}@${Math.round(avail.widthMm)}x${Math.round(avail.heightMm)}`;
  if (!force && sig === lastSig) {
    setLayoutScrollLock(univerAPI, true);
    return;
  }

  try {
    if ((sheet.getMaxColumns?.() ?? colCount) !== colCount)
      sheet.setColumnCount?.(colCount);
    if ((sheet.getMaxRows?.() ?? rowCount) !== rowCount)
      sheet.setRowCount?.(rowCount);

    if (sheet.setColumnWidths)
      sheet.setColumnWidths(0, colCount, colWidthPx);
    else if (sheet.setColumnWidth)
      for (let c = 0; c < colCount; c++)
        sheet.setColumnWidth(c, colWidthPx);

    if (sheet.setRowHeightsForced)
      sheet.setRowHeightsForced(0, rowCount, rowHeightPx);
    else if (sheet.setRowHeight)
      for (let r = 0; r < rowCount; r++)
        sheet.setRowHeight(r, rowHeightPx);

    lastSig = sig;
    sheet.refreshCanvas?.();
    // refreshCanvas 可能重建 ScrollBar，下一帧再隐藏
    setLayoutScrollLock(univerAPI, true);
    window.requestAnimationFrame(() => setLayoutScrollLock(univerAPI, true));
  } catch (error) {
    console.warn('[HyCAD] layout grid fit failed', error);
  }
}

function syncLayoutGrid(univerAPI: ReturnType<typeof FUniver.newAPI>, force = false, reseed = false): void {
  if (!shouldApplyLayoutFit()) {
    restoreBaseline(univerAPI);
    return;
  }

  if (reseed)
    reseedRibbonGridCounts();

  applyGridFit(univerAPI, force);
}

/** 安装布局网格铺满（幂等）：仅布局 Tab 临时生效，离开即恢复 Univer 默认网格。 */
export function installLayoutGridFit(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
): () => void {
  const run = (force = false, reseed = false): void => syncLayoutGrid(univerAPI, force, reseed);

  const unsubTab = subscribeLayoutTabActive((active) => {
    if (active)
      run(true, true);
    else
      run(true, false);
  });

  const unsubView = subscribeLayoutViewState(() => {
    if (shouldApplyLayoutFit())
      run(true, true);
    else if (savedBaseline)
      run(true, false);
  });

  const unsubRibbon = subscribeLayoutRibbonModel(() => {
    if (!shouldApplyLayoutFit() || reseedingRibbon)
      return;

    const model = getLayoutRibbonModelState();
    if (model.rowCount === lastRibbonCounts.rowCount
      && model.colCount === lastRibbonCounts.colCount)
      return;

    lastRibbonCounts = { rowCount: model.rowCount, colCount: model.colCount };
    applyGridFit(univerAPI, true);
  });

  const unsubSnapshot = subscribeSnapshotDims(() => {
    savedBaseline = null;
    lastSig = '';
    if (shouldApplyLayoutFit())
      run(true, true);
  });

  return () => {
    unsubTab();
    unsubView();
    unsubRibbon();
    unsubSnapshot();
    restoreBaseline(univerAPI);
    resetLayoutSheetScrollbars(univerAPI);
  };
}
