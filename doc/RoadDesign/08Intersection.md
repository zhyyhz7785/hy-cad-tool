# 08 平面交叉口（Intersection）— Civil 3D / 鸿业 / HyCADTool 用户操作流程对标

> 性质：**功能与流程对标文档** —— 把 Civil 3D（Intersection Wizard 范式的定义者）、鸿业市政道路（国内"自动交叉口"最成熟的 UI）、HyCADTool（v1.x P3-I1 新交付的 `hyRoadIntersection` / `hyRoadCurbRamp` / `hyRoadTactilePaving` 三件套）三方放在同一坐标系下，逐条比对"用户怎么画一个交叉口"。
>
> 重点：**用户视角** —— 从哪条命令进，点哪里，填哪些参数，规范怎么校核，缺什么功能。
>
> 非重点：Domain / Infrastructure / Presentation 分层细节。请回查 `01MASTER.md` §2 / `03RoadSelect.md` §3 与源码 XML Doc。
>
> 上游姊妹篇：
>
> - `07Alignment.md` 平面线位 —— 交叉口依赖 **≥ 2 条已存在的 Alignment**；
> - `09Assembly.md` 横断面装配 —— 交叉口的半宽 `HalfWidth` 常取自 Template 的有效路幅；
> - `10Corridor.md` 走廊 —— 交叉口处 Corridor 需拆 Region 换用"交叉口专用 Assembly"（v3 议题）。
>
> 资料来源：Autodesk Civil 3D 2024–2026 Intersection Objects 帮助、Civil3D.tv 教学站、鸿业市政道路 9.0 用户手册平交口章节、纬地道路 BIM 2.0 "平交口 BIM 一键"、CJJ 37-2012 附录 B、CJJ 152-2010 §6.2 / §6.4、GB 50763-2012 §3.2 / §3.3，HyCADTool 源码与 P3 交付清单。

---

## 0. 文档定位

| 维度     | 取值                                                                    |
| -------- | ----------------------------------------------------------------------- |
| 性质     | 三方功能对标 + P3-I1 交付说明 + 规范贯穿                                 |
| 目标读者 | 道路设计工程师 / HyCADTool 开发者 / QA                                   |
| 上游依赖 | `07Alignment.md`（前置 Alignment 要可选 2 条以上）、`Civil3D.md` §1.5（Intersection 总论） |
| 下游依赖 | `05计划书.md` P3 看板、`01MASTER.md` P3 阶段                              |
| 不重叠   | `07Alignment.md` 平面线位、`09Assembly.md` 横断面装配、`03RoadSelect.md` 选择系统 |
| 维护节奏 | 每完成 v1.x 的一个 Intersection 命令 → 在 §9 勾选并归档                   |

---

## 1. 平面交叉口是什么 —— 三方共识与术语统一

### 1.1 几何构成

| 概念           | Civil 3D                         | 鸿业                 | HyCADTool v1.x                          | 说明                                  |
| -------------- | -------------------------------- | -------------------- | --------------------------------------- | ------------------------------------- |
| 交叉口对象     | **Intersection**（专有对象）     | **交叉口**           | `Domain.Models.Road.Intersection`       | 聚合根                                 |
| 接入的路       | Primary / Secondary Road（2 条） | 主路 / 支路（≥ 2 条）| `IntersectionLeg[]`（≥ 2 条）           | HyCAD 支持任意臂数，未限定 2 条       |
| 单条臂         | Road Centerline Arm              | 臂 / 道路端          | **`IntersectionLeg`**（值对象）         | 引用所属 Alignment + 端点             |
| 臂端点         | Centerline intersection          | 相交端               | `IntersectionLeg.ApproachPoint`         | Alignment 距 aroundPoint 较近的那端   |
| 臂的半宽       | Primary / Secondary Road Width   | 路幅宽度             | `IntersectionLeg.HalfWidth`             | 米，默认 7.5                          |
| 臂的入向       | —（由 Alignment 切线自动）       | —                    | `IntersectionLeg.InwardDirection`       | 从 ApproachPoint 指向交叉口中心       |
| 转角圆弧       | **Corner Radius / Fillet**       | 缘石转角             | **`CornerArc`**（值对象）               | 相邻两臂"外边线内切"圆弧              |
| 导流岛         | Channelized Turn Lane            | 导流岛               | ❌ v3 议题                               | 大型主干道主流技术                    |
| 停止线         | Stop Bar                         | 停止线               | ❌（留给 P3-I2 标线）                    | P3-I2 `hyRoadMarkStopLine`            |
| 人行横道       | Crosswalk                        | 人行横道             | `CrosswalkService`（**v0 老实现**）     | 新交叉口模型暂未接入，留 v1.2         |
| 缘石坡道       | Curb Ramp                        | 无障碍坡道           | **`CurbRamp`**（值对象）                | GB 50763 §3.2                         |
| 盲道           | Truncated Dome                   | 盲道                 | **`TactilePaving`**（class）            | GB 50763 §3.3                         |

