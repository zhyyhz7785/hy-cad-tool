/** hymd PreviewScale：每 mm 对应的 CSS 像素（px/mm）。 */
const MIN_SCALE = 0.1;
const MAX_SCALE = 5.0;
const ZOOM_FACTOR = 1.06;

let previewScalePxPerMm = 0;
/** 量化 zoom 反推的实际 ppm（page-viewport 写入；标尺/纸面几何读此值）。 */
let effectivePagePpm = 0;
const listeners = new Set<() => void>();

function emit(): void {
  for (const fn of listeners)
    fn();
}

export function getPagePreviewScalePxPerMm(): number {
  return previewScalePxPerMm;
}

export function getEffectivePagePpm(): number {
  return effectivePagePpm;
}

/** 由 page-viewport 在每次布局应用后写入；触发现有 listeners 供标尺重绘。 */
export function setEffectivePagePpm(next: number): void {
  if (!Number.isFinite(next) || next <= 0)
    return;
  if (Math.abs(next - effectivePagePpm) < 0.0001)
    return;
  effectivePagePpm = next;
  emit();
}

export function subscribePagePreviewScale(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function setPagePreviewScalePxPerMm(next: number): void {
  const clamped = Math.max(MIN_SCALE, Math.min(MAX_SCALE, next));
  if (Math.abs(clamped - previewScalePxPerMm) < 0.0001)
    return;
  previewScalePxPerMm = clamped;
  emit();
}

/** hymd PreviewManager.ApplyPreviewScaleStep */
export function applyPagePreviewScaleStep(step: number): void {
  if (!Number.isFinite(step) || Math.abs(step) < 0.0001)
    return;
  setPagePreviewScalePxPerMm(previewScalePxPerMm * Math.pow(ZOOM_FACTOR, step));
}

/** 布局标尺/纸面几何读 ppm：优先 effective（量化后），回退连续累积值。 */
export function resolveLayoutPpm(fallback = 0): number {
  if (effectivePagePpm > 0)
    return effectivePagePpm;
  if (previewScalePxPerMm > 0)
    return previewScalePxPerMm;
  return fallback;
}

export function resetPagePreviewScaleFromFit(fitPxPerMm: number): void {
  if (!Number.isFinite(fitPxPerMm) || fitPxPerMm <= 0)
    return;
  previewScalePxPerMm = Math.max(MIN_SCALE, Math.min(MAX_SCALE, fitPxPerMm));
  emit();
}
