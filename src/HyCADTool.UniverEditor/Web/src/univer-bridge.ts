import type { FUniver } from '@univerjs/core/facade';
import {
  mmToColDisplayPx,
  mmToRowDisplayPx,
  colDisplayPxToMm,
  rowDisplayPxToMm,
} from './mm-display';
import {
  BooleanNumber,
  BorderStyleTypes,
  HorizontalAlign,
  LocaleType,
  VerticalAlign,
  WrapStrategy,
  type IBorderData,
  type IStyleData,
  type IWorkbookData,
} from '@univerjs/core';

export interface HyCadCellBorders {
  topMm: number;
  rightMm: number;
  bottomMm: number;
  leftMm: number;
}

export type HyCadCellAlign = 'start' | 'center' | 'end';
export type HyCadCellOrientation = 'horizontal' | 'verticalStacked';

export interface HyCadCellSnapshot {
  row: number;
  col: number;
  rowSpan: number;
  colSpan: number;
  text: string;
  editable: boolean;
  // ---- v2 富快照（可选；缺省时回退 v1 行为）----
  hAlign?: HyCadCellAlign;
  vAlign?: HyCadCellAlign;
  textHeightMm?: number;
  allowWrap?: boolean;
  orientation?: HyCadCellOrientation;
  borders?: HyCadCellBorders;
  role?: string;
  fieldKey?: string;
  isPhotoSlot?: boolean;
}
export interface HyCadGridSnapshot {
  rowCount: number;
  colCount: number;
  rowHeightsMm: number[];
  colWidthsMm: number[];
  cells: HyCadCellSnapshot[];
}

export interface HyCadRect {
  startRow: number;
  startCol: number;
  endRow: number;
  endCol: number;
}

export type HyCadPublishExportMode = 'default' | 'full' | 'content';

/** 布局 Tab 用：最近一次加载快照的纸面 mm 尺寸（口径 B，非像素夹值）。 */
export interface HyCadSnapshotDims {
  rowCount: number;
  colCount: number;
  rowHeightsMm: number[];
  colWidthsMm: number[];
}

let lastSnapshotDims: HyCadSnapshotDims | null = null;
const snapshotDimListeners = new Set<() => void>();

function emitSnapshotDims(): void {
  for (const fn of snapshotDimListeners)
    fn();
}

/** 快照纸面 mm 尺寸变更（loadSnapshot 后触发，供 page-viewport 重算页面框）。 */
export function subscribeSnapshotDims(listener: () => void): () => void {
  snapshotDimListeners.add(listener);
  return () => snapshotDimListeners.delete(listener);
}

let autoFitUniverAPI: ReturnType<typeof FUniver.newAPI> | null = null;
let autoFitPostHost: ((payload: Record<string, unknown>) => void) | null = null;

/** 供布局 Tab「自动调整行高/列宽」使用：注册 postHostMessage。 */
export function configureHyCadAutoFitPost(
  postHostMessage: (payload: Record<string, unknown>) => void,
): void {
  autoFitPostHost = postHostMessage;
}

/** 读取最近加载快照的纸面 mm 尺寸（供布局 Tab 显示真值）。 */
export function getLastSnapshotDims(): HyCadSnapshotDims | null {
  return lastSnapshotDims;
}

declare global {
  interface Window {
    hyCadBridge?: {
      loadSnapshot: (snapshot: HyCadGridSnapshot | string) => boolean;
      exportSnapshot: () => HyCadGridSnapshot | null;
      exportSnapshotForPublish: (mode: HyCadPublishExportMode) => {
        snapshot: HyCadGridSnapshot | null;
        clipRect?: HyCadRect;
        error?: string;
      };
    };
  }
}

function colToLetter(col: number): string {
  let value = col + 1;
  let letters = '';
  while (value > 0) {
    const remainder = (value - 1) % 26;
    letters = String.fromCharCode(65 + remainder) + letters;
    value = Math.floor((value - 1) / 26);
  }
  return letters;
}