### 1.2 拓扑类型（按臂数）

| 类型       | Civil 3D                 | 鸿业                | HyCADTool                                    |
| ---------- | ------------------------ | ------------------- | -------------------------------------------- |
| 十字口     | Cross                    | 十字交叉            | ✅（`Legs.Count == 4`，CCW 排序自动正交）     |
| T 字口     | T                        | T 形交叉            | ✅（`Legs.Count == 3`，缺臂不补）             |
| Y 字口     | Y                        | Y 形交叉            | ✅（`Legs.Count == 3`，异角）                 |
| 多路口     | 3-way / 4-way / 5+       | 多路交叉            | ✅（任意 ≥ 2 臂，按入向方位角 CCW 自动排序） |
| 环岛       | Roundabout（独立对象）   | 环交                | ❌（v3 议题）                                |
| 立交       | Interchange              | 互通立交             | ❌（v∞ 议题，Blender 端处理）                |

> HyCAD 在 P3-I1 阶段**不区分拓扑类型**：用户拾取任意 N 条 Alignment（N ≥ 2），点一个大致中心点，Designer 按入向方位角 CCW 排序、逐对相邻生成 CornerArc。实现上"十字 / T / Y" 只是 `Legs.Count` 不同时的同一算法。

### 1.3 与 `RoadNode` 的区别（HyCAD 内部）

HyCAD 有两套交叉口模型并存：

| 维度     | `RoadNode`（v0）                            | `Intersection`（P3-I1 新）                       |
| -------- | ------------------------------------------- | ------------------------------------------------ |
| 出身     | 从 DWG 的 Line + Arc 反推                    | 从已存在的 `Alignment` 聚合构造                  |
| 驱动命令 | 旧 `hyRoad`（Line/Arc 拾取）                 | 新 `hyRoadIntersection`（Alignment 拾取）         |
| 几何模型 | `RoadArm` 半平面 + Arc                        | `IntersectionLeg` + `CornerArc`（精确切点）      |
| 标准校核 | ❌                                          | ✅ `IntersectionCodeChecker`（CJJ 37/152）        |
| 人行横道 | ✅ `CrosswalkService`                       | ❌ v1.1 接入                                    |
| 缘石坡道 | ❌                                          | ✅ `CurbRampDesigner`                            |
| 盲道     | ❌                                          | ✅ `TactilePavingDesigner`                       |
| JSON 持久化 | ❌                                          | ✅ `RoadDesign.Intersections`                    |

> **路线**：v1.x 内两者并存；v2 把 `CrosswalkService` 迁到 `Intersection` 聚合，然后归档 `RoadNode`。

---

## 2. 规范依据 —— CJJ 37 / CJJ 152 / GB 50763

HyCAD P3-I1 的两个校核器分别对应：

| 校核器                                | 规范                       | 条款                       | 关键值                                 |
| ------------------------------------- | -------------------------- | -------------------------- | -------------------------------------- |
| `IntersectionCodeChecker`             | **CJJ 37-2012** 附录 B     | 平面交叉口缘石转弯半径推荐表 | V ≤ 20 → R ≥ 15 m；V ≤ 30 → R ≥ 20 m；V ≤ 40 → R ≥ 25 m；V ≤ 50 → R ≥ 30 m；V ≤ 60 → R ≥ 40 m；V > 60 → R ≥ 50 m |
| `IntersectionCodeChecker`             | **CJJ 152-2010** 表 6.4.2  | 交叉口转角最小半径          | 本项目取上表与 CJJ 37 的**保守并集**    |
| `IntersectionCodeChecker`             | CJJ 152-2010 §6.2.2        | 相邻臂夹角                   | ≥ 15°（CJJ 原文建议 45°，立交匝道放宽） |
| `AccessibilityCodeChecker`            | **GB 50763-2012** §3.2.1   | 缘石坡道宽度                 | 单面坡 ≥ 1.50 m；三面坡 ≥ 1.20 m；扇形 ≥ 1.50 m |
| `AccessibilityCodeChecker`            | **GB 50763-2012** §3.2.3   | 缘石坡道坡度                 | ≤ 1:12                                  |
| `AccessibilityCodeChecker`            | **GB 50763-2012** §3.3.1   | 盲道宽度                     | 行进盲道 0.25~0.50 m；提示盲道 0.30~0.60 m |
| `AccessibilityCodeChecker`            | **GB 50763-2012** §3.3.1   | 盲道距缘石最小净距           | ≥ 0.25 m（由 Designer 布置时强制 offset，Checker 不重复校验） |

