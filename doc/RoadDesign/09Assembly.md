# 09 横断面装配（Assembly）— Civil 3D / 鸿业 / HyCADTool 用户操作流程对标

> 性质：**功能与流程对标文档** —— 把 Civil 3D（Assembly + Subassembly 范式的定义者）、鸿业市政道路（国内"标准横断面编辑器"最成熟的实现）、HyCADTool（v2 `hyRoadCs`/`hyRoadT` 现状）三方放在同一坐标系下，逐条比对"用户怎么把一个横断面拼出来"。
>
> 重点：**用户视角**——从哪个菜单进、在哪块面板填参、拖哪个部件、镜像怎么做、参数怎么生效、缺什么功能。
>
> 非重点：架构、Domain/Service 分层、接口命名。这些请回查 `01MASTER.md` §2 / `Civil3D.md` §1.3 与源码 XML Doc。
>
> 上游姊妹篇：
>
> - `06.md` 横断面**单张字段映射**（鸿业字段 ↔ HyCAD ViewModel 字段）——细到每个单元格；
> - `07Alignment.md` 平面线位**用户操作流程**——同一写法，本篇延用。
>
> 资料来源：Autodesk Civil 3D 2024–2026 在线帮助、Civil3D.tv 教学站、NobleDesktop Assembly 教程、Eagle Point Subassembly Composer 课程、Autodesk 官方 Subassembly Reference、鸿业市政道路 8.0/9.0 用户手册、中海达 HBC 横断面教程、HyCADTool 当前源码与 06 对标文档。

---

## 0. 文档定位

| 维度      | 取值                                                                    |
| --------- | ----------------------------------------------------------------------- |
| 性质      | 三方功能对标 + 用户操作流程梳理                                         |
| 目标读者  | 道路设计工程师 / HyCADTool 开发者                                       |
| 上游依赖  | `Civil3D.md` §1.3（Assembly/Subassembly 总论）、`06.md`（鸿业字段映射） |
| 下游依赖  | `05计划书.md` P2 看板、`01MASTER.md` P2/P3 阶段                         |
| 不重叠    | `06.md` 单字段映射、`07Alignment.md` 平面线位、`03RoadSelect.md` 选择系统 |
| 维护节奏  | 每完成 v1.x 的一个 Assembly/Template 命令 → 在 §9 勾选并归档             |

---

## 1. Assembly 是什么 —— 三方共识与术语统一

> 三家对"横断面装配"的语义高度一致，只是抽象粒度不同。Civil 3D 拆两层（Assembly 容器 + Subassembly 部件）；鸿业和 HyCAD 都拍扁成一层（标准横断面 = 板块列表）。本文统一用"Assembly / 部件 / 板块"指代同一语义。

### 1.1 核心概念三方对照

| 概念             | Civil 3D                  | 鸿业                          | HyCADTool v2                              | 说明                                      |
| ---------------- | ------------------------- | ----------------------------- | ----------------------------------------- | ----------------------------------------- |
| 容器             | **Assembly**              | **标准横断面**                | **Template** / **CrossSectionLayout**     | 一整条横断面的"装订本"                    |
| 装订本的脊       | Assembly Baseline（竖线） | 道路中心线（虚线）            | `CenterlinePosition`（默认居中的虚拟轴）  | 参考轴，定左右                            |
| 一个零件         | **Subassembly**           | **板块**                      | **Band** / **CrossSectionBand**           | 车道 / 路缘石 / 人行道等单元              |
| 零件分类         | Tool Palette（9 大类）    | 板块类型枚举（约 7 种）       | `TemplateComponentKind`（8 种）           | 见 §6                                     |
| 零件的可调参数   | Properties → Advanced Parameters（侧 / 宽 / 坡 / 深 / ...） | 表格单元格（宽度 / 坡度 / 路牙 / ...） | `BandRowViewModel` 属性（同鸿业） | 见 §1.3                                   |
| 零件之间的接口   | 带箭头的 Marker Point（插接点） | 列表顺序（从中心向外）        | 列表顺序（`LeftBands` / `RightBands`）    | Civil 3D 显式绑、鸿业 / HyCAD 按顺序自动绑 |
| 零件的编码系统   | Point / Link / Shape Code | 隐式（按类型反查）            | `Kind` + `Name`                           | 用于出图样式与工程量                      |
| 横断面规格等级   | Assembly Type（5 种）     | 道路等级（主 / 次 / 支）      | 由 `DesignSpeed` 自动映射规范              | 见 §2.1                                   |

### 1.2 Civil 3D 的 Assembly Type（创建时必选）

| Assembly Type                 | 含义                            | 典型场景         |
| ----------------------------- | ------------------------------- | ---------------- |
| Undivided Crowned Road        | 无中央分隔带 + 拱形路面         | 次干路 / 支路    |
| Undivided Planar Road         | 无中央分隔带 + 单一坡面（平面） | 坡地 / 桥面      |
| Divided Crowned Road          | 有中央分隔带 + 拱形路面         | 主干路 / 城市道路 |
| Divided Planar Road           | 有中央分隔带 + 单一坡面         | 高速分幅超高段   |
| Railway                       | 铁路断面                        | 轨道交通         |
| Other                         | 自定义                          | 特殊桥 / 隧道    |

> Assembly Type 并不改变你能放什么部件，只决定：(1) 超高（Superelevation）轴心位置、(2) 支持的 Target 数量、(3) 自动镜像时是否以"路拱中线"为对称轴。

### 1.3 Subassembly / 板块的通用参数（跨三方的最小公倍数）

| 参数       | Civil 3D 叫法              | 鸿业叫法     | HyCAD 字段（`BandRowViewModel`） |
| ---------- | -------------------------- | ------------ | -------------------------------- |
| 侧别       | Side (Left / Right)        | 左 / 右      | `Side: BandSide`                 |
| 宽度       | Width                      | 宽度         | `Width: double`                  |
| 横坡       | Slope / Cross Slope        | 坡度         | `CrossSlopePct: double`          |
| 厚度 / 深度 | Depth                      | 结构厚度     | 通过 `SurfaceLayer` 表示         |
| 路牙       | Curb Subassembly（独立部件） | 路牙类型下拉 | `KerbSpec` VO                    |
| 名称       | —                          | 板块名       | `Name: string`                   |

### 1.4 Assembly 中的三种"点"（Civil 3D 独有）

> 这是 Civil 3D 区别于鸿业 / HyCAD 的最大设计点。鸿业 / HyCAD 只有"中心线"，没有后两类。

| 点                   | 含义                                                         | 对标场景                               |
| -------------------- | ------------------------------------------------------------ | -------------------------------------- |
| **Insertion Point**  | 用户第一次在 DWG 上点下去的地方（Assembly 对象的原点）       | HyCAD 的 `PromptInsertionPoint`（出图位）|
| **Baseline Point**   | Assembly "装订脊" 上第一个 Subassembly 的挂点（可偏离 Insertion）| HyCAD 默认等于中心线（不可偏移）        |
| **Offset Point**     | 服务于 Offset Alignment（主辅路分幅）的副挂点，0 ~ N 个     | HyCAD / 鸿业均 ❌                      |

