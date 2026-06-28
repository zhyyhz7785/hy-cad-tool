import type { FUniver } from '@univerjs/core/facade';



import { getFormulaBarBottomPx } from './formula-bar-layout';
import { DISPLAY_PX_PER_MM } from './mm-display';

import { getLayoutViewState, subscribeLayoutViewState } from './layout-view-state';

import { isLayoutTabActive, subscribeLayoutTabActive } from './layout-tab-inject';

import { subscribePagePreviewScale } from './page-preview-scale';

import { resolvePageBoxLayout, resolveViewportRulerBand, resolvePixelsPerMm } from './page-box-layout';
import { getPagePreviewScalePxPerMm } from './page-preview-scale';
import { parseMarginDataset } from './page-margins';

import { installViewportProbe, type UniverViewportMetrics } from './univer-viewport';



const RULER_THICKNESS = 24;

const MINOR_MM = 4;

const FONT = '8px Segoe UI, system-ui, sans-serif';

/** 与 sheet-headers.ts 默认一致；× zoom 得到行/列头像素，用于把标尺 0 点推到 A1。 */
const DEFAULT_ROW_HEADER_W = 46;
const DEFAULT_COL_HEADER_H = 20;

const COLORS = {

  light: {

    bg: '#3b4252',

    label: '#d8dee9',

    major: '#8b949e',

    minor: '#4c566a',

    border: '#4c566a',

    boundary: '#ebcb8b',

    boundaryLabel: '#eceff4',

  },

  dark: {

    bg: '#161b22',

    label: '#8b949e',

    major: '#6e7681',

    minor: '#30363d',

    border: '#30363d',

    boundary: '#d29922',

    boundaryLabel: '#f0f6fc',

  },

};



interface RulerDrawOptions {

  boundaryMm?: number;

  /** 标尺画布坐标：仅在此水平/垂直区间内绘制刻度（px，含端点） */
  tickClipStart?: number;
  tickClipEnd?: number;

}



function isDarkTheme(): boolean {

  return document.documentElement.classList.contains('dark')

    || document.body.classList.contains('dark')

    || Boolean(document.querySelector('#app .dark'));

}



function pickColors() {

  return isDarkTheme() ? COLORS.dark : COLORS.light;

}



function drawBoundaryTickHorizontal(

  ctx: CanvasRenderingContext2D,

  width: number,

  height: number,

  ppm: number,

  originPx: number,

  boundaryMm: number,

  colors: typeof COLORS.light,

): void {

  if (boundaryMm <= 0)

    return;



  const px = originPx + boundaryMm * ppm;

  if (px < 0 || px > width + 0.5)

    return;



  const snap = Math.round(px) + 0.5;

  ctx.strokeStyle = colors.boundary;

  ctx.lineWidth = 1.5;

  ctx.beginPath();

  ctx.moveTo(snap, height);

  ctx.lineTo(snap, 1);

  ctx.stroke();

  ctx.lineWidth = 1;



  ctx.font = FONT;

  ctx.fillStyle = colors.boundaryLabel;

  ctx.textBaseline = 'top';

  const label = String(Math.round(boundaryMm));

  const textW = ctx.measureText(label).width;

  const labelX = px + 2 + textW > width ? px - textW - 2 : px + 2;

  ctx.fillText(label, labelX, 1);

}



function drawBoundaryTickVertical(

  ctx: CanvasRenderingContext2D,

  width: number,

  height: number,

  ppm: number,

  originPx: number,

  boundaryMm: number,

  colors: typeof COLORS.light,

): void {

  if (boundaryMm <= 0)

    return;



  const px = originPx + boundaryMm * ppm;

  if (px < 0 || px > height + 0.5)

    return;



  const snap = Math.round(px) + 0.5;

  ctx.strokeStyle = colors.boundary;

  ctx.lineWidth = 1.5;

  ctx.beginPath();

  ctx.moveTo(width, snap);

  ctx.lineTo(1, snap);

  ctx.stroke();

  ctx.lineWidth = 1;



  ctx.font = FONT;

  ctx.fillStyle = colors.boundaryLabel;

  ctx.textBaseline = 'middle';

  const label = String(Math.round(boundaryMm));

  ctx.fillText(label, 1, px);

}