**决策**：所有校核器返回 `CheckResult` 结构，**不抛异常**；命令层决定"硬停 / 警告 / 忽略"。与 `AlignmentCodeChecker` / `CrossSectionCodeChecker` 保持一致。

---

## 3. `hyRoadIntersection` —— 从 Alignment 生成转角圆弧

### 3.1 前置条件

1. 至少已用 `hyRoadAlnPI` / `hyRoadAlnImportXml` 创建 **2 条 Alignment**；
2. 建议这些 Alignment 的端点在"交叉口大致中心"附近（Designer 按距离选就近端为 `ApproachPoint`）。

### 3.2 用户交互流程

| 步骤 | 提示                                             | 默认值 / 约束                                    |
| ---- | ------------------------------------------------ | ------------------------------------------------ |
| 1    | `拾取参与交叉口的平面线位（Polyline），回车结束：` | ≥ 2 条；按 HY_ROAD Xdata KIND="Alignment" 过滤    |
| 2    | `指定交叉口大致中心点：`                           | 点击任意位置；用于决定每条 Alignment 取起 / 末端  |
| 3    | `转角半径 R (默认 20 m)：`                         | `Intersection.DefaultCornerRadiusValue`          |
| 4    | `半宽 W (默认 7.5 m)：`                            | `IntersectionLeg.DefaultHalfWidth`               |
| 5    | `设计速度 V (km/h，默认 30)：`                     | 用于 `CheckCornerRadius` 查表                     |

### 3.3 内部流程

```text
拾取 Alignment ID  ─┐
指定 aroundPoint  ─┼─>  IntersectionDesigner.ComputeFromAlignments
                   │         │
                   │         ├─ 每条 Alignment → 取距 aroundPoint 较近端
                   │         │   ├─ ApproachPoint = 该端坐标
                   │         │   └─ InwardDirection = 从该端指向交叉口中心
                   │         │
                   │         ├─ Legs 按 InwardAngle CCW 排序（[0, 2π) 归一化）
                   │         │
                   │         ├─ Center = Legs.ApproachPoint 平均（偏差过大时退回 aroundPoint）
                   │         │
                   │         └─ 相邻 (i, (i+1)%N) → TryBuildCornerArc
                   │                 ├─ 两条路缘外边线 X 交点
                   │                 ├─ 内角 θ = uA.AngleTo(uB)
                   │                 ├─ T = R / tan(θ/2), D = R / sin(θ/2)
                   │                 ├─ bisector = normalize(uA + uB)
                   │                 ├─ start/end = X + uA·T, X + uB·T
                   │                 └─ center = X + bisector · D
                   ▼
          Intersection { Legs, CornerArcs, Center, DesignSpeed }
                   │
                   ├─ CheckCornerRadius（逐弧，警告）
                   ▼
          design.Intersections.Add(...)
                   │
                   ▼
          RoadIntersectionService.RebuildIntersection
                   │   ├─ ClearIntersectionEntities（按 Xdata KIND="Intersection"，ID=intersection.Id）
                   │   └─ DrawIntersection → 每条 CornerArc → AutoCAD Arc
                   │       └─ HyRoadXdata.Write(KIND="Intersection", ID=intersection.Id)
                   ▼
          RoadJsonExportService.SaveForDocument
```

### 3.4 输出

| 载体                        | 内容                                                            |
| --------------------------- | --------------------------------------------------------------- |
| AutoCAD 模型空间            | N 条 `Arc`，图层 `05_hy_道路_交叉口`（红色 1），挂 HY_ROAD Xdata  |
| `RoadDesign.Intersections`   | `Intersection { Id, Center, Legs[], CornerArcs[] }`              |
| `<dwg>.roaddesign.json`      | 同步 Save（原子替换，与 Alignment 写同一文件）                    |
| 命令行                      | 校核警告 / 通过数 + Legs / Arcs 计数                              |

