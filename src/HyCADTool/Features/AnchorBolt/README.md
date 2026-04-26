# AnchorBolt

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**地脚螺栓**：平面绘制、对齐、计算、显隐切换、材料表、矩形剖面等，约 6 条对外命令。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|--------------|
| 螺栓图元与表格、剖面流程 | 设备基础「底座/轴线」数据流 → `EquipmentFoundation` |
| 命令类与 AutoCAD 交互 | 纯 Domain 实体若全局复用 → 项目 `Domain` 或 `Shared`（以实际命名空间为准） |

## 目录结构

- 根目录：各 `*Command.cs`（`AnchorBoltCommand`、`AnchorBoltAlignCommand`、`AnchorBoltCalcCommand`、`AnchorBoltToggleDisplayCommand`、`AnchorBoltTableCommand`、`AnchorBoltSectionCommand`）

## 命令与入口（`src/ReCall/commands.json`）

| 键 | 说明 |
|----|------|
| `hyab` | 绘制 |
| `hyabA` | 对齐 |
| `hyabC` | 计算 |
| `hyabCD` | 切换显示 |
| `hyabCT` | 螺栓表 |
| `hyabR` | 矩形剖面 |

## 依赖与协作

- **Shell**：`Shell/Preferences` 中地脚螺栓设置页与 `SettingsPanelViewModel` 字段联动（若有）。
- **Shared**：图层、扩展字典、样式服务接口等。

## 开发与审查要点

- [ ] 与 `EquipmentFoundation` 的螺栓数据区分：审查 XData/扩展字典键是否混用。
- [ ] 表格创建遵循 Table API：`SetSize` 再写单元格（pitfalls 文档）。
- [ ] 命令仅经 `commands.json` 注册，勿在主工程业务类上加 `[CommandMethod]`。
