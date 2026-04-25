# Shared（真共用代码）

迁入前请确认三点同时满足：

1. **≥3 个 Feature** 实际引用（grep 计数）。
2. **接口稳定**（近期不会大改签名）。
3. **平台无关** 或 **AutoCAD/WPF 通用基础设施**。

子目录约定：`AutoCAD/`、`Geometry/`、`Persistence/`、`Bootstrap/` 等；namespace 形如 `HyCADTool.Shared.AutoCAD`。
