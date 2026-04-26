# 10 Corridor（走廊）— Civil 3D 功能与用户操作流程（网络资料整理）

> 性质：**功能与用户操作流程说明** —— 归纳 Autodesk Civil 3D 中 **Corridor（道路走廊）** 在软件里“做什么、先做什么、对话框里点哪里、常用选项含义是什么”。
>
> **非重点**：Domain/接口分层、类图、HyCADTool 实现策略（这些见 `01MASTER.md`、`03RoadSelect.md`、`Civil3D.md` §1.4）。
>
> **交叉阅读**：平面线位流程见 [`07Alignment.md`](./07Alignment.md)；横断面字段见 [`06.md`](./06.md)；Civil 3D 五件套总论见 [`Civil3D.md`](./Civil3D.md)。

---

## 0. 文档定位

| 维度 | 取值 |
|------|------|
| 主题 | Civil 3D **Corridor** 的功能边界与典型操作路径 |
| 资料性质 | 公开帮助、Autodesk 社区与培训站点要点的**二次整理**（非官方译文） |
| 版本语境 | 以 **Civil 3D 2024–2026** 英文帮助中的术语为准；不同版本 Ribbon 归类可能微调 |

---

## 1. Corridor 是什么（用户视角）

**一句话**：Corridor 是沿 **Baseline（基准线）** 按桩号放置 **Assembly（装配）**，把二维横断面“扫”成连续三维道路模型（含路面、边坡、结构层等）的**参数化对象**。

**它依赖的上游对象（缺一不可的常见组合）**：

| 对象 | 用户可理解的职责 |
|------|------------------|
| **Alignment** | 平面中心线 + 桩号体系 |
| **Profile（通常为 Finished Ground / 设计线）** | 沿 Alignment 的设计高程 |
| **Assembly** | 由多个 **Subassembly（子部件）** 拼成的标准横断面模板 |
| **（可选）Surface** | 地形曲面，用于边坡“贴地”、Daylight 等子部件 |
| **（可选）Target** | 宽度/高程/偏移等绑定到 Feature Line、Alignment、Profile 等，使走廊随外部几何变化 |

改 Alignment、Profile、Assembly 或 Target 后，需要 **Rebuild（重建）** 走廊以更新三维结果。

---

## 2. Ribbon 入口与创建走廊的主流程

### 2.1 典型入口

- **Home** 选项卡 → **Create Design** 面板 → **Corridor** 下拉 → **Create Corridor**（或等价命令）。

（不同版本可能将 Corridor 相关命令归在 **Corridor** 选项卡；若找不到，可用命令行输入 `CreateCorridor` 辅助定位。）

### 2.2 创建向导中的核心填写项

用户在第一次创建时，一般需要依次明确：

| 步骤 | 用户操作要点 |
|------|----------------|
| 1. 命名与样式 | 给 Corridor **名称**；指定 **样式（Style）**、**图层**（影响显示与出图） |
| 2. 指定 Baseline | 选择 **Alignment**；选择与之关联的 **Profile**（常见为设计线 FG） |
| 3. 指定 Assembly | 选一个已建好的 **Assembly**（或从工具库拖入后再选） |
| 4. 桩号范围 | 设定该 Region 的 **起始桩号 / 结束桩号**（可小于整条 Alignment 全长，用于分段） |
| 5. 目标与频率 | 打开 **Set Baseline and Region Parameters**（或向导等价页），设置 **Frequency（装配频率）** 与 **Target Mapping（目标映射）** |
| 6. 地形（可选） | 若子部件需要贴地，指定 **Surface Target** |

完成后生成 Corridor 对象；图形中出现走廊实体、特征线或曲面（取决于样式与后续提取操作）。

---

## 3. Baseline（基准线）与 Region（区间）

### 3.1 术语

| 术语 | 含义（操作层面） |
|------|------------------|
| **Baseline** | 一条 Corridor 内，驱动装配的“线位 + 纵断面”组合；一条路可有 **多条 Baseline**（主路、辅路、匝道等） |
| **Region** | 同一条 Baseline 上，**连续桩号段**；每一段可绑定 **不同的 Assembly**、**不同的频率**、**不同的 Target** |

### 3.2 常见用户操作

