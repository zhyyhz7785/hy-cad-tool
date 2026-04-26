using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Domain.ValueObjects.Road;
using HyCADTool.Shared.AutoCAD.Geometry;
using HyCADTool.Presentation.ViewModels.Road;

namespace HyCADTool.Shared.AutoCAD.Workflows.Road
{
    public static class RoadCsPickGeometryInteractor
    {
        public sealed class PickResult
        {
            public double Width { get; set; }
            public double SlopePct { get; set; }
            public double ThicknessCm { get; set; }
            public double ElevationDiff { get; set; }
            /// <summary>拾取实体的当前图层名（用于 rCs 属性面板回显）。</summary>
            public string EntityLayer { get; set; }
        }

        public static PickResult PickAndExtract(TemplateComponentKind kind)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc?.Editor;
            if (ed == null) return null;

            var opts = new PromptEntityOptions("\n选取条带轮廓 Polyline:");
            opts.SetRejectMessage("\n请选择 2D Polyline。");
            opts.AddAllowedClass(typeof(Polyline), exactMatch: true);
            var per = ed.GetEntity(opts);
            if (per.Status != PromptStatus.OK) return null;

            using var tr = doc.TransactionManager.StartOpenCloseTransaction();
            var polyline = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Polyline;
            if (polyline == null)
            {
                return null;
            }

            var domainPolyline = RoadGeometryBridge.ToDomain(polyline);
            if (!PolylineToBandExtractor.TryExtractWithThickness(
                    domainPolyline,
                    kind,
                    BandSide.Left,
                    kind.ToString(),
                    out var extracted,
                    out var error))
            {
                if (!string.IsNullOrWhiteSpace(error))
                    ed.WriteMessage($"\nrCs拾取失败：{error}");
                return null;
            }

            return new PickResult
            {
                Width = extracted.Band.Width,
                SlopePct = extracted.Band.CrossSlopePct,
                ThicknessCm = extracted.ThicknessCm,
                ElevationDiff = extracted.ElevationDiff,
                EntityLayer = polyline.Layer,
            };
        }

        public static void DistributeThickness(BandRowViewModel row, double thicknessCm)
        {
            if (row == null || thicknessCm <= 0) return;
            if (!row.StructureLayers.Any())
            {
                if (row.CanAddSurfaceLayer) row.AddSurfaceLayerCommand.Execute(null);
                if (row.CanAddBaseLayer) row.AddBaseLayerCommand.Execute(null);
                if (row.CanAddSubbaseLayer) row.AddSubbaseLayerCommand.Execute(null);
            }

            var layers = row.StructureLayers.ToList();
            if (layers.Count == 0) return;

            var sum = layers.Sum(l => l.ThicknessCm);
            if (sum <= 0)
            {
                var avg = thicknessCm / layers.Count;
                foreach (var layer in layers) layer.ThicknessCm = avg;
                return;
            }

            var ratio = thicknessCm / sum;
            foreach (var layer in layers)
            {
                layer.ThicknessCm *= ratio;
            }
        }
    }
}
