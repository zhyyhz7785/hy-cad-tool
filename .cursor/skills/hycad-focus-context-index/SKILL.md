---
name: hycad-focus-context-index
description: |
  Build or update a single-entry "AI 上下文索引" markdown for a HyCADTool 工作聚焦主题（道路设计 / 钢筋 / 桩基 / 布基础 / 道路交叉口…）。
  产出一个 03X/04X 号代号文件（如 `doc/RoadDesign/041-道路设计-AI上下文索引.md`），
  聚合该主题的 总纲 / 对标 / 快照 / 备忘 四类 Markdown，
  以及 `HyCADTool.Refactored` 下对应 `.cs` 与 `.xaml` 的 **分层文件清单**（Views/ViewModels/Commands/Domain/Infrastructure）。
  用户只需在对话里写代号（"041"）或 `@` 该文件即可一次性拉齐上下文。
  触发场景：用户说 "做一个索引" / "041 风格" / "让 @ 一个文件就够" / "给 xxx 主题做个 AI 上下文入口" /
  "补 cs xaml 到 04X" / "完善/更新 0XX 索引" 时。
author: Cursor Agent
version: 1.0.0
---

# HyCADTool 工作聚焦 · AI 上下文索引生成

## 产出定位（Single Entry Point）

- **代号**：三位数字（如 `041`/`042`），对话里的快速记号
- **路径**：`doc/<子领域目录>/<代号>-<主题>-AI上下文索引.md`
- **作用**：`@` 一次即拉齐该主题的 文档树 + 源码锚点 + 避坑 skill
- **边界**：**只做文件级目录**，不贴方法签名、不解释实现（那是 `markdown-editor-doc` L2/L3 的事）

参考实例：`doc/RoadDesign/041-道路设计-AI上下文索引.md`

---

## 何时应用本 skill

用户说：
- "制作索引 / 生成索引 / 041 风格 / 做个 04X"
- "以后我的上下文只需要传入一个 XX 就够了"
- "按 <主题> 做个 AI 上下文入口 / 聚合入口"
- "补 cs / xaml 到 <代号> / 完善 <代号>"
- "更新 <代号> 的源码清单"

---

## 固定七节模板

索引文件**必须**按此顺序，缺项直接省略章节（不留空表）：

1. **一、必读总纲** — P0/P1 的总纲、计划书、路线图
2. **二、领域与对标** — 按主题深入的文档
3. **三、实施快照与工程笔记** — `05X-标题-YYYY-MM-DD.md` 类日期报告
4. **四、个人工作备忘（非正式规格）** — `00-*.md` 随记
5. **五、源码索引** — 分层的 `.cs` / `.xaml` 文件表
6. **六、项目级避坑** — 关联 `.cursor/skills/` 名
7. **七、维护说明** — 新增文档/源码时如何回填本索引

---

## 执行流程（新建索引）

### Step 1 — 对齐参数

向用户确认（未明确时按会话推断并回显）：

| 字段 | 示例 | 用途 |
|------|------|------|
| 主题中文 | 道路设计 | 标题 / 正文 |
| 主题前缀 | `Road` | `Glob`/`Grep` 扫源码 |
| 代号 | `041` | 三位数字，按同目录习惯向后排 |
| 文档目录 | `doc/RoadDesign/` | 已存在的 Markdown 目录 |
| 源码根 | `HyCADTool.Refactored/` | 默认即可 |

### Step 2 — 扫 Markdown 并归类

- `Glob doc/<目录>/*.md` 得到全部 md
- 按文件名归类（命中任一即分入该桶）：

| 桶 | 命中规则 |
|----|---------|
| 必读总纲 | `README.md` / `01MASTER*.md` / `05计划*.md` / `04Pipeline*.md` |
| 对标 / 领域 | `02*.md` / `03*.md` / `06*.md` / `07*.md` / `08*.md` / `09*.md` / `10*.md`；单品对标（Civil3D.md、HongYe*.md、HintCAD.md…） |
| 实施快照 | `05X-<标题>-YYYY-MM-DD.md` |
| 个人备忘 | `00-*.md` 或明显是随记 |

### Step 3 — 扫源码并分层（关键）

对 `HyCADTool.Refactored/` 执行 `Glob`，按主题前缀 `<Prefix>` 分层落到各表；**某一层无命中即省略该表**。

