import type { FUniver } from '@univerjs/core/facade';

import { DISPLAY_PX_PER_MM } from './mm-display';

export interface UniverViewportMetrics {
  zoom: number;
  scrollX: number;
  scrollY: number;
  /** 主网格 canvas 相对视口的 left */
  originX: number;
  /** 主网格 canvas 相对视口的 top */
  originY: number;
  displayPxPerMm: number;
  /** 主网格 canvas 宽度 */
  canvasWidth: number;
  /** 主网格 canvas 高度 */
  canvasHeight: number;
}

export function readZoom(univerAPI: ReturnType<typeof FUniver.newAPI>): number {
  // 首选 FWorksheet.getZoom()（sheets-ui facade mixin，运行时已验证可靠）；
  // getZoomRatio / footer 正则仅作后备。
  try {
    const sheet = univerAPI.getActiveWorkbook?.()?.getActiveSheet?.() as unknown as {
      getZoom?: () => number;
    } | null | undefined;
    const fromSheet = sheet?.getZoom?.();
    if (typeof fromSheet === 'number' && fromSheet > 0)
      return fromSheet;
  }
  catch {
    // ignore
  }

  try {
    const api = univerAPI as unknown as {
      getZoomRatio?: () => number;
      getActiveWorkbook?: () => { getZoomRatio?: () => number } | null;
    };
    const direct = api.getZoomRatio?.();
    if (typeof direct === 'number' && direct > 0)
      return direct;

    const wb = api.getActiveWorkbook?.();
    const fromWb = wb?.getZoomRatio?.();
    if (typeof fromWb === 'number' && fromWb > 0)
      return fromWb;
  }
  catch {
    // ignore
  }

  const footer = document.querySelector('#app footer');
  const text = footer?.textContent ?? '';
  const match = text.match(/(\d{2,3})\s*%/);
  if (match) {
    const pct = Number(match[1]);
    if (Number.isFinite(pct) && pct > 0)
      return pct / 100;
  }

  return 1;
}

export function findScrollElement(): HTMLElement | null {
  const canvas = document.querySelector('#app canvas');
  let node: HTMLElement | null = canvas instanceof HTMLElement ? canvas : null;
  while (node) {
    const style = window.getComputedStyle(node);
    if (/(auto|scroll)/.test(style.overflow + style.overflowX + style.overflowY))
      return node;
    node = node.parentElement;
  }
  return document.querySelector('#app [data-u-comp="workbench-layout"]') as HTMLElement | null;
}

/** 主区 spreadsheet canvas（优先取面积最大的 canvas）。 */
export function findMainSheetCanvas(): HTMLCanvasElement | null {
  const canvases = Array.from(document.querySelectorAll('#app canvas'));
  let best: HTMLCanvasElement | null = null;
  let bestArea = 0;
  for (const node of canvases) {
    if (!(node instanceof HTMLCanvasElement))
      continue;
    const rect = node.getBoundingClientRect();
    const area = rect.width * rect.height;
    if (area > bestArea) {
      bestArea = area;
      best = node;
    }
  }
  return best;
}

export function probeViewport(univerAPI: ReturnType<typeof FUniver.newAPI>): UniverViewportMetrics {
  const zoom = readZoom(univerAPI);
  const scroller = findScrollElement();
  const scrollX = scroller?.scrollLeft ?? 0;
  const scrollY = scroller?.scrollTop ?? 0;

  const canvas = findMainSheetCanvas();
  let originX = 46;
  let originY = 100;
  let canvasWidth = 0;
  let canvasHeight = 0;

  if (canvas) {
    const rect = canvas.getBoundingClientRect();
    originX = rect.left;
    originY = rect.top;
    canvasWidth = rect.width;
    canvasHeight = rect.height;
  }

  return {
    zoom,
    scrollX,
    scrollY,
    originX,
    originY,
    displayPxPerMm: DISPLAY_PX_PER_MM,
    canvasWidth,
    canvasHeight,
  };
}

export type ViewportListener = (metrics: UniverViewportMetrics) => void;

/** 节流监听 zoom/scroll/resize，供 mm 标尺与纸张扩展消费。 */
export function installViewportProbe(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
  listener: ViewportListener,
  intervalMs = 80,
): () => void {
  let timer: number | undefined;

  const emit = (): void => {
    listener(probeViewport(univerAPI));
  };

  const schedule = (): void => {
    window.clearTimeout(timer);
    timer = window.setTimeout(emit, intervalMs);
  };

  window.addEventListener('resize', schedule);
  window.addEventListener('scroll', schedule, true);

  const appRoot = document.getElementById('app');
  let observer: MutationObserver | undefined;
  if (appRoot) {
    observer = new MutationObserver(schedule);
    observer.observe(appRoot, { subtree: true, attributes: true, childList: true });
  }

  const interval = window.setInterval(emit, 500);
  window.setTimeout(emit, 400);
  window.setTimeout(emit, 1200);

  return () => {
    window.clearTimeout(timer);
    window.clearInterval(interval);
    window.removeEventListener('resize', schedule);
    window.removeEventListener('scroll', schedule, true);
    observer?.disconnect();
  };
}
