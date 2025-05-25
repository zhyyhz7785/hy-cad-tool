using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using System.IO;
namespace HyCADTool.Services
{
    public class AutoCadService : Interfaces.ICadService
    {
        public AutoCadService()
        {
            // 构造函数不再初始化 _editor 和 _db，改为在每个方法中动态获取
        }
        private Document GetActiveDocument()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                throw new InvalidOperationException("没有活动的 AutoCAD 文档。");
            }
            return doc;
        }
        public void WriteMessage(string message)
        {
            try
            {
                var doc = GetActiveDocument();
                doc.Editor.WriteMessage(message);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                throw new InvalidOperationException($"无法写入消息: {ex.Message}", ex);
            }
        }
        public Polyline SelectPolyline()
        {
            try
            {
                var doc = GetActiveDocument();
                return doc.Database.SelectAEntity<Polyline>();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                throw new InvalidOperationException($"无法选择多段线: {ex.Message}", ex);
            }
        }
        public void CreateMultipleLayers(params (string layerName, short colorIndex)[] layerInfos)
        {
            var doc = GetActiveDocument();
            var db = doc.Database;
            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    LayerTable layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    foreach (var (layerName, colorIndex) in layerInfos)
                    {
                        if (layerTable.Has(layerName))
                        {
                            doc.Editor.WriteMessage($"\n图层 '{layerName}' 已经存在.");
                        }
                        else
                        {
                            layerTable.UpgradeOpen();
                            LayerTableRecord newLayer = new LayerTableRecord
                            {
                                Name = layerName,
                                Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, colorIndex)
                            };
                            layerTable.Add(newLayer);
                            tr.AddNewlyCreatedDBObject(newLayer, true);
                            doc.Editor.WriteMessage($"\n图层 '{layerName}' 已创建.");
                        }
                    }
                    tr.Commit();
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                throw new InvalidOperationException($"无法创建图层: {ex.Message}", ex);
            }
        }
        public void SetLayer(Entity entity, string newLayerName)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));
            if (string.IsNullOrEmpty(newLayerName))
                throw new ArgumentNullException(nameof(newLayerName));
            var db = entity.Database ?? GetActiveDocument().Database;
            ObjectId layerId;
            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    if (lt.Has(newLayerName))
                    {
                        layerId = lt[newLayerName];
                    }
                    else
                    {
                        LayerTableRecord ltr = new LayerTableRecord { Name = newLayerName };
                        lt.UpgradeOpen();
                        layerId = lt.Add(ltr);
                        tr.AddNewlyCreatedDBObject(ltr, true);
                    }
                    if (!entity.IsWriteEnabled)
                        entity.UpgradeOpen();
                    entity.LayerId = layerId;
                    tr.Commit();
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                throw new InvalidOperationException($"无法设置图层: {ex.Message}", ex);
            }
        }
        public void DrawEntities(IEnumerable<Entity> entities, string layerName)
        {
            var doc = GetActiveDocument();
            var db = doc.Database;
            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    var btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    foreach (var entity in entities)
                    {
                        entity.SetLayer(layerName);
                        btr.AppendEntity(entity);
                        tr.AddNewlyCreatedDBObject(entity, true);
                    }
                    tr.Commit();
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                throw new InvalidOperationException($"无法绘制实体: {ex.Message}", ex);
            }
        }
        public void CreateTable(Point3d insertionPoint, string csvFilePath, double scale)
        {
            var doc = GetActiveDocument();
            var db = doc.Database;
            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    var btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    Tools.Tools.CreateLayer("00_hy_4公共_表格", 7);
                    var table = new Table();
                    table.SetSize(12, 2);
                    table.Position = insertionPoint;
                    double baseColumnWidth = 50;
                    double baseRowHeight = 6;
                    table.Columns[0].Width = baseColumnWidth * scale;
                    table.Columns[1].Width = baseColumnWidth * scale;
                    for (int i = 0; i < 12; i++)
                    {
                        table.Rows[i].Height = baseRowHeight * scale;
                    }
                    string[] lines = File.ReadAllLines(csvFilePath);
                    for (int i = 0; i < lines.Length && i < 12; i++)
                    {
                        string[] parts = lines[i].Split(',');
                        table.Cells[i, 0].TextString = parts[0].Trim();
                        table.Cells[i, 1].TextString = parts[1].Trim();
                        double textHeight = 2.5 * scale;
                        table.Cells[i, 0].TextHeight = textHeight;
                        table.Cells[i, 1].TextHeight = textHeight;
                        table.Cells[i, 0].Alignment = CellAlignment.MiddleCenter;
                        table.Cells[i, 1].Alignment = CellAlignment.MiddleCenter;
                    }
                    table.SetLayer("00_hy_4公共_表格");
                    btr.AppendEntity(table);
                    tr.AddNewlyCreatedDBObject(table, true);
                    tr.Commit();
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                throw new InvalidOperationException($"无法创建表格: {ex.Message}", ex);
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException($"无法读取 CSV 文件: {ex.Message}", ex);
            }
        }
    }
}