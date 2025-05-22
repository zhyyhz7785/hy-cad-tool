using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Interop.Common;
using HyCADTool.Config;
using System;
using System.Collections.Generic;
using System.Linq;
namespace HyCADTool.Tools
{
    public static partial class Et
    {
        public enum DimAlineBy { ShortPoint, LongPoint }
        /// <summary>
        /// 插件预热方法：插入并删除一个隐藏标注，用于预热AutoCAD LayerUsage机制
        /// </summary>
        public static void PrewarmAnnotation()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var db = doc.Database;
            using (var docLock = doc.LockDocument())
            {
                using (var trans = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        var bt = (BlockTable)trans.GetObject(db.BlockTableId, OpenMode.ForRead);
                        var btr = (BlockTableRecord)trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                        // 创建临时标注
                        Point3d p1 = new Point3d(0, 0, 0);
                        Point3d p2 = new Point3d(100, 0, 0);
                        Point3d dimLinePoint = new Point3d(50, -10, 0);
                        var tempDim = new RotatedDimension
                        {
                            XLine1Point = p1,
                            XLine2Point = p2,
                            DimLinePoint = dimLinePoint,
                            Rotation = 0,
                            DimensionStyle = db.Dimstyle
                        };
                        // 插入并立即删除
                        var dimId = btr.AppendEntity(tempDim);
                        trans.AddNewlyCreatedDBObject(tempDim, true);
                        tempDim.Erase(); // 标记为删除
                        trans.Commit();
                    }
                    catch (System.Exception ex)
                    {
                        doc.Editor.WriteMessage($"\n预热标注失败: {ex.Message}");
                        trans.Abort();
                    }
                }
            }
        }        
        /// <summary>
        /// 根据一组点批量创建标注（高效版）
        /// 要求：点数量 >=2，否则返回空
        /// </summary>
        /// <param name="points">输入点</param>
        /// <param name="direction">标注方向</param>
        /// <param name="offsetDist">偏移量（默认5）</param>
        /// <param name="isEqualLength">是否等长</param>
        /// <param name="layerId">放置到的图层Id（必填）</param>
        /// <returns>RotatedDimension集合</returns>
        public static List<RotatedDimension> CreateDimensionsByPoints(
            IEnumerable<Point3d> points,
            DimensionFor direction,
            double offsetDist = 5.0,
            bool isEqualLength = false,
            ObjectId layerId = default(ObjectId)
        )
        {
            if (points == null || !points.Any() || points.Count() < 2)
            {
                Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\n点数量不足，无法生成标注。");
                return new List<RotatedDimension>();
            }
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            var sortedPoints = points.ToArray();
            switch (direction)
            {
                case DimensionFor.ForLeft:
                    sortedPoints = sortedPoints.OrderByDescending(p => p.Y).ToArray();
                    break;
                case DimensionFor.ForRight:
                    sortedPoints = sortedPoints.OrderBy(p => p.Y).ToArray();
                    break;
                case DimensionFor.ForUp:
                    sortedPoints = sortedPoints.OrderBy(p => p.X).ToArray();
                    break;
                case DimensionFor.ForDown:
                    sortedPoints = sortedPoints.OrderByDescending(p => p.X).ToArray();
                    break;
            }
            var dims = new List<RotatedDimension>();
            double d = offsetDist * BaseConfig.Scale; // 保持您原来的缩放策略
            for (int i = 0; i < sortedPoints.Length - 1; i++)
            {
                var p1 = sortedPoints[i];
                var p2 = sortedPoints[i + 1];
                double angle = (direction == DimensionFor.ForLeft || direction == DimensionFor.ForRight) ? Math.PI / 2 : 0;
                var offsetVec = (direction == DimensionFor.ForLeft || direction == DimensionFor.ForRight)
                    ? new Vector3d(0, d, 0)
                    : new Vector3d(d, 0, 0);
                Point3d dimLinePoint;
                if (direction == DimensionFor.ForLeft || direction == DimensionFor.ForDown)
                    dimLinePoint = p1 - offsetVec;
                else
                    dimLinePoint = p2 + offsetVec;
                var dim = new RotatedDimension
                {
                    XLine1Point = p1,
                    XLine2Point = p2,
                    DimLinePoint = dimLinePoint,
                    Rotation = angle,
                    DimensionStyle = db.Dimstyle,
                    LayerId = layerId
                };
                if (isEqualLength)
                    dim.DimVEqualLength();
                dims.Add(dim);
            }
            return dims;
        }
        #region 1. 标注创建工具
        /// <summary>
        /// 根据两个点创建旋转标注
        /// </summary>
        public static RotatedDimension GetDimByTwoPoints(Point3d p1, Point3d p2, double offsetDist,
            DimensionFor dimensionFor, bool isEqualLength = false)
        {
            double angle = 0;
            var pointDistance = new Point3d();
            var dim = new RotatedDimension();
            switch (dimensionFor)
            {
                case DimensionFor.ForLeft:
                    angle = Math.PI / 2;
                    pointDistance = p1.X < p2.X ? p1 - new Vector3d(offsetDist, 0, 0) : p2 - new Vector3d(offsetDist, 0, 0);
                    break;
                case DimensionFor.ForRight:
                    angle = Math.PI / 2;
                    pointDistance = p1.X < p2.X ? p2 + new Vector3d(offsetDist, 0, 0) : p1 + new Vector3d(offsetDist, 0, 0);
                    break;
                case DimensionFor.ForUp:
                    angle = 0;
                    pointDistance = p1.Y < p2.Y ? p2 + new Vector3d(0, offsetDist, 0) : p1 + new Vector3d(0, offsetDist, 0);
                    break;
                case DimensionFor.ForDown:
                    angle = 0;
                    pointDistance = p1.Y < p2.Y ? p1 - new Vector3d(0, offsetDist, 0) : p2 - new Vector3d(0, offsetDist, 0);
                    break;
            }
            dim.XLine1Point = p1;
            dim.XLine2Point = p2;
            dim.DimLinePoint = pointDistance;
            dim.Rotation = angle;
            if (isEqualLength) dim.DimVEqualLength();
            return dim;
        }
        public static RotatedDimension[] CreateOrderedDimensions(
    this Point3d[] points,
    DimensionFor dimDirection,
    double offsetDist = 5.0,
    bool isEqualLength = false,
    ObjectId layerId = default) // ✨ 新增可选参数：layerId
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            var d = offsetDist * BaseConfig.Scale;
            if (points.Length < 2)
            {
                ed.WriteMessage("\n错误：点数不足，无法创建标注。");
                return Array.Empty<RotatedDimension>();
            }
            // 根据方向排序点位
            Point3d[] sortedPoints;
            switch (dimDirection)
            {
                case DimensionFor.ForLeft:
                    sortedPoints = points.OrderByDescending(p => p.Y).ToArray();
                    break;
                case DimensionFor.ForRight:
                    sortedPoints = points.OrderBy(p => p.Y).ToArray();
                    break;
                case DimensionFor.ForUp:
                    sortedPoints = points.OrderBy(p => p.X).ToArray();
                    break;
                case DimensionFor.ForDown:
                    sortedPoints = points.OrderByDescending(p => p.X).ToArray();
                    break;
                default:
                    sortedPoints = points;
                    break;
            }
            // 创建标注数组
            RotatedDimension[] dimensions = new RotatedDimension[sortedPoints.Length - 1];
            for (int i = 0; i < sortedPoints.Length - 1; i++)
            {
                Point3d p1 = sortedPoints[i];
                Point3d p2 = sortedPoints[i + 1];
                RotatedDimension dim = GetDimByTwoPoints(p1, p2, d, dimDirection, isEqualLength);
                dim.DimensionStyle = db.Dimstyle;
                // ✨ 新增：如果传入了layerId，则在此处设置
                if (!layerId.IsNull)
                {
                    dim.LayerId = layerId;
                }
                dimensions[i] = dim;
            }
            return dimensions;
        }
        /// <summary>
        /// 根据多条线段批量创建标注
        /// </summary>
        public static RotatedDimension[] GetDimsByLines(this Line[] lines, double dBetweenLine,
            DimensionFor dimensionFor, bool isEqualLength = false)
        {
            var dims = new List<RotatedDimension>();
            foreach (var line in lines)
            {
                var dim = GetDimByTwoPoints(line.StartPoint, line.EndPoint, dBetweenLine, dimensionFor, isEqualLength);
                dims.Add(dim);
            }
            return dims.ToArray();
        }
        /// <summary>
        /// 根据实体边界创建标注
        /// </summary>
        public static RotatedDimension GetDimByEntityBound(this Extents3d bound, double distance,
            DimensionFor dimensionFor)
        {
            double angle = 0;
            var pointDistance = new Point3d();
            var dim = new RotatedDimension();
            switch (dimensionFor)
            {
                case DimensionFor.ForLeft:
                    angle = Math.PI / 2;
                    pointDistance = bound.MinPoint - new Vector3d(distance, 0, 0);
                    dim.XLine1Point = bound.MinPoint;
                    dim.XLine2Point = bound.MaxPoint;
                    break;
                case DimensionFor.ForRight:
                    angle = Math.PI / 2;
                    pointDistance = bound.MaxPoint + new Vector3d(distance, 0, 0);
                    dim.XLine1Point = bound.MinPoint;
                    dim.XLine2Point = bound.MaxPoint;
                    break;
                case DimensionFor.ForUp:
                    pointDistance = bound.MaxPoint + new Vector3d(0, distance, 0);
                    break;
                case DimensionFor.ForDown:
                    pointDistance = bound.MinPoint - new Vector3d(0, distance, 0);
                    break;
            }
            dim.DimLinePoint = pointDistance;
            dim.Rotation = angle;
            return dim;
        }
        #endregion
        #region 2. 线段处理工具
        /// <summary>
        /// 调整直线起点终点以满足坐标系正向要求
        /// </summary>
        public static Line SortLineSEPointForDIm(this Line line)
        {
            if (Math.Abs(line.StartPoint.X - line.EndPoint.X) < BaseConfig.ToleranceDouble)
            {
                if (line.StartPoint.Y > line.EndPoint.Y)
                {
                    var temp = line.StartPoint;
                    line.StartPoint = line.EndPoint;
                    line.EndPoint = temp;
                }
            }
            else if (Math.Abs(line.StartPoint.X - line.EndPoint.X) > BaseConfig.ToleranceDouble)
            {
                var temp = line.StartPoint;
                line.StartPoint = line.EndPoint;
                line.EndPoint = temp;
            }
            return line;
        }
        /// <summary>
        /// 批量调整直线起点终点以满足坐标系正向要求
        /// </summary>
        public static Line[] SortLineSEPointForDIm(this IEnumerable<Line> lines)
        {
            var lineOut = new List<Line>();
            foreach (var line in lines)
            {
                lineOut.Add(line.SortLineSEPointForDIm());
            }
            return lineOut.ToArray();
        }
        #endregion
        #region 3. 标注修改工具
        /// <summary>
        /// 删除标注点相同的重复标注
        /// </summary>
        public static IEnumerable<RotatedDimension> DeleteSamePointDim(this IEnumerable<RotatedDimension> list)
        {
            return list.GroupBy(x => x, new DimEqualityComparer()).Select(x => x.Key);
        }
        /// <summary>
        /// 删除测量值为零的标注
        /// </summary>
        public static RotatedDimension[] DeleteDimensionZero(this IEnumerable<RotatedDimension> dims)
        {
            var tolerance = 1;
            var list = new List<RotatedDimension>();
            foreach (var dim in dims)
            {
                if (dim.Measurement > tolerance) list.Add(dim);
            }
            return list.ToArray();
        }
        /// <summary>
        /// 删除距离过近的平行标注
        /// </summary>
        public static RotatedDimension[] DeleteNearbyParallelDim(this IEnumerable<RotatedDimension> dims, double distance)
        {
            var s = new DimParallelComparer();
            var groups = dims.GroupBy(x => x, s);
            var dimsOut = dims.ToList();
            foreach (var group in groups)
            {
                var sameDims = group.ToList();
                for (int i = 0; i < sameDims.Count() - 1; i++)
                {
                    var d = GetParallelDimDistance(sameDims[i], sameDims[i + 1]);
                    if (IsDimParallel(sameDims[i], sameDims[i + 1]) && d < distance * Reinforcement.Scale)
                    {
                        dimsOut.Remove(sameDims[i]);
                    }
                }
            }
            return dimsOut.ToArray();
        }
        /// <summary>
        /// 调整标注竖线长度为等长
        /// </summary>
        public static RotatedDimension[] AdjustDimPoint(this IEnumerable<RotatedDimension> dims, DimAlineBy dimAlineBy)
        {
            var shorDim = dims.ShortestDimV();
            var dss = shorDim.DimVLength();
            var shorD = Math.Min(dss[0], dss[1]);
            var longDim = dims.LongestDimV();
            var dLs = longDim.DimVLength();
            var longD = Math.Max(dLs[0], dLs[1]);
            foreach (var dim in dims)
            {
                switch (dimAlineBy)
                {
                    case DimAlineBy.ShortPoint:
                        var dimD11 = dim.DimVLength()[0];
                        var dimD12 = dim.DimVLength()[1];
                        dim.ChangeDimLineLength(dimD11 - shorD, dimD12 - shorD);
                        break;
                    case DimAlineBy.LongPoint:
                        var dimD21 = dim.DimVLength()[0];
                        var dimD22 = dim.DimVLength()[1];
                        dim.ChangeDimLineLength(dimD21 - longD, dimD22 - longD);
                        break;
                }
            }
            return dims.ToArray();
        }
        /// <summary>
        /// 根据基准标注对齐其他标注的位置
        /// </summary>
        public static RotatedDimension[] AdjustDimLinePoint(this IEnumerable<RotatedDimension> dims, RotatedDimension dimBase)
        {
            dims = dims.GetDimSameRoByMaxCount();
            foreach (var dim in dims)
            {
                dim.DimLinePoint = dimBase.DimLinePoint;
            }
            return dims.ToArray();
        }
        #endregion
        #region 4. 标注属性扩展工具
        /// <summary>
        /// 使所有标注的竖线长度相等
        /// </summary>
        public static RotatedDimension[] DimVsEqualLength(this IEnumerable<RotatedDimension> dims)
        {
            var dimShort = dims.ShortestDimV();
            var d = dimShort.DimVLength()[0];
            var list = new List<RotatedDimension>();
            foreach (var dim in dims)
            {
                list.Add(dim.ChangeDimVLineLength(d, d));
            }
            return list.ToArray();
        }
        /// <summary>
        /// 使单个标注的两条竖线长度相等
        /// </summary>
        public static RotatedDimension DimVEqualLength(this RotatedDimension dim)
        {
            var dimPoint = dim.DimVPoint();
            var pIntersection1 = dimPoint[0];
            var pIntersection2 = dimPoint[1];
            var ds = dim.DimVLength();
            var pdis1 = ds[0];
            var pdis2 = ds[1];
            var d = Math.Abs(pdis2 - pdis1);
            if (pdis1 > pdis2)
            {
                var vec = pIntersection1 - dim.XLine1Point;
                dim.XLine1Point = dim.XLine1Point + vec.GetNormal() * d;
            }
            else
            {
                var vec = pIntersection2 - dim.XLine2Point;
                dim.XLine2Point = dim.XLine2Point + vec.GetNormal() * d;
            }
            return dim;
        }
        #endregion
        #region 5. 标注查询工具
        /// <summary>
        /// 获取方向相同且数量最多的标注集合
        /// </summary>
        public static RotatedDimension[] GetDimSameRoByMaxCount(this IEnumerable<RotatedDimension> dims)
        {
            var rotatSortDimG = dims.GroupBy(x => x.Rotation);
            var dicCount = rotatSortDimG.GetGroupCount();
            var rotationMaxCount = dicCount.LastOrDefault().Key;
            return dims.Where(x => x.Rotation == rotationMaxCount).ToArray();
        }
        /// <summary>
        /// 判断多个标注是否在同一直线上
        /// </summary>
        public static bool IsDimInALine(this IEnumerable<RotatedDimension> dims)
        {
            var dimFirst = dims.FirstOrDefault();
            var dimLinePoint = dimFirst.DimLinePoint;
            var vecBase = Vector3d.XAxis.RotateBy(dimFirst.Rotation, Vector3d.ZAxis).GetNormal();
            foreach (var dim in dims)
            {
                if (!dim.DimLinePoint.IsEqualTo(dimLinePoint, BaseConfig.TolerancePoint))
                {
                    var vec = (dim.DimLinePoint - dimLinePoint).GetNormal();
                    if (!(vecBase.IsEqualTo(vec, BaseConfig.ToleranceVec) || vecBase.IsEqualTo(-vec, BaseConfig.ToleranceVec)))
                        return false;
                }
            }
            return true;
        }
        #endregion
        #region 6. 标注分割工具
        /// <summary>
        /// 用直线分割标注(用于添加轴线)
        /// </summary>
        public static RotatedDimension[] DivisionDimByAxis(this IEnumerable<RotatedDimension> dims, Line line)
        {
            var list = new List<RotatedDimension>();
            var oldRoDim = new List<RotatedDimension>();
            foreach (var dim in dims)
            {
                var p1 = dim.DimVPoint()[0];
                var p2 = dim.DimVPoint()[1];
                var dimLine = new Line(p1, p2);
                var ps = line.GetIntersectionPointsByLine(dimLine);
                if (ps.Count() == 1)
                {
                    oldRoDim.Add(dim);
                    var dimN1 = new RotatedDimension
                    {
                        Rotation = dim.Rotation,
                        DimLinePoint = dim.DimLinePoint,
                        XLine1Point = dim.XLine1Point,
                        XLine2Point = dim.XLine1Point + (ps[0] - p1)
                    };
                    var dimN2 = new RotatedDimension
                    {
                        Rotation = dim.Rotation,
                        DimLinePoint = dim.DimLinePoint,
                        XLine1Point = dim.XLine1Point + (ps[0] - p1),
                        XLine2Point = dim.XLine2Point
                    };
                    list.Add(dimN1);
                    list.Add(dimN2);
                    dimN1.ToSpace();
                    dimN2.ToSpace();
                }
                else
                {
                    list.Add(dim);
                }
            }
            oldRoDim.ChangeEntitiesPropertyInDb(x => x.Erase());
            return list.ToArray();
        }
        /// <summary>
        /// 用多条直线分割标注
        /// </summary>
        public static RotatedDimension[] DivisionDimByAxes(this IEnumerable<RotatedDimension> dims, Line[] lines)
        {
            foreach (var line in lines)
            {
                dims = dims.DivisionDimByAxis(line);
            }
            return dims.ToArray();
        }
        #endregion
        #region 7. 多重引线工具
        /// <summary>
        /// 从多重引线获取文本对象
        /// </summary>
        public static MText MleaderText(this MLeader ml)
        {
            var mt = new MText
            {
                TextStyleId = ml.TextStyleId,
                Color = ml.TextColor,
                TextHeight = ml.TextHeight,
                Contents = ml.MText.Contents
            };
            return mt;
        }
        /// <summary>
        /// 获取多重引线文本的各种镜像位置
        /// </summary>
        public static Dictionary<string, Polyline> MleaderMtPositonDic(this MLeader mle, double range)
        {
            var pointa = mle.GetFirstVertex(0);
            var pointb = mle.GetFirstVertex(1);
            var vec = (pointb - pointa).GetNormal();
            var vecV = vec.RotateBy(Math.PI / 2, Vector3d.ZAxis).GetNormal();
            var pointm = pointa + vec * pointa.DistanceTo(pointb) / 2;
            var pointm1 = pointm + vecV * 100;
            var baseP = mle.MText.GetRecByText();
            var mirrorX = mle.GetTextMirrorPosition(pointa, pointb);
            var mirrorY = mle.GetTextMirrorPosition(pointm, pointm1);
            var mleMirror = mle.GetMirrorMle(pointa, pointb);
            var mirrorXy = mleMirror.GetTextMirrorPosition(pointm, pointm1);
            var distance = baseP.MoveEntCopy(vecV * range);
            return new Dictionary<string, Polyline>
            {
                { "baseP", baseP },
                { "mirrorX", mirrorX },
                { "mirrorY", mirrorY },
                { "mirrorXy", mirrorXy },
                { "distance", distance }
            };
        }
        #endregion
        #region 8. 依赖方法
        /// <summary>
        /// 修改标注线的长度
        /// </summary>
        public static RotatedDimension ChangeDimLineLength(this RotatedDimension dim, double length1, double length2)
        {
            var dimPoint = dim.DimVPoint();
            var pIntersection1 = dimPoint[0];
            var pIntersection2 = dimPoint[1];
            var vec1 = pIntersection1 - dim.XLine1Point;
            dim.XLine1Point = dim.XLine1Point + vec1.GetNormal() * length1;
            var vec2 = pIntersection2 - dim.XLine2Point;
            dim.XLine2Point = dim.XLine2Point + vec2.GetNormal() * length2;
            return dim;
        }
        /// <summary>
        /// 修改标注竖线的长度
        /// </summary>
        public static RotatedDimension ChangeDimVLineLength(this RotatedDimension dim, double length1, double length2)
        {
            var dimPoint = dim.DimVPoint();
            var pIntersection1 = dimPoint[0];
            var pIntersection2 = dimPoint[1];
            var vec1 = dim.XLine1Point - pIntersection1;
            dim.XLine1Point = pIntersection1 + vec1.GetNormal() * length1;
            var vec2 = dim.XLine2Point - pIntersection2;
            dim.XLine2Point = pIntersection2 + vec2.GetNormal() * length2;
            return dim;
        }
        /// <summary>
        /// 查找竖线最短的标注
        /// </summary>
        public static RotatedDimension ShortestDimV(this IEnumerable<RotatedDimension> dims)
        {
            var dimN = dims.SortDimVs();
            var shortDim = dimN.FirstOrDefault();
            var firstds = shortDim.DimVLength();
            var baseD = Math.Min(firstds[0], firstds[1]);
            foreach (var dim in dimN)
            {
                var ds = dim.DimVLength();
                var shorD = Math.Min(ds[0], ds[1]);
                if (baseD > shorD)
                {
                    shortDim = dim;
                    baseD = Math.Min(dim.DimVLength()[0], dim.DimVLength()[1]);
                }
            }
            return shortDim;
        }
        /// <summary>
        /// 查找竖线最长的标注
        /// </summary>
        public static RotatedDimension LongestDimV(this IEnumerable<RotatedDimension> dims)
        {
            var dimN = dims.SortDimVs();
            var longDim = dimN.FirstOrDefault();
            var firstds = longDim.DimVLength();
            var baseD = Math.Max(firstds[0], firstds[1]);
            foreach (var dim in dimN)
            {
                var ds = dim.DimVLength();
                var longD = Math.Max(ds[0], ds[1]);
                if (baseD < longD)
                {
                    longDim = dim;
                    baseD = Math.Max(dim.DimVLength()[0], dim.DimVLength()[1]);
                }
            }
            return longDim;
        }
        /// <summary>
        /// 根据竖线长度排序单个标注
        /// </summary>
        public static RotatedDimension SortDimV(this RotatedDimension dim)
        {
            var l1 = dim.DimVLength()[0];
            var l2 = dim.DimVLength()[1];
            if (l1 > l2)
            {
                var temp = dim.XLine1Point;
                dim.XLine1Point = dim.XLine2Point;
                dim.XLine2Point = temp;
            }
            return dim;
        }
        /// <summary>
        /// 根据竖线长度排序多个标注
        /// </summary>
        public static RotatedDimension[] SortDimVs(this IEnumerable<RotatedDimension> dims)
        {
            var list = new List<RotatedDimension>();
            foreach (var dim in dims)
            {
                list.Add(dim.SortDimV());
            }
            return list.ToArray();
        }
        /// <summary>
        /// 计算两个平行标注之间的距离
        /// </summary>
        public static double GetParallelDimDistance(this RotatedDimension dim1, RotatedDimension dim2)
        {
            var dim1Ps = dim1.DimVPoint().OrderBy(x => x.X).ThenBy(x => x.Y).ToArray();
            var dim2Ps = dim2.DimVPoint().OrderBy(x => x.X).ThenBy(x => x.Y).ToArray();
            var seg1 = new LineSegment3d(dim1Ps[0], dim1Ps[1]);
            var seg2 = new LineSegment3d(dim2Ps[0], dim2Ps[1]);
            return seg1.GetDistanceTo(seg2);
        }
        /// <summary>
        /// 判断两个标注是否平行
        /// </summary>
        public static bool IsDimParallel(this RotatedDimension dim1, RotatedDimension dim2)
        {
            var dim1Ps = dim1.DimVPoint().OrderBy(x => x.X).ThenBy(x => x.Y).ToArray();
            var dim2Ps = dim2.DimVPoint().OrderBy(x => x.X).ThenBy(x => x.Y).ToArray();
            var vec1 = (dim1Ps[0] - dim2Ps[0]).GetNormal();
            var vec2 = (dim1Ps[1] - dim2Ps[1]).GetNormal();
            return vec1.IsEqualTo(vec2, BaseConfig.ToleranceVec);
        }
        /// <summary>
        /// 获取标注竖线的长度
        /// </summary>
        public static double[] DimVLength(this RotatedDimension dim)
        {
            var dimPoints = dim.DimVPoint();
            var pIntersection1 = dimPoints[0];
            var pIntersection2 = dimPoints[1];
            return new double[] { pIntersection1.DistanceTo(dim.XLine1Point), pIntersection2.DistanceTo(dim.XLine2Point) };
        }
        /// <summary>
        /// 获取标注线与竖线的交点
        /// </summary>
        public static Point3d[] DimVPoint(this RotatedDimension dim)
        {
            var vecDim = Vector3d.XAxis.RotateBy(dim.Rotation, Vector3d.ZAxis);
            var xline = new Xline { BasePoint = dim.DimLinePoint, SecondPoint = dim.DimLinePoint + vecDim };
            var pIntersection1 = xline.GetClosestPointTo(dim.XLine1Point, vecDim.RotateBy(Math.PI / 2, Vector3d.ZAxis), true);
            var pIntersection2 = xline.GetClosestPointTo(dim.XLine2Point, vecDim.RotateBy(Math.PI / 2, Vector3d.ZAxis), true);
            return new Point3d[] { pIntersection1, pIntersection2 };
        }
        /// <summary>
        /// 获取多重引线文本的镜像位置
        /// </summary>
        public static Polyline GetTextMirrorPosition(this MLeader ent, Point3d a, Point3d b)
        {
            var entId = ent.ToSpace();
            dynamic aent = ent.AcadObject;
            var mirror = aent.Mirror(a.ToArray(), b.ToArray());
            var mirrorCom = (IAcadObject)mirror;
            var id = mirrorCom.ObjectID;
            var idN = new ObjectId((IntPtr)id);
            var entMirror = idN.IdToEntity() as MLeader;
            var poly = entMirror.MText.GetRecByText();
            entMirror.ChangeEntityPropertyInDb(x => x.Erase());
            return poly;
        }
        /// <summary>
        /// 获取镜像后的多重引线对象
        /// </summary>
        public static MLeader GetMirrorMle(this MLeader ent, Point3d a, Point3d b)
        {
            var entId = ent.ToSpace();
            dynamic aent = ent.AcadObject;
            var mirror = aent.Mirror(a.ToArray(), b.ToArray());
            var mirrorCom = (IAcadObject)mirror;
            var id = mirrorCom.ObjectID;
            var idN = new ObjectId((IntPtr)id);
            var entMirror = idN.IdToEntity() as MLeader;
            ent.ChangeEntityPropertyInDb(x => x.Erase());
            return entMirror;
        }
        #endregion
    }
    #region 辅助类
    public class DimEqualityComparer : IEqualityComparer<RotatedDimension>
    {
        public bool Equals(RotatedDimension x, RotatedDimension y)
        {
            if (x == null && y == null) return true;
            if (x == null || y == null) return false;
            var a = GetSamePointDim(x);
            var b = GetSamePointDim(y);
            return a.XLine1Point.IsEqualTo(b.XLine1Point, BaseConfig.TolerancePoint) &&
                   a.XLine2Point.IsEqualTo(b.XLine2Point, BaseConfig.TolerancePoint);
        }
        public int GetHashCode(RotatedDimension dim)
        {
            string hCode = dim.XLine1Point.X.ToString() + dim.XLine1Point.Y.ToString() +
                          dim.XLine2Point.X.ToString() + dim.XLine2Point.Y.ToString();
            return hCode.GetHashCode();
        }
        public static RotatedDimension GetSamePointDim(RotatedDimension dim)
        {
            Point3d[] dimOrder = new Point3d[] { dim.XLine1Point, dim.XLine2Point }.OrderBy(p => p.X).ThenBy(p => p.Y).ToArray();
            return new RotatedDimension { XLine1Point = dimOrder[0], XLine2Point = dimOrder[1] };
        }
    }
    public class DimParallelComparer : IEqualityComparer<RotatedDimension>
    {
        public bool Equals(RotatedDimension x, RotatedDimension y)
        {
            if (x == null && y == null) return true;
            if (x == null || y == null) return false;
            return (x.Rotation - y.Rotation) < BaseConfig.ToleranceDouble &&
                   (x.Measurement - y.Measurement) < BaseConfig.ToleranceDouble;
        }
        public int GetHashCode(RotatedDimension dim)
        {
            string hCode = dim.Rotation.ToString("N6") + dim.Measurement.ToString("N6");
            return hCode.GetHashCode();
        }
    }
    #endregion
}