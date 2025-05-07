//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Runtime;
//using HyCADTool.Config;
//using System;
//using Exception = Autodesk.AutoCAD.Runtime.Exception;
//[assembly: CommandClass(typeof(HyCADTool.MainPlugin))]
//namespace HyCADTool
//{
//    /// <summary>
//    /// 主插件类，实现 IExtensionApplication 接口，用于管理 AutoCAD 插件的生命周期和事件。
//    /// </summary>
//    public class MainPlugin : IExtensionApplication
//    {
//        private readonly Editor editor = Application.DocumentManager.MdiActiveDocument.Editor;
//        /// <summary>
//        /// 插件初始化方法，在加载插件时自动调用。
//        /// 用于初始化插件设置、注册事件和处理现有文档。
//        /// </summary>
//        public void Initialize()
//        {
//            try
//            {
//                // 初始化插件的样式和配置
//                BaseConfig.InitializeStyle();
//                // 注册应用程序级别的事件
//                RegisterAppEvents();
//                // 为当前已打开的所有文档注册文档级别的事件
//                foreach (Document doc in Application.DocumentManager)
//                {
//                    RegisterDocEvents(doc);
//                }
//                editor.WriteMessage("\nHyCADTool Plugin Initialized.");
//            }
//            catch (Exception ex)
//            {
//                // 捕获并记录初始化过程中的任何异常
//                editor.WriteMessage($"\n初始化失败: {ex.Message}");
//            }
//        }
//        /// <summary>
//        /// 插件终止方法，在卸载插件时自动调用。
//        /// 用于清理资源并注销事件。
//        /// </summary>
//        public void Terminate()
//        {
//            try
//            {
//                // 注销应用程序级别的事件，防止内存泄漏
//                Application.DocumentManager.DocumentActivated -= DocumentManager_DocumentActivated;
//                Application.DocumentManager.DocumentCreated -= DocumentManager_DocumentCreated;
//                editor.WriteMessage("\nHyCADTool Plugin Terminated.");
//            }
//            catch (Exception ex)
//            {
//                // 捕获并记录终止过程中的任何异常
//                editor.WriteMessage($"\n终止失败: {ex.Message}");
//            }
//        }
//        /// <summary>
//        /// 注册应用程序级别的事件（如文档激活和创建事件）。
//        /// </summary>
//        private void RegisterAppEvents()
//        {
//            Application.DocumentManager.DocumentActivated += DocumentManager_DocumentActivated;
//            Application.DocumentManager.DocumentCreated += DocumentManager_DocumentCreated;
//        }
//        /// <summary>
//        /// 注册文档级别的事件和初始化逻辑。
//        /// 在文档创建或加载时调用，确保每个文档初始化插件设置。
//        /// </summary>
//        /// <param name="doc">要注册事件的文档</param>
//        private void RegisterDocEvents(Document doc)
//        {
//            try
//            {
//                // 在文档创建时初始化样式，确保新文档具有正确的配置
//                // 可以在这里添加更多文档特定的事件监听，如对象创建或修改事件
//            }
//            catch (Exception ex)
//            {
//                // 捕获并记录文档事件注册中的异常
//                doc.Editor.WriteMessage($"\n注册文档事件失败: {ex.Message}");
//            }
//        }
//        /// <summary>
//        /// 处理文档激活事件。
//        /// 在文档变为活动文档时触发，可用于更新状态或显示提示。
//        /// 目前留空，供后续扩展。
//        /// </summary>
//        private void DocumentManager_DocumentActivated(object sender, DocumentCollectionEventArgs e)
//        {
//            // 留空，供后续扩展
//            // 示例：e.Document.Editor.WriteMessage($"\n文档已激活: {e.Document.Name}");
//        }
//        /// <summary>
//        /// 处理文档创建事件。
//        /// 在新建或加载 DWG 文件时触发，可用于初始化新文档的设置。
//        /// 目前留空，供后续扩展。
//        /// </summary>
//        private void DocumentManager_DocumentCreated(object sender, DocumentCollectionEventArgs e)
//        {
//            // 自动注册新文档的事件和初始化逻辑
//            //BaseConfig.InitializeStyle();
//            RegisterDocEvents(e.Document);
//            // 示例：e.Document.Editor.WriteMessage($"\n已创建新文档: {e.Document.Name}");
//        }
//    }
//}