# HyCAD.BlenderUI — Blender 1:1 迁移与宿主说明

> 详细变更记录见 `001-migration-blender-1to1-2026-04-21.md`；控件与 `UI_BTYPE` 映射见 `001-uiBType-mapping-2026-04-21.md`。

## 三条等价线

- **视觉等价**：`Brush_Shadetop_*` / `Brush_Shadedown_*` + `BlenderWidgetDraw` / `WidgetBackdrop.xaml`
- **语义等价**：`Controls/Widgets/*` 与 `UI_BTYPE` 映射
- **架构等价**：`Layout/*`、`Screen/Areas/*`、`Controls/Spaces/*`、`WM/*`

## PaletteSet 宿主（B1 / B5 / B6 / B7 / B11）

- 禁止对 Popup 使用 `AllowsTransparency`（Popover / PanelHeader 等已改为 `False`）
- 无 `D3DImage` 自绘；全**命名** `Style`（无隐式 `TargetType`）；跨字典用 `DynamicResource`；Brush Facade 换肤

## 独立 WPF 回归

运行 `HyCAD.BlenderUI.TestHost`（.exe），从列表打开各 Sample（含 `SpaceTypeShowcaseSample`）。

## AutoCAD 插件示例

- 工程：`HyCAD.BlenderUI.PaletteSetDemo`（`net48`，命令 `BLENDERUI_DEMO`，内嵌 `BlenderArea` + `Properties` 壳）
- 构建后将 `HyCAD.BlenderUI.PaletteSetDemo.dll` 与 `HyCAD.BlenderUI.dll` 置于 AutoCAD 可解析路径，`NETLOAD` 后执行 `BLENDERUI_DEMO`
- 生产环境亦可参考 `HyCADTool.Refactored` 中 `PanelManager` + `PaletteSet` 接入方式
