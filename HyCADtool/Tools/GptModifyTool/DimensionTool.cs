//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.Geometry;
//using Autodesk.AutoCAD.Interop.Common;
//using HyCADTool.Config;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//namespace HyCADTool.Tools
//{
//    public static partial class EtGpt
//    {
//        public enum DimAlineBy
//        {
//            ShortPoint,
//            LongPoint,
//        }
//        #region 标注区域
//        //public static RotatedDimension GetDimByTwoPoints(Point3d p1, Point3d p2,
//        // double dBetweenLine, DimensionFor dimensionFor, bool isEqualLength = false)
//        //{
//        //    double angele = 0;
//        //    var piontDistance = new Point3d();
//        //    var dim = new RotatedDimension();
//        //    switch (dimensionFor)
//        //    {
//        //        case DimensionFor.ForLeft:
//        //            angele = Math.PI / 2;
//        //            if (p1.X < p2.X)
//        //            {
//        //                piontDistance = p1 - new Vector3d(dBetweenLine, 0, 0);
//        //            }
//        //            else
//        //            {
//        //                piontDistance = p2 - new Vector3d(dBetweenLine, 0, 0);
//        //            }
//        //            break;
//        //        case DimensionFor.ForRight:
//        //            angele = Math.PI / 2;
//        //            if (p1.X < p2.X)
//        //            {
//        //                piontDistance = p2 + new Vector3d(dBetweenLine, 0, 0);
//        //            }
//        //            else
//        //            {
//        //                piontDistance = p1 + new Vector3d(dBetweenLine, 0, 0);
//        //            }
//        //            break;
//        //        case DimensionFor.ForUp:
//        //            if (p1.Y < p2.Y)
//        //            {
//        //                piontDistance = p2 + new Vector3d(0, dBetweenLine, 0);
//        //            }
//        //            else
//        //            {
//        //                piontDistance = p1 + new Vector3d(0, dBetweenLine, 0);
//        //            }
//        //            break;
//        //        case DimensionFor.ForDown:
//        //            if (p1.Y < p2.Y)
//        //            {
//        //                piontDistance = p1 - new Vector3d(0, dBetweenLine, 0);
//        //            }
//        //            else
//        //            {
//        //                piontDistance = p2 - new Vector3d(0, dBetweenLine, 0);
//        //            }
//        //            break;
//        //    }
//        //    dim.XLine1Point = p1;
//        //    dim.XLine2Point = p2;
//        //    dim.DimLinePoint = piontDistance;
//        //    dim.Rotation = angele;
//        //    if (isEqualLength)
//        //    {
//        //        dim.DimVEqualLength();
//        //    }
//        //    return dim;
//        //}
//        public static RotatedDimension GetDimByTwoPoints
//            (Point3d p1, Point3d p2, double dBetweenLine, DimensionFor dimensionFor, bool isEqualLength = false)
//        {
//            // 初始化变量，angle为旋转角度，pointDistance为标注线位置点
//            double angle = 0;
//            var pointDistance = new Point3d();
//            var dim = new RotatedDimension();
//            // 根据不同的标注方向设置对应的旋转角度和标注线位置
//            switch (dimensionFor)
//            {
//                case DimensionFor.ForLeft:
//                    // 左侧标注，设置旋转角度为90度
//                    angle = Math.PI / 2;
//                    // 根据点的位置确定标注线位置
//                    if (p1.X < p2.X)
//                    {
//                        pointDistance = p1 - new Vector3d(dBetweenLine, 0, 0);
//                    }
//                    else
//                    {
//                        pointDistance = p2 - new Vector3d(dBetweenLine, 0, 0);
//                    }
//                    break;
//                case DimensionFor.ForRight:
//                    // 右侧标注，设置旋转角度为90度
//                    angle = Math.PI / 2;
//                    // 根据点的位置确定标注线位置
//                    if (p1.X < p2.X)
//                    {
//                        pointDistance = p2 + new Vector3d(dBetweenLine, 0, 0);
//                    }
//                    else
//                    {
//                        pointDistance = p1 + new Vector3d(dBetweenLine, 0, 0);
//                    }
//                    break;
//                case DimensionFor.ForUp:
//                    // 上侧标注，设置旋转角度为0度
//                    angle = 0;
//                    // 根据点的位置确定标注线位置
//                    if (p1.Y < p2.Y)
//                    {
//                        pointDistance = p2 + new Vector3d(0, dBetweenLine, 0);
//                    }
//                    else
//                    {
//                        pointDistance = p1 + new Vector3d(0, dBetweenLine, 0);
//                    }
//                    break;
//                case DimensionFor.ForDown:
//                    // 下侧标注，设置旋转角度为0度
//                    angle = 0;
//                    // 根据点的位置确定标注线位置
//                    if (p1.Y < p2.Y)
//                    {
//                        pointDistance = p1 - new Vector3d(0, dBetweenLine, 0);
//                    }
//                    else
//                    {
//                        pointDistance = p2 - new Vector3d(0, dBetweenLine, 0);
//                    }
//                    break;
//            }
//            // 设置标注对象的属性
//            dim.XLine1Point = p1;
//            dim.XLine2Point = p2;
//            dim.DimLinePoint = pointDistance;
//            dim.Rotation = angle;
//            // 如果需要相等长度标注，调用相应方法
//            if (isEqualLength)
//            {
//                dim.DimVEqualLength();
//            }
//            return dim;
//        }
//        public static RotatedDimension[] GetDimsByLines(this Line[] lines, double dBetweenLine,
//            DimensionFor dimensionFor, bool isEqualLength = false)
//        {
//            var dims = new List<RotatedDimension>();
//            foreach (var line in lines)
//            {
//                var dim = GetDimByTwoPoints(line.StartPoint, line.EndPoint, dBetweenLine, dimensionFor, isEqualLength);
//                dims.Add(dim);
//            }
//            return dims.ToArray();
//        }
//        public static RotatedDimension GetDimByEntityBound(this Extents3d bound,
//           double distance, DimensionFor dimensionFor)
//        {
//            double angele = 0;
//            var piontDistance = new Point3d();
//            var dim = new RotatedDimension();
//            switch (dimensionFor)
//            {
//                case DimensionFor.ForLeft:
//                    angele = Math.PI / 2;
//                    piontDistance = bound.MinPoint - new Vector3d(distance, 0, 0);
//                    dim.XLine1Point = bound.MinPoint;
//                    dim.XLine2Point = bound.MaxPoint;
//                    break;
//                case DimensionFor.ForRight:
//                    angele = Math.PI / 2;
//                    piontDistance = bound.MaxPoint + new Vector3d(distance, 0, 0);
//                    dim.XLine1Point = bound.MinPoint;
//                    dim.XLine2Point = bound.MaxPoint;
//                    break;
//                case DimensionFor.ForUp:
//                    piontDistance = bound.MaxPoint + new Vector3d(0, distance, 0);
//                    break;
//                case DimensionFor.ForDown:
//                    piontDistance = bound.MinPoint - new Vector3d(0, distance, 0);
//                    break;
//            }
//           dim.DimLinePoint = piontDistance;
//            dim.Rotation = angele;
//            return dim;
//        }
//        /// <summary>
//        /// 改变直线的起点终点，使它满足坐标系正向要求
//        /// </summary>
//        /// <param name="line"></param>
//        /// <returns></returns>
//        public static Line SortLineSEPointForDIm(this Line line)
//        {
//            if (Math.Abs(line.StartPoint.X - line.EndPoint.X) < BaseConfig.ToleranceDouble)
//            {
//                if (line.StartPoint.Y > line.EndPoint.Y)
//                {
//                    var temp = line.StartPoint;
//                    line.StartPoint = line.EndPoint;
//                    line.EndPoint = temp;
//                }
//           }
//            else if (Math.Abs(line.StartPoint.X - line.EndPoint.X) > BaseConfig.ToleranceDouble)
//            {
//                var temp = line.StartPoint;
//                line.StartPoint = line.EndPoint;
//                line.EndPoint = temp;
//            }
//            return line;
//        }
//        public static Line[] SortLineSEPointForDIm(this IEnumerable<Line> lines)
//        {
//            var lineOut = new List<Line>();
//            foreach (var line in lines)
//            {
//                lineOut.Add(line.SortLineSEPointForDIm());
//            }
//            return lineOut.ToArray();
//        }
//       #endregion
//        #region 修改区域
//       /// <summary>
//        /// 删除标注点相同的标注
//        /// </summary>
//        /// <param name="list"></param>
//        /// <returns></returns>
//        public static IEnumerable<RotatedDimension> DeleteSamePointDim(this IEnumerable<RotatedDimension> list)
//        {
//            var value = list.GroupBy(x => x, new DimEqualityComparer()).ToArray().Select(x => x.Key);
//            return value;
//        }
//        public static RotatedDimension[] DeleteDimensionZero(this IEnumerable<RotatedDimension> dims)
//        {
//            var list = new List<RotatedDimension>();
//            foreach (var dim in dims)
//            {
//                if (dim.Measurement > BaseConfig.ToleranceDouble)
//                {
//                    list.Add(dim);
//                }
//            }
//            return list.ToArray();
//        }
//        public static RotatedDimension[] DeleteNearbyParallelDim(this IEnumerable<RotatedDimension> dims, double distance)
//        {
//            var s = new DimParallelComparer();
//            var groups = dims.GroupBy(x => x, s);
//            var dimsOut = dims.ToList();
//            foreach (var item in dims)
//            {
//                var ro = item.Rotation;
//                var me = item.Measurement;
//            }
//            foreach (var group in groups)
//            {
//                var sameDims = group.ToList();
//                for (int i = 0; i < sameDims.Count() - 1; i++)
//                {
//                    var d = GetParallelDimDistance(sameDims[i], sameDims[i + 1]);
//                    var b = IsDimParallel(sameDims[i], sameDims[i + 1]);
//                    if (b)
//                    {
//                        if (d < distance * Reinforcement.Scale)
//                        {
//                            dimsOut.Remove(sameDims[i]);
//                       }
//                    }
//                }
//                //var dimsGroup = dims.ToArray();           
//                //var dimsArray = dims.OrderBy(x=>x.XLine1Point.X).ThenBy(x=>x.XLine1Point.Y).ToList();
//                //for (int i = 0; i < dimsArray.Count() - 1; i++)
//                //{
//                //    var d = GetParallelDimDistance(dimsArray[i], dimsArray[i + 1]);
//                //    if (IsDimParallel(dimsArray[i], dimsArray[i + 1]))
//                //    {
//                //        if (d < distance * Scale)
//                //        {
//                //            dimsArray.Remove(dimsArray[i ]);
//                //            i--;
//                //        }
//                //    }                
//                //}
//           }
//            return dimsOut.ToArray();
//        }
//        //public static RotatedDimension[] AdjustOutSideDim(this IEnumerable<RotatedDimension> dims)
//        //{
//       //}
//       /// <summary>
//        /// 把标注竖线长度变为等长
//        /// </summary>
//        /// <param name="dims"></param>
//        /// <param name="dimAlineBy"></param>
//        /// <returns></returns>
//        /// 
//        public static RotatedDimension[] AdjustDimPoint(this IEnumerable<RotatedDimension> dims, DimAlineBy dimAlineBy)
//        {
//            //  1 找到所选标注中，角度值最多的一个值，仅对这些值进行操作         
//            var shorDim = dims.ShortestDimV();
//            var dss = shorDim.DimVLength();
//            var shorD = Math.Min(dss[0], dss[1]);
//            var longDim = dims.LongestDimV();
//            var dLs = longDim.DimVLength();
//            var longD = Math.Max(dLs[0], dLs[1]);
//            foreach (var dim in dims)
//            {
//                switch (dimAlineBy)
//                {
//                    case DimAlineBy.ShortPoint:
//                        var dimD11 = dim.DimVLength()[0];
//                        var dimD12 = dim.DimVLength()[1];
//                        dim.ChangeDimLineLength(dimD11 - shorD, dimD12 - shorD);
//                        break;
//                    case DimAlineBy.LongPoint:
//                        var dimD21 = dim.DimVLength()[0];
//                        var dimD22 = dim.DimVLength()[1];
//                        dim.ChangeDimLineLength(dimD21 - longD, dimD22 - longD);
//                        break;
//                }
//            }
//            return dims.ToArray();
//        }
//        /// <summary>
//        /// 对齐标注
//        /// </summary>
//        /// <param name="dims"></param>
//        /// <param name="dimBase"></param>
//        /// <returns></returns>
//        public static RotatedDimension[] AdjustDimLinePoint(this IEnumerable<RotatedDimension> dims, RotatedDimension dimBase)
//        {
//            //  1 找到所选标注中，角度值最多的一个值，仅对这些值进行操作
//           dims = dims.GetDimSameRoByMaxCount();
//            foreach (var dim in dims)
//            {
//                dim.DimLinePoint = dimBase.DimLinePoint;
//            }
//            return dims.ToArray();
//        }
//       public static RotatedDimension ChangeDimLineLength(this RotatedDimension dim, double length1, double length2)
//        {
//            //  求交点
//            var dimPoint = dim.DimVPoint();
//            var pIntersection1 = dimPoint[0];
//            var pIntersection2 = dimPoint[1];
//            //  改变标注竖线1长度
//            var vec1 = pIntersection1 - dim.XLine1Point;
//            var pChange1 = dim.XLine1Point + vec1.GetNormal() * length1;
//            dim.XLine1Point = pChange1;
//            //  改变标注竖线2长度
//            var vec2 = pIntersection2 - dim.XLine2Point;
//            var pChange2 = dim.XLine2Point + vec2.GetNormal() * length2;
//            dim.XLine2Point = pChange2;
//            return dim;
//        }
//       public static RotatedDimension ChangeDimVLineLength(this RotatedDimension dim, double length1, double length2)
//        {
//            //  求交点
//            var dimPoint = dim.DimVPoint();
//            var pIntersection1 = dimPoint[0];
//            var pIntersection2 = dimPoint[1];
//            //  改变标注竖线1长度
//            var vec1 = dim.XLine1Point - pIntersection1;
//            var pChange1 = pIntersection1 + vec1.GetNormal() * length1;
//            dim.XLine1Point = pChange1;
//            //  改变标注竖线2长度
//            var vec2 = dim.XLine2Point - pIntersection2;
//            var pChange2 = pIntersection2 + vec2.GetNormal() * length2;
//            dim.XLine2Point = pChange2;
//            return dim;
//        }
//       /// <summary>
//        /// 判断标注是否在一条直线上
//        /// </summary>
//        /// <param name="dims"></param>
//        /// <returns></returns>
//        public static bool IsDimInALine(this IEnumerable<RotatedDimension> dims)
//        {
//            var dimFirst = dims.FirstOrDefault();
//            var dimLinePoint = dims.FirstOrDefault().DimLinePoint;
//           var vecBase = Vector3d.XAxis.RotateBy(dimFirst.Rotation, Vector3d.ZAxis).GetNormal();
//           var b = false;
//            foreach (var dim in dims)
//            {
//                if (dim.DimLinePoint.IsEqualTo(dimLinePoint, BaseConfig.TolerancePoint))
//                {
//                    b = true;
//                }
//                else
//                {
//                    var vec = (dim.DimLinePoint - dimLinePoint).GetNormal();
//                    if (!(vecBase.IsEqualTo(vec, BaseConfig.ToleranceVec) || vecBase.IsEqualTo(-vec, BaseConfig.ToleranceVec)))
//                    {
//                        return false;
//                    }
//                }
//           }
//            return b;
//        }
//        #endregion
//        #region 标注属性扩展
//        /// <summary>
//        /// 求所有标注中标注竖线最短的标注
//        /// </summary>
//        /// <param name="dims"></param>
//        /// <returns></returns>
//        /// 
//        public static RotatedDimension[] DimVsEqualLength(this IEnumerable<RotatedDimension> dims)
//        {
//            var dimShort = dims.ShortestDimV();
//            var d = dimShort.DimVLength()[0];
//            var list = new List<RotatedDimension>();
//            foreach (var dim in dims)
//            {
//                list.Add(dim.ChangeDimVLineLength(d, d));
//            }
//            return list.ToArray();
//        }
//        public static RotatedDimension ShortestDimV(this IEnumerable<RotatedDimension> dims)
//        {
//            var dimN = dims.SortDimVs();
//            var shortDim = dimN.FirstOrDefault();
//            var firstds = shortDim.DimVLength();
//            var firstshorD = Math.Min(firstds[0], firstds[1]);
//            var baseD = firstshorD;
//            foreach (var dim in dimN)
//            {
//                var ds = dim.DimVLength();
//                var shorD = Math.Min(ds[0], ds[1]);
//                if (baseD > shorD)
//                {
//                    shortDim = dim;
//                    var tempD = shortDim.DimVLength()[0];
//                    baseD = tempD;
//               }
//            }
//            return shortDim;
//        }
//        public static RotatedDimension LongestDimV(this IEnumerable<RotatedDimension> dims)
//        {
//            var dimN = dims.SortDimVs();
//            var LongDim = dimN.FirstOrDefault();
//            var firstds = LongDim.DimVLength();
//            var firstLongD = Math.Max(firstds[0], firstds[1]);
//            var baseD = firstLongD;
//            foreach (var dim in dimN)
//            {
//                var ds = dim.DimVLength();
//                var shorD = Math.Max(ds[0], ds[1]);
//                if (baseD < shorD)
//                {
//                    LongDim = dim;
//                    var tempD = LongDim.DimVLength()[0];
//                    baseD = tempD;
//                }
//            }
//            var a = LongDim.DimVLength();
//            var b = Math.Max(a[0], a[1]);
//            return LongDim;
//        }
//        public static RotatedDimension SortDimV(this RotatedDimension dim)
//        {
//            var l1 = dim.DimVLength()[0];
//            var l2 = dim.DimVLength()[1];
//            if (l1 > l2)
//            {
//                var temp = dim.XLine1Point;
//                dim.XLine1Point = dim.XLine2Point;
//                dim.XLine2Point = temp;
//            }
//            return dim;
//        }
//        public static RotatedDimension[] SortDimVs(this IEnumerable<RotatedDimension> dims)
//        {
//            var list = new List<RotatedDimension>();
//            foreach (var dim in dims)
//            {
//                var dimN = dim.SortDimV();
//                list.Add(dimN);
//            }
//            return list.ToArray();
//        }
//       public static RotatedDimension DimVEqualLength(this RotatedDimension dim)
//        {
//            //  求交点
//            var dimPoint = dim.DimVPoint();
//            var pIntersection1 = dimPoint[0];
//            var pIntersection2 = dimPoint[1];
//            // 求长度
//            var ds = dim.DimVLength();
//            var pdis1 = ds[0];
//            var pdis2 = ds[1];
//            // 判断那根标注线长，把长线缩短
//            var d = Math.Abs(pdis2 - pdis1);
//            if (pdis1 > pdis2)
//            {
//                var vec = pIntersection1 - dim.XLine1Point;
//                var pChange1 = dim.XLine1Point + vec.GetNormal() * d;
//                dim.XLine1Point = pChange1;
//            }
//            else
//            {
//                var vec = pIntersection2 - dim.XLine2Point;
//                var pChange2 = dim.XLine2Point + vec.GetNormal() * d;
//                dim.XLine2Point = pChange2;
//            }
//            return dim;
//        }
//        public static double GetParallelDimDistance(this RotatedDimension dim1, RotatedDimension dim2)
//        {
//            //var dim1P1 = dim1.DimPoint()[0];
//            //var dim1P2 = dim1.DimPoint()[1];
//            //var dim2P1 = dim2.DimPoint()[0];
//            //var dim2P2 = dim2.DimPoint()[1];
//            //var seg1 = new LineSegment3d(dim1P1, dim1P2);
//            //var seg2 = new LineSegment3d(dim2P1, dim2P2);
//            var dim1Ps = dim1.DimVPoint().OrderBy(x => x.X).ThenBy(x => x.Y).ToArray();
//            var dim2Ps = dim2.DimVPoint().OrderBy(x => x.X).ThenBy(x => x.Y).ToArray();
//            var seg1 = new LineSegment3d(dim1Ps[0], dim1Ps[1]);
//            var seg2 = new LineSegment3d(dim2Ps[0], dim2Ps[1]);
//            return seg1.GetDistanceTo(seg2);
//        }
//        public static bool IsDimParallel(this RotatedDimension dim1, RotatedDimension dim2)
//        {
//            var dim1Ps = dim1.DimVPoint().OrderBy(x => x.X).ThenBy(x => x.Y).ToArray();
//            var dim2Ps = dim2.DimVPoint().OrderBy(x => x.X).ThenBy(x => x.Y).ToArray();
//            var vec1 = (dim1Ps[0] - dim2Ps[0]).GetNormal();
//            var vec2 = (dim1Ps[1] - dim2Ps[1]).GetNormal();
//            var vec = (dim1Ps[0] - dim1Ps[1]);
//            var a = vec.DotProduct(vec1);
//            var c = vec.DotProduct(vec2);
//            var b1 = vec.DotProduct(vec1) < BaseConfig.ToleranceDouble;
//            var b2 = vec.DotProduct(vec2) < BaseConfig.ToleranceDouble;
//            var b = vec1.IsEqualTo(vec2, BaseConfig.ToleranceVec) && b1 && b2;
//            return vec1.IsEqualTo(vec2, BaseConfig.ToleranceVec);
//        }
//        public static double[] DimVLength(this RotatedDimension dim)
//        {
//            var dimPoints = dim.DimVPoint();
//            var pIntersection1 = dimPoints[0];
//            var pIntersection2 = dimPoints[1];
//            var pdis1 = pIntersection1.DistanceTo(dim.XLine1Point);
//            var pdis2 = pIntersection2.DistanceTo(dim.XLine2Point);
//            var d = new double[] { pdis1, pdis2 };
//            return d;
//        }
//        /// <summary>
//        /// 
//        /// </summary>
//        /// <param name="dim"></param>
//        /// <returns>标注点标注线的点</returns>
//        public static Point3d[] DimVPoint(this RotatedDimension dim)
//        {
//            //  1 得到标注向量
//            var vecDim = Vector3d.XAxis.RotateBy(dim.Rotation, Vector3d.ZAxis);            //  2 做辅助线 （无限长）（覆盖标注尺寸线）
//            var xline = new Xline();
//            xline.BasePoint = dim.DimLinePoint;
//            xline.SecondPoint = dim.DimLinePoint + vecDim;
//            //  3 求标注点，到辅助线垂线的交点
//            var pIntersection1 = xline.GetClosestPointTo(dim.XLine1Point, vecDim.RotateBy(Math.PI / 2, Vector3d.ZAxis), true);
//            var pIntersection2 = xline.GetClosestPointTo(dim.XLine2Point, vecDim.RotateBy(Math.PI / 2, Vector3d.ZAxis), true);
//            var points = new Point3d[] { pIntersection1, pIntersection2 };
//            return points;
//        }
//        #endregion
//        #region 标注属性查询
//        /// <summary>
//        /// 获取到相同方向的标注
//        /// </summary>
//        /// <param name="dims"></param>
//        /// <returns></returns>
//        public static RotatedDimension[] GetDimSameRoByMaxCount(this IEnumerable<RotatedDimension> dims)
//        {
//            //  1 找到所选标注中，角度值最多的一个值，仅对这些值进行操作
//            var rotatSortDimG = dims.GroupBy(x => x.Rotation);
//            var dicCount = rotatSortDimG.GetGroupCount();
//            var rotationMaxCount = dicCount.LastOrDefault().Key;
//            dims = dims.Where(x => x.Rotation == rotationMaxCount);
//            return dims.ToArray();
//        }
//        #endregion
//        #region 用直线分割标注，用于添加轴线操作
//       public static RotatedDimension[] DivisionDimByAxis(this IEnumerable<RotatedDimension> dims,
//            Line line)
//        {
//            var list = new List<RotatedDimension>();
//            var oldRoDim = new List<RotatedDimension>();
//            foreach (var dim in dims)
//            {
//                var p1 = dim.DimVPoint()[0];
//                var p2 = dim.DimVPoint()[1];
//                var dimLine = new Line(p1, p2);
//                var ps = line.GetIntersectionPointsByLine(dimLine);                
//                if (ps.Count() == 1)
//                {
//                    oldRoDim.Add(dim);
//                    var dimN1 = new RotatedDimension();
//                    dimN1.Rotation = dim.Rotation;
//                    dimN1.DimLinePoint = dim.DimLinePoint;
//                    dimN1.XLine1Point = dim.XLine1Point;
//                    dimN1.XLine2Point = dim.XLine1Point + (ps[0] - p1);
//                   var dimN2 = new RotatedDimension();
//                    dimN2.Rotation = dim.Rotation;
//                    dimN2.DimLinePoint = dim.DimLinePoint;
//                    dimN2.XLine1Point = dim.XLine1Point + (ps[0] - p1);
//                    dimN2.XLine2Point = dim.XLine2Point;
//                    list.Add(dimN1);
//                    list.Add(dimN2);
//                    dimN1.ToSpace();
//                    dimN2.ToSpace();
//                }
//                else
//                {
//                    list.Add(dim);
//                }
//            }
//            oldRoDim.ChangeEntitiesPropertyInDb(x => x.Erase());
//            return list.ToArray();
//        }
//        public static RotatedDimension[] DivisionDimByAxes(this IEnumerable<RotatedDimension> dims,
//           Line[] lines)
//        {           
//           foreach (var line in lines)
//            {
//                dims = dims.DivisionDimByAxis(line);              
//            }
//            return dims.ToArray();
//        }
//        #endregion
//        #region 设置多重引线样式 ,添加修改多重引线      
//       public static MText MleaderText(this MLeader ml)
//        {
//           var mt = new MText();
//            // 设置标注字体               
//            mt.TextStyleId = ml.TextStyleId;
//            mt.Color = ml.TextColor;
//            mt.TextHeight = ml.TextHeight;
//            mt.Contents = ml.MText.Contents;
//           return mt;
//        }
//       public static Dictionary<string, Polyline> MleaderMtPositonDic(this MLeader mle, double range)
//        {
//            //获取Mleader 标注点位置 为镜像使用
//            var pointa = mle.GetFirstVertex(0);
//            //var pointb = mle.GetFirstVertex(mle.LeaderLineCount-1 );
//            var pointb = mle.GetFirstVertex(1);
//            var vec = (pointb - pointa).GetNormal();
//            var vecV = vec.RotateBy(Math.PI / 2, Vector3d.ZAxis).GetNormal();
//            var pointm = pointa + vec * pointa.DistanceTo(pointb) / 2;
//            var pointm1 = pointm + vecV * 100;
//            pointa.MakeMark();
//            pointb.MakeMark();
//            //pointm.MakeMark();
//            //pointm1.MakeMark();
//           var baseP = mle.MText.GetRecByText();
//            //var mirrorXText = mle.MText.MirrorMText(pointa, pointb);
//            var mirrorX = mle.GetTextMirrorPosition(pointa, pointb);
//            var mirrorY = mle.GetTextMirrorPosition(pointm, pointm1);
//            var mleMirror = mle.GetMirrorMle(pointa, pointb);
//            var mirrorXy = mleMirror.GetTextMirrorPosition(pointm, pointm1);
//            var distance = baseP.MoveEntCopy(vecV * range);
//            //mleMirror.ChangeEntityPropertyInDb(x => x.Erase());
//            ////var baseP = mle.MText.GetRecByMt();
//            //var mirrorX = baseP.MirrorEntCopy(pointa, pointb);
//            //var mirrorY = baseP.MirrorEntCopy(pointm, pointm1);
//            //var mirrorXy = mirrorX.MirrorEntCopy(pointm, pointm1);
//            //var distance = baseP.MoveEntCopy(vecV * range);   
//            var dic = new Dictionary<string, Polyline>();
//            dic.Add("baseP", baseP);
//            dic.Add("mirrorX", mirrorX);
//            dic.Add("mirrorY", mirrorY);
//            dic.Add("mirrorXy", mirrorXy);
//            dic.Add("distance", distance);
//            return dic;
//        }
//        public static Polyline GetTextMirrorPosition(this MLeader ent, Point3d a, Point3d b)
//        {
//            var entId = ent.ToSpace();
//            var db = Application.DocumentManager.MdiActiveDocument.Database;
//            dynamic aent = ent.AcadObject;
//            var mirror = aent.Mirror(a.ToArray(), b.ToArray());
//            var mirrorCom = (IAcadObject)mirror;
//            var id = mirrorCom.ObjectID;
//            var idN = new ObjectId((IntPtr)id);
//            var entMirror = idN.IdToEntity() as MLeader;
//            var poly = new Polyline();
//            poly = entMirror.MText.GetRecByText();
//            entMirror.ChangeEntityPropertyInDb(x => x.Erase());
//            return poly;
//        }
//        public static MLeader GetMirrorMle(this MLeader ent, Point3d a, Point3d b)
//        {
//            var entId = ent.ToSpace();
//            var db = Application.DocumentManager.MdiActiveDocument.Database;
//            dynamic aent = ent.AcadObject;
//            var mirror = aent.Mirror(a.ToArray(), b.ToArray());
//            var mirrorCom = (IAcadObject)mirror;
//            var id = mirrorCom.ObjectID;
//            var idN = new ObjectId((IntPtr)id);
//            var entMirror = idN.IdToEntity() as MLeader;
//            var poly = new Polyline();
//            poly = entMirror.MText.GetRecByText();
//            ent.ChangeEntityPropertyInDb(x => x.Erase());
//            return entMirror;
//        }
//       #endregion
//    }
//    public class DimEqualityComparer : IEqualityComparer<RotatedDimension>
//    {
//        public bool Equals(RotatedDimension x, RotatedDimension y)
//        {
//            if (x == null && y == null)
//                return true;
//            else if (x == null || y == null)
//                return false;
//            else
//            {
//                var a = GetSamePointDim(x);
//                var b = GetSamePointDim(y);
//                //if (a.XLine1Point.X == b.XLine1Point.X && a.XLine1Point.Y == b.XLine1Point.Y
//                //    && a.XLine2Point.X == b.XLine2Point.X && a.XLine2Point.Y == b.XLine2Point.Y)
//                if (a.XLine1Point.IsEqualTo(b.XLine1Point, BaseConfig.TolerancePoint) &&
//                a.XLine2Point.IsEqualTo(b.XLine2Point, BaseConfig.TolerancePoint))
//                {
//                    return true;
//                }
//                else
//                {
//                    return false;
//                }
//            }
//       }
//        public int GetHashCode(RotatedDimension dim)
//        {
//            string hCode = dim.XLine1Point.X.ToString() + dim.XLine1Point.Y.ToString() + dim.XLine2Point.X.ToString() + dim.XLine2Point.Y.ToString();
//            return hCode.GetHashCode();
//        }
//        public static RotatedDimension GetSamePointDim(RotatedDimension dim)
//        {
//            Point3d[] dimOrder = new Point3d[] { dim.XLine1Point, dim.XLine2Point };
//            dimOrder = dimOrder.OrderBy(p => p.X).ThenBy(p => p.Y).ToArray();
//            var dimNEq = new RotatedDimension();
//            dimNEq.XLine1Point = dimOrder[0];
//            dimNEq.XLine2Point = dimOrder[1];
//            return dimNEq;
//        }
//    }
//    public class DimParallelComparer : IEqualityComparer<RotatedDimension>
//    {
//        public bool Equals(RotatedDimension x, RotatedDimension y)
//        {
//            if (x == null && y == null)
//                return true;
//            else if (x == null || y == null)
//                return false;
//            else
//            {
//                var a1 = x.Rotation;
//                var a2 = x.Measurement;
//                var c1 = y.Rotation;
//                var c2 = y.Measurement;
//                var b1 = (a1 - c1) < BaseConfig.ToleranceDouble;
//                var b2 = (a2 - c2) < BaseConfig.ToleranceDouble;
//                if (b1 && b2)
//                {
//                    return true;
//                }
//               else
//                {
//                    return false;
//                }
//            }
//       }
//        public int GetHashCode(RotatedDimension dim)
//        {
//            string hCode = dim.Rotation.ToString("N6") + dim.Measurement.ToString("N6");
//            return hCode.GetHashCode();
//        }
//   }
//}
