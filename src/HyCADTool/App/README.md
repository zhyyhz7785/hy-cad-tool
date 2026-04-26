# App

插件**开机与交付**：WPF/Autofac 启动、`PluginInitializer`、配置加载、`Production` 入口、测试辅助（`TestCommand`）及随 DLL 部署的 `_libraries/` 与 `Resources/`。

- **不**放业务命令实现（在 `Features/`）或统一面板壳（在 `Shell/`）。
