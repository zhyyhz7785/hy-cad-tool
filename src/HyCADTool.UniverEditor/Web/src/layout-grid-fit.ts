import type { FUniver } from '@univerjs/core/facade';

import {
  deriveGridCountsFromPaper,
  mmToColDisplayPx,
  mmToRowDisplayPx,
} from './mm-display';
import { getLayoutViewState, subscribeLayoutViewState } from './layout-view-state';
import { isLayoutTabActive, subscribeLayoutTabActive } from './layout-tab-inject';
import { resolveSheetSizeMm } from './paper-sheet';
import { getLastSnapshotDims, subscribeSnapshotDims } from './univer-bridge';
import { resetLayoutSheetScrollbars, setLayoutSheetScrollbarsVisible } from './layout-sheet-scrollbars';

/**
 * 布局窗口「网格铺满纸面、无滚动条」。
 *
 * 仅在布局 Tab + 显示纸张边界时临时裁剪/均分网格；切回「开始」等其它 Tab 时
 * 恢复进入布局前的行列规模，与 Univer 默认行为一致（可有滚动条）。
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
/** 本次布局会话是否改过 dev 模式下的格距（需完整恢复默认格距）。 */
let devSizesApplied = false;

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

function captureBaseline(sheet: FitSheet): SavedSheetBaseline {
  const config = sheet.getSheet?.()?.getConfig?.();
  return {
    rowCount: sheet.getMaxRows?.() ?? 200,
    colCount: sheet.getMaxColumns?.() ?? 26,
    defaultRowHeight: config?.defaultRowHeight ?? 24,
    defaultColumnWidth: config?.defaultColumnWidth ?? 88,
  };
}

function setLayoutScrollLock(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
  lock: boolean,
): void {
  setLayoutSheetScrollbarsVisible(univerAPI, !lock);
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

    if (devSizesApplied) {
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
    }

    devSizesApplied = false;
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
  const snapshot = getLastSnapshotDims();
  const hasSnapshot = !!snapshot && snapshot.rowCount > 0 && snapshot.colCount > 0;

  const derived = deriveGridCountsFromPaper(avail.widthMm, avail.heightMm);
  const rowCount = hasSnapshot ? snapshot.rowCount : derived.rowCount;
  const colCount = hasSnapshot ? snapshot.colCount : derived.colCount;
  if (rowCount < 1 || colCount < 1)
    return;

  const colWidthPx = hasSnapshot ? 0 : mmToColDisplayPx(avail.widthMm / colCount);
  const rowHeightPx = hasSnapshot ? 0 : mmToRowDisplayPx(avail.heightMm / rowCount);

  const sig = `${hasSnapshot ? 'S' : 'D'}:${rowCount}x${colCount}@${colWidthPx}x${rowHeightPx}`;
  if (!force && sig === lastSig) {
    setLayoutScrollLock(univerAPI, true);
    return;
  }

  try {
    if ((sheet.getMaxColumns?.() ?? colCount) !== colCount)
      sheet.setColumnCount?.(colCount);
    if ((sheet.getMaxRows?.() ?? rowCount) !== rowCount)
      sheet.setRowCount?.(rowCount);

    if (!hasSnapshot) {
      devSizesApplied = true;
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
    } else {
      devSizesApplied = false;
    }

    lastSig = sig;
    setLayoutScrollLock(univerAPI, true);
    sheet.refreshCanvas?.();
  } catch (error) {
    console.warn('[HyCAD] layout grid fit failed', error);
  }
}

function syncLayoutGrid(univerAPI: ReturnType<typeof FUniver.newAPI>, force = false): void {
  if (shouldApplyLayoutFit())
    applyGridFit(univerAPI, force);
  else
    restoreBaseline(univerAPI);
}

/** 安装布局网格铺满（幂等）：仅布局 Tab 临时生效，离开即恢复 Univer 默认网格。 */
export function installLayoutGridFit(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
): () => void {
  const run = (force = false): void => syncLayoutGrid(univerAPI, force);

  const unsubTab = subscribeLayoutTabActive(() => run(true));
  const unsubView = subscribeLayoutViewState(() => run(false));
  const unsubSnapshot = subscribeSnapshotDims(() => {
    savedBaseline = null;
    lastSig = '';
    run(true);
  });

  return () => {
    unsubTab();
    unsubView();
    unsubSnapshot();
    restoreBaseline(univerAPI);
    resetLayoutSheetScrollbars(univerAPI);
  };
}
