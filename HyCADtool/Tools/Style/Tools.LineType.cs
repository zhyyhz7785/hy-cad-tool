using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Colors;
using System.Linq;

namespace HyCADTool.Tools
{
    public static partial class Et
    {
        public static void ExportLinetypesToLIN()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;

            string path   = @"E:\BaiduSyncdisk\Code\testResult";
            string outputPath = Path.Combine(path, "ExportedLinetypes.lin");
            using (Transaction tr = db.TransactionManager.StartTransaction())
            using (StreamWriter writer = new StreamWriter(outputPath, false, Encoding.UTF8))
            {
                var ltTable = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);

                writer.WriteLine("; Exported from DWG");
                writer.WriteLine("; Generated on " + DateTime.Now);
                writer.WriteLine();

                foreach (ObjectId ltId in ltTable)
                {
                    var ltr = (LinetypeTableRecord)tr.GetObject(ltId, OpenMode.ForRead);

                    // 跳过内部默认线型
                    if (ltr.Name == "BYBLOCK" || ltr.Name == "BYLAYER" || ltr.Name == "CONTINUOUS")
                        continue;

                    writer.WriteLine($"*{ltr.Name},{ltr.Comments}");
                    writer.Write("A");

                    for (int i = 0; i < ltr.NumDashes; i++)
                    {
                        double len = ltr.DashLengthAt(i);
                        writer.Write($",{len.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}");
                    }

                    writer.WriteLine();
                    writer.WriteLine();
                }

                tr.Commit();
            }

            doc.Editor.WriteMessage($"\n线型已导出至：{outputPath}");
        }

        public static void RegisterStandardLinetypes()
        {
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var ltTable = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);

                var standardLinetypes = new List<(string Name, string Comment, double[] Pattern)>
        {
            ("点画线", "点画线: ____ . ____ . ____ .", new double[] { 12, -2, 2, -2 }),
            ("虚线",   "虚线: __ __ __ __ __",       new double[] { 1, -1 }),
            ("双点画线", "双点画线: ____ . . ____ . .", new double[] { 12, -2, 2, -2, 2, -2 })
        };

                foreach (var item in standardLinetypes)
                {
                    var name = item.Name;
                    var comment = item.Comment;
                    var pattern = item.Pattern;

                    if (!ltTable.Has(name))
                    {
                        ltTable.UpgradeOpen();

                        var ltr = new LinetypeTableRecord
                        {
                            Name = name,
                            Comments = comment
                        };

                        // 设置线型图案（scale=1.0, offset=0.0）
                        ltr.PatternLength = pattern.Sum(Math.Abs); // 图案总长度
                        ltr.NumDashes = pattern.Length;
                        for (int i = 0; i < pattern.Length; i++)
                        {
                            ltr.SetDashLengthAt(i, pattern[i]);
                        }

                        ltTable.Add(ltr);
                        tr.AddNewlyCreatedDBObject(ltr, true);
                    }
                }

                tr.Commit();
            }
        }



    }
}
