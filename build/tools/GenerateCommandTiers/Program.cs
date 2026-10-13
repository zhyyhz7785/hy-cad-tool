using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace GenerateCommandTiers
{
    /// <summary>
    /// 从 commands.json 生成 CommandTiers.g.cs，可选再生成 CommandRoutes.g.cs。
    /// 用法: GenerateCommandTiers &lt;commands.json&gt; &lt;CommandTiers.g.cs&gt; [CommandRoutes.g.cs] [stamp]
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("用法: GenerateCommandTiers <commands.json> <CommandTiers.g.cs> [CommandRoutes.g.cs] [stamp] [facade-methods.json] [CommandFacade.cs] [ProductionCommandFacade.cs] [modulesDir] [CommandModules.g.cs]");
                return 1;
            }

            var jsonPath = args[0];
            var tiersPath = args[1];
            var routesPath = args.Length >= 3 ? args[2] : null;
            var stampPath = args.Length >= 4 ? args[3] : null;
            var facadeSpecPath = args.Length >= 5 ? args[4] : null;
            var devFacadePath = args.Length >= 6 ? args[5] : null;
            var prodFacadePath = args.Length >= 7 ? args[6] : null;
            var modulesDir = args.Length >= 8 ? args[7] : null;
            var modulesGenerated = args.Length >= 9 ? args[8] : null;

            if (!File.Exists(jsonPath))
            {
                Console.Error.WriteLine("找不到 " + jsonPath);
                return 1;
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(File.ReadAllText(jsonPath, Encoding.UTF8));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("解析 " + jsonPath + " 失败: " + ex.Message);
                return 1;
            }

            using (doc)
            {
                if (!doc.RootElement.TryGetProperty("commands", out var commands)
                    || commands.ValueKind != JsonValueKind.Object)
                {
                    Console.Error.WriteLine("commands.json 缺少 commands 对象。");
                    return 1;
                }

                var rows = new List<CommandRow>();
                foreach (var prop in commands.EnumerateObject())
                {
                    if (prop.Value.ValueKind != JsonValueKind.Object)
                        continue;
                    rows.Add(ReadRow(prop.Name, prop.Value));
                }

                rows.Sort(CompareKeyCasefold);

                List<ModuleRows> modules = null;
                if (!string.IsNullOrWhiteSpace(modulesDir) && !string.IsNullOrWhiteSpace(modulesGenerated))
                {
                    modules = ReadModules(modulesDir);
                    if (modules == null)
                        return 1;
                    if (!WriteIfChanged(modulesGenerated, RenderModules(modules)))
                        return 1;
                    Console.WriteLine("已写入 " + modulesGenerated);
                }

                if (!WriteIfChanged(tiersPath, RenderTiers(rows, useRegistry: modules != null)))
                    return 1;
                Console.WriteLine("已写入 " + tiersPath);

                if (!string.IsNullOrWhiteSpace(routesPath))
                {
                    if (!WriteIfChanged(routesPath, RenderRoutes(rows, useRegistry: modules != null)))
                        return 1;
                    Console.WriteLine("已写入 " + routesPath);
                }

                if (!string.IsNullOrWhiteSpace(facadeSpecPath)
                    && !string.IsNullOrWhiteSpace(devFacadePath)
                    && !string.IsNullOrWhiteSpace(prodFacadePath))
                {
                    if (!File.Exists(facadeSpecPath))
                    {
                        Console.Error.WriteLine("找不到 " + facadeSpecPath);
                        return 1;
                    }

                    FacadeSpec spec;
                    try
                    {
                        spec = ReadFacadeSpec(facadeSpecPath);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("解析 " + facadeSpecPath + " 失败: " + ex.Message);
                        return 1;
                    }

                    if (!WriteIfChanged(devFacadePath, RenderDevFacade(spec)))
                        return 1;
                    Console.WriteLine("已写入 " + devFacadePath);

                    if (!WriteIfChanged(prodFacadePath, RenderProdFacade(spec)))
                        return 1;
                    Console.WriteLine("已写入 " + prodFacadePath);
                }

                if (!string.IsNullOrWhiteSpace(stampPath) && !TouchStamp(stampPath))
                    return 1;
            }

            return 0;
        }

        private static CommandRow ReadRow(string key, JsonElement value)
        {
            string rawTier = null;
            if (value.TryGetProperty("tier", out var tierEl) && tierEl.ValueKind == JsonValueKind.String)
                rawTier = tierEl.GetString();

            string type = null;
            if (value.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String)
                type = typeEl.GetString();

            string method = "Execute";
            if (value.TryGetProperty("method", out var methodEl) && methodEl.ValueKind == JsonValueKind.String)
            {
                var m = methodEl.GetString();
                if (!string.IsNullOrWhiteSpace(m))
                    method = m;
            }

            object[] ctor = null;
            if (value.TryGetProperty("ctor", out var ctorEl) && ctorEl.ValueKind == JsonValueKind.Array)
            {
                var list = new List<object>();
                foreach (var item in ctorEl.EnumerateArray())
                    list.Add(ReadCtorValue(item));
                ctor = list.ToArray();
            }

            string[] ctorEnumTypes = null;
            if (value.TryGetProperty("ctorEnumTypes", out var enumEl) && enumEl.ValueKind == JsonValueKind.Array)
            {
                var list = new List<string>();
                foreach (var item in enumEl.EnumerateArray())
                    list.Add(item.ValueKind == JsonValueKind.String ? item.GetString() : null);
                ctorEnumTypes = list.ToArray();
            }

            return new CommandRow(key, NormalizeTier(rawTier), type, method, ctor, ctorEnumTypes);
        }

        private static object ReadCtorValue(JsonElement item)
        {
            switch (item.ValueKind)
            {
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.String:
                    return item.GetString();
                case JsonValueKind.Number:
                    if (item.TryGetInt64(out var l) && l >= int.MinValue && l <= int.MaxValue && item.GetRawText().IndexOf('.') < 0)
                        return (int)l;
                    return item.GetDouble();
                case JsonValueKind.Null:
                    return null;
                default:
                    return item.GetRawText();
            }
        }

        /// <summary>与 Python str.casefold 在 ASCII 键上同序：先小写再按码位，避免 OrdinalIgnoreCase 把 _ 排到字母后。</summary>
        private static int CompareKeyCasefold(CommandRow a, CommandRow b)
        {
            return string.CompareOrdinal(a.Key.ToLowerInvariant(), b.Key.ToLowerInvariant());
        }

        internal static string NormalizeTier(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "standard";
            var t = raw.Trim();
            if (t.Equals("freemium", StringComparison.OrdinalIgnoreCase) || t.Equals("free", StringComparison.OrdinalIgnoreCase))
                return "freemium";
            if (t.Equals("standard", StringComparison.OrdinalIgnoreCase))
                return "standard";
            if (t.Equals("professional", StringComparison.OrdinalIgnoreCase) || t.Equals("pro", StringComparison.OrdinalIgnoreCase))
                return "professional";
            if (t.Equals("enterprise", StringComparison.OrdinalIgnoreCase) || t.Equals("ent", StringComparison.OrdinalIgnoreCase))
                return "enterprise";
            return "standard";
        }

        private static string RenderTiers(List<CommandRow> rows, bool useRegistry)
        {
            var sb = new StringBuilder();
            sb.Append("// <auto-generated />\n");
            sb.Append("// 由 build/tools/GenerateCommandTiers 从模块命令清单汇总生成，不要手改。\n");
            sb.Append("using System;\n");
            sb.Append("using System.Collections.Generic;\n");
            sb.Append("\n");
            sb.Append("namespace HyCADTool.Shell.Licensing\n");
            sb.Append("{\n");
            sb.Append("    /// <summary>\n");
            sb.Append("    /// 命令许可档位编译期常量表。授权判断只读本表，不读运行时 commands.json 的 tier。\n");
            sb.Append("    /// </summary>\n");
            sb.Append("    public static class CommandTiers\n");
            sb.Append("    {\n");
            if (useRegistry)
            {
                sb.Append("        static CommandTiers()\n");
                sb.Append("        {\n");
                sb.Append("            CommandModuleBootstrap.RegisterAll();\n");
                sb.Append("        }\n");
                sb.Append("\n");
                sb.Append("        public static IReadOnlyDictionary<string, string> Table => CommandModuleRegistry.MergeTiers();\n");
            }
            else
            {
                sb.Append("        public static readonly IReadOnlyDictionary<string, string> Table =\n");
                sb.Append("            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)\n");
                sb.Append("            {\n");
                foreach (var row in rows)
                    sb.Append("                { \"").Append(Cs(row.Key)).Append("\", \"").Append(row.Tier).Append("\" },\n");
                sb.Append("            };\n");
            }
            sb.Append("\n");
            sb.Append("        public static string GetRaw(string key)\n");
            sb.Append("        {\n");
            sb.Append("            if (string.IsNullOrWhiteSpace(key))\n");
            sb.Append("                return null;\n");
            sb.Append("            string value;\n");
            sb.Append("            return Table.TryGetValue(key.Trim(), out value) ? value : null;\n");
            sb.Append("        }\n");
            sb.Append("    }\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        private static string RenderRoutes(List<CommandRow> rows, bool useRegistry)
        {
            var sb = new StringBuilder();
            sb.Append("// <auto-generated />\n");
            sb.Append("// 由 build/tools/GenerateCommandTiers 从模块命令清单汇总生成，不要手改。\n");
            sb.Append("using System;\n");
            sb.Append("using System.Collections.Generic;\n");
            sb.Append("\n");
            sb.Append("namespace HyCADTool.Shell.Licensing\n");
            sb.Append("{\n");
            sb.Append("    /// <summary>编译期命令路由：键 → 类型 / 方法 / 构造参数 / 档位。</summary>\n");
            sb.Append("    public sealed class CommandRoute\n");
            sb.Append("    {\n");
            sb.Append("        public CommandRoute(string key, string type, string method, object[] ctor, string[] ctorEnumTypes, string tier)\n");
            sb.Append("        {\n");
            sb.Append("            Key = key;\n");
            sb.Append("            Type = type;\n");
            sb.Append("            Method = method;\n");
            sb.Append("            Ctor = ctor;\n");
            sb.Append("            CtorEnumTypes = ctorEnumTypes;\n");
            sb.Append("            Tier = tier;\n");
            sb.Append("        }\n");
            sb.Append("\n");
            sb.Append("        public string Key { get; }\n");
            sb.Append("        public string Type { get; }\n");
            sb.Append("        public string Method { get; }\n");
            sb.Append("        public object[] Ctor { get; }\n");
            sb.Append("        public string[] CtorEnumTypes { get; }\n");
            sb.Append("        public string Tier { get; }\n");
            sb.Append("    }\n");
            sb.Append("\n");
            sb.Append("    /// <summary>Production 调度只读本表，不跟运行时 commands.json 的 type/method。</summary>\n");
            sb.Append("    public static class CommandRoutes\n");
            sb.Append("    {\n");
            if (useRegistry)
            {
                sb.Append("        static CommandRoutes()\n");
                sb.Append("        {\n");
                sb.Append("            CommandModuleBootstrap.RegisterAll();\n");
                sb.Append("        }\n");
                sb.Append("\n");
                sb.Append("        public static IReadOnlyDictionary<string, CommandRoute> Table => CommandModuleRegistry.MergeRoutes();\n");
            }
            else
            {
                sb.Append("        public static readonly IReadOnlyDictionary<string, CommandRoute> Table =\n");
                sb.Append("            new Dictionary<string, CommandRoute>(StringComparer.OrdinalIgnoreCase)\n");
                sb.Append("            {\n");
                foreach (var row in rows)
                {
                    sb.Append("                { \"").Append(Cs(row.Key)).Append("\", new CommandRoute(\"")
                        .Append(Cs(row.Key)).Append("\", ")
                        .Append(CsOrNull(row.Type)).Append(", ")
                        .Append(CsOrNull(row.Method)).Append(", ")
                        .Append(RenderObjectArray(row.Ctor)).Append(", ")
                        .Append(RenderStringArray(row.CtorEnumTypes)).Append(", \"")
                        .Append(row.Tier).Append("\") },\n");
                }
                sb.Append("            };\n");
            }
            sb.Append("\n");
            sb.Append("        public static CommandRoute Get(string key)\n");
            sb.Append("        {\n");
            sb.Append("            if (string.IsNullOrWhiteSpace(key))\n");
            sb.Append("                return null;\n");
            sb.Append("            CommandRoute route;\n");
            sb.Append("            return Table.TryGetValue(key.Trim(), out route) ? route : null;\n");
            sb.Append("        }\n");
            sb.Append("    }\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        private static string RenderObjectArray(object[] values)
        {
            if (values == null)
                return "null";
            var sb = new StringBuilder();
            sb.Append("new object[] { ");
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(RenderLiteral(values[i]));
            }
            sb.Append(" }");
            return sb.ToString();
        }

        private static string RenderStringArray(string[] values)
        {
            if (values == null)
                return "null";
            var sb = new StringBuilder();
            sb.Append("new string[] { ");
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(CsOrNull(values[i]));
            }
            sb.Append(" }");
            return sb.ToString();
        }

        private static string RenderLiteral(object value)
        {
            if (value == null)
                return "null";
            if (value is bool b)
                return b ? "true" : "false";
            if (value is int i)
                return i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (value is long l)
                return l.ToString(System.Globalization.CultureInfo.InvariantCulture) + "L";
            if (value is double d)
                return d.ToString("G17", System.Globalization.CultureInfo.InvariantCulture) + "d";
            return "\"" + Cs(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)) + "\"";
        }

        private static string Cs(string value)
        {
            if (value == null)
                return "";
            var sb = new StringBuilder(value.Length + 8);
            foreach (var c in value)
            {
                switch (c)
                {
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        if (c < 32)
                            sb.Append("\\u").Append(((int)c).ToString("X4"));
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        private static string CsOrNull(string value)
        {
            return value == null ? "null" : "\"" + Cs(value) + "\"";
        }

        private static bool WriteIfChanged(string path, string text)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                if (File.Exists(path))
                {
                    var existing = File.ReadAllText(path, Encoding.UTF8).Replace("\r\n", "\n");
                    if (existing == text)
                        return true;
                }

                var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                File.WriteAllText(path, text, utf8);
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("写入 " + path + " 失败: " + ex.Message);
                return false;
            }
        }

        private static bool TouchStamp(string path)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllBytes(path, Array.Empty<byte>());
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("写入 stamp " + path + " 失败: " + ex.Message);
                return false;
            }
        }

        private static FacadeSpec ReadFacadeSpec(string path)
        {
            using (var doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8)))
            {
                var root = doc.RootElement;
                var methods = ReadFacadeMethods(root, "methods");
                var productionOnly = ReadFacadeMethods(root, "productionOnly");
                return new FacadeSpec(methods, productionOnly);
            }
        }

        private static List<FacadeMethod> ReadFacadeMethods(JsonElement root, string property)
        {
            var list = new List<FacadeMethod>();
            if (!root.TryGetProperty(property, out var arr) || arr.ValueKind != JsonValueKind.Array)
                return list;
            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;
                var invoke = item.TryGetProperty("invoke", out var inv) && inv.ValueKind == JsonValueKind.String
                    ? inv.GetString()
                    : null;
                var method = item.TryGetProperty("method", out var meth) && meth.ValueKind == JsonValueKind.String
                    ? meth.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(invoke) || string.IsNullOrWhiteSpace(method))
                    continue;
                var names = new List<string>();
                if (item.TryGetProperty("names", out var namesEl) && namesEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var n in namesEl.EnumerateArray())
                    {
                        if (n.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(n.GetString()))
                            names.Add(n.GetString());
                    }
                }
                if (names.Count == 0)
                    names.Add(invoke);
                string flags = null;
                if (item.TryGetProperty("flags", out var flagsEl) && flagsEl.ValueKind == JsonValueKind.String)
                    flags = flagsEl.GetString();
                list.Add(new FacadeMethod(invoke, method, names, flags));
            }
            return list;
        }

        private static string RenderDevFacade(FacadeSpec spec)
        {
            var sb = new StringBuilder();
            sb.Append("using Autodesk.AutoCAD.Runtime;\n");
            sb.Append("\n");
            sb.Append("[assembly: CommandClass(typeof(HyCADTool.ReCall.CommandFacade))]\n");
            sb.Append("\n");
            sb.Append("namespace HyCADTool.ReCall\n");
            sb.Append("{\n");
            sb.Append("    /// <summary>\n");
            sb.Append("    /// AutoCAD 命令总入口。由 build/tools/GenerateCommandTiers 从 facade-methods.json 生成，不要手改。\n");
            sb.Append("    /// hySeg3 挂在 ReCallClass 上（与 C2 同程序集），不进本门面，避免 eDuplicateKey。\n");
            sb.Append("    /// </summary>\n");
            sb.Append("    public class CommandFacade\n");
            sb.Append("    {\n");
            foreach (var method in spec.Methods)
                sb.Append(RenderDevMethod(method));
            sb.Append("    }\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        private static string RenderProdFacade(FacadeSpec spec)
        {
            var sb = new StringBuilder();
            sb.Append("#if HYCAD_PRODUCTION\n");
            sb.Append("\n");
            sb.Append("using Autodesk.AutoCAD.Runtime;\n");
            sb.Append("using HyCADTool.Shell.Licensing;\n");
            sb.Append("\n");
            sb.Append("namespace HyCADTool.App.Production\n");
            sb.Append("{\n");
            sb.Append("    /// <summary>生产模式 AutoCAD 命令总入口。由 GenerateCommandTiers 从 facade-methods.json 生成，不要手改。</summary>\n");
            sb.Append("    public class ProductionCommandFacade\n");
            sb.Append("    {\n");
            foreach (var method in spec.Methods)
                sb.Append(RenderProdMethod(method));
            foreach (var method in spec.ProductionOnly)
                sb.Append(RenderProdMethod(method));
            sb.Append("    }\n");
            sb.Append("}\n");
            sb.Append("\n");
            sb.Append("#endif\n");
            return sb.ToString();
        }

        private static string RenderDevMethod(FacadeMethod method)
        {
            var sb = new StringBuilder();
            sb.Append("        ");
            foreach (var name in method.Names)
                sb.Append(RenderCommandAttribute(name, method.Flags));
            sb.Append("public void ").Append(method.Method)
                .Append("() => ReCallClass.Invoke(\"").Append(Cs(method.Invoke)).Append("\");\n");
            return sb.ToString();
        }

        private static string RenderProdMethod(FacadeMethod method)
        {
            var sb = new StringBuilder();
            sb.Append("        ");
            foreach (var name in method.Names)
                sb.Append(RenderCommandAttribute(name, method.Flags));
            sb.Append("public void ").Append(method.Method)
                .Append("() => LicenseGate.RunGated(\"").Append(Cs(method.Invoke))
                .Append("\", () => ProductionDispatcher.Invoke(\"").Append(Cs(method.Invoke)).Append("\"));\n");
            return sb.ToString();
        }

        private static string RenderCommandAttribute(string name, string flags)
        {
            if (string.IsNullOrWhiteSpace(flags))
                return "[CommandMethod(\"" + Cs(name) + "\")] ";
            return "[CommandMethod(\"" + Cs(name) + "\", " + flags + ")] ";
        }

        private static List<ModuleRows> ReadModules(string modulesDir)
        {
            if (!Directory.Exists(modulesDir))
            {
                Console.Error.WriteLine("找不到模块目录 " + modulesDir);
                return null;
            }

            var list = new List<ModuleRows>();
            foreach (var dir in Directory.GetDirectories(modulesDir))
            {
                var json = Path.Combine(dir, "commands.json");
                if (!File.Exists(json))
                    continue;
                JsonDocument doc;
                try
                {
                    doc = JsonDocument.Parse(File.ReadAllText(json, Encoding.UTF8));
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("解析 " + json + " 失败: " + ex.Message);
                    return null;
                }

                using (doc)
                {
                    if (!doc.RootElement.TryGetProperty("commands", out var commands)
                        || commands.ValueKind != JsonValueKind.Object)
                    {
                        Console.Error.WriteLine(json + " 缺少 commands 对象。");
                        return null;
                    }

                    var moduleId = Path.GetFileName(dir);
                    if (doc.RootElement.TryGetProperty("module", out var mid)
                        && mid.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(mid.GetString()))
                        moduleId = mid.GetString();

                    var rows = new List<CommandRow>();
                    foreach (var prop in commands.EnumerateObject())
                    {
                        if (prop.Value.ValueKind != JsonValueKind.Object)
                            continue;
                        rows.Add(ReadRow(prop.Name, prop.Value));
                    }
                    rows.Sort(CompareKeyCasefold);
                    list.Add(new ModuleRows(moduleId, rows));
                }
            }

            list.Sort((a, b) => string.CompareOrdinal(a.ModuleId, b.ModuleId));
            return list;
        }

        private static string RenderModules(List<ModuleRows> modules)
        {
            var sb = new StringBuilder();
            sb.Append("// <auto-generated />\n");
            sb.Append("// 由 build/tools/GenerateCommandTiers 从 src/HyCADTool/Modules/*/commands.json 生成，不要手改。\n");
            sb.Append("using System;\n");
            sb.Append("using System.Collections.Generic;\n");
            sb.Append("\n");
            sb.Append("namespace HyCADTool.Shell.Licensing\n");
            sb.Append("{\n");
            sb.Append("    /// <summary>各模块档位表 / 路由表，经 <see cref=\"CommandModuleRegistry\"/> 登记后汇总。</summary>\n");
            sb.Append("    public static class CommandModuleBootstrap\n");
            sb.Append("    {\n");
            sb.Append("        private static readonly object Gate = new object();\n");
            sb.Append("        private static bool _registered;\n");
            sb.Append("\n");
            sb.Append("        public static void RegisterAll()\n");
            sb.Append("        {\n");
            sb.Append("            lock (Gate)\n");
            sb.Append("            {\n");
            sb.Append("                if (_registered) return;\n");
            foreach (var mod in modules)
            {
                var id = ModuleIdent(mod.ModuleId);
                sb.Append("                CommandModuleRegistry.Register(\"").Append(Cs(mod.ModuleId))
                    .Append("\", ").Append(id).Append("Tiers.Table, ").Append(id).Append("Routes.Table);\n");
            }
            sb.Append("                _registered = true;\n");
            sb.Append("            }\n");
            sb.Append("        }\n");
            sb.Append("    }\n");

            foreach (var mod in modules)
            {
                var id = ModuleIdent(mod.ModuleId);
                sb.Append("\n");
                sb.Append("    internal static class ").Append(id).Append("Tiers\n");
                sb.Append("    {\n");
                sb.Append("        internal static readonly IReadOnlyDictionary<string, string> Table =\n");
                sb.Append("            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)\n");
                sb.Append("            {\n");
                foreach (var row in mod.Rows)
                    sb.Append("                { \"").Append(Cs(row.Key)).Append("\", \"").Append(row.Tier).Append("\" },\n");
                sb.Append("            };\n");
                sb.Append("    }\n");
                sb.Append("\n");
                sb.Append("    internal static class ").Append(id).Append("Routes\n");
                sb.Append("    {\n");
                sb.Append("        internal static readonly IReadOnlyDictionary<string, CommandRoute> Table =\n");
                sb.Append("            new Dictionary<string, CommandRoute>(StringComparer.OrdinalIgnoreCase)\n");
                sb.Append("            {\n");
                foreach (var row in mod.Rows)
                {
                    sb.Append("                { \"").Append(Cs(row.Key)).Append("\", new CommandRoute(\"")
                        .Append(Cs(row.Key)).Append("\", ")
                        .Append(CsOrNull(row.Type)).Append(", ")
                        .Append(CsOrNull(row.Method)).Append(", ")
                        .Append(RenderObjectArray(row.Ctor)).Append(", ")
                        .Append(RenderStringArray(row.CtorEnumTypes)).Append(", \"")
                        .Append(row.Tier).Append("\") },\n");
                }
                sb.Append("            };\n");
                sb.Append("    }\n");
            }

            sb.Append("}\n");
            return sb.ToString();
        }

        private static string ModuleIdent(string moduleId)
        {
            var parts = moduleId.Split(new[] { '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();
            foreach (var part in parts)
            {
                if (part.Length == 0) continue;
                sb.Append(char.ToUpperInvariant(part[0]));
                if (part.Length > 1)
                    sb.Append(part.Substring(1));
            }
            return sb.Length == 0 ? "Module" : sb.ToString();
        }

        private sealed class ModuleRows
        {
            public ModuleRows(string moduleId, List<CommandRow> rows)
            {
                ModuleId = moduleId;
                Rows = rows;
            }

            public string ModuleId { get; }
            public List<CommandRow> Rows { get; }
        }

        private sealed class FacadeSpec
        {
            public FacadeSpec(List<FacadeMethod> methods, List<FacadeMethod> productionOnly)
            {
                Methods = methods;
                ProductionOnly = productionOnly;
            }

            public List<FacadeMethod> Methods { get; }
            public List<FacadeMethod> ProductionOnly { get; }
        }

        private sealed class FacadeMethod
        {
            public FacadeMethod(string invoke, string method, List<string> names, string flags)
            {
                Invoke = invoke;
                Method = method;
                Names = names;
                Flags = flags;
            }

            public string Invoke { get; }
            public string Method { get; }
            public List<string> Names { get; }
            public string Flags { get; }
        }

        private sealed class CommandRow
        {
            public CommandRow(string key, string tier, string type, string method, object[] ctor, string[] ctorEnumTypes)
            {
                Key = key;
                Tier = tier;
                Type = type;
                Method = method;
                Ctor = ctor;
                CtorEnumTypes = ctorEnumTypes;
            }

            public string Key { get; }
            public string Tier { get; }
            public string Type { get; }
            public string Method { get; }
            public object[] Ctor { get; }
            public string[] CtorEnumTypes { get; }
        }
    }
}
