# Shared/UI

全插件共用的 WPF 原语（与业务特性无关）：

- `Converters/` — `IValueConverter` 实现，供 Shell、Features 下 XAML 通过 `xmlns` 引用。
- `Helpers/` — 如 `TextBoxHelper` 等 UI 辅助类。

命名空间：`HyCADTool.Shared.UI.Converters` / `HyCADTool.Shared.UI.Helpers`。
