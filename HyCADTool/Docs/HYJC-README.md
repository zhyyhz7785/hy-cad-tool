# HYJC — 基础沉降计算命令

## 简介

**HYJC** 是 HyCADTool.Refactored 中的定式命令，用于打开「基础沉降计算」独立窗口（WPF）。在窗口内完成参数输入与按 GB 50007 等思路的分层总和法沉降计算后，可选择将**结果表格**插入当前图纸。

实现入口：`HyCADTool.Refactored/Presentation/Commands/CommandRegistry.cs`  
业务逻辑：`SettlementCalculationCommand` → `SettlementWindow` / `SettlementPanelViewModel`。

---

## 命令与别名

| 命令 | 说明 |
|------|------|
| `HYJC` | 主命令：打开沉降计算窗口 |
| `hySC` | 与 HYJC 等价（短别名） |

在 AutoCAD 命令行直接输入即可（无需先执行 C1）。

---

## 使用流程

1. 输入 **`HYJC`** 或 **`hySC`**。
2. 在弹出窗口中设置：**高程与基础参数**、**土层**（可粘贴 Markdown 表格解析）、**基础类型**（天然 / 复合 / 桩基等）、**p₀ 自动或手动**等。
3. 点击**计算**，查看 DataGrid 结果与下方摘要。
4. 若需落图：在窗口中确认**插入表格**相关选项后关闭窗口，按提示**选择表格插入点**；程序会在当前模型空间插入 `Table` 实体（格式对齐表 5-7 类输出）。

表格生成代码：`Infrastructure/AutoCAD/Services/SettlementTableService.cs`。

---

## 开发联调（C1 / C2）

- **常规使用**：直接 `HYJC`，不依赖测试命令。
- **从 C1 单步触发**（与 `TestCommand.cs` 联调一致）：在 `HyCADTool.Refactored/Test/TestCommand.cs` 的 `switch` 中已包含 **`hyjc`** 分支，会执行与 HYJC 相同的 `SettlementCalculationCommand().Execute()`。
- **C2 热重载之后**：`HYJC` / `hySC` 通过 **`RouteThroughC1("hyjc", …)`** 转发到最新程序集执行的 C1 路径，避免旧程序集加载 WPF 资源时出现 XAML 资源找不到等问题。若热重载环境异常，可先 **C2 再执行 HYJC**，或暂时改为直接 `NETLOAD` 最新 DLL 后重试。

---

## 相关源码索引

| 路径 | 作用 |
|------|------|
| `Presentation/Commands/CommandRegistry.cs` | `HYJC` / `hySC` 注册与 `RouteThroughC1` |
| `Presentation/Commands/SettlementCalculationCommand.cs` | 打开窗口、关窗后绘表 |
| `Presentation/Views/SettlementWindow.xaml(.cs)` | 沉降计算 UI |
| `Presentation/ViewModels/SettlementPanelViewModel.cs` | 绑定、计算触发、报告与设置 |
| `Domain/Services/SettlementCalculationService.cs` | 核心沉降与系数计算 |
| `Domain/Services/SettlementReportGenerator.cs` | Markdown / 计算书内容 |
| `Infrastructure/AutoCAD/Services/SettlementTableService.cs` | AutoCAD 结果表 |

---

## 常见问题

1. **提示找不到 SettlementWindow 的 XAML 资源**  
   多见于热重载后仍从旧程序集实例化窗口。请按上文使用 **C2 → HYJC**（或确保 C1 的 `hyjc` 分支指向新 DLL）。

2. **绘制表格失败**  
   查看命令行完整错误信息。表格依赖当前图的 `TableStyle`；首行若曾被默认 Title 合并，代码侧已做解除合并及字符显示兼容处理，仍失败时请记录异常类型与堆栈便于排查。

3. **比例与文字**  
   插入表格使用的比例来自 `SettingsPanelViewModel.Current?.Scale`（与面板全局比例一致）。

---

## 版本与维护

文档随代码演进更新；命令行为以 `SettlementCalculationCommand` 与 `CommandRegistry` 中 `HYJC` 实现为准。
