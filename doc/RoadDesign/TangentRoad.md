# 天正市政道路设计（TDL）分析：借鉴与超越

> 导航：[README](./README.md) · [01MASTER 总纲](./01MASTER.md) · [02INDEX 总对比](./02Software_Overview_INDEX.md) · [03RoadSelect 选型](./03RoadSelect.md)

天正软件是国内 AutoCAD 二次开发的**元老级厂商**，"天正建筑 T20/T30" 几乎是全国建筑院的标配。天正市政道路设计（TDL）虽然在市场份额上不如鸿业/纬地，但它代表了"**最符合中国国标制图习惯**"的 UX 流派——图块库、图块屏蔽、夹点动态块、标注样式，都被国内工程师熟悉到肌肉记忆。HyCAD 的出图语言应向天正看齐。

---

## 一、值得借鉴的天正核心设计理念

### 1.1 图库管理（TKW 格式）

天正最耐看的资产是它的 **图库**——按类别分组的图块集合：

```
天正图库（*.tkw）
├── 车辆图库
│    ├── 小汽车 / 巴士 / 货车 / 消防车
├── 植物图库
│    ├── 乔木 / 灌木 / 绿篱
├── 标志图库
│    ├── 警告 / 禁令 / 指示 / 指路
├── 标线图库
│    ├── 导向箭头（直行/左转/右转/掉头/组合）
│    ├── 文字标线（30/50/80...）
│    └── 禁止标线（斑马线/禁停网格）
├── 家具图库
└── 设施图库
     ├── 路灯 / 信号灯 / 公交站牌 / 果皮箱 / 指示牌
```

图库特性：

- **拖拽排序**：UI 直接拖动调整显示顺序
- **批量改名**：一键重命名一组图块
- **自动命名**：按分类与序号自动生成图块名
- **图块屏蔽**：图块的背景可自动遮挡下层实体（无需 wipeout）

**HyTool 借鉴**：引入 `RoadBlockLibrary` 机制：

```csharp
public interface IRoadBlockLibrary
{
    string FilePath { get; }                                   // *.hytkw（JSON）
    IReadOnlyList<RoadBlockCategory> Categories { get; }
    RoadBlockDefinition? Find(string code);
    void Register(RoadBlockDefinition def);
    void Export(string path);
}

public sealed class RoadBlockDefinition
{
    public string Code { get; init; }                          // "Arrow.Straight"
    public string DisplayName { get; init; }                   // "直行箭头"
    public string Category { get; init; }                      // "标线/导向箭头"
    public string DwgFilePath { get; init; }                   // 图块几何源文件
    public IReadOnlyDictionary<string, ParameterDef> DynamicParameters { get; }  // 动态块参数
    public bool EnableMasking { get; init; }                   // 图块屏蔽
}
```

初始图库（随 HyCAD 分发）：

```
HyCADTool.Refactored/Resources/Road/Blocks/
├── markings/
│    ├── arrow-straight.dwg
│    ├── arrow-left.dwg
│    ├── arrow-right.dwg
│    ├── arrow-u-turn.dwg
│    └── arrow-combined-*.dwg
├── signs/
│    ├── warning-*.dwg
│    ├── prohibition-*.dwg
│    ├── indication-*.dwg
│    └── guide-*.dwg
├── facilities/
│    ├── streetlight-*.dwg
│    ├── traffic-signal.dwg
│    ├── bus-stop.dwg
│    └── trash-bin.dwg
└── vehicles/
     └── (AutoTURN 可覆盖这一类)
```

### 1.2 图块屏蔽 — "自动擦除背景"

天正图块的**图块屏蔽**功能：图块内置一个透明遮罩，插入时自动遮挡底图。例如在平面图上插入"公交站台"图块，图块会自动遮住下面的车行道线。

**AutoCAD 原生方式**：

- 用 `WIPEOUT` 命令手工画遮罩
- 或者用 Dynamic Block 内嵌 Wipeout 实体

**天正方式**：图块内预置 `Wipeout` 实体 + 控制它的可见性的属性。

**HyTool 策略**：复用这个机制——所有 `RoadBlockDefinition` 若 `EnableMasking = true`，在图块生成脚本中自动加入 `Wipeout`。

### 1.3 夹点动态块 — 最高性价比的参数化

天正对 AutoCAD 动态块的定义习惯：**五个夹点**：

```
     [右上]
        |
[左] — [中心] — [右]
        |
     [左下]

- 中心夹点：移动整个图块
- 四角夹点：对角缩放
- 右侧夹点：旋转
```

配合"对象编辑"弹窗，用户可以：

- 双击图块修改参数（宽度、角度）
- 拖夹点快速调整
- 多个图块同参数联动（通过 Block Name 或 Layer 关联）

**HyTool 借鉴**：HyCAD 现有命令已大量使用 AutoCAD 动态块，继续沿用——**不引入专有的"参数化图块"格式**，直接使用 DWG 动态块语义。

### 1.4 标注样式按比例分级

天正标注样式的组织：

