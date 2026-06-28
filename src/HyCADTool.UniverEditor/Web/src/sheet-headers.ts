import type { FUniver } from '@univerjs/core/facade';
import {
  SetColumnHeaderHeightCommand,
  SetRowHeaderWidthCommand,
} from '@univerjs/sheets-ui';
import { subscribeLayoutTabActive } from './layout-tab-inject';
import { getLayoutViewState, subscribeLayoutViewState } from './layout-view-state';

/** 原生行头宽 / 列头高（屏幕像素，不随缩放变化） */
export const GRID_ROW_HEADER_W = 46;
export const GRID_COL_HEADER_H = 20;

const DEFAULT_ROW_HEADER_W = GRID_ROW_HEADER_W;
const DEFAULT_COL_HEADER_H = GRID_COL_HEADER_H;

let lastRowW = DEFAULT_ROW_HEADER_W;
let lastColH = DEFAULT_COL_HEADER_H;

function getWorkbookIds(univerAPI: ReturnType<typeof FUniver.newAPI>): { unitId: string; subUnitId: string } | null {
  const wb = univerAPI.getActiveWorkbook?.();
  if (!wb)
    return null;
  const sheet = wb.getActiveSheet?.();
  if (!sheet)
    return null;
  return { unitId: wb.getId(), subUnitId: sheet.getSheetId() };
}

export async function setHeadersVisible(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
  visible: boolean,
): Promise<void> {
  const ids = getWorkbookIds(univerAPI);
  if (!ids)
    return;

  const rowSize = visible ? lastRowW || DEFAULT_ROW_HEADER_W : 0;
  const colSize = visible ? lastColH || DEFAULT_COL_HEADER_H : 0;

  if (visible) {
    lastRowW = rowSize;
    lastColH = colSize;
  }
  else {
    if (lastRowW <= 0)
      lastRowW = DEFAULT_ROW_HEADER_W;
    if (lastColH <= 0)
      lastColH = DEFAULT_COL_HEADER_H;
  }

  await univerAPI.executeCommand(SetRowHeaderWidthCommand.id, {
    unitId: ids.unitId,
    subUnitId: ids.subUnitId,
    size: rowSize,
  });
  await univerAPI.executeCommand(SetColumnHeaderHeightCommand.id, {
    unitId: ids.unitId,
    subUnitId: ids.subUnitId,
    size: colSize,
  });

  const sheet = univerAPI.getActiveWorkbook?.()?.getActiveSheet?.() as { refreshCanvas?: () => void } | null | undefined;
  sheet?.refreshCanvas?.();

  const root = document.getElementById('app');
  root?.classList.toggle('hycad-headers-hidden', !visible);
}

export function installHeadersToggle(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
): () => void {
  const apply = (): void => {
    void setHeadersVisible(univerAPI, getLayoutViewState().showHeaders);
  };

  const unsubView = subscribeLayoutViewState(apply);
  const unsubTab = subscribeLayoutTabActive(apply);

  return () => {
    unsubView();
    unsubTab();
  };
}
