/** 页面边距（mm），对齐 Hy 桩面板「边距参数」。 */
export interface PageMarginsMm {
  /** 整体轮廓距离：修改时同步上下左右 */
  outlineMm: number;
  top: number;
  bottom: number;
  left: number;
  right: number;
}

export const DEFAULT_PAGE_MARGINS: PageMarginsMm = {
  outlineMm: 10,
  top: 10,
  bottom: 10,
  left: 10,
  right: 10,
};

export function normalizeMarginMm(value: number, fallback = 0): number {
  if (!Number.isFinite(value) || value < 0)
    return fallback;
  return Math.round(value * 100) / 100;
}

export function syncAllMarginsFromOutline(outlineMm: number): PageMarginsMm {
  const v = normalizeMarginMm(outlineMm, DEFAULT_PAGE_MARGINS.outlineMm);
  return { outlineMm: v, top: v, bottom: v, left: v, right: v };
}

export function patchPageMargins(
  current: PageMarginsMm,
  patch: Partial<PageMarginsMm>,
): PageMarginsMm {
  const next: PageMarginsMm = {
    outlineMm: patch.outlineMm ?? current.outlineMm,
    top: normalizeMarginMm(patch.top ?? current.top, current.top),
    bottom: normalizeMarginMm(patch.bottom ?? current.bottom, current.bottom),
    left: normalizeMarginMm(patch.left ?? current.left, current.left),
    right: normalizeMarginMm(patch.right ?? current.right, current.right),
  };
  if (next.top === next.bottom && next.left === next.right && next.top === next.left)
    next.outlineMm = next.top;
  return next;
}

/** C# TableViewport.MarginMm 仍为对称单边距；取左右均值供 Domain 推导。 */
export function marginsToDomainUniformMm(m: PageMarginsMm): number {
  return normalizeMarginMm((m.left + m.right) / 2, DEFAULT_PAGE_MARGINS.left);
}

export interface PageMarginPaddingPx {
  padLeft: number;
  padTop: number;
  padRight: number;
  padBottom: number;
}

export function marginsToPaddingPx(m: PageMarginsMm, ppm: number): PageMarginPaddingPx {
  const scale = ppm > 0 ? ppm : 1;
  return {
    padLeft: Math.max(0, Math.round(m.left * scale)),
    padTop: Math.max(0, Math.round(m.top * scale)),
    padRight: Math.max(0, Math.round(m.right * scale)),
    padBottom: Math.max(0, Math.round(m.bottom * scale)),
  };
}

export function formatMarginDataset(p: PageMarginPaddingPx): string {
  return `${p.padLeft}x${p.padTop}x${p.padRight}x${p.padBottom}`;
}

export function parseMarginDataset(raw: string | undefined): PageMarginPaddingPx {
  if (!raw)
    return { padLeft: 0, padTop: 0, padRight: 0, padBottom: 0 };
  const parts = raw.split('x').map((s) => Number.parseFloat(s));
  if (parts.length >= 4 && parts.every((n) => Number.isFinite(n)))
    return {
      padLeft: Math.max(0, parts[0]),
      padTop: Math.max(0, parts[1]),
      padRight: Math.max(0, parts[2]),
      padBottom: Math.max(0, parts[3]),
    };
  if (parts.length >= 2 && parts.every((n) => Number.isFinite(n)))
    return {
      padLeft: Math.max(0, parts[0]),
      padTop: Math.max(0, parts[1]),
      padRight: Math.max(0, parts[0]),
      padBottom: Math.max(0, parts[1]),
    };
  return { padLeft: 0, padTop: 0, padRight: 0, padBottom: 0 };
}