function drawHorizontalRuler(

  ctx: CanvasRenderingContext2D,

  width: number,

  height: number,

  ppm: number,

  originPx: number,

  colors: typeof COLORS.light,

  options: RulerDrawOptions = {},

): void {

  ctx.clearRect(0, 0, width, height);

  ctx.fillStyle = colors.bg;

  ctx.fillRect(0, 0, width, height);



  ctx.strokeStyle = colors.border;

  ctx.beginPath();

  ctx.moveTo(0, height - 0.5);

  ctx.lineTo(width, height - 0.5);

  ctx.stroke();



  const minorPx = MINOR_MM * ppm;

  if (width <= 0 || minorPx <= 0)

    return;



  const clipStart = options.tickClipStart ?? 0;

  const clipEnd = options.tickClipEnd ?? width;

  if (clipEnd <= clipStart)

    return;



  const startMinor = Math.max(0, Math.floor(-originPx / minorPx));

  let endMinor = Math.ceil((clipEnd - originPx) / minorPx);

  if (typeof options.boundaryMm === 'number' && options.boundaryMm > 0)

    endMinor = Math.min(endMinor, Math.ceil(options.boundaryMm / MINOR_MM));



  ctx.font = FONT;

  ctx.textBaseline = 'top';

  ctx.save();

  ctx.beginPath();

  ctx.rect(clipStart, 0, clipEnd - clipStart, height);

  ctx.clip();



  const boundaryPxH = (typeof options.boundaryMm === 'number' && options.boundaryMm > 0)

    ? originPx + options.boundaryMm * ppm

    : Number.NaN;



  for (let i = startMinor; i <= endMinor; i++) {

    const px = originPx + i * minorPx;

    if (px < clipStart - 0.5 || px > clipEnd + 0.5)

      continue;



    const isMajor = (i % 5) === 0;

    const tickLen = isMajor ? height * 0.6 : height * 0.25;

    const snap = Math.round(px) + 0.5;



    ctx.strokeStyle = isMajor ? colors.major : colors.minor;

    ctx.beginPath();

    ctx.moveTo(snap, height);

    ctx.lineTo(snap, height - tickLen);

    ctx.stroke();



    if (i === 0) {

      ctx.fillStyle = colors.label;

      ctx.fillText('0', px + 2, 1);

    }

    else if (isMajor) {

      // 与图纸尺寸数字重合时，仅保留图纸尺寸数字
      if (Number.isFinite(boundaryPxH) && Math.abs(px - boundaryPxH) < 20)
        continue;

      const mmVal = i * 4;

      ctx.fillStyle = colors.label;

      ctx.fillText(String(mmVal), px + 2, 1);

    }

  }



  ctx.restore();



  if (typeof options.boundaryMm === 'number') {

    ctx.save();

    ctx.beginPath();

    ctx.rect(clipStart, 0, clipEnd - clipStart, height);

    ctx.clip();

    drawBoundaryTickHorizontal(ctx, width, height, ppm, originPx, options.boundaryMm, colors);

    ctx.restore();

  }

}



function drawVerticalRuler(

  ctx: CanvasRenderingContext2D,

  width: number,

  height: number,

  ppm: number,

  originPx: number,

  colors: typeof COLORS.light,

  options: RulerDrawOptions = {},

): void {

  ctx.clearRect(0, 0, width, height);

  ctx.fillStyle = colors.bg;

  ctx.fillRect(0, 0, width, height);



  ctx.strokeStyle = colors.border;

  ctx.beginPath();

  ctx.moveTo(width - 0.5, 0);

  ctx.lineTo(width - 0.5, height);

  ctx.stroke();



  const minorPx = MINOR_MM * ppm;

  if (height <= 0 || minorPx <= 0)

    return;



  const clipStart = options.tickClipStart ?? 0;

  const clipEnd = options.tickClipEnd ?? height;

  if (clipEnd <= clipStart)

    return;



  const startMinor = Math.max(0, Math.floor(-originPx / minorPx));

  let endMinor = Math.ceil((clipEnd - originPx) / minorPx);

  if (typeof options.boundaryMm === 'number' && options.boundaryMm > 0)

    endMinor = Math.min(endMinor, Math.ceil(options.boundaryMm / MINOR_MM));



  ctx.font = FONT;

  ctx.textBaseline = 'middle';

  ctx.save();

  ctx.beginPath();

  ctx.rect(0, clipStart, width, clipEnd - clipStart);

  ctx.clip();



  const boundaryPxV = (typeof options.boundaryMm === 'number' && options.boundaryMm > 0)

    ? originPx + options.boundaryMm * ppm

    : Number.NaN;



  for (let i = startMinor; i <= endMinor; i++) {

    const px = originPx + i * minorPx;

    if (px < clipStart - 0.5 || px > clipEnd + 0.5)

      continue;



    const isMajor = (i % 5) === 0;

    const tickLen = isMajor ? width * 0.6 : width * 0.25;

    const snap = Math.round(px) + 0.5;



    ctx.strokeStyle = isMajor ? colors.major : colors.minor;

    ctx.beginPath();

    ctx.moveTo(width, snap);

    ctx.lineTo(width - tickLen, snap);

    ctx.stroke();



    if (i === 0) {

      ctx.fillStyle = colors.label;

      ctx.textBaseline = 'top';

      ctx.fillText('0', 1, px + 1);

      ctx.textBaseline = 'middle';

    }

    else if (isMajor) {

      // 与图纸尺寸数字重合时，仅保留图纸尺寸数字
      if (Number.isFinite(boundaryPxV) && Math.abs(px - boundaryPxV) < 14)
        continue;

      const mmVal = i * 4;

      ctx.fillStyle = colors.label;

      ctx.fillText(String(mmVal), 1, px);

    }

  }



  ctx.restore();



  if (typeof options.boundaryMm === 'number') {

    ctx.save();

    ctx.beginPath();

    ctx.rect(0, clipStart, width, clipEnd - clipStart);

    ctx.clip();

    drawBoundaryTickVertical(ctx, width, height, ppm, originPx, options.boundaryMm, colors);

    ctx.restore();

  }

}



