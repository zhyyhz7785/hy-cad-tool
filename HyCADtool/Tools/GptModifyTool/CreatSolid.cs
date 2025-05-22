using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
namespace HyCADTool.Tools
{
    public static partial class Et
    {
        private static void CreatePolylineFromPoints(Database db, List<Point3d> points, string layerName)
        {
            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                // 获取块表
                BlockTable bt = trans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                // 获取模型空间块表记录
                BlockTableRecord btr = trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                // 创建 Polyline 对象
                Polyline polyline = new Polyline();
                polyline.SetDatabaseDefaults();
                // 设置 Polyline 所属图层
                polyline.Layer = layerName;
                // 添加顶点
                for (int i = 0; i < points.Count; i++)
                {
                    polyline.AddVertexAt(i, new Point2d(points[i].X, points[i].Y), 0, 0, 0);
                }
                // 设置 Polyline 为闭合
                polyline.Closed = true;
                // 将 Polyline 添加到块表记录
                btr.AppendEntity(polyline);
                trans.AddNewlyCreatedDBObject(polyline, true);
                // 提交事务
                trans.Commit();
            }
        }
        public static LineSegment3d[] GetLineSegment3d(this Line[] lines)
        {
            LineSegment3d[] segments = new LineSegment3d[lines.Length];
            int i = 0;
            foreach (var line in lines)
            {
                segments[i] = new LineSegment3d(line.StartPoint, line.EndPoint);
                i++;
            }
            return segments;
        }
        public static Line[] GetLines(this LineSegment3d[] segments)
        {
            Line[] lines = new Line[segments.Length];
            int i = 0;
            foreach (var segment in segments)
            {
                lines[i] = new Line(segment.StartPoint, segment.EndPoint);
                i++;
            }
            return lines;
        }
        public static Line[] GetLinesByDisplacement(this Line baseLine, Vector3d vector3d, int number)
        {
            List<Line> lines = new List<Line>();
            for (int i = 0; i < number; i++)
            {
                Matrix3d matrix3dd = Matrix3d.Displacement(vector3d * i);
                var lineCopy = baseLine.GetTransformedCopy(matrix3dd) as Line;
                lines.Add(lineCopy);
            }
            return lines.ToArray();
        }
        public static List<DBText> CreatLinesWithDbText(this Line baseLine, Vector3d vector3d, int number, string[] strs)
        {
            List<DBText> texts = new List<DBText>();
            for (int i = 0; i < number; i++)
            {
                var p1 = baseLine.StartPoint;
                var vec1 = baseLine.Angle;
                var text = new DBText();
                text.Position = p1 + vector3d * i;
                text.Rotation = baseLine.Angle;
                text.TextString = strs[i];
                text.Height = 250;
                texts.Add(text);
            }
            return texts;
        }
        public static MText DeepCloneText(this MText source)
        {
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            var tr = db.TransactionManager;
            var ids = new ObjectIdCollection(new[] { source.ObjectId });
            var mapping = new IdMapping();
            db.DeepCloneObjects(ids, db.CurrentSpaceId, mapping, false);
            var clone = (MText)tr.GetObject(mapping[source.ObjectId].Value, OpenMode.ForWrite);
            return clone;
        }
        public static void MakeMarks(this IEnumerable<Point3d> pointBases,
            string str = "P", double diameter = 50, double TextHeight = 125, double x = 0, double y = 0)
        {
            int i = 0;
            foreach (var point in pointBases)
            {
                Vector3d vec = new Vector3d(x, y, 0);
                Matrix3d matrix3dd = Matrix3d.Displacement(vec);
                var pointIn = point.TransformBy(matrix3dd);
                Circle circle = new Circle();
                DBText text = new DBText();
                text.Position = pointIn;
                text.TextString = str + i.ToString();
                text.Height = TextHeight;
                text.ColorIndex = 7;
                circle.Center = point;
                circle.Diameter = diameter;
                circle.ColorIndex = 1;
                text.ToSpace();
                circle.ToSpace();
                i++;
            }
        }
        #region 创建实体操作，别人的代码
        /// <summary>
        /// 创建多边形。
        /// </summary>
        /// <param name="locations">{ x0, y0, x1, y1, ... }</param>
        /// <param name="bulges"></param>
        /// <param name="startWidth"></param>
        /// <param name="endWidth"></param>
        /// <param name="closed"></param>
        /// <returns></returns>
        public static Polyline CreatePolygon(
           double[] locations,
           Tuple<int, double>[] bulges = null,
           Tuple<int, double>[] startWidth = null,
           Tuple<int, double>[] endWidth = null,
           bool closed = true)
        {
            var poly = new Polyline(locations.Length / 2);
            for (var i = 0; i < locations.Length; i += 2)
            {
                poly.AddVertexAt(poly.NumberOfVertices,
                    new Point2d(locations[i], locations[i + 1]),
                    0, 0, 0);
            }
            if (bulges != null)
            {
                foreach (var b in bulges)
                {
                    poly.SetBulgeAt(b.Item1, b.Item2);
                }
            }
            if (startWidth != null)
            {
                foreach (var s in startWidth)
                {
                    poly.SetStartWidthAt(s.Item1, s.Item2);
                }
            }
            if (endWidth != null)
            {
                foreach (var e in endWidth)
                {
                    poly.SetEndWidthAt(e.Item1, e.Item2);
                }
            }
            poly.Closed = closed;
            return poly;
        }
        /// <summary>
        /// 创建矩形。
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="w"></param>
        /// <param name="h"></param>
        /// <returns></returns>
        public static Polyline CreateRect(double x, double y, double w, double h)
        {
            return CreatePolygon(new[]
            {
                x, y, x + w, y, x + w, y + h, x, y + h,
            });
        }
        /// <summary>
        /// 创建实心矩形。
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="w"></param>
        /// <param name="h"></param>
        /// <returns></returns>
        public static Polyline CreateSolidRect(double x, double y, double w, double h)
        {
            return CreatePolygon(new[]
                {
                    x, y + h * 0.5, x + w, y + h * 0.5,
                },
                startWidth: new[] { Tuple.Create(0, h) },
                endWidth: new[] { Tuple.Create(0, h) });
        }
        /// <summary>
        /// 创建圆形。
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="r"></param>
        /// <returns></returns>
        public static Polyline CreateCircle(double x, double y, double r)
        {
            return CreatePolygon(
                new[]
                {
                    x + r, y, x - r, y,
                },
                new[]
                {
                    Tuple.Create(0, Math.Tan(Math.PI / 4)),
                    Tuple.Create(1, Math.Tan(Math.PI / 4)),
                });
        }
        /// <summary>
        /// 创建实心圆。
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="r"></param>
        /// <returns></returns>
        public static Polyline CreateSolidCircle(double x, double y, double r)
        {
            return CreatePolygon(
                new[]
                {
                    x + r * 0.5, y, x - r * 0.5, y,
                },
                new[]
                {
                    Tuple.Create(0, Math.Tan(Math.PI / 4)),
                    Tuple.Create(1, Math.Tan(Math.PI / 4)),
                },
                new[]
                {
                    Tuple.Create(0, r),
                    Tuple.Create(1, r),
                },
                new[]
                {
                    Tuple.Create(0, r),
                    Tuple.Create(1, r),
                });
        }
        public static Polyline CreateSolidCircle(double r, Point3d basePint)
        {
            var x = basePint.X;
            var y = basePint.Y;
            return CreatePolygon(
                new[]
                {
                    x + r * 0.5, y, x - r * 0.5, y,
                },
                new[]
                {
                    Tuple.Create(0, Math.Tan(Math.PI / 4)),
                    Tuple.Create(1, Math.Tan(Math.PI / 4)),
                },
                new[]
                {
                    Tuple.Create(0, r),
                    Tuple.Create(1, r),
                },
                new[]
                {
                    Tuple.Create(0, r),
                    Tuple.Create(1, r),
                });
        }
        /// <summary>
        /// 创建填充。
        /// </summary>
        /// <param name="pattern"></param>
        /// <param name="loops"></param>
        /// <param name="type"></param>
        /// <param name="scale"></param>
        /// <param name="otherSetting"></param>
        /// <param name="db"></param>
        /// <param name="space"></param>
        /// <returns></returns>
        public static ObjectId CreateHatch(string pattern, Polyline[] loops,
            HatchPatternType type = HatchPatternType.PreDefined, double scale = 1,
            Action<Hatch> otherSetting = null, Database db = null, string space = null)
        {
            var hatch = new Hatch();
            var id = hatch.ToSpace(db, space);
            using (var trans = id.Database.TransactionManager.StartTransaction())
            {
                hatch = trans.GetObject(id, OpenMode.ForWrite) as Hatch;
                hatch.PatternScale = scale;
                hatch.SetHatchPattern(type, pattern);
                foreach (var loop in loops)
                {
                    var hatchLoop = new HatchLoop(HatchLoopTypes.Polyline);
                    for (var i = 0; i < loop.NumberOfVertices; i++)
                    {
                        hatchLoop.Polyline.Add(
                            new BulgeVertex(loop.GetPoint2dAt(i), loop.GetBulgeAt(i)));
                    }
                    hatch.AppendLoop(hatchLoop);
                }
                otherSetting?.Invoke(hatch);
                trans.Commit();
            }
            return id;
        }
        /// <summary>
        /// 创建填充。
        /// </summary>
        /// <param name="pattern"></param>
        /// <param name="loops"></param>
        /// <param name="type"></param>
        /// <param name="scale"></param>
        /// <param name="otherSetting"></param>
        /// <param name="db"></param>
        /// <param name="space"></param>
        /// <returns></returns>
        public static ObjectId CreateHatch(string pattern, ObjectIdCollection loops,
            HatchPatternType type = HatchPatternType.PreDefined, double scale = 1,
            Action<Hatch> otherSetting = null, string space = null)
        {
            var hatch = new Hatch();
            var id = hatch.ToSpace(loops[0].Database, space);
            using (var trans = id.Database.TransactionManager.StartTransaction())
            {
                hatch = trans.GetObject(id, OpenMode.ForWrite) as Hatch;
                hatch.PatternScale = scale;
                hatch.SetHatchPattern(type, pattern);
                foreach (ObjectId loopId in loops)
                {
                    hatch.AppendLoop(HatchLoopTypes.Default, new ObjectIdCollection() { loopId });
                }
                otherSetting?.Invoke(hatch);
                trans.Commit();
            }
            return id;
        }
        /// <summary>
        /// 创建组。
        /// </summary>
        /// <param name="groupName"></param>
        /// <param name="ids"></param>
        /// <param name="setting"></param>
        public static void MakeGroup(string groupName, ObjectIdCollection ids, Action<Group> setting = null)
        {
            using (var trans = ids[0].Database.TransactionManager.StartTransaction())
            {
                var groupDict = trans.GetObject(ids[0].Database.GroupDictionaryId,
                    OpenMode.ForWrite) as DBDictionary;
                Group group;
                if (groupDict.Contains(groupName))
                {
                    group = trans.GetObject(groupDict.GetAt(groupName), OpenMode.ForWrite) as Group;
                }
                else
                {
                    group = new Group();
                    groupDict.SetAt(groupName, group);
                    trans.AddNewlyCreatedDBObject(group, true);
                }
                group.Append(ids);
                setting?.Invoke(group);
                trans.Commit();
            }
            return;
        }
        /// <summary>
        /// 向实体添加注释比例。
        /// </summary>
        /// <param name="ent"></param>
        /// <param name="db"></param>
        public static void Annotationscale(this Entity ent, Database db = null)
        {
            db = db ?? Application.DocumentManager.MdiActiveDocument.Database;
            ent.Annotative = AnnotativeStates.True;
            var objCtxMng = db.ObjectContextManager;
            var objCtxCol = objCtxMng.GetContextCollection("ACDB_ANNOTATIONSCALES");
            foreach (var objCtx in objCtxCol)
            {
                ent.AddContext(objCtx);
            }
        }
        #endregion
    }
}
