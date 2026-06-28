import { getLastSnapshotDims } from './univer-bridge';
import { getLayoutRibbonModelState } from './layout-ribbon-model';
import {
  deriveGridCountsFromPaper,
  mmSizeToDisplayPx,
  type MmDisplaySize,
} from './mm-display';
import {
  getLayoutViewState,
  resolvePaperHeightMm,
  resolvePaperWidthMm,
  type LayoutViewState,
} from './layout-view-state';

export type { MmDisplaySize };

export interface PaperDisplaySize extends MmDisplaySize {
  rowCount: number;
  colCount: number;
}

function sumTrackSizes(sizes: number[]): number {
  let total = 0;
  for (const size of sizes) {
    if (Number.isFinite(size) && size > 0)
      total += size;
  }
  return total;
}

/** 纸面可用宽高（mm），与 C# TableEditorViewModel.ResolvePaperAvailable 一致。 */
export function resolvePaperAvailableMm(state: LayoutViewState): { widthMm: number; heightMm: number } {
  return {
    widthMm: resolvePaperWidthMm(
      state.paperPresetIndex,
      state.orientation,
      state.marginMm,
      state.targetWidthMm,
    ),
    heightMm: resolvePaperHeightMm(
      state.paperPresetIndex,
      state.orientation,
      state.marginMm,
    ),
  };
}

/**
 * 页面 U7a 尺寸（mm + 100% zoom 下 CSS px）。
 * 优先用快照 track 累加；否则用纸面可用区（与 Domain 均分后一致）。
 */
export function computePaperDisplaySize(
  state: LayoutViewState,
  zoom: number,
): PaperDisplaySize {
  const paper = resolvePaperAvailableMm(state);
  const model = getLayoutRibbonModelState();
  const derived = deriveGridCountsFromPaper(paper.widthMm, paper.heightMm);

  let widthMm = paper.widthMm;
  let heightMm = paper.heightMm;

  const snapshot = getLastSnapshotDims();
  if (snapshot) {
    const sumW = sumTrackSizes(snapshot.colWidthsMm);
    const sumH = sumTrackSizes(snapshot.rowHeightsMm);
    if (sumW > 0 && sumH > 0) {
      widthMm = sumW;
      heightMm = sumH;
    }
  }

  const px = mmSizeToDisplayPx(widthMm, heightMm, zoom);
  return {
    ...px,
    rowCount: snapshot?.rowCount ?? model.rowCount ?? derived.rowCount,
    colCount: snapshot?.colCount ?? model.colCount ?? derived.colCount,
  };
}

export function getCurrentPaperDisplaySize(zoom: number): PaperDisplaySize {
  return computePaperDisplaySize(getLayoutViewState(), zoom);
}
