using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 螺栓受力计算命令（对应旧命令 hyabC_Calculat）
    /// 流程：选圆 → 计算形心 → 输入荷载 → 计算各点受力 → 生成表格
    /// </summary>
    public class AnchorBoltCalcCommand
    {
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "CIRCLE") });
            PromptSelectionResult selRes = ed.GetSelection(filter);
            if (selRes.Status != PromptStatus.OK) return;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var points = new List<Point3d>();
                foreach (SelectedObject selObj in selRes.Value)
                {
                    var circle = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Circle;
                    if (circle != null && !double.IsNaN(circle.Center.X))
                        points.Add(circle.Center);
                }

                if (!points.Any()) { ed.WriteMessage("\n未找到有效圆！"); tr.Commit(); return; }

                // 计算形心
                double sumX = points.Sum(p => p.X), sumY = points.Sum(p => p.Y);
                int n = points.Count;
                var centroid = new Point3d(sumX / n, sumY / n, 0);

                var relPoints = points.Select(p => (Abs: p, RelX: p.X - centroid.X, RelY: p.Y - centroid.Y))
                    .OrderBy(p => p.Abs.X).ThenBy(p => p.Abs.Y).ToList();

                // 用户输入荷载
                var nRes = ed.GetDouble("\n请输入法向力 N (kN)：");
                if (nRes.Status != PromptStatus.OK) return;
                double N = nRes.Value;

                var mxRes = ed.GetDouble("\n请输入绕X轴的力矩 M_x (kNm)：");
                if (mxRes.Status != PromptStatus.OK) return;
                double Mx = mxRes.Value * 1000;

                var myOpt = new PromptDoubleOptions("\n请输入绕Y轴的力矩 M_y (kNm) [默认0]：") { AllowNone = true, DefaultValue = 0.0 };
                var myRes = ed.GetDouble(myOpt);
                double My = myRes.Status == PromptStatus.OK ? myRes.Value : 0;

                var vRes = ed.GetDouble("\n请输入剪力 V (kN)：");
                if (vRes.Status != PromptStatus.OK) return;

                var sfRes = ed.GetDouble("\n请输入安全系数：");
                if (sfRes.Status != PromptStatus.OK || sfRes.Value <= 0) return;
                double safetyFactor = sfRes.Value;

                // 惯性矩
                double sumY2 = relPoints.Sum(p => p.RelY * p.RelY);
                double sumX2 = relPoints.Sum(p => p.RelX * p.RelX);
                if (Math.Abs(sumY2) < 1e-6 || Math.Abs(sumX2) < 1e-6)
                { ed.WriteMessage("\n惯性矩分量过小！"); tr.Commit(); return; }

                // 计算受力
                var vm = ViewModels.SettingsPanelViewModel.Current;
                double scale = vm != null ? vm.Scale : 40.0;

                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                var forces = new List<(int Sn, double X, double Y, double RelX, double RelY, double Force)>();
                int sn = 1;
                foreach (var p in relPoints)
                {
                    double force = (N / n) + (Mx * p.RelY / sumY2) + (My * p.RelX / sumX2);
                    forces.Add((sn, p.Abs.X, p.Abs.Y, p.RelX, p.RelY, force));

                    // 放置序号
                    var text = new DBText
                    {
                        Position = p.Abs,
                        Height = 2.5 * scale,
                        TextString = sn.ToString(),
                        HorizontalMode = TextHorizontalMode.TextCenter,
                        VerticalMode = TextVerticalMode.TextVerticalMid,
                        AlignmentPoint = p.Abs
                    };
                    btr.AppendEntity(text);
                    tr.AddNewlyCreatedDBObject(text, true);
                    sn++;
                }

                // 生成表格
                var tablePos = new Point3d(centroid.X + 10 * scale, centroid.Y, 0);
                var table = new Table { Position = tablePos };
                table.SetSize(forces.Count + 2, 7);

                for (int i = 0; i < table.Rows.Count; i++)
                    table.Rows[i].TextHeight = 2.5 * scale;

                string[] headers = { "编号", "X (mm)", "Y (mm)", "相对X", "相对Y", "受力 (kN)", "状态" };
                for (int j = 0; j < headers.Length; j++)
                    table.Cells[0, j].TextString = headers[j];

                for (int i = 0; i < forces.Count; i++)
                {
                    var f = forces[i];
                    table.Cells[i + 1, 0].TextString = f.Sn.ToString();
                    table.Cells[i + 1, 1].TextString = f.X.ToString("F0");
                    table.Cells[i + 1, 2].TextString = f.Y.ToString("F0");
                    table.Cells[i + 1, 3].TextString = f.RelX.ToString("F0");
                    table.Cells[i + 1, 4].TextString = f.RelY.ToString("F0");
                    table.Cells[i + 1, 5].TextString = f.Force.ToString("F2");
                    table.Cells[i + 1, 6].TextString = f.Force >= 0 ? "拉力" : "压力";
                }

                table.Cells[forces.Count + 1, 0].TextString = "安全系数";
                table.Cells[forces.Count + 1, 1].TextString = safetyFactor.ToString("F2");

                for (int j = 0; j < 7; j++)
                    table.Columns[j].Width = 15 * scale;

                table.GenerateLayout();
                btr.AppendEntity(table);
                tr.AddNewlyCreatedDBObject(table, true);

                tr.Commit();
            }
            ed.Regen();
        }
    }
}
