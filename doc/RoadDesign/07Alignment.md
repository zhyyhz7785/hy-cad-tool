# 07 平面线位（Alignment）— Civil 3D / 鸿业 / HyCADTool 用户操作流程对标

> 性质：**功能与流程对标文档** —— 把 Civil 3D（理论与工具栏最完备）、鸿业市政道路（国内规范 + UI 习惯）、HyCADTool（v1 现状）三方放在同一坐标系下，逐条比对"用户实际怎么用"。
>
> 重点：**用户视角**——按什么键、看到什么、能做什么、缺什么。
>
> 非重点：架构、接口、命名空间、Domain/Service 分层。这些请回查 `01MASTER.md` §2 / `03RoadSelect.md` §3 与源码 XML Doc。
>
> 资料来源：Autodesk Civil 3D 2024–2026 在线帮助、Civil3D.tv 教学站、SolidCAD Blog、HongYe 市政道路 v8 用户手册截图、HyCADTool 当前源码与 06 对标文档。

---

## 0. 文档定位

| 维度       | 取值                                                           |
| ---------- | -------------------------------------------------------------- |
| 性质       | 三方功能对标 + 用户操作流程梳理                                |
| 目标读者   | 道路设计工程师 / HyCADTool 开发者                              |
| 上游依赖   | `Civil3D.md` §1.1（Alignment 总论）、`HongYeRoad.md` §2（PI 法） |
| 下游依赖   | `05计划书.md` P1/P2 看板、`01MASTER.md` P1/P2 阶段             |
| 不重叠     | `06.md` 横断面字段、`03RoadSelect.md` 选择系统                 |
| 维护节奏   | 每完成 v1.x 的一个 Alignment 命令 → 在 §8 勾选并归档            |

---

## 1. Alignment 是什么 —— 三方共识与术语统一

> 三家 CAD 对"平面线位"的定义高度一致，仅命名不同。下表统一术语，本文余下章节按 HyCADTool 的命名为主。

### 1.1 几何元素

| 概念     | Civil 3D                   | 鸿业              | HyCADTool                    | 说明                              |
| -------- | -------------------------- | ----------------- | ---------------------------- | --------------------------------- |
| 直线     | Line / Tangent             | 直线段            | Line / 直线段                | 起点+终点                         |
| 圆曲线   | Curve                      | 圆曲线            | CircularArc / 圆曲线         | 半径 R                            |
| 缓和曲线 | Spiral / Transition        | 缓和曲线 / 回旋线 | Spiral / 缓和曲线            | 参数 A，长度 Ls，A=√(R·Ls)        |
| 缓和类型 | Clothoid/Bloss/Sinusoidal/Cubic | 回旋线（默认）    | Clothoid（v1 仅此一种）      | 见 §1.5                           |
| 复合缓和 | Compound Spiral            | 复合缓和          | ❌                           | 两段不同 R 的圆曲线之间的缓和     |
| 反向缓和 | Reverse Spiral             | 反向缓和          | ❌                           | 两个反向圆曲线之间的"S"型缓和     |

### 1.2 桩号 (Station) 系统

| 概念       | Civil 3D                  | 鸿业                | HyCADTool                              |
| ---------- | ------------------------- | ------------------- | -------------------------------------- |
| 桩号格式   | `1+234.567` / `K1+234.567` | `K1+234.567`         | `K1+234.567`（`Station.Format()`）     |
| 起始桩号   | Starting Station          | 起始桩号 K0+000     | `Alignment.StartStation`（默认 0）     |
| 反向       | Reverse Direction         | 反向                | ❌                                     |
| 参考点     | Reference Point + Reference Station | 起始桩号 + 偏移 | ❌（v1 强制以起点为 K0+xxx） |
| 桩号方程   | Station Equation          | 桩号方程 / 断链     | ❌                                     |
| 主桩间隔   | Major Station（默认 100ft / 100m） | 主桩 20m         | 主桩 20m（`MainInterval=20`）           |
| 副桩间隔   | Minor Station（默认 10ft / 20m）  | 副桩 5m / 10m       | 副桩 5m（`SubInterval=5`）              |

### 1.3 几何点 (Geometry Points)

> 沿 Alignment 的"过渡点"，是国内复测表 / Civil 3D Geometry Point Label 的核心。

| 缩写 | 全称                       | 含义                       | Civil 3D | 鸿业 | HyCAD |
| ---- | -------------------------- | -------------------------- | -------- | ---- | ----- |
| BC   | Begin of Curve             | 直线 → 圆曲线              | ✅       | ✅   | ❌    |
| EC   | End of Curve               | 圆曲线 → 直线              | ✅       | ✅   | ❌    |
| TS   | Tangent → Spiral           | 直线 → 缓和                | ✅       | ✅   | ❌    |
| SC   | Spiral → Curve             | 缓和 → 圆曲线              | ✅       | ✅   | ❌    |
| CS   | Curve → Spiral             | 圆曲线 → 缓和              | ✅       | ✅   | ❌    |
| ST   | Spiral → Tangent           | 缓和 → 直线                | ✅       | ✅   | ❌    |
| PI   | Point of Intersection      | 切线交点（设计点）         | ✅       | ✅   | ✅（PI 表）|
| BP   | Begin Point                | 起点                       | ✅       | ✅   | ✅    |
| EP   | End Point                  | 终点                       | ✅       | ✅   | ✅    |

> HyCADTool v1 在 `hyRoadAlnStation` 里只画了"等距桩号"，没标 BC/EC/TS/SC/CS/ST。**§8.4 第 2 项**已列入 v1.1 必补。

### 1.4 设计速度 (Design Speed)

| 项               | Civil 3D                         | 鸿业                     | HyCADTool                           |
| ---------------- | -------------------------------- | ------------------------ | ----------------------------------- |
| 多速度分段       | ✅ 一条 Alignment 多个段，每段一个速度 | ✅ 同 Civil 3D            | ❌（v1 整条统一一个速度）           |
| 多速度时取值     | 取最大值做规范校核               | 取最大值                 | ❌                                  |
| 速度→最小 R 查表 | XML 设计规范文件                 | 内置 CJJ 37/45/152 表    | 硬编码 `MinRadiusTable`（CJJ 37+152）|
| 速度选项         | 任意                             | 20/30/40/50/60/80/100    | 同鸿业（`AvailableSpeeds`）         |

### 1.5 缓和曲线 (Spiral) 类型

| 类型              | 数学特征                                     | Civil 3D | 鸿业 | HyCAD | 主要用途           |
| ----------------- | -------------------------------------------- | -------- | ---- | ----- | ------------------ |
| Clothoid（回旋线） | 曲率沿弧长线性增长，A=√(R·Ls)                | ✅ 默认  | ✅ 默认 | ✅ 唯一 | 中国 / 欧洲 公路   |
| Bloss              | shift (P) 较小，过渡更长 K 较大              | ✅       | ❌   | ❌    | 铁路（德标）       |
| Sinusoidal         | 曲率呈正弦                                   | ✅       | ❌   | ❌    | 罕见，过陡         |
| Cubic              | 立方型 spiral                                | ✅       | ❌   | ❌    | 老英标             |
| Sine 半波 / NSW    | 国别专用                                     | ✅       | ❌   | ❌    | 澳大利亚等         |

> 国内 CJJ 37 / GB 5768 默认 Clothoid，HyCADTool v1 锁死 Clothoid 不亏。v2 接铁路项目时再扩。

---

## 2. 创建 Alignment 的 5 类工作流

> 三方加起来共识 5 类创建方式。本节先列 Civil 3D 全套（最完整）做基线，再看鸿业、HyCADTool。

### 2.1 Civil 3D 工作流概览

