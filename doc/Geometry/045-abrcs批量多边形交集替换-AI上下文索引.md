# 045 · abrcs 批量多边形交集替换 · AI 上下文索引

> **代号 `045`**：在对话里写「045」「rcs」或 `@` 本文件，即表示需要按 **`abrc` / `abrcs`**（Clipper2 闭合多段线交集替换）的**文档树 + 代码锚点**对齐后再回答或改代码。  
> **速记**：`rcs` → 命令名 **`abrcs`**（末尾三字）。  
> 完整路径：`doc/Geometry/045-abrcs批量多边形交集替换-AI上下文索引.md`

---

## 一、必读总纲

| 优先级 | 文档 | 用途 |
|:------:|------|------|
| P0 | [../rules/03-HyCADTool 命令详细清单.md](../rules/03-HyCADTool%20命令详细清单.md) | **`abrc` / `abrcs`** 条目、Clipper2 依赖说明 |
| P0 | [../../ReCall/Doc/Recall-工作原理.md](../../ReCall/Doc/Recall-工作原理.md) | `CommandFacade` → `commands.json` → Refactored 反射调度 |
| P1 | [../05-重构完整报告-2026-02-11-220000.md](../05-重构完整报告-2026-02-11-220000.md) | §2.8 几何处理：`ReplacePolygonCommand` / `ReplacePolygonBatchCommand` 对照旧命令 |

## 二、领域与对标

| 主题 | 文档 |
|------|------|
| 用户侧命令说明（多边形替换小节） | [../06-HyCADTool命令使用说明-2026-02-11-230000.md](../06-HyCADTool命令使用说明-2026-02-11-230000.md) |

## 三、实施快照与工程笔记

| 编号 | 文档 | 用途 |
|------|------|------|
| — | [../04-重构进度追踪-2026-02-11-200000.md](../04-重构进度追踪-2026-02-11-200000.md) | `abrcs` 在进度表中的定位 |
| — | [../040-Refactored未迁移命令盘点-2026-03-08-233050.md](../040-Refactored未迁移命令盘点-2026-03-08-233050.md) | `abrcs` 迁移状态勾选 |

## 四、个人工作备忘（非正式规格）

（本主题暂无独立 `00-*.md`；以第一节正式文档与代码为准。）

## 五、源码索引

以下路径相对**仓库根目录**。**业务实现**在 `HyCADTool.Refactored`；**命令名注册**在 `ReCall`（需 NETLOAD 的 DLL 与 `commands.json`）。

### 5.1 命令实现（Refactored · `Presentation/Commands/`）

| 文件 | 说明 |
|------|------|
| `HyCADTool.Refactored/Presentation/Commands/ReplacePolygonCommand.cs` | **`abrc`**：两选闭合多段线；`ReplaceWithIntersection` + Clipper2 `Path64` 交集；面积阈值 70% 时用参考多段线整形替换 |
| `HyCADTool.Refactored/Presentation/Commands/ReplacePolygonBatchCommand.cs` | **`abrcs`**：两样本定图层 → 框选批量；按图层分组、形心在对方多边形内匹配最近邻 → 逐对调用 `ReplacePolygonCommand.ReplaceWithIntersection` |

### 5.2 ReCall 命令映射（宿主 DLL）

| 文件 | 说明 |
|------|------|
| `ReCall/commands.json` | 键 **`abrc`** / **`abrcs`** → 上表两类的 `type` / `method`（`Execute`） |
| `ReCall/CommandFacade.cs` | `[CommandMethod("abrc")]` / `[CommandMethod("abrcs")]` → `ReCallClass.Invoke` |
| `ReCall/Recall.cs` | `Invoke(key)`：加载 Refactored 后按 `CommandTable` 反射执行（改映射一般**不需**改此文件） |

### 5.3 相关基础设施（Clipper2 多边形布尔 · 与 abrc 路径独立）

| 文件 | 说明 |
|------|------|
| `HyCADTool.Refactored/Infrastructure/AutoCAD/Services/AutoCadGeometryService.cs` | `IGeometryService`：Union / Difference / **Intersection** / Offset（`PathD` + Clipper2）；**当前 `abrc`/`abrcs` 未走此类**，属同栈几何能力 |

## 六、项目级避坑

- **hycad-project-pitfalls**：ReCall `commands.json` 与 C2 热重载、Refactored 未加载时 `Invoke` 提示等宿主级行为。
- **hycad-refactored-migration-patterns**：新增/调整命令时与 `commands.json`、占位键、DI 注册的一致性。

## 七、维护说明

- **文档**：若补充专题设计说明，优先写入 `doc/Geometry/` 或 `doc/rules/`，并在本节 **二、三** 增行链回。
- **源码**：修改 `ReplacePolygon*` 或 ReCall 映射时，同步更新 **第五节**；若新增测试项目，在 **5.1** 补表。
- **命令表**：`ReCall/commands.json` 中 `abrc` / `abrcs` 的 `type` 必须与 Refactored 全名一致。