/** 读取 page-viewport 写入图纸内白边像素（四边 mm × ppm）。 */
function resolveSheetPaddingPx(): {
  padLeft: number;
  padTop: number;
  padRight: number;
  padBottom: number;
} {
  const content = document.querySelector('#app [data-range-selector]');
  const sheetBox = content?.parentElement;
  if (sheetBox instanceof HTMLElement)
    return parseMarginDataset(sheetBox.dataset.hycadMarginPx);
  return { padLeft: 0, padTop: 0, padRight: 0, padBottom: 0 };
}

/**
 * 标尺条固定锚定在画布工作区（gridHost）上/左，位置与宽度不随缩放变化；
 * 仅测量原点（0 = 图纸 A1，扣除行/列头）与刻度间距随缩放变化。
 * 竖标尺左移出窗口时做 clamp，避免数字被裁。
 */
function layoutRulersOnPaper(
  pageBox: NonNullable<ReturnType<typeof resolvePageBoxLayout>>,
  showHeaders: boolean,
): {
  cornerLeft: number;
  cornerTop: number;
  hLeft: number;
  hWidth: number;
  vTop: number;
  vHeight: number;
  hOrigin: number;
  vOrigin: number;
  ppm: number;
  boundaryWidthMm: number;
  boundaryHeightMm: number;
  hTickClipStart: number;
  hTickClipEnd: number;
  vTickClipStart: number;
  vTickClipEnd: number;
} {
  const { rect: paperRect, pageWidthPx, pageHeightPx, widthMm } = pageBox;
  const ppm = getPagePreviewScalePxPerMm() > 0
    ? getPagePreviewScalePxPerMm()
    : (widthMm > 0 && pageWidthPx > 0 ? pageWidthPx / widthMm : DISPLAY_PX_PER_MM);

  const zoom = ppm / DISPLAY_PX_PER_MM;
  const rowW = showHeaders ? DEFAULT_ROW_HEADER_W * zoom : 0;
  const colH = showHeaders ? DEFAULT_COL_HEADER_H * zoom : 0;

  // 图纸内白边（边距）：网格相对纸边内缩 padX/padY（与 page-viewport 写入值一致）
  const { padLeft, padTop, padRight, padBottom } = resolveSheetPaddingPx();

  // A1（图纸内，扣除内白边与行/列头）在视口中的坐标
  const a1Left = paperRect.left + padLeft + rowW;
  const a1Top = paperRect.top + padTop + colH;
  const paperRight = paperRect.right - padRight;
  const paperBottom = paperRect.bottom - padBottom;

  // 标尺条锚在画布工作区，固定不随缩放移动
  const band = resolveViewportRulerBand();
  const formulaBottom = getFormulaBarBottomPx();
  const canvasLeft = band ? band.contentLeft : paperRect.left;
  const canvasRight = band ? band.rect.right : paperRect.right;
  const canvasTop = band ? band.contentTop : paperRect.top;
  const canvasBottom = band ? band.rect.bottom : paperRect.bottom;

  const cornerLeft = Math.max(0, canvasLeft - RULER_THICKNESS);
  const cornerTop = Math.max(0, formulaBottom > 0 ? formulaBottom : canvasTop - RULER_THICKNESS);

  const hLeft = cornerLeft + RULER_THICKNESS;
  const hWidth = Math.max(0, canvasRight - hLeft);
  const vTop = cornerTop + RULER_THICKNESS;
  const vHeight = Math.max(0, canvasBottom - vTop);

  return {
    cornerLeft,
    cornerTop,
    hLeft,
    hWidth,
    vTop,
    vHeight,
    hOrigin: a1Left - hLeft,
    vOrigin: a1Top - vTop,
    ppm,
    boundaryWidthMm: Math.max(0, (paperRight - a1Left) / ppm),
    boundaryHeightMm: Math.max(0, (paperBottom - a1Top) / ppm),
    hTickClipStart: Math.max(0, a1Left - hLeft),
    hTickClipEnd: Math.min(hWidth, paperRight - hLeft),
    vTickClipStart: Math.max(0, a1Top - vTop),
    vTickClipEnd: Math.min(vHeight, paperBottom - vTop),
  };
}