```
HY-Dim-30   — 比例 1:30
HY-Dim-50   — 比例 1:50
HY-Dim-100  — 比例 1:100
HY-Dim-200  — 比例 1:200
HY-Dim-500  — 比例 1:500
HY-Dim-1000 — 比例 1:1000
```

文字高度 / 箭头大小 / 偏移距离都按比例自动缩放。用户切换"比例"下拉框，标注样式联动切换。

**HyTool 现状**：[`SettingsPanelViewModel.cs`](../../HyCADTool.Refactored/Presentation/ViewModels/SettingsPanelViewModel.cs) 已经支持 `Scale` 字段并在 `EnsureStylesApplied` 中按比例生成样式名，这与天正一致。**不需要新增，保持即可**。

### 1.5 文字样式与图层的成套配合

天正的图层命名规则被设计院广泛习惯：

```
0-建筑-轴线      →  HyCAD: 0-road-中心线
0-建筑-柱        →  HyCAD: 0-road-车行道-边线
0-建筑-墙        →  HyCAD: 0-road-路缘石
0-建筑-标注      →  HyCAD: 0-road-标注
```

HyCAD 现有命名（`0-road-辅助线` / `0-road-标线-人行横道-实线` / `0-road-标线-停止线`）已符合天正风格。

### 1.6 命令首字母简写 + 多字母组合

天正命令多为双字母（`WL` = 墙线，`CZ` = 柱子），易记。

HyCAD 延续"hy" 前缀 + 语义后缀，如 `hyRoad` / `hyRoadLane` / `hyRoadProf`。

### 1.7 路网广场设计

天正 TDL 的"路网广场设计"功能专门处理**非矩形地块**中的道路布置，如广场、学校、医院等。

**HyTool 策略**：v3+ 考虑，v1/v2 聚焦标准市政道路。

### 1.8 对 T20/T30 的升级路径

天正对 AutoCAD 版本跟进较快，T20 v10 已支持 AutoCAD 2026。

**HyTool 现状**：HyCADTool.Refactored 项目目标即对标最新 AutoCAD 版本，与天正策略一致。

---

## 二、必须超越天正的缺点

### 2.1 工具多但设计深度浅

天正的强项是"出图快"，弱项是"设计弱"——没有走廊模型、没有规范校核引擎、没有 BIM 输出。对需要正向设计的项目不够用。

**HyTool 应对**：在"出图语言"对齐天正的基础上，补齐"**设计深度**"——走廊、规范、BIM 全都要。

### 2.2 扩展性差

天正图库是二进制 TKW 格式，用户自定义图块需要通过天正的"图库管理"工具，不能直接用 DWG 导入。

**HyTool 应对**：图库用文件夹 + 元数据 JSON 的形式，直接加入 DWG 就被识别。

### 2.3 协作与版本管理弱

天正没有模型版本、没有多人协作、没有云端。

**HyTool 应对**：`.roaddesign` 文件 + 本地快照；远期可接 git。

### 2.4 规范升级响应慢

天正对 GB 5768-2022 等最新规范的响应通常比鸿业/纬地慢 1-2 年。

**HyTool 应对**：规范规则 JSON 化，由社区或用户即时更新。

### 2.5 BIM 化几乎空白

T20/T30 一直是 2D 思维，BIM 正向设计几乎不做。

