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

/**
 * 布局窗口「网格铺满纸面、无滚动条」。
 *
 * 行列数与格距一一对应：列数 = 可用宽 / 23.3 取整，行数 = 可用高 / 6.4 取整（见
 * mm-display.SEED_*），均分后合计 = 纸面可用区像素，网格与纸面等大 → 自然无滚动条
 * （不是锁定滚动条，而是裁掉超出纸面的多余行列）。仅在布局 Tab 激活 + 显示纸张边界时生效，
 * 其它 Tab 行为不变。
 */

type FitSheet = {
  getMaxColumns?: () => number;
  getMaxRows?: () => number;
  setColumnCount?: (count: number) => unknown;
  setRowCount?: (count: number) => unknown;
  setColumnWidths?: (startColumn: number, numColumn: number, width: number) => unknown;
  setColumnWidth?: (column: number, width: number) => unknown;
  setRowHeightsForced?: (startRow: number, numRows: number, height: number) => unknown;
  setRowHeight?: (row: number, height: number) => unknown;
};

let lastSig = '';

/** 纸面可用区（mm）= 整纸 − 上下左右边距，与 page-viewport 的纸张框口径一致。 */
function resolveAvailableMm(): { widthMm: number; heightMm: number } {
  const state = getLayoutViewState();
  const sheet = resolveSheetSizeMm(state.paperPresetIndex, state.orientation);
  const m = state.pageMargins;
  return {
    widthMm: Math.max(1, sheet.widthMm - m.left - m.right),
    heightMm: Math.max(1, sheet.heightMm - m.top - m.bottom),
  };
}

function applyGridFit(univerAPI: ReturnType<typeof FUniver.newAPI>, force = false): void {
  if (!isLayoutTabActive())
    return;
  if (!getLayoutViewState().showPaperBoundary)
    return;

  const avail = resolveAvailableMm();
  const snapshot = getLastSnapshotDims();
  const hasSnapshot = !!snapshot && snapshot.rowCount > 0 && snapshot.colCount > 0;

  const derived = deriveGridCountsFromPaper(avail.widthMm, avail.heightMm);
  const rowCount = hasSnapshot ? snapshot.rowCount : derived.rowCount;
  const colCount = hasSnapshot ? snapshot.colCount : derived.colCount;
  if (rowCount < 1 || colCount < 1)
    return;

  // dev（无快照）：均分铺满；prod（有快照）：尺寸已由 applySnapshotDimensions 铺满，
  // 仅把行列数裁到与快照一致 → 去掉 Math.max(rowCount,64) 的补白空行 → 无滚动条。
  const colWidthPx = hasSnapshot ? 0 : mmToColDisplayPx(avail.widthMm / colCount);
  const rowHeightPx = hasSnapshot ? 0 : mmToRowDisplayPx(avail.heightMm / rowCount);

  const sig = `${hasSnapshot ? 'S' : 'D'}:${rowCount}x${colCount}@${colWidthPx}x${rowHeightPx}`;
  if (!force && sig === lastSig)
    return;

  const sheet = univerAPI.getActiveWorkbook?.()?.getActiveSheet?.() as FitSheet | null | undefined;
  if (!sheet)
    return;

  try {
    if ((sheet.getMaxColumns?.() ?? colCount) !== colCount)
      sheet.setColumnCount?.(colCount);
    if ((sheet.getMaxRows?.() ?? rowCount) !== rowCount)
      sheet.setRowCount?.(rowCount);

    if (!hasSnapshot) {
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
    }

    lastSig = sig;
  } catch (error) {
    console.warn('[HyCAD] layout grid fit failed', error);
  }
}

/** 安装布局网格铺满（幂等）：布局 Tab 激活 / 纸张/边距变化 / 快照加载时重算。 */
export function installLayoutGridFit(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
): () => void {
  const run = (force = false): void => applyGridFit(univerAPI, force);

  const unsubTab = subscribeLayoutTabActive(() => {
    lastSig = '';
    run(true);
  });
  const unsubView = subscribeLayoutViewState(() => run(false));
  const unsubSnapshot = subscribeSnapshotDims(() => {
    lastSig = '';
    run(true);
  });

  window.setTimeout(() => run(true), 700);
  window.setTimeout(() => run(true), 1800);

  return () => {
    unsubTab();
    unsubView();
    unsubSnapshot();
  };
}
