using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using HyCADTool.MarkdownEditor.Models;
using HyCADTool.MarkdownEditor.Views;
using HyCADTool.TextLayout;
using Newtonsoft.Json;

namespace HyCADTool.MarkdownEditor.TestHost
{
    /// <summary>
    /// MarkdownEditor 独立测试宿主
    /// 
    /// 用法：
    ///   1. 在 VS 中将此项目设为启动项目
    ///   2. F5 启动 → 编辑器窗口直接弹出
    ///   3. 操作编辑器 → 关闭后控制台显示结果
    ///   4. 修改 MarkdownEditor 代码 → 重新 F5，无需 AutoCAD
    /// 
    /// 切换测试场景：修改下方 BuildTestInput() 即可
    /// </summary>
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (args != null && args.Any(a => string.Equals(a, "--regression", StringComparison.OrdinalIgnoreCase)))
            {
                RunLayoutRegression();
                return;
            }

            // 分配控制台（WinExe 模式下默认无控制台，手动附加用于输出）
            AllocConsole();

            Console.WriteLine("═══════════════════════════════════════");
            Console.WriteLine("  HyCADTool.MarkdownEditor 独立测试");
            Console.WriteLine("═══════════════════════════════════════");
            Console.WriteLine();

            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;

            // ★ 构建测试输入（改这里切换测试场景）
            var input = BuildTestInput();

            Console.WriteLine($"[启动] 栏数={input.Config.ColumnCount}, 比例=1:{input.Config.DrawScale}");
            Console.WriteLine($"[启动] Markdown 长度={input.Markdown?.Length ?? 0} 字符");
            Console.WriteLine();

            var sw = Stopwatch.StartNew();
            var window = new EditorWindow(input);

            window.Closed += (s, e) =>
            {
                sw.Stop();
                Console.WriteLine();
                Console.WriteLine($"[耗时] 编辑器打开 {sw.ElapsedMilliseconds}ms");
                PrintResult(window.Result);

                Console.WriteLine();
                Console.WriteLine("按任意键退出...");
                Console.ReadKey();
            };

            app.Run(window);
        }

        /// <summary>
        /// ★ 测试入口：修改此方法切换测试场景
        /// </summary>
        static EditorInput BuildTestInput()
        {
            // ── 场景 1：空白新建（默认）──
            return new EditorInput();

            // ── 场景 2：带初始 Markdown ──
            // return new EditorInput
            // {
            //     Markdown = "# 测试标题\n\n这是测试内容。\n\n## 二级标题\n\n1. 列表项1\n2. 列表项2"
            // };

            // ── 场景 3：模拟二次编辑（带配置）──
            // return new EditorInput
            // {
            //     Markdown = "# 已有说明\n\n内容...",
            //     Config = new EditorConfig
            //     {
            //         Scale = 40,
            //         ColumnCount = 3,
            //         ColumnGutter = 15,
            //         TextSize = 2.5
            //     }
            // };
        }

        static void RunLayoutRegression()
        {
            AllocConsole();
            Console.WriteLine("开始执行 LayoutEngine RegressionSamples 校验...");

            string baseDir = AppContext.BaseDirectory;
            string samplesDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "HyCADTool.MarkdownEditor", "RegressionSamples"));
            if (!Directory.Exists(samplesDir))
            {
                Console.WriteLine($"样本目录不存在: {samplesDir}");
                return;
            }

            var sampleFiles = Directory.GetFiles(samplesDir, "*.md", SearchOption.TopDirectoryOnly)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (sampleFiles.Length == 0)
            {
                Console.WriteLine("未找到回归样本。");
                return;
            }

            int passed = 0;
            int failed = 0;
            var engine = new LayoutEngine();
            foreach (string file in sampleFiles)
            {
                string name = Path.GetFileName(file);
                try
                {
                    string markdown = File.ReadAllText(file);
                    var cfg = BuildRegressionLayoutConfig();
                    var blocks = MarkdownBlockParser.ParseTopLevelBlocks(markdown);
                    var result = engine.Distribute(blocks, cfg);
                    var issues = ValidateLayout(result, blocks.Count);
                    if (issues.Count == 0)
                    {
                        passed++;
                        Console.WriteLine($"[PASS] {name}  pages={result.PageCount}");
                    }
                    else
                    {
                        failed++;
                        Console.WriteLine($"[FAIL] {name}  {string.Join("; ", issues)}");
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.WriteLine($"[FAIL] {name}  {ex.Message}");
                }
            }

            Console.WriteLine($"回归结束：PASS={passed}, FAIL={failed}, TOTAL={sampleFiles.Length}");
        }

        static DesignSpecConfig BuildRegressionLayoutConfig()
        {
            return new DesignSpecConfig
            {
                Scale = 1.0,
                PreviewScale = 1.0,
                ColumnCount = 2,
                CharsPerColumn = new[] { 28, 28 },
                ColumnGutter = 0,
                TextSize = 2.5,
                TextXScale = 0.7,
                LineSpacingFactor = 1.2,
                TotalHeight = 350,
                PageWidthMm = 594,
                PageHeightMm = 420,
                MarginLeftMm = 25,
                MarginRightMm = 10,
                MarginTopMm = 10,
                MarginBottomMm = 10
            };
        }

        static List<string> ValidateLayout(LayoutResult result, int blockCount)
        {
            var issues = new List<string>();
            if (result == null)
            {
                issues.Add("result=null");
                return issues;
            }
            if (result.Pages == null || result.Pages.Length == 0)
                issues.Add("pages=0");

            var indexSet = new HashSet<int>();
            if (result.Pages != null)
            {
                foreach (var page in result.Pages)
                {
                    var cols = page?.ColumnBlockIndices;
                    if (cols == null) continue;
                    foreach (var col in cols)
                    {
                        if (col == null) continue;
                        foreach (var idx in col)
                            indexSet.Add(idx);
                    }
                }
            }

            if (blockCount > 0 && indexSet.Count == 0)
                issues.Add("no-block-mapped");

            return issues;
        }

        static void PrintResult(EditorResult result)
        {
            Console.WriteLine("───────────── 结果 ─────────────");

            if (result == null || !result.Confirmed)
            {
                Console.WriteLine("[取消] 用户未确认");
                return;
            }

            Console.WriteLine($"[确认] Markdown 长度 = {result.Markdown?.Length ?? 0}");
            Console.WriteLine($"[配置] 栏数={result.Config?.ColumnCount}, 比例=1:{result.Config?.DrawScale}");
            Console.WriteLine($"[配置] 字高={result.Config?.TextSize}, X比例={result.Config?.TextXScale}");

            if (result.CharsPerColumn != null)
                Console.WriteLine($"[分栏] 每栏字符数 = [{string.Join(", ", result.CharsPerColumn)}]");

            if (!string.IsNullOrEmpty(result.ColumnParagraphIndices))
                Console.WriteLine($"[分栏] 段落索引 = {result.ColumnParagraphIndices}");

            // 输出 Markdown 前 200 字符预览
            if (!string.IsNullOrEmpty(result.Markdown))
            {
                string preview = result.Markdown.Length > 200
                    ? result.Markdown.Substring(0, 200) + "..."
                    : result.Markdown;
                Console.WriteLine($"\n[Markdown 预览]\n{preview}");
            }

            // 完整 JSON（方便调试 EditorLoader 的解析逻辑）
            Console.WriteLine($"\n[完整 JSON]\n{JsonConvert.SerializeObject(result, Formatting.Indented)}");
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern bool AllocConsole();
    }
}