| 层 | 目录模式 |
|----|---------|
| WPF 资源 | `Presentation/Resources/*<Prefix>*.xaml` |
| 偏好 / 设置 | `Presentation/Views/Preferences/*<Prefix>*.xaml(.cs)` |
| 窗口 / 面板 | `Presentation/Views/<Prefix>/*.xaml(.cs)` |
| ViewModels | `Presentation/ViewModels/<Prefix>/*.cs` + 根目录 `<Prefix>*ViewModel.cs` |
| 命令 | `Presentation/Commands/<Prefix>/*.cs` |
| 领域模型 | `Domain/Models/<Prefix>/**/*.cs` |
| 值对象 | `Domain/ValueObjects/<Prefix>/*.cs` |
| 领域服务 | `Domain/Services/<Prefix>/*.cs` |
| 领域事件 | `Domain/Events/<Prefix>/*.cs` |
| AutoCAD 服务 | `Infrastructure/AutoCAD/Services/<Prefix>/*.cs` |
| 几何桥接 | `Infrastructure/AutoCAD/Geometry/*<Prefix>*.cs` |
| 交互 Jig | `Infrastructure/AutoCAD/Interactive/*<Prefix>*.cs` |
| Xdata / 图层 | `Infrastructure/AutoCAD/Xdata/*<Prefix>*.cs` |
| IO | `Infrastructure/IO/<Prefix>/*.cs` |

呈现规则：

- XAML 与同名 `.xaml.cs` **成对展示**（见模板的 5.2）
- 每个文件只列 **文件名 + 一句话职责**（可缺省），禁止贴方法签名
- 每组加一句说明父目录，表格后**不**追加废话

### Step 4 — 关联 skill

在 `.cursor/skills/` 下扫现有 skill，按主题写进第六节：

| 主题特征 | 建议关联 skill |
|---------|----------------|
| AutoCAD + PaletteSet + WPF | `hycad-project-pitfalls`、`wpf-paletteset-resource-pitfalls` |
| 命令迁移 / 分层 | `hycad-refactored-migration-patterns` |
| WebView2 | `wpf-webview2-pitfalls` |
| 文档生成 | `markdown-editor-doc` |

### Step 5 — 写文件

用 `Write` 生成 `doc/<目录>/<代号>-<主题>-AI上下文索引.md`，按下方「输出模板」填充。

---

## 输出模板（原样套用，`<>` 处替换）

```markdown
# <代号> · <主题> · AI 上下文索引

> **代号 `<代号>`**：在对话里写「<代号>」或 `@` 本文件，即表示需要按<主题>（HyCADTool.Refactored）的**文档树 + 代码锚点**对齐后再回答或改代码。  
> 完整路径：`doc/<目录>/<代号>-<主题>-AI上下文索引.md`

---

## 一、必读总纲

| 优先级 | 文档 | 用途 |
|:------:|------|------|
| P0 | [...](./...) | ... |

## 二、领域与对标

| 主题 | 文档 |
|------|------|

## 三、实施快照与工程笔记

| 编号 | 文档 | 用途 |
|------|------|------|

## 四、个人工作备忘（非正式规格）

| 文件 | 说明 |
|------|------|
| [00-.md](./00-.md) | 随记；**以正式文档与代码为准** |

## 五、源码索引（HyCADTool.Refactored）

以下路径相对仓库根目录。DI 注册在 `Infrastructure/Configuration/AutofacModule.cs` 中搜 `<Prefix>` 即可；持久化细节以对应快照文档为准。

### 5.1 WPF 资源与偏好

| 文件 | 说明 |

### 5.2 Views/<Prefix>/（窗口 / 面板）

| XAML | Code-behind |

### 5.3 ViewModels

### 5.4 命令 Commands/<Prefix>/

### 5.5 领域模型 Domain/Models/<Prefix>/

### 5.6 值对象 Domain/ValueObjects/<Prefix>/

### 5.7 领域服务 Domain/Services/<Prefix>/

### 5.8 领域事件 Domain/Events/<Prefix>/

### 5.9 Infrastructure（AutoCAD 服务 / 几何 / 交互 / Xdata / IO）

## 六、项目级避坑

- <skill 名>（原因一句）

## 七、维护说明

- **文档**：新增时在第二/三节增加一行。
- **源码**：在 `HyCADTool.Refactored/` 下新增或搬迁相关文件时，同步更新 **第五节** 对应分组。
```

---

## 执行流程（更新已存在索引）

用户说「补 xxx 到 <代号>」或「完善 <代号>」时：

1. `Read` 索引文件
2. 依 Step 2/3 重新扫目录；做差集
3. 仅用 `StrReplace` 精准替换 **第五节（或第二/三节）** 对应子表，**不**重写全文
4. 回显新增/删除清单给用户

---

## 反模式（禁止）

- ❌ 把方法签名 / 代码体塞进索引 — 那是 L2/L3 的事
- ❌ 一张大平铺表 — 必须分层，单表超 30 行就看不动
- ❌ Windows 反斜杠路径 — 一律 `/`
- ❌ 为同一主题维护多个代号索引 — 一个主题只保留一个
- ❌ 把编译产物（`obj/**`、`*.g.i.cs`）列进源码表 — `Glob` 要过滤 `obj/`
- ❌ 让代号与 `doc/<目录>/` 已有编号冲突 — 冲突时向后排或征询用户

---

## 已有实例

- `doc/RoadDesign/041-道路设计-AI上下文索引.md`（道路设计，代号 `041`）

后续新增请在本节补一行，方便快速复用模板。