function toRangeAddress(row: number, col: number, rowSpan: number, colSpan: number): string {
  const start = `${colToLetter(col)}${row + 1}`;
  const end = `${colToLetter(col + colSpan - 1)}${row + rowSpan}`;
  return rowSpan === 1 && colSpan === 1 ? start : `${start}:${end}`;
}

function readNumber(value: unknown, fallback = 0): number {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : fallback;
}

function readOptionalAlign(value: unknown): HyCadCellAlign | undefined {
  if (value === 'start' || value === 'center' || value === 'end')
    return value;
  return undefined;
}

function readOptionalOrientation(value: unknown): HyCadCellOrientation | undefined {
  if (value === 'horizontal' || value === 'verticalStacked')
    return value;
  return undefined;
}

function readOptionalBool(value: unknown): boolean | undefined {
  if (typeof value === 'boolean')
    return value;
  return undefined;
}

function readOptionalNumber(value: unknown): number | undefined {
  if (value == null)
    return undefined;
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : undefined;
}

function readOptionalString(value: unknown): string | undefined {
  if (typeof value === 'string' && value !== '')
    return value;
  return undefined;
}

function readBorders(raw: unknown): HyCadCellBorders | undefined {
  if (!raw || typeof raw !== 'object')
    return undefined;
  const b = raw as Record<string, unknown>;
  const top = readNumber(b.topMm ?? b.TopMm);
  const right = readNumber(b.rightMm ?? b.RightMm);
  const bottom = readNumber(b.bottomMm ?? b.BottomMm);
  const left = readNumber(b.leftMm ?? b.LeftMm);
  if (top <= 0 && right <= 0 && bottom <= 0 && left <= 0)
    return undefined;
  return { topMm: top, rightMm: right, bottomMm: bottom, leftMm: left };
}

function normalizeCell(raw: Record<string, unknown>): HyCadCellSnapshot {
  return {
    row: readNumber(raw.row ?? raw.Row),
    col: readNumber(raw.col ?? raw.Col),
    rowSpan: readNumber(raw.rowSpan ?? raw.RowSpan, 1),
    colSpan: readNumber(raw.colSpan ?? raw.ColSpan, 1),
    text: String(raw.text ?? raw.Text ?? ''),
    editable: Boolean(raw.editable ?? raw.Editable ?? true),
    hAlign: readOptionalAlign(raw.hAlign ?? raw.HAlign),
    vAlign: readOptionalAlign(raw.vAlign ?? raw.VAlign),
    textHeightMm: readOptionalNumber(raw.textHeightMm ?? raw.TextHeightMm),
    allowWrap: readOptionalBool(raw.allowWrap ?? raw.AllowWrap),
    orientation: readOptionalOrientation(raw.orientation ?? raw.Orientation),
    borders: readBorders(raw.borders ?? raw.Borders),
    role: readOptionalString(raw.role ?? raw.Role),
    fieldKey: readOptionalString(raw.fieldKey ?? raw.FieldKey),
    isPhotoSlot: readOptionalBool(raw.isPhotoSlot ?? raw.IsPhotoSlot),
  };
}

function normalizeSnapshot(input: HyCadGridSnapshot | string | Record<string, unknown>): HyCadGridSnapshot | null {
  let raw: Record<string, unknown>;
  if (typeof input === 'string') {
    try {
      raw = JSON.parse(input) as Record<string, unknown>;
    } catch {
      return null;
    }
  } else {
    raw = input as Record<string, unknown>;
  }

  const cellsRaw = (raw.cells ?? raw.Cells ?? []) as Array<Record<string, unknown>>;
  return {
    rowCount: readNumber(raw.rowCount ?? raw.RowCount, 1),
    colCount: readNumber(raw.colCount ?? raw.ColCount, 1),
    rowHeightsMm: (raw.rowHeightsMm ?? raw.RowHeightsMm ?? []) as number[],
    colWidthsMm: (raw.colWidthsMm ?? raw.ColWidthsMm ?? []) as number[],
    cells: cellsRaw.map(normalizeCell),
  };
}

