//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Runtime;
//using HyCADTool.Config;
//using System;
//using Exception = Autodesk.AutoCAD.Runtime.Exception;
//[assembly: CommandClass(typeof(HyCADTool.MainPlugin))]
//namespace HyCADTool
//{
//    public class MainPlugin : IExtensionApplication
//    {
//        private readonly Editor editor = Application.DocumentManager.MdiActiveDocument.Editor;
//        public void Initialize()
//        {
//            try
//            {
//                //初始化我的软件
//                BaseConfig.InitializeStyle();
//                RegisterAppEvents();
//                foreach (Document doc in Application.DocumentManager)
//                {
//                    RegisterDocEvents(doc);
//                }
//                editor.WriteMessage("\nHyCADTool Plugin Initialized.");
//            }
//            catch (Exception ex)
//            {
//                editor.WriteMessage($"\n初始化失败: {ex.Message}");
//            }
//        }
//        public void Terminate()
//        {
//            try
//            {
//                Application.DocumentManager.DocumentActivated -= DocumentManager_DocumentActivated;
//                Application.DocumentManager.DocumentCreated -= DocumentManager_DocumentCreated;
//                editor.WriteMessage("\nHyCADTool Plugin Terminated.");
//            }
//            catch (Exception ex)
//            {
//                editor.WriteMessage($"\n终止失败: {ex.Message}");
//            }
//        }
//        private void RegisterAppEvents()
//        {
//            Application.DocumentManager.DocumentActivated += DocumentManager_DocumentActivated;
//            Application.DocumentManager.DocumentCreated += DocumentManager_DocumentCreated;
//        }
//        private void RegisterDocEvents(Document doc)
//        {
//            // 留空，供后续扩展文档事件
//        }
//        private void DocumentManager_DocumentActivated(object sender, DocumentCollectionEventArgs e)
//        {
//            // 留空，供后续扩展
//        }
//        private void DocumentManager_DocumentCreated(object sender, DocumentCollectionEventArgs e)
//        {
//            // 留空，供后续扩展
//        }
//    }
//}