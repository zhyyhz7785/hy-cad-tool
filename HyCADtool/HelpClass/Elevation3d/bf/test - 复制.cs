//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Geometry;
//using Autodesk.AutoCAD.Runtime;
//using System;
//namespace HyCADTool.HelpClass.CreatBase
//{
//    public class WallConnectionTester
//    {
//        [CommandMethod("TestHandleWallConnection")]
//        public void TestHandleWallConnection()
//        {
//            // 获取当前文档、数据库和编辑器
//            Document doc = Application.DocumentManager.MdiActiveDocument;
//            Database db = doc.Database;
//            Editor ed = doc.Editor;
//            // 使用事务处理整个操作
//            using (Transaction tr = db.TransactionManager.StartTransaction())
//            {
//                // 选择第一条直线 (q1)
//                PromptEntityOptions peo1 = new PromptEntityOptions("\n选择第一条墙中心线 (Line):");
//                peo1.SetRejectMessage("\n所选对象不是 Line。");
//                peo1.AddAllowedClass(typeof(Line), true);
//                PromptEntityResult per1 = ed.GetEntity(peo1);
//                if (per1.Status != PromptStatus.OK) return;
//                Line q1 = (Line)tr.GetObject(per1.ObjectId, OpenMode.ForRead);
//                // 选择第二条直线 (q2)
//                PromptEntityOptions peo2 = new PromptEntityOptions("\n选择第二条墙中心线 (Line):");
//                peo2.SetRejectMessage("\n所选对象不是 Line。");
//                peo2.AddAllowedClass(typeof(Line), true);
//                PromptEntityResult per2 = ed.GetEntity(peo2);
//                if (per2.Status != PromptStatus.OK) return;
//                Line q2 = (Line)tr.GetObject(per2.ObjectId, OpenMode.ForRead);
//                // 输入第一条墙的厚度
//                //PromptDoubleOptions pdo1 = new PromptDoubleOptions("\n输入第一条墙的厚度: ");
//                //PromptDoubleResult pdr1 = ed.GetDouble(pdo1);
//                //if (pdr1.Status != PromptStatus.OK) return;
//                //double thickness1 = pdr1.Value;
//                double thickness1 = 300;
//                // 输入第二条墙的厚度
//                //PromptDoubleOptions pdo2 = new PromptDoubleOptions("\n输入第二条墙的厚度: ");
//                //PromptDoubleResult pdr2 = ed.GetDouble(pdo2);
//                //if (pdr2.Status != PromptStatus.OK) return;
//                //double thickness2 = pdr2.Value;
//                double thickness2 = 300;
//                // 选择连接类型 JoinType
//                PromptKeywordOptions pko = new PromptKeywordOptions("\n选择 JoinType [Miter/Square/Bevel]: ", "Miter Square Bevel");
//                pko.Keywords.Default = "Miter";
//                PromptResult pr = ed.GetKeywords(pko);
//                if (pr.Status != PromptStatus.OK) return;
//                GeometryExtensions.JoinType = (JoinType)Enum.Parse(typeof(JoinType), pr.StringResult);
//                // 调用 HandleWallConnection 方法
//                Polyline qb1 = null;
//                Polyline qb2 = null;
//               // GeometryExtensions.HandleWallConnection(q1, q2, thickness1, thickness2, ref qb1, ref qb2);
//                // 获取模型空间
//                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
//                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
//                // 注意：qb1 和 qb2 已经在 HandleWallConnection 中添加到了数据库中，无需再次添加
//                // 计算关键点
//                Point3d p11 = q1.StartPoint;  // q1 的起点
//                Point3d p14 = q1.EndPoint;    // q1 的终点
//                Vector3d dir1 = p14 - p11;    // q1 的方向向量
//                Vector3d normal1 = dir1.GetNormal().RotateBy(Math.PI / 2, Vector3d.ZAxis); // q1 的法向量
//                Point3d p12 = p11 + normal1 * thickness1; // q1 起点偏移后的点
//                Point3d p13 = p14 + normal1 * thickness1; // q1 终点偏移后的点
//                Point3d p21 = q2.StartPoint;  // q2 的起点
//                Point3d p24 = q2.EndPoint;    // q2 的终点
//                Vector3d dir2 = p24 - p21;    // q2 的方向向量
//                Vector3d normal2 = dir2.GetNormal().RotateBy(Math.PI / 2, Vector3d.ZAxis); // q2 的法向量
//                Point3d p22 = p21 + normal2 * thickness2; // q2 起点偏移后的点
//                Point3d p23 = p24 + normal2 * thickness2; // q2 终点偏移后的点
//                // 计算交点
//                Point3d? j1 = GeometryExtensions.GetIntersectionPointNullable(p13, p13 + dir1, p22, p22 + dir2);
//                Point3d? j2 = GeometryExtensions.GetIntersectionPointNullable(p23, p22, p14, p13);
//                Point3d? j3 = GeometryExtensions.GetIntersectionPointNullable(p13, p12, p22, p21);
//                // 添加文字注释
//                AddTextAnnotation(btr, "p11", p11);
//                AddTextAnnotation(btr, "p12", p12);
//                AddTextAnnotation(btr, "p13", p13);
//                AddTextAnnotation(btr, "p14", p14);
//                AddTextAnnotation(btr, "p21", p21);
//                AddTextAnnotation(btr, "p22", p22);
//                AddTextAnnotation(btr, "p23", p23);
//                AddTextAnnotation(btr, "p24", p24);
//                if (j1.HasValue) AddTextAnnotation(btr, "j1", j1.Value);
//                if (j2.HasValue) AddTextAnnotation(btr, "j2", j2.Value);
//                if (j3.HasValue) AddTextAnnotation(btr, "j3", j3.Value);
//                // 提交事务
//                tr.Commit();
//            }
//        }
//        // 添加文字注释的方法
//        private static void AddTextAnnotation(BlockTableRecord btr, string label, Point3d position)
//        {
//            using (DBText text = new DBText())
//            {
//                text.TextString = label;
//                text.Position = position;
//                text.Height = 100; // 文字高度为 100
//                btr.AppendEntity(text);
//                Application.DocumentManager.MdiActiveDocument.TransactionManager.AddNewlyCreatedDBObject(text, true);
//            }
//        }
//    }
//}