function ensureSheetSize(univerAPI: ReturnType<typeof FUniver.newAPI>, rowCount: number, colCount: number): void {
  const workbook = univerAPI.getActiveWorkbook();
  const sheet = workbook?.getActiveSheet();
  if (!workbook || !sheet)
    return;

  const targetRows = Math.max(rowCount, 1);
  const targetCols = Math.max(colCount, 1);
  const currentRows = sheet.getMaxRows?.() ?? targetRows;
  const currentCols = sheet.getMaxColumns?.() ?? targetCols;

  if (targetRows > currentRows && sheet.insertRowsAfter)
    sheet.insertRowsAfter(currentRows - 1, targetRows - currentRows);
  if (targetCols > currentCols && sheet.insertColumnsAfter)
    sheet.insertColumnsAfter(currentCols - 1, targetCols - currentCols);
}

type SheetLike = NonNullable<ReturnType<NonNullable<ReturnType<ReturnType<typeof FUniver.newAPI>['getActiveWorkbook']>>['getActiveSheet']>>;

type RangeLike = {
  getRow?: () => number;
  getColumn?: () => number;
  getLastRow?: () => number;
  getLastColumn?: () => number;
  getHeight?: () => number;
  getWidth?: () => number;
  isPartOfMerge?: () => boolean;
  isMerged?: () => boolean;
  getMergeData?: () => { startRow: number; endRow: number; startColumn: number; endColumn: number } | null;
  getDisplayValue?: () => unknown;
  getValue?: () => unknown;
};

type ExtendedRangeLike = RangeLike & {
  getStartRow?: () => number;
  getStartColumn?: () => number;
  getEndRow?: () => number;
  getEndColumn?: () => number;
};

function readRangeBounds(range: ExtendedRangeLike): HyCadRect {
  const startRow = range.getRow?.() ?? range.getStartRow?.() ?? 0;
  const startCol = range.getColumn?.() ?? range.getStartColumn?.() ?? 0;
  const endRow = range.getLastRow?.() ?? range.getEndRow?.()
    ?? (startRow + Math.max(1, range.getHeight?.() ?? 1) - 1);
  const endCol = range.getLastColumn?.() ?? range.getEndColumn?.()
    ?? (startCol + Math.max(1, range.getWidth?.() ?? 1) - 1);
  return { startRow, startCol, endRow, endCol };
}

function readSelectionBounds(sheet: SheetLike): HyCadRect | null {
  const selection = sheet.getSelection?.() as {
    getActiveRange?: () => ExtendedRangeLike | null;
    getActiveRangeList?: () => ExtendedRangeLike[];
    getCurrentCell?: () => { actualRow?: number; actualColumn?: number; row?: number; column?: number } | null;
  } | null | undefined;

  const activeRange = selection?.getActiveRange?.() ?? sheet.getActiveRange?.() as ExtendedRangeLike | null | undefined;
  if (activeRange)
    return readRangeBounds(activeRange);

  const rangeList = selection?.getActiveRangeList?.();
  if (rangeList?.length)
    return readRangeBounds(rangeList[0]);

  const currentCell = selection?.getCurrentCell?.();
  if (currentCell) {
    const row = currentCell.actualRow ?? currentCell.row;
    const col = currentCell.actualColumn ?? currentCell.column;
    if (typeof row === 'number' && typeof col === 'number' && row >= 0 && col >= 0)
      return { startRow: row, startCol: col, endRow: row, endCol: col };
  }

  return null;
}

function intersectRects(a: HyCadRect, b: HyCadRect): HyCadRect {
  return {
    startRow: Math.max(a.startRow, b.startRow),
    startCol: Math.max(a.startCol, b.startCol),
    endRow: Math.min(a.endRow, b.endRow),
    endCol: Math.min(b.endCol, b.endCol),
  };
}

function isValidRect(rect: HyCadRect): boolean {
  return rect.endRow >= rect.startRow && rect.endCol >= rect.startCol;
}

