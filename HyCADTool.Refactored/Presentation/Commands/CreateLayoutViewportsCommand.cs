using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System.Collections.Generic;
using System.Linq;
using HyCADTool.Refactored.Domain.ValueObjects.Configuration.User;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Configuration;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 从模型空间 "00_hy_2公共_视口" 图层的闭合多段线生成布局视口（HYMBRC）
    /// </summary>
    public class CreateLayoutViewportsCommand
    {
        private static string ViewportLayerName => UserLayerNameResolver.Get(LayerSemanticIds.PublicViewport, LayerBuiltinDefaults.PublicViewport);

        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            // 视口比例
            var scaleOpts = new PromptDoubleOptions("\n请输入视口比例（如 1:50 输入 0.02）：")
            {
                AllowNegative = false, AllowZero = false, DefaultValue = 0.02
            };
            var scaleRes = ed.GetDouble(scaleOpts);
            if (scaleRes.Status != PromptStatus.OK) return;
            double scale = scaleRes.Value;

            // 视口间距
            var spacingOpts = new PromptDoubleOptions("\n请输入视口间距（默认=10）：")
            {
                AllowNegative = false, AllowZero = true, DefaultValue = 10.0
            };
            var spacingRes = ed.GetDouble(spacingOpts);
            if (spacingRes.Status != PromptStatus.OK) return;
            double spacing = spacingRes.Value;

            try
            {
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                    var ps = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.PaperSpace], OpenMode.ForWrite);

                    // 收集符合条件的多段线
                    var polylines = new List<Polyline>();
                    foreach (ObjectId id in ms)
                    {
                        var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (ent is Polyline pl && pl.Layer == ViewportLayerName && pl.Closed)
                            polylines.Add(pl);
                    }

                    if (polylines.Count == 0)
                    {
                        ed.WriteMessage($"\n未找到图层 {ViewportLayerName} 上的闭合多段线");
                        return;
                    }

                    // 排序：先Y（高到低），再X（左到右）
                    polylines = polylines
                        .OrderByDescending(pl => (pl.GeometricExtents.MinPoint.Y + pl.GeometricExtents.MaxPoint.Y) / 2.0)
                        .ThenBy(pl => (pl.GeometricExtents.MinPoint.X + pl.GeometricExtents.MaxPoint.X) / 2.0)
                        .ToList();

                    double baseX = polylines
                        .Select(pl => (pl.GeometricExtents.MinPoint.X + pl.GeometricExtents.MaxPoint.X) / 2.0)
                        .Min();

                    double currentY = 0;
                    foreach (var pl in polylines)
                    {
                        var ext = pl.GeometricExtents;
                        double wModel = ext.MaxPoint.X - ext.MinPoint.X;
                        double hModel = ext.MaxPoint.Y - ext.MinPoint.Y;
                        var centerModel = new Point3d(
                            (ext.MinPoint.X + ext.MaxPoint.X) / 2.0,
                            (ext.MinPoint.Y + ext.MaxPoint.Y) / 2.0, 0);

                        double vpW = wModel * scale;
                        double vpH = hModel * scale;
                        double cpX = (centerModel.X - baseX) * scale;
                        double cpY = currentY + vpH / 2.0;

                        var vp = new Viewport
                        {
                            CenterPoint = new Point3d(cpX, cpY, 0),
                            Width = vpW,
                            Height = vpH,
                            ViewCenter = new Point2d(centerModel.X, centerModel.Y),
                            ViewTarget = Point3d.Origin,
                            ViewHeight = hModel,
                            CustomScale = scale,
                            Layer = ViewportLayerName
                        };

                        ps.AppendEntity(vp);
                        tr.AddNewlyCreatedDBObject(vp, true);
                        vp.On = true;
                        vp.Locked = true;

                        currentY += vpH + spacing;
                    }

                    tr.Commit();
                    ed.WriteMessage($"\n生成 {polylines.Count} 个视口");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n创建视口失败: {ex.Message}");
            }
        }
    }
}
