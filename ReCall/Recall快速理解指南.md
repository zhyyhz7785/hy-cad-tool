# Recall 快速理解指南（新对话必读）

> **用途**: 在新对话开始时快速理解 Recall.cs 的核心原理  
> **时长**: 3 分钟阅读

---

## 🎯 一句话总结

**Recall = 通过反射+委托实现 AutoCAD 插件热重启**

---

## 🔑 核心概念（必须理解）

### 1. 为什么需要 Recall？

```
问题：
    修改代码 → 编译 → NETLOAD → 执行命令
                              ↓
                        ❌ 还是旧代码（AutoCAD 缓存了命令）

解决：
    修改代码 → 编译 → C2（热重启）→ C11（测试命令）
                              ↓
                        ✅ 执行最新代码
```

### 2. 为什么普通命令不支持热重启？

```csharp
// 第一次 NETLOAD HyCADTool.Refactored.dll
[CommandMethod("HYOV")]
public void Execute() { /* v1 代码 */ }
    ↓
AutoCAD 注册：HYOV → 0x12345678（方法地址）
    ↓
再次 NETLOAD 新版本 DLL
    ↓
AutoCAD 忽略（命令已注册）❌
    ↓
执行 HYOV 仍然调用 0x12345678（旧代码）
```

### 3. Recall 如何解决？

```
用户执行: C11
    ↓
AutoCAD 调用: ReCallClass.ExecuteC11() ← 固定地址（永不改变）
    ↓
ExecuteC11() → ExecuteTestCommand("C11")
    ↓
查找委托: _testCommandActions["C11"]  ← C2 时更新，指向最新方法
    ↓
执行委托: method.Invoke(instance, null)  ← 最新代码！✅
```

---

## 📋 命令清单

| 命令 | 功能 | 执行内容 | 支持热重启 |
|------|------|---------|-----------|
| C1 | 运行测试 | TestRunner.RunAllTests() | ✅ |
| C2 | 热重启 | 重新加载 DLL，更新所有委托 | - |
| C11 | 测试命令1 | 配置的第1个命令 | ✅ |
| C12 | 测试命令2 | 配置的第2个命令 | ✅ |
| C13-C19 | 测试命令3-9 | 可配置槽位 | ✅ |
| HYOV | 正式命令 | OverKillCommand.Execute() | ❌ |

---

## 🔧 配置测试命令（TEST_COMMANDS）

**位置**: `Recall.cs` 第 69-80 行

```csharp
private static readonly (string Command, string ClassName, string MethodName)[] TEST_COMMANDS = new[]
{
    ("C11", "HyCADTool.Refactored.Presentation.Commands.OverKillCommand", "Execute"),
    ("C12", "HyCADTool.Refactored.Presentation.Commands.OverKillCommand", "ExecuteSettings"),
    ("C13", "", ""),  // 留空 = 未配置
    // ...
};
```

**添加新命令**：编辑这个数组 → VS 编译 ReCall 项目 → AutoCAD C2 重新加载

---

## ⚙️ 工作流程（开发测试循环）

```
1. 修改 HyCADTool.Refactored 中的代码
   ↓
2. Visual Studio 编译项目
   ↓
3. AutoCAD 执行 C2
   （重新加载 DLL，更新委托）
   ↓
4. AutoCAD 执行 C11（或 C12-C19）
   （测试最新代码）
   ↓
5. 检查结果
   ↓
6. 重复 1-5，直到功能完善
```

---

## 🧠 核心技术原理

### 关键代码1：固定的命令入口

```csharp
// 这些方法在 Recall.dll 第一次加载时注册，地址固定
[CommandMethod("C11")] 
public void ExecuteC11() => ExecuteTestCommand("C11");
```

### 关键代码2：动态的委托字典

```csharp
// 这个字典在每次 C2 时更新，存储最新的方法引用
private Dictionary<string, Action> _testCommandActions;

// C2 执行时
_testCommandActions["C11"] = () => method_v2.Invoke(instance_v2, null);
                                    ↑                  ↑
                                最新方法          最新实例
```

### 关键代码3：执行最新代码

```csharp
private void ExecuteTestCommand(string commandName)
{
    // 查找并调用最新的委托
    _testCommandActions[commandName].Invoke();  ← 总是最新代码
}
```

---

## 🎓 必须理解的3个概念

### 1. 反射（Reflection）

