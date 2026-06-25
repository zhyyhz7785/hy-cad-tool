# HyCAD.Tables Domain 代码完成情况（007）

> 文档编号：007  
> 生成日期：2026-06-25  
> 上游计划：[006 复杂表格 Domain 分阶段实施计划](./006-复杂表格Domain分阶段实施计划-2026-06-24-235651.md)  
> 设计规格：[005 复杂表格 Domain 统一设计](./005-复杂表格Domain统一设计-2026-06-24-232335.md)  
> 性质：**现状快照**——对照 006 的 P1–P9 验收线，说明代码已落地内容与剩余缺口。  
> 代码位置：`src/HyCAD.Tables`（测试 `src/HyCAD.Tables.Tests`）

---

## 1. 结论（一句话）

**006 定义的 Domain 主线（P1–P8）已全部落地并通过单测；P9 公式能力超出原计划最小集，已实现 Excel 风格子集并完成稳定性加固。**  
程序集已加入 `HyCADtoolGpt.sln`，**尚未**接入 `HyCADTool` 主工程与 AutoCAD Adapter。

| 里程碑 | 006 定义 | 当前状态 |
|---|---|---|
| **M-α 模型可用** | P1 + P2 | ✅ 完成 |
| **M-β 可编辑可逆** | P3 + P4 + P5 + P6 | ✅ 完成 |
| **M-γ 可持久化 + 验收** | P7 + P8 | ✅ 完成 |
| **P9 公式（可选）** | SUM/四则/引用 | ✅ 超额完成（见 §4） |

**单测**：114 项，0 失败（`dotnet test src/HyCAD.Tables.Tests`）。

---

## 2. 与 006 分步对照

| 步 | 006 目标 | 状态 | 主要交付物 | 测试覆盖 |
|---|---|---|---|---|
| **P1** | 静态模型骨架 | ✅ | `TableDocument` / `TableGrid` / `GridStructure` / `GridTopology` / `GridData` / `CellValue` 等 53 个 `.cs` | `TableGridCreationTests`、`PlatformIndependenceTests` |
| **P2** | 不变量校验（5 条） | ✅ 并扩展 | `GridInvariants` + `TableValidationReport` | `GridInvariantsTests`（16） |
| **P3** | 合并/斜线/可见性 | ✅ | `GridEditor.Merge/Unmerge/SplitDiagonal/ClearDiagonal`；`IsHidden/IsCovered/GetAnchorOf` | `GridEditorTests`（14） |
| **P4** | 行列增删 + 重映射 | ✅ | `InsertRow/Column`、`DeleteRow/Column` + `Remap` | `GridEditorRemapTests`（18） |
| **P5** | 填值 + FieldKey + 镜像 | ✅ | `SetValue/SetValueByField/SetFieldKey/GetValue*` | `GridEditorValueTests`（14） |
| **P6** | OpLog + Undo/Redo | ✅ | `TableOperation`（10 种语义命令）、`TableOpLog` | `TableOpLogTests`（6） |
| **P7** | JSON 往返 | ✅ | `TableJson` / `TableJsonMapper` / DTO；稀疏字典、`"r,c"` 键、SameValue 只存 Anchor | `TableJsonTests`（7） |
| **P8** | 两张样表 E2E | ✅ | `SampleTablesEndToEndTests.BuildFamilyTable`（7×6）、`BuildPersonnelTable`（7×7） | `SampleTablesEndToEndTests`（7） |
| **P9** | 简易公式（可选） | ✅ 超额 | 见 §4 | `Formula*Tests`（14+） |

### 2.1 P2 不变量：计划内 + 审查后新增

