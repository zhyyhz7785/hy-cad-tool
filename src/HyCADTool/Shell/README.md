# Shell

> **文档深度**: L3 索引层 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../Features/README.md`](../Features/README.md)

**客户端壳**：主 Blender 侧栏/筛选/首选项、许可激活窗、键位与调度、全局资源字典、Ribbon/CUI 构建。命令基础设施（`CommandDispatcher`、`CommandCatalog` 等）在此，经 `SendStringToExecute` 与 `commands.json` 路由到 `Features` 内具体命令；Markdown 编辑器由 `Features/Export` 等处的 `EditorLoader` 调起，**不在**本目录。

各子目录 **L3** 说明见下表；子目录内另有 `README.md`。

| 子目录 | 一句话 |
|--------|--------|
| [`Activation/`](./Activation/README.md) | 许可/激活 WPF 窗口 |
| [`Behaviors/`](./Behaviors/README.md) | 壳层附加行为（如 Outliner 拖放） |
| [`BlenderPanel/`](./BlenderPanel/README.md) | HyBlender 侧栏、筛选、主 VM、PanelManager |
| [`Commands/`](./Commands/README.md) | 统一分发、面板入口、调试用命令 |
| [`Configuration/`](./Configuration/README.md) | 全局/模块/用户配置模型 |
| [`Contracts/`](./Contracts/README.md) | DI 与面板用的接口契约 |
| [`Input/`](./Input/README.md) | 键位表、操作符注册与调度 |
| [`Licensing/`](./Licensing/README.md) | 许可门闸与命令档位映射 |
| [`Preferences/`](./Preferences/README.md) | 设置面板、子 Tab、VM、RelayCommand |
| [`Resources/`](./Resources/README.md) | 壳层 WPF 资源字典（主题等） |
| [`Ribbon/`](./Ribbon/README.md) | Ribbon/CUI 构建与命令处理 |
| [`Services/`](./Services/README.md) | 壳层纯服务（如命令搜索） |

---

- **不**放按业务域切片的命令/算法（在 `Features/`）或真通用 AutoCAD/几何（在 `Shared/`）。

---

## 与 `Features` / `Shared` / `App` 的边界（两条轴）

| 轴 | 含义 |
|----|------|
| **业务切片轴** | 钢筋、道路、桩基等 → 整包在 `Features/<切片>/` |
| **职责纵轴** | Domain / Services / ViewModels / Views / Commands → 都在各自切片内 |

`Shell` 是**与业务无关的机箱**：唯一 `HyBlenderPanel` PaletteSet 框架、Ribbon、配置 schema（`Configuration/`）、契约（`Contracts/`）、许可、命令目录（`CommandCatalog`）、统一分发（`CommandDispatcher`）。**业务子面板**的 ViewModel 仍住在对应 `Features` 目录，只是被 Shell 嵌进「设置」等 Tab。

---

## 放新文件：5 秒决策（Shell 侧）

1. 若**不知道**会被哪个 Feature 专用 → 可能应进 `Shared/` 或留在 `Shell`（机箱）。
2. 若**明确**只属于某一业务（如桩基实体、道路线位）→ **不应**进 `Shell`，应进 `Features/<切片>/`。
3. 全局 JSON/用户设置**结构类型**若引用 `Features.*.Domain` → **坏味道**：配置应靠近该 Feature 或抽到中立模型。

---

## PR Review Checklist（改 Shell 时逐项勾）

- [ ] **无业务命令**：未把 `Execute`、Jig、业务算法塞进 `Shell`（应在 `Features`）。
- [ ] **无业务 Domain**：`Shell/Configuration`、`Shell/Contracts` 未新增对 `Features.*.Domain` 的依赖（现有历史问题见下节「已知耦合」）。
- [ ] **主题与资源**：WPF 资源合并仍按项目约定（局部 `MergedDictionaries`，不把业务主题挂到 `Application.Current.Resources`）；AdWindows/Badge 类问题先查 ReCall 黑名单与 bin 宿主 DLL，勿加 `ResourceAssembly`/Badge 预热（见 `.cursor/rules/05-AdWindows-WPF-PaletteSet宿主.mdc`）。
- [ ] **命令分发**：面板/Ribbon 调业务逻辑优先 `CommandDispatcher.Send(key)`（与 `commands.json` 一致），避免在 Shell 里手写反射调 Feature 类型。
- [ ] **PanelManager**：新增 PaletteSet 时评估是否应归属某 `Features/<切片>/Hosting`，避免 `PanelManager` 无限膨胀、直接 `new` 各 Feature 视图。
- [ ] **HySettingsViewModel**：新增「设置」子页若强依赖某 Feature VM，在 review 中注明；长期应用 `ISettingsTabContribution` 类插件点收敛（规划中）。

---

## 已知耦合（历史债，新代码勿加码）

| 位置 | 问题 | 期望方向 |
|------|------|----------|
| `Configuration/Modules/PileConfiguration.cs` | 引用 `Features.Pile.Domain.Entities` | 配置模型挪到 `Features/Pile` 或中立层 |
| `Preferences/HySettingsViewModel.cs` | 直接持有 `BaseRein` / `Pile` / `Cluster` 的 VM | 插件式设置 Tab |
| `BlenderPanel/PanelManager.cs` | 直接创建道路多个 Feature 面板 PaletteSet | 按模块拆 Hosting |
| `Features` 引用 `Shell.Commands.CommandDispatcher` | Feature → Shell | 可接受为「机箱公共服务」；或日后迁到 `Shared` |

---

## 整理优先级（供排期）

1. 禁止新增 `Shell → Features.Domain` 引用。
2. 搬迁 `PileConfiguration` 与 Pile 实体的耦合。
3. `PanelManager` 与道路专用 PaletteSet 解耦。
4. 设置 Tab 的 Feature VM 收敛为接口 + DI 发现。

更细的 WPF/PaletteSet 陷阱见 `.cursor/skills/hycad-project-pitfalls/SKILL.md` 与 `wpf-paletteset-resource-pitfalls`。
