import type { UniverViewportMetrics } from './univer-viewport';
import { DISPLAY_PX_PER_MM, mmSizeToDisplayPx } from './mm-display';
import { getLayoutViewState } from './layout-view-state';
import { resolveLayoutPpm } from './page-preview-scale';
import { resolveSheetSizeMm } from './paper-sheet';

export interface PageBoxLayout {
  /** 页面白底区（sheetBox）视口矩形 */
  rect: DOMRect;
  /** 整纸幅面 mm（hymd PageWidthMm / PageHeightMm） */
  widthMm: number;
  heightMm: number;
  /** 当前显示 CSS 像素宽高 */
  pageWidthPx: number;
  pageHeightPx: number;
  /** px/mm，与 hymd PreviewScale 一致 */
  pixelsPerMm: number;
}

export interface ViewportRulerBand {
  /** U7 工作区（gridHost 可视区，不随纸缩放位移） */
  rect: DOMRect;
  contentLeft: number;
  contentTop: number;
  contentWidth: number;
  contentHeight: number;
}

/** 标尺条固定锚点：贴 gridHost 左/上，不跟 sheetBox 缩放移动。 */
export function resolveViewportRulerBand(): ViewportRulerBand | null {
  const gridHost = document.querySelector('#app .hycad-page-grid-host');
  if (!(gridHost instanceof HTMLElement))
    return null;

  const leftAside = gridHost.querySelector(':scope > aside[data-u-comp="left-sidebar"]');
  const rightAside = gridHost.querySelector(':scope > aside[data-u-comp="right-sidebar"]');
  const leftW = leftAside instanceof HTMLElement ? leftAside.offsetWidth : 0;
  const rightW = rightAside instanceof HTMLElement ? rightAside.offsetWidth : 0;
  const rect = gridHost.getBoundingClientRect();

  return {
    rect,
    contentLeft: rect.left + leftW,
    contentTop: rect.top,
    contentWidth: Math.max(0, rect.width - leftW - rightW),
    contentHeight: Math.max(0, rect.height),
  };
}

/** 布局 Tab + 纸张边界开启时，取 U7a 页面盒子；否则回退 canvas 区域。 */
export function resolvePageBoxLayout(metrics: UniverViewportMetrics): PageBoxLayout | null {
  const pageHost = document.querySelector('#app .hycad-page-grid-host');
  const content = document.querySelector('#app [data-range-selector]');
  const sheetBox = content?.parentElement;

  if (pageHost && sheetBox instanceof HTMLElement && sheetBox.style.width) {
    const rect = sheetBox.getBoundingClientRect();
    const state = getLayoutViewState();
    const sheet = resolveSheetSizeMm(state.paperPresetIndex, state.orientation);
    const ppm = resolveLayoutPpm(
      rect.width > 0 && sheet.widthMm > 0 ? rect.width / sheet.widthMm : DISPLAY_PX_PER_MM,
    );
    return {
      rect,
      widthMm: sheet.widthMm,
      heightMm: sheet.heightMm,
      pageWidthPx: rect.width,
      pageHeightPx: rect.height,
      pixelsPerMm: ppm,
    };
  }

  if (metrics.canvasWidth <= 0 || metrics.canvasHeight <= 0)
    return null;

  const state = getLayoutViewState();
  const sheet = resolveSheetSizeMm(state.paperPresetIndex, state.orientation);
  const zoom = metrics.zoom > 0 ? metrics.zoom : 1;
  const px = mmSizeToDisplayPx(sheet.widthMm, sheet.heightMm, zoom);
  return {
    rect: new DOMRect(
      metrics.originX,
      metrics.originY,
      metrics.canvasWidth,
      metrics.canvasHeight,
    ),
    widthMm: sheet.widthMm,
    heightMm: sheet.heightMm,
    pageWidthPx: metrics.canvasWidth,
    pageHeightPx: metrics.canvasHeight,
    pixelsPerMm: px.widthPx / sheet.widthMm,
  };
}

/** 当前 zoom 下每 mm 对应的 CSS 像素（hymd PreviewScale）。 */
export function resolvePixelsPerMm(
  metrics: UniverViewportMetrics,
  pageBox?: PageBoxLayout | null,
): number {
  if (pageBox && pageBox.pixelsPerMm > 0)
    return pageBox.pixelsPerMm;
  if (pageBox && pageBox.widthMm > 0 && pageBox.pageWidthPx > 0)
    return pageBox.pageWidthPx / pageBox.widthMm;

  const zoom = metrics.zoom > 0 ? metrics.zoom : 1;
  return mmSizeToDisplayPx(1, 1, zoom).widthPx;
}