入口：**Home tab → Create Design panel → Alignment dropdown → Alignment Creation Tools**。

弹两步：

1. **Create Alignment - Layout 对话框**：填 Name / Type（Centerline / Offset / Curb Return / Rail / Miscellaneous）/ Starting Station / Style / Layer / Label Set / **Design Criteria**（重要！选 xml 文件即开实时校核）→ OK。
2. 弹 **Alignment Layout Tools** 工具栏（约 14 + 个图标，分两组）。

工具栏分两组：

- **Freehand 自由组**：Quick Layout 类，按"鼠标点击 + 当前 Curve&Spiral Settings 默认值"快速创建。
- **Constraint-Based 约束组**：参数式建单段，按 **Fixed / Floating / Free** 三种约束 × **Line / Curve / Spiral** 9 类组合 + Reverse Spiral / Compound Spiral 等扩展。

### 2.2 Civil 3D：Quick Layout（PI 法，对标鸿业 / HyCAD 主用法）

| 步骤 | 操作                                                                                  | 命令行 / UI 反馈                                |
| ---- | ------------------------------------------------------------------------------------- | ----------------------------------------------- |
| 0    | 工具栏下拉 → **Curve and Spiral Settings**                                            | 弹对话框：Type=Clothoid / Default Radius=300 / Spiral In ☑ / Spiral Out ☑ |
| 1    | 工具栏选 **Tangent-Tangent (With Curves)**（含圆角）或 **(No Curves)**（不加圆角）     | "Specify start point:"                          |
| 2    | 点 PI₁                                                                                | "Specify next point:"                           |
| 3    | 点 PI₂ → PI₃ → PI₄ ...                                                                | 每点一个内部 PI 自动加：缓 + 圆 + 缓             |
| 4    | 右键 / Esc                                                                            | 结束；Alignment 出现，几何点自动标               |

**关键点**：

- **Curve and Spiral Settings** 的默认值是 Quick Layout 的灵魂；不进这个对话框就只能拿到 Civil 3D 内置默认（300m / 60m），常会和 Design Criteria 冲突。
- **Tangent-Tangent (With Curves)**：每个内部 PI 自动按当前默认 R / Spiral In/Out 加圆角。
- **Tangent-Tangent (No Curves)**：先把折线骨架画完，后续再回来用 **Free Curve Fillet** 单独加圆角。

### 2.3 Civil 3D：约束式 Free / Fixed / Floating（精细做法）

> 这是 Civil 3D 区别于鸿业 / HyCAD 的最大特色。约束式不点 PI，而是**逐段选实体**约束几何。

#### 2.3.1 三类约束的核心规则

| 类型         | 端点依附                  | 编辑时行为                     | 典型用法                        |
| ------------ | ------------------------- | ------------------------------ | ------------------------------- |
| **Fixed 固定** | 不依附其他段               | 改参数时自身位置 / 形状变化     | 起点段 / 已知坐标的段           |
| **Floating 浮动** | 一端依附另一段（保切线）    | 端点跟着前段动                 | 接续段，比如"接前段终点的曲线"    |
| **Free 自由** | 两端都依附（保两端切线）    | 两端都被夹住，几何被两端约束    | 中间圆角 / 缓和（最常用）        |

#### 2.3.2 9 类组合 + 扩展（工具栏图标顺序）

```
 Fixed Line   - Two Points
 Fixed Line   - Best Fit
 Fixed Curve  - Three Points / Center+Radius / Two Points + Direction
 Fixed Curve  - Best Fit
 Fixed Spiral - From Curve, End on Object
 Floating Line  - From Curve, Through Point
 Floating Curve - From Entity, End on Object
 Floating Curve - From Entity, Through Point
 Floating Spiral- From Curve to Point
 Free Line   - Between Two Curves
 Free Curve Fillet (Between Two Entities, Radius)
 Free Spiral - Between Two Entities
 Free Spiral-Curve-Spiral (Between Two Entities)
 Free Spiral-Line-Spiral  (Between Two Curves)
 Free Reverse Spiral
 Free Compound Spiral
 Convert AutoCAD Line and Arc
 Insert PI / Delete PI / Break Apart PI
 Reverse Sub-entity Direction
 Sub-entity Editor / Alignment Grid View
```

#### 2.3.3 典型场景：在两段已存在的 Fixed Line 之间插 Free Spiral-Curve-Spiral

| 步骤 | 操作                                            | 输入参数                              |
| ---- | ----------------------------------------------- | ------------------------------------- |
| 1    | 工具栏 → Free 类 → **Free Spiral-Curve-Spiral** | —                                     |
| 2    | 命令行 "Select first entity:"                   | 点前一段 Fixed Line                   |
| 3    | 命令行 "Select next entity:"                    | 点后一段 Fixed Line                   |
| 4    | "Specify pass-through point:"                   | 在大概位置点一下，约束圆曲线穿过该点 |
| 5    | "Specify radius:"                               | 输 R（如 200）                        |
| 6    | "Specify spiral A in:"                          | 输 Aᵢₙ 或 Lsᵢₙ                        |
| 7    | "Specify spiral A out:"                         | 输 Aₒᵤₜ 或 Lsₒᵤₜ                      |

如果 Design Criteria 文件已加载且 R<最小值 → 命令行立即给警告 + 子单元出 ⚠ 黄三角。

### 2.4 Civil 3D：其他 3 种创建方式（覆盖度参考）

| 方式                              | 入口                                  | 用法                              |
| --------------------------------- | ------------------------------------- | --------------------------------- |
| **Create from Polyline**           | Alignment dropdown → Create Alignment from Objects | 先画 Polyline → 转 Alignment（最快但失参数） |
| **Create Best Fit Alignment**      | Alignment dropdown → Create Best Fit Alignment | 给一组测量点 → 自动拟合最优线位 |
| **Create Reference Alignment**     | Project → Reference                   | 引用其他 DWG / 项目里的 Alignment |
| **Create Offset Alignment**        | Modify → Create Offset Alignment      | 从主线偏移 N 米建并行辅道线        |

### 2.5 鸿业的 4 种创建方法（截图自《鸿业市政道路 v8 用户手册》§3.2）

| 方法     | 适用场景                              | 操作要点                                                    |
| -------- | ------------------------------------- | ----------------------------------------------------------- |
| **PI 法** | 已知交点坐标 + R + Ls                  | 菜单"道路 → 平面 → 交点法"，逐 PI 输 X Y R Ls，可批量从 Excel 粘 |
| **参数法** | 已知起点 + 各段长 + 方位角             | 菜单"参数法"，逐段输 长 / 方位角 / R / Ls                    |
| **连接法** | 两条 Alignment 末段切线接驳            | 菜单"连接法"，分别选两端点 + 一致 R                           |
| **块法**   | 复用已有标准弯道                       | 菜单"块法"，从图块库拖入弯道，自动适配两侧切线                |

辅助工具：

- **拾取多段线**：现有 LWPOLYLINE / 3DPOLY → 自动断成"直线 + 圆曲线"段。
- **Excel 粘交点表**：复制 Excel → 鸿业菜单"交点表 → 粘贴" → 直接生成 Alignment。

### 2.6 HyCADTool 的现有 2 种创建方法

#### 2.6.1 `hyRoadA` —— 从已有 Polyline 导入

