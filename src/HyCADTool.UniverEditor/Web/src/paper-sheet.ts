import { PAPER_PRESETS } from './layout-ribbon-model';
import { resolvePaperWidthMm } from './layout-view-state';

/** 与 hymd EditorViewModel.PaperDefs 一致：短边 × 长边（mm）。 */
export const PAPER_SHEET_DEFS: ReadonlyArray<{ name: string; short: number; long: number }> = [
  { name: 'A4', short: 210, long: 297 },
  { name: 'A3', short: 297, long: 420 },
  { name: 'A2', short: 420, long: 594 },
  { name: '自定义', short: 400, long: 280 },
];

export function isCustomPaperPreset(presetIndex: number): boolean {
  return presetIndex >= PAPER_PRESETS.length - 1;
}

/** hymd 式整纸尺寸（含装订边外的幅面，如 A2 横 = 594×420）。 */
export function resolveSheetSizeMm(
  presetIndex: number,
  orientation: number,
): { widthMm: number; heightMm: number; label: string } {
  const idx = Math.max(0, Math.min(PAPER_SHEET_DEFS.length - 1, presetIndex));
  const def = PAPER_SHEET_DEFS[idx] ?? PAPER_SHEET_DEFS[1];
  const landscape = orientation <= 0;
  const widthMm = landscape ? def.long : def.short;
  const heightMm = landscape ? def.short : def.long;
  return {
    widthMm,
    heightMm,
    label: `${Math.round(widthMm)}×${Math.round(heightMm)}`,
  };
}

export function orientationLabel(orientation: number): string {
  return orientation > 0 ? '竖' : '横';
}

export function nextOrientation(orientation: number): number {
  return orientation > 0 ? 0 : 1;
}

/** Domain 内容区宽（mm），与 C# PaperPresetCatalog.ResolveTargetWidthMm 一致。 */
export function resolveContentTargetWidthMm(
  paperPresetIndex: number,
  orientation: number,
  pageMargins: { left: number; right: number },
  currentTargetWidthMm: number,
): number {
  if (isCustomPaperPreset(paperPresetIndex))
    return currentTargetWidthMm > 0 ? currentTargetWidthMm : 400;
  return resolvePaperWidthMm(
    paperPresetIndex,
    orientation,
    pageMargins.left,
    pageMargins.right,
  );
}
