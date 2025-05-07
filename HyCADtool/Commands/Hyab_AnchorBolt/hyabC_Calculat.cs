using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Config;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Command
{
    public static partial class HyCommand
    {
        [CommandMethod("hyabC_Calculat")] // AutoCAD 命令名 "hyc2ab"
        public static void CalculateBoltForces()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            // 1. 选取点（圆）
            TypedValue[] filterList = new TypedValue[]
            {
                new TypedValue((int)DxfCode.Start, "CIRCLE")
            };
            SelectionFilter filter = new SelectionFilter(filterList);
            PromptSelectionResult selRes = ed.GetSelection(filter);
            if (selRes.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n未选择任何圆！");
                return;
            }

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var points = new List<Point3d>();
                foreach (SelectedObject selObj in selRes.Value)
                {
                    if (tr.GetObject(selObj.ObjectId, OpenMode.ForRead) is Circle circle)
                    {
                        if (double.IsNaN(circle.Center.X) || double.IsNaN(circle.Center.Y))
                        {
                            ed.WriteMessage($"\n跳过无效圆（中心: {circle.Center.X}, {circle.Center.Y}），坐标无效。");
                            continue;
                        }
                        points.Add(circle.Center);
                    }
                }

                if (!points.Any())
                {
                    ed.WriteMessage("\n未找到任何有效定位的圆！");
                    tr.Commit();
                    return;
                }

                // 2. 计算形心
                double sumX = 0, sumY = 0;
                int pointCount = points.Count;
                foreach (var point in points)
                {
                    sumX += point.X;
                    sumY += point.Y;
                }
                Point3d centroid = new Point3d(sumX / pointCount, sumY / pointCount, 0);
                ed.WriteMessage($"\n点群形心坐标：({centroid.X:F2}, {centroid.Y:F2})");

                // 3. 生成相对坐标并按 x, y 升序排序
                var pointsWithRelativeCoords = points.Select(p => (
                    AbsPoint: p,
                    RelX: p.X - centroid.X,
                    RelY: p.Y - centroid.Y
                )).OrderBy(p => p.AbsPoint.X).ThenBy(p => p.AbsPoint.Y).ToList();

                // 4. 用户输入 N, M_x, M_y, V 和安全系数
                PromptDoubleResult nRes = ed.GetDouble("\n请输入法向力 N (kN)：");
                if (nRes.Status != PromptStatus.OK) return;
                double N = nRes.Value * 1; // 转换为 KN

                PromptDoubleResult mxRes = ed.GetDouble("\n请输入绕X轴的力矩 M_x (kNm)：");
                if (mxRes.Status != PromptStatus.OK) return;
                double Mx = mxRes.Value * 1000; // 转换为 KN·mm

                PromptDoubleOptions myOpt = new PromptDoubleOptions("\n请输入绕Y轴的力矩 M_y (kNm) [默认0]：")
                {
                    AllowNone = true,
                    DefaultValue = 0.0
                };
                PromptDoubleResult myRes = ed.GetDouble(myOpt);
                if (myRes.Status != PromptStatus.OK && myRes.Status != PromptStatus.None) return;
                double My = myRes.Status == PromptStatus.OK ? myRes.Value * 1 : 0;

                PromptDoubleResult vRes = ed.GetDouble("\n请输入剪力 V (kN)：");
                if (vRes.Status != PromptStatus.OK) return;
                double V = vRes.Value * 1;

                PromptDoubleResult sfRes = ed.GetDouble("\n请输入安全系数：");
                if (sfRes.Status != PromptStatus.OK) return;
                double safetyFactor = sfRes.Value;
                if (safetyFactor <= 0)
                {
                    ed.WriteMessage("\n安全系数必须大于0！");
                    tr.Commit();
                    return;
                }

                // 5. 计算惯性矩分量
                double sumY2 = 0, sumX2 = 0;
                foreach (var point in pointsWithRelativeCoords)
                {
                    sumY2 += point.RelY * point.RelY;
                    sumX2 += point.RelX * point.RelX;
                }

                if (Math.Abs(sumY2) < 1e-6 || Math.Abs(sumX2) < 1e-6)
                {
                    ed.WriteMessage("\n惯性矩分量过小，点分布可能有问题！");
                    tr.Commit();
                    return;
                }

                // 6. 计算每个点的受力并分配序列号
                var pointForces = new List<(int SerialNumber, double X, double Y, double RelX, double RelY, double Force)>();
                int serialNumber = 1;
                foreach (var point in pointsWithRelativeCoords)
                {
                    double force = (N / pointCount) + (Mx * point.RelY / sumY2) + (My * point.RelX / sumX2); // 单位：N
                    pointForces.Add((
                        SerialNumber: serialNumber++,
                        X: point.AbsPoint.X,
                        Y: point.AbsPoint.Y,
                        RelX: point.RelX,
                        RelY: point.RelY,
                        Force: force
                    ));

                    // 在点位置放置序列号
                    PlaceSerialNumber(db, tr, point.AbsPoint, (serialNumber - 1).ToString());
                }

                // 7. 生成表格
                CreateForceTable(db, tr, ed, pointForces, centroid, safetyFactor);

                tr.Commit();
            }

            ed.Regen();
        }

        private static void PlaceSerialNumber(Database db, Transaction tr, Point3d position, string serialNumber)
        {
            BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

            // 创建文本（序列号）
            using (DBText text = new DBText())
            {
                text.Position = position;
                text.Height = 2.5 * BaseConfig.Scale; // 按比例缩放文本高度
                text.TextString = serialNumber;
                text.HorizontalMode = TextHorizontalMode.TextCenter;
                text.VerticalMode = TextVerticalMode.TextVerticalMid;
                text.AlignmentPoint = position;

                btr.AppendEntity(text);
                tr.AddNewlyCreatedDBObject(text, true);
            }
        }

        private static void CreateForceTable(Database db, Transaction tr, Editor ed,
            List<(int SerialNumber, double X, double Y, double RelX, double RelY, double Force)> pointForces, Point3d centroid, double safetyFactor)
        {
            BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

            // 表格位置（形心右侧，按比例偏移）
            Point3d tablePosition = new Point3d(centroid.X + 10 * BaseConfig.Scale, centroid.Y, 0);

            Table table = new Table
            {
                Position = tablePosition
            };
            table.SetSize(pointForces.Count + 2, 7);

            // 设置文本高度（按比例缩放）
            table.Rows[0].TextHeight = 2.5 * BaseConfig.Scale;
            for (int i = 1; i < table.Rows.Count; i++)
            {
                table.Rows[i].TextHeight = 2.5 * BaseConfig.Scale;
            }

            table.Cells[0, 0].TextString = "编号";
            table.Cells[0, 1].TextString = "X (mm)";
            table.Cells[0, 2].TextString = "Y (mm)";
            table.Cells[0, 3].TextString = "相对X (mm)";
            table.Cells[0, 4].TextString = "相对Y (mm)";
            table.Cells[0, 5].TextString = "受力 (kN)";
            table.Cells[0, 6].TextString = "状态";
            table.Cells[0, -1].Alignment = CellAlignment.MiddleCenter;

            for (int i = 0; i < pointForces.Count; i++)
            {
                var point = pointForces[i];
                table.Cells[i + 1, 0].TextString = point.SerialNumber.ToString();
                table.Cells[i + 1, 1].TextString = point.X.ToString("F0");
                table.Cells[i + 1, 2].TextString = point.Y.ToString("F0");
                table.Cells[i + 1, 3].TextString = point.RelX.ToString("F0");
                table.Cells[i + 1, 4].TextString = point.RelY.ToString("F0");
                table.Cells[i + 1, 5].TextString = (point.Force).ToString("F2");
                table.Cells[i + 1, 6].TextString = point.Force >= 0 ? "拉力" : "压力";
                table.Cells[i + 1, -1].Alignment = CellAlignment.MiddleCenter;
            }

            table.Cells[pointForces.Count + 1, 0].TextString = "安全系数";
            table.Cells[pointForces.Count + 1, 1].TextString = safetyFactor.ToString("F2");
            table.Cells[pointForces.Count + 1, -1].Alignment = CellAlignment.MiddleLeft;

            // 设置列宽（按比例缩放）
            table.Columns[0].Width = 15 * BaseConfig.Scale;
            table.Columns[1].Width = 15 * BaseConfig.Scale;
            table.Columns[2].Width = 15 * BaseConfig.Scale;
            table.Columns[3].Width = 15 * BaseConfig.Scale;
            table.Columns[4].Width = 15 * BaseConfig.Scale;
            table.Columns[5].Width = 15 * BaseConfig.Scale;
            table.Columns[6].Width = 15 * BaseConfig.Scale;

            table.GenerateLayout();

            btr.AppendEntity(table);
            tr.AddNewlyCreatedDBObject(table, true);
        }
    }
}