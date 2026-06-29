import type { FUniver } from '@univerjs/core/facade';
import { IRenderManagerService } from '@univerjs/engine-render';

import { colDisplayPxToMm, rowDisplayPxToMm } from './mm-display';
import { getLayoutViewState, subscribeLayoutViewState } from './layout-view-state';
import { getLastSnapshotDims, subscribeSnapshotDims } from './univer-bridge';

/**
 * 原生行列头尺寸标签：「尺寸」开时通过 setCustomHeader 把 A/B/C、1/2/3 换成 mm 宽/高；
 * 「尺寸」关时清空 columnsCfg/rowsCfg 恢复默认字母/数字。
 */

const COLUMN_HEADER_KEY = '__SpreadsheetColumnHeader__';
const ROW_HEADER_KEY = '__SpreadsheetRowHeader__';
const APPLY_RETRY_MS = 50;
const APPLY_MAX_RETRIES = 30;

/** 行列宽/高 mutation 完成后立即刷新 mm 标签。 */
const GRID_SIZE_REFRESH_COMMANDS = new Set([
  'sheet.mutation.set-worksheet-row-height',
  'sheet.mutation.set-worksheet-col-width',
  'sheet.mutation.set-worksheet-row-auto-height',
  'sheet.mutation.set-worksheet-row-is-auto-height',
  'sheet.command.set-row-height',
  'sheet.command.set-worksheet-col-width',
  'sheet.command.delta-row-height',
  'sheet.command.delta-column-width',
]);

interface SheetSizeApi {
  getColumnWidth?: (col: number) => number;
  getRowHeight?: (row: number) => number;
  getColumnCount?: () => number;
  getRowCount?: () => number;
  getMaxColumns?: () => number;
  getMaxRows?: () => number;
  getSheet?: () => {
    getConfig?: () => {
      defaultColumnWidth?: number;
      defaultRowHeight?: number;
      columnCount?: number;
      rowCount?: number;
    };
  };
}

interface HeaderComponent {
  setCustomHeader?: (
    cfg: { columnsCfg?: Record<number, string>; rowsCfg?: Record<number, string> },
    sheetId?: string,
  ) => void;
}