- **加宽 / 断面变化**：在变化桩号处 **拆分 Region**，后一段换用另一 Assembly（例如标准段 → 加宽段）。
- **匝道接入**：新增一条 Baseline，单独指定 Alignment + Profile + Assembly。

---

## 4. Assembly Frequency（装配频率）

**含义**：沿 Baseline 每隔多远（或哪些几何特征点）放置一次 Assembly 实例，用于生成 **Corridor Station（走廊站位）** 序列。

### 4.1 设置层级（由粗到细）

公开资料与社区文章普遍归纳为多层覆盖关系（新走廊默认值来自 **Command Settings**，可被更细层级覆盖）：

1. **Command Settings**（命令默认，影响“以后新建”的默认值）  
2. **Entire Corridor**（整条走廊）  
3. **Baseline**（单条基准线）  
4. **Region**（单个桩号区间）——**最常用、最细**

### 4.2 频率类型（典型选项）

不同线段类型可分别设定间隔，常见包括：

- **Along Tangents**：直线段上沿弧长等距  
- **Along Curves / Spirals**：圆曲线、缓和曲线段（可加密）  
- **Along Profile Curves / Tangents**：与纵断面几何相关的采样  
- **At Geometry Points**：水平/竖向几何变化点（例如曲线起终点、竖曲线点）  
- **At Superelevation Critical Points**：超高控制点（若启用超高）  
- **Adjacent to Offset Target**：与偏距目标相邻处加密（提高与目标线贴合度）

### 4.3 实务建议（来自网络共识）

- 直线段可用较大间距（如 **25 ft / 7.5 m** 量级，按项目单位调整）。  
- **平曲线、缓和曲线** 宜 **加密**（例如 **2.5–5 ft** 或更密），否则路面模型在弯道处出现折皱或偏差不收敛。  
- 频率过密（全程 0.3 m 级）会显著拖慢 **Rebuild** 与文件体积，需权衡。

**帮助主题（英文）**：*About Changing the Frequency of Stations in a Corridor*（Autodesk Civil 3D User’s Guide）。

---

## 5. Target Mapping（目标映射）

**含义**：把 Subassembly 里声明的 **逻辑目标**（宽度、偏距、高程、坡度、曲面等）绑定到 **图形中的真实对象**。

### 5.1 打开方式

- 选中 Corridor → 右键 **Corridor Properties** → **Parameters** 选项卡中进入 **Target Mapping**；或在创建/编辑 Region 的流程中进入同名对话框。

### 5.2 三大类目标（用户最常碰到的）

| 类型 | 常绑定对象 | 典型用途 |
|------|------------|----------|
| **Surface** | TIN Surface | 边坡与地形相交（Daylight）、填挖参考 |
| **Width / Offset** | Alignment、Feature Line、Polyline、Survey Figure | 车道宽度变化、机动车道边线控制 |
| **Elevation** | Profile、Feature Line、3D Polyline | 路缘/中央分隔带顶面随纵断面或控制线起伏 |

### 5.3 操作要点

- **Surfaces 页**：可为“全部子部件”指定同一 Surface，也可在表格里对单个子部件指定不同 Surface。  
- **Offset / Elevation 列**：点单元格弹出 **Set Offset Targets** / **Set Elevation Targets**，可从图形拾取或按 **图层批量** 过滤候选对象。  
- 修改 Target 后需 **Rebuild** 走廊。

**帮助主题（英文）**：*Target Mapping Dialog Box*；*To Specify Corridor Targets*。

---

## 6. 超高（Superelevation）与走廊

**用户侧关联**：若在 Alignment 或 Corridor 上配置了 **Superelevation（超高）**，部分车道子部件会按超高表在弯道段改变横坡；走廊重建后，横坡变化会体现在 **Corridor Section** 与后续曲面中。

**操作提示**：超高突变点常与 **Frequency** 中的 *Superelevation Critical Points* 联动；弯道建模应同时检查 **频率加密** 与 **超高数据** 是否一致。

---

## 7. 走廊输出物：曲面、实体与工程量

### 7.1 Corridor Surface

用户可从 Corridor 提取 **Corridor Surface**（常见子类型名如 **Top / Datum** 等，随模板与版本略有差异），用于：

