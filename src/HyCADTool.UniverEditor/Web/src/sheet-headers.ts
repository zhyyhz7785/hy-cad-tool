import type { FUniver } from '@univerjs/core/facade';
import {
  SetColumnHeaderHeightCommand,
  SetRowHeaderWidthCommand,
} from '@univerjs/sheets-ui';
import { subscribeLayoutTabActive } from './layout-tab-inject';
import { getLayoutViewState, subscribeLayoutViewState } from './layout-view-state';
import { refreshGridHeaderLabels } from './grid-ruler-overlay';

/** 原生行头宽 / 列头高（Univer API 恒定值）。CellRegion 不扣 header；可见性见 HeaderBand.visible + CSS。 */
export const GRID_ROW_HEADER_W = 46;
export const GRID_COL_HEADER_H = 20;

function getWorkbookIds(univerAPI: ReturnType<typeof FUniver.newAPI>): { unitId: string; subUnitId: string } | null {
  const wb = univerAPI.getActiveWorkbook?.();
  if (!wb)
    return null;
  const sheet = wb.getActiveSheet?.();
  if (!sheet)
    return null;
  return { unitId: wb.getId(), subUnitId: sheet.getSheetId() };
}

/**
 * HeaderBand.visible：通过 Univer API 把行头宽/列头高切到 0 实现隐藏——
 * 行列头绘制在主 canvas 内部，CSS 选择器无法命中，纯 CSS 隐藏无效。
 * 显示时恢复恒定 GRID_ROW/COL_HEADER 尺寸；配套的 CellRegion 锚点偏移
 * 见 layout-sheet-geometry.cellRegionAnchorOffset(showHeaders)。
 */
export async function setHeadersVisible(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
  visible: boolean,
): Promise<void> {
  const ids = getWorkbookIds(univerAPI);
  if (!ids)
    return;

  await univerAPI.executeCommand(SetRowHeaderWidthCommand.id, {
    unitId: ids.unitId,
    subUnitId: ids.subUnitId,
    size: visible ? GRID_ROW_HEADER_W : 0,
  });
  await univerAPI.executeCommand(SetColumnHeaderHeightCommand.id, {
    unitId: ids.unitId,
    subUnitId: ids.subUnitId,
    size: visible ? GRID_COL_HEADER_H : 0,
  });

  const sheet = univerAPI.getActiveWorkbook?.()?.getActiveSheet?.() as { refreshCanvas?: () => void } | null | undefined;
  sheet?.refreshCanvas?.();

  if (visible)
    refreshGridHeaderLabels(univerAPI);

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
