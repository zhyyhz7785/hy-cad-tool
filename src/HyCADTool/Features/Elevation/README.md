# Elevation

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**标高** 符号与文字：绘制、更新、旋转、文字批量转标高；含 **3D** 辅助（墙/板等几何）供立面相关流程使用。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|--------------|
| `DrawElevationCommand` 等命令与 Jig | 全局 TextStyle/DimStyle → `Shell/Configuration` |
| `Domain` 3D 与 `Services` 构造 | 纯 2D 通用几何 → `Shared/Geometry` |

## 目录结构

- `Domain/`：值对象、3D 表示、服务接口（如 `IGeospatialService`）
- `Services/`：墙/板生成、表面标高等
- 根目录：`*Command.cs`、可能的 `*Jig*.cs`

## 命令与入口（`src/ReCall/commands.json`）

| 键 | 类 | 面板 category |
|----|-----|---------------|
| `bg` | `DrawElevationCommand` | 标高 |
| `bgu` | `UpdateElevationTextCommand` | 标高 |
| `bgR` | `RotateElevationCommand` | 标高 |
| `hybgTCE` | `TextsCreateElevationCommand` | 标高 |
| `HY3` | `Elevation3DCommand` | 杂项（历史归类） |

## 依赖与协作

- **Shell**：`Shell.Configuration.User`（多命令 `using`）。
- **Shared**：几何、转换器。

## 开发与审查要点

- [ ] 3D 与标高符号图层、比例与 `SettingsPanelViewModel` 一致。
- [ ] 多文档：每次取当前 `Document`。
- [ ] WPF 无关标高命令路径勿引入面板资源全局 merge。