| 步骤 | 操作                                                                  | 命令行 / 反馈                                                                       |
| ---- | --------------------------------------------------------------------- | ----------------------------------------------------------------------------------- |
| 1    | 命令行输 `hyRoadA`                                                    | "[道路] 选择一条多段线作为平面线位中心线："                                         |
| 2    | 拾取 LWPOLYLINE                                                       | 仅接受 2D `Polyline`（`Polyline3d` / `Line` / `Spline` 暂不支持）                   |
| 3    | 自动写 Xdata + 登记 Domain + 同步落 JSON                              | "[道路] 平面线位已登记 Alignment-1：Id=…，顶点 5 个，含弧段 2 段，平面长度 1235.421 m" |
| 4    | 视觉是弧但 bulge=0 时自动给修复建议                                    | "PEDIT → S 拟合 / 未按 A 切弧" 两类原因 + 修复指引                                 |

> 对应 Civil 3D 的 Create Alignment from Objects；对应鸿业的"拾取多段线"。

#### 2.6.2 `hyRoadAlnByPi` —— PI 表法（三通道输入）

| 步骤    | 操作                                                                                                                                 | 命令行 / 反馈                                                                                                                                                                      |
| ------- | ------------------------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Step 1  | 命令行输 `hyRoadAlnByPi`                                                                                                             | "[道路] 选择 PI 输入方式 [点取(P)/导入CSV(F)/剪贴板(C)] <P>："                                                                                                                  |
| Step 2a | **点取 (P)**：图上连续点 PI                                                                                                          | 每点完一个内部 PI 立即追问 "PI[i] 圆曲线 R（0=折线）" → "PI[i] 入侧缓和曲线 Ls_in（0=无）" → "PI[i] 出侧缓和曲线 Ls_out（0=无）"，默认值复用上一次输入                          |
| Step 2b | **CSV (F)**：选 `.csv` 文件                                                                                                          | 列顺序 `x,y[,R,Ls_in,Ls_out,tag]`；解析出错 → 命令行打印每条错误                                                                                                                  |
| Step 2c | **剪贴板 (C)**：从 Excel 复制 → 命令直接读                                                                                           | 同 CSV 的列规则；STA 线程 3s 超时保护                                                                                                                                              |
| Step 3  | **干跑预览**：先在命令行打印 PI 表 + 总览 + 每段诊断（i / X / Y / 转角° / R / Ls / T / 状态），再追问 "[接受(A)/重选半径(R)/取消(X)] <A>" | 状态包含：圆曲线 / 直-缓-圆-缓-直 / 跳过（带原因，如"切线长 > 段长"） / 折线                                                                                                       |
| Step 4  | "R" → 一次性把所有 R=0 的 PI 改成同一半径 → 重新预览                                                                                  | 解决"CSV 半径列留空"的常见场景                                                                                                                                                     |
| Step 5  | "A" → 写 DWG + Xdata + JSON + 自检                                                                                                   | "[道路] 导线法生成 Alignment-1：PI 5 个，圆角 3 段，缓和 2 段，折线 0 段，跳过 0 段，顶点 47 个，平面长度 1235.421 m。（输入通道：CSV(line.csv)）"                            |

> 对应 Civil 3D 的 **Tangent-Tangent (With Curves)** + **Curve and Spiral Settings**，但功能更强：
>
> - ✅ 三通道输入（Civil 3D 只有点取，鸿业有 Excel 粘贴）
> - ✅ 干跑预览 + 批量改 R（Civil 3D 没有，必须先建后改）
> - ✅ 每个内部 PI 单独配 R / Ls_in / Ls_out（Civil 3D 用工具栏默认值，每条 PI 独立改要进 Sub-Entity Editor）
> - ❌ 无 **Curve and Spiral Settings** 默认值面板 —— 每次都从 R=30 / Ls=0 起步（v1.1 §8.4）

### 2.7 三方创建工作流对照表

| 工作流                       | Civil 3D                              | 鸿业                       | HyCADTool v1.0          |
| ---------------------------- | ------------------------------------- | -------------------------- | ----------------------- |
| 现有 Polyline 转 Alignment   | Create Alignment from Objects         | 拾取多段线                 | ✅ `hyRoadA`            |
| PI 法 / 交点法               | Tangent-Tangent (With Curves)         | 平面 → 交点法              | ✅ `hyRoadAlnByPi`-P/F/C |
| 参数法（长度+方位角）        | 约束式 Fixed Line - Two Points 链式    | 平面 → 参数法              | ❌                      |
| 连接法（两线接驳）           | Free Curve Fillet                     | 平面 → 连接法              | ❌                      |
| 块法（复用标准弯道）         | Civil Cells（OpenRoads 才有）         | 平面 → 块法                | ❌                      |
| Best Fit（测量点拟合）       | Create Best Fit Alignment             | 部分支持                   | ❌                      |
| Reference（引用其他项目）    | Create Reference Alignment            | ❌                         | ❌                      |
| Offset（偏移辅道）           | Create Offset Alignment               | 平行线                     | ❌                      |
| 单段约束式（Free/Fixed/Float） | 14+ 工具栏命令                       | ❌                         | ❌                      |

---

## 3. 编辑 Alignment 的工作流

### 3.1 Civil 3D：Sub-Entity Editor + Alignment Layout Tools

入口：选 Alignment → 右键 → **Edit Alignment Geometry** → 弹回 Alignment Layout Tools 工具栏。

| 步骤 | 操作                                          | UI 反馈                                                                 |
| ---- | --------------------------------------------- | ----------------------------------------------------------------------- |
| 1    | 工具栏点 **Sub-Entity Editor**                | 弹 **Alignment Layout Parameters** 浮动窗口                             |
| 2    | 点 Alignment 中某段（或在 Alignment Entities 表里选行） | 浮动窗显示该段的全部参数（黑色字段可编辑，灰色字段是派生）                |
| 3    | 改 R 或 Length 或 Spiral A                    | 实时刷新：DWG 中曲线动 + 表里下游段的 Station 跟着变 + 切线约束自动维护 |
| 4    | 子单元违反 Design Criteria                    | 表里和 DWG 中出现 ⚠ 黄三角，悬停看哪条规范 + 怎么改                     |

辅助命令（同一个工具栏）：

- **Insert PI**：在 Fixed Line 上指定一点 → 拆成两段 + 新 PI（用于"中间加一个交点"）。
- **Delete PI**：选 PI → 直接合并两侧 Tangent 成一段。
- **Break Apart PI**：把 Free 关系打断（一段曲线 → 两段独立）。
- **Reverse Sub-entity Direction**：单段反向。
- **Reverse Direction**：整条 Alignment 反向（桩号方向反转）。

### 3.2 Civil 3D：Grip Edit（夹点编辑，最快的微调）

选 Alignment → 出现彩色夹点：

| 夹点颜色 | 含义                | 拖动行为                         |
| -------- | ------------------- | -------------------------------- |
| 蓝方块   | PI 点               | 拖动 → PI 移位，两侧切线 + 圆角自动跟          |
| 三角形   | 起 / 终点           | 拖动 → Alignment 端点变          |
| 圆形     | 半径夹点（圆曲线中点） | 拖动垂直方向 → R 实时变          |
| 菱形     | Spiral A 夹点       | 拖动 → A 实时变                  |

### 3.3 鸿业：双击 PI 入参数面板 + 拖拽

- **双击 PI**：弹"交点参数"对话框 → 改 R / Ls_in / Ls_out / 速度，确定后整段自动重算。
- **拖 PI**：直接拖动 PI 标记 → 两侧切线 / 圆角自动跟。
- **右键 PI**：菜单"插入 PI / 删除 PI / 反向"。
- **批量改速度**：菜单"道路 → 设计速度" → 弹列表（每段一个速度）→ 改完整体重算。

### 3.4 HyCADTool：`hyRoadAlnEditPi`（窗口 + 命令行双模）

