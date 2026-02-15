# HyCADTool.MarkdownEditor 目录结构说明

独立 WPF 库，提供 Markdown 所见即所得编辑器与分栏预览，供主项目通过 `EditorLauncher.ShowDialog` 反射调用。输出到 `HyCADTool.Refactored/bin/$(Configuration)/net8/`，与 ReCall 热重载配合使用。

---

## 根目录

| 文件 | 说明 |
|------|------|
| `HyCADTool.MarkdownEditor.csproj` | 项目文件。目标 `net8.0-windows`，WPF；依赖 WebView2、Markdig、Newtonsoft.Json；`RegressionSamples/**` 复制到输出目录；构建后复制 `WebView2Loader.dll` 到输出根目录。 |
| `EditorLauncher.cs` | **对外唯一入口**。静态方法 `ShowDialog(string inputJson, long ownerHandle)`：反序列化 `EditorInput`、创建 `EditorWindow`、可选设置父窗口、ShowDialog 后返回 `EditorResult` 的 JSON。 |

---

## Models/

编辑器与主项目之间的数据契约（JSON 序列化）。

| 文件 | 说明 |
|------|------|
| `EditorContract.cs` | **EditorInput**：初始 Markdown、EditorConfig、当前文件路径。<br>**EditorResult**：Confirmed、Markdown、Config、CharsPerColumn、ColumnParagraphIndices、PreviewStats。<br>**EditorConfig**：出图/预览比例、栏数/栏距/每栏字数/总高、字体（SHX/大字体/粗体）、标题倍率、缩进、段前段后间距等。<br>**PreviewStats**：SchemaVersion、CharsPerColumn、ColumnParagraphIndices、Columns、BlockTypeCounts。<br>**PreviewColumnStats**：单栏字数/段数/总字数/显示单位等。 |

---

## ViewModels/

| 文件 | 说明 |
|------|------|
| `EditorViewModel.cs` | 编辑器 ViewModel（INotifyPropertyChanged）。管理 Markdown 文本、当前文件路径、EditorConfig 绑定、工具栏命令；暴露 `EditorContentLoadRequested` 等事件；与 Vditor（SetMarkdownFromEditor/GetContent）、预览（HTML 注入、统计回传）协作。纯 WPF，不依赖 AutoCAD。 |

---

## Views/

### 主窗口

| 文件 | 说明 |
|------|------|
| `EditorWindow.xaml` | 主窗口：无边框、主题色资源、工具栏、编辑区（WebView2 Vditor）、预览区（WebView2 分栏 HTML）、状态栏、标尺等。 |
| `EditorWindow.xaml.cs` | 窗口逻辑：接收 EditorInput、构造 EditorViewModel、WebView2 初始化、Vditor 与预览的导航/消息、关闭时产出 EditorResult。 |

### Views/Controls/

| 文件 | 说明 |
|------|------|
| `RulerControl.cs` | 毫米标尺控件。水平/垂直、PixelsPerMm、SegmentCount；主题色通过依赖属性注入；20mm 大刻度、4mm 小刻度。 |

### Views/Helpers/

| 文件 | 说明 |
|------|------|
| `TextBoxHelper.cs` | TextBox 附加行为：全选、回车确认、滚轮/上下键步进（ScrollStep），用于数值输入等。 |

---

## Html/

Markdown 编辑（Vditor）与预览（分栏 HTML）的生成与桥接。

| 文件 | 说明 |
|------|------|
| `VditorHtmlTemplate.cs` | 生成嵌入 Vditor 的 HTML 页面（IR 即时渲染、暗色主题）。支持本地缓存或 CDN 的 `cdnBase`；对初始 Markdown 做 JS 转义后注入。 |
| `PreviewHtmlRenderer.cs` | Markdown → 分栏预览 HTML。用 Markdig 转 HTML，再包一层交互式分栏布局（viewport、paper、拖拽调整、footer 统计）；动态 CSS/JS 根据 EditorConfig 与 previewScale 生成。 |
| `VditorCacheManager.cs` | Vditor 本地缓存。缓存目录 `%LocalAppData%/HyCADTool/VditorDist/{VERSION}/dist/`；未缓存时从 npmmirror/npm 下载 tgz 并解压；CDN 回退为 jsdelivr。 |
| `VditorJsHelper.cs` | C# → Vditor 的 JS 调用封装（通过 WebView2 ExecuteScriptAsync）：撤销/重做、焦点、剪贴板、GetContent/SetContent、插入标题/段落/表格等。 |

---

## RegressionSamples/

预览与统计的回归样本集；构建时整体复制到输出目录。

| 文件 | 说明 |
|------|------|
| `manifest.json` | 用例列表与说明：id、file、focus、quickChecklist（如 A2 横向、1/2/3 栏、TextSize/TextXScale、DPI 等验证项）。 |
| `01-empty.md` | 空文档：空栏位、统计稳定性。 |
| `02-all-cn.md` | 全中文：字/行、显示宽度。 |
| `03-mixed-extreme.md` | 中英混排极端比例、宽度口径一致。 |
| `04-code-block.md` | 代码块：统计边界、段落拆分。 |
| `05-table-heavy.md` | 表格：blockTypes 等统计。 |
| `06-long-doc.md` | 长文档分栏、拖拽后统计稳定性。 |
| `07-font-consistency.md` | 字体一致性（若存在）。 |
| `08-special-symbols.md` | 特殊符号（若存在）。 |
| `09-mixed-code-table.md` | 混合代码与表格（若存在）。 |

---

## 依赖与构建

- **NuGet**：Microsoft.Web.WebView2、Markdig、Newtonsoft.Json。
- **输出**：`../HyCADTool.Refactored/bin/$(Configuration)/net8/`，CopyLocalLockFileAssemblies + WebView2Loader 拷贝到输出根目录。
- **调用方式**：主项目反射调用 `HyCADTool.MarkdownEditor.EditorLauncher.ShowDialog(inputJson, ownerHandle)`，传入/返回 JSON。

---

*文档生成自项目源码与目录扫描。*