function cellHasContent(range: RangeLike): boolean {
  const text = String(range.getDisplayValue?.() ?? range.getValue?.() ?? '').trim();
  if (text !== '')
    return true;

  return Boolean(range.isMerged?.());
}

function computeContentBounds(
  sheet: SheetLike,
  scanRect: HyCadRect,
): HyCadRect | null {
  let minRow = Number.POSITIVE_INFINITY;
  let minCol = Number.POSITIVE_INFINITY;
  let maxRow = -1;
  let maxCol = -1;

  for (let r = scanRect.startRow; r <= scanRect.endRow; r++) {
    for (let c = scanRect.startCol; c <= scanRect.endCol; c++) {
      const range = sheet.getRange(`${colToLetter(c)}${r + 1}`) as RangeLike;
      if (range.isPartOfMerge?.() && !range.isMerged?.())
        continue;

      if (!cellHasContent(range))
        continue;

      let endR = r;
      let endC = c;
      if (range.isMerged?.()) {
        const mergeData = range.getMergeData?.();
        if (mergeData) {
          endR = mergeData.endRow;
          endC = mergeData.endColumn;
        }
      }

      minRow = Math.min(minRow, r);
      minCol = Math.min(minCol, c);
      maxRow = Math.max(maxRow, endR);
      maxCol = Math.max(maxCol, endC);
    }
  }

  if (maxRow < 0)
    return null;

  return {
    startRow: minRow,
    startCol: minCol,
    endRow: maxRow,
    endCol: maxCol,
  };
}

function exportRegion(
  sheet: SheetLike,
  exportRect: HyCadRect,
  options: { rebase: boolean },
): HyCadGridSnapshot {
  const rowOffset = options.rebase ? exportRect.startRow : 0;
  const colOffset = options.rebase ? exportRect.startCol : 0;
  const rowCount = options.rebase
    ? exportRect.endRow - exportRect.startRow + 1
    : exportRect.endRow + 1;
  const colCount = options.rebase
    ? exportRect.endCol - exportRect.startCol + 1
    : exportRect.endCol + 1;

  const rowHeightsMm: number[] = [];
  const colWidthsMm: number[] = [];
  const cells: HyCadCellSnapshot[] = [];
  const mergedSeen = new Set<string>();

  for (let r = exportRect.startRow; r <= exportRect.endRow; r++)
    rowHeightsMm.push(rowDisplayPxToMm(sheet.getRowHeight?.(r) ?? 0));
  for (let c = exportRect.startCol; c <= exportRect.endCol; c++)
    colWidthsMm.push(colDisplayPxToMm(sheet.getColumnWidth?.(c) ?? 0));

  for (let r = exportRect.startRow; r <= exportRect.endRow; r++) {
    for (let c = exportRect.startCol; c <= exportRect.endCol; c++) {
      const range = sheet.getRange(`${colToLetter(c)}${r + 1}`) as RangeLike;
      if (range.isPartOfMerge?.() && !range.isMerged?.())
        continue;

      const key = `${r}:${c}`;
      if (mergedSeen.has(key))
        continue;

      let rowSpan = 1;
      let colSpan = 1;
      if (range.isMerged?.()) {
        const mergeData = range.getMergeData?.();
        if (mergeData) {
          rowSpan = mergeData.endRow - mergeData.startRow + 1;
          colSpan = mergeData.endColumn - mergeData.startColumn + 1;
          for (let rr = mergeData.startRow; rr <= mergeData.endRow; rr++) {
            for (let cc = mergeData.startColumn; cc <= mergeData.endColumn; cc++)
              mergedSeen.add(`${rr}:${cc}`);
          }
        }
      }

      const text = String(range.getDisplayValue?.() ?? range.getValue?.() ?? '');
      cells.push({
        row: r - rowOffset,
        col: c - colOffset,
        rowSpan,
        colSpan,
        text,
        editable: true,
      });
    }
  }

  return {
    rowCount: Math.max(rowCount, 1),
    colCount: Math.max(colCount, 1),
    rowHeightsMm,
    colWidthsMm,
    cells,
  };
}