| 步骤   | 操作                                                                                                          | 命令行 / UI 反馈                                                                                                                                                                                  |
| ------ | ------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Step 1 | 命令行输 `hyRoadAlnEditPi`                                                                                    | "[道路] 拾取要编辑的平面线位（HY_ROAD Alignment）："                                                                                                                                            |
| Step 2 | 选挂了 HY_ROAD Xdata 的 Polyline                                                                              | 没挂 Xdata → 提示先跑 `hyRoadA` / `hyRoadAlnByPi`；不是 PI 法创建 → 提示"老 JSON 无 PI 表快照"                                                                                                |
| Step 3 | "[道路] 选择交互方式 [窗口(W)/命令行(C)] <窗口>："                                                            | 默认窗口；命令行模式给脚本 / 批处理用                                                                                                                                                           |
| Step 4 | 命令行打印 PI 表（idx / X / Y / R / Ls_in / Ls_out / Tag），首尾标 *                                       | 用户输 PI 序号（仅 3 个 PI 时自动选中间那个）                                                                                                                                                   |
| Step 5a | **窗口模式**：弹 **PiThreeUnitWindow**（三 Tab）                                                              | Tab 1 参数：设计速度 V / 圆曲线 R / 对称锁 Ls1=Ls2 / Ls1 / Ls2 + 派生量（α / T1 / T2 / Ly / 前后切线）。<br>Tab 2 规范检查：6 项 ✓✗ 列表（红黄绿）。<br>Tab 3 示意图：缓-圆-缓 SVG 示意 + 实时数值。 |
| Step 5b | 改参数时窗口实时算 → 通过 `RoadAlignmentPreviewService` 把新几何画到 DWG 的"Transient 临时图层"做预览           | 用户能边改边看 DWG 中的红色虚线预览                                                                                                                                                              |
| Step 6  | "确定"（违反规范时仍可强制）→ 整条 Alignment 重建 + 写 DWG + JSON + 自检                                       | "[道路] PI[2] 已更新：R=200, Ls_in=80, Ls_out=80。重建后 顶点=53, 圆角=3, 缓和=2, 跳过=0, 平面长度=1289.012 m"                                                                                  |
| Step 5c | **命令行模式**：依次 Prompt 新 R / 新 Ls_in / 新 Ls_out                                                       | 老脚本 / 批处理友好                                                                                                                                                                              |

> 对应 Civil 3D 的 **Sub-Entity Editor**，但目前只能改一个 PI；批量改、Insert/Delete PI、Reverse 都没有。

### 3.5 三方编辑工作流对照表

| 操作               | Civil 3D                       | 鸿业                  | HyCADTool v1.0                 |
| ------------------ | ------------------------------ | --------------------- | ------------------------------ |
| 改单个 PI 参数     | Sub-Entity Editor              | 双击 PI               | ✅ `hyRoadAlnEditPi` (W)        |
| 改单段长度 / 半径  | Sub-Entity Editor              | 双击曲线              | ⚠ 只能从 PI 进，不能直接改段   |
| 拖 PI / 拖端点     | Grip Edit                      | 拖 PI                 | ❌                             |
| 拖半径（动态改 R） | Grip Edit                      | 拖圆弧                | ❌                             |
| 插入 PI            | Insert PI                      | 右键 → 插入 PI         | ❌                             |
| 删除 PI            | Delete PI                      | 右键 → 删除 PI         | ❌                             |
| 反向               | Reverse Direction              | 反向                  | ❌                             |
| 多段批量改速度     | Design Speeds 选项卡           | 设计速度 → 多段        | ❌                             |
| 实时几何预览       | 工具栏改完即变                 | 双击改完即变           | ✅ Transient 临时图层（窗口模式）|
| 实时规范校核       | ⚠ 黄三角                       | 弹窗提示              | ✅ 6 项 ✓✗ 列表（窗口 Tab 2）   |
| 强制提交（违反规范） | 关掉 Criteria 即可             | "强制确定"             | ✅ NonCompliantConfirm 二次确认 |

---

## 4. 桩号系统 (Station) 操作流程

### 4.1 Civil 3D：Station Control + Reference Point + Equation

入口：选 Alignment → 右键 → **Alignment Properties** → **Station Control 选项卡**。

| 区域                  | 字段                                  | 作用                                                                |
| --------------------- | ------------------------------------- | ------------------------------------------------------------------- |
| **Reference Point**    | 坐标（点取按钮）                      | 选 Alignment 上某一点作为参考点（默认起点）                         |
| **Reference Station**  | 数值（如 1+000）                       | 把上面那个参考点的桩号定义为这个数（默认 0+000）                    |
| **Station Equations**  | Add Station Equation 按钮 / 表        | 加一行桩号方程                                                      |
| 表字段                | Station Back / Station Ahead / Increase or Decrease / Equation # / Comment | 桩号方程的 4 元 + 说明                                              |

**桩号方程操作**：

| 步骤 | 操作                                              | 字段                                                                     |
| ---- | ------------------------------------------------- | ------------------------------------------------------------------------ |
| 1    | Station Control 选项卡 → **Add Station Equation** | —                                                                        |
| 2    | 在 DWG 上点参考点                                 | Station Back 自动填该点桩号                                              |
| 3    | 输 Station Ahead                                  | 该点之后桩号从这个值重新开始                                            |
| 4    | 选 Increase / Decrease                            | 新段桩号方向                                                            |
| 5    | OK                                                | 桩号在该点出现"K0+523.4 = K1+000.0" 跳变；下游段桩号按新方程编号        |

> 国内常见场景：路改造在某点改用新里程（如老路桩号 K0+523.4，市政重新编号 K1+000.0）。

### 4.2 鸿业：起始桩号 + 桩号方程（断链）

- **起始桩号**：菜单"道路 → 平面 → 设计参数" → 弹"起始桩号 K0+000"输入。
- **断链**：菜单"道路 → 桩号 → 加断链" → 在图上点参考点 → 输前桩号 / 后桩号 → 自动计算长断链 / 短断链。
- **桩号反向**：菜单"道路 → 桩号 → 反向"。

### 4.3 HyCADTool v1.0：仅起始桩号字段

实现：

- `Alignment.StartStation`（默认 0）—— 整条 Alignment 唯一参考点 = 起点。
- `Station.Format()` —— 把任意 m 数转 `K{km}+{m:000.000}` 字符串。

缺失（v1.1+ 待补，§8.4-3）：

- ❌ Reference Point + Reference Station（参考点不一定是起点）
- ❌ Station Equation（桩号方程 / 断链）
- ❌ Reverse Direction（整条反向）

### 4.4 三方对照表

| 项                 | Civil 3D                              | 鸿业                  | HyCADTool v1.0      |
| ------------------ | ------------------------------------- | --------------------- | ------------------- |
| 起始桩号           | Reference Point + Reference Station    | 起始桩号 K0+xxx       | ✅ `StartStation`    |
| 起始点 ≠ 参考点    | ✅                                    | ✅                    | ❌（强制起点参考）   |
| 桩号方程 / 断链    | ✅ Station Equations 表                | ✅ 加断链              | ❌                  |
| 桩号反向           | ✅ Reverse Direction                   | ✅                    | ❌                  |
| 桩号格式           | `1+234.567` / `K1+234.567` 可切      | `K1+234.567` 固定     | `K1+234.567` 固定   |
| 单位               | 公制 / 英制可切                       | 公制                  | 公制                |

---

## 5. 桩号标注 (Labels) 工作流

### 5.1 Civil 3D：Add Labels 7 种类型 + Label Set

入口：**Annotate tab → Labels & Tables panel → Add Labels → Alignment → Add/Edit Chainage Labels**。

#### 5.1.1 7 种 Alignment Label 类型