| 不变量 | 006 | 代码 |
|---|---|---|
| 网格尺寸 ≥ 1 | ✅ | `InvalidDimensions` |
| 合并矩形/不重叠/不越界 | ✅ | `InvalidMergeSpan` / `MergeOverlap` / `MergeOutOfBounds` |
| 斜线不落被覆盖非 Anchor 格 | ✅ | `DiagonalOnCoveredCell` |
| FieldKey 全表唯一 | ✅ | `DuplicateFieldKey` |
| Formula/Bound 必填字段 | ✅ | `FormulaMissing` / `BindingMissing` |
| 地址越界 | （隐含） | `AddressOutOfBounds` |
| FieldKey 须绑 Anchor | 审查新增 | `FieldKeyNotOnAnchor` |
| AnchorOnly 下 hidden 格脏数据 | 审查新增 | `DataOnHiddenCell`（SameValue 镜像格豁免） |
| SameValue 镜像一致 | 审查新增 | `SameValueMirrorMismatch` |

---

## 3. 程序集与目录结构

### 3.1 与 006 计划的命名差异

| 006 计划 | 实际落地 | 说明 |
|---|---|---|
| `HyCADTool.Shared.Tables` | **`HyCAD.Tables`** | 独立程序集，命名更短；职责相同 |
| net 对齐主工程 | **`netstandard2.0`** | 平台无关，可被 net48 主工程引用 |
| 仅 `System.*` 依赖 | + **`System.Text.Json` 8.0.5** | JSON 序列化 |

### 3.2 源码分层（`src/HyCAD.Tables`）

```
HyCAD.Tables/
├── TableDocument.cs / TableGrid.cs / TableConstants.cs / TableSourceInfo.cs
├── IDocumentNode.cs / NodeKind.cs
├── Structure/          # 拓扑、合并、斜线、样式、角色
├── Data/               # GridData、CellValue、TextRun
├── Operations/         # GridEditor、GridInvariants、TableOpLog、TableOperation
├── Serialization/      # TableJson、TableJsonMapper、DTO
├── Formulas/           # P9：Tokenizer → Parser → AST → Evaluator → Service
├── Diagnostics/        # TableInvariantCode、TableViolation、TableValidationReport
└── Internal/           # IsExternalInit（netstandard2.0 补 record init）
```

### 3.3 硬约束验证

- ✅ **零平台依赖**：`PlatformIndependenceTests` 断言无 `Autodesk.*` / NPOI / WPF 引用  
- ✅ **稀疏存储**：默认样式/空值不进字典  
- ✅ **派生不落盘**：`IsHidden` 等由 `MergeRegion` 计算；JSON 跳过 hidden 格，加载后 `RebuildMirrors`  
- ✅ **不可变编辑**：`GridEditor` 每次操作返回新 `TableGrid`  
- ⛔ **未做**：`PreserveEach` / `Composite` 合并策略（MVP 抛 `NotSupportedException`）

---

## 4. P9 公式引擎（超出 006 最小集）

006 原计划：仅 `SUM`、四则、单元格/区域引用。当前实现：

| 能力 | 状态 |
|---|---|
| 四则、`^`（右结合）、括号、一元 `+/-` | ✅ |
| `%`、`!`（阶乘） | ✅ |
| A1 引用、区域 `A1:B2` | ✅ |
| 依赖拓扑排序 + 循环引用 | ✅ 显示 `#CIRCULAR!` |
| 错误传播（`#VALUE!` 等） | ✅ 不抛未捕获异常 |
| 聚合 | `SUM` / `AVERAGE` / `COUNT` / `MIN` / `MAX` / `PRODUCT` |
| 数学 | `SIN/COS/TAN/ASIN/ACOS/ATAN/ATAN2/SINH/COSH/TANH` |
| 指数/对数 | `EXP/LN/LOG/LOG10`（非法域 → `#NUM!`） |
| 其它 | `SQRT/ABS/SIGN/ROUND/FLOOR/CEILING/TRUNC/MOD/POWER/FACT/DEGREES/RADIANS` |
| 常量 | `PI`、`E` |
| 全表重算 | `FormulaService.Recalculate` |
| JSON 加载后重算 | `TableJson.Deserialize*` 自动调用 |

**未做**：`$A$1` 绝对引用、R1C1、跨表引用、完整 Excel 函数库、协同/OpLog 持久化。

