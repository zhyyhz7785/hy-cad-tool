export interface HyCadViewportPayload {
  paperPresetIndex?: number;
  orientation?: number;
  targetWidthMm?: number;
  marginMm?: number;
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
  marginMm: number;
}

const DEFAULT_STATE: LayoutViewState = {
  showRulers: true,
  showHeaders: true,
  showPaperBoundary: true,
  paperPresetIndex: 1,
  orientation: 0,
  targetWidthMm: 400,
  marginMm: 10,
};

let state: LayoutViewState = { ...DEFAULT_STATE };
const listeners = new Set<(s: LayoutViewState) => void>();

function emit(): void {
  for (const fn of listeners)
    fn(state);
}

export function getLayoutViewState(): LayoutViewState {
  return state;
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

export function resolvePaperHeightMm(
  paperPresetIndex: number,
  orientation: number,
  marginMm: number,
): number {
  const idx = Math.max(0, Math.min(PAPER_HEIGHTS_LANDSCAPE.length - 1, paperPresetIndex));
  const landscape = orientation <= 0;
  const raw = PAPER_HEIGHTS_LANDSCAPE[idx];
  const pageH = landscape ? raw : PAPER_WIDTHS_LANDSCAPE[idx];
  return Math.max(1, pageH - marginMm * 2);
}

export function resolvePaperWidthMm(
  paperPresetIndex: number,
  orientation: number,
  marginMm: number,
  targetWidthOverride?: number,
): number {
  if (typeof targetWidthOverride === 'number' && targetWidthOverride > 0)
    return targetWidthOverride;

  const idx = Math.max(0, Math.min(PAPER_WIDTHS_LANDSCAPE.length - 1, paperPresetIndex));
  const landscape = orientation <= 0;
  const raw = PAPER_WIDTHS_LANDSCAPE[idx];
  const pageW = landscape ? raw : PAPER_HEIGHTS_LANDSCAPE[idx];
  return Math.max(1, pageW - marginMm * 2);
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
    marginMm: typeof payload.marginMm === 'number'
      ? payload.marginMm
      : state.marginMm,
  };
  emit();
}

export function syncPaperFromLayoutInputs(
  paperPresetIndex: number,
  orientation: number,
  targetWidthMm: number,
  marginMm: number,
): void {
  state = {
    ...state,
    paperPresetIndex,
    orientation,
    targetWidthMm,
    marginMm,
  };
  emit();
}
