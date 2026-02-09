using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Extensions
{
    /// <summary>
    /// AutoCAD 实体扩展方法（对应旧代码 ZTools 中的 ToSpace、CreateSolidCircle 等）
    /// </summary>
    public static class EntityExtensions
    {
        /// <summary>写入单个实体到模型空间</summary>
        public static ObjectId ToSpace(this Entity ent, Database db = null)
        {
            db = db ?? Application.DocumentManager.MdiActiveDocument.Database;
            var doc = Application.DocumentManager.MdiActiveDocument;
            ObjectId id = ObjectId.Null;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                if (ent.Id.IsNull)
                {
                    id = btr.AppendEntity(ent);
                    tr.AddNewlyCreatedDBObject(ent, true);
                }
                else
                {
                    id = ent.Id;
                }
                tr.Commit();
            }
            return id;
        }

        /// <summary>写入多个实体到模型空间</summary>
        public static ObjectIdCollection ToSpace(this IEnumerable<Entity> ents, Database db = null)
        {
            db = db ?? Application.DocumentManager.MdiActiveDocument.Database;
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ids = new ObjectIdCollection();
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                foreach (var ent in ents)
                {
                    if (ent != null && ent.Id.IsNull)
                    {
                        ids.Add(btr.AppendEntity(ent));
                        tr.AddNewlyCreatedDBObject(ent, true);
                    }
                }
                tr.Commit();
            }
            return ids;
        }

        /// <summary>
        /// 创建实心圆（多段线近似，带宽度填充）
        /// 对应旧代码 ZTools.CreateSolidCircle
        /// </summary>
        public static Polyline CreateSolidCircle(double r, Point3d basePoint)
        {
            double x = basePoint.X;
            double y = basePoint.Y;
            var poly = new Polyline();
            double bulge = Math.Tan(Math.PI / 4); // = 1.0
            // 两个顶点，间距 r*0.5，宽度为 r → 填充效果
            poly.AddVertexAt(0, new Point2d(x + r * 0.5, y), bulge, r, r);
            poly.AddVertexAt(1, new Point2d(x - r * 0.5, y), bulge, r, r);
            poly.Closed = true;
            return poly;
        }

        /// <summary>创建实心圆（坐标版本）</summary>
        public static Polyline CreateSolidCircle(double r, double x, double y)
        {
            return CreateSolidCircle(r, new Point3d(x, y, 0));
        }

        /// <summary>选择单个实体</summary>
        public static T SelectAEntity<T>(this Database db, string prompt = null) where T : Entity
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var result = ed.GetEntity(prompt ?? $"\n选择一个 {typeof(T).Name}: ");
            if (result.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return null;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var ent = tr.GetObject(result.ObjectId, OpenMode.ForRead) as T;
                tr.Commit();
                return ent;
            }
        }

        /// <summary>Point3d 转 Point2d</summary>
        public static Point2d Point3dTo2d(this Point3d pt)
        {
            return new Point2d(pt.X, pt.Y);
        }

        /// <summary>Point3d 数组转为实心圆多段线数组（点钢筋）</summary>
        public static Polyline[] PointsToDotRein(this IEnumerable<Point3d> points, double diameter)
        {
            var list = new List<Polyline>();
            foreach (var pt in points)
            {
                list.Add(CreateSolidCircle(diameter, pt));
            }
            return list.ToArray();
        }
    }
}
