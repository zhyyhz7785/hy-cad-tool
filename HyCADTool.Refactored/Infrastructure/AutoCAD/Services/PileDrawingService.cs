using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Domain.Entities.Pile;
using HyCADTool.Refactored.Domain.Services;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Extensions;
using System;
using System.Collections.Generic;
using System.IO;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services
{
    /// <summary>
    /// 桩绘制服务（Infrastructure 层）
    /// 将 PileLayoutResult 绘制到 AutoCAD 图纸
    /// </summary>
    public class PileDrawingService
    {
        private const string LAYER_PILE = "02_hy_1桩_主";
        private const string LAYER_GRID = "02_hy_3桩_地基内轮廓";
        private const string LAYER_ANNOTATION = "00_hy_3公共_标注3_引线";

        /// <summary>
        /// 绘制桩布置结果
        /// </summary>
        public void Draw(PileLayoutService.PileLayoutResult result)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            if (result.Error != null)
            {
                ed.WriteMessage($"\n桩布置错误: {result.Error}");
                return;
            }

            if (result.PilePoints.Count == 0)
            {
                ed.WriteMessage("\n无桩点可绘制。");
                return;
            }

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                // 图层已在 PluginInitializer 统一创建

                // 1. 绘制桩（圆或方）
                DrawPileEntities(tr, btr, result);

                // 2. 绘制小矩形网格
                DrawSmallRects(tr, btr, result.SmallRects);

                // 3. 绘制内缩矩形
                DrawRect(tr, btr, result.InsetRect, LAYER_GRID);

                // 4. 桩编号标注
                DrawPileAnnotations(tr, btr, result);

                // 5. 参数表
                DrawSummaryTable(tr, btr, result);

                tr.Commit();
            }

            ed.WriteMessage($"\n桩绘制完成：{result.PilePoints.Count} 根桩" +
                $"，nX={result.NX}，nY={result.NY}" +
                $"，实际置换率={result.ActualDisplacementRate:P2}");
        }

        #region 绘制实体

        private void DrawPileEntities(Transaction tr, BlockTableRecord btr, PileLayoutService.PileLayoutResult result)
        {
            foreach (var pt in result.PilePoints)
            {
                Entity ent;
                if (result.SectionType == PileSectionType.Circle)
                {
                    ent = new Circle(
                        new Point3d(pt.X, pt.Y, 0),
                        Vector3d.ZAxis,
                        result.DiameterOrEdge / 2);
                }
                else
                {
                    var pl = new Polyline();
                    double h = result.DiameterOrEdge / 2;
                    pl.AddVertexAt(0, new Point2d(pt.X - h, pt.Y - h), 0, 0, 0);
                    pl.AddVertexAt(1, new Point2d(pt.X + h, pt.Y - h), 0, 0, 0);
                    pl.AddVertexAt(2, new Point2d(pt.X + h, pt.Y + h), 0, 0, 0);
                    pl.AddVertexAt(3, new Point2d(pt.X - h, pt.Y + h), 0, 0, 0);
                    pl.Closed = true;
                    ent = pl;
                }
                ent.Layer = LAYER_PILE;
                btr.AppendEntity(ent);
                tr.AddNewlyCreatedDBObject(ent, true);
            }
        }

        private void DrawSmallRects(Transaction tr, BlockTableRecord btr, List<PileLayoutService.Rect> rects)
        {
            foreach (var r in rects)
            {
                var pl = CreatePolylineFromRect(r);
                pl.Layer = LAYER_GRID;
                btr.AppendEntity(pl);
                tr.AddNewlyCreatedDBObject(pl, true);
            }
        }

        private void DrawRect(Transaction tr, BlockTableRecord btr, PileLayoutService.Rect rect, string layer)
        {
            var pl = CreatePolylineFromRect(rect);
            pl.Layer = layer;
            btr.AppendEntity(pl);
            tr.AddNewlyCreatedDBObject(pl, true);
        }

        private void DrawPileAnnotations(Transaction tr, BlockTableRecord btr, PileLayoutService.PileLayoutResult result)
        {
            double scale = result.Scale;
            int id = 1;
            foreach (var pt in result.PilePoints)
            {
                var cadPt = new Point3d(pt.X, pt.Y, 0);
                var textPt = new Point3d(pt.X + 5 * scale, pt.Y + 5 * scale, 0);

                var leader = new Leader();
                leader.AppendVertex(cadPt);
                leader.AppendVertex(textPt);
                leader.HasArrowHead = true;
                leader.Layer = LAYER_ANNOTATION;
                btr.AppendEntity(leader);
                tr.AddNewlyCreatedDBObject(leader, true);

                var mtext = new MText();
                mtext.Contents = id.ToString();
                mtext.Location = new Point3d(pt.X + 6 * scale, pt.Y + 6 * scale, 0);
                mtext.TextHeight = 2.5 * scale;
                mtext.Layer = LAYER_ANNOTATION;
                btr.AppendEntity(mtext);
                tr.AddNewlyCreatedDBObject(mtext, true);

                id++;
            }
        }

        private void DrawSummaryTable(Transaction tr, BlockTableRecord btr, PileLayoutService.PileLayoutResult result)
        {
            double scale = result.Scale;
            double pileArea = result.SectionType == PileSectionType.Circle
                ? Math.PI * Math.Pow(result.DiameterOrEdge / 2, 2)
                : result.DiameterOrEdge * result.DiameterOrEdge;
            double contourArea = result.ContourRect.Area;
            double calcPiles = contourArea * result.InputDisplacementRate / pileArea;

            // 插入点：轮廓左上角偏移
            var insertPt = new Point3d(
                result.ContourRect.X0 - 100 * scale,
                result.ContourRect.Y1 + 100 * scale, 0);

            var table = new Table();
            table.TableStyle = Application.DocumentManager.MdiActiveDocument.Database.Tablestyle;
            table.SetSize(12, 2);

            for (int r = 0; r < 12; r++)
            {
                for (int c = 0; c < 2; c++)
                {
                    try
                    {
                        var range = table.Cells[r, c].GetMergeRange();
                        if (range.TopRow != range.BottomRow || range.LeftColumn != range.RightColumn)
                            table.UnmergeCells(range);
                    }
                    catch
                    {
                        // 仅用于消除默认标题行自动合并。
                    }
                }
            }

            table.SetRowHeight(2.5 * scale);
            table.SetColumnWidth(15 * scale);

            var data = new (string name, string value)[]
            {
                ("参数名称", "值"),
                ("布置类型", result.ArrangementType == PileArrangementType.Rectangle ? "矩形" : "梅花形"),
                ("桩直径/边长", result.DiameterOrEdge.ToString("F0")),
                ("桩面积", pileArea.ToString("F2")),
                ("X 方向桩数", result.NX.ToString()),
                ("Y 方向桩数", result.NY.ToString()),
                ("X 间距", result.CellWidth.ToString("F2")),
                ("Y 间距", result.CellHeight.ToString("F2")),
                ("计算总桩数", calcPiles.ToString("F2")),
                ("实际总桩数", result.PilePoints.Count.ToString()),
                ("输入置换率", result.InputDisplacementRate.ToString("F4")),
                ("实际置换率", result.ActualDisplacementRate.ToString("F4")),
            };

            for (int i = 0; i < data.Length; i++)
            {
                table.Cells[i, 0].TextString = data[i].name;
                table.Cells[i, 1].TextString = data[i].value;
                table.Cells[i, 0].TextHeight = scale;
                table.Cells[i, 1].TextHeight = scale;
            }

            table.Position = insertPt;
            table.Layer = LAYER_ANNOTATION;
            btr.AppendEntity(table);
            tr.AddNewlyCreatedDBObject(table, true);
        }

        #endregion

        #region 辅助方法

        private Polyline CreatePolylineFromRect(PileLayoutService.Rect r)
        {
            var pl = new Polyline();
            pl.AddVertexAt(0, new Point2d(r.X0, r.Y0), 0, 0, 0);
            pl.AddVertexAt(1, new Point2d(r.X1, r.Y0), 0, 0, 0);
            pl.AddVertexAt(2, new Point2d(r.X1, r.Y1), 0, 0, 0);
            pl.AddVertexAt(3, new Point2d(r.X0, r.Y1), 0, 0, 0);
            pl.Closed = true;
            return pl;
        }

        // EnsureLayer 已移除 —— 图层在 PluginInitializer 统一创建

        #endregion
    }
}