function resolveDefaultExportRect(sheet: SheetLike, maxRows: number, maxCols: number): HyCadRect {
  const scanRect: HyCadRect = {
    startRow: 0,
    startCol: 0,
    endRow: Math.max(maxRows - 1, 0),
    endCol: Math.max(maxCols - 1, 0),
  };
  const content = computeContentBounds(sheet, scanRect);
  if (!content)
    return { startRow: 0, startCol: 0, endRow: 0, endCol: 0 };

  return {
    startRow: 0,
    startCol: 0,
    endRow: content.endRow,
    endCol: content.endCol,
  };
}

const MM_TO_POINT = 72 / 25.4;

function mmToPoint(textHeightMm: number | undefined): number {
  const mm = !textHeightMm || textHeightMm <= 0 ? 3.5 : textHeightMm;
  return Math.round(mm * MM_TO_POINT * 100) / 100;
}

function toHorizontalAlign(align: HyCadCellAlign | undefined): HorizontalAlign {
  switch (align) {
    case 'center':
      return HorizontalAlign.CENTER;
    case 'end':
      return HorizontalAlign.RIGHT;
    default:
      return HorizontalAlign.LEFT;
  }
}

function toVerticalAlign(align: HyCadCellAlign | undefined): VerticalAlign {
  switch (align) {
    case 'start':
      return VerticalAlign.TOP;
    case 'end':
      return VerticalAlign.BOTTOM;
    default:
      return VerticalAlign.MIDDLE;
  }
}

function toBorderStyleType(widthMm: number): BorderStyleTypes {
  if (widthMm <= 0)
    return BorderStyleTypes.NONE;
  return widthMm >= 0.5 ? BorderStyleTypes.MEDIUM : BorderStyleTypes.THIN;
}

function buildBorderData(borders: HyCadCellBorders | undefined): IBorderData | undefined {
  if (!borders)
    return undefined;
  const black = { rgb: '#000000' };
  const bd: IBorderData = {};
  if (borders.topMm > 0)
    bd.t = { s: toBorderStyleType(borders.topMm), cl: black };
  if (borders.bottomMm > 0)
    bd.b = { s: toBorderStyleType(borders.bottomMm), cl: black };
  if (borders.leftMm > 0)
    bd.l = { s: toBorderStyleType(borders.leftMm), cl: black };
  if (borders.rightMm > 0)
    bd.r = { s: toBorderStyleType(borders.rightMm), cl: black };
  return Object.keys(bd).length > 0 ? bd : undefined;
}

/**
 * 把 v2 富快照字段烘焙成 Univer IStyleData。
 * 无任何可渲染语义时返回 null（保持 v1 行为，不写 style）。
 * 注：不再自动写背景底纹（颜色交由用户在 Univer 中自行设置）。
 */
function buildCellStyle(cell: HyCadCellSnapshot): IStyleData | null {
  const hasV2 = cell.hAlign !== undefined
    || cell.vAlign !== undefined
    || cell.textHeightMm !== undefined
    || cell.allowWrap !== undefined
    || cell.orientation !== undefined
    || cell.borders !== undefined
    || cell.role !== undefined
    || cell.isPhotoSlot !== undefined;
  if (!hasV2)
    return null;

  const style: IStyleData = {
    ht: toHorizontalAlign(cell.hAlign),
    vt: toVerticalAlign(cell.vAlign),
    fs: mmToPoint(cell.textHeightMm),
  };

  const wrap = cell.allowWrap === true || cell.orientation === 'verticalStacked';
  style.tb = wrap ? WrapStrategy.WRAP : WrapStrategy.CLIP;

  const border = buildBorderData(cell.borders);
  if (border)
    style.bd = border;

  return style;
}