### 3.5 CornerArc 几何推导

> 这是 P3-I1 Domain 最难啃的一小块；单测已覆盖十字 / T / Y / 60° 斜交等主要场景。

设相邻两臂 A / B（按 CCW 排序，A 在前）：

- Leg A 入向单位向量 `u_A`（从 ApproachPoint 指向交叉口内部）；
- Leg B 入向单位向量 `u_B`；
- Leg A 的 "左侧路缘外边线" 沿 `u_A` 前进左手侧，过点 `P_A = ApproachPoint_A + perpCCW(u_A) · halfWidth_A`，方向 `u_A`；
- Leg B 的 "右侧路缘外边线" 过点 `P_B = ApproachPoint_B − perpCCW(u_B) · halfWidth_B`，方向 `u_B`。
- 两条直线相交于 `X`（必有交点，除非两臂共线 / 反向 → Designer 过滤）。

令 `θ = u_A.AngleTo(u_B) ∈ (0, π)`（CCW 相邻 θ 恒正），则转角圆弧应位于 **内角口袋**（两 Inward 之间的锐角区域）：

```
T = R / tan(θ / 2)          ← 切线长：X 到两切点的距离
D = R / sin(θ / 2)          ← 圆心偏距：X 到圆心的距离
bisector = normalize(u_A + u_B)    ← 内角平分方向（指向"更深"的口袋）
start = X + u_A · T
end   = X + u_B · T
center = X + bisector · D
```

圆半径 R 即用户输入的 `defaultRadius`。

CCW 扫过时，由外侧看两切点在 "X 前方"（+u_A / +u_B 方向）， 圆心在 "X 前方更远的口袋里"（+bisector 方向），因此：

- `start → end` 实际走 **CW**（`SweepAngle < 0`），但起止点在圆周上位置正确；
- AutoCAD `Arc` 的构造始终是 **CCW** 扫过 StartAngle → EndAngle，所以 `RoadIntersectionService.ToAutoCadArc` 在 `SweepAngle < 0` 时把 Domain 的 StartAngle / EndAngle **对调**后交给 AutoCAD —— 这是 P3-I1 最隐蔽的一个转换，测试用 `Cross_AllCornerArcsTangentToLegCurbLines` 保住。

---

## 4. `hyRoadCurbRamp` —— 无障碍缘石坡道（GB 50763 §3.2）

### 4.1 前置条件

已用 `hyRoadIntersection` 生成至少一个交叉口（存在 `CornerArcs`）。

### 4.2 用户交互流程

| 步骤 | 提示                                                 | 默认值                                 |
| ---- | ---------------------------------------------------- | -------------------------------------- |
| 1    | `拾取目标交叉口的任意图元（转角弧 / 已有坡道 / 已有盲道）：` | 通过 HY_ROAD Xdata ID 反查 Intersection |
| 2    | `坡道类型 [单面(S) / 三面(T) / 扇形(F)]：`             | S（单面）                              |
| 3    | `坡道宽度 W (默认 1.50 m)：`                          | `CurbRamp.DefaultWidth`                |
| 4    | `坡道深度 D (默认 1.80 m)：`                          | `CurbRamp.DefaultDepth`                |
| 5    | `坡度（如 1:12 输入 0.0833，默认 0.0833）：`           | `CurbRamp.DefaultSlope` = 1/12         |

### 4.3 布置策略（P3-I1-C 简化）

对每个 `CornerArc` 布置 **1 个单面坡**（或按用户输入的 Kind），位于弧中点：

- `FrontCenter = Center + midDir · Radius`，其中 `midDir = normalize((StartPoint - Center) + (EndPoint - Center))`；
- `OutwardNormal = normalize(Center - FrontCenter)`（从弧中点指向圆心 = 从车道侧指向人行道侧 —— 因为 CornerArc 凸向交叉口内，圆心在人行道远端）；
- `Tangent = OutwardNormal.Perpendicular()`（沿切向，Width 沿此方向展开）；
- 矩形四角 = `FrontCenter ± Tangent·(Width/2) + OutwardNormal·(0 或 Depth)`。

### 4.4 输出

