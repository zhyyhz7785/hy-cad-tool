// HyCADTool/MainPlugin.cs
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Tools;
using System;

[assembly: CommandClass(typeof(HyCADTool.MainPlugin))]

namespace HyCADTool
{
    public class MainPlugin : IExtensionApplication
    {
        public void Initialize()
        {
            try
            {
                WriteMessage("\nHyCADTool 插件初始化中...");

                InitStyles();        // 初始化图层样式
                RegisterAppEvents(); // 注册文档事件

                foreach (Document doc in Application.DocumentManager)
                    RegisterDocEvents(doc);

                WriteMessage("\nHyCADTool 插件初始化完成。");
            }
            catch (System.Exception ex)
            {
                WriteMessage($"\n[错误] 插件初始化失败：{ex.Message}");
            }
        }

        public void Terminate()
        {
            try
            {
                Application.DocumentManager.DocumentActivated -= DocumentManager_DocumentActivated;
                Application.DocumentManager.DocumentCreated -= DocumentManager_DocumentCreated;
                WriteMessage("\nHyCADTool 插件终止。");
            }
            catch (System.Exception ex)
            {
                WriteMessage($"\n[错误] 插件终止失败：{ex.Message}");
            }
        }

        private void RegisterAppEvents()
        {
            Application.DocumentManager.DocumentActivated += DocumentManager_DocumentActivated;
            Application.DocumentManager.DocumentCreated += DocumentManager_DocumentCreated;
        }

        private void RegisterDocEvents(Document doc)
        {
            // 保留扩展接口
        }

        private void DocumentManager_DocumentActivated(object sender, DocumentCollectionEventArgs e) { }
        private void DocumentManager_DocumentCreated(object sender, DocumentCollectionEventArgs e)
        {
            RegisterDocEvents(e.Document);
        }

        /// <summary>
        /// 样式和图层初始化（根据 HyTool 的工具方法）
        /// </summary>
        private void InitStyles()
        {
            try
            {
                using (var tr = Application.DocumentManager.MdiActiveDocument.Database.TransactionManager.StartTransaction())
                {
                    LayerTable lt = (LayerTable)tr.GetObject(
                        Application.DocumentManager.MdiActiveDocument.Database.LayerTableId, OpenMode.ForRead);

                    //CreateLayerIfNotExist(lt, tr, "00_hy_0公共_默认", 7);
                    //CreateLayerIfNotExist(lt, tr, "00_hy_4公共_表格", 7);
                    //CreateLayerIfNotExist(lt, tr, "00_hy_3公共_标注3_引线", 6);

                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                WriteMessage($"\n[样式初始化失败] {ex.Message}");
            }
        }

        private void CreateLayerIfNotExist(LayerTable lt, Transaction tr, string name, short colorIndex)
        {
            if (!lt.Has(name))
            {
                lt.UpgradeOpen();
                LayerTableRecord ltr = new LayerTableRecord
                {
                    Name = name,
                    Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                        Autodesk.AutoCAD.Colors.ColorMethod.ByAci, colorIndex)
                };
                lt.Add(ltr);
                tr.AddNewlyCreatedDBObject(ltr, true);
            }
        }

        private void WriteMessage(string message)
        {
            Application.DocumentManager.MdiActiveDocument?.Editor?.WriteMessage(message);
        }
    }

}