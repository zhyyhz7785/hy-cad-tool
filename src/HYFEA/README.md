# HYFEA（Hy Finite Element Analysis）

与 `HyCADTool` **同仓库**、**独立项目**的线弹性有限元内核：**禁止**引用 AutoCAD、WPF、ReCall、HyCADTool 业务代码。参见 [005-HYFEA第一步](../../docs/FiniteElement/005-HYFEA第一步-内核独立与HyCADTool适配-目录结构模块清单与个人评估流程-2026-05-17-105500.md)。

## 项目

| 项目 | 目标框架 | 说明 |
|------|----------|------|
| `HyCAD.Geometry`（仓库 `src/HyCAD.Geometry/`） | netstandard2.0 | **共用**平面/向量/多边形等几何；`HYFEA.Core` 与 `HyCADTool` 均 `ProjectReference`，命名空间 `HyCAD.Geometry.*` |
| `HYFEA.Core` | netstandard2.0 | IR、DOF、装配、稠密求解、线弹性 Spring1D / Truss2D、`GoldenCaseRunner` |
| `HYFEA.Tests` | net48 | xunit + FluentAssertions，G01–G04 黄金算例 |
| `HYFEA.Examples` | net48 | 控制台演示桁架（见 `Program.cs`） |

## 生成与测试

```powershell
dotnet build src\HYFEA\HYFEA.Core\HYFEA.Core.csproj -c Debug
dotnet test src\HYFEA\HYFEA.Tests\HYFEA.Tests.csproj -c Debug
dotnet run --project src\HYFEA\HYFEA.Examples\HYFEA.Examples.csproj -c Debug
```

也可在 Visual Studio 中打开根目录 `HyCADtoolGpt.sln`，已包含上述工程及 **`HyCAD.Geometry`**。

## P0.v0.1 范围

- 线静力：`LinearStaticAnalysis` → `FemResult`（位移、约束反力、杆轴力）。
- 单元：Spring1D（仅 UX 自由度链）、Truss2D（UX/UY）。
- 求解：`DenseLinearSolver`（稠密矩阵、列主元高斯消元）。

## 算例说明（G03）

仅两根斜杆、无底杆的「开 V」在平面桁架中为**机构**；黄金算例 **G03** 采用 **底弦 + 两斜杆** 的闭合三角形，与经典静定桁架一致。

## 后续（未实现）

- Euler 梁、平面实体单元、稀疏求解器（如 CSparse.NET）、外部求解器子进程等见总纲 `docs/FiniteElement/005-HYFEA第一步-*.md`。
