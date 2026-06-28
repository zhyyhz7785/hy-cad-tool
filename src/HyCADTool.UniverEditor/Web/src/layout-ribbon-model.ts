import { subscribeLayoutViewState } from './layout-view-state';
import type { PageMarginsMm } from './page-margins';
import { DEFAULT_PAGE_MARGINS } from './page-margins';
import { DEFAULT_CANVAS_SHEET_GAP_MM } from './page-canvas-gap';

export const PAPER_PRESETS = ['A4', 'A3', 'A2', '自定义'] as const;
export const ORIENTATION_OPTIONS = ['横向', '纵向'] as const;
export const TEMPLATE_OPTIONS = ['空白', '人员', '家庭'] as const;

export interface LayoutRibbonModelState {
  showRulers: boolean;
  showHeaders: boolean;
  showPaperBoundary: boolean;
  showGridSize: boolean;
  structureMode: boolean;
  paperPresetIndex: number;
  orientation: number;
  targetWidthMm: number;
  pageMargins: PageMarginsMm;
  canvasSheetGapMm: number;
  rowCount: number;
  colCount: number;
  templateIndex: number;
  rowHeightMm: number | null;
  colWidthMm: number | null;
  scale: number | null;
}

const DEFAULT_STATE: LayoutRibbonModelState = {
  showRulers: true,
  showHeaders: true,
  showPaperBoundary: true,
  showGridSize: false,
  structureMode: true,
  paperPresetIndex: 1,
  orientation: 0,
  targetWidthMm: 400,
  pageMargins: { ...DEFAULT_PAGE_MARGINS },
  canvasSheetGapMm: DEFAULT_CANVAS_SHEET_GAP_MM,
  rowCount: 5,
  colCount: 4,
  templateIndex: 0,
  rowHeightMm: null,
  colWidthMm: null,
  scale: null,
};

let state: LayoutRibbonModelState = { ...DEFAULT_STATE };
const listeners = new Set<() => void>();

function emit(): void {
  for (const fn of listeners)
    fn();
}

export function getLayoutRibbonModelState(): LayoutRibbonModelState {
  return state;
}

export function subscribeLayoutRibbonModel(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function patchLayoutRibbonModel(patch: Partial<LayoutRibbonModelState>): void {
  let changed = false;
  for (const key of Object.keys(patch) as Array<keyof LayoutRibbonModelState>) {
    const next = patch[key];
    if (next === undefined || Object.is(state[key], next))
      continue;
    state = { ...state, [key]: next };
    changed = true;
  }
  if (changed)
    emit();
}

subscribeLayoutViewState((view) => {
  patchLayoutRibbonModel({
    showRulers: view.showRulers,
    showHeaders: view.showHeaders,
    showPaperBoundary: view.showPaperBoundary,
    showGridSize: view.showGridSize,
    paperPresetIndex: view.paperPresetIndex,
    orientation: view.orientation,
    targetWidthMm: view.targetWidthMm,
    pageMargins: view.pageMargins,
    canvasSheetGapMm: view.canvasSheetGapMm,
  });
});
