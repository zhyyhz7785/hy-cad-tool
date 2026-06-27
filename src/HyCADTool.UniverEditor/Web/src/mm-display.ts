/** CSS 参考像素/mm；行列同一常数，保证屏幕宽高比 = 纸面 mm 比。 */
export const DISPLAY_PX_PER_MM = 96 / 25.4;

/** 防极端 mm 撑爆视图（不破坏比例，仅 cap 绝对 px）。 */
export const MAX_DISPLAY_PX = 800;

export const DEFAULT_ROW_HEIGHT_MM = 10;
export const DEFAULT_COL_WIDTH_MM = 25;

function clampDisplayPx(px: number): number {
  if (!Number.isFinite(px) || px <= 0)
    return 0;
  return Math.min(px, MAX_DISPLAY_PX);
}

/** 纸面 mm → Univer 显示 px（行/列同一换算）。 */
export function mmToDisplayPx(mm: number, fallbackMm: number): number {
  const value = mm > 0 ? mm : fallbackMm;
  const px = value * DISPLAY_PX_PER_MM;
  return Math.round(clampDisplayPx(px) * 100) / 100;
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