function positionAndDraw(

  hCanvas: HTMLCanvasElement,

  vCanvas: HTMLCanvasElement,

  corner: HTMLElement,

  metrics: UniverViewportMetrics,

  showRulers: boolean,

  showHeaders: boolean,

  showPaperBoundary: boolean,

): void {

  const root = document.getElementById('hycad-editor-root');

  if (!root)

    return;



  if (!showRulers) {

    hCanvas.style.display = 'none';

    vCanvas.style.display = 'none';

    corner.style.display = 'none';

    root.classList.remove('hycad-rulers-on');

    return;

  }



  root.classList.add('hycad-rulers-on');



  const pageBox = showPaperBoundary ? resolvePageBoxLayout(metrics) : null;

  let cornerLeft: number;

  let cornerTop: number;

  let hLeft: number;

  let hWidth: number;

  let vTop: number;

  let vHeight: number;

  let hOrigin: number;

  let vOrigin: number;

  let ppm: number;

  let boundaryWidthMm: number | undefined;

  let boundaryHeightMm: number | undefined;

  let hTickClipStart: number | undefined;

  let hTickClipEnd: number | undefined;

  let vTickClipStart: number | undefined;

  let vTickClipEnd: number | undefined;



  if (pageBox) {

    const layout = layoutRulersOnPaper(pageBox, showHeaders);

    cornerLeft = layout.cornerLeft;

    cornerTop = layout.cornerTop;

    hLeft = layout.hLeft;

    hWidth = layout.hWidth;

    vTop = layout.vTop;

    vHeight = layout.vHeight;

    hOrigin = layout.hOrigin;

    vOrigin = layout.vOrigin;

    ppm = layout.ppm;

    boundaryWidthMm = layout.boundaryWidthMm;

    boundaryHeightMm = layout.boundaryHeightMm;

    hTickClipStart = layout.hTickClipStart;

    hTickClipEnd = layout.hTickClipEnd;

    vTickClipStart = layout.vTickClipStart;

    vTickClipEnd = layout.vTickClipEnd;

  }

  else {

    const band = resolveViewportRulerBand();

    const formulaBottom = getFormulaBarBottomPx();

    ppm = resolvePixelsPerMm(metrics, null);

    if (band) {

      const rulerBandTop = formulaBottom > 0

        ? formulaBottom

        : band.contentTop - RULER_THICKNESS;

      const vTopPx = rulerBandTop + RULER_THICKNESS;

      cornerLeft = Math.max(0, band.contentLeft - RULER_THICKNESS);

      cornerTop = rulerBandTop;

      hLeft = band.contentLeft;

      hWidth = band.contentWidth;

      vTop = vTopPx;

      vHeight = Math.max(0, band.rect.bottom - vTopPx);

      hOrigin = metrics.originX - band.contentLeft - metrics.scrollX;

      vOrigin = metrics.originY - vTopPx - metrics.scrollY;

    }

    else {

      cornerLeft = Math.max(0, metrics.originX - RULER_THICKNESS);

      cornerTop = formulaBottom > 0 ? formulaBottom : metrics.originY - RULER_THICKNESS;

      hLeft = metrics.originX;

      hWidth = Math.max(0, metrics.canvasWidth);

      vTop = cornerTop + RULER_THICKNESS;

      vHeight = Math.max(0, metrics.originY + metrics.canvasHeight - vTop);

      hOrigin = -metrics.scrollX;

      vOrigin = -metrics.scrollY;

    }

  }



  corner.style.display = 'block';

  corner.style.position = 'fixed';

  corner.style.left = `${cornerLeft}px`;

  corner.style.top = `${cornerTop}px`;

  corner.style.width = `${RULER_THICKNESS}px`;

  corner.style.height = `${RULER_THICKNESS}px`;

  corner.style.zIndex = '10000';



  hCanvas.style.display = 'block';

  hCanvas.style.position = 'fixed';

  hCanvas.style.left = `${hLeft}px`;

  hCanvas.style.top = `${cornerTop}px`;

  hCanvas.style.width = `${hWidth}px`;

  hCanvas.style.height = `${RULER_THICKNESS}px`;

  hCanvas.style.zIndex = '10000';



  vCanvas.style.display = 'block';

  vCanvas.style.position = 'fixed';

  vCanvas.style.left = `${cornerLeft}px`;

  vCanvas.style.top = `${vTop}px`;

  vCanvas.style.width = `${RULER_THICKNESS}px`;

  vCanvas.style.height = `${vHeight}px`;

  vCanvas.style.zIndex = '10000';



  const dpr = window.devicePixelRatio || 1;

  if (hCanvas.width !== Math.round(hWidth * dpr) || hCanvas.height !== Math.round(RULER_THICKNESS * dpr)) {

    hCanvas.width = Math.round(hWidth * dpr);

    hCanvas.height = Math.round(RULER_THICKNESS * dpr);

    hCanvas.style.width = `${hWidth}px`;

    hCanvas.style.height = `${RULER_THICKNESS}px`;

  }

  if (vCanvas.width !== Math.round(RULER_THICKNESS * dpr) || vCanvas.height !== Math.round(vHeight * dpr)) {

    vCanvas.width = Math.round(RULER_THICKNESS * dpr);

    vCanvas.height = Math.round(vHeight * dpr);

    vCanvas.style.width = `${RULER_THICKNESS}px`;

    vCanvas.style.height = `${vHeight}px`;

  }



  const colors = pickColors();

  corner.style.background = colors.bg;

  corner.style.borderRight = `1px solid ${colors.border}`;

  corner.style.borderBottom = `1px solid ${colors.border}`;



  const hCtx = hCanvas.getContext('2d');

  const vCtx = vCanvas.getContext('2d');

  if (!hCtx || !vCtx)

    return;



  hCtx.setTransform(dpr, 0, 0, dpr, 0, 0);

  vCtx.setTransform(dpr, 0, 0, dpr, 0, 0);



  drawHorizontalRuler(hCtx, hWidth, RULER_THICKNESS, ppm, hOrigin, colors, {

    boundaryMm: boundaryWidthMm,

    tickClipStart: hTickClipStart,

    tickClipEnd: hTickClipEnd,

  });

  drawVerticalRuler(vCtx, RULER_THICKNESS, vHeight, ppm, vOrigin, colors, {

    boundaryMm: boundaryHeightMm,

    tickClipStart: vTickClipStart,

    tickClipEnd: vTickClipEnd,

  });

}



