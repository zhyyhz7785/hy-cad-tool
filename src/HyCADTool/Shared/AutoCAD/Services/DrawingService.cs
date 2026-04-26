using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Shell.Contracts;
using HyCADTool.Shared.AutoCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Shared.AutoCAD.Services
{
    /// <summary>
    /// 实体绘制服务实现 (Drawing Service Implementation)
    /// 封装 AutoCAD 实体绘制操作
    /// </summary>
    public class DrawingService : IDrawingService
    {
        private readonly ILayerService _layerService;

        public DrawingService(ILayerService layerService)
        {
            _layerService = layerService ?? throw new ArgumentNullException(nameof(layerService));
        }

        #region 基础几何图形绘制 (Basic Geometry Drawing)

        public ObjectId DrawLine(Point3d start, Point3d end, string layerName = null)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("没有活动的 AutoCAD 文档 (No active AutoCAD document)");

            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace],
                        OpenMode.ForWrite
                    );

                    var line = new Line(start, end);

                    if (!string.IsNullOrEmpty(layerName))
                    {
                        line.Layer = layerName;
                    }

                    var id = btr.AppendEntity(line);
                    tr.AddNewlyCreatedDBObject(line, true);
                    tr.Commit();

                    return id;
                }
                catch
                {
                    tr.Abort();
                    throw;
                }
            }
        }

        public ObjectId DrawPolyline(IEnumerable<Point2d> points, string layerName = null, bool isClosed = false)
        {
            if (points == null || !points.Any())
                throw new ArgumentException("点集合不能为空 (Point collection cannot be empty)", nameof(points));

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("没有活动的 AutoCAD 文档 (No active AutoCAD document)");

            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace],
                        OpenMode.ForWrite
                    );

                    var polyline = new Polyline();
                    int index = 0;
                    foreach (var point in points)
                    {
                        polyline.AddVertexAt(index++, point, 0, 0, 0);
                    }
                    polyline.Closed = isClosed;

                    if (!string.IsNullOrEmpty(layerName))
                    {
                        polyline.Layer = layerName;
                    }

                    var id = btr.AppendEntity(polyline);
                    tr.AddNewlyCreatedDBObject(polyline, true);
                    tr.Commit();

                    return id;
                }
                catch
                {
                    tr.Abort();
                    throw;
                }
            }
        }

        public ObjectId DrawCircle(Point3d center, double radius, string layerName = null)
        {
            if (radius <= 0)
                throw new ArgumentException("半径必须大于 0 (Radius must be greater than 0)", nameof(radius));

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("没有活动的 AutoCAD 文档 (No active AutoCAD document)");

            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace],
                        OpenMode.ForWrite
                    );

                    var circle = new Circle(center, Vector3d.ZAxis, radius);

                    if (!string.IsNullOrEmpty(layerName))
                    {
                        circle.Layer = layerName;
                    }

                    var id = btr.AppendEntity(circle);
                    tr.AddNewlyCreatedDBObject(circle, true);
                    tr.Commit();

                    return id;
                }
                catch
                {
                    tr.Abort();
                    throw;
                }
            }
        }

        public ObjectId DrawArc(Point3d center, double radius, double startAngle, double endAngle, string layerName = null)
        {
            if (radius <= 0)
                throw new ArgumentException("半径必须大于 0 (Radius must be greater than 0)", nameof(radius));

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("没有活动的 AutoCAD 文档 (No active AutoCAD document)");

            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace],
                        OpenMode.ForWrite
                    );

                    var arc = new Arc(center, radius, startAngle, endAngle);

                    if (!string.IsNullOrEmpty(layerName))
                    {
                        arc.Layer = layerName;
                    }

                    var id = btr.AppendEntity(arc);
                    tr.AddNewlyCreatedDBObject(arc, true);
                    tr.Commit();

                    return id;
                }
                catch
                {
                    tr.Abort();
                    throw;
                }
            }
        }

        public ObjectId DrawEllipse(Point3d center, Vector3d majorAxis, double radiusRatio, string layerName = null)
        {
            if (radiusRatio <= 0 || radiusRatio > 1)
                throw new ArgumentException("短长轴比率必须在 (0, 1] 范围内 (Radius ratio must be in (0, 1])", nameof(radiusRatio));

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("没有活动的 AutoCAD 文档 (No active AutoCAD document)");

            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace],
                        OpenMode.ForWrite
                    );

                    var ellipse = new Ellipse(center, Vector3d.ZAxis, majorAxis, radiusRatio, 0, 2 * Math.PI);

                    if (!string.IsNullOrEmpty(layerName))
                    {
                        ellipse.Layer = layerName;
                    }

                    var id = btr.AppendEntity(ellipse);
                    tr.AddNewlyCreatedDBObject(ellipse, true);
                    tr.Commit();

                    return id;
                }
                catch
                {
                    tr.Abort();
                    throw;
                }
            }
        }

        #endregion

        #region 文本绘制 (Text Drawing)

        public ObjectId DrawText(Point3d position, string content, double height, string layerName = null)
        {
            if (string.IsNullOrEmpty(content))
                throw new ArgumentException("文本内容不能为空 (Text content cannot be empty)", nameof(content));

            if (height <= 0)
                throw new ArgumentException("文字高度必须大于 0 (Text height must be greater than 0)", nameof(height));

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("没有活动的 AutoCAD 文档 (No active AutoCAD document)");

            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace],
                        OpenMode.ForWrite
                    );

                    var text = new DBText
                    {
                        Position = position,
                        TextString = content,
                        Height = height
                    };

                    if (!string.IsNullOrEmpty(layerName))
                    {
                        text.Layer = layerName;
                    }

                    var id = btr.AppendEntity(text);
                    tr.AddNewlyCreatedDBObject(text, true);
                    tr.Commit();

                    return id;
                }
                catch
                {
                    tr.Abort();
                    throw;
                }
            }
        }

        public ObjectId DrawMText(Point3d position, string content, double height, double width, string layerName = null)
        {
            if (string.IsNullOrEmpty(content))
                throw new ArgumentException("文本内容不能为空 (Text content cannot be empty)", nameof(content));

            if (height <= 0)
                throw new ArgumentException("文字高度必须大于 0 (Text height must be greater than 0)", nameof(height));

            if (width <= 0)
                throw new ArgumentException("文本框宽度必须大于 0 (Text width must be greater than 0)", nameof(width));

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("没有活动的 AutoCAD 文档 (No active AutoCAD document)");

            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace],
                        OpenMode.ForWrite
                    );

                    var mtext = new MText
                    {
                        Location = position,
                        Contents = content,
                        TextHeight = height,
                        Width = width
                    };

                    if (!string.IsNullOrEmpty(layerName))
                    {
                        mtext.Layer = layerName;
                    }

                    var id = btr.AppendEntity(mtext);
                    tr.AddNewlyCreatedDBObject(mtext, true);
                    tr.Commit();

                    return id;
                }
                catch
                {
                    tr.Abort();
                    throw;
                }
            }
        }

        #endregion

        #region 标注绘制 (Dimension Drawing)

        public ObjectId DrawAlignedDimension(Point3d start, Point3d end, Point3d textPoint, string layerName = null)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("没有活动的 AutoCAD 文档 (No active AutoCAD document)");

            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace],
                        OpenMode.ForWrite
                    );

                    var dimension = new AlignedDimension(start, end, textPoint, string.Empty, db.Dimstyle);

                    if (!string.IsNullOrEmpty(layerName))
                    {
                        dimension.Layer = layerName;
                    }

                    var id = btr.AppendEntity(dimension);
                    tr.AddNewlyCreatedDBObject(dimension, true);
                    tr.Commit();

                    return id;
                }
                catch
                {
                    tr.Abort();
                    throw;
                }
            }
        }

        public ObjectId DrawRotatedDimension(Point3d start, Point3d end, Point3d textPoint, double rotation, string layerName = null)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("没有活动的 AutoCAD 文档 (No active AutoCAD document)");

            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace],
                        OpenMode.ForWrite
                    );

                    var dimension = new RotatedDimension(rotation, start, end, textPoint, string.Empty, db.Dimstyle);

                    if (!string.IsNullOrEmpty(layerName))
                    {
                        dimension.Layer = layerName;
                    }

                    var id = btr.AppendEntity(dimension);
                    tr.AddNewlyCreatedDBObject(dimension, true);
                    tr.Commit();

                    return id;
                }
                catch
                {
                    tr.Abort();
                    throw;
                }
            }
        }

        #endregion

        #region 批量绘制 (Batch Drawing)

        public ObjectId[] DrawEntities(IEnumerable<Entity> entities, string layerName = null)
        {
            if (entities == null || !entities.Any())
                throw new ArgumentException("实体集合不能为空 (Entity collection cannot be empty)", nameof(entities));

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("没有活动的 AutoCAD 文档 (No active AutoCAD document)");

            var db = doc.Database;
            var ids = new List<ObjectId>();

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace],
                        OpenMode.ForWrite
                    );

                    foreach (var entity in entities)
                    {
                        if (!string.IsNullOrEmpty(layerName))
                        {
                            entity.Layer = layerName;
                        }

                        var id = btr.AppendEntity(entity);
                        tr.AddNewlyCreatedDBObject(entity, true);
                        ids.Add(id);
                    }

                    tr.Commit();
                    return ids.ToArray();
                }
                catch
                {
                    tr.Abort();
                    throw;
                }
            }
        }

        #endregion

        #region Block 绘制 (Block Drawing)

        public ObjectId DrawToBlock(Entity entity, string blockName, string layerName = null)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (string.IsNullOrEmpty(blockName))
                throw new ArgumentException("Block 名称不能为空 (Block name cannot be empty)", nameof(blockName));

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("没有活动的 AutoCAD 文档 (No active AutoCAD document)");

            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

                    if (!bt.Has(blockName))
                    {
                        throw new ArgumentException($"Block '{blockName}' 不存在 (Block '{blockName}' does not exist)");
                    }

                    var btr = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForWrite);

                    if (!string.IsNullOrEmpty(layerName))
                    {
                        entity.Layer = layerName;
                    }

                    var id = btr.AppendEntity(entity);
                    tr.AddNewlyCreatedDBObject(entity, true);
                    tr.Commit();

                    return id;
                }
                catch
                {
                    tr.Abort();
                    throw;
                }
            }
        }

        #endregion

        #region 实体删除 (Entity Deletion)

        public void DeleteEntity(ObjectId entityId)
        {
            if (entityId.IsNull)
                throw new ArgumentException("实体 ObjectId 无效 (Entity ObjectId is invalid)", nameof(entityId));

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("没有活动的 AutoCAD 文档 (No active AutoCAD document)");

            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var entity = tr.GetObject(entityId, OpenMode.ForWrite) as Entity;
                    if (entity != null && !entity.IsErased)
                    {
                        entity.Erase();
                    }
                    tr.Commit();
                }
                catch
                {
                    tr.Abort();
                    throw;
                }
            }
        }

        public void DeleteEntities(IEnumerable<ObjectId> entityIds)
        {
            if (entityIds == null || !entityIds.Any())
                throw new ArgumentException("实体 ObjectId 集合不能为空 (Entity ObjectId collection cannot be empty)", nameof(entityIds));

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("没有活动的 AutoCAD 文档 (No active AutoCAD document)");

            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    foreach (var id in entityIds)
                    {
                        if (!id.IsNull)
                        {
                            var entity = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                            if (entity != null && !entity.IsErased)
                            {
                                entity.Erase();
                            }
                        }
                    }
                    tr.Commit();
                }
                catch
                {
                    tr.Abort();
                    throw;
                }
            }
        }

        #endregion
    }
}

