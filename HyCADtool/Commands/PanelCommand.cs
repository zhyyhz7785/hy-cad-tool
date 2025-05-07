//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Runtime;
//using Autodesk.AutoCAD.Windows;
//using HyCADTool.Command;
//using HyCADTool.Interfaces;
//using HyCADTool.Services;
//using HyCADTool.Views;
//using System.Windows.Threading;
//using Exception = Autodesk.AutoCAD.Runtime.Exception;
//[assembly: CommandClass(typeof(HyCommand))]
//namespace HyCADTool.Command
//{
//    /// <summary>
//    /// 管理 AutoCAD 插件的命令类，提供显示 HY 面板的命令。
//    /// </summary>
//    public partial class HyCommand
//    {
//        private static PaletteSet ps; // 静态面板实例，确保单例
//        private static ICadService _cadService;
//        private static IAreaFactory _areaFactory;
//        private static IConfigService _configService;
//        /// <summary>
//        /// 初始化依赖服务（在插件加载时调用）
//        /// </summary>
//        public static void InitializeServices()
//        {
//            _cadService = new AutoCadService();
//            _configService = new ConfigService();
//            _areaFactory = new AreaFactory(_cadService, _configService);
//        }
//        /// <summary>
//        /// 显示 HY 面板的命令，通过 PaletteSet 显示钢筋、过滤器、基础钢筋和桩的 WPF 控件。
//        /// </summary>
//        [CommandMethod("hy", CommandFlags.Modal | CommandFlags.UsePickSet)]
//        public static void ShowPanel()
//        {
//            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
//            try
//            {
//                // 确保服务已初始化
//                if (_cadService == null || _areaFactory == null || _configService == null)
//                {
//                    InitializeServices();
//                }
//                // 确保在 UI 线程上操作
//                if (Dispatcher.CurrentDispatcher.CheckAccess())
//                {
//                    DisplayPalette(ed);
//                }
//                else
//                {
//                    Dispatcher.CurrentDispatcher.Invoke(() => DisplayPalette(ed));
//                }
//            }
//            catch (Exception ex)
//            {
//                // 捕获所有异常，确保 AutoCAD 稳定运行
//                ed.WriteMessage($"\n显示面板失败: {ex.Message}");
//            }
//        }
//        /// <summary>
//        /// 创建并显示 PaletteSet 面板，添加 WPF 控件。
//        /// </summary>
//        /// <param name="ed">当前文档的编辑器，用于输出消息</param>
//        private static void DisplayPalette(Editor ed)
//        {
//            if (ps == null)
//            {
//                ps = new PaletteSet("HY的面板")
//                {
//                    Style = PaletteSetStyles.ShowAutoHideButton | PaletteSetStyles.ShowCloseButton | PaletteSetStyles.Snappable
//                };
//                ps.AddVisual("钢筋", new ReinPanel());
//                ps.AddVisual("过滤器", new FilterPanel());
//                ps.AddVisual("基础钢筋", new BaseReinPanel());
//                ps.AddVisual("桩", new PilePanel(_cadService, _areaFactory, _configService));
//                // 注入依赖到 PilePanel
//            }
//            ps.Visible = true;
//            ed.WriteMessage("\nHY 面板显示成功。");
//        }
//    }
//}