- 与地形 Surface 做 **体积表（Volume）**  
- 可视化、渲染、导出

### 7.2 Corridor Solids / 结构层

新版本帮助与发行说明中强调 **Corridor Solids** 能力增强，用于更直观地获得路体实体，便于可视化与部分下游用途（具体命令以安装版本 Ribbon 为准）。

### 7.3 QTO（工程量）

子部件 **Shape** 带 **代码（Code）** 时，可在 QTO 体系中按材质/区域统计；用户需保证 **Assembly 子部件编码策略** 与出表模板一致。

---

## 8. 重建、编辑与性能

| 操作 | 说明 |
|------|------|
| **Rebuild Corridor** | Alignment/Profile/Target 变更后执行；大范围修改可能耗时 |
| **Split Region / 调整起终桩号** | 用于分段换 Assembly 或改频率 |
| **性能** | 走廊过长、频率过密、Surface 点数过大时，Rebuild 可能达到分钟级；宜分段工程或优化频率策略 |

---

## 9. 常见问题（排错向的操作清单）

| 现象 | 可检查项 |
|------|----------|
| 边坡未贴地 | Surface Target 是否指定；子部件是否支持 Daylight；Surface 范围是否覆盖该桩号 |
| 宽度未随控制线变化 | Target Mapping 是否绑定到正确 **Width/Offset**；子部件参数是否为“由目标驱动”的模式 |
| 弯道模型有折线感 | **Frequency** 是否在曲线段过稀；是否启用几何点/目标邻接加密 |
| 修改目标后无变化 | 是否执行 **Rebuild** |

---

## 10. 资料来源与延伸阅读（链接）

以下为整理本文时参考的 **公开网页类型**（Autodesk 帮助、社区与培训文章）；具体页面可能随版本重定向。

**Autodesk 帮助（英文）**

- [To Create a Corridor](https://help.autodesk.com/cloudhelp/2026/ENG/Civil3D-UserGuide/files/GUID-5817971D-0F32-4872-A88B-2379FF34DB32.htm)（创建走廊）  
- [About Changing the Frequency of Stations in a Corridor](https://help.autodesk.com/cloudhelp/2026/ENG/Civil3D-UserGuide/files/GUID-95498140-3AE4-44DF-8FC0-5CE2ADB3AD54.htm)（走廊站位频率）  
- [Frequency to Apply Assemblies Dialog Box](https://help.autodesk.com/cloudhelp/2026/ENG/Civil3D-UserGuide/files/GUID-4D57F0BE-4EAF-4CBB-AE80-F9C6F45A7AE1.htm)（应用装配的频率对话框）  
- [Target Mapping Dialog Box](https://help.autodesk.com/cloudhelp/2026/ENG/Civil3D-UserGuide/files/GUID-62445E31-49A2-4C29-9F79-5A0E11410273.htm)（目标映射对话框）  
- [Parameters Tab (Corridor Properties)](https://help.autodesk.com/cloudhelp/2026/ENG/Civil3D-UserGuide/files/GUID-2FE5FB9F-AF80-4E10-89F4-A809C1D873E8.htm)（走廊特性 — Parameters）

**第三方教程与博客（步骤说明类）**

- [Creating Corridors with Assemblies — Noble Desktop](https://www.nobledesktop.com/learn/civil-3d/creating-corridors-with-assemblies-in-civil-3d-step-by-step-guide)  
- [Civil 3D Corridor Frequency Settings — Design & Motion](https://designandmotion.net/autodesk/autocad-civil-3d/civil-3d-corridor-frequency-settings/)  
- [Creating Corridors for an Existing Highway — VDCI](https://vdci.edu/learn/civil-3d/creating-a-corridor-for-an-existing-highway-setting-up-parameters-and-targets)（参数与目标设置案例向）

**项目内关联**

- 五件套概念与 Target 思想：[`Civil3D.md`](./Civil3D.md) §1.3–1.5  
- HyCADTool 横断面模板字段：[`06.md`](./06.md)  
- 平面线位操作对标：[`07Alignment.md`](./07Alignment.md)

---

*文档版本：2026-04-19 · 主题：Civil 3D Corridor 功能与操作流程（网络整理）*
