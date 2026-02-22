using HyCADTool.Refactored.Domain.Models.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using LayoutResultModel = HyCADTool.TextLayout.LayoutResult;
using LayoutSpecConfig = HyCADTool.TextLayout.DesignSpecConfig;
using SharedBlockParser = HyCADTool.TextLayout.MarkdownBlockParser;
using SharedLayoutEngine = HyCADTool.TextLayout.LayoutEngine;

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
        private const string METHOD_NAME_NON_MODAL = "ShowNonModal";
        private const string METHOD_NAME_IS_NON_MODAL_OPEN = "IsNonModalOpen";
        private const string REGISTER_SYNC_CALLBACKS_METHOD = "RegisterSyncCallbacks";
        private const string CLEAR_SYNC_CALLBACKS_METHOD = "ClearSyncCallbacks";

        public delegate void EditorSyncDataHandler(
            string[] columnContents,
            string[] columnMarkdowns,
            string markdownSource,
            DesignSpecConfig config,
            LayoutResultModel layoutResult);

        private static Assembly _editorAssembly;
        private static MethodInfo _showDialogMethod;
        private static MethodInfo _showNonModalMethod;
        private static MethodInfo _isNonModalOpenMethod;
        private static MethodInfo _registerSyncCallbacksMethod;
        private static MethodInfo _clearSyncCallbacksMethod;
        private static bool _loadAttempted;
        private static string _net8Dir;
        public static string LastError { get; private set; }

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
            out DesignSpecConfig config,
            out LayoutResultModel layoutResult)
        {
            return TryShowEditor(
                markdownText,
                existingConfig,
                ownerHandle,
                null,
                null,
                out columnContents,
                out columnMarkdowns,
                out markdownSource,
                out config,
                out layoutResult);
        }

        public static bool TryShowEditor(
            string markdownText,
            DesignSpecConfig existingConfig,
            long ownerHandle,
            EditorSyncDataHandler onManualSync,
            EditorSyncDataHandler onLiveSync,
            out string[] columnContents,
            out string[] columnMarkdowns,
            out string markdownSource,
            out DesignSpecConfig config,
            out LayoutResultModel layoutResult)
        {
            LastError = null;
            columnContents = null;
            columnMarkdowns = null;
            markdownSource = null;
            config = null;
            layoutResult = null;

            if (!EnsureLoaded())
                return false;

            try
            {
                if (!RegisterSyncCallbacksIfNeeded(onManualSync, onLiveSync))
                    return false;

                string inputJson = BuildInputJson(markdownText, existingConfig);

                // 反射调用 EditorLauncher.ShowDialog(inputJson, ownerHandle)
                string resultJson = (string)_showDialogMethod.Invoke(null, new object[] { inputJson, ownerHandle });
                return TryParseEditorResultJson(
                    resultJson,
                    out columnContents,
                    out columnMarkdowns,
                    out markdownSource,
                    out config,
                    out layoutResult);
            }
            catch (System.Exception ex)
            {
                LastError = $"调用编辑器失败: {ex.Message}";
                return false;
            }
            finally
            {
                try
                {
                    _clearSyncCallbacksMethod?.Invoke(null, null);
                }
                catch
                {
                    // ignore clear failure
                }
            }
        }

        /// <summary>
        /// 以非模态方式打开编辑器，AutoCAD 可继续交互，结果通过回调返回。
        /// </summary>
        public static bool TryShowEditorNonModal(
            string markdownText,
            DesignSpecConfig existingConfig,
            long ownerHandle,
            EditorSyncDataHandler onManualSync,
            EditorSyncDataHandler onLiveSync)
        {
            LastError = null;

            if (!EnsureLoaded())
                return false;

            if (_showNonModalMethod == null)
            {
                LastError = "未找到 ShowNonModal 方法。";
                return false;
            }

            if (IsNonModalEditorOpen())
            {
                LastError = "Markdown 编辑器已打开，请先关闭当前窗口。";
                return false;
            }

            if (!RegisterSyncCallbacksIfNeeded(onManualSync, onLiveSync))
                return false;

            try
            {
                string inputJson = BuildInputJson(markdownText, existingConfig);
                object ret = _showNonModalMethod.Invoke(null, new object[] { inputJson, ownerHandle });
                bool opened = ret is bool b ? b : true;
                if (!opened)
                {
                    LastError = "打开非模态编辑器失败。";
                    _clearSyncCallbacksMethod?.Invoke(null, null);
                    return false;
                }

                return true;
            }
            catch (System.Exception ex)
            {
                LastError = $"打开非模态编辑器失败: {ex.Message}";
                try { _clearSyncCallbacksMethod?.Invoke(null, null); } catch { }
                return false;
            }
        }

        #region 程序集加载

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        private static bool EnsureLoaded()
        {
            if (_showDialogMethod != null)
            {
                return true;
            }

            if (_loadAttempted)
            {
                if (string.IsNullOrEmpty(LastError))
                    LastError = "编辑器加载曾失败且未恢复。";
                return false;
            }

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
                {
                    if (string.IsNullOrWhiteSpace(LastError))
                        LastError = "未找到 HyCADTool.MarkdownEditor.dll。";
                    return false;
                }

                // 预加载 WebView2 原生 DLL（Windows LoadLibrary 不搜索托管程序集目录）
                PreloadNativeDependencies();

                var launcherType = _editorAssembly.GetType(LAUNCHER_TYPE);
                if (launcherType == null)
                {
                    LastError = "未找到 EditorLauncher 类型。";
                    return false;
                }

                _showDialogMethod = launcherType.GetMethod(METHOD_NAME, BindingFlags.Public | BindingFlags.Static);
                _showNonModalMethod = launcherType.GetMethod(METHOD_NAME_NON_MODAL, BindingFlags.Public | BindingFlags.Static);
                _isNonModalOpenMethod = launcherType.GetMethod(METHOD_NAME_IS_NON_MODAL_OPEN, BindingFlags.Public | BindingFlags.Static);
                _registerSyncCallbacksMethod = launcherType.GetMethod(
                    REGISTER_SYNC_CALLBACKS_METHOD,
                    BindingFlags.Public | BindingFlags.Static);
                _clearSyncCallbacksMethod = launcherType.GetMethod(
                    CLEAR_SYNC_CALLBACKS_METHOD,
                    BindingFlags.Public | BindingFlags.Static);
                if (_showDialogMethod == null)
                {
                    LastError = "未找到 ShowDialog 方法。";
                    return false;
                }

                return true;
            }
            catch (System.Exception ex)
            {
                LastError = $"加载编辑器异常: {ex.Message}";
                return false;
            }
        }

        private static string BuildInputJson(string markdownText, DesignSpecConfig existingConfig)
        {
            var input = new
            {
                Markdown = markdownText ?? "",
                Config = existingConfig != null ? ConfigToContract(existingConfig) : null
            };
            return JsonConvert.SerializeObject(input);
        }

        private static bool RegisterSyncCallbacksIfNeeded(
            EditorSyncDataHandler onManualSync,
            EditorSyncDataHandler onLiveSync)
        {
            if (onManualSync == null && onLiveSync == null)
                return true;

            if (_registerSyncCallbacksMethod == null)
            {
                LastError = "MarkdownEditor.dll 缺少 RegisterSyncCallbacks 接口。";
                return false;
            }

            Action<string> manualJsonCallback = json =>
            {
                if (TryParseEditorResultJson(
                    json,
                    out var cbColumnContents,
                    out var cbColumnMarkdowns,
                    out var cbMarkdownSource,
                    out var cbConfig,
                    out var cbLayoutResult))
                {
                    onManualSync?.Invoke(
                        cbColumnContents,
                        cbColumnMarkdowns,
                        cbMarkdownSource,
                        cbConfig,
                        cbLayoutResult);
                }
            };

            Action<string> liveJsonCallback = json =>
            {
                if (TryParseEditorResultJson(
                    json,
                    out var cbColumnContents,
                    out var cbColumnMarkdowns,
                    out var cbMarkdownSource,
                    out var cbConfig,
                    out var cbLayoutResult))
                {
                    onLiveSync?.Invoke(
                        cbColumnContents,
                        cbColumnMarkdowns,
                        cbMarkdownSource,
                        cbConfig,
                        cbLayoutResult);
                }
            };

            try
            {
                _registerSyncCallbacksMethod.Invoke(null, new object[] { manualJsonCallback, liveJsonCallback });
                return true;
            }
            catch (System.Exception ex)
            {
                LastError = $"注册同步回调失败: {ex.Message}";
                return false;
            }
        }

        private static bool IsNonModalEditorOpen()
        {
            if (_isNonModalOpenMethod == null)
                return false;

            try
            {
                object ret = _isNonModalOpenMethod.Invoke(null, null);
                return ret is bool b && b;
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
            try
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
                var asm = Assembly.LoadFrom(dllPath);
                return asm;
            }
            catch (System.Exception ex)
            {
                LastError = $"找到编辑器 DLL 但加载失败: {ex.Message}";
                return null;
            }
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
                cfg.PagePreset,
                cfg.PageWidthMm,
                cfg.PageHeightMm,
                cfg.MarginLeftMm,
                cfg.MarginRightMm,
                cfg.MarginTopMm,
                cfg.MarginBottomMm,
                cfg.ColumnInnerPaddingMm,
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
                cfg.QuoteSpaceAfter,
                cfg.MTextAttachment,
                cfg.MTextLineSpacingStyle,
                cfg.MTextObliquingAngle,
                cfg.MTextCharSpacing,
                cfg.MTextParagraphAlign
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
                ColumnGutter = Val("ColumnGutter", 5.0),
                CharsPerColumn = cpc ?? new[] { 28, 28 },
                TotalHeight = Val("TotalHeight", 350.0),
                TextSize = Val("TextSize", 2.5),
                TextXScale = Val("TextXScale", 1.0),
                PagePreset = StrVal("PagePreset", "A2横向"),
                PageWidthMm = Val("PageWidthMm", 594.0),
                PageHeightMm = Val("PageHeightMm", 420.0),
                MarginLeftMm = Val("MarginLeftMm", 25.0),
                MarginRightMm = Val("MarginRightMm", 10.0),
                MarginTopMm = Val("MarginTopMm", 10.0),
                MarginBottomMm = Val("MarginBottomMm", 10.0),
                ColumnInnerPaddingMm = Val("ColumnInnerPaddingMm", 5.0),
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
                FontFileName = StrVal("FontFileName", "Microsoft YaHei"),
                BigFontFileName = StrVal("BigFontFileName", string.Empty),
                BoldFontName = StrVal("BoldFontName", "Microsoft YaHei"),
                TextStyleName = StrVal("CadSyncStyleName", null),
                H1Scale = Val("H1Scale", 1.6),
                H2Scale = Val("H2Scale", 1.3),
                H3Scale = Val("H3Scale", 1.1),
                LineSpacingFactor = Val("LineSpacingFactor", 1.2),
                ListIndent = Val("ListIndent", 4),
                QuoteIndent = Val("QuoteIndent", 5),
                MTextAttachment = StrVal("MTextAttachment", "TopLeft"),
                MTextLineSpacingStyle = StrVal("MTextLineSpacingStyle", "Exactly"),
                MTextObliquingAngle = Val("MTextObliquingAngle", 0),
                MTextCharSpacing = Val("MTextCharSpacing", 1.0),
                MTextParagraphAlign = StrVal("MTextParagraphAlign", "Left")
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

        #region Result 解析

        private static bool TryParseEditorResultJson(
            string resultJson,
            out string[] columnContents,
            out string[] columnMarkdowns,
            out string markdownSource,
            out DesignSpecConfig config,
            out LayoutResultModel layoutResult)
        {
            columnContents = null;
            columnMarkdowns = null;
            markdownSource = null;
            config = null;
            layoutResult = null;

            if (string.IsNullOrEmpty(resultJson))
                return false;

            JObject result;
            try
            {
                result = JObject.Parse(resultJson);
            }
            catch
            {
                return false;
            }

            if (result == null || result.Value<bool>("Confirmed") == false)
                return false;

            markdownSource = result.Value<string>("Markdown") ?? "";
            config = ContractToConfig(result);
            layoutResult = ParseLayoutResult(result, markdownSource, config);

            string colParaIndices = ExtractColumnParagraphIndices(result, layoutResult);
            int targetColumnCount = Math.Max(1, config?.ColumnCount ?? 1);
            if (layoutResult?.Pages != null && layoutResult.Pages.Length > 0)
            {
                var page0 = layoutResult.Pages[0];
                if (page0?.CharsPerColumn != null && page0.CharsPerColumn.Length > 0)
                    config.CharsPerColumn = page0.CharsPerColumn;
                targetColumnCount = Math.Max(targetColumnCount, page0?.ColumnBlockIndices?.Length ?? 0);
            }

            var colMarkdowns = NormalizeColumnArray(SplitMarkdownByColumns(markdownSource, colParaIndices), targetColumnCount);
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

        #endregion

        #region Markdown 拆分

        private static string ExtractColumnParagraphIndices(JObject result, LayoutResultModel layoutResult)
        {
            if (layoutResult?.Pages != null && layoutResult.Pages.Length > 0)
            {
                string byLayout = layoutResult.ToColumnParagraphIndicesText(0);
                if (!string.IsNullOrWhiteSpace(byLayout))
                    return byLayout;
            }

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

        private static LayoutResultModel ParseLayoutResult(JObject result, string markdownSource, DesignSpecConfig config)
        {
            try
            {
                var token = result?["LayoutResult"];
                if (token != null && token.Type != JTokenType.Null)
                {
                    var parsed = token.ToObject<LayoutResultModel>();
                    if (parsed?.Pages != null && parsed.Pages.Length > 0)
                        return parsed;
                }
            }
            catch
            {
                // ignore and fallback
            }

            try
            {
                var layoutConfig = ToLayoutConfig(config);
                var blocks = SharedBlockParser.ParseTopLevelBlocks(markdownSource ?? "");
                return new SharedLayoutEngine().Distribute(blocks, layoutConfig);
            }
            catch
            {
                return null;
            }
        }

        private static LayoutSpecConfig ToLayoutConfig(DesignSpecConfig cfg)
        {
            var layoutConfig = new LayoutSpecConfig
            {
                Scale = cfg.Scale,
                PreviewScale = cfg.PreviewScale,
                ColumnCount = cfg.ColumnCount,
                ColumnGutter = cfg.ColumnGutter,
                CharsPerColumn = cfg.CharsPerColumn ?? new[] { 28, 28 },
                LineSpacingFactor = cfg.LineSpacingFactor,
                H1Scale = cfg.H1Scale,
                H2Scale = cfg.H2Scale,
                H3Scale = cfg.H3Scale,
                H1SpaceBefore = cfg.H1SpaceBefore,
                H1SpaceAfter = cfg.H1SpaceAfter,
                H2SpaceBefore = cfg.H2SpaceBefore,
                H2SpaceAfter = cfg.H2SpaceAfter,
                H3SpaceBefore = cfg.H3SpaceBefore,
                H3SpaceAfter = cfg.H3SpaceAfter,
                PSpaceAfter = cfg.PSpaceAfter,
                LiSpaceAfter = cfg.LiSpaceAfter,
                QuoteSpaceBefore = cfg.QuoteSpaceBefore,
                QuoteSpaceAfter = cfg.QuoteSpaceAfter,
                ListIndent = cfg.ListIndent,
                QuoteIndent = cfg.QuoteIndent,
                FontFileName = cfg.FontFileName,
                BigFontFileName = cfg.BigFontFileName,
                BoldFontName = cfg.BoldFontName,
                TextSize = cfg.TextSize,
                TextXScale = cfg.TextXScale,
                TotalHeight = cfg.TotalHeight,
                PagePreset = cfg.PagePreset,
                PageWidthMm = cfg.PageWidthMm,
                PageHeightMm = cfg.PageHeightMm,
                MarginLeftMm = cfg.MarginLeftMm,
                MarginRightMm = cfg.MarginRightMm,
                MarginTopMm = cfg.MarginTopMm,
                MarginBottomMm = cfg.MarginBottomMm,
                ColumnInnerPaddingMm = cfg.ColumnInnerPaddingMm
            };
            layoutConfig.Normalize();
            return layoutConfig;
        }

        private static string[] SplitMarkdownByColumns(string markdown, string paraIndices)
        {
            return MarkdownColumnSplitter.SplitByColumnIndices(markdown, paraIndices);
        }

        private static string[] NormalizeColumnArray(string[] source, int targetLength)
        {
            int length = Math.Max(1, targetLength);
            var result = new string[length];
            for (int i = 0; i < length; i++)
                result[i] = (source != null && i < source.Length) ? (source[i] ?? string.Empty) : string.Empty;
            return result;
        }

        #endregion
    }
}