**HyTool 应对**：Domain 为 BIM 预留（见 [HintCAD.md § 1.6](./HintCAD.md#16-三维-bim-正向设计)、[Novapoint.md § 1.5](./Novapoint.md#15-ifc-43-road--bim-国际标准)）。

---

## 三、映射到 HyCADTool.Refactored 的设计

### 3.1 图库结构（首批内置）

```
HyCADTool.Refactored/Resources/Road/Library/
├── library-manifest.json          # 图库元数据
├── markings/
│    ├── arrows/
│    │    ├── straight.dwg
│    │    ├── left-turn.dwg
│    │    ├── right-turn.dwg
│    │    ├── u-turn.dwg
│    │    ├── straight-left.dwg
│    │    ├── straight-right.dwg
│    │    └── left-right.dwg
│    ├── text/
│    │    ├── speed-30.dwg
│    │    ├── speed-40.dwg
│    │    └── ...
│    ├── grids/
│    │    ├── no-stop-grid.dwg
│    │    └── keep-clear-grid.dwg
│    └── special/
│         ├── crosswalk-strip.dwg   # 已在 CrosswalkService 中程序化生成
│         └── stop-line.dwg
├── signs/
│    ├── warning/                   # GB 5768.2 警告标志
│    ├── prohibition/                # 禁令标志
│    ├── indication/                 # 指示标志
│    └── guide/                      # 指路标志
├── facilities/
│    ├── streetlights/
│    ├── signals/                   # 信号灯杆
│    ├── bus-stops/
│    ├── trash/
│    └── benches/
└── accessibility/
     ├── tactile-paving.dwg         # 盲道
     └── curb-ramp.dwg              # 缘石坡道
```

`library-manifest.json`：

```json
{
  "version": "1.0",
  "name": "HyCAD 市政道路图库",
  "standard": "GB 5768.1~5-2022",
  "categories": [
    {
      "code": "marking.arrow",
      "displayName": "标线/导向箭头",
      "blocks": [
        {
          "code": "marking.arrow.straight",
          "displayName": "直行箭头",
          "file": "markings/arrows/straight.dwg",
          "scales": [1, 2, 3],
          "enableMasking": false,
          "dynamicParameters": {
            "Length": { "type": "number", "default": 4.0, "min": 3.0, "max": 9.0 }
          }
        }
      ]
    }
  ]
}
```

### 3.2 命令挂点

| 命令 | 功能 | 天正对应 |
|------|------|----------|
| `hyRoadBlockLib` | 打开图库浏览器 | 图库管理 |
| `hyRoadArrow` | 插入导向箭头（从图库选） | 标线箭头 |
| `hyRoadSign` | 插入标志牌 | 标志图块 |
| `hyRoadSpeedText` | 插入速度文字标线 | 速度文字 |
| `hyRoadGrid` | 绘制禁停网格 | 禁停网格 |
| `hyRoadStreetLight` | 布置路灯（沿 Alignment 等间距） | 路灯布置 |

### 3.3 图库浏览面板（建议 WPF）

```
┌ 道路图库 ───────────────────────────┐
│ [搜索: _______]                      │
│                                      │
│ 分类                                  │
│ ├─ 标线                               │
│ │   ├─ 导向箭头 (15)                 │
│ │   ├─ 文字标线 (12)                 │
│ │   ├─ 禁止网格 (3)                  │
│ │   └─ 特殊标线 (5)                  │
│ ├─ 标志                               │
│ │   ├─ 警告标志 (36)                 │
│ │   ├─ 禁令标志 (48)                 │
│ │   ├─ 指示标志 (27)                 │
│ │   └─ 指路标志 (53)                 │
│ ├─ 设施                               │
│ └─ 无障碍                             │
│                                      │
│ ┌─────┬─────┬─────┐                  │
│ │ ↑   │ ←   │ →   │ 直行/左转/右转   │
│ ├─────┼─────┼─────┤                  │
│ │ U   │ ↑←  │ ↑→  │                  │
│ └─────┴─────┴─────┘                  │
│                                      │
│ 选中 "直行箭头"                       │
│   长度  [4.0] m                       │
│   比例  [1:500 ▼]                    │
│   [插入]  [批量沿 Alignment 布置]    │
└──────────────────────────────────────┘
```

### 3.4 沿 Alignment 批量布置

天正没有"沿道路中心线等间距自动布置设施"的功能——HyCAD 可以**超越**：

```csharp
public sealed class AlignmentBatchPlacement
{
    public IAlignment Along { get; init; }
    public string BlockCode { get; init; }          // 图库中的图块
    public double Offset { get; init; }              // 偏离中心线的距离
    public PlacementSide Side { get; init; }         // Left / Right / Both
    public double SpacingInterval { get; init; }    // 等间距
    public Station? Start { get; init; }
    public Station? End { get; init; }
    public bool RotateToAlignment { get; init; } = true;   // 是否跟随 Alignment 方位角
}
```

用户场景：

- 路灯：`Offset=7.5`, `Side=Both`, `SpacingInterval=30`, `RotateToAlignment=true`
- 公交站台：`Offset=3.5`, `Side=Right`, 手动桩号，不等间距
- 果皮箱：`Offset=1.0`, `Side=Both`, `SpacingInterval=50`

---

## 四、总结：借鉴 vs 超越

| 天正设计 | 借鉴 | HyCAD 超越 |
|----------|------|-------------|
| 图库管理（分类+自动命名） | 完全借鉴 | 用 JSON manifest + DWG 文件，可直接编辑 |
| 图块屏蔽 | 完全借鉴 | 通过 Wipeout 预置 |
| 夹点动态块 | 完全借鉴 | 沿用 AutoCAD 原生动态块 |
| 标注样式按比例分级 | **已实现** | 已在 SettingsPanelViewModel |
| 图层命名规则 | **已对齐** | `0-road-*` 符合习惯 |
| 命令简写 | 完全借鉴 | `hy` 前缀 + 语义后缀 |
| 路网广场设计 | 借鉴 | v3+ |
| 设计深度浅 | **避免** | 补齐走廊/规范/BIM |
| 图库扩展性差 | **避免** | DWG + JSON 开放格式 |
| 协作版本弱 | **避免** | 本地快照 |
| 规范升级慢 | **避免** | 规则 JSON 化 |
| 2D 思维 | **避免** | Domain 为 BIM 预留 |

---

## 五、与其他对标篇的交叉引用

- [HongYeRoad.md](./HongYeRoad.md)：天正的出图语言 + 鸿业的设计深度 = HyCAD v1 的基本面
- [01MASTER.md § 附](./01MASTER.md#附命名前缀约定)：图层/样式/命令命名约定表
- [03RoadSelect.md](./03RoadSelect.md)：图库按分类/属性的双轴组织
