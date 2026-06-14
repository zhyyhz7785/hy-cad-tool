// =============================================================================
// 生产模式命令调度器（仅在 Configuration=Production 时编译）
// =============================================================================
//
// 这是 ReCallClass.Invoke 的"零依赖"等价实现：
//   - 不依赖 ReCall.dll（生产包不包含 ReCall）
//   - 复用 HyCADTool.Shell.Commands.CommandCatalog 解析 commands.json
//     （与 Blender 面板 / Ribbon / CUIX 共用同一份命令目录，避免双解析）
//   - 反射目标程序集 = Assembly.GetExecutingAssembly() = HyCADTool.dll 自己
//   - 前置钩子（CommitFocusedTextBoxValue / LoadSettings / EnsureStylesApplied）逻辑与 ReCall 一致
//
// 性能特征：
//   - commands.json 由 CommandCatalog 按 mtime 缓存（内存字典）
//   - 反射 Type/Method 按 type+method 字符串缓存（避免每次命令重复 GetType + GetMethod）
//   - 单次命令调度开销 ≈ 字典 2 次查询 + 1 次 Activator.CreateInstance + 1 次 MethodInfo.Invoke
// =============================================================================

#if HYCAD_PRODUCTION

using Autodesk.AutoCAD.ApplicationServices;
using HyCADTool.Shell.Commands;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace HyCADTool.App.Production
{
    /// <summary>
    /// 生产模式：把 [CommandMethod("xxx")] 的调用转发到 commands.json 中映射的 Type.Method。
    /// </summary>
    public static class ProductionDispatcher
    {
        private const string VmType = "HyCADTool.Shell.ViewModels.SettingsPanelViewModel";

        // 反射结果缓存：避免每次命令都 asm.GetType / type.GetMethod
        private static readonly ConcurrentDictionary<string, Type> _typeCache = new ConcurrentDictionary<string, Type>(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, MethodInfo> _methodCache = new ConcurrentDictionary<string, MethodInfo>(StringComparer.Ordinal);

        /// <summary>commands.json 实际定位路径（由 CommandCatalog 决定）。</summary>
        public static string GetCommandsJsonPath() => CommandCatalog.GetFilePath();

        /// <summary>
        /// AutoCAD 命令总入口：CommandFacade 里 144 条 [CommandMethod] 都调到这里。
        /// </summary>
        public static void Invoke(string key)
        {
            var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            if (ed == null) return;

            try
            {
                CommandCatalog.EnsureLoaded();

                // _HyExec 特殊分支：面板按钮通过 SendStringToExecute 触发
                if (string.Equals(key, "_HyExec", StringComparison.Ordinal))
                {
                    InvokePanelPendingCommand(ed);
                    return;
                }

                if (!CommandCatalog.Contains(key))
                {
                    ed.WriteMessage("\n✗ 命令表中未定义键 \"" + key + "\"。请检查 " + GetCommandsJsonPath() + "。");
                    return;
                }

                var entry = CommandCatalog.Get(key);
                if (entry == null)
                {
                    ed.WriteMessage("\n✗ 占位符 \"" + key + "\" 尚未分配。请在 " + GetCommandsJsonPath() + " 中把它指向实际的 type/method。");
                    return;
                }

                InvokePreHooks();

                var asm = typeof(ProductionDispatcher).Assembly;
                var targetType = _typeCache.GetOrAdd(entry.Type, t => asm.GetType(t));
                if (targetType == null)
                {
                    ed.WriteMessage("\n✗ 类型未找到: " + entry.Type);
                    return;
                }

                object instance = null;
                if (!(targetType.IsAbstract && targetType.IsSealed))
                {
                    var ctorArgs = ResolveCtorArgs(entry.Ctor, entry.CtorEnumTypes, targetType, asm);
                    instance = Activator.CreateInstance(targetType, ctorArgs);
                }

                var methodKey = entry.Type + "." + entry.Method;
                var method = _methodCache.GetOrAdd(methodKey, _ =>
                    targetType.GetMethod(entry.Method, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static));
                if (method == null)
                {
                    ed.WriteMessage("\n✗ 方法未找到: " + methodKey);
                    return;
                }

                method.Invoke(instance, null);
            }
            catch (TargetInvocationException tie)
            {
                var inner = tie.InnerException ?? tie;
                ed.WriteMessage("\n✗ 命令执行失败 [" + key + "]: " + inner.GetType().Name + ": " + inner.Message);
                if (inner.StackTrace != null)
                {
                    var firstLine = inner.StackTrace.Split('\n').FirstOrDefault();
                    if (firstLine != null) ed.WriteMessage("\n  " + firstLine);
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n✗ 命令调度失败 [" + key + "]: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// 前置钩子：与 ReCallClass.InvokePreHooks 等价。LoadSettings 后 Commit，面板编辑值优先。
        /// </summary>
        private static void InvokePreHooks()
        {
            try
            {
                var asm = typeof(ProductionDispatcher).Assembly;
                var vmType = _typeCache.GetOrAdd(VmType, t => asm.GetType(t));
                if (vmType == null) return;

                var currentProp = vmType.GetProperty("Current", BindingFlags.Public | BindingFlags.Static);
                var current = currentProp?.GetValue(null);
                if (current == null) return;

                vmType.GetMethod("LoadSettings", BindingFlags.Public | BindingFlags.Instance)?.Invoke(current, null);
                vmType.GetMethod("CommitFocusedTextBoxValue", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
                vmType.GetMethod("EnsureStylesApplied", BindingFlags.Public | BindingFlags.Instance)?.Invoke(current, null);
            }
            catch
            {
                /* 钩子失败不阻塞业务命令 */
            }
        }

        private static void InvokePanelPendingCommand(Autodesk.AutoCAD.EditorInput.Editor ed)
        {
            try
            {
                var asm = typeof(ProductionDispatcher).Assembly;
                var vmType = _typeCache.GetOrAdd(VmType, t => asm.GetType(t));
                if (vmType == null)
                {
                    ed?.WriteMessage("\n✗ 未找到 " + VmType);
                    return;
                }
                var consume = vmType.GetMethod("ConsumePendingCommand", BindingFlags.Public | BindingFlags.Static);
                var action = consume?.Invoke(null, null) as Delegate;
                action?.DynamicInvoke();
            }
            catch (TargetInvocationException tie)
            {
                var inner = tie.InnerException ?? tie;
                ed?.WriteMessage("\n✗ 面板命令失败: " + inner.GetType().Name + ": " + inner.Message);
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage("\n✗ 面板命令失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 与 ReCallClass.ResolveCtorArgs 完全一致的逻辑：JSON 字符串 → 枚举 / long → int / double → float 等。
        /// </summary>
        private static object[] ResolveCtorArgs(object[] ctor, string[] ctorEnumTypes, Type targetType, Assembly refactored)
        {
            if (ctor == null || ctor.Length == 0) return null;

            var args = new object[ctor.Length];
            for (int i = 0; i < ctor.Length; i++)
            {
                object raw = ctor[i];
                string enumTypeName = (ctorEnumTypes != null && i < ctorEnumTypes.Length) ? ctorEnumTypes[i] : null;
                if (!string.IsNullOrEmpty(enumTypeName) && raw is string enumValueName)
                {
                    var enumType = refactored.GetType(enumTypeName) ?? Type.GetType(enumTypeName);
                    if (enumType != null)
                    {
                        args[i] = Enum.Parse(enumType, enumValueName, ignoreCase: true);
                        continue;
                    }
                }
                args[i] = raw;
            }

            var ctors = targetType.GetConstructors();
            var best = ctors.FirstOrDefault(c => c.GetParameters().Length == args.Length);
            if (best != null)
            {
                var ps = best.GetParameters();
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == null) continue;
                    var expected = ps[i].ParameterType;
                    if (!expected.IsInstanceOfType(args[i]))
                    {
                        try { args[i] = Convert.ChangeType(args[i], expected); }
                        catch { /* 留给 CreateInstance 抛出清晰异常 */ }
                    }
                }
            }
            return args;
        }
    }
}

#endif