**作用差异**：Offset Point 让 Civil 3D 能在一个 Assembly 内同时装配"主路 + 辅路（有独立纵断面）"；鸿业 / HyCAD 走不了这条，必须拆成两个横断面。

---

## 2. 创建 Assembly 的工作流

### 2.1 Civil 3D：Create Assembly 对话框 + 选 Type + 点插入点

入口：**Home tab → Create Design panel → Assembly dropdown → Create Assembly**（或菜单 `Assembly` 命令）。

| 步骤 | 操作                                                                                                    | UI 反馈                                                                  |
| ---- | ------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------ |
| 1    | Ribbon → Assembly → **Create Assembly**                                                                  | 弹 **Create Assembly** 对话框                                            |
| 2    | 填 **Name**（如"Dev Main"）                                                                              | 名字后续出现在 Prospector → Assemblies 列表                              |
| 3    | 选 **Assembly Type**（见 §1.2）                                                                          | 决定超高轴心 / 对称行为                                                   |
| 4    | 选 **Assembly Style**（默认 Basic）                                                                      | 控制 DWG 中 Assembly 符号的显示                                           |
| 5    | 选 **Code Set Style**（默认 All Codes / All Codes with Hatching）                                        | 决定 Point/Link/Shape Code 的颜色 / 图层 / 样式                           |
| 6    | 选 **Assembly Layer**（默认 `C-ROAD-ASSM`）                                                              | —                                                                        |
| 7    | OK                                                                                                       | 对话框关，命令行 "Specify assembly baseline location:"                   |
| 8    | 在 DWG 上点一下（建议点在"空白处，不压覆"）                                                              | DWG 出一根竖的**红/黄虚线 Baseline**，中间有一个方形 Marker Point        |
| 9    | Civil 3D 自动缩放到 Baseline，便于拖部件                                                                 | 此时 Assembly 是"空壳"，还没有任何 Subassembly                           |

> 关键：Create Assembly **只建容器**，部件一个都没有，必须接 §3.1 的"从 Tool Palette 拖部件"才完整。

### 2.2 鸿业：标准横断面设计界面 + 从预设加载

入口：菜单 **道路 → 标准横断面设计**（或工具栏"标准横断面"图标）。

| 步骤 | 操作                                                 | UI 反馈                                                                 |
| ---- | ---------------------------------------------------- | ----------------------------------------------------------------------- |
| 1    | 菜单 → 标准横断面设计                                | 弹**标准横断面设计窗口**（左：表格，右：实时预览）                      |
| 2    | 右下角"文件夹"图标 → 选 `.hdy` 横断面文件（可选）    | 加载预设；或留空走 "空横断面"                                           |
| 3    | 左上角选等级："主干路 / 次干路 / 支路"（可选）       | 按等级自动加载一套板块                                                   |
| 4    | 左右表格里直接看到各板块行                            | 右侧预览即时刷新                                                        |

> 鸿业没有"建空壳再拖"的步骤，一进来就带一套默认板块，可直接改 / 删 / 添。

### 2.3 HyCADTool v2：`hyRoadCs` 三入口（新建 / 加载 / 命令行）

入口：命令行 `hyRoadCs`（或历史命令 `hyRoadT`，已标 Obsolete 转发）。

| 步骤 | 操作                                                              | 命令行反馈                                                                                                         |
| ---- | ----------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------ |
| 1    | 命令行 `hyRoadCs`                                                  | "[道路] 选择横断面绘制模式 [新建(N)/加载(L)/命令行(C)] <N>："                                                      |
| 2a   | **新建 (N)**：回车                                                 | 启动 `CrossSectionDrawWindow`，默认加载 `CreateCjj37UrbanArterial()` 预设（主干路双向 6 车道）                     |
| 2b   | **加载 (L)**：从当前 DWG `Design.Templates` 列表选一条             | 反序列化 → 同一窗口编辑 → 确定后"覆盖保存 + 重绘"                                                                  |
| 2c   | **命令行 (C)**：纯命令行快速分支                                   | 追问"选择预设 [主干(A)/次干(S)/支路(L)] <A>" → 直出，不开窗口；常用于批量脚本                                       |
| 3    | 窗口里编辑（见 §3.3 / §4.3）                                       | BlenderUI workbench 风格：**Outliner + Toolbar + Canvas + PropertyEditor + StatusBar**                             |
| 4    | 点"确定"                                                           | 命令行 "指定横断面插入点："                                                                                         |
| 5    | 在 DWG 上点一下                                                    | 画出整张断面图到 ModelSpace；JSON 写回 `Design.Templates`；命令行打印 "[道路] 已绘制/保存 Template-1，总宽 29.0 m" |

**三入口对应关系**：

| Branch       | 对标 Civil 3D                  | 对标鸿业                       |
| ------------ | ------------------------------ | ------------------------------ |
| 新建 (N)     | Create Assembly + Basic 预设   | 标准横断面设计 + 主干路等级   |
| 加载 (L)     | 从 Content Browser 拖已保存 Assembly | "文件夹图标"选 `.hdy` 横断面文件 |
| 命令行 (C)   | ❌（Civil 3D 无纯命令行分支）   | ❌（鸿业无纯命令行分支）        |

### 2.4 三方创建工作流对照表

| 维度                     | Civil 3D                           | 鸿业                     | HyCADTool v2            |
| ------------------------ | ---------------------------------- | ------------------------ | ----------------------- |
| 入口                     | Ribbon Assembly → Create Assembly  | 菜单 道路 → 标准横断面设计 | 命令行 `hyRoadCs`       |
| 初始是否带部件            | ❌（空壳，必须拖）                  | ✅（按等级带）            | ✅（3 个预设可切换）     |
| 需要选 Assembly Type     | ✅（5 种）                          | 等级（3 种，隐含）       | 通过 `DesignSpeed` 推  |
| 需要选 Style / Code Set  | ✅                                  | ❌                       | ❌（硬编码图层与 Style）|
| 需要先在 DWG 点插入点     | ✅（创建对话框后）                   | ❌（在右侧预览区显示）    | ✅（确定窗口后）         |
| 在画布中直接编辑         | ✅                                  | ❌（独立窗口）            | ❌（独立窗口）           |
| 批量 / 脚本友好           | ❌                                  | ❌                       | ✅（命令行分支 C）       |

---

## 3. 添加 / 插入 Subassembly 的工作流

### 3.1 Civil 3D：Tool Palette 拖 + 在 Baseline 上点 Marker

入口：**Home tab → Palettes panel → Tool Palettes**（快捷键 Ctrl+3）→ 选"Civil Metric Subassemblies"或"Basics"选项卡。