function snapshotToWorkbookData(snapshot: HyCadGridSnapshot): IWorkbookData {
  const rowCount = Math.max(snapshot.rowCount, 1);
  const colCount = Math.max(snapshot.colCount, 1);
  const cellData: NonNullable<IWorkbookData['sheets'][string]['cellData']> = {};
  const mergeData: NonNullable<IWorkbookData['sheets'][string]['mergeData']> = [];
  const styles: NonNullable<IWorkbookData['styles']> = {};
  const styleIdByKey = new Map<string, string>();

  const internStyle = (style: IStyleData): string => {
    const key = JSON.stringify(style);
    const existing = styleIdByKey.get(key);
    if (existing)
      return existing;
    const id = `s${styleIdByKey.size + 1}`;
    styleIdByKey.set(key, id);
    styles[id] = style;
    return id;
  };

  for (const cell of snapshot.cells ?? []) {
    const text = cell.text ?? '';
    const style = buildCellStyle(cell);
    if (text !== '' || style) {
      if (!cellData[cell.row])
        cellData[cell.row] = {};
      const cellObj: { v?: string; s?: string } = {};
      if (text !== '')
        cellObj.v = text;
      if (style)
        cellObj.s = internStyle(style);
      cellData[cell.row][cell.col] = cellObj;
    }

    const rowSpan = cell.rowSpan ?? 1;
    const colSpan = cell.colSpan ?? 1;
    if (rowSpan > 1 || colSpan > 1) {
      mergeData.push({
        startRow: cell.row,
        endRow: cell.row + rowSpan - 1,
        startColumn: cell.col,
        endColumn: cell.col + colSpan - 1,
      });
    }
  }

  return {
    id: `hycad-${Date.now()}`,
    name: 'HyCAD Table',
    appVersion: '0.25.0',
    locale: LocaleType.ZH_CN,
    sheetOrder: ['sheet-01'],
    styles,
    sheets: {
      'sheet-01': {
        id: 'sheet-01',
        name: 'Sheet1',
        rowCount: Math.max(rowCount, 64),
        columnCount: Math.max(colCount, 16),
        defaultRowHeight: mmToRowDisplayPx(snapshot.rowHeightsMm?.[0] ?? 10),
        defaultColumnWidth: mmToColDisplayPx(snapshot.colWidthsMm?.[0] ?? 25),
        showGridlines: BooleanNumber.TRUE,
        freeze: { xSplit: 0, ySplit: 0, startRow: 0, startColumn: 0 },
        mergeData,
        cellData,
      },
    },
  };
}

function applySnapshotDimensions(sheet: SheetLike, snapshot: HyCadGridSnapshot): void {
  for (let r = 0; r < snapshot.rowCount; r++) {
    const height = mmToRowDisplayPx(snapshot.rowHeightsMm?.[r] ?? 10);
    if (sheet.setRowHeightsForced)
      sheet.setRowHeightsForced(r, 1, height);
    else
      sheet.setRowHeight?.(r, height);
  }

  for (let c = 0; c < snapshot.colCount; c++) {
    const width = mmToColDisplayPx(snapshot.colWidthsMm?.[c] ?? 25);
    sheet.setColumnWidth?.(c, width);
  }
}

function rebuildWorkbookFromSnapshot(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
  snapshot: HyCadGridSnapshot,
): void {
  const active = univerAPI.getActiveWorkbook();
  const unitId = active?.getId?.();
  const workbookData = snapshotToWorkbookData(snapshot);

  if (unitId)
    univerAPI.disposeUnit(unitId);

  univerAPI.createWorkbook(workbookData);

  const sheet = univerAPI.getActiveWorkbook()?.getActiveSheet();
  if (sheet)
    applySnapshotDimensions(sheet, snapshot);
}

type AutoFitSheetLike = SheetLike & {
  autoResizeRows?: (startRow: number, numRows: number) => void;
  autoResizeColumns?: (startColumn: number, numColumns: number) => void;
  getRowHeight?: (row: number) => number;
  getColumnWidth?: (col: number) => number;
  getMaxRows?: () => number;
  getMaxColumns?: () => number;
};