| 类型                       | 默认格式            | 含义                              |
| -------------------------- | ------------------- | --------------------------------- |
| **Major Chainage**          | `1+000`             | 主桩（默认 100m）                 |
| **Minor Chainage**          | `1+020`             | 副桩（必须先有 Major）            |
| **Geometry Point**          | `CS: 0+327.65`      | 几何点（TS/SC/CS/ST/PI 自动识别） |
| **Profile Geometry Point**  | —                   | 纵断面几何点投影到平面            |
| **Chainage Equation**       | `0+523.4 = 1+000.0` | 桩号方程位置                      |
| **Design Speeds**           | `V=80`              | 设计速度变化点                    |
| **Superelevation Critical** | —                   | 超高临界点（C0/C1/B/A/M/N 等）    |

#### 5.1.2 Label Set（标签集）

把若干 Label 类型打包，跨项目复用。Civil 3D 自带几个：

- `_No Labels`（不标）
- `Major Minor and Geometry Points`（最常用）
- `Major and Minor only`
- 自定义...

操作步骤：

| 步骤 | 操作                                                        | UI 反馈                                          |
| ---- | ----------------------------------------------------------- | ------------------------------------------------ |
| 1    | Annotate → Add Labels → Alignment → Add/Edit Chainage Labels | 命令行 "Select an alignment:"                    |
| 2    | 选 Alignment                                                | 弹 **Alignment Labels** 对话框                   |
| 3    | Type 下拉选 Major / Minor / Geometry Points / ...           | Style 下拉自动过滤当前 Drawing 里的可用样式      |
| 4    | Add → Specify start / end station                           | 标签自动加到 DWG                                 |
| 5    | "Import Label Set" 按钮                                     | 一键导入预定义的 Label Set，省去 4 单独加        |

### 5.2 鸿业：桩号标注 + 几何点标注

- 菜单"道路 → 桩号 → 桩号标注" → 弹对话框 → 主桩间距 / 副桩间距 / 字高 / 旋转方向 / 文字偏移 → 确定。
- 菜单"道路 → 桩号 → 几何点标注" → 自动标 BC/EC/TS/SC/CS/ST。
- 菜单"道路 → 桩号 → 桩号方程标注"。

### 5.3 HyCADTool：`hyRoadAlnStation`

| 步骤 | 操作                                       | 命令行反馈                                                                                                              |
| ---- | ------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------- |
| 1    | 命令行输 `hyRoadAlnStation`                | 自动遍历当前 DWG 已登记的所有 Alignment（无参数调用，幂等：先按 AlignmentId 清旧标注，再重画）                          |
| 2    | 自动按 `RoadStationLabelOptions.Default` 出图 | 主桩 20m + 副桩 5m，主桩刻度 4m 长 + 桩号文字（沿切线旋转，左侧），副桩 1.5m 短刻度无文字                              |
| 3    | 命令行汇总                                 | "[道路] Alignment-1：主桩 62（每 20m）、副桩 247（每 5m），平面长度 1235.421 m。"<br>"[道路] 桩号标注完成：共 1 条 Alignment，主桩 62、副桩 247；已写入图层 RD-STA。" |

**关键限制**（§8.4 待补）：

- ❌ 没有 UI 改 `MainInterval` / `SubInterval` / `TextHeight` / `TextSide`（v1 全部硬编码）
- ❌ 没有 Geometry Point 标注（BC / EC / TS / SC / CS / ST）
- ❌ 没有 Label Set / 出图模板
- ❌ 没有桩号方程标注

### 5.4 三方标注类型对照表

| 标注类型           | Civil 3D                  | 鸿业              | HyCADTool v1.0       |
| ------------------ | ------------------------- | ----------------- | -------------------- |
| 主桩号             | ✅ Major Chainage          | ✅                | ✅ `hyRoadAlnStation` |
| 副桩号             | ✅ Minor Chainage          | ✅                | ✅（短刻度无文字）    |
| 几何点 BC/EC       | ✅ Geometry Point          | ✅                | ❌                   |
| 几何点 TS/SC/CS/ST | ✅ Geometry Point          | ✅                | ❌                   |
| 几何点 PI          | ✅ Geometry Point          | ✅                | ❌                   |
| 桩号方程位置       | ✅ Chainage Equation       | ✅                | ❌                   |
| 设计速度变化点     | ✅ Design Speeds           | ✅                | ❌                   |
| 超高临界点         | ✅ Superelevation Critical | ✅                | ❌                   |
| Label Set 打包重用 | ✅                        | 出图模板          | ❌（硬编码 Default）  |
| UI 配置参数        | ✅ Style 编辑器           | ✅ 标注对话框      | ❌（v1 全硬编码）     |
| 文字沿切线旋转     | ✅                        | ✅                | ✅（默认开）          |
| 文字左 / 右切换    | ✅                        | ✅                | ✅（`TextSide` 字段，但 UI 未暴露）|

---

## 6. 设计规范校核 (Design Criteria) 工作流

### 6.1 Civil 3D：xml 规范文件 + 实时 ⚠ 警告

入口：

- **创建时**：Create Alignment 对话框 → Design Criteria 选项卡 → 勾 "Use criteria-based design" → 选 xml 文件 + 设计速度。
- **后期补**：选 Alignment → 右键 → Alignment Properties → **Design Criteria 选项卡**。

xml 文件结构：

```xml
<DesignCriteriaFile>
  <MinRadiusTable>
    <Speed>40</Speed><MinRadius>60</MinRadius>
    <Speed>50</Speed><MinRadius>100</MinRadius>
    ...
  </MinRadiusTable>
  <MinTransitionLengthTable>
    <Speed>50</Speed><Radius>100</Radius><MinLs>40</MinLs>
    ...
  </MinTransitionLengthTable>
</DesignCriteriaFile>
```

校核行为：

- 子单元违反 → DWG 中曲线上出 ⚠ 黄三角 + Alignment Entities 表对应行出 ⚠
- 鼠标悬停 ⚠ → tooltip 显示"违反 Min Radius @ V=80：当前 200，需 ≥ 250"
- **Compound Spiral** 长度不在 xml 文件验证范围 —— 必须用 **Design Checks**（另一套机制）单独验证。

### 6.2 鸿业：内置 CJJ + 弹窗

- 内置 CJJ 37 / CJJ 45 / CJJ 152 / GB 5768 表，无需选文件。
- 速度从设计参数对话框选；改速度 → 自动重算。
- 校核失败 → 弹窗"半径 200 不满足 V=80 的最小半径 250，是否继续？" → 用户选"是 / 否"。

### 6.3 HyCADTool：6 项实时校核 + ✓✗ 列表

实现：`AlignmentCodeChecker.cs`，`PiThreeUnitWindow` 的 Tab 2 显示。

| # | 检查项                       | 规范条款             | 校核内容                                                                  |
| - | ---------------------------- | -------------------- | ------------------------------------------------------------------------- |
| 1 | 转角分类 + 缓和必要性        | CJJ 37 §7.5.4        | α ≤ 7°：不需缓和；7° < α ≤ 10°：推荐；α > 10°：必须有缓和                |
| 2 | 最小圆曲线半径               | CJJ 37 §7.5.2 表 7.5.2 | 速度→最小 R 表（20→15 / 30→30 / 40→60 / 50→100 / 60→150 / 80→250 / 100→400）|
| 3 | 最小缓和曲线长度             | CJJ 37 §7.5.5        | 速度 → 最小 Ls 表（20→20 / 30→25 / 40→30 / 50→35 / 60→50 / 80→70 / 100→85） |
| 4 | 最小圆曲线长（≥3 秒行车时间） | CJJ 152              | Ly ≥ V/3.6 × 3 = V × 0.833                                                |
| 5 | 切线长 vs 段长               | 几何可行性           | T1 + T2 < 该 PI 的入 / 出切线段长（不允许相邻 PI 的曲线重叠）             |
| 6 | 缓和对称性                   | CJJ 37 §7.5.5        | |Ls_in - Ls_out| / max(Ls_in, Ls_out) < 0.05（5% 容差）                  |

