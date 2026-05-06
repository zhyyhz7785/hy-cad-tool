# AutoCAD 几何图形结构化传递给 AI 方案

## 1. 背景

当前问题不是 AI 完全不能理解 DWG，而是 **DWG 是 AutoCAD 私有工程数据库**，里面同时包含几何、样式、图层、块、标注、字典、扩展数据、视图状态等大量上下文。直接把 DWG 文件交给 AI，会遇到三个核心障碍：

1. **二进制不可读**：AI 无法稳定直接解析 DWG 的内部对象图。
2. **工程语义混杂**：同一张图里既有有效设计对象，也有底图、构造线、临时线、块定义、布局对象。
3. **关注范围不明确**：软件开发调试时通常只需要“这一次命令相关的几何集合”，而不是整张图。

因此更好的方向是：在 AutoCAD 内部用 .NET API 读取 DWG 数据库，把用户选中的对象转换成版本化、可读、可压缩、可分块的结构化数据，再交给 AI 分析。

## 2. 各种传递方式对比

| 方式 | 优点 | 缺点 | 适合场景 | 结论 |
|---|---|---|---|---|
| 截图 / 屏幕录制 | 最直观，AI 能看到图形关系 | 没有精确坐标、对象类型、图层、半径、文字内容难保证 | 解释界面现象、视觉排版、崩溃前状态 | 只能做辅助，不适合作为几何分析主通道 |
| 手工描述 | 成本低，不需要开发工具 | 容易漏信息，复杂图形无法完整表达 | 极小问题、概念沟通 | 只适合补充背景 |
| 直接上传 DWG | 保真度最高 | AI 侧通常不能直接解析，且包含过多无关数据 | 理论上最好，当前不可控 | 不作为主方案 |
| 导出 DXF | 文本格式，包含较完整 CAD 对象 | 文件很大，结构冗长；AI 需要从低层 DXF group code 中反推语义 | 作为原始兜底、第三方交换 | 可作为备份，不适合作为日常 AI 调试格式 |
| SVG / PDF | 视觉矢量清晰 | 丢失 CAD 工程语义，圆弧/块/标注常被打散 | 版式、出图效果检查 | 适合作为视觉旁路 |
| CSV / 表格 | 简单、短小、容易读 | 只能表达点线等简单对象，无法表达块、圆弧、标注关系 | 单一算法输入，如点集、轴线、网格 | 可用于专项命令，不宜作为统一格式 |
| WKT / GeoJSON | 结构清晰，适合 2D 几何 | CAD 特有信息不足：图层、块、文字、标注、bulge、颜色、线型等需要扩展 | 道路、边界、多段线、GIS 类数据 | 可借鉴，但需要 HyCAD 扩展 |
| IFC / STEP | 工程语义强，适合 BIM/三维 | 对普通 AutoCAD 平面图过重，映射复杂 | BIM、构件级模型 | 当前 HyCAD 开发调试不优先 |
| AutoCAD 插件导出 JSON | 可控、精确、可裁剪、可版本化 | 需要实现导出命令和 DTO | 日常开发、命令调试、AI 分析 | 推荐作为主方案 |
| JSONL 分块快照 | 可处理大图，AI 可按块读取 | 需要建立索引、分块规则 | 大选择集、大图纸、批量分析 | 推荐作为大图增强方案 |
| SQLite / Parquet | 查询强、适合超大数据 | AI 直接阅读不方便，开发成本更高 | 批量工程数据仓库 | 后期可做，不作为第一阶段 |

## 3. 推荐方案：HyCAD 几何快照包

推荐建立一个轻量格式：**HyCAD Geometry Snapshot Package**，简称 `hygeom`。

它不是替代 DWG，而是为 AI、调试、回归测试服务的“选择集几何快照”。核心思想：

```text
AutoCAD 选择集
  -> HyCAD .NET 提取器
  -> 标准 DTO
  -> hygeom.json / hygeom.jsonl
  -> preview.png / preview.svg
  -> AI 读取并分析
```

### 3.1 文件组成

一个快照包建议包含：

| 文件 | 必需 | 作用 |
|---|---:|---|
| `snapshot.hygeom.json` | 是 | 主数据：元信息、坐标系、对象、关系、统计 |
| `preview.png` | 否 | 视觉参考，帮助 AI 对照结构化数据 |
| `preview.svg` | 否 | 轻量矢量预览，适合查看对象拓扑 |
| `raw.dxf` | 否 | 原始兜底，仅在结构化导出不够时使用 |
| `notes.md` | 否 | 用户补充：这次要分析什么、命令现象是什么 |

