# Transoft AutoTurn 分析：借鉴与超越

> 导航：[README](./README.md) · [01MASTER 总纲](./01MASTER.md) · [02INDEX 总对比](./02Software_Overview_INDEX.md) · [03RoadSelect 选型](./03RoadSelect.md)

AutoTurn 是加拿大 Transoft Solutions 的**车辆扫略路径分析（Swept Path Analysis）**软件，市政道路领域"**转弯/视距专项**"的事实标准。它解决的是其他所有设计软件都回避的问题：**"我画的这个交叉口/停车场/回车场，大货车 / 公交 / 消防车能不能开进去？"** 对 HyCAD，AutoTurn 代表的是**"交通工程校验"** 这一独立模块，v2 值得重点投入。

---

## 一、值得借鉴的 AutoTurn 核心设计理念

### 1.1 车辆库（1700+ 标准车辆）

AutoTurn 最有价值的资产是**庞大的车辆参数库**——覆盖：

```
AutoTurn 车辆库
├── 标准车辆（按国家/设计规范）
│    ├── 美国 AASHTO（WB-67, SU-30, ...）
│    ├── 欧洲 EC（16.5m trailer, bus, ...）
│    ├── 澳洲 Austroads
│    ├── 中国 GB/CJJ（小型车 / 普通汽车 / 铰接公交 / 铰接货车）
│    └── ...
├── 特定制造商车辆
│    ├── 梅赛德斯消防车
│    ├── 沃尔沃货车
│    └── ...（900+ 真实型号）
├── 自行车 / 电动车（12+ 种）
├── 工程车辆（叉车、起重机、搅拌车）
└── 自定义车辆（用户输入轴距 / 轮距 / 转向角 / 外轮廓）
```

每辆车定义的核心参数：

```csharp
public sealed class VehicleDefinition
{
    public string Code { get; init; }                // "CN-BUS-LENGTH12"
    public string DisplayName { get; init; }         // "12m 普通公交车"
    public string Standard { get; init; }            // "GB 1589-2016"

    // 几何
    public double TotalLength { get; init; }         // 全长
    public double Width { get; init; }
    public double Height { get; init; }

    // 轴配置
    public IReadOnlyList<VehicleAxle> Axles { get; init; }
    public double WheelBase => Axles[^1].Offset - Axles[0].Offset;   // 轴距

    // 转向
    public double MaxSteeringAngle { get; init; }    // 最大前轮转角（度）
    public double MinTurnRadius { get; init; }        // 最小转弯半径（m）

    // 多段铰接（拖挂车 / 铰接公交）
    public IReadOnlyList<VehicleSection> Sections { get; init; }
}

public sealed record VehicleAxle(double Offset, double Width, bool IsSteering);
public sealed record VehicleSection(string Name, double Length, double PivotOffset);
```

**HyTool 借鉴**：

- 内置**中国常用 20-30 种车辆**（GB 1589 / CJJ 37 推荐的车型）
- 支持用户自定义车辆（JSON 存储）
- 首批车辆：小型车、普通汽车、铰接公交、8 米公交、12 米公交、18 米铰接公交、小型货车、普通货车、10 吨货车、16 吨货车、半挂车、消防车（云梯 / 水罐）、救护车、垃圾车、自行车、电瓶车

```
HyCADTool.Refactored/Resources/Road/Vehicles/
├── vehicle-catalog.json                  # 车辆总目录
├── cn/
│    ├── gb1589-small-car.json
│    ├── gb1589-normal-truck.json
│    ├── cjj37-public-bus-12m.json
│    ├── cjj37-articulated-bus-18m.json
│    └── ...
├── international/
│    ├── aashto-wb67.json
│    └── ...
└── user-custom/                          # 用户自定义
```

### 1.2 扫略路径的几何算法

AutoTurn 核心算法 — **自行车模型（Bicycle Model）扫略**：

```mermaid
graph LR
    subgraph bicycle ["自行车模型"]
        FrontAxle["前轴中心"]
        RearAxle["后轴中心"]
        WB["轴距 L"]
        SteerAngle["前轮转角 δ"]
    end

    subgraph output ["扫略包络"]
        FrontPath["前轴轨迹"]
        RearPath["后轴轨迹"]
        LeftEnv["左侧外包络"]
        RightEnv["右侧外包络"]
    end

    bicycle --> output
```

