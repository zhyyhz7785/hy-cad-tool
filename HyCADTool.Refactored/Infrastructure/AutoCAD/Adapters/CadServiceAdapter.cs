using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Interfaces;
using System.Collections.Generic;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Adapters
{
    /// <summary>
    /// CAD 服务适配器实现
    /// 桥接原项目接口和重构项目的实现
    /// </summary>
    public class CadServiceAdapter : ICadService
    {
        public void WriteMessage(string message)
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage(message);
        }

        public Polyline SelectPolyline()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var db = doc.Database;

            var options = new PromptEntityOptions("\n请选择一个多段线: ");
            options.SetRejectMessage("\n选择的不是多段线，请重新选择。");
            options.AddAllowedClass(typeof(Polyline), true);

            var result = ed.GetEntity(options);
            if (result.Status != PromptStatus.OK)
                return null;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var polyline = tr.GetObject(result.ObjectId, OpenMode.ForRead) as Polyline;
                tr.Commit();
                return polyline?.Clone() as Polyline;
            }
        }

        public void DrawEntities(IEnumerable<Entity> entities, string layerName)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                var btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                // 确保图层存在
                var lt = tr.GetObject(db.LayerTableId, OpenMode.ForWrite) as LayerTable;
                if (!lt.Has(layerName))
                {
                    var ltr = new LayerTableRecord { Name = layerName };
                    lt.Add(ltr);
                    tr.AddNewlyCreatedDBObject(ltr, true);
                }

                // 添加实体
                foreach (var entity in entities)
                {
                    entity.Layer = layerName;
                    btr.AppendEntity(entity);
                    tr.AddNewlyCreatedDBObject(entity, true);
                }

                tr.Commit();
            }
        }

        public void CreateTable(Point3d insertionPoint, string csvFilePath, double scale)
        {
            // 简化实现 - 创建一个文本提示
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                var btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                var text = new DBText
                {
                    TextString = $"表格: {csvFilePath}",
                    Position = insertionPoint,
                    Height = 100 * scale
                };

                btr.AppendEntity(text);
                tr.AddNewlyCreatedDBObject(text, true);
                tr.Commit();
            }
        }
    }
}

