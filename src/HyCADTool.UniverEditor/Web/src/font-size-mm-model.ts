import type { FUniver } from '@univerjs/core/facade';

import {
  CAD_TEXT_HEIGHTS_MM,
  formatMm,
  mmToPoint,
  pointToMm,
  positiveMm,
  TEXT_HEIGHT_MM,
} from './mm-display';

let textHeightMm: number | null = null;
const listeners = new Set<() => void>();

function emit(): void {
  for (const fn of listeners)
    fn();
}

export function getFontSizeMm(): number | null {
  return textHeightMm;
}

export function subscribeFontSizeMm(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function setFontSizeMm(mm: number | null): void {
  const next = mm == null ? null : formatMm(mm);
  if (Object.is(textHeightMm, next))
    return;
  textHeightMm = next;
  emit();
}

type FontRangeLike = {
  getFontSize?: (type?: string) => number | null;
};

function readSelectionFontSizePt(univerAPI: ReturnType<typeof FUniver.newAPI>): number | null {
  const sheet = univerAPI.getActiveWorkbook()?.getActiveSheet();
  const range = sheet?.getActiveRange?.() as FontRangeLike | null | undefined;
  if (!range?.getFontSize)
    return null;
  return range.getFontSize('cell') ?? range.getFontSize('row') ?? range.getFontSize();
}

/** SelectionChanged 时回填字高(mm)。 */
export function updateFontSizeMmFromSelection(univerAPI: ReturnType<typeof FUniver.newAPI>): void {
  const pt = readSelectionFontSizePt(univerAPI);
  if (pt == null || pt <= 0) {
    setFontSizeMm(null);
    return;
  }
  setFontSizeMm(pointToMm(pt, TEXT_HEIGHT_MM));
}

type SetFontRangeLike = FontRangeLike & {
  setFontSize?: (size: number | null) => unknown;
};

/** 对当前选区应用字高(mm)。 */
export function applyFontSizeMmToSelection(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
  mm: number,
): void {
  const normalized = formatMm(mm);
  if (normalized <= 0)
    return;

  const sheet = univerAPI.getActiveWorkbook()?.getActiveSheet();
  const range = sheet?.getActiveRange?.() as SetFontRangeLike | null | undefined;
  if (!range?.setFontSize)
    return;

  range.setFontSize(mmToPoint(normalized));
  setFontSizeMm(normalized);
}

/** 在 CAD 预设字高档或自定义 0.5mm 步长上增减。 */
export function stepFontSizeMm(current: number | null, delta: 1 | -1): number {
  const base = positiveMm(current, TEXT_HEIGHT_MM);
  const idx = CAD_TEXT_HEIGHTS_MM.findIndex(v => Math.abs(v - base) < 0.001);
  if (idx >= 0) {
    const nextIdx = idx + delta;
    if (nextIdx >= 0 && nextIdx < CAD_TEXT_HEIGHTS_MM.length)
      return CAD_TEXT_HEIGHTS_MM[nextIdx];
    return base;
  }
  return Math.max(0.1, formatMm(base + delta * 0.5));
}