给定前轴轨迹（用户指定）+ 轴距 L + 转角约束，反算后轴轨迹和车体包络。对挂车/铰接车，级联递推。

**HyTool 借鉴**（`VehicleTrackingService`）：

```csharp
public sealed class VehicleTrackingService
{
    public SweptPathResult Track(
        VehicleDefinition vehicle,
        IReadOnlyList<Point3d> driverPath,   // 驾驶员指定路径
        SweptPathOptions opt);
}

public sealed class SweptPathResult
{
    public IReadOnlyList<Point3d> FrontAxlePath { get; }
    public IReadOnlyList<Point3d> RearAxlePath { get; }
    public IReadOnlyList<Polygon2d> VehicleEnvelopes { get; }   // 每个步长的车体外框
    public Polygon2d OuterEnvelope { get; }                      // 合并后的外包络
    public IReadOnlyList<SteerAngleWarning> Warnings { get; }    // 超过最大转角的点
}
```

### 1.3 SmartPath — 智能路径

AutoTurn 的 **SmartPath**：**用户指定若干控制点 + 速度，自动生成符合车辆动力学的转弯路径**。不要求手工画每一步。

这对设计师极友好——不用具备车辆工程知识，软件"替你开一遍"。

**HyTool 策略**：

```csharp
public sealed class SmartPathRequest
{
    public VehicleDefinition Vehicle { get; init; }
    public IReadOnlyList<Point3d> ControlPoints { get; init; }    // 起点 + 途经点 + 终点
    public double MaxSpeedKmh { get; init; }                       // 最大速度
    public double? MaxLateralAcceleration { get; init; }           // 最大侧向加速度（m/s²）
}

public SweptPathResult SmartPath(SmartPathRequest req);
```

### 1.4 视距检查

AutoTurn 不止车辆路径，还包含**视距分析**：

- **停车视距（SSD）**：司机察觉障碍到完全停下所需距离
- **会车视距**：两车相向距离
- **超车视距**：超车所需距离
- **交叉口视距三角形**：符合 CJJ 152 § 5.1.4 的视距三角形

```csharp
public interface ISightDistanceService
{
    SightDistanceResult CheckStoppingSightDistance(
        IAlignment alignment,
        double designSpeed,
        ITerrain? terrain,
        IReadOnlyList<SightObstruction> obstructions);

    SightTriangleResult CheckIntersectionSightTriangle(
        Intersection isec,
        IReadOnlyList<SightObstruction> obstructions);
}

public sealed record SightObstruction(
    Polygon2d Footprint,     // 建筑/树/墙平面轮廓
    double Height);          // 高度（用于判断是否遮挡驾驶员视线）
```

### 1.5 间隙分析（2D / 3D）

AutoTurn Pro 的 **Clearance Analysis**：检测车体与：

- 路缘石（横向间隙）
- 路灯杆 / 树 / 护栏（横向）
- 桥梁下横梁、隧道顶（垂直，**净空**）
- 坡道顶面凸起（底盘刮擦）

**HyTool 策略**（v2+）：

```csharp
public interface IVehicleClearanceService
{
    ClearanceReport CheckLateral(
        SweptPathResult path,
        IReadOnlyList<IClearanceObstacle> obstacles,
        double minLateralClearance);

    ClearanceReport CheckVertical(
        SweptPathResult path,
        VehicleDefinition vehicle,
        ITerrain groundProfile,
        double minVerticalClearance);
}
```

### 1.6 AutoTurn Online — 浏览器版

AutoTurn Online 是简化版，**浏览器中上传 DWG 即可做基础转弯模拟**。这代表未来 Web 化趋势，但 HyCAD 当前场景不适用。

### 1.7 3D 可视化（Pro 版）

AutoTurn Pro 支持在 3D 场景里播放车辆行驶动画，用于向业主展示。

**HyTool 策略**：v3 考虑，v1 只做 2D 平面包络。

---

## 二、必须超越 AutoTurn 的缺点

### 2.1 纯插件，无设计能力

AutoTurn 只做"校验"，不做"设计"。一旦发现问题，设计师必须切回 AutoCAD / Civil 3D 改设计。