| 步骤 | 操作                                                                                                 | UI 反馈                                                                                                             |
| ---- | ---------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------- |
| 1    | Tool Palettes → Basics tab                                                                           | 看到一排图标：Basic Lane / Basic Curb and Gutter / Basic Sidewalk / Basic Shoulder / Basic Side Slope 等             |
| 2    | 点一个图标（如 **Basic Lane**）                                                                      | 右侧 **Properties** 弹出，展开 **Advanced Parameters**：Side=Right / Width=12ft / Depth=0.67 / Slope=-2%            |
| 3    | 在 Properties 里改参数（如 Width=17, Slope=-2）                                                       | 参数实时生效                                                                                                        |
| 4    | 命令行 "Select marker point within assembly:"                                                         | 鼠标变十字 + 挂着 Subassembly 预览                                                                                  |
| 5    | 在 Baseline 上点一个 Marker Point（起始就一个，在 Baseline 中心）                                      | Subassembly 被挂上去；原来的 Marker Point 变成 Link 起点，外端出现新的 Marker Point（下一块接这里）                 |
| 6    | 继续点别的 Subassembly（如 Basic Curb and Gutter）                                                    | 命令行继续追问 marker point，鼠标悬停哪个 Marker 就高亮哪个，点选即挂                                               |
| 7    | 一块接一块：Lane → Curb → Sidewalk → Side Slope                                                      | 每块都能实时在 DWG 看到出现                                                                                         |
| 8    | **Insert 插入**：命令行输 "I" → 选新 Marker → 在两块已有之间插                                         | Civil 3D 自动断开 Link 把新块嵌入，**不会重画下游**                                                                 |
| 9    | Esc                                                                                                  | 结束挂载模式                                                                                                        |

**关键点**：

- Subassembly 一旦挂上去就**立刻固化**在 Assembly 对象里（不是"指向调色板的引用"），之后可以独立改参数。
- Tool Palette 里的每个图标本质是一个 `.pkt` 文件（Subassembly Composer 输出）或内置编码部件。
- 参数改了之后，**必须重选图标重拖才生效新参数**；已挂的块改参要走 §4.1 Subassembly Properties。

### 3.2 鸿业：右键菜单"添加 / 插入"+ 单元格编辑

| 步骤 | 操作                                                                                  | UI 反馈                               |
| ---- | ------------------------------------------------------------------------------------- | ------------------------------------- |
| 1    | 在左表格（左半幅）选中任意一行                                                        | 行高亮                                |
| 2    | 右键 → **添加**（在列表末尾加）/ **插入**（在选中行之前加）                            | 新行出现，默认类型 = 机动车道         |
| 3    | 双击单元格：**类型**（下拉：机动/非机动/人行/绿化/中分带/路肩/边坡）、**宽度**、**坡度**、**路牙**（下拉外侧/内侧） | 右侧预览同步                          |
| 4    | 勾选 **"左右相同"**（标题栏选项）                                                      | 编辑左侧时右侧同步（镜像锁）          |
| 5    | 重复 1-3 直到从中心到最外侧都有板块                                                   | 右下角自动显示总宽度                  |

> 鸿业没有"拖"的概念，**纯表格驱动**。行顺序 = 从中心到外侧的拼装顺序，拖动行头可上下移动。

### 3.3 HyCADTool v2：Outliner + 添加按钮 / 右键菜单

> 入口：§2.3 打开的 `CrossSectionDrawWindow`，**Outliner 面板**（左侧，BlenderUI 风格）列出左/右两棵树 + 中分带节点。

| 步骤 | 操作                                                                                | UI 反馈                                                                                                |
| ---- | ----------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------ |
| 1    | Outliner 左半幅 / 右半幅节点上右键 → **添加板块**（或按底部 ➕ 按钮）                 | 新板块默认 `Kind=Pavement / Width=3.5 / CrossSlopePct=1.5 / Name=条带`                                 |
| 2    | 在 Outliner 选中新板块                                                              | 右侧 **PropertyEditor** 面板显示它的所有字段（§1.3）                                                   |
| 3    | PropertyEditor 改参数：Kind 下拉（7 种）、Width、CrossSlopePct、名称                | Canvas（中央预览画布）实时刷新                                                                          |
| 4    | 切换 **路牙**：`KerbOuter` / `KerbInner` 两个字段下拉                               | 外侧路牙 / 内侧路牙可分别设；立缘 / 平缘 / 无                                                           |
| 5    | 板块拖动（Outliner 树）                                                             | ⚠ **v2 未实现**（§9.2-6），必须"删→重加"才能改顺序                                                     |
| 6    | **复制粘贴板块**：Outliner 选中行 → Ctrl+C / Ctrl+V（v2 新增）                       | 粘到同侧末尾                                                                                            |
| 7    | 顶部 **Toolbar** 里的 "左右同步锁"（类似鸿业勾选"左右相同"）                          | 开启后在左侧改 → 右侧镜像同步；关闭后左右独立                                                           |

**添加路径对比**：

| 动作             | Civil 3D                 | 鸿业                  | HyCADTool v2           |
| ---------------- | ------------------------ | --------------------- | ---------------------- |
| 添加板块         | Tool Palette 点图标+ Baseline 点 marker | 右键 → 添加          | Outliner 右键 → 添加板块 |
| 插入板块（中间）  | 命令行 "I" + 选 marker    | 右键 → 插入          | ❌（v2 未实现 §9.2-7）  |
| 删除板块         | 选 → Delete 键           | 右键 → 删除          | Outliner 右键 → 删除     |
| 复制板块         | Ctrl+C / Ctrl+V          | Ctrl+C / Ctrl+V      | ✅ v2 Outliner 内       |
| 左右镜像         | 选 → Right-click → Mirror | 勾选"左右相同"        | Toolbar"左右同步锁"    |

---

## 4. 编辑 Assembly 的工作流

### 4.1 Civil 3D：Subassembly Properties + Assembly Properties 双层

**单块编辑**（Subassembly Properties）：

| 步骤 | 操作                                          | UI 反馈                                                               |
| ---- | --------------------------------------------- | --------------------------------------------------------------------- |
| 1    | 选中 DWG 中的某个 Subassembly                 | Properties palette 自动切到该块                                       |
| 2    | 展开 **Advanced Parameters**                  | 看到 Side / Width / Depth / Slope / Target Parameters 等              |
| 3    | 改 Width → Enter                              | DWG 中该块的几何立刻变；下游块（外侧）自动沿 Link 方向顺延            |
| 4    | Target Parameters 栏 → 点 `...` 按钮           | 弹 **Target Mapping** 对话框（见 §5.1）                               |

**整个 Assembly 编辑**（Assembly Properties）：

| 步骤 | 操作                                                             | UI 反馈                                                |
| ---- | ---------------------------------------------------------------- | ------------------------------------------------------ |
| 1    | 选 Assembly（点 Baseline）→ 右键 → **Assembly Properties**       | 弹 **Assembly Properties** 对话框，有 3 个选项卡       |
| 2    | **Information 选项卡**：Name / Description / Style / Code Set    | 一般只改 Name                                          |
| 3    | **Construction 选项卡**：树状列出所有 Subassembly，可重命名、删除、上下移动 | 最常用的编辑面板                                       |
| 4    | **Codes 选项卡**：Point / Link / Shape Code 的样式绑定            | 出图阶段用                                             |

### 4.2 鸿业：单元格直接改 + 右键菜单

