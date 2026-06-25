using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCAD.Tables.Inference;
using HyCAD.Tables.Layout;

namespace HyCADTool.Features.Tables.Infrastructure.AutoCad
{
    /// <summary>
    /// 将 AutoCAD 实体归一化为 TableInferInput（AC9 W8）。
    /// </summary>
    public sealed class AcadTableEntityCollector
    {
        public TableInferInput Collect(ObjectId[] ids, Transaction tr)
        {
            if (ids == null)
                throw new ArgumentNullException(nameof(ids));
            if (tr == null)
                throw new ArgumentNullException(nameof(tr));

            var segments = new List<LineSegment2d>();
            var texts = new List<TextBox2d>();

            foreach (var id in ids)
            {
                if (id.IsNull || !id.IsValid)
                    continue;

                var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (entity == null)
                    continue;

                CollectEntity(entity, segments, texts);
            }

            return new TableInferInput(segments, texts);
        }

        public TableInferInput CollectFromSelection(Editor ed)
        {
            if (ed == null)
                throw new ArgumentNullException(nameof(ed));

            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "LINE,POLYLINE,LWPOLYLINE,DBTEXT,MTEXT"),
            });

            var psr = ed.GetSelection(filter);
            if (psr.Status != PromptStatus.OK)
                return null;

            var db = ed.Document.Database;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var ids = new ObjectId[psr.Value.Count];
                for (var i = 0; i < psr.Value.Count; i++)
                    ids[i] = psr.Value[i].ObjectId;

                var input = Collect(ids, tr);
                tr.Commit();
                return input;
            }
        }

        private static void CollectEntity(Entity entity, List<LineSegment2d> segments, List<TextBox2d> texts)
        {
            switch (entity)
            {
                case Line line:
                    segments.Add(new LineSegment2d(
                        line.StartPoint.X,
                        line.StartPoint.Y,
                        line.EndPoint.X,
                        line.EndPoint.Y,
                        GridLineOrientation.Horizontal));
                    break;

                case Polyline polyline:
                    CollectPolyline(polyline, segments);
                    break;

                case DBText dbText:
                    texts.Add(CreateTextBox(dbText.GeometricExtents, dbText.TextString, dbText.Height));
                    break;

                case MText mText:
                    texts.Add(CreateTextBox(mText.GeometricExtents, StripMText(mText.Contents), mText.TextHeight));
                    break;
            }
        }

        private static void CollectPolyline(Polyline polyline, List<LineSegment2d> segments)
        {
            var count = polyline.NumberOfVertices;
            if (count < 2)
                return;

            for (var i = 0; i < count; i++)
            {
                var next = (i + 1) % count;
                if (!polyline.Closed && next == 0)
                    break;

                if (Math.Abs(polyline.GetBulgeAt(i)) > 1e-6)
                    continue;

                var p0 = polyline.GetPoint2dAt(i);
                var p1 = polyline.GetPoint2dAt(next);
                segments.Add(new LineSegment2d(p0.X, p0.Y, p1.X, p1.Y, GridLineOrientation.Horizontal));
            }
        }

        private static TextBox2d CreateTextBox(Extents3d extents, string content, double height)
        {
            var bounds = new LayoutRect(
                extents.MinPoint.X,
                extents.MaxPoint.Y,
                extents.MaxPoint.X,
                extents.MinPoint.Y);
            return new TextBox2d(bounds, content ?? string.Empty, height);
        }

        private static string StripMText(string contents)
        {
            if (string.IsNullOrEmpty(contents))
                return string.Empty;

            return contents
                .Replace("\\P", " ")
                .Replace("{", string.Empty)
                .Replace("}", string.Empty);
        }
    }
}