**HyTool 应对**：**整合到同一个 HyToolPanel**——发现视距三角形不满足 → 直接在面板上跳到 Alignment 编辑 → 改完即刻重新校验。**零切换**。

### 2.2 中国车辆库覆盖不全

虽然 AutoTurn 号称 1700+ 车辆，但中国标准车辆仅寥寥数款，且不一定对齐 GB 1589 最新版。

**HyTool 应对**：首发就把中国 20-30 种常用车型内置，参数取自 GB 1589-2016 + CJJ 37-2012。

### 2.3 价格高

AutoTurn Pro 单席位年费 $3000+，国内中小设计院难承受。

**HyTool 应对**：随 HyCAD 集成。

### 2.4 与道路设计数据隔离

AutoTurn 不知道"这是一条市政主干路"——需要用户手工指定"在 CL 上走""起点 K0+100 终点 K0+300"等信息。

**HyTool 应对**：车辆跟踪命令**直接消费 `IAlignment` / `Intersection` 对象**——选一条 Alignment 即可运行，无需手工输入。

### 2.5 对"车辆动力学"的抽象有限

AutoTurn 基于自行车模型，对侧倾 / 滑移 / 紧急制动的模拟有限，做不到真实驾驶仿真。

**HyTool 应对**：v1/v2 沿用自行车模型（够用），v3 可接 **Unity / UE 驾驶仿真** 做高阶验证。

---

## 三、映射到 HyCADTool.Refactored 的设计

### 3.1 命令挂点

| 命令 | 功能 |
|------|------|
| `hyRoadVehicle` | 打开车辆库浏览器 |
| `hyRoadTrack` | 指定车辆 + 路径，生成扫略包络 |
| `hyRoadSmartPath` | 智能路径（控制点 + 速度） |
| `hyRoadSsd` | 停车视距检查 |
| `hyRoadSightTri` | 交叉口视距三角形（自动取当前 `Intersection`） |
| `hyRoadTurnAround` | 回车场可达性检查（消防车） |
| `hyRoadClearance` | 车辆间隙检查（v2+） |

### 3.2 首批内置车辆清单

| 代号 | 名称 | 规范来源 | 长×宽×高 | 轴距 | 最小转弯半径 |
|------|------|----------|----------|------|--------------|
| `cn-car-small` | 小型车（如家用轿车） | CJJ 37 表 3.2.2-1 | 6.0×1.8×1.5 | 3.8 | 6.0 |
| `cn-car-normal` | 普通汽车（工程参考） | CJJ 37 表 3.2.2-1 | 12.0×2.5×4.0 | 6.5 | 12.0 |
| `cn-truck-articulated` | 铰接车 | CJJ 37 表 3.2.2-1 | 18.0×2.5×4.0 | 5.8+6.7 | 15.5 |
| `cn-bus-8m` | 8 米公交车 | GB 1589 | 8.0×2.5×3.0 | 4.5 | 9.5 |
| `cn-bus-12m` | 12 米公交车 | GB 1589 | 12.0×2.5×3.2 | 6.5 | 12.0 |
| `cn-bus-artic-18m` | 18 米铰接公交 | GB 1589 | 18.0×2.5×3.2 | 5.4+6.3 | 13.5 |
| `cn-truck-light` | 轻型货车（5 吨以下） | GB 1589 | 6.0×2.0×2.5 | 3.5 | 7.5 |
| `cn-truck-normal` | 普通货车（10-20 吨） | GB 1589 | 9.0×2.5×3.5 | 5.5 | 10.5 |
| `cn-truck-semi` | 半挂车（40 吨） | GB 1589 | 16.5×2.5×4.0 | 4.5+10 | 13.0 |
| `cn-firetruck-water` | 水罐消防车 | GA/T 972 | 10.5×2.5×3.5 | 5.5 | 12.0 |
| `cn-firetruck-ladder` | 云梯消防车 | GA/T 972 | 12.5×2.5×4.0 | 6.8 | 14.0 |
| `cn-ambulance` | 救护车 | 行业标准 | 5.5×1.9×2.3 | 3.2 | 6.5 |
| `cn-garbage-truck` | 垃圾车 | 行业标准 | 8.5×2.5×3.3 | 5.0 | 10.5 |
| `cn-bicycle` | 自行车 | GB 3565 | 1.9×0.6×1.1 | 1.0 | 1.5 |
| `cn-ebike` | 电动自行车 | GB 17761 | 1.9×0.7×1.1 | 1.1 | 1.5 |
| `cn-scooter-moped` | 电动摩托车 | — | 2.1×0.75×1.2 | 1.3 | 1.8 |

