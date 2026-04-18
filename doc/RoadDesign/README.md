# HyCADTool.Refactored 市政道路设计 对标调研文档集

本目录存放"市政道路设计"新功能的**对标调研与架构设计文档集**，写作范式借鉴 `HyTool/Doc` 的"借鉴/超越/映射"三段式。

本阶段**只产出 Markdown 文档，不动任何代码**。最终工具链路线已确定为：

> **AutoCAD（平面 + 施工图出图） → Blender（3D 模型） → Lumion（渲染）**，v∞ 准备抛弃 AutoCAD 迁移到 Blender-first。

v1 主攻 AutoCAD 出图功能落地（[01MASTER § 十一 P0-P5](./01MASTER.md#十一产出路线图-p0---p6路线-c-下的七阶段)），同步预埋 Blender/Lumion 联调所需的**中性化接口**（[04Pipeline § 五预留点清单](./04Pipeline_CAD_Blender_Lumion.md#五v1-预留点清单不实现但接口字段留好)）。实施路线最终采用**路线 C（混合路线）**，依据见 [01MASTER § 八](./01MASTER.md#八实施路线-a--b--c三选一由工程师定) 路线决策分析。

---

## 功能范围（v1）

覆盖：
- 道路几何要素（中心线 / 路缘石 / 车道 / 人行道 / 绿化带）
- 平交交叉口（转角圆弧 / 导流岛 / 渠化 / 人行横道 / 停止线 / 视距三角形）
- 交通标线标志（车道分界 / 导向箭头 / 禁停网格 / 标志牌图块）
- 纵断面设计（地面线 / 设计线 / 竖曲线 / 标高标注）
- 横断面设计（标准模板 / 路拱 / 结构层 / 边坡）
- 说明与表单（设计说明 / 工程量表 / 结构层表 / 坐标表）
- 规范校核（CJJ 37 / CJJ 152 / GB 50647 / CJJ/T 266 / GB 5768）

不含：管线综合协调、排水附属（雨水口 / 检查井）。

---

## 文档清单（18 篇）

### 整合类（5 篇）

| 文档 | 定位 |
|------|------|
| [01MASTER.md](./01MASTER.md) | 总设计纲要（第一性原理、架构分层、实施路线 A/B/C、P0-P7 路线图、规范清单） |
| [02Software_Overview_INDEX.md](./02Software_Overview_INDEX.md) | 软件总览索引 + 总对比表 + 收敛图 |
| [03RoadSelect.md](./03RoadSelect.md) | 道路核心系统专项：Alignment/Profile/Assembly/Corridor 四级体系 + 五维选择 + 联动 |
| [04Pipeline_CAD_Blender_Lumion.md](./04Pipeline_CAD_Blender_Lumion.md) | **AutoCAD → Blender → Lumion 工具链专项**：v1 预留点清单、三维格式选型、v2/v3/v∞ 演进 |
| [05计划书.md](./05计划书.md) | **总体作战地图**：现状坐标、5 条主线、不可动原则、任务看板、节奏节点、风险清单 |

### 国外标杆（5 篇）

| 文档 | 核心看点 |
|------|----------|
| [Civil3D.md](./Civil3D.md) | Alignment/Profile/Assembly/Subassembly/Corridor 五件套、Target Mapping、Subassembly Composer |
| [OpenRoads.md](./OpenRoads.md) | Template Library (.itl)、Corridor + Superelevation、Point Controls、Civil Cells |
| [Novapoint.md](./Novapoint.md) | Trimble Quadri 云端多专业协同、LandXML 交换 |
| [InfraWorks.md](./InfraWorks.md) | 概念设计、大场景城市模型、AI 规划提示 |
| [12dModel.md](./12dModel.md) | 澳洲派一体化（地形 + 道路 + 土方 + 排水）、.12d 格式、Visual Programming |

### 国内实用派（5 篇）

| 文档 | 核心看点 |
|------|----------|
| [HongYeRoad.md](./HongYeRoad.md) | 鸿业市政道路 9.0：4 种平面法、自动交叉口、超高加宽、CJJ 37/152 内置 |
| [HintCAD.md](./HintCAD.md) | 纬地道路 BIM 2.0：平纵横一体化、智能布线、平交口 BIM 一键、自动审核 |
| [EICAD.md](./EICAD.md) | 同济 EICAD：交叉口与立交专项、左转待行区、交织区 |
| [TangentRoad.md](./TangentRoad.md) | 天正道路：国标制图习惯、图块库、动态块、标注样式 |
| [FastTFT_LiZheng.md](./FastTFT_LiZheng.md) | 飞时达 / 理正市政：快速出图派、横断面图框、土方调配、工程量统计 |

### 横向借鉴（3 篇）

| 文档 | 核心看点 |
|------|----------|
| [AutoTurn.md](./AutoTurn.md) | Transoft AutoTurn：车辆转弯轨迹、视距三角形、行人导流 |
| [RhinoGH_Parametric.md](./RhinoGH_Parametric.md) | Rhino + Grasshopper：DAG 参数化、夹点预览、批量断面生成 |
| [QGIS_GIS.md](./QGIS_GIS.md) | QGIS / ArcGIS：CRS / EPSG、矢量分层、空间索引、LandXML/GeoJSON/Shapefile |

---

## 每篇对标文档的统一模板

1. 软件简介与市场定位（1 段）
2. 值得借鉴的核心设计（3-7 小节，每节配 mermaid / 伪代码 / 示意）
3. 必须超越的缺点
4. **映射到 HyCADTool.Refactored 的设计**——落到具体命令名、ViewModel 属性、Service 类、图层、面板 Tab 挂点
5. 总结：借鉴 vs 超越对比表

---

## 阅读顺序建议

- **想看 HyCAD 怎么做**：[01MASTER.md](./01MASTER.md) → [03RoadSelect.md](./03RoadSelect.md) → [04Pipeline_CAD_Blender_Lumion.md](./04Pipeline_CAD_Blender_Lumion.md)
- **想看对比与选型**：[02Software_Overview_INDEX.md](./02Software_Overview_INDEX.md) → 逐个对标软件篇
- **想直接进 v1 实施**：[01MASTER § 十一 P0-P5](./01MASTER.md#十一产出路线图-p0---p6路线-c-下的七阶段) + [04Pipeline § 四 五](./04Pipeline_CAD_Blender_Lumion.md#四v1-必做autocad-平面出图主线)（必做与预留清单）
- **关心 Blender/Lumion 工具链**：直接读 [04Pipeline_CAD_Blender_Lumion.md](./04Pipeline_CAD_Blender_Lumion.md)
- **按国家 / 学派浏览**：先 `Civil3D.md` / `OpenRoads.md` 看国外范式，再 `HongYeRoad.md` / `HintCAD.md` 看国内范式

---

## 产出批次（全部已交付）

- 批 1 ✔：`01MASTER.md` 骨架 + `Civil3D.md` + `OpenRoads.md` + `Novapoint.md` + `InfraWorks.md` + `12dModel.md`
- 批 2 ✔：`HongYeRoad.md` + `HintCAD.md` + `EICAD.md` + `TangentRoad.md` + `FastTFT_LiZheng.md` + `AutoTurn.md`
- 批 3 ✔：`RhinoGH_Parametric.md` + `QGIS_GIS.md` + `02Software_Overview_INDEX.md` + `03RoadSelect.md` + 回填 `01MASTER.md` 实施路线 P0-P6 + 路线 C 决策
- 批 4 ✔：`04Pipeline_CAD_Blender_Lumion.md` + 01MASTER 集成工具链路线（§ 零工具链、§ 5.5 三维交换、P0/P2/P5 必做/预留细分、新增 P7 Blender 同步阶段、风险扩充）

## 关键决策（2026-04-17 已确认）

| # | 决策项 | 结果 | 工期影响 |
|---|--------|------|:---:|
| 1 | `.roaddesign.json` 粒度 | **单文件**（一个项目一个 JSON） | 0 |
| 2 | v1 是否做 glTF 导出 | **不做**（纯 JSON） | 0 |
| 3 | 事件总线 | **接口 + 真实发布订阅** | **P0 +2d** |
| 4 | GUID 存储位置 | **DWG Xdata**（`HY_ROAD` 应用名） | 0 |
| 5 | Blender 插件启动 | **v2（P7）** | 0 |

**v1 总工期 69d → 71d（约 14.2 周）**。决策详情见 [04Pipeline § 十一](./04Pipeline_CAD_Blender_Lumion.md#十一v1-启动前的-5-个关键决策已确认2026-04-17)。

**下一步**：进入 [01MASTER § 十一 P0](./01MASTER.md#p0--架构预埋--事件总线--命令骨架12-工作日) 代码落地（12 个工作日）。
