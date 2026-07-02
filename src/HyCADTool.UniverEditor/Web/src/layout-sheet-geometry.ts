/**
 * 布局页几何：PaperBox / CellRegion / HeaderBand / HeaderBleedViewport
 *
 * 不变式：
 * - grid-fit、snapZoomToGridEdge 只读 CellRegion
 * - HeaderBand 永不参与 cellRegion.width/height
 * - HeaderBleedViewport = Univer DOM 宿主尺寸（CellRegion + header 带锚点偏移）
 * - showHeaders 切换 Univer header 尺寸（0 或恒定值，见 sheet-headers），
 *   锚点偏移/负边距随之归零或恢复；CellRegion 始终不变，A1 恒钉在边距处
 */
import { DISPLAY_PX_PER_MM } from './mm-display';
import { marginsToPaddingPx, type PageMarginsMm } from './page-margins';
import { GRID_COL_HEADER_H, GRID_ROW_HEADER_W } from './sheet-headers';

export interface LayoutPageNodes {
  gridHost: HTMLElement;
  sheetBox: HTMLElement;
  cellViewport: HTMLElement;
}

export interface PaperBox {
  boxW: number;
  boxH: number;
  maxBoxW: number;
  maxBoxH: number;
  gapPx: number;
  padLeft: number;
  padTop: number;
  padRight: number;
  padBottom: number;
  contentW: number;
  contentH: number;
}

/** 边距内单元格可绘区；与行列头开关无关。 */
export interface CellRegion {
  widthPx: number;
  heightPx: number;
  /** 虚拟 header 带在 layout zoom 下的 px，用于 HeaderBleed 负边距 */
  anchorOffsetX: number;
  anchorOffsetY: number;
}

/** Univer 宿主 DOM 尺寸：CellRegion + header 带，负边距伸入 padding。 */
export interface HeaderBleedViewport {
  hostWidthPx: number;
  hostHeightPx: number;
  bleedLeftPx: number;
  bleedTopPx: number;
}

/** 行列头视觉带；尺寸恒定，visible 仅控制 CSS。 */
export interface HeaderBand {
  rowWidth: number;
  colHeight: number;
  visible: boolean;
}

export interface LayoutGeometry {
  paper: PaperBox;
  cellRegion: CellRegion;
  headerBand: HeaderBand;
  headerBleed: HeaderBleedViewport;
}

export function resolveLayoutPageNodes(): LayoutPageNodes | null {
  const cellViewport = document.querySelector('#app [data-range-selector]');
  if (!(cellViewport instanceof HTMLElement))
    return null;

  const sheetBox = cellViewport.parentElement;
  if (!(sheetBox instanceof HTMLElement))
    return null;

  const gridHost = sheetBox.parentElement;
  if (!(gridHost instanceof HTMLElement))
    return null;

  return { gridHost, sheetBox, cellViewport };
}

export function resolveCanvasContentSize(nodes: LayoutPageNodes): { width: number; height: number } {
  return {
    width: Math.max(0, nodes.gridHost.clientWidth),
    height: Math.max(0, nodes.gridHost.clientHeight),
  };
}

/** A1 锚点偏移：行列头可见时为 header 带 × layoutZoom；隐藏时 Univer header 尺寸为 0，偏移归零。 */
export function cellRegionAnchorOffset(appliedPpm: number, showHeaders = true): { x: number; y: number } {
  if (!showHeaders)
    return { x: 0, y: 0 };
  const layoutZoom = appliedPpm / DISPLAY_PX_PER_MM;
  return {
    x: GRID_ROW_HEADER_W * layoutZoom,
    y: GRID_COL_HEADER_H * layoutZoom,
  };
}

export function computeHeaderBleedViewport(cellRegion: CellRegion): HeaderBleedViewport {
  const bleedLeftPx = Math.round(cellRegion.anchorOffsetX);
  const bleedTopPx = Math.round(cellRegion.anchorOffsetY);
  return {
    hostWidthPx: Math.round(cellRegion.widthPx + bleedLeftPx),
    hostHeightPx: Math.round(cellRegion.heightPx + bleedTopPx),
    bleedLeftPx,
    bleedTopPx,
  };
}

export function computeLayoutGeometry(
  nodes: LayoutPageNodes,
  sheetWidthMm: number,
  sheetHeightMm: number,
  ppm: number,
  pageMargins: PageMarginsMm,
  gapMm: number,
  showHeaders: boolean,
): LayoutGeometry | null {
  const canvas = resolveCanvasContentSize(nodes);
  const gapPx = Math.round(gapMm * ppm);
  const maxBoxW = Math.max(0, canvas.width - 2 * gapPx);
  const maxBoxH = Math.max(0, canvas.height - 2 * gapPx);

  const boxW = Math.round(sheetWidthMm * ppm);
  const boxH = Math.round(sheetHeightMm * ppm);

  if (boxW <= 0 || boxH <= 0)
    return null;

  const innerWmm = Math.max(1, sheetWidthMm - pageMargins.left - pageMargins.right);
  const innerHmm = Math.max(1, sheetHeightMm - pageMargins.top - pageMargins.bottom);

  const pad = marginsToPaddingPx(pageMargins, ppm);
  const maxPadLeft = Math.max(0, Math.floor(boxW / 2) - 1);
  const maxPadTop = Math.max(0, Math.floor(boxH / 2) - 1);

  const padLeft = Math.min(Math.max(0, Math.round(pad.padLeft)), maxPadLeft);
  const padTop = Math.min(Math.max(0, Math.round(pad.padTop)), maxPadTop);

  // CellRegion 由内区 mm 直算；右/下 pad 吸收取整残差，保证网格右/下边与边距线重合。
  const widthPx = Math.max(1, Math.round(innerWmm * ppm));
  const heightPx = Math.max(1, Math.round(innerHmm * ppm));
  const padRight = Math.max(0, boxW - padLeft - widthPx);
  const padBottom = Math.max(0, boxH - padTop - heightPx);
  const contentW = widthPx;
  const contentH = heightPx;

  const anchor = cellRegionAnchorOffset(ppm, showHeaders);

  return {
    paper: {
      boxW,
      boxH,
      maxBoxW,
      maxBoxH,
      gapPx,
      padLeft,
      padTop,
      padRight,
      padBottom,
      contentW,
      contentH,
    },
    cellRegion: {
      widthPx,
      heightPx,
      anchorOffsetX: anchor.x,
      anchorOffsetY: anchor.y,
    },
    headerBand: {
      rowWidth: GRID_ROW_HEADER_W,
      colHeight: GRID_COL_HEADER_H,
      visible: showHeaders,
    },
    headerBleed: computeHeaderBleedViewport({
      widthPx,
      heightPx,
      anchorOffsetX: anchor.x,
      anchorOffsetY: anchor.y,
    }),
  };
}