| 载体                        | 内容                                                             |
| --------------------------- | ---------------------------------------------------------------- |
| AutoCAD 模型空间            | N 个 4 顶点闭合 `Polyline`（矩形），图层 `05_hy_道路_缘石坡道`（淡红 11） |
| `Intersection.CurbRamps`    | `CurbRamp[]`                                                     |
| `<dwg>.roaddesign.json`      | 随 Intersection 序列化                                            |
| 命令行                      | 宽度 / 坡度校核警告 + 生成数                                       |

### 4.5 规范校核

| 条款             | 公式                                                   | 消息                             |
| ---------------- | ------------------------------------------------------ | -------------------------------- |
| §3.2.1 单面坡    | `Width ≥ 1.50 m`                                       | `[FAIL] 单面坡宽度 1.20 不足 1.50`|
| §3.2.1 三面坡    | `Width ≥ 1.20 m`                                       | —                                |
| §3.2.1 扇形      | `Width ≥ 1.50 m`                                       | —                                |
| §3.2.3 坡度      | `Slope ≤ 1/12 ≈ 0.0833`                                | `[FAIL] 坡度 1:10 陡于 1:12 上限` |

---

## 5. `hyRoadTactilePaving` —— 盲道（GB 50763 §3.3）

### 5.1 前置条件

已用 `hyRoadIntersection` 生成交叉口；**若未执行 `hyRoadCurbRamp`**，本命令会**自动按默认参数**先布置一轮坡道，保证提示盲道有落点。

### 5.2 用户交互流程

| 步骤 | 提示                                            | 默认值                                 |
| ---- | ----------------------------------------------- | -------------------------------------- |
| 1    | `拾取目标交叉口的任意图元：`                     | 同 hyRoadCurbRamp                      |
| 2    | `提示盲道宽度 (默认 0.60 m)：`                    | `TactilePaving.DefaultStopWidth`       |
| 3    | `行进盲道宽度 (默认 0.30 m)：`                    | `TactilePaving.DefaultAdvanceWidth`    |
| 4    | `行进盲道沿 Leg 延伸长度 (默认 10.0 m)：`         | `TactilePavingDesigner.DefaultAdvanceLengthFromApproach` |

### 5.3 布置策略

| 类型      | 布置规则                                                                         |
| --------- | -------------------------------------------------------------------------------- |
| **Stop**  | 每个 `CurbRamp` 前沿一条，中心线 = `FrontLeft → FrontRight`，宽度 = 提示盲道宽度  |
| **Advance** | 每条 `IntersectionLeg` 左 / 右各一条，中心线 = `ApproachPoint + ±perp · (halfWidth + 0.25) → 再沿 −Inward 退 L m` |

`0.25 m` 来自 GB 50763 §3.3.1（盲道距缘石内边最小净距）。

### 5.4 输出

| 载体                              | 内容                                                      |
| --------------------------------- | --------------------------------------------------------- |
| AutoCAD 模型空间                  | `Polyline`（`ConstantWidth = TactilePaving.Width` 呈带状），图层 `05_hy_道路_盲道`（土黄 42） |
| `Intersection.TactilePavings`     | `TactilePaving[]`                                         |
| `<dwg>.roaddesign.json`           | 随 Intersection 序列化                                      |
| 命令行                            | 宽度范围校核警告 + 生成数                                    |

---

## 6. 数据模型 —— Domain / Xdata / JSON 四层视图

### 6.1 Domain 聚合

```
RoadDesign (aggregate root)
├── Alignments[]                             （P1）
├── Templates[]                              （P2）
├── Corridors[]                              （P5）
├── Nodes[]                                  （v0 老模型）
└── Intersections[]                          ← P3-I1 新增
    ├── Id : Guid                            （稳定 Id，写 Xdata）
    ├── Name : string
    ├── Center : Point2D
    ├── Legs : List<IntersectionLeg>         （按 InwardAngle CCW 排序）
    │   ├── AlignmentId : Guid
    │   ├── ApproachRawDistance : double
    │   ├── ApproachPoint : Point2D
    │   ├── InwardDirection : Vector2D       （单位向量）
    │   ├── HalfWidth : double
    │   └── Tag : string
    ├── CornerArcs : List<CornerArc>         （CCW 相邻两 Leg 之间各一弧）
    │   ├── LegIndexA, LegIndexB : int
    │   ├── Center : Point2D
    │   ├── Radius : double
    │   ├── StartPoint, EndPoint : Point2D
    │   ├── StartAngle, EndAngle : double     （AutoCAD 习惯：X+ 为 0，CCW 正）
    │   └── SweepAngle : double               （CCW 相邻臂恒负 = CW 扫）
    ├── CurbRamps : List<CurbRamp>           ← P3-I1-C
    │   ├── CornerArcIndex : int
    │   ├── Kind : { SingleFace, ThreeFace, Fan }
    │   ├── FrontCenter : Point2D
    │   ├── Tangent, OutwardNormal : Vector2D （单位向量）
    │   ├── Width, Depth, Slope : double
    │   └── 派生：BackCenter, FrontLeft, FrontRight, BackLeft, BackRight, Area
    └── TactilePavings : List<TactilePaving>  ← P3-I1-C
        ├── Id : Guid
        ├── Kind : { Advance, Stop }
        ├── Centerline : List<Point2D>
        ├── Width : double
        └── CornerArcIndex : int              （Stop 绑定，Advance = -1）
```

