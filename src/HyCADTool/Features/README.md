# Features（按功能切片）

## 约定

- **一个功能 = 本目录下的一个子文件夹**，该文件夹内放此功能相关的模型、服务、命令、视图、ViewModel（自包含）。
- **Shell 边界**：`Shell/` 只放全插件共用外壳，包括主面板、命令检索、设置总壳、Ribbon/CUIX、`commands.json` 读取/分发、PaletteSet 宿主、全局主题资源和全局配置模型。业务专属面板、窗口、ViewModel 不放 `Shell/`。
- **Feature 内部目录**：业务专属 UI 统一放 `Views/`，业务专属 ViewModel 统一放 `ViewModels/`，命令入口放 `Commands/`，领域模型/算法放 `Domain/`，业务或绘图服务放 `Services/`。
- **namespace**：`HyCADTool.Features.{文件夹名}`（如 `HyCADTool.Features.Settlement`）。Road 等超大功能可用子命名空间 `HyCADTool.Features.Road.PlanAlignment`。当文件进入 `Views/` / `ViewModels/` 时，命名空间也跟随目录，例如 `HyCADTool.Features.BaseRein.Views`。
- **README.md**：每个 Feature 根目录一份稳定档案卡（命令、面板、算法、依赖）。
- **Doc/**：每个 Feature 下的 `Doc/序号-标题-YYYY-MM-DD-HHMMSS.md` 记录修改历史；跨多个 Feature 的全局文档仍放在仓库顶层 `doc/`。

## Shared 何时抽离

仅当某代码 **≥3 个 Feature 使用**、接口稳定、且确为平台基础能力时，才迁入 `HyCADTool/Shared/`。宁可重复，避免过早抽象。

## 当前边界决策

- `Shell/Preferences/` 暂保留为设置总壳；其中各设置页虽然按功能分组展示，但统一依赖 `HySettingsViewModel` / `SettingsPanelViewModel` 作为全局设置聚合入口。
- `DesignSpec` 的 Markdown 编辑器与 `EditorLoader` 属于设计说明业务能力，已下沉到 `Features/DesignSpec/Views`、`Features/DesignSpec/ViewModels`、`Features/DesignSpec/Services`。
- `BaseRein`、`Settlement` 等业务面板统一采用 `Views/` + `ViewModels/`；命令入口进入 `Commands/`。