---

## 5. 006 Domain DoD 勾选

- [x] 程序集编译 0 error，零平台依赖（P1）
- [x] 不变量正反例覆盖（P2，且扩展 3 条）
- [x] 合并/拆分/斜线可逆，可见性派生正确（P3）
- [x] 行列增删后全覆盖层重映射、FieldKey 解析正确（P4）
- [x] 填值/镜像/FieldIndex 口径一致；AnchorOnly 合并清理 hidden 脏数据（P5）
- [x] 操作序列 Undo/Redo 可逆（P6）
- [x] JSON 往返等价、SameValue 只存 Anchor、加载重建镜像 + 重算公式（P7）
- [x] 家庭成员表 + 人员基本情况表 E2E 绿灯（P8）
- [x] 公式求值（P9，超额）

---

## 6. 006 明确 OUT 的范围（尚未开始）

以下仍属 **阶段 B/C/D**，本仓库 Domain 层**未实现**：

| 类别 | 内容 |
|---|---|
| **Adapter** | Markdown / HTML / AutoCAD / Excel(NPOI) |
| **AutoCAD 双模式** | 编辑模式散开 / 定稿模式渲染 |
| **线框识别器** | 退出编辑时反推 `TableGrid` |
| **WPF 面板** | 表格结构/填值 UI |
| **命令入口** | `HyCADTool` 命令、`commands.json` 注册 |
| **XData 落地** | CAD 实体 ↔ Domain 双向绑定 |
| **合并策略 V2** | `PreserveEach`、`Composite` |
| **OpLog 磁盘持久化** | 仅内存栈 |

---

## 7. 测试矩阵（114 项）

| 测试类 | 数量 | 对应能力 |
|---|---|---|
| `GridEditorRemapTests` | 18 | P4 行列重映射 |
| `GridInvariantsTests` | 16 | P2 不变量 |
| `GridEditorValueTests` | 14 | P5 填值/镜像 |
| `GridEditorTests` | 14 | P3 结构操作 |
| `FormulaCalculatorTests` | 4 | P9 表达式求值 |
| `FormulaServiceRecalcTests` | 4 | P9 全表重算 |
| `FormulaReferenceTests` | 4 | P9 A1 引用 |
| `SampleTablesEndToEndTests` | 7 | P8 黄金样表 |
| `TableJsonTests` | 7 | P7 JSON |
| `TableOpLogTests` | 6 | P6 OpLog |
| `TableGridCreationTests` | 4 | P1 构造 |
| `CellReferenceTests` | 2 | P9 列号转换 |
| `PlatformIndependenceTests` | 2 | P1 零依赖 |

---

## 8. 建议的下一步（阶段 B 入口）

按 005/006 后续接缝，推荐顺序：

1. **引用接入**：`HyCADTool.csproj` 引用 `HyCAD.Tables`（仅 Domain，不引 AutoCAD API 到 Tables 内）  
2. **AutoCAD Adapter 骨架**：`ITableAdapter<T>` + `Capability` 矩阵（005 §6）  
3. **XData 载体**：P7 JSON 作为 XData 真相源；C2 热重载下验证读写  
4. **命令 MVP**：单命令「从样表 JSON 插入表格几何」（只读渲染，不含双模式）  
5. **P8 夹具复用**：`BuildFamilyTable` / `BuildPersonnelTable` 作为 Adapter 黄金测试

---

## 9. 变更记录

| 日期 | 说明 |
|---|---|
| 2026-06-25 | 007 初版：对照 006 汇总 P1–P9 落地状态；114 单测全绿 |
| 2026-06-25 | 含审查修复：公式错误传播、`#CIRCULAR!`、MOD/LN 域错误、AnchorOnly 脏数据清理、扩展不变量、JSON 反序列化自动重算 |

---

> **验收命令**：`dotnet test src/HyCAD.Tables.Tests/HyCAD.Tables.Tests.csproj`  
> Domain 层可独立迭代；接入主工程时再走 C2→C1 联调。