### 3.3 视距三角形（CJJ 152 § 5.1.4）

```csharp
public sealed class SightTriangleService
{
    public SightTriangleResult Check(
        Intersection isec,
        double designSpeed,
        IReadOnlyList<SightObstruction> obstructions)
    {
        // 1. 取两条进口臂的 Alignment
        // 2. 按设计速度取识别视距 Ss：CJJ 152 表 5.1.4
        // 3. 从冲突点向两条臂各倒退 Ss，构成三角形
        // 4. 检查三角形内有无 obstruction（高度 > 1.2m）
        // 5. 输出：是否满足 / 阻挡对象列表 / 建议退让距离
    }
}
```

### 3.4 `hy-settings.json` 扩展

```json
{
  "Road": {
    "VehicleTracking": {
      "DefaultVehicle": "cn-bus-12m",
      "DefaultLateralClearance": 0.5,
      "StepInterval": 1.0,
      "ShowEnvelope": true,
      "EnvelopeOffset": 0.1,
      "EnvelopeColor": 1
    },
    "SightDistance": {
      "DesignSpeedForSsd": 50,
      "ObstructionHeight": 1.2,
      "ObserverEyeHeight": 1.08,
      "ObjectHeight": 0.5
    }
  }
}
```

### 3.5 面板 UI（视距与转弯专项）

```
┌ 道路 Tab → 交通校核 ──────────────────┐
│ ● 车辆扫略                              │
│   车辆 [12m 公交 ▼] [库]                │
│   [选路径]  [智能路径]  [分析]          │
│                                         │
│ ● 视距                                  │
│   [停车视距]  [会车视距]                │
│   [超车视距]  [交叉口视距三角形]        │
│                                         │
│ ● 间隙（v2）                            │
│   最小横向间隙 [0.5] m                  │
│   最小竖向净空 [4.5] m                  │
│   [检查当前]                            │
│                                         │
│ 报告: 2 项警告 / 0 项错误               │
│ [查看详情]  [导出 PDF]                  │
└─────────────────────────────────────────┘
```

---

## 四、总结：借鉴 vs 超越

| AutoTurn 设计 | 借鉴 | HyCAD 超越 |
|---------------|------|-------------|
| 庞大车辆库 | 借鉴 | 中国车辆 20-30 种开箱即用 |
| 自行车模型扫略算法 | 完全借鉴 | C# 原生实现 |
| SmartPath 智能路径 | 完全借鉴 | `hyRoadSmartPath` |
| 视距检查 | 完全借鉴 | 集成 CJJ 152 视距三角形 |
| 间隙分析 2D/3D | 借鉴 | v2 落地 |
| 车辆库自定义 | 借鉴 | JSON 存储，可手改 |
| 3D 可视化 | 借鉴 | v3 考虑 |
| AutoTurn Online 浏览器 | **暂不做** | HyCAD 聚焦桌面 |
| 纯插件无设计能力 | **避免** | 与设计模块同一面板 |
| 中国车辆覆盖不全 | **避免** | 首发就全 |
| 高价 | **避免** | 随 HyCAD 集成 |
| 与设计数据隔离 | **避免** | 直接消费 Alignment / Intersection |

---

## 五、与其他对标篇的交叉引用

- [Civil3D.md](./Civil3D.md)：Autodesk 也有 Vehicle Tracking（收购自 Transoft 竞品），与 AutoTurn 思路类似
- [EICAD.md](./EICAD.md)：VRRoad 驾驶模拟的专业升级路径
- [HongYeRoad.md](./HongYeRoad.md)：鸿业无车辆扫略功能，这正是 HyCAD 的差异化优势
- [01MASTER.md § 九](./01MASTER.md#九规范清单)：CJJ 152 § 5.1.4 视距三角形条款
