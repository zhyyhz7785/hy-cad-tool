import {
  DEFAULT_PAGE_MARGINS,
  marginsToDomainUniformMm,
  normalizeMarginMm,
  syncAllMarginsFromOutline,
  type PageMarginsMm,
} from './page-margins';
import {
  DEFAULT_CANVAS_SHEET_GAP_MM,
  normalizeCanvasSheetGapMm,
} from './page-canvas-gap';

export type { PageMarginsMm };

export interface HyCadViewportPayload {
  paperPresetIndex?: number;
  orientation?: number;
  targetWidthMm?: number;
  marginMm?: number;
  marginTopMm?: number;
  marginBottomMm?: number;
  marginLeftMm?: number;
  marginRightMm?: number;
  rowCount?: number;
  colCount?: number;
  templateIndex?: number;
  structureMode?: boolean;
}

/** 纸型可用宽/高（mm），与 C# PaperPresetCatalog 口径一致。 */
const PAPER_WIDTHS_LANDSCAPE = [297, 420, 594, 400] as const;
const PAPER_HEIGHTS_LANDSCAPE = [210, 297, 420, 280] as const;

export interface LayoutViewState {
  showRulers: boolean;
  showHeaders: boolean;
  showPaperBoundary: boolean;
  paperPresetIndex: number;
  orientation: number;
  targetWidthMm: number;
  pageMargins: PageMarginsMm;
  /** 图纸外框与画布/标尺四边间距 mm */
  canvasSheetGapMm: number;
}

const DEFAULT_STATE: LayoutViewState = {
  showRulers: true,
  showHeaders: true,
  showPaperBoundary: true,
  paperPresetIndex: 1,
  orientation: 0,
  targetWidthMm: 400,
  pageMargins: { ...DEFAULT_PAGE_MARGINS },
  canvasSheetGapMm: DEFAULT_CANVAS_SHEET_GAP_MM,
};

let state: LayoutViewState = {
  ...DEFAULT_STATE,
  pageMargins: { ...DEFAULT_PAGE_MARGINS },
};
const listeners = new Set<(s: LayoutViewState) => void>();

function emit(): void {
  for (const fn of listeners)
    fn(state);
}

export function getLayoutViewState(): LayoutViewState {
  return state;
}

/** @deprecated 用 pageMargins；保留供 Domain 对称推导 */
export function getDomainMarginMm(): number {
  return marginsToDomainUniformMm(state.pageMargins);
}

export function subscribeLayoutViewState(listener: (s: LayoutViewState) => void): () => void {
  listeners.add(listener);
  listener(state);
  return () => listeners.delete(listener);
}

export function setShowRulers(on: boolean): void {
  if (state.showRulers === on)
    return;
  state = { ...state, showRulers: on };
  emit();
}

export function setShowHeaders(on: boolean): void {
  if (state.showHeaders === on)
    return;
  state = { ...state, showHeaders: on };
  emit();
}

export function setShowPaperBoundary(on: boolean): void {
  if (state.showPaperBoundary === on)
    return;
  state = { ...state, showPaperBoundary: on };
  emit();
}

export function setPageMargins(margins: PageMarginsMm): void {
  state = { ...state, pageMargins: margins };
  emit();
}

export function setCanvasSheetGapMm(gapMm: number): void {
  const next = normalizeCanvasSheetGapMm(gapMm);
  if (state.canvasSheetGapMm === next)
    return;
  state = { ...state, canvasSheetGapMm: next };
  emit();
}

export function resolvePaperHeightMm(
  paperPresetIndex: number,
  orientation: number,
  marginTopMm: number,
  marginBottomMm = marginTopMm,
): number {
  const idx = Math.max(0, Math.min(PAPER_HEIGHTS_LANDSCAPE.length - 1, paperPresetIndex));
  const landscape = orientation <= 0;
  const raw = PAPER_HEIGHTS_LANDSCAPE[idx];
  const pageH = landscape ? raw : PAPER_WIDTHS_LANDSCAPE[idx];
  return Math.max(1, pageH - marginTopMm - marginBottomMm);
}

export function resolvePaperWidthMm(
  paperPresetIndex: number,
  orientation: number,
  marginLeftMm: number,
  marginRightMm = marginLeftMm,
  targetWidthOverride?: number,
): number {
  if (typeof targetWidthOverride === 'number' && targetWidthOverride > 0)
    return targetWidthOverride;

  const idx = Math.max(0, Math.min(PAPER_WIDTHS_LANDSCAPE.length - 1, paperPresetIndex));
  const landscape = orientation <= 0;
  const raw = PAPER_WIDTHS_LANDSCAPE[idx];
  const pageW = landscape ? raw : PAPER_HEIGHTS_LANDSCAPE[idx];
  return Math.max(1, pageW - marginLeftMm - marginRightMm);
}

function marginsFromPayload(payload: HyCadViewportPayload): PageMarginsMm {
  if (typeof payload.marginTopMm === 'number'
    || typeof payload.marginBottomMm === 'number'
    || typeof payload.marginLeftMm === 'number'
    || typeof payload.marginRightMm === 'number') {
    const top = normalizeMarginMm(payload.marginTopMm ?? state.pageMargins.top, DEFAULT_PAGE_MARGINS.top);
    const bottom = normalizeMarginMm(payload.marginBottomMm ?? state.pageMargins.bottom, DEFAULT_PAGE_MARGINS.bottom);
    const left = normalizeMarginMm(payload.marginLeftMm ?? state.pageMargins.left, DEFAULT_PAGE_MARGINS.left);
    const right = normalizeMarginMm(payload.marginRightMm ?? state.pageMargins.right, DEFAULT_PAGE_MARGINS.right);
    const outline = (top === bottom && left === right && top === left)
      ? top
      : state.pageMargins.outlineMm;
    return { outlineMm: outline, top, bottom, left, right };
  }
  if (typeof payload.marginMm === 'number')
    return syncAllMarginsFromOutline(payload.marginMm);
  return state.pageMargins;
}

export function updateLayoutViewFromViewport(payload: HyCadViewportPayload): void {
  state = {
    ...state,
    paperPresetIndex: typeof payload.paperPresetIndex === 'number'
      ? payload.paperPresetIndex
      : state.paperPresetIndex,
    orientation: typeof payload.orientation === 'number'
      ? payload.orientation
      : state.orientation,
    targetWidthMm: typeof payload.targetWidthMm === 'number'
      ? payload.targetWidthMm
      : state.targetWidthMm,
    pageMargins: marginsFromPayload(payload),
  };
  emit();
}

export function syncPaperFromLayoutInputs(
  paperPresetIndex: number,
  orientation: number,
  targetWidthMm: number,
  pageMargins: PageMarginsMm,
): void {
  state = {
    ...state,
    paperPresetIndex,
    orientation,
    targetWidthMm,
    pageMargins,
  };
  emit();
}
