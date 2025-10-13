# HyCADTool.Refactored 通用热重启系统使用说明

## 📋 概述

本热重启系统为 `HyCADTool.Refactored` 项目提供通用测试框架，支持在 AutoCAD 中无需重启即可测试重构代码。

**核心设计原则**:
- **ReCall 不再修改**: 本项目在后续所有重构阶段中保持不变
- **测试扩展在 HyCADTool.Refactored**: 所有测试逻辑在 `HyCADTool.Refactored/Test/TestRunner.cs` 中实现
- **自动化测试**: 通过 `C1` 命令自动运行所有阶段的测试

---

## 🚀 快速开始

### 1. 编译项目（仅需一次）

#### 编译 ReCall 项目
```bash
# 在项目根目录执行
cd ReCall
dotnet build --configuration Debug
```

#### 编译 HyCADTool.Refactored 项目
**重要**: 由于 .NET Framework 4.8 + PackageReference 的兼容性问题，请使用 Visual Studio 编译：
1. 在 Visual Studio 中打开 `HyCADToolGpt.sln`
2. 在解决方案资源管理器中右键点击 `HyCADTool.Refactored` 项目
3. 选择"生成" (或按 `Ctrl+Shift+B` 生成整个解决方案)

**注意**: ReCall 项目只需编译一次，后续不再需要重新编译！

### 2. 在 AutoCAD 中加载 ReCall.dll

```
NETLOAD
选择: E:\BaiduSyncdisk\Code\CSharp\CursorProjects\hy-cad-tool\ReCall\bin\Debug\ReCall.dll
```

### 3. 使用命令

| 命令 | 功能 | 说明 |
|------|------|------|
| **C2** | 重新加载插件 | 修改代码后执行此命令刷新，无需重启 AutoCAD |
| **C1** | 运行所有测试 | 执行 `TestRunner.RunAllTests()` 方法 |

---

## 🔄 典型工作流程

### 场景 1: 首次测试

```
1. NETLOAD → 加载 ReCall.dll
2. C2      → 加载 HyCADTool.Refactored.dll  
3. C1      → 运行所有测试
```

### 场景 2: 修改代码后重新测试（最常用）

```
1. 在 VS 中修改 HyCADTool.Refactored 项目代码
2. 按 Ctrl+Shift+B 编译
3. 在 AutoCAD 中执行: C2
4. 在 AutoCAD 中执行: C1
5. 查看测试结果
```

### 场景 3: 快速迭代开发

```
循环执行：
  修改代码 → 编译(Ctrl+Shift+B) → C2 → C1 → 查看结果 → 修改代码...
```

**关键**: 整个过程中无需重启 AutoCAD！

---

## 📊 命令输出示例

### C2 - 重新加载插件

```
============================================================
开始重新加载 HyCADTool.Refactored.dll...
============================================================
插件路径: E:\...\HyCADTool.Refactored\bin\Debug\HyCADTool.Refactored.dll
✓ 插件加载成功！
============================================================
可用命令：
  C1 - 运行所有测试
  C2 - 重新加载插件
============================================================
```

### C1 - 运行所有测试

```
╔════════════════════════════════════════════════════╗
║      HyCADTool.Refactored - 完整测试套件          ║
╚════════════════════════════════════════════════════╝

【阶段 1】配置层测试
══════════════════════════════════════════════════
▶ 全局配置... ✓
▶ 模块配置... ✓
▶ 配置更新... ✓

══════════════════════════════════════════════════
测试完成: 3/3 通过
✓ 所有测试通过！
╔════════════════════════════════════════════════════╗
║              测试执行成功！                        ║
╚════════════════════════════════════════════════════╝
```

---

## 🎯 测试内容说明

### 当前阶段（阶段 1 - 配置层）

| 测试项 | 内容 |
|--------|------|
| **全局配置** | 验证比例、容差、样式、路径等全局配置的加载 |
| **模块配置** | 验证桩基、基础、钢筋、标高等模块配置的加载 |
| **配置更新** | 验证配置的动态修改和恢复功能 |

### 未来阶段（自动扩展）

随着重构进度，`TestRunner` 会自动增加新的测试：
- **阶段 2**: ZTools 工具层测试（几何工具、数学工具等）
- **阶段 3**: 服务层测试（样式服务、图层服务等）
- **阶段 4**: 辅助类层测试
- **阶段 5**: 核心工具层测试
- **阶段 6**: 命令层测试

---

## 🔧 系统架构

### 文件结构

```
ReCall/
├── bin/Debug/
│   └── ReCall.dll              ← 只需编译一次（通用热重启加载器）
├── Recall.cs                   ← 通用热重启类（后续不再修改）
└── README-使用说明.md           ← 本文档

HyCADTool.Refactored/
├── bin/Debug/
│   └── HyCADTool.Refactored.dll  ← 每次修改后需重新编译
└── Test/
    └── TestRunner.cs            ← 测试运行器（持续扩展）
```

### 工作原理

1. **ReCall.dll** (固定)
   - 在 AutoCAD 中加载一次
   - 提供 C1、C2 命令
   - 负责动态加载测试 DLL

2. **HyCADTool.Refactored.dll** (可修改)
   - 每次执行 C2 重新加载
   - 使用 `Assembly.Load(File.ReadAllBytes())` 加载
   - 修改后无需重启 AutoCAD

3. **TestRunner** (可扩展)
   - 管理所有测试逻辑
   - 根据重构进度自动运行相应测试
   - 统一输出测试结果

### 依赖解析

- 自动解析 NuGet 包依赖
- 从 `bin\Debug` 和 `.nuget\packages` 查找
- 支持 Autofac、Newtonsoft.Json 等依赖

