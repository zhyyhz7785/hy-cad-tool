# Export

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**导出与捕集**：实体属性 → CSV、Markdown 表导出、当前图线型目录写入 JSON/LIN；并承载 **设计说明** 的**命令入口**（实现委托 `DesignSpec`）。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|--------------|
| `ExportEntityPropertiesToCsvCommand`、`ExportMarkdownTableCommand`、`CaptureDrawingLinetypesCommand` | Markdown 解析与对话框 UI → `../DesignSpec` |
| `DesignSpecCommand` / `DesignSpecEditCommand`（命令壳） | 全局 `GlobalConfiguration` 字段定义 → `Shell/Configuration` |

## 目录结构

- 根目录：上述命令类 + `Services/`（如 `LinetypeTableExporter`）

## 命令与入口（`src/ReCall/commands.json`）

| 键 | 类 |
|----|-----|
| `hymd` | `DesignSpecCommand` |
| `hymdE` | `DesignSpecEditCommand` |
| `hyex` | `ExportMarkdownTableCommand` |
| `hyex_csv` | `ExportEntityPropertiesToCsvCommand` |
| `hyLtCapture` | `CaptureDrawingLinetypesCommand` |

Blender 分类：`导出说明`。

## 依赖与协作

- **DesignSpec**：`DesignSpecService`、编辑器视图。
- **Shell**：`Shell.Configuration.Global`（线型目录等）。

## 开发与审查要点

- [ ] 改 `DesignSpec*` 类名须同步 `commands.json` + `DesignSpec/README.md`。
- [ ] 写 `hy-settings.json` 时遵守 settings 文档约定（`docs/settings/`）。
- [ ] OpenXml/文件 IO 异常须捕获并向 `Editor` 简短输出。
