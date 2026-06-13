using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCAD.Geometry;
using HyCADTool.Shared.AutoCAD.Extensions;
using HyCADTool.Shared.AutoCAD.Configuration;
using HyCADTool.Shell.Configuration.User;
using HyCADTool.Shared.AutoCAD.Services;
using System.Collections.Generic;

namespace HyCADTool.Features.TitleBlock
{
    /// <summary>
    /// HYMBR / HYMBRN 共享流程（选集、边界收集、矩形生成）。
    /// </summary>
    internal static class MbrCommandCore
    {
        internal const double DefaultDistanceThreshold = 1500.0;
        internal const double DefaultExpandX = 100.0;
        internal const double DefaultExpandY = 100.0;

        internal static string DefaultLayerName =>
            UserLayerNameResolver.Get(LayerSemanticIds.PublicViewport, LayerBuiltinDefaults.PublicViewport);

        internal static (double DistanceThreshold, double ExpandX, double ExpandY)? GetUserInput(Editor ed)
        {
            var distOpt = new PromptDoubleOptions($"\n请输入聚类距离阈值 [默认={DefaultDistanceThreshold}]：")
            {
                AllowNegative = false,
                AllowZero = false,
                DefaultValue = DefaultDistanceThreshold
            };
            var distRes = ed.GetDouble(distOpt);
            if (distRes.Status != PromptStatus.OK)
                return null;

            var expandXOpt = new PromptDoubleOptions($"\n请输入X方向放大距离 [默认={DefaultExpandX}]：")
            {
                AllowNegative = false,
                DefaultValue = DefaultExpandX
            };
            var expandXRes = ed.GetDouble(expandXOpt);
            if (expandXRes.Status != PromptStatus.OK)
                return null;

            var expandYOpt = new PromptDoubleOptions($"\n请输入Y方向放大距离 [默认={DefaultExpandY}]：")
            {
                AllowNegative = false,
                DefaultValue = DefaultExpandY
            };
            var expandYRes = ed.GetDouble(expandYOpt);
            if (expandYRes.Status != PromptStatus.OK)
                return null;

            return (distRes.Value, expandXRes.Value, expandYRes.Value);
        }

        internal static PromptSelectionResult SelectEntities(Editor ed)
        {
            var selOpts = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择图元进行区域聚类与边界框生成："
            };
            return ed.GetSelection(selOpts);
        }

        internal static List<(ObjectId Id, BoundingBox Bounds)> CollectEntityBounds(
            Transaction tr, SelectionSet selection)
        {
            var entityBounds = new List<(ObjectId Id, BoundingBox Bounds)>(selection.Count);

            foreach (SelectedObject selObj in selection)
            {
                if (selObj == null) continue;

                var ent = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Entity;
                if (ent == null) continue;

                try
                {
                    var extents = ent.GeometricExtents;
                    entityBounds.Add((selObj.ObjectId, extents.ToBoundingBox()));
                }
                catch
                {
                    continue;
                }
            }

            return entityBounds;
        }

        internal static void GenerateBoundaryBoxes(
            Transaction tr,
            Database db,
            List<List<(ObjectId Id, BoundingBox Bounds)>> clusters,
            double expandX,
            double expandY)
        {
            var bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            var btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

            foreach (var cluster in clusters)
            {
                if (cluster.Count == 0) continue;

                var mergedBounds = cluster[0].Bounds;
                for (int i = 1; i < cluster.Count; i++)
                    mergedBounds = mergedBounds.Union(cluster[i].Bounds);

                var expandedBounds = mergedBounds.Expand(expandX, expandY);
                var rectangle = CreateRectangleFromBoundingBox(expandedBounds);
                rectangle.Layer = DefaultLayerName;

                btr.AppendEntity(rectangle);
                tr.AddNewlyCreatedDBObject(rectangle, true);
            }
        }

        private static Polyline CreateRectangleFromBoundingBox(BoundingBox box)
        {
            var vertices = box.ToVertices();
            var pline = new Polyline(vertices.Length);

            for (int i = 0; i < vertices.Length; i++)
                pline.AddVertexAt(i, new Point2d(vertices[i].X, vertices[i].Y), 0, 0, 0);

            pline.Closed = true;
            return pline;
        }
    }
}
