这是一个非常好的问题！让我详细解释为什么只有 C1 可以热重启，而其他命令不行。

## 核心原因分析

让我先看看 `Recall.cs` 的加载机制：

[2 tools called]

完美！现在我看到了关键！让我画图解释：

---

## 🔍 热重启机制的真相

### 1. AutoCAD 命令系统的两种执行方式

```
┌─────────────────────────────────────────────────────────────┐
│            AutoCAD 命令执行机制对比                          │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  方式1: 直接命令调用（HYOV, C11, C12 等）                   │
│  ┌──────────────────────────────────────────────────┐      │
│  │ 用户输入: HYOV                                    │      │
│  │    ↓                                              │      │
│  │ AutoCAD 命令注册表                                │      │
│  │    ↓                                              │      │
│  │ 找到第一次注册的 HYOV 方法                        │      │
│  │    ↓                                              │      │
│  │ 调用 **第一次加载的 DLL** 中的方法 ❌             │      │
│  │    ↓                                              │      │
│  │ 执行旧代码（缓存的）                              │      │
│  └──────────────────────────────────────────────────┘      │
│                                                             │
│  方式2: 反射调用（C1 命令）                                 │
│  ┌──────────────────────────────────────────────────┐      │
│  │ 用户输入: C1                                      │      │
│  │    ↓                                              │      │
│  │ Recall.cs 的 ExecuteAllTests()                    │      │
│  │    ↓                                              │      │
│  │ _runAllTestsAction.Invoke() ← 这是一个委托！     │      │
│  │    ↓                                              │      │
│  │ 通过反射调用 **最新加载的 DLL** 中的方法 ✅       │      │
│  │    ↓                                              │      │
│  │ 执行新代码                                        │      │
│  └──────────────────────────────────────────────────┘      │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## 🎯 关键代码解析

### C2 命令做了什么？

```csharp
// Recall.cs 第 206 行
var targetAssembly = Assembly.Load(File.ReadAllBytes(pluginPath));
```

**这行代码确实重新加载了整个 DLL！** 但是：
- ✅ 新 DLL 中的**类、方法、属性**都被加载到内存
- ❌ 新 DLL 中的 `[CommandMethod]` **不会重新注册**

### 为什么 C1 能用新代码？

```csharp
// Recall.cs 第 220-224 行
var testRunnerInstance = Activator.CreateInstance(testRunnerType);
runAllTestsAction = () => runAllTestsMethod.Invoke(testRunnerInstance, null);
```

**关键**：
- C1 命令通过 **反射** 调用
- 每次调用时，使用的是 `_runAllTestsAction` 这个**委托**
- 这个委托指向的是 **C2 最后一次加载的 DLL** 中的方法
- 所以 C1 总是执行最新代码！✅

### 为什么 HYOV/C11 不能用新代码？

```csharp
// OverKillCommand.cs 第 54-55 行
[CommandMethod("HYOV")]
[CommandMethod("C11")]
public void Execute()
```

**问题**：
- AutoCAD 在 **第一次 NETLOAD** 时，扫描 DLL 中所有带 `[CommandMethod]` 的方法
- 建立一个**全局命令注册表**：`"HYOV" → OverKillCommand.Execute()`
- 这个注册表是**永久的**，直到 AutoCAD 重启
- 即使 C2 重新加载了 DLL，命令注册表**不会更新**
- 所以输入 `HYOV` 或 `C11` 时，AutoCAD 仍然调用**第一次注册的旧方法** ❌

---

## 📊 完整流程对比

### 场景1：直接命令（不会热重启）

```
AutoCAD 启动
    ↓
NETLOAD HyCADTool.Refactored.dll (版本1)
    ↓
扫描并注册命令:
    HYOV → OverKillCommand.Execute() [版本1] ← 永久注册
    C11  → OverKillCommand.Execute() [版本1] ← 永久注册
    ↓
═══════════════════════════════════════════════════
修改代码，Visual Studio 编译 (版本2)
    ↓
