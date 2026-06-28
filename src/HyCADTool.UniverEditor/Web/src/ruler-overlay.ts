import type { FUniver } from '@univerjs/core/facade';



import { getFormulaBarBottomPx } from './formula-bar-layout';
import { DISPLAY_PX_PER_MM } from './mm-display';

import { getLayoutViewState, subscribeLayoutViewState } from './layout-view-state';

import { isLayoutTabActive, subscribeLayoutTabActive } from './layout-tab-inject';

import { subscribePagePreviewScale } from './page-preview-scale';

import { resolvePageBoxLayout, resolveViewportRulerBand, resolvePixelsPerMm } from './page-box-layout';
import { getPagePreviewScalePxPerMm } from './page-preview-scale';

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



/**
 * 标尺贴图纸外缘（上/左），但测量原点 = 图纸内 A1 左上角（扣除行/列头）。
 * 行表头宽、列表头高仅用于把 0 点推到 A1，不计入图纸刻度范围。
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
  const { rect, pageWidthPx, pageHeightPx, widthMm } = pageBox;
  const ppm = getPagePreviewScalePxPerMm() > 0
    ? getPagePreviewScalePxPerMm()
    : (widthMm > 0 && pageWidthPx > 0 ? pageWidthPx / widthMm : DISPLAY_PX_PER_MM);

  const zoom = ppm / DISPLAY_PX_PER_MM;
  const rowW = showHeaders ? DEFAULT_ROW_HEADER_W * zoom : 0;
  const colH = showHeaders ? DEFAULT_COL_HEADER_H * zoom : 0;

  return {
    cornerLeft: rect.left - RULER_THICKNESS,
    cornerTop: rect.top - RULER_THICKNESS,
    hLeft: rect.left,
    hWidth: pageWidthPx,
    vTop: rect.top,
    vHeight: pageHeightPx,
    hOrigin: rowW,
    vOrigin: colH,
    ppm,
    boundaryWidthMm: Math.max(0, (pageWidthPx - rowW) / ppm),
    boundaryHeightMm: Math.max(0, (pageHeightPx - colH) / ppm),
    hTickClipStart: rowW,
    hTickClipEnd: pageWidthPx,
    vTickClipStart: colH,
    vTickClipEnd: pageHeightPx,
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


