import type { FUniver } from '@univerjs/core/facade';

/**
 * 纸张边界（U7）已改为 DOM 区域划分（见 page-viewport.ts）：
 *   外层 gridHost 灰底 = 空白工作区；内层 sheetBox = 页面（白底 + 边框 + 阴影）。
 * 不再在 canvas 上绘制灰底/页框，避免与滚动坐标系冲突。此处保留空壳以兼容旧调用。
 */
export function registerPaperBoundaryExtension(
  _univerAPI: ReturnType<typeof FUniver.newAPI>,
): void {
  // no-op：页面区域划分由 page-viewport.ts 的 DOM 层负责
}