界面行为：

- 在 PiThreeUnitWindow Tab 2 实时显示 6 行 ✓✗ + 状态文字
- 全部通过 → 状态栏绿色"✓ 全部规范项通过"
- 任意一项失败 → 状态栏黄色"✗ 有规范项未通过（可强制确定）"
- 用户点"确定"时若有失败 → `NonCompliantConfirm` 二次确认弹窗

### 6.4 三方对照表

| 项                       | Civil 3D                      | 鸿业              | HyCADTool v1.0    |
| ------------------------ | ----------------------------- | ----------------- | ----------------- |
| 速度→最小 R              | ✅ XML 文件（可选，可定制）    | ✅ 内置 CJJ        | ✅ 硬编码 CJJ 37   |
| 速度→最小 Ls             | ✅ XML 文件                   | ✅                | ✅                |
| 最小圆曲线长（行车时间）  | ❌ 无内置项（需 Design Check）| ✅                | ✅                |
| 转角分类（缓和必要性）    | ❌ 无                         | ✅                | ✅                |
| 缓和对称性                | ❌ 无                         | ✅                | ✅                |
| 切线长可行性              | ✅ 几何引擎自带               | ✅                | ✅                |
| 实时反馈方式              | ⚠ 黄三角 + tooltip            | 弹窗              | ✓✗ 列表（窗口 Tab）|
| 强制确定                  | ✅ 关 Criteria                | ✅ 弹窗"是"        | ✅ NonCompliantConfirm |
| 复合规范文件加载          | ✅ 多个 XML 切换              | ❌ 硬编码          | ❌ 硬编码          |
| Compound Spiral 长校核    | ❌（需 Design Check）          | ✅                | ❌                |

---

## 7. 端到端场景对比：城市次干路 1.2 km，4 PI，V=50

> 用一个**完全相同的设计任务**，比较三家从"打开 CAD"到"出 Alignment + 桩号"完成的全部步骤、时间。
>
> 任务：4 个 PI 坐标已在 Excel 里，每 PI 配 R=200, Ls=80，要求生成 Alignment + 主副桩号 + 几何点标注。

### 7.1 Civil 3D 操作步骤（约 5–6 分钟，标准流程）

| 步 | 操作                                                                                          | 时间   |
| -- | --------------------------------------------------------------------------------------------- | ------ |
| 1  | Home → Alignment → Alignment Creation Tools                                                   | 3s     |
| 2  | Create Alignment - Layout 对话框：Name / Type=Centerline / Starting Station=0+000 / Style / Layer / Label Set=Major Minor and Geometry / Design Criteria 选 China.xml + V=50 → OK | 60s    |
| 3  | 工具栏 → Curve and Spiral Settings：Type=Clothoid / Default Radius=200 / Spiral In ☑ Out ☑ / Default Ls=80 → OK | 30s    |
| 4  | 工具栏 → **Tangent-Tangent (With Curves)**                                                    | 2s     |
| 5  | 在命令行依次输 4 个 PI 坐标（或图上点）                                                       | 60s    |
| 6  | 右键完成 → DWG 出 Alignment + 自动加 ⚠ 警告（如果 R 不达标）                                  | 5s     |
| 7  | 按需 Sub-Entity Editor 微调（本场景无需）                                                     | 0s     |
| 8  | Annotate → Add Labels → Alignment → Add/Edit Chainage Labels → 选 Alignment → Import Label Set "Major Minor and Geometry" → Add | 60s    |
| 9  | （可选）出 Plan Production / 导 PDF                                                           | 60s    |
| **总** | **约 5 分钟**                                                                                  | **5 min** |

### 7.2 鸿业操作步骤（约 4 分钟，国内最熟）

| 步 | 操作                                                                                | 时间   |
| -- | ----------------------------------------------------------------------------------- | ------ |
| 1  | 菜单"道路 → 平面 → 设计参数"：起始桩号 K0+000 / 设计速度 50 → 确定                  | 30s    |
| 2  | 菜单"道路 → 平面 → 交点法"                                                          | 2s     |
| 3  | "交点表"对话框 → 复制 Excel 4 行 → 粘贴 → 输 R=200 / Ls=80 → 确定                   | 90s    |
| 4  | 自动出 Alignment + 弹规范检查报告（如有违反弹窗确认）                              | 5s     |
| 5  | 菜单"道路 → 桩号 → 桩号标注"：主桩 20 / 副桩 5 / 字高 3 → 确定                      | 30s    |
| 6  | 菜单"道路 → 桩号 → 几何点标注"：默认全部 → 确定                                     | 20s    |
| 7  | 菜单"道路 → 出图 → 平面图"（可选）                                                  | 60s    |
| **总** | **约 4 分钟**                                                                       | **4 min** |

### 7.3 HyCADTool 操作步骤（v1.0 现状，约 2 分钟）

| 步 | 操作                                                                                                  | 时间   |
| -- | ----------------------------------------------------------------------------------------------------- | ------ |
| 1  | 在 Excel 选 4 行（x, y, R, Ls_in, Ls_out 五列）→ Ctrl+C                                                | 5s     |
| 2  | 命令行 `hyRoadAlnByPi` → 选 **C**（剪贴板）                                                            | 3s     |
| 3  | 自动解析 + 命令行打印 PI 表 + 干跑预览（含 6 项规范状态摘要）                                          | 5s     |
| 4  | 输 **A** 接受                                                                                         | 1s     |
| 5  | 自动写 DWG + Xdata + JSON + 自检 → 命令行汇总                                                         | 2s     |
| 6  | 命令行 `hyRoadAlnStation`                                                                             | 3s     |
| 7  | 自动按默认参数标主副桩 + 命令行汇总                                                                   | 5s     |
| **总** | **约 25 秒**                                                                                          | **0.5 min** |

> ⚠ 注意：HyCADTool 时间短的代价是 **§5.3** / **§5.4** 列出的功能差距：没几何点标注 / 没 UI 改桩号参数 / 没桩号方程 / 没 Label Set。简单场景能比鸿业快 8 倍，复杂场景立即露出短板。

### 7.4 步骤数 vs 灵活度的折中

| 维度                 | Civil 3D       | 鸿业           | HyCADTool v1.0  |
| -------------------- | -------------- | -------------- | --------------- |
| 总操作步数           | 9              | 7              | 7               |
| 鼠标点击次数（估）   | 30+            | 20+            | 8               |
| 键盘输入次数（估）   | 20+            | 15+            | 4（命令名 + AC + 坐标） |
| 完成时间（简单场景） | 5 min          | 4 min          | 0.5 min         |
| 完成时间（复杂场景） | 5–8 min        | 5–7 min        | 受限于 §8.2 缺口 |
| 可调参数数（创建期） | 50+            | 30+            | 10              |
| 可调参数数（标注期） | 50+ × Label Set | 30+            | 0（硬编码）     |
| 学习曲线             | 陡（2 周）     | 中（3 天）     | 平（1 小时）    |

---

## 8. HyCADTool 功能清单与差距

### 8.1 v1.0 已实现 ✅

#### 8.1.1 创建

- ✅ `hyRoadA` —— 从已有 LWPOLYLINE 导入（含 bulge 弧段诊断）
- ✅ `hyRoadAlnByPi` —— PI 表法（点取 / CSV / 剪贴板 三通道 + 干跑预览 + 批量改 R）