日常最小组合：

```text
snapshot.hygeom.json + preview.png
```

大图组合：

```text
manifest.hygeom.json
entities-0001.hygeom.jsonl
entities-0002.hygeom.jsonl
preview.png
```

## 4. 为什么 JSON 是第一阶段最优解

JSON 相比 DXF、SVG、CSV 的优势在于：

1. **AI 可直接阅读**：字段名就是语义，不需要解释 DXF group code。
2. **开发可控**：由 HyCAD 自己决定导出哪些对象、保留哪些属性。
3. **可以版本化**：后续新增字段不破坏旧数据。
4. **可裁剪**：只导出当前命令相关的选择集，而不是整张图。
5. **可分层表达**：既能保留原始 CAD 对象，也能额外输出算法需要的归一化几何。
6. **可接入测试**：同一份快照可以成为命令回归测试输入。

推荐原则：

```text
DWG 负责生产数据
JSON 负责 AI 分析与开发调试
PNG/SVG 负责视觉校验
DXF 负责兜底追溯
```

## 5. 数据结构设计

### 5.1 顶层结构

```json
{
  "schema": "hygeom.snapshot",
  "version": "1.0",
  "source": {
    "dwgPath": "D:/Project/example.dwg",
    "documentName": "example.dwg",
    "exportedAt": "2026-04-27T02:26:00+08:00",
    "exportedBy": "HyCADTool",
    "command": "HyExportGeometrySnapshot"
  },
  "coordinateSystem": {
    "space": "ModelSpace",
    "unit": "mm",
    "coordinate": "WCS",
    "tolerance": 0.001
  },
  "selection": {
    "mode": "PickSet",
    "count": 3,
    "bounds": {
      "min": [0.0, 0.0, 0.0],
      "max": [5000.0, 3000.0, 0.0]
    }
  },
  "layers": [],
  "entities": [],
  "relationships": [],
  "summary": {}
}
```

### 5.2 Entity 通用字段

每个 CAD 对象都应有统一外壳：

```json
{
  "id": "E001",
  "handle": "3F2A",
  "objectType": "Polyline",
  "layer": "HY-ROAD-CENTER",
  "color": "ByLayer",
  "linetype": "Continuous",
  "visible": true,
  "locked": false,
  "bounds": {
    "min": [0.0, 0.0, 0.0],
    "max": [5000.0, 1000.0, 0.0]
  },
  "geometry": {},
  "text": null,
  "cad": {
    "objectId": "optional-debug-only",
    "isFromBlock": false,
    "blockPath": []
  },
  "tags": {
    "role": "centerline",
    "confidence": "user-selected"
  }
}
```

关键点：

- `handle` 用于回到 AutoCAD 中定位对象。
- `objectType` 使用 HyCAD 统一枚举，不直接暴露所有 AutoCAD 类型细节。
- `bounds` 便于 AI 快速判断空间关系。
- `tags` 预留给后续命令识别出的工程语义，例如道路中心线、钢筋线、基础边界、标注文字。

### 5.3 几何类型建议

#### Line

```json
{
  "objectType": "Line",
  "geometry": {
    "start": [0.0, 0.0, 0.0],
    "end": [1000.0, 0.0, 0.0],
    "length": 1000.0
  }
}
```

#### Polyline

多段线要保留 `bulge`，否则圆弧段会丢失。

```json
{
  "objectType": "Polyline",
  "geometry": {
    "closed": false,
    "vertices": [
      { "point": [0.0, 0.0, 0.0], "bulge": 0.0 },
      { "point": [1000.0, 0.0, 0.0], "bulge": 0.4142135624 },
      { "point": [1000.0, 1000.0, 0.0], "bulge": 0.0 }
    ],
    "length": 2570.8,
    "area": null
  }
}
```

#### Arc / Circle

```json
{
  "objectType": "Arc",
  "geometry": {
    "center": [0.0, 0.0, 0.0],
    "radius": 500.0,
    "startAngleRad": 0.0,
    "endAngleRad": 1.5707963268,
    "normal": [0.0, 0.0, 1.0]
  }
}
```

#### Text / MText

```json
{
  "objectType": "MText",
  "geometry": {
    "position": [1000.0, 500.0, 0.0],
    "rotationRad": 0.0,
    "height": 250.0,
    "width": 1200.0
  },
  "text": {
    "plain": "B14@200",
    "raw": "{\\fSimSun|b0|i0|c134|p2;B14@200}",
    "style": "HY-TEXT"
  }
}
```