| 操作            | 入口                                        |
| --------------- | ------------------------------------------- |
| 改单板块参数    | 双击该板块单元格（宽度 / 坡度 / 路牙 / 类型） |
| 删除板块        | 右键 → 删除（弹确认框，连带清除关联超高/加宽）|
| 上下移动        | 拖动行头                                    |
| 整体重排（镜像）| 勾选 "左右相同" 复选框                       |
| 改全局参数      | 左上角"设计参数"按钮 → 弹对话框             |

### 4.3 HyCADTool v2：PropertyEditor + Canvas 实时预览

| 编辑对象              | 入口                                                                           | 实时预览            |
| --------------------- | ------------------------------------------------------------------------------ | ------------------- |
| 板块参数（宽 / 坡 / 名） | Outliner 选中 → 右侧 PropertyEditor 改                                         | Canvas 即刻重绘     |
| 板块类型 (Kind)       | PropertyEditor 下拉 7 选                                                       | 路牙工厂自动套      |
| 路牙                  | PropertyEditor → `KerbOuter` / `KerbInner` 下拉 + 三字段（高 / 宽 / 埋深）      | 缘石块实时刷新      |
| 全局参数              | Toolbar 顶部"设计参数"按钮 → 弹下拉面板（DesignSpeed / CenterMedianWidth / Title / ScaleDenominator / 路拱 / 坡型） | Canvas 重算         |
| 路面结构层            | PropertyEditor → 表面层 / 基层 / 垫层三行（厚度 + 材料下拉）                   | ⚠ v2 仅记录字段，出图未画结构层    |
| 删除板块              | Outliner 右键 → 删除                                                           | 即刻                |
| 左右同步改             | Toolbar "左右同步锁"开启后，左侧任意改 → 右侧对称写回                           | 即刻                |

**v2 规范校核（7 项，`CrossSectionCodeChecker`）**：

| #   | 检查项                 | 规范条款             | 校核内容                                   |
| --- | ---------------------- | -------------------- | ------------------------------------------ |
| 1   | 总宽上限（≤ 60m）       | CJJ 37 经验值        | 左 + 中分 + 右 ≤ 60                       |
| 2   | 至少 1 个机动车道       | —                    | `Pavement` 条带 ≥ 1                        |
| 3   | 机动车道宽度区间        | CJJ 37 §5.2.2        | [2.75, 4.0]                                |
| 4   | 人行道最小宽度          | CJJ 37 §5.5.2        | ≥ 1.5                                      |
| 5   | 非机动车道最小宽度      | CJJ 37 §5.4.3        | ≥ 2.0                                      |
| 6   | 横坡区间                | CJJ 37 §6.2.2        | 机动 1–2% / 人行 1–2% / 绿化 0–2%         |
| 7   | 结构层总厚上限          | CJJ 169 经验值       | ≤ 1.2 m                                    |

**界面行为**（同 `07Alignment.md` §6.3）：

- PropertyEditor 下方实时显示 7 行 ✓✗
- 任意项失败 → StatusBar 黄色警告
- 点"确定"时若有失败 → `NonCompliantConfirm` 二次确认

### 4.4 三方编辑工作流对照表

| 操作                | Civil 3D                       | 鸿业                  | HyCADTool v2                    |
| ------------------- | ------------------------------ | --------------------- | ------------------------------- |
| 改单板参数          | Properties → Advanced          | 双击单元格            | ✅ PropertyEditor               |
| 改全局参数          | Assembly Properties 对话框      | "设计参数"按钮        | ✅ Toolbar "设计参数"            |
| 删除板块            | Delete                         | 右键删除              | ✅ Outliner 右键                 |
| 上下移动板块        | Construction 选项卡            | 拖行头                | ❌（§9.2-6）                    |
| 实时画布预览        | ✅（DWG 中即所得）              | ✅（右侧预览）         | ✅（Canvas）                     |
| 实时规范校核        | ⚠ 黄三角（需 Design Check）     | 弹窗"是否继续"         | ✅ 7 项 ✓✗（§4.3 表）            |
| 强制提交（违规）    | 关 Criteria                    | 强制确定              | ✅ `NonCompliantConfirm`         |
| 撤销 / 重做         | Ctrl+Z                         | Ctrl+Z                | ⚠ v2 窗口内无 Undo（§9.2-8）    |

---

## 5. 镜像、复制、Assembly Offset 工作流

### 5.1 Civil 3D：Mirror + Copy to Assembly + Add Assembly Offset

#### 5.1.1 Mirror 镜像

| 步骤 | 操作                                                | UI 反馈                                  |
| ---- | --------------------------------------------------- | ---------------------------------------- |
| 1    | 框选（或按住 Shift 多选）要镜像的 Subassembly         | 高亮                                     |
| 2    | 右键 → **Mirror**（或上下文 Ribbon 里 Mirror）      | —                                        |
| 3    | 命令行 "Select marker point within assembly:"       | 鼠标十字                                 |
| 4    | 在 Baseline 上点对称轴位置（一般点 Baseline 中点）  | 镜像副本挂到对侧                         |
| 5    | Esc                                                  | 结束                                     |

> **关键限制**：Civil 3D 镜像**不是动态链接**。镜像后改一侧的 Width，另一侧**不会跟着动**。这是设计者故意的（方便非对称断面），但对"标准对称路"是反人类。

#### 5.1.2 Copy to Assembly

把已有 Assembly 的一部分复制到另一个 Assembly：

| 步骤 | 操作                                                    | UI 反馈                          |
| ---- | ------------------------------------------------------- | -------------------------------- |
| 1    | 选源 Assembly 的一块或多块 Subassembly                   | 高亮                             |
| 2    | 右键 → **Copy to Assembly**                             | 命令行提示选目标                 |
| 3    | 在目标 Assembly 上点 Marker                             | 副本挂过去                       |

#### 5.1.3 Add Assembly Offset（Offset Point）

入口：**Home tab → Create Design panel → Assembly → Add Assembly Offset**。

| 步骤 | 操作                                           | UI 反馈                                       |
| ---- | ---------------------------------------------- | --------------------------------------------- |
| 1    | Ribbon → Add Assembly Offset                   | 命令行 "Select an assembly:"                  |
| 2    | 选主 Assembly                                  | 命令行 "Specify offset location:"             |
| 3    | 在 Baseline 两侧指定距离（如右侧 5m）          | 在该距离处出现第二根竖的 Baseline（Offset Point）|
| 4    | 从 Tool Palette 拖 Subassembly 挂到 Offset Point | 挂上的部件会跟随 Offset Alignment             |

> 用途：主路 + 辅路在同一 Assembly 里，主路跟主 Alignment / Profile，辅路跟 Offset Alignment / Offset Profile（两套独立的平纵）。

### 5.2 鸿业：左右相同锁 / 一键镜像

- **左右相同** 复选框（顶部 + 单个板块行都有）：勾选后左侧改 → 右侧同步写回，**持续保持同步**（改 Civil 3D 一截）。
- 菜单 **道路 → 断面 → 镜像**：把当前左侧整体拷到右侧（一次性）。
- 鸿业 ❌ Assembly Offset 概念，需要主辅路时必须建两个独立横断面 + 两条中心线。

### 5.3 HyCADTool v2：左右同步锁 + Outliner 复制

