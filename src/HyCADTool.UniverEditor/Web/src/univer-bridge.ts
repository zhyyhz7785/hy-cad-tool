import type { FUniver } from '@univerjs/core/facade';

export interface HyCadCellSnapshot {
  row: number;
  col: number;
  rowSpan: number;
  colSpan: number;
  text: string;
  editable: boolean;
}
export interface HyCadGridSnapshot {
  rowCount: number;
  colCount: number;
  rowHeightsMm: number[];
  colWidthsMm: number[];
  cells: HyCadCellSnapshot[];
}

declare global {
  interface Window {
    hyCadBridge?: {
      loadSnapshot: (snapshot: HyCadGridSnapshot | string) => boolean;
      exportSnapshot: () => HyCadGridSnapshot | null;
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

function mmToRowPx(mm: number): number {
  const value = mm <= 0 ? 10 : mm;
  return Math.max(22, Math.min(140, value * 2));
}

function mmToColPx(mm: number): number {
  const value = mm <= 0 ? 25 : mm;
  return Math.max(52, Math.min(260, value * 1.6));
}

function readNumber(value: unknown, fallback = 0): number {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : fallback;
}

function normalizeCell(raw: Record<string, unknown>): HyCadCellSnapshot {
  return {
    row: readNumber(raw.row ?? raw.Row),
    col: readNumber(raw.col ?? raw.Col),
    rowSpan: readNumber(raw.rowSpan ?? raw.RowSpan, 1),
    colSpan: readNumber(raw.colSpan ?? raw.ColSpan, 1),
    text: String(raw.text ?? raw.Text ?? ''),
    editable: Boolean(raw.editable ?? raw.Editable ?? true),
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

export function installHyCadBridge(univerAPI: ReturnType<typeof FUniver.newAPI>): void {
  window.hyCadBridge = {
    loadSnapshot(raw: HyCadGridSnapshot | string): boolean {
      const snapshot = normalizeSnapshot(raw);
      if (!snapshot)
        return false;

      const workbook = univerAPI.getActiveWorkbook();
      const sheet = workbook?.getActiveSheet();
      if (!workbook || !sheet)
        return false;

      try {
        ensureSheetSize(univerAPI, snapshot.rowCount, snapshot.colCount);

        for (let r = 0; r < snapshot.rowCount; r++) {
          const height = mmToRowPx(snapshot.rowHeightsMm?.[r] ?? 10);
          sheet.setRowHeight?.(r, height);
        }

        for (let c = 0; c < snapshot.colCount; c++) {
          const width = mmToColPx(snapshot.colWidthsMm?.[c] ?? 25);
          sheet.setColumnWidth?.(c, width);
        }

        const merged = sheet.getMergedRanges?.() ?? [];
        merged.forEach((range: { breakApart?: () => void }) => range.breakApart?.());

        for (const cell of snapshot.cells ?? []) {
          const address = toRangeAddress(cell.row, cell.col, cell.rowSpan ?? 1, cell.colSpan ?? 1);
          const range = sheet.getRange(address);
          range.setValue(cell.text ?? '');
          if ((cell.rowSpan ?? 1) > 1 || (cell.colSpan ?? 1) > 1)
            range.merge();
        }

        return true;
      } catch (error) {
        console.error('[HyCAD] loadSnapshot failed', error);
        return false;
      }
    },

    exportSnapshot(): HyCadGridSnapshot | null {
      const workbook = univerAPI.getActiveWorkbook();
      const sheet = workbook?.getActiveSheet();
      if (!workbook || !sheet)
        return null;

      try {
        const rowCount = sheet.getMaxRows?.() ?? 1;
        const colCount = sheet.getMaxColumns?.() ?? 1;
        const rowHeightsMm: number[] = [];
        const colWidthsMm: number[] = [];
        const cells: HyCadCellSnapshot[] = [];
        const mergedSeen = new Set<string>();

        for (let r = 0; r < rowCount; r++)
          rowHeightsMm.push((sheet.getRowHeight?.(r) ?? 22) / 2);
        for (let c = 0; c < colCount; c++)
          colWidthsMm.push((sheet.getColumnWidth?.(c) ?? 52) / 1.6);

        for (let r = 0; r < rowCount; r++) {
          for (let c = 0; c < colCount; c++) {
            const range = sheet.getRange(`${colToLetter(c)}${r + 1}`);
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
              row: r,
              col: c,
              rowSpan,
              colSpan,
              text,
              editable: true,
            });
          }
        }

        return {
          rowCount,
          colCount,
          rowHeightsMm,
          colWidthsMm,
          cells,
        };
      } catch (error) {
        console.error('[HyCAD] exportSnapshot failed', error);
        return null;
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
    const message = JSON.parse(raw) as { type?: string; payload?: unknown };
    switch (message.type) {
      case 'loadSnapshot': {
        const ok = window.hyCadBridge?.loadSnapshot(message.payload as HyCadGridSnapshot | string) ?? false;
        postHostMessage({ type: ok ? 'snapshotLoaded' : 'error', message: ok ? undefined : 'loadSnapshot failed' });
        break;
      }
      case 'exportSnapshot': {
        const snapshot = window.hyCadBridge?.exportSnapshot();
        postHostMessage({ type: 'snapshot', payload: snapshot });
        break;
      }
      case 'hyCadAction': {
        postHostMessage({ type: 'hyCadAction', action: (message as { action?: string }).action });
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

