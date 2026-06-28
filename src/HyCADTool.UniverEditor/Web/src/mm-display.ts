/**
 * 纸面 mm ↔ 屏幕 CSS 像素（Win11 / WPF DIP 同口径：96px = 1in = 25.4mm）。
 * 与 devicePixelRatio 无关；Canvas backing store 由浏览器按 DPR 放大。
 */
export const DISPLAY_PX_PER_MM = 96 / 25.4;

/** 与 TableEditorViewModel.SeedRowHeightMm / SeedColWidthMm 一致：推导行列数。 */
export const SEED_ROW_HEIGHT_MM = 5;
export const SEED_COL_WIDTH_MM = 25;

/** 样表默认量级（仅作 fallback 文案，纸面重算后以均分 track 为准）。 */
export const DEFAULT_ROW_HEIGHT_MM = 10;
export const DEFAULT_COL_WIDTH_MM = 25;

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