### 6.2 Xdata（DWG）

| 图元                          | KIND              | ID                 | 图层                        |
| ----------------------------- | ----------------- | ------------------ | --------------------------- |
| `Arc`（转角圆弧）             | `Intersection`    | `Intersection.Id`  | `05_hy_道路_交叉口`           |
| `Polyline`（坡道矩形）        | `CurbRamp`        | `Intersection.Id`  | `05_hy_道路_缘石坡道`         |
| `Polyline`（盲道带宽）        | `TactilePaving`   | `Intersection.Id`  | `05_hy_道路_盲道`             |

> **三种 KIND 共用 `Intersection.Id`**：CurbRamp / TactilePaving 本身是值对象（structs）/ 辅助模型，没有独立稳定 Id；隶属的 Intersection 有。扫描删除某个交叉口时，按 `ID == intersection.Id` 过滤，三种 KIND 一并清空。

### 6.3 JSON（`.roaddesign.json`）

`RoadDesign.Intersections` 是 `Intersection` 的 `List<>`，嵌套 `IntersectionLeg` / `CornerArc`（readonly struct）+ `CurbRamp`（readonly struct）+ `TactilePaving`（class）。

**回归防护**（`RoadJsonExportServiceTests`）：

| 测试                                                      | 目的                                                            |
| --------------------------------------------------------- | --------------------------------------------------------------- |
| `SaveThenLoad_Roundtrip_PreservesIntersection`            | `Legs` / `CornerArcs` 读写不退化（Point2D struct 坑）           |
| `SaveThenLoad_Roundtrip_PreservesCurbRampsAndTactilePavings` | `CurbRamps` / `TactilePavings` 字段完整 + `TactilePaving.Centerline` 列表保留 |
| `SaveForDocument_WithOnlyIntersection_WritesFile`         | 仅含交叉口的 design 不被 `IsEmpty` 误判而跳过落盘                 |

### 6.4 幂等重建

`RoadIntersectionService.RebuildIntersection` + `RoadAccessibilityService.RebuildAccessibility` 都实现为"扫 Xdata 按 ID 清空 → Draw"，保证命令任意次重入产物一致。

---

## 7. 与三方的功能对比

### 7.1 Civil 3D Intersection Wizard

| 步骤 / 字段                  | Civil 3D                         | HyCAD v1.x                            |
| ---------------------------- | -------------------------------- | ------------------------------------- |
| 对象类型                     | 专有对象（Intersection）         | ✅ `Intersection` 聚合                 |
| 创建方式                     | Wizard 5 步 + 预览               | ✅ 命令行 4 提示（无预览）             |
| 向导第 1 步：中心点           | Point on Road Centerline         | ✅ `aroundPoint`                       |
| 向导第 2 步：Assembly / 横断面 | Primary / Secondary Assembly      | ❌（v3 议题，暂走默认半宽）            |
| 向导第 3 步：转角半径         | All Quadrants / By Quadrant       | ⚠ 所有象限同半径（v1.1 `hyRoadIntersectionEdit` 支持逐 corner） |
| 向导第 4 步：进口展宽         | Lane Widening at Intersection    | ❌（留给 P2 Template 的 ApproachWiden） |
| 向导第 5 步：Corridor Region  | Auto-create per-quadrant region  | ❌（v3）                               |
| 规范校核                     | ❌（需人工查手册）                | ✅ `IntersectionCodeChecker`           |
| 编辑                         | 选对象改属性 → 自动重建            | ✅ 重入同命令覆盖（v1.0 无属性对话框） |

### 7.2 鸿业市政道路 "自动交叉口"