interface RenderUnitLike {
  components?: Map<string, HeaderComponent>;
  scene?: { makeDirty: (dirty?: boolean) => void };
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

function resolveRender(unitId: string): RenderUnitLike | null {
  const render = getRenderManager()?.getRenderById(unitId);
  if (!render?.components)
    return null;
  return render as RenderUnitLike;
}

function forceRenderRefresh(univerAPI: ReturnType<typeof FUniver.newAPI>, render: RenderUnitLike): void {
  render.scene?.makeDirty(true);
  const sheet = univerAPI.getActiveWorkbook?.()?.getActiveSheet?.() as { refreshCanvas?: () => void } | null | undefined;
  sheet?.refreshCanvas?.();
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
  const sheetConfig = sheet?.getSheet?.()?.getConfig?.();

  const defaultColPx = sheetConfig?.defaultColumnWidth ?? 88;
  const defaultRowPx = sheetConfig?.defaultRowHeight ?? 24;

  const colCount = sheet?.getColumnCount?.()
    ?? sheet?.getMaxColumns?.()
    ?? sheetConfig?.columnCount
    ?? dims?.colCount
    ?? 26;
  const rowCount = sheet?.getRowCount?.()
    ?? sheet?.getMaxRows?.()
    ?? sheetConfig?.rowCount
    ?? dims?.rowCount
    ?? 200;

  const colMm: number[] = [];
  for (let c = 0; c < colCount; c++) {
    const livePx = sheet?.getColumnWidth?.(c);
    const mm = (typeof livePx === 'number' && livePx > 0)
      ? colDisplayPxToMm(livePx)
      : (dims?.colWidthsMm?.[c] ?? colDisplayPxToMm(defaultColPx));
    colMm.push(mm);
  }

  const rowMm: number[] = [];
  for (let r = 0; r < rowCount; r++) {
    const livePx = sheet?.getRowHeight?.(r);
    const mm = (typeof livePx === 'number' && livePx > 0)
      ? rowDisplayPxToMm(livePx)
      : (dims?.rowHeightsMm?.[r] ?? rowDisplayPxToMm(defaultRowPx));
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
): boolean {
  const ids = getWorkbookIds(univerAPI);
  if (!ids)
    return false;

  const render = resolveRender(ids.unitId);
  if (!render?.components)
    return false;

  const colHeader = render.components.get(COLUMN_HEADER_KEY);
  const rowHeader = render.components.get(ROW_HEADER_KEY);
  if (!colHeader?.setCustomHeader || !rowHeader?.setCustomHeader)
    return false;

  if (showSize) {
    const tracks = gatherTracks(univerAPI);
    const columnsCfg: Record<number, string> = {};
    for (let c = 0; c < tracks.colMm.length; c++)
      columnsCfg[c] = formatMm(tracks.colMm[c]);

    const rowsCfg: Record<number, string> = {};
    for (let r = 0; r < tracks.rowMm.length; r++)
      rowsCfg[r] = formatMm(tracks.rowMm[r]);

    colHeader.setCustomHeader({ columnsCfg }, ids.subUnitId);
    rowHeader.setCustomHeader({ rowsCfg }, ids.subUnitId);
  }
  else {
    colHeader.setCustomHeader({ columnsCfg: {} }, ids.subUnitId);
    rowHeader.setCustomHeader({ rowsCfg: {} }, ids.subUnitId);
  }

  forceRenderRefresh(univerAPI, render);
  return true;
}

/** 行列头尺寸命令会覆盖 columnsCfg；在 setHeadersVisible 之后调用以恢复 mm 标签。 */
export function refreshGridHeaderLabels(univerAPI: ReturnType<typeof FUniver.newAPI>): void {
  scheduleApplyCustomHeaders(univerAPI, getLayoutViewState().showGridSize);
}

/** 行列宽/高已变更：立即重算标签（拉伸行高/列宽、Ribbon 改尺寸等）。 */
function applyGridHeaderLabelsNow(univerAPI: ReturnType<typeof FUniver.newAPI>): void {
  const vs = getLayoutViewState();
  if (!vs.showHeaders) {
    applyCustomHeaders(univerAPI, false);
    return;
  }
  if (!vs.showGridSize)
    return;
  if (!applyCustomHeaders(univerAPI, true))
    scheduleApplyCustomHeaders(univerAPI, true, 1);
}

function scheduleApplyCustomHeaders(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
  showSize: boolean,
  attempt = 0,
): void {
  window.setTimeout(() => {
    if (applyCustomHeaders(univerAPI, showSize))
      return;
    if (attempt < APPLY_MAX_RETRIES)
      scheduleApplyCustomHeaders(univerAPI, showSize, attempt + 1);
  }, attempt === 0 ? 0 : APPLY_RETRY_MS);
}

export function installGridRulerOverlay(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
): () => void {
  hideOverlayElements();

  let retryTimer: number | undefined;

  const refresh = (): void => {
    hideOverlayElements();
    window.clearTimeout(retryTimer);

    const vs = getLayoutViewState();
    // 延迟到 Univer 行列头尺寸命令之后，避免 columnsCfg 被 headerStyle 写入覆盖。
    retryTimer = window.setTimeout(() => {
      if (!vs.showHeaders) {
        scheduleApplyCustomHeaders(univerAPI, false);
        return;
      }
      scheduleApplyCustomHeaders(univerAPI, vs.showGridSize);
    }, 0);
  };

  const unsubView = subscribeLayoutViewState(() => refresh());
  const unsubSnapshot = subscribeSnapshotDims(() => refresh());

  const unsubSkeletonEvent = (() => {
    const eventName = univerAPI.Event?.SheetSkeletonChanged;
    if (!eventName)
      return () => {};
    return univerAPI.addEvent(eventName, () => applyGridHeaderLabelsNow(univerAPI));
  })();

  const unsubCommands = univerAPI.onCommandExecuted?.((command) => {
    if (!GRID_SIZE_REFRESH_COMMANDS.has(command.id))
      return;
    applyGridHeaderLabelsNow(univerAPI);
  }) ?? (() => {});

  refresh();

  return () => {
    unsubView();
    unsubSnapshot();
    unsubSkeletonEvent();
    unsubCommands();
    window.clearTimeout(retryTimer);
    applyCustomHeaders(univerAPI, false);
    hideOverlayElements();
  };
}
