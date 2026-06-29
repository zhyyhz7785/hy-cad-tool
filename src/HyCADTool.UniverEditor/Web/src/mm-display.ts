/**
 * 纸面 mm ↔ 屏幕 CSS 像素（Win11 / WPF DIP 同口径：96px = 1in = 25.4mm）。
 * 与 devicePixelRatio 无关；Canvas backing store 由浏览器按 DPR 放大。
 */
export const DISPLAY_PX_PER_MM = 96 / 25.4;

/** 纸面 mm ↔ Univer 字号 pt。 */
export const MM_TO_POINT = 72 / 25.4;
export const POINT_TO_MM = 25.4 / 72;

/** 与 TableMmDefaults 同名同值。 */
export const SEED_ROW_HEIGHT_MM = 6.4;
export const SEED_COL_WIDTH_MM = 23.3;
export const FALLBACK_ROW_HEIGHT_MM = 10;
export const FALLBACK_COL_WIDTH_MM = 25;
export const TEXT_HEIGHT_MM = 3.5;
export const CAD_TEXT_HEIGHTS_MM = [2.5, 3.5, 5, 7, 10, 14, 20] as const;

/** 样表默认量级（仅作 fallback 文案，纸面重算后以均分 track 为准）。 */
export const DEFAULT_ROW_HEIGHT_MM = FALLBACK_ROW_HEIGHT_MM;
export const DEFAULT_COL_WIDTH_MM = FALLBACK_COL_WIDTH_MM;

export interface MmDisplaySize {
  widthMm: number;
  heightMm: number;
  widthPx: number;
  heightPx: number;
}

function roundPx(px: number): number {
  return Math.round(px * 100) / 100;
}

/** 单轴 mm → CSS px（不单独 cap，避免宽高比失真；视口 fit 在 page-viewport 做）。 */
export function mmToDisplayPx(mm: number, fallbackMm: number): number {
  const value = mm > 0 ? mm : fallbackMm;
  return roundPx(value * DISPLAY_PX_PER_MM);
}

/** 纸面 mm 矩形 → CSS px，保持宽高比。 */
export function mmSizeToDisplayPx(widthMm: number, heightMm: number, zoom = 1): MmDisplaySize {
  const wMm = widthMm > 0 ? widthMm : 1;
  const hMm = heightMm > 0 ? heightMm : 1;
  const z = zoom > 0 ? zoom : 1;
  return {
    widthMm: wMm,
    heightMm: hMm,
    widthPx: roundPx(wMm * DISPLAY_PX_PER_MM * z),
    heightPx: roundPx(hMm * DISPLAY_PX_PER_MM * z),
  };
}

/** Univer 显示 px → 纸面 mm（export 逆换算）。 */
export function displayPxToMm(px: number, fallbackMm: number): number {
  if (!Number.isFinite(px) || px <= 0)
    return fallbackMm;
  const mm = px / DISPLAY_PX_PER_MM;
  return Math.round(mm * 100) / 100;
}

export function mmToRowDisplayPx(mm: number): number {
  return mmToDisplayPx(mm, DEFAULT_ROW_HEIGHT_MM);
}

export function mmToColDisplayPx(mm: number): number {
  return mmToDisplayPx(mm, DEFAULT_COL_WIDTH_MM);
}

export function rowDisplayPxToMm(px: number): number {
  return displayPxToMm(px, DEFAULT_ROW_HEIGHT_MM);
}

export function colDisplayPxToMm(px: number): number {
  return displayPxToMm(px, DEFAULT_COL_WIDTH_MM);
}

/** 与 TableEditorViewModel.ClampPaperDimension 一致。 */
export function clampPaperGridCount(count: number): number {
  const n = Math.round(count);
  if (!Number.isFinite(n) || n < 1)
    return 1;
  return Math.min(n, 999);
}

/** 纸面可用区 ÷ 种子格距 → 行列数（四舍五入）。 */
export function deriveGridCountsFromPaper(
  availWidthMm: number,
  availHeightMm: number,
): { rowCount: number; colCount: number } {
  return {
    rowCount: clampPaperGridCount(availHeightMm / SEED_ROW_HEIGHT_MM),
    colCount: clampPaperGridCount(availWidthMm / SEED_COL_WIDTH_MM),
  };
}

export function formatMm(value: number): number {
  if (!Number.isFinite(value))
    return 0;
  return Math.round(value * 100) / 100;
}

export function positiveMm(value: number | null | undefined, fallback: number): number {
  if (typeof value !== 'number' || !Number.isFinite(value) || value <= 0)
    return fallback;
  return formatMm(value);
}

export function mmToPoint(textHeightMm: number | undefined): number {
  const mm = positiveMm(textHeightMm, TEXT_HEIGHT_MM);
  return formatMm(mm * MM_TO_POINT);
}

export function pointToMm(pt: number | null | undefined, fallbackMm = TEXT_HEIGHT_MM): number {
  if (typeof pt !== 'number' || !Number.isFinite(pt) || pt <= 0)
    return fallbackMm;
  return formatMm(pt * POINT_TO_MM);
}