#### BlockReference

块建议同时保留“引用”和“可选展开结果”。

```json
{
  "objectType": "BlockReference",
  "geometry": {
    "name": "ANCHOR_BOLT",
    "position": [2000.0, 1000.0, 0.0],
    "rotationRad": 0.0,
    "scale": [1.0, 1.0, 1.0],
    "transform": [
      [1.0, 0.0, 0.0, 2000.0],
      [0.0, 1.0, 0.0, 1000.0],
      [0.0, 0.0, 1.0, 0.0],
      [0.0, 0.0, 0.0, 1.0]
    ],
    "attributes": {
      "BOLT_NO": "A1",
      "DIA": "M24"
    },
    "explodedEntityIds": ["E010", "E011"]
  }
}
```

### 5.4 关系结构

AI 分析图纸时，单个对象不够，还需要关系：

```json
{
  "relationships": [
    {
      "type": "intersects",
      "from": "E001",
      "to": "E002",
      "points": [[1000.0, 0.0, 0.0]]
    },
    {
      "type": "near",
      "from": "E003",
      "to": "E001",
      "distance": 120.0
    },
    {
      "type": "labels",
      "from": "E004",
      "to": "E001",
      "text": "B14@200"
    }
  ]
}
```

第一阶段不必计算所有关系。建议只输出：

1. 包围盒相交关系。
2. 文字到最近几何对象的关系。
3. 块属性与块几何关系。
4. 用户命令明确需要的工程关系。

## 6. 推荐工作流

### 6.1 日常开发调试

```text
1. 在 AutoCAD 中框选问题对象
2. 执行 HyCAD 导出命令
3. 生成 snapshot.hygeom.json + preview.png
4. 在 Cursor 中 @ 该 JSON 或把路径发给 AI
5. AI 根据结构化对象分析几何、命令逻辑、异常原因
```

适合问题：

- 某个命令选线后结果不对。
- 多段线方向、闭合、偏移、相交判断异常。
- 标注文字没有识别到对应对象。
- 块属性读取错误。
- 图层过滤、对象筛选、包络合并结果异常。

### 6.2 大图分析

大图不要一次导出完整 JSON，而是导出索引 + 分块：

```text
manifest.hygeom.json
  - 图纸摘要
  - 图层统计
  - 对象类型统计
  - 全局包围盒
  - chunk 文件清单

entities-0001.hygeom.jsonl
entities-0002.hygeom.jsonl
...
```

每行一个对象：

```jsonl
{"id":"E001","objectType":"Line","layer":"A-WALL","geometry":{"start":[0,0,0],"end":[1000,0,0]}}
{"id":"E002","objectType":"Circle","layer":"A-COL","geometry":{"center":[500,500,0],"radius":250}}
```

这样 AI 可以先读 `manifest`，再按需要读取某个 chunk。

### 6.3 命令回归测试

同一套格式还可以用于测试：

```text
真实 DWG 选择集
  -> 导出 hygeom 快照
  -> 作为测试输入保存
  -> 命令算法在无 AutoCAD 环境中读取快照
  -> 验证输出几何是否符合预期
```

这对 HyCAD 的 Domain 层尤其有价值：把 AutoCAD API 依赖隔离在 Infrastructure，算法只吃结构化 DTO。

## 7. AutoCAD 侧实现建议

### 7.1 分层

建议放在 HyCADTool 主工程的独立功能区，例如：

```text
Features/DataExchange/
  Commands/
    ExportGeometrySnapshotCommand.cs
  Domain/
    GeometrySnapshot.cs
    SnapshotEntity.cs
    SnapshotGeometry.cs
  Infrastructure/
    AutoCadGeometrySnapshotExtractor.cs
    GeometrySnapshotJsonWriter.cs
    PreviewRenderer.cs
  Options/
    GeometrySnapshotExportOptions.cs
```

职责划分：

| 层 | 职责 |
|---|---|
| Command | 交互：选择对象、选择输出目录、提示结果 |
| Infrastructure | 读取 AutoCAD `Database` / `Transaction` / `Entity` |
| Domain | 定义与 AutoCAD 无关的快照 DTO |
| Writer | JSON / JSONL / PNG / SVG 输出 |

### 7.2 导出选项

建议第一版提供这些选项：

