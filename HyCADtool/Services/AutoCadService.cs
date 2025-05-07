//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Geometry;
//using HyCADTool.Tools;
//using System;
//using System.Collections.Generic;
//using System.IO;
//namespace HyCADTool.Services
//{
//    public class AutoCadService : Interfaces.ICadService
//    {
//        private readonly Editor _editor;
//        private readonly Database _db;
//        public AutoCadService()
//        {
//            var doc = Application.DocumentManager.MdiActiveDocument;
//            _editor = doc.Editor;
//            _db = doc.Database;
//        }
//        public void WriteMessage(string message)
//        {
//            _editor.WriteMessage(message);
//        }
//        public Polyline SelectPolyline()
//        {
//            return _db.SelectAEntity<Polyline>();
//        }
//        public void CreateMultipleLayers(params (string layerName, short colorIndex)[] layerInfos)
//        {
//            // 这里保留原始实现，假设它由外部调用时已加锁
//            Document doc = Application.DocumentManager.MdiActiveDocument;
//            Database db = doc.Database;
//            using (Transaction tr = db.TransactionManager.StartTransaction())
//            {
//                LayerTable layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
//                foreach (var (layerName, colorIndex) in layerInfos)
//                {
//                    if (layerTable.Has(layerName))
//                    {
//                        doc.Editor.WriteMessage($"\n图层 '{layerName}' 已经存在.");
//                    }
//                    else
//                    {
//                        layerTable.UpgradeOpen();
//                        LayerTableRecord newLayer = new LayerTableRecord
//                        {
//                            Name = layerName,
//                            Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, colorIndex)
//                        };
//                        layerTable.Add(newLayer);
//                        tr.AddNewlyCreatedDBObject(newLayer, true);
//                        doc.Editor.WriteMessage($"\n图层 '{layerName}' 已创建.");
//                    }
//                }
//                tr.Commit();
//            }
//        }
//        public void SetLayer(Entity entity, string newLayerName)
//        {
//            if (entity == null)
//                throw new ArgumentNullException(nameof(entity));
//            if (string.IsNullOrEmpty(newLayerName))
//                throw new ArgumentNullException(nameof(newLayerName));
//            Database db = entity.Database ?? Application.DocumentManager.MdiActiveDocument.Database;
//            ObjectId layerId;
//            using (Transaction tr = db.TransactionManager.StartTransaction())
//            {
//                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
//                if (lt.Has(newLayerName))
//                {
//                    layerId = lt[newLayerName];
//                }
//                else
//                {
//                    LayerTableRecord ltr = new LayerTableRecord { Name = newLayerName };
//                    lt.UpgradeOpen();
//                    layerId = lt.Add(ltr);
//                    tr.AddNewlyCreatedDBObject(ltr, true);
//                }
//                if (!entity.IsWriteEnabled)
//                    entity.UpgradeOpen();
//                entity.LayerId = layerId;
//                tr.Commit();
//            }
//        }
//        public void DrawEntities(IEnumerable<Entity> entities, string layerName)
//        {
//            // 移除 DocumentLock，依赖调用者提供锁
//            using (var tr = _db.TransactionManager.StartTransaction())
//            {
//                var bt = tr.GetObject(_db.BlockTableId, OpenMode.ForRead) as BlockTable;
//                var btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
//                foreach (var entity in entities)
//                {
//                    entity.SetLayer(layerName);
//                    btr.AppendEntity(entity);
//                    tr.AddNewlyCreatedDBObject(entity, true);
//                }
//                tr.Commit();
//            }
//        }
//        public void CreateTable(Point3d insertionPoint, string csvFilePath, double scale)
//        {
//            // 移除 DocumentLock，依赖调用者提供锁
//            using (var tr = _db.TransactionManager.StartTransaction())
//            {
//                var bt = tr.GetObject(_db.BlockTableId, OpenMode.ForRead) as BlockTable;
//                var btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
//                EtGpt.CreateLayer("00_hy_4公共_表格", 7);
//                var table = new Table();
//                table.SetSize(12, 2);
//                table.Position = insertionPoint;
//                double baseColumnWidth = 50;
//                double baseRowHeight = 6;
//                table.Columns[0].Width = baseColumnWidth * scale;
//                table.Columns[1].Width = baseColumnWidth * scale;
//                for (int i = 0; i < 12; i++)
//                {
//                    table.Rows[i].Height = baseRowHeight * scale;
//                }
//                string[] lines = File.ReadAllLines(csvFilePath);
//                for (int i = 0; i < lines.Length && i < 12; i++)
//                {
//                    string[] parts = lines[i].Split(',');
//                    table.Cells[i, 0].TextString = parts[0].Trim();
//                    table.Cells[i, 1].TextString = parts[1].Trim();
//                    double textHeight = 2.5 * scale;
//                    table.Cells[i, 0].TextHeight = textHeight;
//                    table.Cells[i, 1].TextHeight = textHeight;
//                    table.Cells[i, 0].Alignment = CellAlignment.MiddleCenter;
//                    table.Cells[i, 1].Alignment = CellAlignment.MiddleCenter;
//                }
//                table.SetLayer("00_hy_4公共_表格");
//                btr.AppendEntity(table);
//                tr.AddNewlyCreatedDBObject(table, true);
//                tr.Commit();
//            }
//        }
//    }
//}