| 功能               | 鸿业 9.0                  | HyCAD v1.x                               |
| ------------------ | ------------------------- | ---------------------------------------- |
| 自动转角拟合       | ✅                        | ✅                                       |
| 支持任意臂数       | ✅                        | ✅                                       |
| 路口渠化岛         | ✅                        | ❌                                       |
| 人行横道           | ✅                        | ⚠ v0 `CrosswalkService`（新模型未接入）  |
| 缘石坡道           | ✅                        | ✅ P3-I1-C                               |
| 盲道               | ✅                        | ✅ P3-I1-C                               |
| 停止线             | ✅                        | ❌（P3-I2 `hyRoadMarkStopLine`）         |
| 视距三角形         | ✅                        | ❌（v3 议题）                            |
| 进口展宽           | ✅                        | ❌（P2 Template）                        |
| 批量导出工程量     | ✅                        | ❌（v2，与横断面工程量同批）             |

### 7.3 纬地道路 BIM "平交口 BIM 一键"

| 功能                   | 纬地 2.0                           | HyCAD v1.x                        |
| ---------------------- | ---------------------------------- | --------------------------------- |
| 一键生成平交口三维     | ✅                                 | ❌（v2/v3，Blender-first 路线）    |
| 平面 + 纵面联动         | ✅                                 | ❌                                |
| 动态调整转角半径       | ✅ 夹点                            | ⚠ 重入 `hyRoadIntersection`       |
| 交叉口路拱匹配         | ✅ 自动过渡                        | ❌                                |

---

## 8. 已知局限（v1.0 占位）

| #   | 局限                                                         | 计划                                                  |
| --- | ------------------------------------------------------------ | ----------------------------------------------------- |
| 1   | 所有 CornerArc 使用同一半径                                   | v1.1 `hyRoadIntersectionEdit`（逐 corner 改 R）        |
| 2   | `CornerArc` 之间不画路缘外边线直段（视觉不连续）              | v1.1 `hyRoadIntersectionKerbChain` 配合 `hyRoadAlnOffset` |
| 3   | 进口展宽 / 渐变段未做                                         | P2 Template 的 ApproachWiden 专项                     |
| 4   | 人行横道未接入新 `Intersection` 模型                          | v1.2 从 `CrosswalkService` 迁移                       |
| 5   | 缘石坡道：三面坡 / 扇形坡的几何只占位（实际画的仍是矩形占位）  | v1.2 分类几何（ThreeFace 加侧面坡 + Fan 扇形）          |
| 6   | 盲道：提示盲道只画前沿短段（实际工程中应覆盖整个人行道宽度）  | v1.2                                                   |
| 7   | 无设计速度多段（整条 Alignment 一段）                          | 继承 Alignment 侧 v2 多速度分段                         |
| 8   | 无交叉口导流岛 / 渠化                                          | v3                                                     |
| 9   | 三维 / 纵面联动                                                | v2 Corridor + P7 Blender                               |
| 10  | 停止线 / 导流箭头 / 车道分界线                                 | P3-I2 标线专项                                         |

---

## 9. P3-I1 交付清单

### 9.1 已交付（✅）

| 模块           | 交付物                                                               | 测试                         |
| -------------- | -------------------------------------------------------------------- | ---------------------------- |
| Domain         | `Intersection` / `IntersectionLeg` / `CornerArc` / `CurbRamp` / `TactilePaving` | 39 new（39/39 绿）          |
| Domain 服务    | `IntersectionDesigner` / `CurbRampDesigner` / `TactilePavingDesigner`       | 含在上述                     |
| Domain 校核    | `IntersectionCodeChecker` / `AccessibilityCodeChecker`               | 含在上述                     |
| JSON           | `RoadDesign.Intersections` 嵌套 + `IsEmpty` 联动                       | 3 new（3/3 绿）              |
| Infrastructure | `RoadIntersectionService` / `RoadAccessibilityService`               | `HyRoadLayers` 图层计数 +3   |
| 图层           | `05_hy_道路_交叉口` / `05_hy_道路_缘石坡道` / `05_hy_道路_盲道`         |                              |
| Xdata          | KIND = `Intersection` / `CurbRamp` / `TactilePaving`，共用 `Intersection.Id` |                              |
| Presentation   | `hyRoadIntersection` / `hyRoadCurbRamp` / `hyRoadTactilePaving`      | 在 AutoCAD 内联调（无自动化） |
| DI             | Autofac 单例注册                                                      |                              |
| 命令注册       | `ReCall/commands.json` order=55/56/57                                |                              |
| 单测总计       | 562 / 564 全绿（2 跳过 = AutoCAD 依赖预期）                           | 从 513 → 562（+49）          |

