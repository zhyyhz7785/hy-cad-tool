# Shell / Licensing

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**许可门闸与命令档位映射**：在壳层决定某命令/功能是否允许执行（`LicenseGate`），以及命令键到许可层级的表（`LicenseCommandTierMap`），与 `../Activation` 窗口、激活命令类配合。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|----------------|
| 纯策略/映射/查询，供命令入口或 `CommandCatalog` 消费 | 网络激活协议细节、加解密若分散在别处以实际为准 |
| 与业务无关的「能不能跑」 | Feature 内付费增值逻辑（若有）应仍通过门闸，不各写一套 |

## 目录结构

- `LicenseGate.cs`：门闸 API。
- `LicenseCommandTierMap.cs`：命令键 ↔ 许可档位。

## 命令与入口

- 激活/许可类命令在 `../Commands/LicenseActivationCommand.cs` 等；键名以 `src/ReCall/commands.json` 为准。

## 依赖与协作

- **与 `../Activation/`**：未激活时引导用户到激活流。
- **与 `Features` 命令**：执行前可查询 `LicenseGate`（以现有模式为准，勿在 Shell 外复制粘贴整段 license 状态）。

## 开发与审查要点

- [ ] 新收费或受限命令时同步 `LicenseCommandTierMap`（及文档）。
- [ ] 门闸检查失败时用户可见提示，避免无声 `return`。
