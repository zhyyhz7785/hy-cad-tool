/** 图纸（sheetBox）外框与画布/标尺四边间距（mm），与 Ribbon「外距」一致。 */
export const DEFAULT_CANVAS_SHEET_GAP_MM = 20;

export function normalizeCanvasSheetGapMm(
  value: number,
  fallback = DEFAULT_CANVAS_SHEET_GAP_MM,
): number {
  if (!Number.isFinite(value) || value < 0)
    return fallback;
  return Math.round(value * 100) / 100;
}