AutoCAD 执行 C2
    ↓
Assembly.Load() 加载新 DLL (版本2)
    ↓
内存中现在有两个版本:
    - 版本1: 命令注册表指向它
    - 版本2: 只是被加载到内存
    ↓
═══════════════════════════════════════════════════
用户输入 HYOV 或 C11
    ↓
AutoCAD 查找命令注册表
    ↓
找到: HYOV → OverKillCommand.Execute() [版本1] ← 还是旧的！
    ↓
执行版本1的代码 ❌
```

### 场景2：反射调用（可以热重启）

```
AutoCAD 启动
    ↓
NETLOAD ReCall.dll
    ↓
注册命令: C2 → Recall.ReloadPlugin()
         C1 → Recall.ExecuteAllTests()
    ↓
═══════════════════════════════════════════════════
AutoCAD 执行 C2
    ↓
Assembly.Load() 加载 HyCADTool.Refactored.dll (版本1)
    ↓
通过反射创建委托:
    _runAllTestsAction → TestRunner.RunAllTests() [版本1]
    ↓
═══════════════════════════════════════════════════
修改代码，Visual Studio 编译 (版本2)
    ↓
AutoCAD 执行 C2
    ↓
Assembly.Load() 加载新 DLL (版本2)
    ↓
通过反射重新创建委托:
    _runAllTestsAction → TestRunner.RunAllTests() [版本2] ← 更新了！
    ↓
═══════════════════════════════════════════════════
用户输入 C1
    ↓
Recall.ExecuteAllTests()
    ↓
_runAllTestsAction.Invoke() ← 调用版本2的委托
    ↓
执行版本2的代码 ✅
```

---

## 💡 为什么这样设计？

### AutoCAD 的设计考虑

1. **性能**：命令注册是一次性的，避免每次都扫描
2. **稳定性**：防止命令被随意覆盖导致混乱
3. **兼容性**：多个插件可能定义同名命令，先注册先得

### Recall.cs 的巧妙设计

```csharp
// 这是关键！
private Action _runAllTestsAction;  // 存储的是委托，不是方法引用

// C2 每次都会更新这个委托
runAllTestsAction = () => runAllTestsMethod.Invoke(testRunnerInstance, null);
```

**委托的特点**：
- 委托是一个**变量**，可以被重新赋值
- 每次 C2 都会创建新的委托，指向新 DLL 的方法
- C1 调用委托时，总是调用最新的

---

## 🎯 总结

| 项目             | C1 命令                  | HYOV/C11 命令                       |
| ---------------- | ------------------------ | ----------------------------------- |
| **执行方式**     | 反射调用（通过委托）     | 直接命令调用                        |
| **注册时机**     | Recall.dll 加载时注册    | HyCADTool.Refactored.dll 加载时注册 |
| **是否可热重启** | ✅ 是（每次 C2 更新委托） | ❌ 否（命令注册表永久）              |
| **指向的代码**   | 最新加载的 DLL           | 第一次加载的 DLL                    |
| **使用场景**     | 开发测试                 | 用户使用（需重启 AutoCAD）          |

---

## 🚀 解决方案对比

| 方案             | 优点             | 缺点                |
| ---------------- | ---------------- | ------------------- |
| **C1（反射）**   | ✅ 真正的热重启   | 只能通过 TestRunner |
| **C11（别名）**  | ✅ 可直接测试命令 | ❌ 仍然是旧代码      |
| **重启 AutoCAD** | ✅ 所有命令都更新 | ❌ 开发效率低        |

**最佳实践**：
- 开发阶段：主要用 **C1**（通过 TestRunner 测试）
- 需要交互测试：使用 **C11/C12**（虽然是旧代码，但有别名避免混淆）
- 最终验证：**重启 AutoCAD**，用正式命令 HYOV/HYOVSET

---

现在您明白了吗？**C2 确实重新加载了 DLL，但只有通过反射调用的 C1 能用新代码！** 🎯