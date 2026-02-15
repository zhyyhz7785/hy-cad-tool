# HyCADTool.TextLayout 文档（L1）

## 1. 模块定位

`HyCADTool.TextLayout` 是一个与平台解耦的排版内核，负责把 Markdown 内容转换为可分页、可分栏、可落图的布局结果，并输出 AutoCAD `MText` 字符串。

目标是统一预览与导出的排版口径，避免两套逻辑导致的显示不一致。

---

## 2. 核心流程（L1）

1. **解析块**：将 Markdown 切分为顶层块，并标注块类型（标题、段落、列表、表格、代码块等）。
2. **估算高度**：按配置（字号、行距、缩放、栏宽）估算每个块的占用高度。
3. **分页分栏**：按“当前栏放不下则流向下一栏/下一页”策略分配块索引。
4. **渲染输出**：将 Markdown AST 渲染为 AutoCAD `MText` 控制码。

---

## 3. 主要输入与输出

- 输入：
  - Markdown 原文字符串
  - `DesignSpecConfig`（版式、字体、页面、缩放、栏数等）
- 输出：
  - `LayoutResult`（页集合、每栏块索引、每栏高度、每栏字符容量）
  - `MText` 字符串（可直接用于 CAD 文本实体）

---

## 4. 关键对象

- `DesignSpecConfig`：统一配置与缩放换算入口。
- `DocumentBlock`：顶层块的标准化数据结构。
- `LayoutEngine`：分页分栏调度核心。
- `LayoutResult / LayoutPage`：布局结果载体。

---

## 5. 文件职责总览

- `LayoutModels.cs`：布局模型定义（块、页、结果）。
- `DesignSpecConfig.cs`：参数模型、归一化与校验。
- `MarkdownBlockParser.cs`：Markdown -> `DocumentBlock` 列表。
- `LayoutEngine.cs`：分栏分页引擎（估高 + 流式分配）。
- `MarkdownColumnSplitter.cs`：按布局索引回切 Markdown。
- `MarkdownToMTextRenderer.cs`：Markdown -> AutoCAD `MText`。
- `MarkdownTableExtractor.cs`：表格抽取与剥离。
- `DisplayWidthCalculator.cs`：显示宽度口径（ASCII=1，CJK/全角=2）。
- `TextAreaCalculator.cs`：文本区宽高与栏宽计算。

---

## 6. 模块边界（L1）

- 依赖：仅 `Markdig`（Markdown AST 解析）。
- 目标框架：`netstandard2.0`。
- 不直接依赖 AutoCAD API，保持可复用的排版内核定位。

---

## 7. 快速调用链

典型调用顺序：

1. `MarkdownBlockParser.ParseTopLevelBlocks(markdown)`
2. `new LayoutEngine().Distribute(blocks, config)`
3. `result.ToColumnParagraphIndicesText(pageIndex)`（供按列切分）
4. `MarkdownColumnSplitter.SplitByColumnIndices(markdown, indices)`
5. `new MarkdownToMTextRenderer(config).Convert(columnMarkdown)`

---

## 8. 当前结论（L1）

`HyCADTool.TextLayout` 已具备“解析、估高、分栏分页、MText 渲染”的完整基础链路，能够作为 Markdown 预览与 CAD 导出的统一排版内核使用。