---

## 🔍 故障排查

### 问题 1: C2 提示"找不到目标文件"

**原因**: HyCADTool.Refactored.dll 未编译或路径错误

**解决**: 
1. 在 Visual Studio 中打开解决方案
2. 编译 `HyCADTool.Refactored` 项目 (Ctrl+Shift+B)
3. 确认 `E:\...\HyCADTool.Refactored\bin\Debug\HyCADTool.Refactored.dll` 存在

### 问题 2: C1 提示"测试运行器未初始化"

**原因**: 未先执行 C2 或 C2 加载失败

**解决**: 
1. 先执行 C2 命令
2. 查看是否有错误消息
3. 确认 TestRunner.cs 已包含在项目中

### 问题 3: 程序集加载失败

**原因**: 依赖项缺失（Autofac、Newtonsoft.Json 等）

**解决**: 
1. 在 Visual Studio 中恢复 NuGet 包
2. 重新编译项目
3. 检查 bin\Debug 目录是否包含所有 DLL

### 问题 4: dotnet build 编译 HyCADTool.Refactored 失败

**原因**: .NET Framework 4.8 + PackageReference 兼容性问题

**解决**: 
- **必须使用 Visual Studio 编译此项目**
- 不要使用 `dotnet build`
- 使用 `msbuild` 也可能遇到同样问题

---

## ⚙️ 后续重构指南

### 如何添加新的测试

1. **在 TestRunner.cs 中添加新的测试方法**
   ```csharp
   // 在 TestRunner.cs 中添加
   private void TestZToolsGeometry()
   {
       // 测试几何工具
       var result = GeometryUtils.CalculateDistance(...);
       if (result <= 0)
       {
           throw new System.Exception("几何计算失败");
       }
   }
   ```

2. **在 RunAllTests() 中调用新测试**
   ```csharp
   // 在 RunAllTests() 方法中添加
   // ===== 阶段 2: ZTools 工具层测试 =====
   _editor.WriteMessage("\n\n【阶段 2】ZTools 工具层测试");
   _editor.WriteMessage("\n" + new string('═', 50));
   
   if (RunTest("几何工具", TestZToolsGeometry))
   {
       passedTests++;
   }
   totalTests++;
   ```

3. **编译并测试**
   - 在 VS 中编译 HyCADTool.Refactored
   - AutoCAD 中执行 C2
   - AutoCAD 中执行 C1

**重要**: 不需要修改 ReCall 项目！

### 如何添加分部测试

如果需要单独测试某个功能：

1. **在 TestRunner.cs 中添加公开方法**
   ```csharp
   public void RunGeometryTests()
   {
       _editor.WriteMessage("\n【几何工具专项测试】");
       TestZToolsGeometry();
       // ... 更多几何相关测试
   }
   ```

2. **临时在 RunAllTests() 中调用**
   ```csharp
   // 开发阶段可以临时调用
   if (args.Contains("geometry"))
   {
       RunGeometryTests();
       return;
   }
   ```

---

## 📌 重要约定

### ⚠️ 后续重构必须遵守的规则

1. **不再修改 ReCall 项目**
   - `ReCall/Recall.cs` 已经定型
   - 所有测试逻辑在 HyCADTool.Refactored 中实现

2. **TestRunner 保持方法签名不变**
   - `public void RunAllTests()` 方法不能改变签名
   - 可以在内部添加任意多的测试逻辑

3. **依赖项管理**
   - 新增 NuGet 包后需在 VS 中重新编译
   - 依赖会自动复制到 bin\Debug

4. **测试命名规范**
   - 测试方法使用 `Test` 前缀
   - 清晰描述测试内容
   - 例如: `TestGeometryUtils()`, `TestStyleService()`

---

## 🎓 最佳实践

### 推荐工作流

1. **开发阶段**
   ```
   修改代码 → Ctrl+Shift+B → AutoCAD: C2 → C1 → 验证
   ```

2. **调试阶段**
   ```
   附加到 acad.exe → 设置断点 → C2 → C1 → 单步调试
   ```

3. **重构阶段**
   ```
   重构代码 → 编译 → C2 → C1 确保所有测试通过 → 提交代码
   ```

### 性能优化

- **快速验证**: 修改单个测试方法，C2 + C1 快速验证
- **完整回归**: 完成一个阶段后，运行 C1 确保所有测试通过
- **增量测试**: 可在 TestRunner 中添加条件逻辑，仅运行特定阶段的测试

---

## 📚 相关文档

- [阶段1-配置层重构完成报告.md](../NewPlan/阶段1-配置层重构完成报告.md)
- [阶段1-配置层重构详细设计.md](../NewPlan/阶段1-配置层重构详细设计.md)
- [详细计划.md](../NewPlan/详细计划.md)

---

## ✅ 验收检查清单

使用此清单验证热重启系统是否正常：

- [ ] ReCall.dll 编译成功
- [ ] HyCADTool.Refactored.dll 在 VS 中编译成功
- [ ] AutoCAD 可以 NETLOAD ReCall.dll
- [ ] C2 命令可以成功加载插件
- [ ] C2 显示"插件加载成功"消息
- [ ] C1 命令执行所有测试
- [ ] C1 显示测试通过/失败统计
- [ ] 修改 TestRunner 后 C2 可以刷新
- [ ] 无需重启 AutoCAD 即可测试修改

---

**版本**: v2.0  
**创建日期**: 2025-10-13  
**设计原则**: ReCall 固定不变，测试逻辑在 HyCADTool.Refactored 中持续扩展  
**维护策略**: ReCall 后续不再修改，保持长期稳定

