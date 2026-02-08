# ReCall / Recall.cs 详尽说明

> 热重启加载器：在 AutoCAD 不关闭的前提下，重新加载 HyCADTool.Refactored 并执行单命令测试。

---

## 一、目的与命令

| 命令 | 作用 |
|------|------|
| **C2** | 将 HyCADTool.Refactored 及其依赖复制到临时目录，从副本加载到当前进程，并初始化 Autofac / TestCommand 入口。**不锁定** 项目下的 `bin\Debug`，因此可在不关 AutoCAD 的情况下在 VS 里重新生成。 |
| **C1** | 执行当前配置的“一个测试命令”，即调用 `HyCADTool.Refactored.Test.TestCommand.Run()`。要测哪个命令只改 Refactored 里的 `Test/TestCommand.cs`，无需改 ReCall。 |

设计要点：

- 加载来自**临时目录副本**，避免锁定 `bin\Debug`，实现**热重启**（改代码 → 编译 → C2 → C1，无需关 AutoCAD）。
- C1 的入口固定为 `TestCommand.Run()`，具体测哪条命令由 `TestCommand.cs` 内一行代码决定。

---

## 二、配置区（仅此处可改）

位于 `Recall.cs` 顶部 `#region ========== 配置 ==========`：

| 常量 | 含义 | 典型值 |
|------|------|--------|
| `TARGET_PROJECT_NAME` | 要加载的项目目录名 | `HyCADTool.Refactored` |
| `TARGET_DLL_NAME` | 主程序集文件名 | `HyCADTool.Refactored.dll` |
| `BUILD_CONFIGURATION` | 使用的生成配置 | `Debug` 或 `Release` |
| `DIRECTORY_LEVELS_UP` | 从 ReCall.dll 所在目录到解决方案根的“向上层数” | `3`（`...\ReCall\bin\Debug\` → 根） |
| `TEST_ENTRY_TYPE` | C1 调用的类型（完整命名空间.类名） | `HyCADTool.Refactored.Test.TestCommand` |
| `TEST_ENTRY_METHOD` | C1 调用的静态方法名 | `Run` |

除上述配置外，ReCall 逻辑不建议修改；换测命令只改 `HyCADTool.Refactored/Test/TestCommand.cs`。

---

## 三、C2（Reload）执行流程

1. **取路径**
   - 用 `Assembly.GetExecutingAssembly().Location` 得到 ReCall.dll 所在目录。
   - 按 `DIRECTORY_LEVELS_UP` 上溯到解决方案根目录 `root`。
   - 主 DLL：`root\HyCADTool.Refactored\bin\Debug\HyCADTool.Refactored.dll` → `pluginPath`。
   - 依赖目录：同级的 `bin\Debug` → `depsPath`。
   - 若 `pluginPath` 不存在，提示“未找到”并返回。

2. **复制到临时目录（关键：实现热重启不锁 bin）**
   - 调用 `CopyToTempAndGetLoadPath(depsPath, pluginPath, ed)`：
     - 在 `%TEMP%\HyCADToolRefactored\<Ticks>\` 下新建子目录（每次 C2 用新目录）。
     - 将 `bin\Debug` 下所有文件复制到该子目录。
     - 返回副本中的主 DLL 路径 `loadPath`，以及其所在目录 `loadDepsPath`。
   - 若复制失败或主 DLL 不存在，提示并返回。
   - 效果：AutoCAD 只加载“临时副本”中的 DLL，**不锁定** 项目里的 `bin\Debug`，VS 可随时重新生成。

3. **注册程序集解析**
   - 订阅 `AppDomain.CurrentDomain.AssemblyResolve`。
   - 解析时优先从 `loadDepsPath`（临时副本）找 DLL，找不到再在 NuGet 包目录中按名称搜索（`ResolveAssembly`）。

4. **加载主程序集**
   - `Assembly.Load(File.ReadAllBytes(loadPath))`，从**副本**加载主 DLL（不锁源文件）。
   - 设置 `ResourceManager.ResourceAssembly = asm`，供 Refactored 内资源使用。

5. **初始化 Refactored 的 DI**
   - `InitializeServiceLocator(asm, ed)`：用反射构造 Autofac 容器、注册 `AutofacModule`、调用 `ServiceLocator.Initialize(container)`，使 Refactored 内通过 ServiceLocator 解析的服务可用。若 Refactored 未用 Autofac 或类型不存在则静默忽略。

6. **绑定 C1 入口**
   - `CreateStaticMethodDelegate(asm, TEST_ENTRY_TYPE, TEST_ENTRY_METHOD, ed)`：
     - 在刚加载的 `asm` 中查找 `TestCommand` 类型和静态方法 `Run`；若当前 asm 中找不到，会尝试从已加载的名为 `HyCADTool.Refactored` 的程序集中再找一次（兼容某些加载顺序）。
     - 得到 `Action` 委托，存到 `_c1Action`。
   - 若找不到类型或方法，提示“请重新生成 HyCADTool.Refactored 后再执行 C2”。

7. **提示**
   - 成功：`插件已加载（从副本，不锁 bin\Debug）。 C2 重载 | C1 测试`
   - 失败：加载失败或未找到 TestCommand 时对应错误信息。

**说明**：不在构造函数里调用 `Reload()`，避免 ReCall 被 AutoCAD 加载时自动执行一次 C2，造成“执行两次”的误解。

---

## 四、C1（RunTest）执行流程

1. 若无当前文档的 Editor，直接返回。
2. 若 `_c1Action == null`（尚未执行过 C2 或 C2 未成功绑定 TestCommand），提示“请先执行 C2 加载插件”并返回。
3. 执行 `_c1Action.Invoke()`，即调用 `TestCommand.Run()`。
4. 若抛出异常，在命令行输出“执行失败”及异常信息。

TestCommand 内部会使用 SimpleLogger 打耗时，并执行你在 `TestCommand.cs` 里配置的那一条命令（如某 Command 的 `Execute()`）。

---

## 五、辅助方法说明

| 方法 | 作用 |
|------|------|
| `GetRootDirectory(FileInfo, levelsUp)` | 从给定文件的目录向上走 `levelsUp` 层，得到解决方案根目录。 |
| `CopyToTempAndGetLoadPath(sourceDir, mainDllPath, ed)` | 将 `sourceDir`（即 bin\Debug）下所有文件复制到 `%TEMP%\HyCADToolRefactored\<新子目录>`，返回副本中主 DLL 的完整路径；单文件复制失败会忽略，只要主 DLL 存在即返回。 |
| `GetLoadedAssembly(shortName)` | 在当前 AppDomain 已加载程序集中，按短名称（如 `HyCADTool.Refactored`）查找并返回，供 `CreateStaticMethodDelegate` 备用查找类型。 |
| `CreateStaticMethodDelegate(asm, typeName, methodName, ed)` | 从 `asm` 中取 `typeName` 的静态方法 `methodName`，生成无参 `Action`；若 asm 中无该类型则尝试从已加载的同名程序集取。用于绑定 `TestCommand.Run`。 |
| `InitializeServiceLocator(targetAssembly, ed)` | 用反射创建 Autofac `ContainerBuilder`、注册 `AutofacModule`、Build 得到容器，再调用 Refactored 的 `ServiceLocator.Initialize(container)`；若 Container 已存在则跳过。 |
| `ResolveAssembly(args, dependenciesPath, nugetPackagesPath)` | `AssemblyResolve` 回调：先忽略 `.resources`，再在 `dependenciesPath` 和 NuGet 包目录中按请求的程序集名找 DLL 并 `LoadFrom`。 |

---

## 六、热重启为何不锁 bin\Debug

- 若直接从 `bin\Debug` 加载 DLL，AutoCAD 会锁定这些文件，VS 重新生成时无法覆盖 → MSB3061。
- ReCall 的做法是：**每次 C2 先把整个 `bin\Debug` 复制到临时目录**，再**只从临时目录**加载主 DLL 和依赖（通过 `loadDepsPath` 和 `AssemblyResolve`）。
- 被进程锁定的是临时目录里的副本，项目下的 `bin\Debug` 从未被加载，因此可以随时被 VS 覆盖。
- 每次 C2 使用新的临时子目录（按 Ticks），避免覆盖当前已加载的副本导致读写冲突。

---

## 七、ResourceManager 类

- 静态属性 `ResourceAssembly`：由 ReCall 在 C2 时设为刚加载的 Refactored 主程序集。
- 供 HyCADTool.Refactored 内需要“当前插件程序集”的资源或反射逻辑使用（例如 XAML 资源、包 URI 等）。

---

## 八、使用流程小结

1. 在 AutoCAD 中加载 ReCall（NETLOAD 或启动加载）。
2. **改 HyCADTool.Refactored 代码**（包括要测的命令）。
3. 在 VS 里**重新生成 HyCADTool.Refactored**（无需关闭 AutoCAD）。
4. 回到 AutoCAD 执行 **C2**：从最新 `bin\Debug` 复制并加载，初始化 DI，绑定 C1。
5. 执行 **C1**：运行 `TestCommand.Run()`，即你在 `TestCommand.cs` 里配置的那一个命令。
6. 要换测其它命令时，只改 `HyCADTool.Refactored/Test/TestCommand.cs` 中那一行，再编译 → C2 → C1。

---

## 九、异常与排查

| 现象 | 可能原因 | 建议 |
|------|----------|------|
| C2 提示“未找到”主 DLL | 路径或配置不对、未生成 | 检查配置区与 `DIRECTORY_LEVELS_UP`，确认 Refactored 已生成到对应 bin\Debug。 |
| C2 提示“未找到类型 TestCommand” | Refactored 未含 Test 或未重新生成 | 确认 `Test/TestCommand.cs` 在项目中且已参与编译，再重新生成后执行 C2。 |
| C2 提示“复制到临时目录失败” | 权限或磁盘问题 | 检查 %TEMP% 可写、磁盘空间。 |
| C1 提示“请先执行 C2” | 尚未执行 C2 或 C2 未成功 | 先成功执行一次 C2 再执行 C1。 |
| 重新生成时仍报“文件被锁定” | 可能从别处加载了 bin\Debug | 确保只通过 ReCall 的 C2 加载 Refactored（不从 bin\Debug 直接 NETLOAD），且 C2 使用当前逻辑（从副本加载）。 |

---

**文档版本**：与 Recall.cs 当前实现对应（热重启采用“复制到临时目录再加载”）。