#### 8.1.2 编辑

- ✅ `hyRoadAlnEditPi` —— 单 PI 参数编辑（窗口模式 = `PiThreeUnitWindow`，命令行模式 = 三 Prompt）
- ✅ Transient 临时图形预览（窗口模式下边改边看 DWG 红色虚线）
- ✅ 改完整条 Alignment 几何重建 + JSON 同步落盘 + 自检

#### 8.1.3 桩号标注

- ✅ `hyRoadAlnStation` —— 主桩 20m + 副桩 5m + 沿切线旋转 + 左侧文字（默认）
- ✅ 幂等（重复跑不累积图元）

#### 8.1.4 规范校核

- ✅ 6 项实时校核（转角分类 / 最小 R / 最小 Ls / 最小 Ly / 切线长 / 缓和对称）
- ✅ 7 档速度（20/30/40/50/60/80/100）查 CJJ 37 + CJJ 152
- ✅ 强制确定（`NonCompliantConfirm`）

#### 8.1.5 桩号系统

- ✅ 起始桩号字段（`StartStation`）
- ✅ `K{km}+{m:000.000}` 标准格式

### 8.2 与 Civil 3D 对标的缺口 ❌

| # | 缺口                                | 优先级 | 备注                                |
| - | ----------------------------------- | ------ | ----------------------------------- |
| 1 | Curve & Spiral Settings 默认值面板  | 🟢 高  | 现在每次都从 R=30 / Ls=0 起步       |
| 2 | 单段约束式（Free / Fixed / Floating） | 🟡 中  | 目前只能"按 PI 整体出"               |
| 3 | Sub-Entity 全表（数据网格 + 选中即高亮） | 🟢 高  | 看不到一条 Alignment 内每段 R/Ls/T |
| 4 | Insert PI / Delete PI               | 🟢 高  | 现在改 PI 数量必须从头重输          |
| 5 | Reverse Direction（整条反向）       | 🟡 中  | —                                   |
| 6 | Grip Edit（拖 PI / 拖半径实时刷）    | 🔴 低  | 工程量较大                          |
| 7 | 几何点标注（TS/SC/CS/ST/PI）        | 🟢 高  | 复测表 / 出图必备                    |
| 8 | Reference Point + Reference Station | 🟡 中  | 起点 ≠ K0+xxx 的场景                |
| 9 | Station Equation（桩号方程 / 断链） | 🟡 中  | 路改造常见                          |
| 10 | 多速度分段                         | 🟡 中  | 大型项目分段提速                    |
| 11 | Compound Spiral / Reverse Spiral   | 🔴 低  | 高速 / 复杂枢纽                      |
| 12 | Spiral 类型可切（Bloss/Sin/Cubic）  | 🔴 低  | 铁路项目才需                        |
| 13 | Best Fit Alignment                 | 🔴 低  | 测量后期改造                        |
| 14 | Offset Alignment                   | 🟡 中  | 辅道 / 自行车道                      |
| 15 | Reference Alignment                | 🔴 低  | 多 DWG 引用                          |
| 16 | Label Set 打包重用                 | 🟡 中  | 出图风格统一                        |
| 17 | XML 规范文件可定制                 | 🔴 低  | v1 硬编码够用                       |

### 8.3 与鸿业对标的缺口 ❌

| # | 缺口                                                         | 优先级 | 备注                                  |
| - | ------------------------------------------------------------ | ------ | ------------------------------------- |
| 1 | 路线复测表（坐标 + 桩号 + 方位角 + 高程，按主桩输出 Excel）   | 🟢 高  | 国内施工放样必备                      |
| 2 | 路线交点表（PI 表 + 转角 + R + Ls + T 出 Excel）              | 🟢 高  | 同上                                  |
| 3 | 平面图自动出图（带方位、比例、桩号、表格图签）                | 🟢 高  | 出图章节                              |
| 4 | 鸿业 / OpenRoads / Civil 3D LandXML 互通（导入导出 .xml）    | 🟡 中  | 项目交付                              |
| 5 | 参数法创建（长度 + 方位角逐段输）                            | 🔴 低  | 老路设计                              |
| 6 | 连接法创建                                                   | 🔴 低  | 改扩建                                |
| 7 | 块法创建（标准弯道库）                                        | 🔴 低  | 复用                                  |
| 8 | 桩号 UI 配置面板（主副桩间隔 / 字高 / 偏移 / 旋转）            | 🟢 高  | 当前 `RoadStationLabelOptions` 全硬编码 |

### 8.4 v1.1 优先补的 4 个功能

> 选取标准：填补 §8.2 + §8.3 中"高优先级 + 1 周内能完成"的项。

#### 1. Sub-Entity 全表（数据网格 + 选中高亮）

- 命令名建议：`hyRoadAlnTable`
- UI：`AlignmentEntityTableWindow`，4 列 = 段类型（直/缓/圆/缓）/ 起桩号 / 长度 / R 或 Ls
- 操作：选中行 → DWG 中对应段红色高亮（Transient）
- 价值：让 PI 法用户也能看到"每段 R / Ls / T 现在是多少"

#### 2. 几何点标注（TS / SC / CS / ST / PI）

- 命令名建议：`hyRoadAlnGeomPt`
- 操作：无参数调用，自动遍历 Alignment 的每个 PI，按其"圆 / 缓-圆-缓"属性出 BC/EC 或 TS/SC/CS/ST 标记
- UI：复用 `RoadStationLabelOptions`，新增 `GeometryPointStyle` 字段（短刻度 + 缩写 + 桩号）
- 价值：补 §8.2-7（最高优先级）

#### 3. 路线交点表 + 复测表导出

- 命令名建议：`hyRoadAlnExportPi`（PI 表）+ `hyRoadAlnExportFrame`（复测表）
- 输出：CSV → 鸿业格式 / 通用 Excel
- 字段：
  - PI 表：序号 / X / Y / 转角 / R / Ls_in / Ls_out / T1 / T2 / Ly
  - 复测表：主桩号 / X / Y / 方位角 / Tag
- 价值：补 §8.3-1 + §8.3-2

#### 4. Curve & Spiral Settings 默认值面板

- 命令名建议：`hyRoadAlnDefaults`
- UI：BlenderUI 小窗口，3 字段 = Default Radius / Default Spiral In / Default Spiral Out
- 持久化：写到 `hy-settings.json` 的 `Road.AlignmentDefaults` 段
- 影响：`hyRoadAlnByPi` 的"中间 PI 追问 R / Ls"默认值不再是上次输入而是 Settings
- 价值：补 §8.2-1，提速 PI 法

### 8.5 v1.2 优先补的 3 个功能

#### 1. Insert PI / Delete PI

- 命令名建议：`hyRoadAlnInsertPi` / `hyRoadAlnDeletePi`
- 操作：拾取 Alignment + 一个新点 → 在最近的段中插 PI（继承默认 R/Ls）；拾取 PI → 删除 + 合并相邻段
- 写回：复用 `hyRoadAlnEditPi` 的"PI 表 → Build → RebuildCenterline"链路
- 价值：补 §8.2-4（v1.0 用户最痛的痛点）

#### 2. 桩号 UI 配置面板（主副桩间隔 / 字高 / 偏移 / 旋转）

- 命令名建议：`hyRoadAlnStationCfg`（或合并到 `hyRoadAlnStation` 的可选关键字 `[配置(C)/默认(D)]`）
- UI：BlenderUI 小窗口，全部 `RoadStationLabelOptions` 字段
- 持久化：`hy-settings.json` 的 `Road.Station` 段
- 价值：补 §8.3-8

