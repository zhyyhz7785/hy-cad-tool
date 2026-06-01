# Shared（真共用代码）

迁入前请确认三点同时满足：

1. **≥3 个 Feature** 实际引用（grep 计数）。
2. **接口稳定**（近期不会大改签名）。
3. **平台无关** 或 **AutoCAD/WPF 通用基础设施**。

子目录约定：`AutoCAD/`、`Persistence/`、`Bootstrap/` 等；namespace 形如 `HyCADTool.Shared.AutoCAD`。  
**通用平面几何**已迁至独立程序集 **`HyCAD.Geometry`**（仓库 `src/HyCAD.Geometry/`，命名空间 `HyCAD.Geometry.*`），HyCADTool 通过 `ProjectReference` 引用，不再放在 `Shared/` 下。