/** Excel 式：按内容自动调整行高 → px→mm 回写 Domain。 */
export function autoFitRowHeights(): void {
  const univerAPI = autoFitUniverAPI;
  const postHost = autoFitPostHost;
  if (!univerAPI || !postHost)
    return;

  const sheet = univerAPI.getActiveWorkbook()?.getActiveSheet() as AutoFitSheetLike | null | undefined;
  if (!sheet) {
    postHost({ type: 'error', message: '无活动工作表' });
    return;
  }

  const selection = readSelectionBounds(sheet);
  const startRow = selection?.startRow ?? 0;
  const endRow = selection?.endRow ?? ((lastSnapshotDims?.rowCount ?? sheet.getMaxRows?.() ?? 1) - 1);
  const numRows = Math.max(1, endRow - startRow + 1);

  try {
    sheet.autoResizeRows?.(startRow, numRows);
    const sizesMm: number[] = [];
    for (let i = 0; i < numRows; i++) {
      const px = sheet.getRowHeight?.(startRow + i) ?? 0;
      sizesMm.push(rowDisplayPxToMm(px));
    }
    postHost({ type: 'hyCadTrackSizes', axis: 'row', start: startRow, sizesMm });
  } catch (error) {
    console.error('[HyCAD] autoFitRowHeights failed', error);
    postHost({
      type: 'error',
      message: error instanceof Error ? error.message : '自动调整行高失败',
    });
  }
}

/** Excel 式：按内容自动调整列宽 → px→mm 回写 Domain。 */
export function autoFitColWidths(): void {
  const univerAPI = autoFitUniverAPI;
  const postHost = autoFitPostHost;
  if (!univerAPI || !postHost)
    return;

  const sheet = univerAPI.getActiveWorkbook()?.getActiveSheet() as AutoFitSheetLike | null | undefined;
  if (!sheet) {
    postHost({ type: 'error', message: '无活动工作表' });
    return;
  }

  const selection = readSelectionBounds(sheet);
  const startCol = selection?.startCol ?? 0;
  const endCol = selection?.endCol ?? ((lastSnapshotDims?.colCount ?? sheet.getMaxColumns?.() ?? 1) - 1);
  const numCols = Math.max(1, endCol - startCol + 1);

  try {
    sheet.autoResizeColumns?.(startCol, numCols);
    const sizesMm: number[] = [];
    for (let i = 0; i < numCols; i++) {
      const px = sheet.getColumnWidth?.(startCol + i) ?? 0;
      sizesMm.push(colDisplayPxToMm(px));
    }
    postHost({ type: 'hyCadTrackSizes', axis: 'col', start: startCol, sizesMm });
  } catch (error) {
    console.error('[HyCAD] autoFitColWidths failed', error);
    postHost({
      type: 'error',
      message: error instanceof Error ? error.message : '自动调整列宽失败',
    });
  }
}

