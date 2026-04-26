# Shell / Preferences

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**「设置」大面板**：`SettingsPanel` / `HyPreferencesView`、主导航 VM（`SettingsPanelViewModel`、`HySettingsViewModel`）、键位/命令分区 VM（`KeyMapSettingsViewModel`、`CommandSectionVm`），以及可复用 `RelayCommand`。**各子 Tab 的 XAML 视图** 放在嵌套目录 [`Preferences/`](./Preferences/README.md)（与历史命名同名的子文件夹）。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|----------------|
| 壳级设置 UI 结构、与 `IConfigurationService` 的绑定 | 业务面板主体（如道路工作台）→ `Features` 内 Views |
| 对多 Feature VM 的**聚合**（历史耦合；新 Tab 应评估插件点） | 业务算法与图面写入 |

## 目录结构

- `SettingsPanelViewModel.cs`、 `HySettingsViewModel.cs`：主设置状态与子 Tab 切换。
- `HyPreferencesView.xaml`、根级设置容器。
- `KeyMapSettingsViewModel.cs`、`CommandSectionVm.cs`：键位与命令列表区。
- `RelayCommand.cs`：WPF/VM 用轻量 ICommand 实现。
- [`Preferences/`](./Preferences/README.md)：**各子分类**的 `*SettingsView.xaml(.cs)`（道路、配筋、桩、主题、尺寸等）。

## 命令与入口

- 打开方式：`ShowPanelCommand.ShowSettingsPanel()` 或 `Hy` 进设置 Tab；键名见主 Shell README 与 `commands.json`。
- 与 **旧** `ReinPanel.ActivePanel` 的替代关系：热启动规则要求改用 `SettingsPanelViewModel.Current` 等（见 `.cursor/rules/01`）。

## 依赖与协作

- **与 `../Configuration/`**：读写 `Global`/`Modules`/`User` 模型。
- **已知耦合**：`HySettingsViewModel` 直接持有多个 Feature VM，长期目标插件式 Tab；见主 `Shell/README.md`。

## 开发与审查要点

- [ ] 新子 Tab 优先独立 VM + 弱耦合接口，避免再增大 `HySettingsViewModel`。
- [ ] 绑定路径与 JSON 键一致，且局部主题合并不挂到 `Application.Current.Resources`（WPF 规则 §5）。
