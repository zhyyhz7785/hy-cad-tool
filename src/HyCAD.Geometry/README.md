# HyCAD.Geometry

平台无关几何库（**netstandard2.0**），供 `HyCADTool`、`HYFEA.Core` 等共用。

- **命名空间**：`HyCAD.Geometry` 及 `Algorithms` / `Interfaces` / `Math` / `Offset` / `Grid`
- **边界**：不得引用 AutoCAD、WPF、`HyCADTool.*` 业务 Feature；依赖 Clipper2、Newtonsoft.Json（见 `.csproj`）
- **沿革**：由原 `HyCADTool/Shared/Geometry/` 抽取（调研文档 §十二）

架构说明：[01 调研 §十二](../../../docs/FiniteElement/Plan/01-算法模块划分调研-HYFEA内核与适配集成边界-2026-05-17-152100.md)、§12.7。
