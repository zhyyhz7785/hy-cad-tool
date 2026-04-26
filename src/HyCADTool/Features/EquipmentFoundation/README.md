# EquipmentFoundation

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**设备基础**：底座轮廓、螺栓数据、轴线编号与显示、轴线表；实体侧通过扩展字典等持久化。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|--------------|
| `EF_*Command`、`EquipmentFoundationService`、`Domain/Entities` | 地脚螺栓平面专用六件套 → `AnchorBolt` |
| XData/字典读写与图面高亮 | 全局图层清单 → `PluginInitializer` + `HyRoadLayers` 等（勿混用键） |

## 目录结构

- `Domain/Entities/`：`BaseData`、`BoltData`、`AxisData`、`EquipmentData` 等
- `Services/EquipmentFoundationService.cs` 等
- 根目录：`EF_ConstructBaseDataCommand`、`EF_HighlightBoltDataCommand`、`EF_Axis*Command` 等

## 命令与入口（`src/ReCall/commands.json`）

| 键 | 说明 |
|----|------|
| `hyef_Base_ConstructBaseData` | 构建基础数据 |
| `hyef_Base_HighlightBoltData` | 高亮螺栓 |
| `hyef_Axis_Construct` | 构建轴线 |
| `hyef_Axis_Initialize` | 初始化轴线 |
| `hyef_Axis_Display` | 显示切换 |
| `hyef_Axis_CreateTable` | 轴线表 |

## 依赖与协作

- **Infrastructure**：`ExtensionDictionaryService` 等（项目内路径以实际引用为准）。
- **Shell**：设置页「设备基础」与 VM 联动。

## 开发与审查要点

- [ ] 扩展字典 GUID/键名变更须迁移策略或版本字段。
- [ ] 与 `AnchorBolt` 数据模型边界清晰，避免同一实体双重语义。
- [ ] Table API：`SetSize` 再写单元格。