export function installHyCadBridge(univerAPI: ReturnType<typeof FUniver.newAPI>): void {
  autoFitUniverAPI = univerAPI;
  let pendingSnapshot: HyCadGridSnapshot | null = null;
  let coalesceToken = 0;

  window.hyCadBridge = {
    loadSnapshot(raw: HyCadGridSnapshot | string): boolean {
      const snapshot = normalizeSnapshot(raw);
      if (!snapshot)
        return false;

      lastSnapshotDims = {
        rowCount: snapshot.rowCount,
        colCount: snapshot.colCount,
        rowHeightsMm: snapshot.rowHeightsMm ?? [],
        colWidthsMm: snapshot.colWidthsMm ?? [],
      };
      emitSnapshotDims();

      pendingSnapshot = snapshot;
      const token = ++coalesceToken;
      queueMicrotask(() => {
        if (token !== coalesceToken)
          return;

        const snap = pendingSnapshot;
        pendingSnapshot = null;
        if (!snap)
          return;

        try {
          rebuildWorkbookFromSnapshot(univerAPI, snap);
        } catch (error) {
          console.error('[HyCAD] loadSnapshot failed', error);
        }
      });

      return true;
    },

    exportSnapshot(): HyCadGridSnapshot | null {
      const workbook = univerAPI.getActiveWorkbook();
      const sheet = workbook?.getActiveSheet();
      if (!workbook || !sheet)
        return null;

      try {
        const sheetMaxRows = sheet.getMaxRows?.() ?? 1;
        const sheetMaxCols = sheet.getMaxColumns?.() ?? 1;
        const exportRect = resolveDefaultExportRect(sheet, sheetMaxRows, sheetMaxCols);
        return exportRegion(sheet, exportRect, { rebase: false });
      } catch (error) {
        console.error('[HyCAD] exportSnapshot failed', error);
        return null;
      }
    },

    exportSnapshotForPublish(mode: HyCadPublishExportMode) {
      const workbook = univerAPI.getActiveWorkbook();
      const sheet = workbook?.getActiveSheet();
      if (!workbook || !sheet)
        return { snapshot: null, error: '无活动工作表' };

      try {
        const sheetMaxRows = sheet.getMaxRows?.() ?? 1;
        const sheetMaxCols = sheet.getMaxColumns?.() ?? 1;

        if (mode === 'default') {
          const exportRect = resolveDefaultExportRect(sheet, sheetMaxRows, sheetMaxCols);
          return {
            snapshot: exportRegion(sheet, exportRect, { rebase: false }),
          };
        }

        const selection = readSelectionBounds(sheet);
        if (!selection)
          return { snapshot: null, error: '请先在表格中框选落图区域（拖选单元格后再点范围落图）' };

        let exportRect = selection;
        if (mode === 'content') {
          const content = computeContentBounds(sheet, selection);
          if (!content)
            return { snapshot: null, error: '选区内没有可落图的内容' };

          exportRect = intersectRects(selection, content);
          if (!isValidRect(exportRect))
            return { snapshot: null, error: '选区内没有可落图的内容' };
        }

        const snapshot = exportRegion(sheet, exportRect, { rebase: true });

        return {
          snapshot,
          clipRect: exportRect,
        };
      } catch (error) {
        console.error('[HyCAD] exportSnapshotForPublish failed', error);
        return {
          snapshot: null,
          error: error instanceof Error ? error.message : 'exportSnapshotForPublish failed',
        };
      }
    },
  };
}

export function handleHostCommand(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
  postHostMessage: (payload: Record<string, unknown>) => void,
  raw: string,
): void {
  try {
    const message = JSON.parse(raw) as { type?: string; payload?: unknown; action?: string };
    switch (message.type) {
      case 'loadSnapshot': {
        const ok = window.hyCadBridge?.loadSnapshot(message.payload as HyCadGridSnapshot | string) ?? false;
        postHostMessage({ type: ok ? 'snapshotLoaded' : 'error', message: ok ? undefined : 'loadSnapshot failed' });
        break;
      }
      case 'exportSnapshot': {
        const snapshot = window.hyCadBridge?.exportSnapshot();
        postHostMessage({ type: 'snapshot', payload: snapshot, publishMode: 'default' });
        break;
      }
      case 'exportSnapshotForPublish': {
        const payload = (message.payload ?? {}) as { mode?: HyCadPublishExportMode };
        const mode = payload.mode ?? 'default';
        const result = window.hyCadBridge?.exportSnapshotForPublish(mode) ?? { snapshot: null };
        if (result.error) {
          postHostMessage({ type: 'error', message: result.error });
          break;
        }
        postHostMessage({
          type: 'snapshot',
          payload: result.snapshot,
          publishMode: mode,
          clipRect: result.clipRect,
        });
        break;
      }
      case 'hyCadAction': {
        postHostMessage({ type: 'hyCadAction', action: message.action });
        break;
      }
      default:
        break;
    }
  } catch (error) {
    postHostMessage({
      type: 'error',
      message: error instanceof Error ? error.message : 'Invalid host command',
    });
  }
}
