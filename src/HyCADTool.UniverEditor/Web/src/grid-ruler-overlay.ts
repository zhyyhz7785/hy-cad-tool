import type { FUniver } from '@univerjs/core/facade';
import { IRenderManagerService } from '@univerjs/engine-render';

import { colDisplayPxToMm, rowDisplayPxToMm } from './mm-display';
import { getLayoutViewState, subscribeLayoutViewState } from './layout-view-state';
import { getLastSnapshotDims, subscribeSnapshotDims } from './univer-bridge';

/**
 * 原生行列头尺寸标签：「尺寸」开时通过 setCustomHeader 把 A/B/C、1/2/3 换成 mm 宽/高；
 * 「尺寸」关时清空 columnsCfg/rowsCfg 恢复默认字母/数字。不再绘制 canvas 复刻层。
 */

const COLUMN_HEADER_KEY = '__SpreadsheetColumnHeader__';
const ROW_HEADER_KEY = '__SpreadsheetRowHeader__';

interface SheetSizeApi {
  getColumnWidth?: (col: number) => number;
  getRowHeight?: (row: number) => number;
  getColumnCount?: () => number;
  getRowCount?: () => number;
  getMaxColumns?: () => number;
  getMaxRows?: () => number;
}

interface HeaderComponent {
  setCustomHeader?: (cfg: { columnsCfg?: Record<number, string>; rowsCfg?: Record<number, string> }, sheetId?: string) => void;
}

function resolveSheet(univerAPI: ReturnType<typeof FUniver.newAPI>): SheetSizeApi | null {
  const wb = univerAPI.getActiveWorkbook?.();
  const sheet = wb?.getActiveSheet?.();
  return (sheet as unknown as SheetSizeApi) ?? null;
}

function getWorkbookIds(univerAPI: ReturnType<typeof FUniver.newAPI>): { unitId: string; subUnitId: string } | null {
  const wb = univerAPI.getActiveWorkbook?.();
  if (!wb)
    return null;
  const sheet = wb.getActiveSheet?.();
  if (!sheet)
    return null;
  return { unitId: wb.getId(), subUnitId: sheet.getSheetId() };
}

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

function formatMm(mm: number): string {
  const rounded = Math.round(mm * 10) / 10;
  return Number.isInteger(rounded) ? String(rounded) : rounded.toFixed(1);
}

interface GridTracks {
  colMm: number[];
  rowMm: number[];
}

function gatherTracks(univerAPI: ReturnType<typeof FUniver.newAPI>): GridTracks {
  const dims = getLastSnapshotDims();
  const sheet = resolveSheet(univerAPI);

  const colCount = sheet?.getColumnCount?.()
    ?? sheet?.getMaxColumns?.()
    ?? dims?.colCount
    ?? 200;
  const rowCount = sheet?.getRowCount?.()
    ?? sheet?.getMaxRows?.()
    ?? dims?.rowCount
    ?? 200;

  const colMm: number[] = [];
  for (let c = 0; c < colCount; c++) {
    const livePx = sheet?.getColumnWidth?.(c);
    const mm = (typeof livePx === 'number' && livePx > 0)
      ? colDisplayPxToMm(livePx)
      : (dims?.colWidthsMm?.[c] ?? 0);
    colMm.push(mm);
  }

  const rowMm: number[] = [];
  for (let r = 0; r < rowCount; r++) {
    const livePx = sheet?.getRowHeight?.(r);
    const mm = (typeof livePx === 'number' && livePx > 0)
      ? rowDisplayPxToMm(livePx)
      : (dims?.rowHeightsMm?.[r] ?? 0);
    rowMm.push(mm);
  }

  return { colMm, rowMm };
}

function hideOverlayElements(): void {
  for (const id of ['hycad-grid-hruler', 'hycad-grid-vruler', 'hycad-grid-ruler-corner']) {
    const el = document.getElementById(id);
    if (el)
      el.style.display = 'none';
  }
}

function applyCustomHeaders(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
  showSize: boolean,
): void {
  const ids = getWorkbookIds(univerAPI);
  if (!ids)
    return;

  const renderManager = getRenderManager();
  const render = renderManager?.getRenderById(ids.unitId);
  if (!render?.components)
    return;

  const colHeader = render.components.get(COLUMN_HEADER_KEY) as HeaderComponent | undefined;
  const rowHeader = render.components.get(ROW_HEADER_KEY) as HeaderComponent | undefined;

  if (showSize) {
    const tracks = gatherTracks(univerAPI);
    const columnsCfg: Record<number, string> = {};
    for (let c = 0; c < tracks.colMm.length; c++) {
      if (tracks.colMm[c] > 0)
        columnsCfg[c] = formatMm(tracks.colMm[c]);
    }
    const rowsCfg: Record<number, string> = {};
    for (let r = 0; r < tracks.rowMm.length; r++) {
      if (tracks.rowMm[r] > 0)
        rowsCfg[r] = formatMm(tracks.rowMm[r]);
    }
    colHeader?.setCustomHeader?.({ columnsCfg }, ids.subUnitId);
    rowHeader?.setCustomHeader?.({ rowsCfg }, ids.subUnitId);
  }
  else {
    colHeader?.setCustomHeader?.({ columnsCfg: {} }, ids.subUnitId);
    rowHeader?.setCustomHeader?.({ rowsCfg: {} }, ids.subUnitId);
  }

  const sheet = univerAPI.getActiveWorkbook?.()?.getActiveSheet?.() as { refreshCanvas?: () => void } | null | undefined;
  sheet?.refreshCanvas?.();
}

export function installGridRulerOverlay(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
): () => void {
  hideOverlayElements();

  const refresh = (): void => {
    hideOverlayElements();
    const vs = getLayoutViewState();
    if (!vs.showHeaders) {
      applyCustomHeaders(univerAPI, false);
      return;
    }
    applyCustomHeaders(univerAPI, vs.showGridSize);
  };

  const unsubView = subscribeLayoutViewState(() => refresh());
  const unsubSnapshot = subscribeSnapshotDims(() => refresh());

  refresh();

  return () => {
    unsubView();
    unsubSnapshot();
    applyCustomHeaders(univerAPI, false);
    hideOverlayElements();
  };
}