| 功能                 | HyCAD v2 入口                                                           |
| -------------------- | ----------------------------------------------------------------------- |
| 持续同步（鸿业风格） | Toolbar "左右同步锁"（切换按钮）                                         |
| 一次性镜像           | Outliner 菜单 → "把左侧镜像到右侧"（v2 新增，未来见 §9.2-11）           |
| 单块复制到对侧       | Outliner 选中 → 右键 → 镜像到右（粘贴时自动反 `Side`）                   |
| Assembly Offset      | ❌ v1/v2 均无；留 `03RoadSelect.md` 的 L3 层（Template）单层架构不支持 |

### 5.4 三方镜像 / 复制 / Offset 对照表

| 能力                      | Civil 3D                          | 鸿业               | HyCADTool v2                  |
| ------------------------- | --------------------------------- | ------------------ | ----------------------------- |
| 一次性镜像                | ✅ Right-click → Mirror           | ✅ 菜单 → 镜像      | ✅ Outliner 右键               |
| 持续同步（左右动态绑定）  | ❌（镜像后即固化）                | ✅ "左右相同"复选框 | ✅ Toolbar "左右同步锁"         |
| 块间复制（同一 Assembly） | ✅ Ctrl+C / Ctrl+V                | ✅ Ctrl+C / Ctrl+V | ✅ Outliner 支持               |
| 块跨 Assembly 复制        | ✅ Copy to Assembly                | ❌                 | ❌                           |
| Assembly Offset（副挂点） | ✅                                 | ❌                 | ❌                           |
| 主辅路同 Assembly         | ✅                                 | ❌                 | ❌                           |

---

## 6. Subassembly / 板块类型库（三方零件清单）

### 6.1 Civil 3D：9 大类工具调色板

入口：Tool Palettes → 右键控件栏 → 选调色板组。

| 大类                    | 典型部件                                                              | 用途                             |
| ----------------------- | --------------------------------------------------------------------- | -------------------------------- |
| **Basic**               | BasicLane / BasicCurbAndGutter / BasicSidewalk / BasicSideSlopeCutDitch | 新手起手的"全能包"                 |
| **Lane**                | LaneOutsideSuper / LaneInsideSuper / LaneSuperelevationAOR / LaneTowardCrown | 主路车道 + 超高                  |
| **Shoulder**            | ShoulderExtendAll / ShoulderExtendSubbase / UrbanShoulder              | 路肩                             |
| **Curb**                | UrbanCurbGutterGeneral / UrbanCurbGutterValley1                        | 各式路缘石                       |
| **Sidewalk**            | UrbanSidewalk / UrbanSidewalkSloped                                    | 人行道                           |
| **Median**              | MedianDepressed / MedianRaised / MedianRaisedConstWidth                | 中分带                           |
| **Daylight**            | DaylightBasic / DaylightStandard / DaylightMinWidth                    | 边坡 / 挖填自适应                |
| **Bridge and Rail**     | BridgeDeck / RailConcreteParapet                                       | 桥面 / 护栏                      |
| **Conditional**         | ConditionalCutOrFill / ConditionalHorizontalTarget                     | 条件子装配（按挖/填切换）        |
| **Generic**             | LinkWidthAndSlope / LinkSlopeToSurface / LinkToMarkedPoint             | 通用"万能积木"                    |

### 6.2 鸿业：7 种板块类型（枚举）

截图自鸿业标准横断面窗口的 "类型" 下拉：

| 鸿业板块       | 典型默认值                      | 主要字段                               |
| -------------- | ------------------------------- | -------------------------------------- |
| 机动车道       | 宽 3.5 / 坡 1.5% / 外侧立缘     | 宽 / 坡 / 路牙                         |
| 非机动车道     | 宽 2.5 / 坡 1.5%                | 宽 / 坡 / 路牙                         |
| 人行道         | 宽 3.0 / 坡 1.5%                | 宽 / 坡 / 路牙                         |
| 绿化带（分车/行道树）| 宽 1.5 / 坡 0% / 无路牙     | 宽 / 坡                                |
| 中央分隔带     | 宽 2.0 / 坡 0% / 两侧立缘       | 宽 / 坡 / 两侧路牙                     |
| 路肩           | 宽 0.5 / 坡 4%                  | 宽 / 坡                                |
| 边坡           | 填 1:1.5 / 挖 1:1.0            | 填坡比 / 挖坡比                        |

### 6.3 HyCADTool：`TemplateComponentKind`（8 值）+ 预设工厂

| Kind 枚举值        | 编号 | 工厂方法（`CrossSectionBand`）       | 默认值                        |
| ------------------ | ---- | ------------------------------------ | ----------------------------- |
| `Pavement`         | 1    | `Lane(w,slope,side,name)` / `LaneWithCurb(...)` | 3.5 / 1.5%                    |
| `Sidewalk`         | 2    | `Sidewalk(w,slope,side,name)`         | 3.0 / 1.5%                    |
| `Kerb`             | 3    | `Kerb(...)`                           | 0.15 / 0% / 单纯缘石          |
| `MedianStrip`      | 4    | `Median(w,side=Center,name)`          | 2.0 / 0%                      |
| `Shoulder`         | 5    | （暂无工厂，用完整构造函数）          | —                             |
| `Slope`            | 6    | （v1.x 占位）                        | —                             |
| `NonMotorized`     | 7    | `NonMotor(w,slope,side,name)`         | 2.5 / 1.5%                    |
| `GreenStrip`       | 8    | `GreenStrip(w,side,name)`             | 1.5 / 0%                      |

**预设库**（`CrossSectionPresets`）：

| 预设 ID              | 描述                         | 总宽  | 默认速度  |
| -------------------- | ---------------------------- | ----- | --------- |
| `urban-arterial`     | 城市主干路（双向 6 车道 + 2m 中分 + 3m 人行） | 29 m | 60 km/h  |
| `secondary`          | 城市次干路（双向 4 车道 + 无中分 + 2.5m 人行） | 19 m | 50 km/h  |
| `local`              | 城市支路（双向 2 车道 + 2m 人行）             | 11 m | 30 km/h  |

### 6.4 三方零件库对照表

| 维度                | Civil 3D                             | 鸿业                 | HyCADTool v2                            |
| ------------------- | ------------------------------------ | -------------------- | --------------------------------------- |
| 部件总数             | 200+ 内置 + 社区无限                  | 7 种类型（参数化展开） | 8 种 Kind + 6 个工厂方法                 |
| 用户可扩展           | ✅ Subassembly Composer 作 PKT        | ❌ 硬编码             | ⚠ 加 `Kind` 需改源码                    |
| 条件逻辑（挖/填切换）| ✅ Conditional 类                     | ❌ 手工               | ❌                                      |
| 边坡 / Daylight      | ✅ Daylight 类（完整）                 | ✅ 边坡枚举（简单）    | ⚠ `Slope` 枚举占位，未实现              |
| 桥 / 护栏            | ✅ Bridge and Rail                    | ❌                   | ❌                                      |
| 本土化（CJJ 默认值） | ❌（AASHTO 默认）                     | ✅                   | ✅（3 个 CJJ 37 预设）                   |
| 出图编码系统         | ✅ Point/Link/Shape Code（跨项目统一） | ❌                   | ❌（`Kind + Name` 就地派生）             |