| 选项 | 默认 | 说明 |
|---|---:|---|
| `SelectionMode` | `PickSet` | 当前选择集 / 框选 / 全图 / 当前图层 |
| `CoordinateSystem` | `WCS` | 坐标统一转世界坐标 |
| `IncludeInvisible` | `false` | 默认不导出不可见对象 |
| `IncludeLockedLayers` | `true` | 锁定层可读，不写入 |
| `ExplodeBlocks` | `false` | 默认保留块引用，必要时展开 |
| `IncludeXData` | `true` | 保留 HyCAD 自定义语义 |
| `IncludeExtensionDictionary` | `true` | 保留设备基础等扩展数据 |
| `MaxEntitiesPerChunk` | `1000` | 大图分块 |
| `CreatePreviewPng` | `true` | 输出视觉旁路 |

### 7.3 对象优先级

第一阶段优先支持：

1. `Line`
2. `Polyline` / `Polyline2d` / `Polyline3d`
3. `Arc`
4. `Circle`
5. `DBText`
6. `MText`
7. `MLeader`
8. `BlockReference`
9. `Dimension`
10. `Hatch` 边界

这些对象覆盖大多数 HyCAD 命令调试场景。

## 8. 与 AI 协作时的最佳提示格式

用户给 AI 的信息建议固定为：

```text
这是 AutoCAD 选择集导出的 HyCAD 几何快照：
@docs/DataExchange/samples/snapshot.hygeom.json

请重点分析：
1. 这些多段线是否形成闭合边界；
2. 文字 B14@200 应该标注到哪一组钢筋线；
3. 当前算法为什么可能选错最近对象。
```

如果有截图：

```text
结构化数据见 snapshot.hygeom.json，视觉参考见 preview.png。
以 JSON 坐标为准，截图只作为空间关系辅助。
```

## 9. 落地路线

### 阶段 1：最小可用

目标：让 AI 能分析选区基础几何。

实现内容：

1. 新增导出命令。
2. 支持选择集导出。
3. 支持 `Line`、`Polyline`、`Arc`、`Circle`、`DBText`、`MText`。
4. 输出单个 `snapshot.hygeom.json`。
5. 输出对象统计、图层统计、包围盒。

验收标准：

```text
在 AutoCAD 中选择 10 个以内对象
执行导出命令
Cursor 能读取 JSON
AI 能准确说出对象类型、坐标、长度、闭合状态、文字内容
```

### 阶段 2：工程语义增强

目标：让 AI 能分析 HyCAD 命令业务问题。

实现内容：

1. 支持块引用、属性、标注、引线。
2. 支持 XData / ExtensionDictionary 摘要。
3. 支持文字到最近对象关系。
4. 支持 preview.png 或 preview.svg。
5. 支持导出时自动生成 `notes.md` 模板。

验收标准：

```text
AI 能根据 JSON 判断文字、块、标注与几何对象的对应关系
能协助排查钢筋、道路、设备基础等命令的问题
```

### 阶段 3：大图与回归测试

目标：支持复杂图纸和自动化测试。

实现内容：

1. 支持 JSONL 分块。
2. 支持空间索引摘要。
3. 支持快照压缩包。
4. 支持从 hygeom 反向构造 Domain 测试输入。
5. 建立 `samples/` 目录保存典型问题快照。

验收标准：

```text
几千个对象可以分块导出
AI 先读 manifest，再按问题读取相关 chunk
Domain 算法可以用 hygeom 样本做回归测试
```

## 10. 关键设计原则

1. **以选择集为中心**：默认不导出全图，只导出本次问题相关对象。
2. **以 WCS 和 mm 为标准**：避免 UCS、视口比例、图纸空间导致坐标误解。
3. **原始几何与归一化几何并存**：例如多段线保留 bulge，同时可额外输出离散采样点。
4. **Handle 必须保留**：AI 发现问题后，开发者能回到 AutoCAD 定位对象。
5. **不要让 AI 反推 CAD 语义**：图层、块名、文字内容、属性、XData 应直接字段化。
6. **截图只作辅助**：最终判断以 JSON 坐标和对象属性为准。
7. **格式版本化**：所有快照必须带 `schema` 和 `version`。

## 11. 最终建议

HyCAD 最合适的数据传递方式不是“把 DWG 直接交给 AI”，而是：

```text
AutoCAD 内部读取 DWG
只导出当前选择集
转成 HyCAD 自定义 JSON 几何快照
附带 PNG/SVG 视觉预览
必要时保留 DXF 兜底
```

第一阶段建议立即做 `snapshot.hygeom.json`。它成本低、收益高，既能解决 AI 分析几何的问题，也能为后续命令回归测试、样本库、自动排障打基础。

