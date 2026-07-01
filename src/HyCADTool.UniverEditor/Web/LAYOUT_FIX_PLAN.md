# 布局网格溢出 — 统一编排器修复方案

## 根因（已用运行时数据 + 代码追踪确证）

grid-fit 与 page-viewport 各自从 mm 独立推导几何，**从不互相校准**：
- grid-fit：列宽 = `round(可用mm/colCount × DPM)`，zoom=1 基线
- page-viewport：zoom = `appliedPpm/DPM`，**从不读 grid-fit 的真实 colSum**

只有 `colSum × zoom ≈ 纸张内区` 才正确，时序一错（再入 / resize / 收敛循环只动一边）就溢出。
用户已确认"时好时坏"，诊断证明同步帧下 449px < 466px（正确）。

## 方案：page-viewport 成为唯一编排者，grid-fit 降为纯函数

### 核心不变式
```
zoom = 纸张内区px / 实际colSum    （从 grid-fit 真实输出反推，恒等成立）
```

### 固定执行顺序（一个 tick 内）
1. reseed ribbon 行列数（按新纸张）
2. grid-fit 设置格子尺寸（zoom=1 基线）
3. 读回真实 colSum / rowSum
4. page-viewport 设纸张盒子 + 应用 zoom = 纸张内区 / colSum

### 改动清单

**layout-grid-fit.ts**
- 导出 `applyGridFitStep(univerAPI, {reseed}): {colSumPx, rowSumPx} | null` —— 纯函数：reseed→设格子→返回真实 colSum/rowSum，不订阅、不设 zoom
- 导出 `restoreGridBaseline(univerAPI)` —— 离开布局时恢复
- **删除** `installLayoutGridFit` 的全部订阅（unsubTab/View/Ribbon/Snapshot）
- 保留 baseline / reseed / resolveTargetCounts 内部逻辑

**page-viewport.ts（唯一编排者）**
- `applyPageViewport` 改为：进入布局后调用 `applyGridFitStep` 拿真实 colSum，
  zoom = 纸张内区px / colSum（不再用 appliedPpm/DPM）
- settle 收敛循环、resize、所有订阅都走这一条 `applyPageViewport`，自动带上 grid-fit
- 离开布局时调用 `restoreGridBaseline`
- 放宽 ZOOM_EPSILON：force/reset 时绕过早退守卫，最终 zoom 不被吞

**main.ts**
- 保留 `installPageViewport`；`installLayoutGridFit` 改为不订阅（或仅注册 teardown）

### 风险
- grid-fit 的 baseline 恢复逻辑需在 page-viewport 离开布局时正确触发
- colSum 须在 grid-fit 设完格子的同一 tick 读回（Univer 同步写入模型，可同步读）
- 纸张内区px 须用与盒子 padding 一致的口径（纸张mm − 边距mm）× ppm

### 验证
- 切 A4/A3/A2、改边距、拖窗口、Ctrl 滚轮 —— 每次 colSum×zoom 都 ≈ 纸张内区
- 控制台无 DIAG、无震荡
