---
name: hycad-code-review
description: |
  对 HyCADTool 项目当前改动（未提交改动或当前分支相对主分支的改动）做增量代码检测，
  从四个维度审查：功能质量（简洁/高效/稳定/安全）、命令用户体验、通用 C# 质量、更优实现建议，
  输出按文件分组的分级报告（必须修复/建议改进/可选优化），只报告不自动改码。
  触发场景：用户说「代码检测」「检查代码」「审查改动」「检测一下」「质量检查」「code review」时使用。
author: Cursor Agent
version: 1.0.0
date: 2026-06-13
---

# HyCADTool 增量代码检测

## 第一步：确定检测范围

1. 优先检测**未提交改动**：`git diff HEAD --stat` + `git status --porcelain` 中的未跟踪源码文件。
2. 若工作区干净，对比主分支：`git diff master...HEAD --stat`（主分支名以 `git branch` 实际为准）。
3. **只看源码**，必须过滤构建产物噪声（本仓库 git status 含大量 bin/obj 文件）：
   - 纳入：`src/**/*.cs`、`src/**/*.xaml`
   - 排除：`bin/`、`obj/`、`.vs/`、`Legacy/`、`*.g.cs`、`*.g.i.cs`、`*.baml`、`*.dll`、`*.pdb`
4. 用 `git diff` 读取每个文件的具体改动内容，新文件（未跟踪）整体读取。

## 第二步：四维检测清单

### 维度一：功能（简洁 / 高效 / 稳定 / 安全）

**简洁**
- 过度工程：为单一用途引入接口/抽象层/工厂
- 重复逻辑：与主工程已有代码重复实现
- 死代码：注释掉的大段代码、永不执行的分支

**高效**
- 循环内反复 `StartTransaction` / 反复 `OpenCloseTransaction`（应一次事务批量处理）
- 重复遍历 ModelSpace 或选择集（应一次遍历多用途收集）
- 明显 O(n²) 的几何配对/查找（可用空间索引、排序、字典替代）
- 未复用已有缓存或服务结果

**稳定（结合本项目已验证陷阱）**
- Transaction 漏 `Commit()` 或漏 `using`（泄漏导致后续命令异常）
- 跨事务持有 DBObject 引用（事务结束后对象失效）
- 多文档场景缓存单例 `Database` / `Document`（切换文档后操作错库）
- 写入锁定图层未用 `LayerLockScope.Unlock`（finally 吞异常静默失败）
- 新增图层未同步注册清单（`Entity.Layer` 静默回落 0 层）
- 存疑时查 `.cursor/skills/hycad-project-pitfalls/SKILL.md` 对应条目

**安全**
- 路径拼接未用 `Path.Combine`、未校验外部输入路径
- 外部文件读写无异常处理或失败后状态不一致
- `catch` 吞掉异常导致静默失败（至少应写日志或命令行 1 行提示）

### 维度二：命令用户体验

- 命令行输出极简：常用命令 1–3 行，**循环内禁止输出**
- 交互提示清晰：提示文案明确、有默认值、ESC 取消（检查 `PromptStatus`）不崩溃
- 撤销友好：单命令单事务，一次 Ctrl+Z 撤掉整个命令效果
- 长操作有耗时反馈（`SimpleLogger.LogElapsedTime`）
- 失败时给可操作的 1 行错误提示，不输出堆栈
- 面板参数默认值合理、走 `SettingsPanelViewModel` 持久化
- Scale 参数分类正确：红色参数（实际 mm）不乘 Scale；绿色参数（缩放值）用于几何时必须 `值 × Scale`（清单见 01 规则）

### 维度三：通用 C# 质量

- 空引用风险（解引用前未判空、`First()` 应为 `FirstOrDefault()` + 判空）
- `IDisposable` 未 `using` / 未释放
- 事件订阅未退订（面板/ViewModel 生命周期泄漏）
- 命名规范、magic number（应提为常量并注明含义）
- SOLID 违例：单类多职责、上层直接依赖具体实现
- 项目硬约束：
  - 主工程**禁用 `[CommandMethod]`**（热重载 eDuplicateKey；仅 `Production/` 目录由 `HYCAD_PRODUCTION` 隔离的文件除外）
  - 引用 AutoCAD 的 C# 中 `catch` 用 `System.Exception`（避免与 `Autodesk.AutoCAD.Runtime.Exception` 歧义）

### 维度四：更优实现建议

- 是否重复造轮子，主工程已有封装优先：
  - `EntityExtensions`（`CreateSolidCircle`、`ToSpace` 等）
  - `MLeaderExtensions.AddMleader`
  - `Shared/AutoCAD` 下的图层、选集、曲线提取等服务
  - `HyCAD.Geometry` 平台无关几何算法
- 是否有更简单的标准 API 或更优算法可替换（给出具体替代写法，不只说"可以优化"）

## 第三步：输出报告

按文件分组、引用具体行号，使用以下模板：

```markdown
# 代码检测报告

检测范围：<N> 个文件（<未提交改动 / 分支对比>）

## <文件路径>

- 🔴 **必须修复** L<行号>：<问题描述 + 后果>（崩溃 / 数据损坏 / 事务泄漏 / 静默失败）
- 🟡 **建议改进** L<行号>：<问题描述>（性能 / 用户体验 / 可读性）
- 🟢 **可选优化** L<行号>：<更优实现建议 + 具体替代写法>

## 总体结论

<一句话评价> 建议处理顺序：<先红后黄，绿色酌情>。
```

- 无问题的文件不列出；全部干净时直接说"未发现问题"并简述检查了什么。
- 每条问题必须具体到行号和原因，不写"建议注意"这类空话。

## 第四步：检测后动作

- **只报告不自动改码**，等用户确认后再修。
- 用户确认修复后：修完必须编译验证到 0 error 再交差：

```powershell
dotnet build src\HyCADTool\HyCADTool.csproj -c Debug
```

- 涉及 `src/ReCall/*.cs` 的修复需提醒用户冷启动（关 AutoCAD → 编译 ReCall → 重启 NETLOAD）。
