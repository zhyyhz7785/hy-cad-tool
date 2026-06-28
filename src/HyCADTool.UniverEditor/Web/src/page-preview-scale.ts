/** hymd PreviewScale：每 mm 对应的 CSS 像素（px/mm）。 */
const MIN_SCALE = 0.1;
const MAX_SCALE = 5.0;
const ZOOM_FACTOR = 1.06;

let previewScalePxPerMm = 0;
const listeners = new Set<() => void>();

function emit(): void {
  for (const fn of listeners)
    fn();
}

export function getPagePreviewScalePxPerMm(): number {
  return previewScalePxPerMm;
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

export function resetPagePreviewScaleFromFit(fitPxPerMm: number): void {
  if (!Number.isFinite(fitPxPerMm) || fitPxPerMm <= 0)
    return;
  previewScalePxPerMm = Math.max(MIN_SCALE, Math.min(MAX_SCALE, fitPxPerMm));
  emit();
}