### 9.2 v1.1 进行中（根据 §8 局限）

- [x] `hyRoadIntersectionEdit`：逐 corner 改 R、修改 Legs.HalfWidth、改 DesignSpeed（§8-1）
   - Domain：`IntersectionDesigner.TryRebuildCornerArc(intersection, index, newRadius)` +
     `IntersectionDesigner.UpdateLegHalfWidth(intersection, legIndex, newHalfWidth)`；
   - 命令交互：拾取一条转角弧 → 选择 `R/W/V/X` → 输入新值 → 局部重画（不破坏其他弧）→
     若已存在 CurbRamp / TactilePaving 则联动 `RebuildAccessibility` → JSON 落盘 + 再校核；
   - 非破坏性：`Intersection.Legs` 排序不重排，未触及的 `CornerArc.Center/Radius` 严格不变；
   - 测试：`IntersectionDesignerLocalEditTests` 9 个（9/9 绿），单测覆盖参数校验 + 影响面隔离。
- [ ] `hyRoadIntersectionKerbChain`：连接相邻 CornerArc 的路缘外边线直段（§8-2）
- [ ] `hyRoadIntersectionCrosswalk`：把 `CrosswalkService` 迁到新 Intersection 聚合（§8-4）
- [ ] CurbRamp 分类几何：ThreeFace / Fan（§8-5）

### 9.3 v1.2+ 待补

- [ ] P3-I2 路面标线（停止线 / 导流箭头 / 车道分界 / 禁停网格）
- [ ] P3-I3 交通标志牌（块库 + 插入 + 规范校核）
- [ ] 交叉口进口展宽（P2 Template 协作）

---

## 10. 速查：命令 / 图层 / Xdata 对照

### 10.1 命令行

| 命令                    | 类                                                     | 说明                                    |
| ----------------------- | ------------------------------------------------------ | --------------------------------------- |
| `hyRoadIntersection`     | `RoadIntersectionCommand`                              | 从 Alignment 生成转角圆弧               |
| `hyRoadIntersectionEdit` | `RoadIntersectionEditCommand`                          | 局部改单弧 R / 单臂 HalfWidth / 设计速度 |
| `hyRoadCurbRamp`         | `RoadCurbRampCommand`                                  | 布置缘石坡道（+ 重建盲道）              |
| `hyRoadTactilePaving`    | `RoadTactilePavingCommand`                             | 布置盲道（若无坡道则自动先布置坡道）     |

### 10.2 AutoCAD 图层

| 图层                            | 色号 | 实体                | 对应 Xdata KIND   |
| ------------------------------- | :--: | ------------------- | ----------------- |
| `05_hy_道路_交叉口`              | 1（红）   | `Arc`               | `Intersection`    |
| `05_hy_道路_缘石坡道`            | 11（淡红） | 闭合 `Polyline`     | `CurbRamp`        |
| `05_hy_道路_盲道`                | 42（土黄） | 带宽 `Polyline`     | `TactilePaving`   |

### 10.3 HY_ROAD Xdata KIND 规约

| KIND              | 含义                 | ID 语义                 |
| ----------------- | -------------------- | ----------------------- |
| `Alignment`       | 平面线位中心线        | `Alignment.Id`          |
| `Intersection`    | 交叉口转角圆弧        | `Intersection.Id`       |
| `CurbRamp`        | 缘石坡道矩形          | `Intersection.Id` *     |
| `TactilePaving`   | 盲道带宽多段线        | `Intersection.Id` *     |

> *：CurbRamp / TactilePaving 本身不独立有 Id，隶属的 Intersection 有；扫描按 Intersection.Id 过滤即可整体删除。

---

## 11. 参考文献

- **CJJ 37-2012**《城市道路工程设计规范》附录 B：平面交叉口缘石转弯半径推荐表
- **CJJ 152-2010**《城市道路交叉口设计规程》§6.2 / §6.4.2
- **GB 50763-2012**《无障碍设计规范》§3.2 / §3.3
- Autodesk Civil 3D 2024–2026 Help: *Creating Intersections* / *Modeling Intersection Objects*
- 鸿业市政道路 9.0 用户手册 §6 "交叉口设计"
- 纬地道路 BIM 2.0 "平交口 BIM 一键" 白皮书