---

## 7. 自定义 Subassembly 的工作流

### 7.1 Civil 3D：Subassembly Composer（外部工具）

入口：独立 app **Autodesk Subassembly Composer for AutoCAD Civil 3D**（与 Civil 3D 同装）。

| 步骤 | 操作                                                                                             | UI 反馈                                                                                          |
| ---- | ------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------ |
| 1    | 启动 Subassembly Composer                                                                        | 主界面 5 区：**Toolbox / Flowchart / Properties / Preview / Packet Settings**                    |
| 2    | **Packet Settings 选项卡** → 填 Name / Description / Help / Image                                 | 这些会出现在导入 Civil 3D 后的 Help 窗口                                                         |
| 3    | **Input/Output Parameters 选项卡** → Create parameter（输入参数，如 Width / Slope / Depth）      | 每个参数有 Type（double / string / bool）/ Default / Display Name                                |
| 4    | **Target Parameters 选项卡** → Create parameter（类型：Elevation / Offset / Surface）             | 这里定义本部件能接哪些"外部目标"（见 §5.1.3 的 Target Mapping）                                  |
| 5    | **Flowchart 面板**：从 Toolbox 拖 Geometry 节点（Add Point / Add Link / Add Shape）+ Operator 节点 | 像画 Scratch 一样连节点                                                                         |
| 6    | 每个节点在 Properties 里配参数（来自 §3 的输入参数，用 `{Width}` 占位）                           | 实时在 Preview 里看效果                                                                          |
| 7    | Test Subassembly（Preview 里给输入值）                                                            | 预览图刷新                                                                                       |
| 8    | File → **Save** → 导出 `.pkt` 文件                                                                | 放到 `C:\ProgramData\Autodesk\C3D 2026\eng\Subassemblies\` 或自建库                              |
| 9    | 在 Civil 3D 里：Tool Palette 右键 → **New Tool From PKT** → 选 `.pkt`                             | 新图标出现在调色板，可直接拖用                                                                   |

**语法约束**：Composer 只支持 VB.NET 表达式语法（Decision Tree、IIF、Math）——见 `Civil3D.md` §2.6。

### 7.2 鸿业：❌ 不支持

鸿业板块类型是硬编码的 7 种，用户无法扩展。

### 7.3 HyCADTool：扩展点（v1 未开放）

Civil3D.md §3.1 提到未来的扩展方向：

- **C# 原生** `ISubassembly` 实现（对标 Civil 3D 的 PKT，但用 C# 代替 VB）
- 加载方式：热加载到 `AssemblyLibrary` 目录
- v1/v2 **均未实现**（§9.2-10），现阶段扩展部件必须改源码加 `Kind` 枚举 + 工厂方法。

### 7.4 三方自定义能力对照

| 维度              | Civil 3D                       | 鸿业           | HyCADTool v2                    |
| ----------------- | ------------------------------ | -------------- | ------------------------------- |
| 有扩展机制        | ✅ Subassembly Composer（PKT） | ❌             | ❌（v1/v2 均未开）              |
| 扩展语言          | VB.NET（PKT 节点表达式）        | —              | 规划 C#（v2+）                  |
| 独立工具还是源码 | 独立工具（可视化 + 无需编译）    | —              | 规划源码 / 独立工具待定          |
| 条件分支 / 决策树 | ✅（Decision 节点）             | —              | ❌                              |
| 调试预览          | ✅ Preview 面板                 | —              | ❌                              |
| 分享机制          | PKT 文件可拷贝                  | —              | 规划 JSON 或 C# DLL             |

---

## 8. 端到端场景对比：城市主干路横断面 29m，双向 6 车道

> 用一个**完全相同的设计任务**，比较三家从"打开 CAD"到"出一张标准横断面"的全部步骤、时间。
>
> 任务：双向 6 车道（3×3.5m 机动）+ 2m 中分带 + 2×3m 人行道；总宽 29m；设计速度 60 km/h；绘到当前 DWG 模型空间。

### 8.1 Civil 3D 操作步骤（约 8–10 分钟）

| 步 | 操作                                                                                                                                                                                    | 时间    |
| -- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------- |
| 1  | Home → Assembly → Create Assembly → Name=Arterial / Type=Divided Crowned Road / Style=Basic / Code Set=All Codes / Layer=C-ROAD-ASSM → OK                                               | 60s     |
| 2  | 在 DWG 空白处点 Baseline 位置                                                                                                                                                            | 5s      |
| 3  | Ctrl+3 打开 Tool Palette → Medians tab → **MedianDepressed** → Properties 改 Width=2.0, Depth=0.3 → 点 Baseline 中心 Marker                                                              | 60s     |
| 4  | Tool Palette → Lanes tab → **LaneOutsideSuper** → Properties 改 Side=Left, Width=3.5, Slope=-1.5% → 点中分带左侧 Marker                                                                  | 60s     |
| 5  | 重复 4 再挂两条左侧车道（机动 2 / 机动 3）                                                                                                                                               | 60s     |
| 6  | Tool Palette → Curb tab → **UrbanCurbGutterGeneral** → Side=Left → 点最外侧车道边 Marker                                                                                                 | 30s     |
| 7  | Tool Palette → Sidewalk tab → **UrbanSidewalk** → Side=Left, Width=3.0 → 点缘石外侧 Marker                                                                                               | 30s     |
| 8  | 框选左侧全部 4 块 Subassembly → Right-click → **Mirror** → 点 Baseline 中心 Marker                                                                                                        | 30s     |
| 9  | 检查：选 Assembly → Assembly Properties → Construction 选项卡核对板块顺序                                                                                                                | 60s     |
| 10 | （可选）导出到 Tool Palette 以复用：拖 Baseline 到 Tool Palette                                                                                                                          | 60s     |
| **总** | **约 7 分钟**                                                                                                                                                                         | **7 min** |

### 8.2 鸿业操作步骤（约 5 分钟）

| 步 | 操作                                                                                   | 时间      |
| -- | -------------------------------------------------------------------------------------- | --------- |
| 1  | 菜单 道路 → 标准横断面设计                                                             | 3s        |
| 2  | 勾选"左右相同"；左上角"等级 = 城市主干路 / 设计速度 = 60 km/h"                          | 30s       |
| 3  | 表格默认已有主干路 4 板块（机动 + 中分 + 人行）——改宽度：机动 3.5 × 3 / 中分 2 / 人行 3 | 90s       |
| 4  | 双击单元格改横坡：机动 1.5% / 人行 1.5% / 中分 0%                                      | 60s       |
| 5  | 外侧机动车道"路牙 = 立缘 15×10×50"                                                     | 30s       |
| 6  | "确定"→ 自动出图到 DWG                                                                 | 30s       |
| 7  | 弹规范校核结果（如有警告确认）                                                         | 10s       |
| **总** | **约 4.5 分钟**                                                                      | **4.5 min** |

### 8.3 HyCADTool 操作步骤（v2 现状，约 1.5 分钟）

| 步 | 操作                                                                                         | 时间      |
| -- | -------------------------------------------------------------------------------------------- | --------- |
| 1  | 命令行 `hyRoadCs` → 回车（默认 N 新建）                                                      | 3s        |
| 2  | 窗口自动加载 `urban-arterial` 预设（主干路 + 中分 + 人行，参数已按 CJJ 37 配齐）             | 5s        |
| 3  | 肉眼看 Canvas 预览 + PropertyEditor 7 项 ✓✗ 全绿                                             | 10s       |
| 4  | （可选）Toolbar 改 Title="主干路 K0+000~K1+200 标准横断面"                                   | 20s       |
| 5  | 点"确定"→ 命令行 "指定横断面插入点："                                                        | 1s        |
| 6  | 在 DWG 上点一下                                                                              | 2s        |
| 7  | 自动出图 + JSON 落盘 + 自检 + 命令行汇总                                                     | 3s        |
| **总** | **约 45 秒**                                                                                | **0.75 min** |

### 8.4 步骤数 vs 灵活度的折中

| 维度                      | Civil 3D        | 鸿业          | HyCADTool v2       |
| ------------------------- | --------------- | ------------- | ------------------ |
| 总操作步数                 | 10              | 7             | 7                  |
| 鼠标点击次数（估）         | 40+             | 20+           | 8                  |
| 键盘输入次数（估）         | 15+             | 10+           | 2（命令名 + 回车） |
| 完成时间（简单标准断面）   | 7 min           | 4.5 min       | 0.75 min           |
| 完成时间（异形断面）       | 10–15 min       | 6–10 min      | 受限于 §9.2 缺口   |
| 可调参数数（创建期）       | 100+（每块独立） | 40+           | 20（PropertyEditor + Toolbar）|
| 零件库丰富度               | 200+            | 7             | 8                  |
| 学习曲线                   | 陡（2–3 周）    | 中（2 天）    | 平（20 分钟）      |
| 复杂断面（主辅 + 分幅）天花板 | 高（Offset + Conditional）| 中（需拆两条）| 低（v2 暂不支持）  |

---

## 9. HyCADTool 功能清单与差距

### 9.1 v2 已实现 ✅

#### 9.1.1 创建 / 加载 / 命令行三入口

- ✅ `hyRoadCs` 命令 + `RoadCrossSectionDrawCommand`（取代 v1 `hyRoadT`）
- ✅ 三分支：新建（加载预设）/ 加载（从 DWG Templates）/ 命令行（纯批量）
- ✅ 三个 CJJ 37 预设：主干路 / 次干路 / 支路

#### 9.1.2 BlenderUI workbench 窗口

- ✅ `CrossSectionDrawWindow` 5 区：Outliner / Toolbar / Canvas / PropertyEditor / StatusBar
- ✅ Canvas 实时预览（修改即重绘）
- ✅ 侧栏显隐 + 复制粘贴（v2 新增）

#### 9.1.3 板块操作

- ✅ 添加（Outliner 右键 / ➕ 按钮）
- ✅ 删除（右键）
- ✅ 左右同步锁（Toolbar 切换）
- ✅ 单块镜像到对侧（Outliner 右键）
- ✅ 复制粘贴板块（Ctrl+C / Ctrl+V，v2 新增）

#### 9.1.4 路牙 / 坡型 / 路拱 / 结构层

- ✅ `KerbSpec` VO：立缘 / 平缘 / 无；内外侧可分别配
- ✅ 路拱（`RoadCrownProfile`）：Linear / Parabolic
- ✅ 坡型（`RoadSlopeType`）：Single / Double
- ✅ 结构层（`RoadSurfaceLayer`）：表面 / 基层 / 垫层三层字段（v2 暂不出图）

#### 9.1.5 规范校核

- ✅ 7 项实时校核（见 §4.3）
- ✅ `NonCompliantConfirm` 二次确认
- ✅ 速度查 CJJ 37 关联 `AlignmentCodeChecker.SupportedSpeeds`

#### 9.1.6 出图 / 持久化

- ✅ `hyRoadCs` → `CrossSectionFigure` → ModelSpace 绘制（带路牙块 / 路拱曲线 / 尺寸标注）
- ✅ JSON 落盘到 `Design.Templates` 列表；DWG 保存时写入 `.roaddesign.json`

### 9.2 与 Civil 3D 对标的缺口 ❌

| #   | 缺口                                                  | 优先级 | 备注                                                |
| --- | ----------------------------------------------------- | ------ | --------------------------------------------------- |
| 1   | Assembly Type 选择对话框（5 种）                      | 🟡 中  | v2 由 `DesignSpeed` 间接推                          |
| 2   | Point / Link / Shape Code 出图编码系统                | 🟡 中  | 便于跨项目样式复用                                  |
| 3   | Code Set Style + 可定制颜色 / 图层                    | 🟡 中  | 现硬编码 `RD-CS-*`                                   |
| 4   | Assembly Properties 对话框（Information / Construction / Codes 三卡） | 🔴 低  | v2 PropertyEditor 已能覆盖                          |
| 5   | Tool Palette 形式的零件库（拖放）                     | 🟢 高  | v2 只能从 Outliner 菜单加，无视觉化库                |
| 6   | 板块**上下移动 / 拖拽排序**                            | 🟢 高  | v2 暂不能改顺序                                     |
| 7   | 板块**中间插入**（在两块之间）                        | 🟡 中  | v2 只能加到末尾                                     |
| 8   | 窗口内 Undo / Redo                                    | 🟡 中  | 改错要靠"取消"整体放弃                              |
| 9   | Conditional Subassembly（挖/填条件切换）              | 🔴 低  | 有边坡才需                                          |
| 10  | 自定义 Subassembly 机制（§7.3 规划）                   | 🔴 低  | 非标断面才需，v2 改源码                             |
| 11  | 一次性"整体镜像左到右" / "右到左"                      | 🟡 中  | v2 只有单块镜像                                     |
| 12  | Assembly Offset（主辅路同 Assembly）                  | 🔴 低  | 拓宽段 / 辅道建模                                   |
| 13  | 把当前 Assembly 保存到零件库（跨 DWG 复用）            | 🟡 中  | 现在只能复用同 DWG 的预设                           |
| 14  | Target Parameters（部件参数绑外部对象）               | 🔴 低  | 走 Corridor 才需                                    |

### 9.3 与鸿业对标的缺口 ❌

| #   | 缺口                                      | 优先级 | 备注                               |
| --- | ----------------------------------------- | ------ | ---------------------------------- |
| 1   | 批量出图：一条 Alignment 按桩号全线出断面 | 🟢 高  | 现 v2 一次只画一张标准断面         |
| 2   | 超高 / 加宽表与板块联动（改宽度同步超高表） | 🟡 中  | v2 无超高数据模型                  |
| 3   | 边坡参数：填坡比 / 挖坡比 / 挡土墙占位     | 🟡 中  | `Slope` 枚举在 v1/v2 仅占位        |
| 4   | `.hdy` 格式 / 标准横断面文件库浏览器       | 🔴 低  | v2 复用 `Design.Templates`         |
| 5   | 板块类型 = 分车绿带（区别于行道树绿带）   | 🔴 低  | v2 统一走 `GreenStrip`             |
| 6   | 路面结构层真实出图（沥青 / 水泥 / 基层填充）| 🟢 高  | v2 仅字段记录未画                  |
| 7   | 工程量表按板块面积 / 长度导出 CSV          | 🟢 高  | 同 `07Alignment.md` §8.3           |

### 9.4 v1.x 优先补的 4 个功能

> 选取标准：填补 §9.2 / §9.3 中"高优先级 + 1 周内能完成"的项。

#### 1. 板块拖拽排序 / 上下移动（§9.2-6）

- 命令名建议：合并到 `CrossSectionDrawWindow`，不单独开命令
- UI：Outliner 树节点支持 ↑ / ↓ 按钮 + 拖拽（WPF TreeView 的 `DragDrop` 事件）
- 实现：修改 `CrossSectionDrawViewModel` 的 `LeftBands` / `RightBands` 顺序 + 触发 `Recalculate()`
- 价值：用户最痛的痛点（v2 唯一的修改手段是删了重加）

#### 2. 板块中间插入（§9.2-7）

- 命令名建议：Outliner 右键菜单项"在此之前插入 / 在此之后插入"
- 实现：`InsertAt(index, band)` 方法 + `MoveToIndex(from, to)`
- 价值：补配 §9.4-1，一套完整的"增删改排"能力

#### 3. 整体一键镜像（§9.2-11）

- 命令名建议：Toolbar 按钮"L→R 镜像整体" / "R→L 镜像整体"
- 实现：清空目标侧，左侧按顺序 `Side = opposite` 后拷贝过去
- 行为：与"左右同步锁"互斥，点一下是一次性复制，之后双向独立编辑
- 价值：首次配非对称断面很快

#### 4. 批量出图沿 Alignment 自动断面（§9.3-1）

- 命令名建议：`hyRoadCsLine`（Cross Section along aLignment）
- 操作：先选 Alignment + 输起止桩号 + 输间隔（默认 20m，和主桩一致） + 选 Template
- 输出：批量 Figure 块插入到另一个 Layout 或网格排布
- 价值：出施工图阶段的真实需求，v2 只能一张一张画

### 9.5 v2.x 优先补的 3 个功能

#### 1. 窗口内 Undo / Redo（§9.2-8）

- 快捷键：Ctrl+Z / Ctrl+Shift+Z
- 实现：`CrossSectionDrawViewModel` 维护一个 `Stack<CrossSectionLayout>` 快照链
- 每次 `Recalculate()` 前先 push；Undo 就 pop 回滚
- 价值：鸿业 / Civil 3D 都有，v2 必须追上

#### 2. Tool Palette 风格的零件库面板（§9.2-5）

- 命令名建议：合入 `CrossSectionDrawWindow`，新增第四个侧栏"零件库"
- 内容：8 个 `TemplateComponentKind` 图标，拖到 Canvas / Outliner 即挂
- 持久化：把当前 Assembly 另存为 "我的零件" 块，供下次复用
- 价值：补 §9.2-5 + §9.2-13

#### 3. 路面结构层真实出图（§9.3-6）

- 命令名建议：Toolbar"显示结构层"开关
- 实现：`CrossSectionFigureBuilder` 遍历每板块的 `SurfaceLayer` 数组，按厚度在板块下方叠画闭合多段线 + 对应图案填充
- 图例：底部自动生成结构层说明表（材料 + 厚度）
- 价值：补 §9.3-6，是施工图必备

### 9.6 v3.x 展望（暂缓）

| 方向                       | 说明                                                          | 依赖                   |
| -------------------------- | ------------------------------------------------------------- | ---------------------- |
| Assembly Offset / 主辅路   | 同 Assembly 挂多条 Baseline                                   | Alignment 支持 Offset 轴 |
| Corridor + 动态横断面联动  | Alignment 改 → 所有桩号断面重算                               | `RoadCorridorService`  |
| Target 参数（宽度绑 Feature Line）| 宽度跟着外部 Polyline 变                               | Feature Line 支持      |
| 自定义 Subassembly（C#）   | 热加载 DLL + C# `ISubassembly`                                | `AssemblyLibrary`      |
| Conditional（挖/填切换）   | 同一 Assembly 在挖方 / 填方不同桩号挂不同板块                 | Corridor + Conditional |
| 三维可视化（Blender）       | 断面拉伸 + 材料赋值导出 glTF                                   | `Road3dExportGltfCommand` |

---

## 10. 速查：命令 / 菜单 / 快捷键对照

### 10.1 Civil 3D

| 功能               | 菜单路径                                                    | 命令行                    |
| ------------------ | ----------------------------------------------------------- | ------------------------- |
| 新建 Assembly      | Home → Create Design → Assembly → Create Assembly           | `CreateAssembly`          |
| 添加 Subassembly   | Tool Palettes (Ctrl+3) → 拖                                 | —                         |
| 插入到中间         | 拖时命令行输 `I`                                            | —                         |
| 镜像               | 选 Subassembly → 右键 → Mirror                              | `MirrorAssemblies`        |
| Add Offset         | Home → Create Design → Assembly → Add Assembly Offset        | `AddAssemblyOffset`       |
| Assembly Properties| 选 Assembly → 右键 → Assembly Properties                     | `EditAssemblyProperties`  |
| 保存到 Tool Palette| 拖 Baseline 到 Palette                                      | —                         |

### 10.2 鸿业

| 功能         | 菜单路径                   |
| ------------ | -------------------------- |
| 标准横断面设计 | 道路 → 标准横断面设计        |
| 加载 hdy      | 窗口右下角文件夹图标        |
| 添加板块     | 表格右键 → 添加              |
| 插入板块     | 表格右键 → 插入              |
| 删除板块     | 表格右键 → 删除              |
| 左右同步     | 窗口标题"左右相同"复选框    |
| 一次性镜像   | 道路 → 断面 → 镜像            |

### 10.3 HyCADTool

| 功能                  | 命令行 / 入口                           |
| --------------------- | --------------------------------------- |
| 新建 / 加载 / 命令行   | `hyRoadCs`（v1 `hyRoadT` 已 Obsolete）  |
| 添加板块               | 窗口 Outliner 右键 → 添加板块             |
| 删除板块               | 窗口 Outliner 右键 → 删除                |
| 单块镜像到对侧         | 窗口 Outliner 右键 → 镜像到对侧           |
| 复制 / 粘贴            | Ctrl+C / Ctrl+V（Outliner）              |
| 左右同步锁             | 窗口 Toolbar 切换按钮                    |
| 设计参数                | 窗口 Toolbar "设计参数"按钮              |
| 规范校核               | 窗口 PropertyEditor 下方 7 项 ✓✗         |
| 批量脚本（纯命令行）    | `hyRoadCs` → C                           |

---

## 11. 变更记录

| 版本  | 日期       | 变更                       |
| ----- | ---------- | -------------------------- |
| v0.1  | 2026-04-19 | 初稿：10 节 + 三方对标完整 |