export function installRulerOverlay(univerAPI: ReturnType<typeof FUniver.newAPI>): () => void {

  const hCanvas = document.getElementById('hycad-hruler') as HTMLCanvasElement | null;

  const vCanvas = document.getElementById('hycad-vruler') as HTMLCanvasElement | null;

  const corner = document.getElementById('hycad-ruler-corner') as HTMLElement | null;



  if (!hCanvas || !vCanvas || !corner)

    return () => {};



  let viewState = getLayoutViewState();

  let lastMetrics: UniverViewportMetrics | null = null;



  const redraw = (metrics: UniverViewportMetrics): void => {

    lastMetrics = metrics;

    const showRulers = viewState.showRulers

      && viewState.showPaperBoundary

      && isLayoutTabActive();

    positionAndDraw(

      hCanvas,

      vCanvas,

      corner,

      metrics,

      showRulers,

      viewState.showHeaders,

      viewState.showPaperBoundary,

    );

  };



  const refresh = (): void => {

    if (lastMetrics)

      redraw(lastMetrics);

  };



  const unsubView = subscribeLayoutViewState((s) => {

    viewState = s;

    refresh();

  });



  const unsubTab = subscribeLayoutTabActive(refresh);

  const unsubScale = subscribePagePreviewScale(refresh);

  const unsubProbe = installViewportProbe(univerAPI, redraw);



  return () => {

    unsubView();

    unsubTab();

    unsubScale();

    unsubProbe();

    hCanvas.style.display = 'none';

    vCanvas.style.display = 'none';

    corner.style.display = 'none';

  };

}


