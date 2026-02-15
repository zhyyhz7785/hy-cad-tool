using HyCADTool.Refactored.Domain.Models.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 动态加载 HyCADTool.MarkdownEditor.dll（net8.0-windows）并调用 Vditor WYSIWYG 编辑器。
    /// 加载失败时返回 false，由调用方提示并中止命令。
    /// </summary>
    public static class EditorLoader
    {
        private const string EDITOR_DLL_NAME = "HyCADTool.MarkdownEditor.dll";
        private const string LAUNCHER_TYPE = "HyCADTool.MarkdownEditor.EditorLauncher";
        private const string METHOD_NAME = "ShowDialog";

        private static Assembly _editorAssembly;
        private static MethodInfo _showDialogMethod;
        private static bool _loadAttempted;
        private static string _net8Dir;

        /// <summary>
        /// 尝试使用 WebView2 编辑器打开 Markdown 编辑对话框。
        /// </summary>
        /// <param name="markdownText">初始 Markdown 文本</param>
        /// <param name="existingConfig">已有配置（二次编辑时传入，新建时为 null）</param>
        /// <param name="ownerHandle">父窗口句柄</param>
        /// <param name="columnContents">输出：编辑后每栏 MText 内容</param>
        /// <param name="markdownSource">输出：编辑后 Markdown 原文</param>
        /// <param name="config">输出：更新后的配置</param>
        /// <returns>true = 用户确认插入；false = 取消或加载失败</returns>
        public static bool TryShowEditor(
            string markdownText,
            DesignSpecConfig existingConfig,
            long ownerHandle,
            out string[] columnContents,
            out string[] columnMarkdowns,
            out string markdownSource,
            out DesignSpecConfig config)
        {
            columnContents = null;
            columnMarkdowns = null;
            markdownSource = null;
            config = null;

            if (!EnsureLoaded())
                return false;

            try
            {
                // 构建输入 JSON
                var input = new
                {
                    Markdown = markdownText ?? "",
                    Config = existingConfig != null ? ConfigToContract(existingConfig) : null
                };
                string inputJson = JsonConvert.SerializeObject(input);

                // 反射调用 EditorLauncher.ShowDialog(inputJson, ownerHandle)
                string resultJson = (string)_showDialogMethod.Invoke(null, new object[] { inputJson, ownerHandle });

                if (string.IsNullOrEmpty(resultJson))
                    return false;

                // 解析结果
                var result = JObject.Parse(resultJson);
                if (result == null || result.Value<bool>("Confirmed") == false)
                    return false;

                markdownSource = result.Value<string>("Markdown") ?? "";
                config = ContractToConfig(result);

                // 按栏拆分 Markdown → 各自渲染 MText
                string colParaIndices = ExtractColumnParagraphIndices(result);
                var colMarkdowns = SplitMarkdownByColumns(markdownSource, colParaIndices);
                columnMarkdowns = colMarkdowns;
                columnContents = new string[colMarkdowns.Length];
                for (int i = 0; i < colMarkdowns.Length; i++)
                {
                    var renderer = new MarkdownToMTextRenderer(config);
                    string markdownWithoutTables = MarkdownTableExtractor.RemoveTopLevelTables(colMarkdowns[i]);
                    columnContents[i] = renderer.Convert(markdownWithoutTables);
                }

                return true;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        #region 程序集加载

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        private static bool EnsureLoaded()
        {
            if (_showDialogMethod != null)
                return true;

            if (_loadAttempted)
                return false;

            _loadAttempted = true;

            try
            {
                // 策略1: 从当前程序集目录加载（NETLOAD 直接部署场景）
                _editorAssembly = TryLoadFromDirectory(
                    Assembly.GetExecutingAssembly().Location);

                // 策略2: 从 ReCall 临时目录加载（C2 热重载场景）
                // ReCall 用 Assembly.Load(byte[]) 加载 Refactored.dll，Location 为空
                // 但 DLL 文件副本在 %TEMP%\HyCADToolRefactored\{ticks}\net8\
                if (_editorAssembly == null)
                    _editorAssembly = TryLoadFromReCallTemp();

                if (_editorAssembly == null)
                    return false;

                // 预加载 WebView2 原生 DLL（Windows LoadLibrary 不搜索托管程序集目录）
                PreloadNativeDependencies();

                var launcherType = _editorAssembly.GetType(LAUNCHER_TYPE);
                if (launcherType == null)
                    return false;

                _showDialogMethod = launcherType.GetMethod(METHOD_NAME, BindingFlags.Public | BindingFlags.Static);
                return _showDialogMethod != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>从指定程序集所在目录加载 MarkdownEditor</summary>
        private static Assembly TryLoadFromDirectory(string assemblyLocation)
        {
            try
            {
                if (string.IsNullOrEmpty(assemblyLocation))
                    return null;

                string baseDir = Path.GetDirectoryName(assemblyLocation);
                if (string.IsNullOrEmpty(baseDir))
                    return null;

                return TryLoadFromBaseDir(baseDir);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>从 ReCall 临时复制目录加载（搜索最近一次 C2 复制的 net8 子目录）</summary>
        private static Assembly TryLoadFromReCallTemp()
        {
            try
            {
                string tempBase = Path.Combine(Path.GetTempPath(), "HyCADToolRefactored");
                if (!Directory.Exists(tempBase))
                    return null;

                // 取最新的临时目录（目录名是 UTC ticks，字符串排序 = 时间排序）
                var dirs = Directory.GetDirectories(tempBase);
                Array.Sort(dirs);

                for (int i = dirs.Length - 1; i >= 0; i--)
                {
                    var asm = TryLoadFromBaseDir(dirs[i]);
                    if (asm != null)
                        return asm;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>在 baseDir 及其 net8 子目录中搜索并加载 MarkdownEditor.dll</summary>
        private static Assembly TryLoadFromBaseDir(string baseDir)
        {
            string[] searchPaths = new[]
            {
                Path.Combine(baseDir, "net8", EDITOR_DLL_NAME),
                Path.Combine(baseDir, EDITOR_DLL_NAME),
            };

            string dllPath = searchPaths.FirstOrDefault(File.Exists);
            if (dllPath == null)
                return null;

            _net8Dir = Path.GetDirectoryName(dllPath);
            AppDomain.CurrentDomain.AssemblyResolve += ResolveEditorDeps;
            return Assembly.LoadFrom(dllPath);
        }

        /// <summary>
        /// 预加载 WebView2Loader.dll 原生 DLL。
        /// Windows LoadLibrary 不搜索托管程序集目录，需用完整路径显式加载。
        /// 搜索顺序：net8 平铺 → net8/runtimes → NuGet 包缓存
        /// </summary>
        private static void PreloadNativeDependencies()
        {
            const string LOADER = "WebView2Loader.dll";
            var paths = new System.Collections.Generic.List<string>();

            // 1. net8 目录（平铺 + runtimes 子目录）
            if (!string.IsNullOrEmpty(_net8Dir))
            {
                paths.Add(Path.Combine(_net8Dir, LOADER));
                paths.Add(Path.Combine(_net8Dir, "runtimes", "win-x64", "native", LOADER));
            }

            // 2. NuGet 包缓存（始终可靠的回退）
            try
            {
                string nugetPkgDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".nuget", "packages", "microsoft.web.webview2");
                if (Directory.Exists(nugetPkgDir))
                {
                    var versions = Directory.GetDirectories(nugetPkgDir);
                    Array.Sort(versions);
                    for (int i = versions.Length - 1; i >= 0; i--)
                        paths.Add(Path.Combine(versions[i], "runtimes", "win-x64", "native", LOADER));
                }
            }
            catch { }

            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    LoadLibrary(path);
                    return;
                }
            }
        }

        private static Assembly ResolveEditorDeps(object sender, ResolveEventArgs args)
        {
            if (string.IsNullOrEmpty(_net8Dir))
                return null;

            string name = new AssemblyName(args.Name).Name + ".dll";
            string path = Path.Combine(_net8Dir, name);
            if (File.Exists(path))
            {
                try { return Assembly.LoadFrom(path); }
                catch { }
            }
            return null;
        }

        #endregion

        #region Config 转换

        private static object ConfigToContract(DesignSpecConfig cfg)
        {
            return new
            {
                DrawScale = cfg.Scale,
                cfg.PreviewScale,
                cfg.ColumnCount,
                cfg.ColumnGutter,
                cfg.CharsPerColumn,
                cfg.TotalHeight,
                cfg.TextSize,
                cfg.TextXScale,
                cfg.FontFileName,
                cfg.BigFontFileName,
                cfg.BoldFontName,
                cfg.H1Scale,
                cfg.H2Scale,
                cfg.H3Scale,
                cfg.LineSpacingFactor,
                cfg.ListIndent,
                cfg.QuoteIndent,
                cfg.H1SpaceBefore,
                cfg.H1SpaceAfter,
                cfg.H2SpaceBefore,
                cfg.H2SpaceAfter,
                cfg.H3SpaceBefore,
                cfg.H3SpaceAfter,
                cfg.PSpaceAfter,
                cfg.LiSpaceAfter,
                cfg.QuoteSpaceBefore,
                cfg.QuoteSpaceAfter
            };
        }

        private static DesignSpecConfig ContractToConfig(JObject result)
        {
            var cfg = result["Config"] as JObject;
            int[] cpc = null;
            try
            {
                var cpcToken = result["CharsPerColumn"] as JArray;
                if (cpcToken != null)
                    cpc = cpcToken.Select(t => (int)t).ToArray();
            }
            catch { }

            if (cpc == null && cfg?["CharsPerColumn"] is JArray cfgCpc)
            {
                try { cpc = cfgCpc.Select(t => (int)t).ToArray(); }
                catch { }
            }
            if (cpc == null && result["PreviewStats"]?["CharsPerColumn"] is JArray statsCpc)
            {
                try { cpc = statsCpc.Select(t => (int)t).ToArray(); }
                catch { }
            }
            if (cpc == null
                && result["PreviewStats"]?["Pages"] is JArray pages
                && pages.Count > 0
                && pages[0]?["CharsPerColumn"] is JArray firstPageCpc)
            {
                try { cpc = firstPageCpc.Select(t => (int)t).ToArray(); }
                catch { }
            }

            double Val(string name, double def) => cfg != null && cfg[name] != null ? (double)cfg[name] : def;
            int IntVal(string name, int def) => cfg != null && cfg[name] != null ? (int)cfg[name] : def;
            string StrVal(string name, string def) => cfg?.Value<string>(name) ?? def;
            double drawScale = Val("DrawScale", Val("Scale", 1.0));

            var config = new DesignSpecConfig
            {
                Scale = drawScale,
                PreviewScale = Val("PreviewScale", 1.0),
                ColumnCount = IntVal("ColumnCount", 2),
                ColumnGutter = Val("ColumnGutter", 10.0),
                CharsPerColumn = cpc ?? new[] { 28, 28 },
                TotalHeight = Val("TotalHeight", 350.0),
                TextSize = Val("TextSize", 2.5),
                TextXScale = Val("TextXScale", 0.7),
                H1SpaceBefore = Val("H1SpaceBefore", 2.0),
                H1SpaceAfter = Val("H1SpaceAfter", 0.8),
                H2SpaceBefore = Val("H2SpaceBefore", 1.5),
                H2SpaceAfter = Val("H2SpaceAfter", 0.6),
                H3SpaceBefore = Val("H3SpaceBefore", 1.2),
                H3SpaceAfter = Val("H3SpaceAfter", 0.4),
                PSpaceAfter = Val("PSpaceAfter", 0.5),
                LiSpaceAfter = Val("LiSpaceAfter", 0.2),
                QuoteSpaceBefore = Val("QuoteSpaceBefore", 0.5),
                QuoteSpaceAfter = Val("QuoteSpaceAfter", 0.5),
                FontFileName = StrVal("FontFileName", "tssdeng.shx"),
                BigFontFileName = StrVal("BigFontFileName", "hztxt.shx"),
                BoldFontName = StrVal("BoldFontName", "SimHei")
            };

            // 参数来源优先级：编辑器结果 > 已存配置；仅缺失字段才回退 Settings
            var vm = ViewModels.SettingsPanelViewModel.Current;
            if (vm != null)
            {
                if (string.IsNullOrWhiteSpace(config.FontFileName))
                    config.FontFileName = vm.FontFileName;
                if (string.IsNullOrWhiteSpace(config.BigFontFileName))
                    config.BigFontFileName = vm.BigFontFileName;
                if (config.TextXScale <= 0)
                    config.TextXScale = vm.TextXScale;
            }

            return config;
        }

        #endregion

        #region Markdown 拆分

        private static string ExtractColumnParagraphIndices(JObject result)
        {
            string direct = result?.Value<string>("ColumnParagraphIndices");
            if (!string.IsNullOrWhiteSpace(direct))
                return direct;

            var stats = result?["PreviewStats"] as JObject;
            if (stats == null)
                return string.Empty;

            string text = stats.Value<string>("ColumnParagraphIndicesText");
            if (!string.IsNullOrWhiteSpace(text))
                return text;

            if (stats["ColumnParagraphIndices"] is JArray colArray)
            {
                var groups = colArray
                    .Select(token => token is JArray row
                        ? string.Join(",", row.Select(v => (int)v))
                        : string.Empty)
                    .ToArray();
                return string.Join("|", groups);
            }

            if (stats["Pages"] is JArray pages
                && pages.Count > 0
                && pages[0] is JObject firstPage)
            {
                string firstText = firstPage.Value<string>("ColumnParagraphIndicesText");
                if (!string.IsNullOrWhiteSpace(firstText))
                    return firstText;

                if (firstPage["ColumnParagraphIndices"] is JArray firstColArray)
                {
                    var firstGroups = firstColArray
                        .Select(token => token is JArray row
                            ? string.Join(",", row.Select(v => (int)v))
                            : string.Empty)
                        .ToArray();
                    return string.Join("|", firstGroups);
                }
            }

            return string.Empty;
        }

        private static string[] SplitMarkdownByColumns(string markdown, string paraIndices)
        {
            return MarkdownColumnSplitter.SplitByColumnIndices(markdown, paraIndices);
        }

        #endregion
    }
}
