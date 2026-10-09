# DesignSpec

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**设计说明 / Markdown 表格** 领域：解析、列宽、HTML 模板、转 MText 排版；与进程外 HyMd（命名管道 `HyMd.Cad.v1`）对接贴图。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|--------------|
| `DesignSpecService`、`Domain/Models/*`、`Views/MarkdownEditorDialog` | **可执行命令类** → 主要在 `../Export/DesignSpecCommand`、`DesignSpecEditCommand` |
| Markdown→图面排版核心 | 纯文本布局算法共享 → `HyCADTool.TextLayout` 程序集 |

## 目录结构

- `Domain/Models/`：模板、渲染器、宽度计算等
- `Services/DesignSpecService.cs`：编排
- `Views/`：WPF 对话框

## 命令与入口

用户可输入命令由 **`Export`** 注册（`commands.json`）：

| 键 | 类（Export 目录） |
|----|-------------------|
| `hymd` | `DesignSpecCommand` |
| `hymdE` | `DesignSpecEditCommand` |

本目录提供能力；**键与 Facade 以 Export README 为准**。

## 依赖与协作

- **Export**：命令层与 `commands.json` 映射。
- **Shell**：`Shell/Configuration/User` 等用户路径或编辑器配置。
- **HyMd**：进程外编辑器；`HyMdCadBridge` 连接管道，`InsertFromHyMdDump` 按导出几何插入。

## 开发与审查要点

- [ ] 改命令签名或类名时同步 `commands.json` + `Export/README` + 此处「命令与入口」表。
- [ ] WebView2 / Markdig 版本升级可能需关 CAD 重载（见 `doc/00` §八）。
- [ ] 不在 Domain 引用 `Autodesk.AutoCAD.*`。
