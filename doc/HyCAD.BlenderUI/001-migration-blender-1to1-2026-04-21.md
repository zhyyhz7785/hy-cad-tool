# HyCAD.BlenderUI 1:1 迁移说明（2026-04-21）

## 三条等价线

- **视觉等价**：`Brush_Shadetop_*` / `Brush_Shadedown_*` + `BlenderWidgetDraw` / `WidgetBackdrop.xaml`
- **语义等价**：`Controls/Widgets/*` 与 `UI_BTYPE` 映射见同目录 `001-uiBType-mapping-2026-04-21.md`
- **架构等价**：`Layout/*`、`Screen/Areas/*`、`WM/*`

## PaletteSet 宿主

- 禁止 `AllowsTransparency`（Popover/PanelHeader 已改为 `False`）
- 无 D3DImage；全命名 `Style`；`DynamicResource` 跨字典；Brush Facade 换肤

## TestHost

运行 `HyCAD.BlenderUI.TestHost` 双击列表打开各 Sample。

## PaletteSet 插件示例

- 独立样本工程：`HyCAD.BlenderUI.PaletteSetDemo`（命令 `BLENDERUI_DEMO`），与 `HyCAD.BlenderUI.dll` 一并 `NETLOAD`。
- 生产环境亦可参考 `HyCADTool.Refactored` 的 `PanelManager`：在引用 `AcDbMgd` / `AcMgd` 的插件中 `PaletteSet` 内嵌 `UserControl`，并 Merge `BlenderTheme.xaml`，遵守 pitfall B1–B11。
