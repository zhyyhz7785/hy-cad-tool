using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using Exception = Autodesk.AutoCAD.Runtime.Exception;
namespace HyCADTool.Tools
{
    public static partial class Tools
    {
        // 通用实体处理方法，在同一个事务和文档锁定下处理每个实体
        public static void ProcessEntities<T>(this IEnumerable<T> entities, Action<T, Transaction> action) where T : Entity
        {
            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            using (DocumentLock docLock = doc.LockDocument())
            {
                using (Transaction trans = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        foreach (var entity in entities)
                        {
                            action(entity, trans); // 在事务内处理每个实体并传递事务
                        }
                        trans.Commit(); // 提交事务
                    }
                    catch (Exception ex)
                    {
                        trans.Abort(); // 出现异常时回滚事务
                        doc.Editor.WriteMessage($"\n错误: {ex.Message}");
                    }
                }
            }
        }
        //放大四边形
        public static Polyline ExpandQuadrilateral(this Polyline originalQuad, double margin, Transaction tr)
        {
            // 如果只传入一个参数，则将其他方向的值设置为相同
            return ExpandQuadrilateral(originalQuad, margin, margin, margin, margin, tr);
        }
        public static Polyline ExpandQuadrilateral(this Polyline originalQuad, double left, double top, double right, double bottom, Transaction tr)
        {
            originalQuad = tr.GetObject(originalQuad.ObjectId, OpenMode.ForWrite) as Polyline;
            if (originalQuad != null)
            {
                originalQuad.ResetPolyVertex();
            }
            if (originalQuad == null || originalQuad.NumberOfVertices != 4)
            {
                throw new ArgumentException("输入的多段线必须是一个四边形。");
            }
            originalQuad.ResetPolyVertex();
            // 获取四边形的四个顶点
            Point2d p0 = originalQuad.GetPoint2dAt(0);
            Point2d p1 = originalQuad.GetPoint2dAt(1);
            Point2d p2 = originalQuad.GetPoint2dAt(2);
            Point2d p3 = originalQuad.GetPoint2dAt(3);
            // 根据 Margin 参数放大四边形
            Point2d newP0 = new Point2d(p0.X - left, p0.Y - bottom); // 左下
            Point2d newP1 = new Point2d(p1.X + right, p1.Y - bottom);    // 左上
            Point2d newP2 = new Point2d(p2.X + right, p2.Y + top);   // 右上
            Point2d newP3 = new Point2d(p3.X - left, p3.Y + top); // 右下
                                                                  // 创建新的放大后的四边形
            Polyline expandedQuad = new Polyline();
            expandedQuad.AddVertexAt(0, newP0, 0, 0, 0);
            expandedQuad.AddVertexAt(1, newP1, 0, 0, 0);
            expandedQuad.AddVertexAt(2, newP2, 0, 0, 0);
            expandedQuad.AddVertexAt(3, newP3, 0, 0, 0);
            expandedQuad.Closed = true; // 确保多段线闭合
            return expandedQuad;
        }
        public static void AddEntityToDatabase(this Entity entity, Transaction transaction, BlockTableRecord blockTableRecord = null)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }
            if (transaction == null)
            {
                throw new ArgumentNullException(nameof(transaction));
            }
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            // 如果没有提供BlockTableRecord，则默认使用当前空间
            if (blockTableRecord == null)
            {
                blockTableRecord = (BlockTableRecord)transaction.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            }
            // 将实体添加到BlockTableRecord中
            blockTableRecord.AppendEntity(entity);
            transaction.AddNewlyCreatedDBObject(entity, true);
        }
    }
}
