# Markdown 设计说明排版 v2 改进

## 核心变更

### 1. 预览引擎：替换为 MdXaml（最优方案）

调研结论：**MdXaml** 是 WPF 生态最成熟的 Markdown 渲染控件。

- NuGet: `MdXaml` 1.27.0，支持 .NET Framework 4.5+
- 原生 GFM 表格渲染（含 colspan/rowspan）
- 通过 `MarkdownStyle` 属性自定义暗色主题
- 内置 `MarkdownScrollViewer` 控件 -- 直接绑定 Markdown 字符串，零手写渲染代码
- 对比当前方案：**删除 ViewModel 中全部 150+ 行手写 FlowDocument 渲染代码**，替换为 1 个 XAML 控件

具体改动：

- [MarkdownEditorDialog.xaml](HyCADTool.Refactored/Presentation/Views/MarkdownEditorDialog.xaml): 将右侧 `FlowDocumentScrollViewer` 替换为 `mdxam:MarkdownScrollViewer`，添加暗色主题 ResourceDictionary
- [MarkdownEditorViewModel.cs](HyCADTool.Refactored/Presentation/ViewModels/MarkdownEditorViewModel.cs): 删除 `RenderBlockToFlowDoc`、`AddInlinesToParagraph`、`ConvertInline` 全部方法，删除 `PreviewDocument` 属性，`MarkdownText` 直接绑定到 `MarkdownScrollViewer.Markdown`
- [.csproj](HyCADTool.Refactored/HyCADTool.Refactored.csproj): 添加 `<PackageReference Include="MdXaml" Version="1.27.0" />`

暗色主题样式要点（在 XAML 中定义 ResourceDictionary）：

```xml
<mdxam:MarkdownScrollViewer Markdown="{Binding MarkdownText}"
                            MarkdownStyle="{StaticResource DarkMdStyle}" />
```

### 2. XRecord 存储 Markdown 源码（必须实现）

使用已有的 [ExtensionDictionaryService](HyCADTool.Refactored/Infrastructure/AutoCAD/Services/ExtensionDictionaryService.cs) 模式。 由于 Markdown 文本可能数千字符，DxfCode.Text 单条 TypedValue 有 ~2000 字符限制，需要分片存储。

在 ExtensionDictionaryService 中新增长字符串读写方法：

```csharp
// 写：分片为每段 2000 字符的 TypedValue[]
public static void WriteLongString(Transaction tr, Entity entity, string text, string key)

// 读：拼接所有 TypedValue 还原完整字符串
public static string ReadLongString(Transaction tr, Entity entity, string key)
```

DesignSpecService.Insert() 中：创建 MText 后，调用 `WriteLongString(tr, mtext, markdownSource, "HyDesignSpec_MD")` 将原始 Markdown 存入 MText 的扩展字典。

### 3. 去除固定图幅，改为自由尺寸控制

当前逻辑用 A1/A2 图幅预设推算文字区域，用户明确表示不需要。

改为：

- 对话框顶部：`总宽度(mm)` + `总高度(mm)` 两个输入框（模型空间实际值），默认 21000 x 14000
- 保留 `栏数`、`栏间距(mm)`、`字号` 控件
- 栏宽自动计算：`(总宽度 - 栏间距 * (栏数-1)) / 栏数`
- 删除 `PaperSize` 属性和 `AvailablePaperSizes`

具体改动：

- [DesignSpecConfig.cs](HyCADTool.Refactored/Domain/Models/Text/DesignSpecConfig.cs): 删除 `PaperSize`，新增 `TotalWidth`/`TotalHeight`（模型空间 mm）
- [TextAreaCalculator.cs](HyCADTool.Refactored/Domain/Models/Text/TextAreaCalculator.cs): 简化为直接用 `TotalWidth`/`TotalHeight` 计算栏宽，删除 `GetPaperDimensions`
- [MarkdownEditorViewModel.cs](HyCADTool.Refactored/Presentation/ViewModels/MarkdownEditorViewModel.cs): 将 `PaperSize` 替换为 `TotalWidth`/`TotalHeight` 属性
- [MarkdownEditorDialog.xaml](HyCADTool.Refactored/Presentation/Views/MarkdownEditorDialog.xaml): 将图纸 ComboBox 替换为宽度/高度 TextBox

### 4. 二次编辑命令（hymdE）

新增 `DesignSpecEditCommand`：

1. 用户选择已有 MText 实体
2. 读取 XRecord 中存储的 Markdown 源码
3. 打开编辑器对话框（预填充 Markdown 文本 + 当前尺寸配置）
4. 用户编辑后确认 → 更新 MText.Contents + 更新 XRecord
5. 如果所选 MText 没有 XRecord → 提示"此 MText 非 Markdown 生成"

新建文件：`Presentation/Commands/DesignSpecEditCommand.cs`

### 5. 模板系统

在对话框中添加"模板"下拉菜单，提供常用设计说明模板：

- 结构设计说明（通用）
- 基础设计说明
- 钢结构设计说明
- 空白模板

模板存储为内嵌 const string，在 ViewModel 中维护字典，选择后替换 MarkdownText。

### 6. 增强 MText 转换

[MarkdownToMTextRenderer.cs](HyCADTool.Refactored/Domain/Models/Text/MarkdownToMTextRenderer.cs) 增强：

- **表格**：将 Markdown 表格渲染为 MText 格式的等宽对齐文本（用 `\pxi` 制表位对齐），v2 先实现文本模拟表格
- **字号联动**：对话框中可直接调整基础字号，实时反映到预览

------

## 文件变更清单

| 操作     | 文件                          | 说明                                        |
| -------- | ----------------------------- | ------------------------------------------- |
| 修改     | .csproj                       | 添加 MdXaml NuGet                           |
| 修改     | DesignSpecConfig.cs           | 删除 PaperSize，新增 TotalWidth/TotalHeight |
| 修改     | TextAreaCalculator.cs         | 简化为直接用宽高计算                        |
| 修改     | MarkdownToMTextRenderer.cs    | 增加简易表格支持                            |
| 修改     | ExtensionDictionaryService.cs | 新增 WriteLongString/ReadLongString         |
| 修改     | DesignSpecService.cs          | 插入后写 XRecord，新增 Update 方法          |
| **重写** | MarkdownEditorDialog.xaml     | MdXaml 预览 + 暗色主题 + 新布局             |
| **重写** | MarkdownEditorViewModel.cs    | 删除手写渲染，新增模板/宽高/字号属性        |
| 修改     | DesignSpecCommand.cs          | 适配新接口                                  |
| 新建     | DesignSpecEditCommand.cs      | 二次编辑命令                                |
| 修改     | TestCommand.cs                | 指向新命令测试                              |