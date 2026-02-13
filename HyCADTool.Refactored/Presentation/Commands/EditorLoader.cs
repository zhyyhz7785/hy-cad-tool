using HyCADTool.Refactored.Domain.Models.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 动态加载 HyCADTool.MarkdownEditor.dll（net8.0-windows）并调用 Vditor WYSIWYG 编辑器。
    /// 加载失败时返回 false，由调用方回退到旧版编辑器。
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
            out string markdownSource,
            out DesignSpecConfig config)
        {
            columnContents = null;
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
                string colParaIndices = result.Value<string>("ColumnParagraphIndices");
                var colMarkdowns = SplitMarkdownByColumns(markdownSource, colParaIndices);
                columnContents = new string[colMarkdowns.Length];
                for (int i = 0; i < colMarkdowns.Length; i++)
                {
                    var renderer = new MarkdownToMTextRenderer(config);
                    columnContents[i] = renderer.Convert(colMarkdowns[i]);
                }

                return true;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        #region 程序集加载

        private static bool EnsureLoaded()
        {
            if (_showDialogMethod != null)
                return true;

            if (_loadAttempted)
                return false;

            _loadAttempted = true;

            try
            {
                string baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrEmpty(baseDir))
                    return false;

                // 搜索 net8 子目录
                string[] searchPaths = new[]
                {
                    Path.Combine(baseDir, "net8", EDITOR_DLL_NAME),
                    Path.Combine(baseDir, EDITOR_DLL_NAME),
                };

                string dllPath = searchPaths.FirstOrDefault(File.Exists);
                if (dllPath == null)
                    return false;

                _net8Dir = Path.GetDirectoryName(dllPath);

                // 注册 AssemblyResolve 以解析 net8 子目录中的依赖
                AppDomain.CurrentDomain.AssemblyResolve += ResolveEditorDeps;

                _editorAssembly = Assembly.LoadFrom(dllPath);

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
                cfg.Scale,
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

            double Val(string name, double def) => cfg != null && cfg[name] != null ? (double)cfg[name] : def;
            int IntVal(string name, int def) => cfg != null && cfg[name] != null ? (int)cfg[name] : def;
            string StrVal(string name, string def) => cfg?.Value<string>(name) ?? def;

            var config = new DesignSpecConfig
            {
                Scale = Val("Scale", 1.0),
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

            // 从 SettingsPanel 补充字体信息
            var vm = ViewModels.SettingsPanelViewModel.Current;
            if (vm != null)
            {
                config.FontFileName = vm.FontFileName;
                config.BigFontFileName = vm.BigFontFileName;
                config.TextXScale = vm.TextXScale;
            }

            return config;
        }

        #endregion

        #region Markdown 拆分

        private static string[] SplitMarkdownByColumns(string markdown, string paraIndices)
        {
            if (string.IsNullOrEmpty(markdown))
                return new[] { "" };

            var blocks = SplitMarkdownBlocks(markdown);

            if (string.IsNullOrEmpty(paraIndices))
                return new[] { markdown };

            var colGroups = paraIndices.Split('|');
            var result = new string[colGroups.Length];

            for (int c = 0; c < colGroups.Length; c++)
            {
                if (string.IsNullOrWhiteSpace(colGroups[c]))
                {
                    result[c] = "";
                    continue;
                }

                var indices = colGroups[c].Split(',')
                    .Select(s => { int v; return int.TryParse(s.Trim(), out v) ? v : -1; })
                    .Where(v => v >= 0 && v < blocks.Length)
                    .ToArray();

                result[c] = string.Join("\n\n", indices.Select(i => blocks[i]));
            }

            return result;
        }

        private static string[] SplitMarkdownBlocks(string markdown)
        {
            var lines = markdown.Replace("\r\n", "\n").Split('\n');
            var blocks = new System.Collections.Generic.List<string>();
            var current = new System.Text.StringBuilder();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    if (current.Length > 0)
                    {
                        blocks.Add(current.ToString().TrimEnd());
                        current.Clear();
                    }
                }
                else
                {
                    if (current.Length > 0) current.Append('\n');
                    current.Append(line);
                }
            }
            if (current.Length > 0)
                blocks.Add(current.ToString().TrimEnd());

            return blocks.ToArray();
        }

        #endregion
    }
}