```csharp
// 运行时动态获取类型、方法、创建实例
var assembly = Assembly.Load(...);                    // 加载 DLL
var type = assembly.GetType("...OverKillCommand");    // 获取类型
var instance = Activator.CreateInstance(type);       // 创建实例
var method = type.GetMethod("Execute");              // 获取方法
method.Invoke(instance, null);                       // 调用方法
```

**用途**: C2 时获取最新的类型和方法

### 2. 委托（Delegate）

```csharp
// 方法的引用/指针
Action myAction = () => Console.WriteLine("Hello");
myAction.Invoke();  // 执行方法
```

**用途**: 存储最新的方法引用，延迟执行

### 3. Lambda 表达式

```csharp
// 创建委托的简写方式
_testCommandActions["C11"] = () => method.Invoke(instance, null);
//                           ↑
//                      捕获 method 和 instance 变量
```

**用途**: 捕获变量，创建委托

---

## 📊 对比：普通命令 vs 测试命令

| 特性 | 普通命令（HYOV） | 测试命令（C11） |
|------|----------------|----------------|
| 注册时机 | HyCADTool.Refactored.dll 加载时 | ReCall.dll 加载时 |
| 方法地址 | 固定（第一次） | 固定（永远） |
| 调用方式 | 直接调用 | 反射调用 |
| 代码版本 | 第一次的 | 最新的（C2更新） |
| 支持热重启 | ❌ 否 | ✅ 是 |

---

## ⚠️ 常见错误

### ❌ 错误1：修改代码后直接执行 HYOV

```
修改代码 → 编译 → HYOV
                    ↓
                  旧代码 ❌
```

**正确做法**: 执行 C11（或先 C2）

### ❌ 错误2：忘记执行 C2

```
修改代码 → 编译 → C11
                    ↓
                  旧代码 ❌（委托未更新）
```

**正确做法**: C2 → C11

### ❌ 错误3：TEST_COMMANDS 中使用简化类名

```csharp
// ❌ 错误
("C11", "OverKillCommand", "Execute")

// ✅ 正确（必须包含命名空间）
("C11", "HyCADTool.Refactored.Presentation.Commands.OverKillCommand", "Execute")
```

---

## 📝 快速查询表

### C2 输出的命令列表含义

```
C1  - 运行所有测试
      → 执行 TestRunner.RunAllTests()

C2  - 重新加载插件
      → 重新加载 DLL，更新所有委托

C11 - OverKillCommand.Execute() ✓
      → 已配置，支持热重启

C13 - （未配置）
      → 留空槽位，可添加新命令
```

### 修改 Recall 的场景

**只需修改配置区域（28-82行）**：
- 更换测试项目：修改 `TARGET_PROJECT_NAME`
- 添加测试命令：编辑 `TEST_COMMANDS` 数组

**不要修改其他代码**（除非理解全部原理）

---

## 🎯 关键规则（遵循这些规则）

### 规则1：开发测试总是用 C11-C19

```
✅ 正确：修改代码 → 编译 → C2 → C11 测试
❌ 错误：修改代码 → 编译 → HYOV 测试（旧代码）
```

### 规则2：C2 是必须的

```
修改代码后，必须执行 C2 才能加载最新代码
```

### 规则3：TEST_COMMANDS 必须是完整类名

```
格式："命名空间.类名"
示例："HyCADTool.Refactored.Presentation.Commands.OverKillCommand"
```

### 规则4：测试方法必须是 public 且无参数

```csharp
// ✅ 正确
public void Execute() { }

// ❌ 错误
private void Execute() { }           // private 无法反射调用
public void Execute(int x) { }       // 带参数无法配置
```

### 规则5：目标类必须有无参构造函数

```csharp
// ✅ 正确（默认构造函数）
public class MyCommand { }

// ✅ 正确（显式无参构造函数）
public class MyCommand 
{
    public MyCommand() { }
}

// ❌ 错误（只有有参构造函数）
public class MyCommand 
{
    public MyCommand(int x) { }
}
```

---

## 📚 进一步学习

**想深入理解？** 阅读 `Recall代码深度分析报告.md`

**想配置命令？** 阅读 `测试命令配置说明.md`

---

## 🎉 总结

**Recall 的核心思想**：

```
固定的命令入口（C11）
    +
动态的委托字典（C2 更新）
    +
反射调用最新代码
    =
热重启功能（无需重启 AutoCAD）
```

**记住这个流程**：

```
修改代码 → VS 编译 → AutoCAD C2 → AutoCAD C11 测试
```

---

**现在您可以自信地使用 Recall 进行开发了！** 🚀




