# Recall.cs 代码深度分析报告

> **生成日期**: 2025-10-18  
> **文件**: `ReCall/Recall.cs`  
> **核心功能**: AutoCAD 插件热重启系统 + 测试命令框架

---

## 📋 目录

1. [架构概览](#架构概览)
2. [核心机制分析](#核心机制分析)
3. [热重启原理](#热重启原理)
4. [测试命令系统（C11-C19）](#测试命令系统c11-c19)
5. [关键代码解析](#关键代码解析)
6. [数据流分析](#数据流分析)
7. [设计亮点](#设计亮点)
8. [常见误区](#常见误区)

---

## 架构概览

### 系统职责

```
ReCall.dll (热重启系统)
    ↓
加载 HyCADTool.Refactored.dll (最新编译的代码)
    ↓
通过反射调用目标方法
    ↓
实现热重启（无需重启 AutoCAD）
```

### 核心组件

| 组件 | 类型 | 职责 |
|------|------|------|
| **ReCallClass** | 类 | 主控制器，管理所有命令 |
| **C1 命令** | 方法 | 执行 TestRunner.RunAllTests() |
| **C2 命令** | 方法 | 重新加载插件 DLL |
| **C11-C19 命令** | 方法组 | 可配置的测试命令槽位 |
| **TEST_COMMANDS** | 配置数组 | 测试命令映射表 |
| **_testCommandActions** | 委托字典 | 存储测试命令的执行委托 |
| **_runAllTestsAction** | 委托 | 存储 TestRunner 的执行委托 |

### 文件结构

```csharp
ReCallClass
├── #region 配置参数 (28-82行)
│   ├── TARGET_PROJECT_NAME = "HyCADTool.Refactored"
│   ├── TARGET_DLL_NAME = "HyCADTool.Refactored.dll"
│   ├── BUILD_CONFIGURATION = "Debug"
│   ├── DIRECTORY_LEVELS_UP = 3
│   ├── TEST_RUNNER_TYPE_NAME = "...TestRunner"
│   ├── TEST_METHOD_NAME = "RunAllTests"
│   └── TEST_COMMANDS[] = { ... }  // C11-C19 配置
│
├── 字段 (84-88行)
│   ├── _runAllTestsAction: Action
│   └── _testCommandActions: Dictionary<string, Action>
│
├── 构造函数 (93-96行)
│   └── ReCallClass() → 调用 Reload()
│
├── 命令方法 (102-211行)
│   ├── [C2] Reload() - 重新加载插件
│   └── [C1] ExecuteAllTests() - 运行所有测试
│
├── 辅助方法 (216-302行)
│   ├── GetRootDirectory() - 获取根目录
│   ├── LoadPlugin() - 加载插件并创建 TestRunner 委托
│   └── ResolveAssembly() - 解析依赖程序集
│
├── 测试命令系统 (307-399行)
│   ├── LoadTestCommands() - 加载 C11-C19 命令
│   ├── ExecuteTestCommand() - 执行测试命令的通用方法
│   └── [C11-C19] ExecuteC1X() - 9个测试命令方法
│
└── ResourceManager (405-408行)
    └── 静态资源管理器
```

---

## 核心机制分析

### 1. AutoCAD 命令注册机制

#### 原理

```csharp
// ✅ 第一次加载时注册（固定在内存中）
[CommandMethod("C11")]
public void ExecuteC11() => ExecuteTestCommand("C11");
```

**关键点**：
- `[CommandMethod]` 特性在 DLL **首次加载时**注册到 AutoCAD
- 方法地址**固定不变**，即使重新加载 DLL
- 这就是为什么普通命令不支持热重启

#### AutoCAD 命令缓存问题

```
第一次 NETLOAD HyCADTool.Refactored.dll
    ↓
AutoCAD 扫描 [CommandMethod] 特性
    ↓
注册命令表：HYOV → Method_Address_0x12345678
    ↓
永久缓存（直到 AutoCAD 重启）
    ↓
再次 NETLOAD 新版本 DLL
    ↓
AutoCAD 忽略已注册的命令 ❌
    ↓
HYOV 仍然指向旧地址 Method_Address_0x12345678
```

**结论**：**普通 [CommandMethod] 命令无法热重启**

---

### 2. 委托机制（Delegate）

#### 什么是委托？

委托 = **方法的指针/引用**

```csharp
// 定义委托类型（指向无参数、无返回值的方法）
Action myAction;

// 创建委托（指向某个方法）
myAction = () => Console.WriteLine("Hello");

// 调用委托（执行方法）
myAction.Invoke();  // 输出: Hello
```

#### Recall 中的委托使用

```csharp
// 1. 声明字段（存储委托）
private Action _runAllTestsAction;                           // 单个委托
private Dictionary<string, Action> _testCommandActions;      // 委托字典

// 2. 创建委托（LoadPlugin 中）
_runAllTestsAction = () => runAllTestsMethod.Invoke(testRunnerInstance, null);

// 3. 创建委托（LoadTestCommands 中）
_testCommandActions["C11"] = () => method.Invoke(instance, null);

// 4. 调用委托
_runAllTestsAction.Invoke();           // 执行 TestRunner.RunAllTests()
_testCommandActions["C11"].Invoke();   // 执行 HYOV 命令
```

---

### 3. 反射机制（Reflection）

#### 什么是反射？

反射 = **在运行时动态获取类型信息、创建对象、调用方法**

```csharp
// 普通调用（编译时确定）
var cmd = new OverKillCommand();
cmd.Execute();

// 反射调用（运行时确定）
var assembly = Assembly.Load(...);                        // 加载程序集
var type = assembly.GetType("...OverKillCommand");       // 获取类型
var instance = Activator.CreateInstance(type);           // 创建实例
var method = type.GetMethod("Execute");                  // 获取方法
method.Invoke(instance, null);                           // 调用方法
```

#### Recall 中的反射流程

```csharp
// LoadTestCommands() 方法中的反射链
var targetAssembly = Assembly.Load(File.ReadAllBytes(pluginPath));  // 1. 加载 DLL
                                ↓
var commandType = targetAssembly.GetType(className);                // 2. 获取类型
                                ↓
var instance = Activator.CreateInstance(commandType);               // 3. 创建实例
                                ↓
var method = commandType.GetMethod(methodName);                     // 4. 获取方法
                                ↓
_testCommandActions[command] = () => method.Invoke(instance, null); // 5. 创建委托
```

**关键**：每次 C2 重新加载时，重新执行整个反射链，获取**最新的**方法信息！

---

## 热重启原理

### 问题：为什么普通命令不能热重启？

```
时间线：
T0: NETLOAD HyCADTool.Refactored.dll (v1)
    ↓
    AutoCAD 注册命令:
    HYOV → Method_0x12345678 (v1 的 Execute 方法地址)
    ↓
T1: 修改代码，编译 (v2)
    ↓
T2: NETLOAD HyCADTool.Refactored.dll (v2)
    ↓
    AutoCAD 发现 HYOV 已注册，跳过 ❌
    ↓
T3: 用户执行 HYOV
    ↓
    AutoCAD 调用 Method_0x12345678 (仍然是 v1 的代码) ❌
```

### 解决方案：Recall 的间接调用

```
时间线：
T0: NETLOAD ReCall.dll
    ↓
    AutoCAD 注册命令:
    C11 → ReCallClass.ExecuteC11() (固定地址，永不改变)
    ↓
T1: NETLOAD HyCADTool.Refactored.dll (v1)
    C2 加载：
    _testCommandActions["C11"] = () => method_v1.Invoke(...)
    ↓
T2: 修改代码，编译 (v2)
    ↓
T3: AutoCAD 执行 C2
    重新加载：
    _testCommandActions["C11"] = () => method_v2.Invoke(...)  ← 更新委托！
    ↓
T4: 用户执行 C11
    ↓
    AutoCAD 调用 ReCallClass.ExecuteC11() (固定地址)
        ↓
        ExecuteC11() → ExecuteTestCommand("C11")
            ↓
            _testCommandActions["C11"].Invoke()  ← 调用最新的 v2 方法！✅
```

### 核心原理图

```
┌────────────────────────────────────────────────────────────────┐
│  AutoCAD 命令表（第一次加载后固定）                              │
├────────────────────────────────────────────────────────────────┤
│  C11 → ReCallClass.ExecuteC11()  ← 地址固定，永不改变          │
└────────────────────────────────────────────────────────────────┘
                        ↓
                        ↓ (调用)
                        ↓
┌────────────────────────────────────────────────────────────────┐
│  ReCallClass.ExecuteC11()                                      │
│  {                                                             │
│      ExecuteTestCommand("C11");  ← 间接调用                    │
│  }                                                             │
└────────────────────────────────────────────────────────────────┘
                        ↓
                        ↓ (查表)
                        ↓
┌────────────────────────────────────────────────────────────────┐
│  _testCommandActions 字典（每次 C2 更新）                        │
├────────────────────────────────────────────────────────────────┤
│  ["C11"] → () => method_v2.Invoke(instance, null)  ← 最新代码！ │
└────────────────────────────────────────────────────────────────┘
                        ↓
                        ↓ (执行)
                        ↓
┌────────────────────────────────────────────────────────────────┐
│  HyCADTool.Refactored.dll (v2)                                 │
│  OverKillCommand.Execute()  ← 执行最新编译的代码！              │
└────────────────────────────────────────────────────────────────┘
```

**关键**：
1. **固定层**：`C11` 命令指向 `ReCallClass.ExecuteC11()`（永不改变）
2. **动态层**：`_testCommandActions["C11"]` 委托（C2 时更新）
3. **间接调用**：通过动态层，实现调用最新代码

---

## 测试命令系统（C11-C19）

### 配置数组：TEST_COMMANDS

```csharp
private static readonly (string Command, string ClassName, string MethodName)[] TEST_COMMANDS = new[]
{
    ("C11", "HyCADTool.Refactored.Presentation.Commands.OverKillCommand", "Execute"),
    ("C12", "HyCADTool.Refactored.Presentation.Commands.OverKillCommand", "ExecuteSettings"),
    ("C13", "", ""),  // 留空 = 未配置
    // ...
};
```

**类型**：元组数组 `(string, string, string)[]`

#### 元组解析

```csharp
// 元组定义
(string Command, string ClassName, string MethodName)

// 元组赋值
("C11", "...OverKillCommand", "Execute")

// 元组解构（foreach 中）
foreach (var (command, className, methodName) in TEST_COMMANDS)
{
    // command = "C11"
    // className = "...OverKillCommand"
    // methodName = "Execute"
}
```

---

### 加载流程：LoadTestCommands()

#### 完整流程图

```
LoadTestCommands(pluginPath)
    ↓
1. 清空旧委托
   _testCommandActions.Clear()
    ↓
2. 加载新 DLL
   Assembly.Load(File.ReadAllBytes(pluginPath))
    ↓
3. 遍历 TEST_COMMANDS 数组
   foreach (var (command, className, methodName) in TEST_COMMANDS)
    ↓
4. 跳过未配置的槽位
   if (string.IsNullOrEmpty(className)) continue;
    ↓
5. 反射获取类型
   var commandType = assembly.GetType(className);
    ↓
6. 反射获取方法
   var method = commandType.GetMethod(methodName);
    ↓
7. 创建实例
   var instance = Activator.CreateInstance(commandType);
    ↓
8. 创建委托并存储
   _testCommandActions[command] = () => method.Invoke(instance, null);
    ↓
9. 重复 3-8，直到所有命令加载完毕
    ↓
10. 输出加载成功的命令数量
    ed.WriteMessage($"\n✓ 已加载 {loadedCount} 个测试命令");
```

#### 关键代码分析（307-361行）

```csharp
private void LoadTestCommands(string pluginPath)
{
    var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
    
    // ==================== 步骤 1：清空旧委托 ====================
    _testCommandActions.Clear();
    
    // ==================== 步骤 2：加载新 DLL ====================
    // 为什么用 File.ReadAllBytes？
    // - Assembly.Load(byte[]) 不锁定文件，允许 VS 重新编译
    // - Assembly.LoadFrom(path) 会锁定文件，VS 无法覆盖
    var targetAssembly = Assembly.Load(File.ReadAllBytes(pluginPath));
    
    int loadedCount = 0;
    
    // ==================== 步骤 3：遍历配置数组 ====================
    foreach (var (command, className, methodName) in TEST_COMMANDS)
    {
        // ==================== 步骤 4：跳过未配置 ====================
        if (string.IsNullOrEmpty(className) || string.IsNullOrEmpty(methodName))
            continue;
        
        try
        {
            // ==================== 步骤 5：获取类型 ====================
            // GetType(string) 需要完整类名（含命名空间）
            var commandType = targetAssembly.GetType(className);
            if (commandType == null)
            {
                ed?.WriteMessage($"\n⚠️ 警告：找不到类型 '{className}'");
                continue;
            }
            
            // ==================== 步骤 6：获取方法 ====================
            // GetMethod(string) 默认查找 public 方法
            var method = commandType.GetMethod(methodName);
            if (method == null)
            {
                ed?.WriteMessage($"\n⚠️ 警告：找不到方法 '{className}.{methodName}'");
                continue;
            }
            
            // ==================== 步骤 7：创建实例 ====================
            // Activator.CreateInstance() 调用无参构造函数
            var instance = Activator.CreateInstance(commandType);
            if (instance == null)
            {
                ed?.WriteMessage($"\n⚠️ 警告：无法创建 '{className}' 的实例");
                continue;
            }
            
            // ==================== 步骤 8：创建委托 ====================
            // Lambda 表达式创建委托，捕获 method 和 instance 变量
            // 注意：这里捕获的是当前加载的版本，下次 C2 时会重新创建
            _testCommandActions[command] = () => method.Invoke(instance, null);
            loadedCount++;
        }
        catch (System.Exception ex)
        {
            ed?.WriteMessage($"\n⚠️ 警告：加载 {command} 失败: {ex.Message}");
        }
    }
    
    ed?.WriteMessage($"\n✓ 已加载 {loadedCount} 个测试命令");
}
```

#### 为什么捕获变量？

```csharp
// 错误示例（不捕获变量）
for (int i = 0; i < 3; i++)
{
    actions[i] = () => Console.WriteLine(i);
}
actions[0]();  // 输出: 3（不是 0！）
actions[1]();  // 输出: 3（不是 1！）
actions[2]();  // 输出: 3（不是 2！）

// 正确示例（捕获变量）
foreach (var (command, className, methodName) in TEST_COMMANDS)
{
    var instance = Activator.CreateInstance(...);   // 局部变量
    var method = commandType.GetMethod(...);        // 局部变量
    
    // Lambda 捕获当前迭代的 instance 和 method
    _testCommandActions[command] = () => method.Invoke(instance, null);
}
```

**关键**：`foreach` 的 `var` 声明在每次迭代时都是新的变量，Lambda 正确捕获！

---

### 执行流程：ExecuteTestCommand()

#### 流程图

```
用户执行 C11
    ↓
AutoCAD 调用 ReCallClass.ExecuteC11()
    ↓
ExecuteC11() → ExecuteTestCommand("C11")
    ↓
    ┌──────────────────────────────────┐
    │ ExecuteTestCommand("C11")        │
    ├──────────────────────────────────┤
    │ 1. 检查命令是否存在              │
    │    _testCommandActions["C11"]?   │
    │    ↓                             │
    │ 2. 调用委托                      │
    │    _testCommandActions["C11"]    │
    │      .Invoke()                   │
    │    ↓                             │
    │ 3. 委托内部执行                  │
    │    method.Invoke(instance, null) │
    │    ↓                             │
    │ 4. 最终执行                      │
    │    OverKillCommand.Execute()     │
    └──────────────────────────────────┘
```

#### 关键代码分析（366-388行）

```csharp
private void ExecuteTestCommand(string commandName)
{
    var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
    if (ed == null) return;
    
    try
    {
        // ==================== 检查命令是否存在 ====================
        if (!_testCommandActions.ContainsKey(commandName))
        {
            ed.WriteMessage($"\n✗ 命令 {commandName} 未配置或加载失败");
            ed.WriteMessage($"\n   请在 Recall.cs 的 TEST_COMMANDS 中配置该命令");
            return;
        }
        
        // ==================== 执行命令委托 ====================
        // 这里的委托是在 LoadTestCommands() 中创建的
        // 每次 C2 重新加载时，委托会更新，指向最新的方法
        _testCommandActions[commandName].Invoke();
        
        // 等价于：
        // var action = _testCommandActions[commandName];
        // action();
    }
    catch (System.Exception ex)
    {
        ed.WriteMessage($"\n✗ 命令 {commandName} 执行失败: {ex.Message}");
        ed.WriteMessage($"\n堆栈跟踪: {ex.StackTrace}");
    }
}
```

---

### 命令方法：ExecuteC11() - ExecuteC19()

#### 代码（391-399行）

```csharp
// 自动生成 C11-C19 命令方法
[CommandMethod("C11")] public void ExecuteC11() => ExecuteTestCommand("C11");
[CommandMethod("C12")] public void ExecuteC12() => ExecuteTestCommand("C12");
[CommandMethod("C13")] public void ExecuteC13() => ExecuteTestCommand("C13");
[CommandMethod("C14")] public void ExecuteC14() => ExecuteTestCommand("C14");
[CommandMethod("C15")] public void ExecuteC15() => ExecuteTestCommand("C15");
[CommandMethod("C16")] public void ExecuteC16() => ExecuteTestCommand("C16");
[CommandMethod("C17")] public void ExecuteC17() => ExecuteTestCommand("C17");
[CommandMethod("C18")] public void ExecuteC18() => ExecuteTestCommand("C18");
[CommandMethod("C19")] public void ExecuteC19() => ExecuteTestCommand("C19");
```

#### 语法解析

```csharp
// 完整形式
[CommandMethod("C11")]
public void ExecuteC11()
{
    ExecuteTestCommand("C11");
}

// 表达式体方法（Lambda 简写）
[CommandMethod("C11")]
public void ExecuteC11() => ExecuteTestCommand("C11");
//                         ↑
//                      等价于 { return ExecuteTestCommand("C11"); }
```

#### 为什么需要 9 个独立方法？

**原因**：`[CommandMethod]` 特性只能应用于方法，不能动态生成

```csharp
// ❌ 无法做到：动态生成命令
for (int i = 11; i <= 19; i++)
{
    [CommandMethod($"C{i}")]  // ❌ 编译错误：特性参数必须是常量
    public void ExecuteCommand() { }
}

// ✅ 只能手动声明
[CommandMethod("C11")] public void ExecuteC11() => ExecuteTestCommand("C11");
[CommandMethod("C12")] public void ExecuteC12() => ExecuteTestCommand("C12");
// ...
```

#### 设计模式：模板方法模式

```
ExecuteC11()  ──┐
ExecuteC12()  ──┤
ExecuteC13()  ──┼──→ ExecuteTestCommand(commandName)  ← 模板方法
...           ──┤           ↓
ExecuteC19()  ──┘    统一的执行流程
                     (检查 → 调用委托 → 异常处理)
```

---

## 关键代码解析

### 1. C2 命令：Reload() 方法

#### 完整流程（102-183行）

```
Reload()
    ↓
1. 获取文档和编辑器
   var doc = Application.DocumentManager.MdiActiveDocument;
   var ed = doc?.Editor;
    ↓
2. 计算路径
   - 当前程序集位置：ReCall\bin\Debug\ReCall.dll
   - 向上 3 级：hy-cad-tool（根目录）
   - 目标 DLL：HyCADTool.Refactored\bin\Debug\HyCADTool.Refactored.dll
    ↓
3. 注册程序集解析器
   AppDomain.CurrentDomain.AssemblyResolve += ...
   （处理依赖 DLL 的加载）
    ↓
4. 加载插件（TestRunner）
   LoadPlugin(targetFilePath, out _runAllTestsAction);
    ↓
5. 加载测试命令（C11-C19）
   LoadTestCommands(targetFilePath);
    ↓
6. 输出成功信息
   显示可用命令列表
```

#### 路径计算逻辑

```csharp
// 当前程序集位置
Assembly.GetExecutingAssembly().Location
// → E:\...\hy-cad-tool\ReCall\bin\Debug\ReCall.dll

var adapterFileInfo = new FileInfo(location);
// adapterFileInfo.Directory
// → E:\...\hy-cad-tool\ReCall\bin\Debug

string rootDirectory = GetRootDirectory(adapterFileInfo, DIRECTORY_LEVELS_UP);
// DIRECTORY_LEVELS_UP = 3
// Debug → bin → ReCall → hy-cad-tool (根目录)
// → E:\...\hy-cad-tool

var targetFilePath = Path.Combine(
    rootDirectory,               // E:\...\hy-cad-tool
    TARGET_PROJECT_NAME,         // HyCADTool.Refactored
    "bin",
    BUILD_CONFIGURATION,         // Debug
    TARGET_DLL_NAME              // HyCADTool.Refactored.dll
);
// → E:\...\hy-cad-tool\HyCADTool.Refactored\bin\Debug\HyCADTool.Refactored.dll
```

#### 程序集解析器（143-145行）

```csharp
AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => 
    ResolveAssembly(args, dependenciesPath, nugetPackagesPath);
```

**为什么需要？**

```
HyCADTool.Refactored.dll 依赖：
    ├── Autodesk.AutoCAD.ApplicationServices.dll
    ├── Clipper2Lib.dll
    ├── NetTopologySuite.dll
    └── ...

默认情况下，.NET 在以下位置查找：
    ├── 当前 AppDomain 基目录
    └── GAC（全局程序集缓存）

但我们的依赖在：
    ├── HyCADTool.Refactored\bin\Debug\（编译输出）
    └── %USERPROFILE%\.nuget\packages\（NuGet 包）

解决方案：注册 AssemblyResolve 事件
    → 在自定义路径中查找依赖
```

---

### 2. LoadPlugin() 方法

#### 完整代码（239-261行）

```csharp
private void LoadPlugin(string pluginPath, out Action runAllTestsAction)
{
    // ==================== 1. 加载程序集 ====================
    // 为什么用 Assembly.Load(byte[])?
    // - 不锁定文件，允许 Visual Studio 重新编译
    // - Assembly.LoadFrom(path) 会锁定文件
    var targetAssembly = Assembly.Load(File.ReadAllBytes(pluginPath));

    // ==================== 2. 设置资源程序集 ====================
    // 用于 WPF 资源解析（如 LibraryResources.xaml）
    ResourceManager.ResourceAssembly = targetAssembly;

    // ==================== 3. 获取 TestRunner 类型 ====================
    // TEST_RUNNER_TYPE_NAME = "HyCADTool.Refactored.Test.TestRunner"
    var testRunnerType = targetAssembly.GetType(TEST_RUNNER_TYPE_NAME)
        ?? throw new InvalidOperationException($"无法找到类型 '{TEST_RUNNER_TYPE_NAME}'。");

    // ==================== 4. 获取 RunAllTests 方法 ====================
    // TEST_METHOD_NAME = "RunAllTests"
    var runAllTestsMethod = testRunnerType.GetMethod(TEST_METHOD_NAME)
        ?? throw new InvalidOperationException($"无法找到方法 '{TEST_METHOD_NAME}'。");

    // ==================== 5. 创建 TestRunner 实例 ====================
    var testRunnerInstance = Activator.CreateInstance(testRunnerType)
        ?? throw new InvalidOperationException("无法创建 TestRunner 的实例。");

    // ==================== 6. 创建委托 ====================
    // Lambda 表达式捕获：runAllTestsMethod 和 testRunnerInstance
    // 每次 C2 重新加载时，这两个变量都是最新的
    runAllTestsAction = () => runAllTestsMethod.Invoke(testRunnerInstance, null);
}
```

#### 委托创建详解

```csharp
// 最终生成的委托等价于：
runAllTestsAction = () =>
{
    // testRunnerInstance = new TestRunner()（最新编译的类）
    // runAllTestsMethod = TestRunner.RunAllTests 方法引用（最新编译的方法）
    runAllTestsMethod.Invoke(testRunnerInstance, null);
    
    // 等价于：
    // testRunnerInstance.RunAllTests();
};
```

---

### 3. ResolveAssembly() 方法

#### 完整代码（266-302行）

```csharp
private static Assembly ResolveAssembly(ResolveEventArgs args, string dependenciesPath, string nugetPackagesPath)
{
    // ==================== 1. 排除资源文件 ====================
    // .resources 程序集是嵌入资源，不需要外部加载
    if (args.Name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
    {
        return null;
    }

    // ==================== 2. 提取程序集文件名 ====================
    // args.Name 格式："Clipper2Lib, Version=1.4.0, Culture=neutral, PublicKeyToken=null"
    // 提取："Clipper2Lib.dll"
    string assemblyName = new AssemblyName(args.Name).Name + ".dll";

    // ==================== 3. 在本地 bin 目录查找 ====================
    // dependenciesPath = HyCADTool.Refactored\bin\Debug
    string assemblyPath = Path.Combine(dependenciesPath, assemblyName);
    if (File.Exists(assemblyPath))
    {
        return Assembly.LoadFrom(assemblyPath);
    }

    // ==================== 4. 在 NuGet 包目录递归查找 ====================
    // nugetPackagesPath = %USERPROFILE%\.nuget\packages
    try
    {
        // 查找所有匹配的目录
        // 例如：Clipper2Lib → .nuget\packages\clipper2\*\lib\*\Clipper2Lib.dll
        var directories = Directory.GetDirectories(
            nugetPackagesPath, 
            assemblyName.Replace(".dll", ""),  // 目录名（不含 .dll）
            SearchOption.AllDirectories         // 递归搜索
        );
        
        foreach (var dir in directories)
        {
            assemblyPath = Path.Combine(dir, assemblyName);
            if (File.Exists(assemblyPath))
            {
                return Assembly.LoadFrom(assemblyPath);
            }
        }
    }
    catch
    {
        // 忽略搜索错误（如权限问题）
    }

    // ==================== 5. 找不到程序集 ====================
    return null;  // 返回 null 让 .NET 继续默认解析流程
}
```

#### 搜索路径优先级

```
1. 本地 bin 目录（最高优先级）
   HyCADTool.Refactored\bin\Debug\Clipper2Lib.dll
        ↓ (如果找不到)
        
2. NuGet 包目录（递归搜索）
   %USERPROFILE%\.nuget\packages\
        ├── clipper2\1.4.0\lib\net48\Clipper2Lib.dll
        ├── nettopologysuite\2.5.0\lib\net48\NetTopologySuite.dll
        └── ...
        ↓ (如果找不到)
        
3. 返回 null（让 .NET 默认流程处理）
   .NET 会在：
        ├── AppDomain 基目录
        ├── GAC（全局程序集缓存）
        └── 探测路径（<probing> 配置）
```

---

## 数据流分析

### 完整开发测试流程

```
┌─────────────────────────────────────────────────────────────┐
│  Step 1: 开发者修改代码                                      │
│  OverKillCommand.cs: 修改 Execute() 方法逻辑                │
└─────────────────────────────────────────────────────────────┘
                        ↓
┌─────────────────────────────────────────────────────────────┐
│  Step 2: Visual Studio 编译                                  │
│  生成: HyCADTool.Refactored.dll (v2, 新版本)                │
└─────────────────────────────────────────────────────────────┘
                        ↓
┌─────────────────────────────────────────────────────────────┐
│  Step 3: AutoCAD 执行 C2 命令                                │
│  ReCallClass.Reload()                                       │
│    ↓                                                        │
│  1. 读取新 DLL 文件                                         │
│     File.ReadAllBytes("HyCADTool.Refactored.dll")          │
│    ↓                                                        │
│  2. 加载新程序集                                            │
│     Assembly.Load(bytes) → targetAssembly (v2)             │
│    ↓                                                        │
│  3. 反射获取新类型                                          │
│     targetAssembly.GetType("...OverKillCommand")           │
│     → commandType (v2)                                     │
│    ↓                                                        │
│  4. 反射获取新方法                                          │
│     commandType.GetMethod("Execute")                       │
│     → method (v2, 新逻辑的方法引用)                         │
│    ↓                                                        │
│  5. 创建新实例                                              │
│     Activator.CreateInstance(commandType)                  │
│     → instance (v2)                                        │
│    ↓                                                        │
│  6. 创建新委托（关键！）                                    │
│     _testCommandActions["C11"] = () => method.Invoke(...)  │
│     （此时 method 指向 v2 的新逻辑）                        │
│    ↓                                                        │
│  7. 输出：✓ 已加载 2 个测试命令                             │
└─────────────────────────────────────────────────────────────┘
                        ↓
┌─────────────────────────────────────────────────────────────┐
│  Step 4: 用户执行 C11 命令                                   │
│  AutoCAD 调用: ReCallClass.ExecuteC11()                     │
│    ↓                                                        │
│  ExecuteC11() → ExecuteTestCommand("C11")                   │
│    ↓                                                        │
│  ExecuteTestCommand("C11"):                                 │
│    ↓                                                        │
│  1. 查找委托                                                │
│     _testCommandActions["C11"]                             │
│    ↓                                                        │
│  2. 调用委托                                                │
│     _testCommandActions["C11"].Invoke()                    │
│     （此时委托内部的 method 是 v2 的新方法）                │
│    ↓                                                        │
│  3. 委托内部执行                                            │
│     method.Invoke(instance, null)                          │
│     （method = v2 的 Execute 方法）                         │
│    ↓                                                        │
│  4. 最终执行                                                │
│     OverKillCommand.Execute() ← v2 的新逻辑！✅             │
└─────────────────────────────────────────────────────────────┘
                        ↓
┌─────────────────────────────────────────────────────────────┐
│  Step 5: 验证结果                                            │
│  新逻辑执行成功 → 测试通过 → 继续开发或提交代码              │
└─────────────────────────────────────────────────────────────┘
```

### 委托更新的关键时刻

```csharp
// C2 执行前（旧委托）
_testCommandActions["C11"] = () => method_v1.Invoke(instance_v1, null);
                                    ↑                  ↑
                                    |                  |
                                  v1 的方法         v1 的实例

// C2 执行：LoadTestCommands()
_testCommandActions.Clear();  // ← 清空旧委托

// 重新创建委托（新委托）
_testCommandActions["C11"] = () => method_v2.Invoke(instance_v2, null);
                                    ↑                  ↑
                                    |                  |
                                  v2 的方法         v2 的实例
                                  ✅ 最新代码      ✅ 最新实例

// C11 执行后
_testCommandActions["C11"].Invoke();  // ← 调用 v2 的新逻辑
```

---

## 设计亮点

### 1. **分离注册与执行**

```
设计理念：
    命令注册（固定）+ 委托执行（动态）= 热重启

实现：
    [CommandMethod("C11")]  ← 第一次加载时注册，永不改变
    ExecuteC11()            ← 固定的方法地址
        ↓
    ExecuteTestCommand()    ← 动态查表
        ↓
    _testCommandActions[]   ← C2 时更新，指向最新代码
```

**优势**：
- ✅ 符合 AutoCAD 命令注册机制
- ✅ 避免命令冲突
- ✅ 支持热重启

### 2. **反射 + 委托的完美结合**

```
反射：动态获取最新的类型、方法、实例
    ↓
委托：封装执行逻辑，延迟调用
    ↓
结果：C2 时更新委托，C11 时执行最新代码
```

**优势**：
- ✅ 灵活性：支持任意类、任意方法
- ✅ 性能：反射只在 C2 时执行，C11 时直接调用委托
- ✅ 解耦：Recall 不需要引用具体命令类

### 3. **配置数组的可扩展性**

```csharp
// 添加新测试命令只需 2 步：

// Step 1: 编辑配置数组
("C13", "HyCADTool.Refactored.Presentation.Commands.NewCommand", "Execute"),

// Step 2: C2 重新加载（自动生效）
```

**优势**：
- ✅ 零代码修改（只修改配置）
- ✅ 支持 9 个槽位（C11-C19）
- ✅ 易于维护和扩展

### 4. **统一的错误处理**

```csharp
// 所有测试命令共享同一个错误处理逻辑
private void ExecuteTestCommand(string commandName)
{
    try
    {
        _testCommandActions[commandName].Invoke();
    }
    catch (System.Exception ex)
    {
        // 统一的错误输出格式
        ed.WriteMessage($"\n✗ 命令 {commandName} 执行失败: {ex.Message}");
        ed.WriteMessage($"\n堆栈跟踪: {ex.StackTrace}");
    }
}
```

**优势**：
- ✅ 减少重复代码
- ✅ 统一的用户体验
- ✅ 易于调试

### 5. **不锁定 DLL 文件**

```csharp
// ✅ 正确：不锁定文件
var bytes = File.ReadAllBytes(pluginPath);
var assembly = Assembly.Load(bytes);

// ❌ 错误：锁定文件（Visual Studio 无法覆盖）
var assembly = Assembly.LoadFrom(pluginPath);
```

**优势**：
- ✅ Visual Studio 可以重新编译
- ✅ 无需手动解锁文件
- ✅ 开发体验更流畅

---

## 常见误区

### ❌ 误区 1：以为普通命令也支持热重启

```csharp
// ❌ 错误理解
// 以为修改代码后，NETLOAD 就能更新 HYOV 命令
[CommandMethod("HYOV")]
public void Execute() { /* 新逻辑 */ }

// ✅ 正确理解
// HYOV 命令在第一次 NETLOAD 时已注册，地址固定
// 再次 NETLOAD 不会更新，除非重启 AutoCAD
```

**解决方案**：使用 C11-C19 测试命令（支持热重启）

---

### ❌ 误区 2：以为委托可以自动更新

```csharp
// ❌ 错误理解
// 以为 _testCommandActions["C11"] 会自动指向最新代码

// ✅ 正确理解
// 委托创建后，指向的是创建时的方法引用
// 必须显式清空并重新创建（C2 时执行）
_testCommandActions.Clear();  // ← 必须清空
// 重新创建委托（指向新方法）
_testCommandActions["C11"] = () => method_v2.Invoke(...);
```

---

### ❌ 误区 3：混淆反射和直接调用

```csharp
// 直接调用（编译时确定）
var cmd = new OverKillCommand();
cmd.Execute();

// 反射调用（运行时确定）
var type = assembly.GetType("...OverKillCommand");
var instance = Activator.CreateInstance(type);
var method = type.GetMethod("Execute");
method.Invoke(instance, null);
```

**区别**：
- **直接调用**：编译时确定类型和方法，无法更新
- **反射调用**：运行时动态获取，每次可以获取最新的

---

### ❌ 误区 4：以为 Lambda 总是捕获最新值

```csharp
// ❌ 错误：循环变量问题
for (int i = 0; i < 3; i++)
{
    actions[i] = () => Console.WriteLine(i);  // 捕获的是 i 的引用
}
// i 最终 = 3
actions[0]();  // 输出: 3（不是 0）

// ✅ 正确：每次迭代创建新变量
foreach (var (command, ...) in TEST_COMMANDS)
{
    var localCommand = command;  // 局部变量
    actions[command] = () => Console.WriteLine(localCommand);
}
```

**Recall 中的实现**：
```csharp
foreach (var (command, className, methodName) in TEST_COMMANDS)
{
    var instance = Activator.CreateInstance(...);  // 每次迭代都是新变量
    var method = commandType.GetMethod(...);       // 每次迭代都是新变量
    
    // Lambda 捕获的是当前迭代的局部变量
    _testCommandActions[command] = () => method.Invoke(instance, null);
}
```

---

### ❌ 误区 5：在 TEST_COMMANDS 中使用简化类名

```csharp
// ❌ 错误
("C11", "OverKillCommand", "Execute")

// ✅ 正确（完整类名，含命名空间）
("C11", "HyCADTool.Refactored.Presentation.Commands.OverKillCommand", "Execute")
```

**原因**：`Assembly.GetType(string)` 需要完整类名（含命名空间）

---

## 总结

### 核心技术

| 技术 | 用途 | 关键代码 |
|------|------|---------|
| **反射** | 动态获取类型、方法、创建实例 | `Assembly.GetType()`, `GetMethod()`, `Activator.CreateInstance()` |
| **委托** | 封装方法调用，延迟执行 | `Action`, `() => method.Invoke(...)` |
| **特性** | 声明 AutoCAD 命令 | `[CommandMethod("C11")]` |
| **事件** | 自定义程序集解析 | `AppDomain.AssemblyResolve += ...` |
| **Lambda** | 捕获变量，创建委托 | `() => method.Invoke(instance, null)` |

### 热重启原理

```
固定的命令入口
    ↓
动态的委托字典
    ↓
反射调用最新代码
    ↓
实现热重启
```

### 设计精髓

1. **分离关注点**：注册 vs 执行
2. **间接调用**：固定方法 → 动态委托
3. **配置驱动**：TEST_COMMANDS 配置数组
4. **错误友好**：统一错误处理和提示
5. **开发友好**：不锁定文件，支持快速迭代

---

**这就是 Recall.cs 的完整实现原理！** 🎉

通过反射 + 委托的巧妙结合，实现了 AutoCAD 插件的热重启功能，极大提升了开发效率！




