# Shell / Input

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**键位与操作符管道**：`HyKeyMap`、配置文件加载（`KeyMapConfigLoader`）、`DispatchOperator` / `OperatorBootstrapper` 将键盘/短按映射到可调度操作，与 Blender 式交互对齐。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|----------------|
| 映射表、加载、分发给**壳层**已知的操作 | 具体 AutoCAD 命令的 `Jig` 实现 |
| 与 WPF/宿主焦点协作的桥接点 | 业务上的「快捷键」常量在 Feature 内定义（若与 UI 表无关） |

## 目录结构

- `HyKeyMap.cs`：键位逻辑核心。
- `KeyMapConfigLoader.cs`：从资源或磁盘加载键位表。
- `DispatchOperator.cs`：将输入事件转为操作/命令键。
- `OperatorBootstrapper.cs`：安装或注册操作符（启动阶段）。

## 命令与入口

无独立 `commands.json` 键；与首选项中「键位」Tab、运行时按键监听联动。

## 依赖与协作

- **与 `../Commands/CommandDispatcher`**：最终可能以命令字符串形式进 AutoCAD。
- **与 `../Preferences/KeyMapSettingsViewModel.cs`**：设置侧编辑与这里加载的源一致。

## 开发与审查要点

- [ ] 键位与 `commands.json` 的 key 保持可文档化对应关系，避免二义性。
- [ ] 热重载/多文档切换时，事件订阅在 `IDisposable` 或等效中释放，避免双注册。