#### 3. 桩号方程 / 断链（Station Equation）

- 命令名建议：`hyRoadAlnStaEq`
- 操作：拾取 Alignment + 在线上点参考点 → 输 Station Back / Station Ahead / Increase or Decrease → 自动重算下游桩号
- Domain 增字段：`Alignment.StationEquations: List<StationEquation>`
- 桩号公式：`actualStation = baseStation + Σ equationOffsets`
- 价值：补 §8.2-9

---

## 9. 命令速查表

### 9.1 创建

| 工作流          | Civil 3D                              | 鸿业            | HyCADTool          |
| --------------- | ------------------------------------- | --------------- | ------------------ |
| 从 Polyline 导入 | `_AlignmentFromPLine`                 | 拾取多段线      | `hyRoadA`          |
| PI 法（点取）   | `_CreateAlignmentLayout` → Tangent-Tangent | 平面 → 交点法 | `hyRoadAlnByPi` → P |
| PI 法（CSV）    | ❌                                    | 交点表粘贴      | `hyRoadAlnByPi` → F |
| PI 法（剪贴板） | ❌                                    | Excel 粘贴      | `hyRoadAlnByPi` → C |
| 单段约束式      | `_CreateAlignmentLayout` 工具栏       | ❌              | ❌                 |
| Best Fit        | `_CreateAlignmentBestFit`             | 部分            | ❌                 |
| Reference       | `_CreateAlignmentReference`           | ❌              | ❌                 |
| Offset          | `_CreateOffsetAlignment`              | 平行线          | ❌                 |

### 9.2 编辑

| 操作               | Civil 3D                      | 鸿业           | HyCADTool                 |
| ------------------ | ----------------------------- | -------------- | ------------------------- |
| 改单 PI 参数       | Sub-Entity Editor             | 双击 PI         | `hyRoadAlnEditPi` (W / C)  |
| 拖 PI              | Grip Edit                     | 拖 PI          | ❌                        |
| 插 PI              | Insert PI                     | 右键 → 插       | ❌（v1.2 计划 `hyRoadAlnInsertPi`） |
| 删 PI              | Delete PI                     | 右键 → 删       | ❌（v1.2 计划 `hyRoadAlnDeletePi`） |
| 反向               | Reverse Direction             | 反向            | ❌                        |
| Sub-Entity 全表查看 | Sub-Entity Editor + Entities Vista | 列表面板  | ❌（v1.1 计划 `hyRoadAlnTable`） |

### 9.3 桩号 + 标注

| 操作               | Civil 3D                              | 鸿业           | HyCADTool                  |
| ------------------ | ------------------------------------- | -------------- | -------------------------- |
| 桩号设置           | Alignment Properties → Station Control | 设计参数       | ❌（v1.2 计划 `hyRoadAlnStaEq`） |
| 桩号方程           | Add Station Equation                  | 加断链         | ❌（同上）                 |
| 主副桩号标注       | Add Labels → Major / Minor            | 桩号 → 桩号标注 | ✅ `hyRoadAlnStation`       |
| 几何点标注         | Add Labels → Geometry Point           | 桩号 → 几何点标注 | ❌（v1.1 计划 `hyRoadAlnGeomPt`） |
| 标注配置 UI        | Style 编辑器                          | 标注对话框     | ❌（v1.2 计划 `hyRoadAlnStationCfg`） |
| Label Set 打包     | Label Set                             | 出图模板       | ❌                          |

### 9.4 规范校核

| 操作               | Civil 3D                              | 鸿业           | HyCADTool                  |
| ------------------ | ------------------------------------- | -------------- | -------------------------- |
| 加载规范文件       | Design Criteria → 选 XML              | 内置不可换     | ❌（硬编码）                |
| 实时反馈           | ⚠ 黄三角 + tooltip                    | 弹窗           | ✅ ✓✗ 列表（窗口 Tab 2）   |
| 强制提交           | 关 Criteria                           | 弹窗 → 是      | ✅ NonCompliantConfirm      |
| 规范校核命令       | `_ApplyDesignCriteria`                | 道路 → 规范检查 | 内嵌于 `hyRoadAlnByPi` / `hyRoadAlnEditPi` |

### 9.5 导入 / 导出

| 操作                     | Civil 3D            | 鸿业           | HyCADTool                 |
| ------------------------ | ------------------- | -------------- | ------------------------- |
| LandXML 导入             | `_LandXMLIn`        | 部分           | ❌                        |
| LandXML 导出             | `_LandXMLOut`       | 部分           | ❌                        |
| 路线复测表（坐标 + 桩号） | Reports Manager → Stations | 桩号坐标表 | ❌（v1.1 计划 `hyRoadAlnExportFrame`） |
| 路线交点表（PI 表）       | Alignment Entity Report | 交点要素表  | ❌（v1.1 计划 `hyRoadAlnExportPi`） |
| 项目级 JSON 持久化       | 内部 .dwg 嵌入       | .htf / .scs    | ✅ `.roaddesign.json`      |

---

## 10. 红线（不可动）

> 任何对 Alignment 命令 / 标注 / 校核的修改，违反下面任何一条都要回退。

1. **`Alignment` / `PiElement` / `Station` 是 Domain 不可变结构**，编辑窗口（`PiThreeUnitWindow`）只能输出"新一组 PI 快照"，绝不允许直接 mutate 已落 DWG 的 `Centerline`。重建走 `RebuildCenterline` 单一入口。
2. **6 项规范校核必须由 `AlignmentCodeChecker` 唯一负责**。新加规范项 → 改 Domain，不允许在 ViewModel / Command 层重复实现校核。
3. **桩号 m / Format 解耦**：所有内部存储、运算、字段、JSON 都以 m 为单位的 `double`；`K0+000` 字符串只在 UI / 标注 / 报表三处生成。
4. **JSON 同步落盘只能在命令收尾**（`tr.Commit()` 之后），绝不允许异步 Timer / 防抖（v1.0 已踩坑，见 `PluginInitializer` §392 注释）。
5. **`hyRoadA` 的 Polyline 必须是 LWPOLYLINE（`Polyline`）**，不接受 `Polyline3d` / `Spline` / `Line+Arc` 集合 —— 用户用别的类型 → 命令必须给清晰的修复指引（PEDIT → S / PEDIT → J）。
6. **Xdata 上 GUID = Alignment.Id 的契约不可破**。Xdata 丢失 / 重写 → `hyRoadAlnEditPi` 等编辑类命令必须直接拒绝并提示重跑 `hyRoadA`，不允许"猜身份"。
7. **新加 Spiral 类型（Bloss/Sin/Cubic）必须先扩 `AlignmentCodeChecker`**，不允许只改 Designer 不改校核 —— 否则规范校核会对新类型给出错误结论。
8. **桩号方程实现时桩号必须仍是单调的实坐标**。Equation 改的是"显示桩号"映射，不能改 `Centerline` 几何。

---

**文档性质**：功能与流程对标文档（Civil 3D / 鸿业 / HyCADTool 三方）  
**更新日期**：2026-04-19  
**上游依赖**：`Civil3D.md` §1.1、`HongYeRoad.md` §2、`OpenRoads.md` §3、HyCADTool 源码（`Presentation/Commands/Road/RoadAlignment*.cs`、`Domain/Services/Road/AlignmentCodeChecker.cs`、`Infrastructure/AutoCAD/Services/Road/RoadAlignmentService.cs`、`Presentation/Views/Road/PiThreeUnitWindow.xaml`）  
**下游依赖**：`05计划书.md` P1 收尾 / P2 启动、`01MASTER.md` P1/P2 阶段、未来的"v1.1 看板（§8.4）"与"v1.2 看板（§8.5）"
