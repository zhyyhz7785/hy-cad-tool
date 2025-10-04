using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Config;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
namespace HyCADTool
{
    public static partial class Reinforcement
    {
        #region 生成配筋
        public static void SetProperties(Polyline boundary)
        {
            //边界框预处理
            boundary.ChangeEntityPropertyInDb(x =>
            {
                x.ResetPolyVertex();
                x.SetPolyLineClockWise();
                x.RemovePolyDuplicateVertices();
                x.Closed = true;
            });
            Boundary = boundary;
            //生成钢筋属性            
            SubReinforcements = GetSubReinforcements(boundary, -ProtectionThickness);
            SubReinforcements = ConnectReinByCondition(SubReinforcements);
            SubReinforcementWithAnchors = GetSubReinforcementWithAnchors(SubReinforcements);
            SubReinforcementWithAnchorsAddhooks = SubReinforcementWithAnchors.Addhook();
            //生成点钢
            DotReinCenterPoly = GetDotReinCenterPoly(boundary, DotReinOffset);
            DotReinPoints = AddDotRein(Boundary, DotSeparation, DotStartDistance, DotReinOffset);
            DotRein = DotReinPoints.PointsToDotRein();
            //生成减少数量点钢
            ReduceDotReinPoints = AddReduceDotRein(Boundary, DotSeparation, DotReinOffset);
            ReduceDotRein = ReduceDotReinPoints.PointsToDotRein();
            //生成钢筋标注
            var a = $"\\U+E532{Reinforcement.RebarDiameter}@{Reinforcement.RebarSpacing}";
            Mleaders = AddMleaders(DotSeparation, a);
        }
        public static void GenerateReinforcement(Polyline boundary)
        {
            Reinforcement.SetProperties(boundary);            
            ZTools.CreateMultipleLayers(("01_hy_1钢筋_线钢筋", 1), ("01_hy_1钢筋_点钢筋", 5), ("00_hy_3公共_标注1_外", 3), ("00_hy_3公共_标注3_引线", 92));
            Tools.ZTools.SetCurrentLayer("01_hy_1钢筋_线钢筋");
            Reinforcement.SubReinforcementWithAnchorsAddhooks.ToSpace();
            Tools.ZTools.SetCurrentLayer("01_hy_1钢筋_点钢筋");
            Reinforcement.ReduceDotRein.ToSpace();
            //Application.SetSystemVariable("CELWEIGHT", Convert.ToInt16(-1));
            Tools.ZTools.SetCurrentLayer("00_hy_3公共_标注3_引线");
            Reinforcement.Mleaders.ToSpace();
        }
        #endregion
        #region 主要方法
        /// <summary>
        /// 按混凝土轮廓，给钢筋分段
        /// </summary>
        /// <param name="polyline"></param>
        /// <returns></returns>
        public static Polyline[] GetSubReinforcements(this Polyline boundary, double d)
        {
            var boundaryoffset = boundary.GetOffsetCurves(d)[0] as Polyline;
            List<Polyline> reinforcement = new List<Polyline>();
            var angles = boundaryoffset.GetPolySegmentAngle();
            LineSegment3d segment = new LineSegment3d();
            for (int i = 0; i < boundaryoffset.NumberOfVertices; i++)
            {
                var tempPolyLine = new Polyline();
                do
                {
                    segment = boundaryoffset.GetLineSegmentAt(i);
                    tempPolyLine.AddVertexAt(tempPolyLine.NumberOfVertices, segment.StartPoint.Convert2d(new Plane(Point3d.Origin, Vector3d.ZAxis)), 0, 0, 0);
                    i++;
                } while (i < boundaryoffset.NumberOfVertices && angles[i] < Math.PI);
                tempPolyLine.AddVertexAt(tempPolyLine.NumberOfVertices, segment.EndPoint.Convert2d(new Plane(Point3d.Origin, Vector3d.ZAxis)), 0, 0, 0);
                reinforcement.Add(tempPolyLine);
                i--;
            }
            // 如果钢筋数量大于等于2根，查看起点角度，如果角度小于3.14，则连接终点 - 起点钢筋
            if (reinforcement.Count > 2)
            {
                if (angles[0] < Math.PI)
                {
                    var count = reinforcement.ToArray().Count();
                    var startPolyLine = reinforcement.ToArray()[0];
                    var endPolyLine = reinforcement.ToArray()[count - 1];
                    endPolyLine.JoinEntity(startPolyLine);
                    reinforcement.RemoveAt(0);
                }
            }
            return reinforcement.ToArray();
        }
        /// <summary>
        /// 延伸所有钢筋到锚固长度
        /// </summary>
        /// <param name="subPolylines"></param>
        /// <param name="boundary"></param>
        /// <param name="AnchorageLength"></param>
        /// <param name="HookLength"></param>
        /// <param name="ProtectionThickness"></param>    
        public static Polyline[] GetSubReinforcementWithAnchors(this Polyline[] subReinforcements)
        {
            Polyline[] subReinforcementWithAnchor = new Polyline[subReinforcements.Count()];
            List<Dictionary<int, bool>> listDic = new List<Dictionary<int, bool>>();
            int i = 0;
            foreach (var subPolyline in subReinforcements)
            {
                var temp = ExtendSingleReinforcement(subPolyline, out Dictionary<int, bool> dic);
                subReinforcementWithAnchor[i] = temp;
                listDic.Add(dic);
                i++;
            }
            IsReinforcementBending = listDic;
            return subReinforcementWithAnchor;
        }
        public static Polyline[] Addhook(this Polyline[] subReinforcementWithAnchors)
        {
            var list = new List<Polyline>();
            var dics = IsReinforcementBending.ToArray();
            int i = 0;
            foreach (var subPolyline in subReinforcementWithAnchors)
            {
                var subPolylineN = subPolyline.Clone() as Polyline;
                //  1 的到钢筋的起止线段
                var endSeg = subPolylineN.GetLineSegmentAt(subPolylineN.NumberOfVertices - 2);
                var startSeg = subPolylineN.GetLineSegmentAt(0);
                //  1.1 开始方向反向延伸
                startSeg = new LineSegment3d(startSeg.EndPoint, startSeg.StartPoint);
                if (dics[i][1])
                {
                    var pointa = AddAnchorToReinforcementIsReverse(startSeg, true);
                    subPolylineN.AddVertexAt(0, pointa.Point3dTo2d(), 0, 0, 0);
                }
                else
                {
                    var pointa = AddAnchorToReinforcement(startSeg, true);
                    subPolylineN.AddVertexAt(0, pointa.Point3dTo2d(), 0, 0, 0);
                }
                if (dics[i][2])
                {
                    var pointb = AddAnchorToReinforcementIsReverse(endSeg, false);
                    subPolylineN.AddVertexAt(subPolylineN.NumberOfVertices, pointb.Point3dTo2d(), 0, 0, 0);
                }
                else
                {
                    var pointb = AddAnchorToReinforcement(endSeg, false);
                    subPolylineN.AddVertexAt(subPolylineN.NumberOfVertices, pointb.Point3dTo2d(), 0, 0, 0);
                }
                list.Add(subPolylineN);
                i++;
            }
            return list.ToArray();
        }
        public static Point3d[] AddDotRein(this Polyline boundary, double separation, double dtStartDistance, double d)
        {
            var list = new List<Point3d>();
            var boundaryoffset = boundary.GetOffsetCurves(-d)[0] as Polyline;
            Line[] lines = boundaryoffset.PolyToLines();
            foreach (var line in lines)
            {
                var points = line.GetLineSeparatPoint(separation, dtStartDistance);
                list = list.Union(points).ToList();
            }
            return list.ToArray();
        }
        public static Point3d[] AddReduceDotRein(this Polyline boundary, double separation, double d)
        {
            var list = new List<Point3d>();
            var boundaryoffset = boundary.GetOffsetCurves(-d)[0] as Polyline;
            Line[] lines = boundaryoffset.PolyToLines();
            foreach (var line in lines)
            {
                var points = line.GetLineReducePoints(separation);
                list = list.Union(points).ToList();
            }
            return list.ToArray();
        }
        public static MLeader[] AddMleaders(double separation, string content)
        {
            var list = new List<MLeader>();
            var lines = DotReinCenterPoly.PolyToLines();
            foreach (var line in lines)
            {
                var points = line.GetLineReducePoints(separation);
                var ml = GetMleaderByPoints(points, separation, content);
                list.Add(ml);
            }
            return list.ToArray();
        }
        #endregion
        #region 辅助方法1
        /// <summary>
        /// 一定条件下，连接两根钢筋为一根
        /// </summary>
        /// <param name="subPolys"></param>
        /// <returns></returns>
        public static Polyline[] ConnectReinByCondition(this Polyline[] subPolys)
        {
            var subPolysN = new List<Polyline>();
            int i = 0;
            //  1 从第一条Poly开始比对，先取第一条，然后依次取到最后一条
            while (i < subPolys.Length)
            {
                //  2.1 得到第一条Poly的最后一条线段，最为开始线段
                var segStart = subPolys[i].GetLineSegment2dAt(subPolys[i].NumberOfVertices - 2);
                //  2.2 得到开始线段的终点，作为比对点
                var pointS = segStart.EndPoint;
                var j = i + 1;
                //  3 分别比对剩余Poly
                while (j < subPolys.Length)
                {
                    //  3.1 得到比对Poly的第一条线段，最为结束线段
                    var segEnd = subPolys[j].GetLineSegment2dAt(0);
                    //  3.2 结束线段的起点，作为比对点
                    var PointE = segEnd.StartPoint;
                    //  4 判断两条线段是否平行
                    var b = segStart.IsParallelTo(segEnd);
                    if (b)
                    {
                        //  5 得到连接钢筋线段的几何性质，作为判断是否连接的条件
                        //  5.1 得到平行钢筋的垂直间距
                        var distanceV = segStart.ParallelLineDistance(segEnd);
                        //  5.2 得到连接钢筋的起点到终点距离
                        var distance = pointS.GetDistanceTo(PointE);
                        //  5.3 得到平行钢筋的水平间距
                        var distanceH = Math.Sqrt(Math.Pow(distance, 2) - Math.Pow(distanceV, 2));
                        double rate = 0;
                        if (distanceV > BaseConfig.ToleranceDouble)
                        {
                            rate = distanceV / distanceH;
                        }
                        //  6 如果钢筋  垂直距离/水平距离<1/6 && 连接钢筋的起点到终点距离<用户输入长度
                        //钢筋连接
                        if (rate < 1.0 / 6 && distance < AnchorageJoinLength)
                        {
                            //  6.1 添加 被连接钢筋的起点
                            subPolys[i].AddVertexAt(subPolys[i].NumberOfVertices, PointE, 0, 0, 0);
                            //  6.2 连接两根钢筋
                            subPolys[i].JoinEntity(subPolys[j]);
                            //  7 判断两根钢筋连接后是否变为封闭图形
                            //判断条件为    连接钢筋的起点==被连接钢筋的终点
                            if (subPolys[i].GetPoint2dAt(0).IsEqualTo(subPolys[j].GetPoint2dAt(subPolys[j].NumberOfVertices - 1), BaseConfig.TolerancePoint))
                            {
                                //  7.1 封闭图形，无法进行钢筋延伸操作，图形改为不封闭；
                                subPolys[i].Closed = false;
                                //  7.2 改为不封闭图形后还需添加 连接钢筋的起点（如不添加，导致钢筋长度缩短）
                                subPolys[i].AddVertexAt(subPolys[i].NumberOfVertices, segStart.StartPoint, 0, 0, 0);
                            }
                            //  8.1 在钢筋集合中 移除被连接的钢筋，（如果不删除，迭代变量会出错）
                            subPolys = subPolys.Where(x => x != subPolys[j]).ToArray();
                            //  8.2连接钢筋只能添加一条被连接钢筋，添加一条连接钢筋后 结束循环
                            break;
                        }
                    }
                    j++;
                }
                subPolysN.Add(subPolys[i]);
                i++;
            }
            return subPolys;
        }
        #endregion
        #region 辅助方法2
        /// <summary>
        /// 获取钢筋同轮廓的正向交点 （钢筋必须在轮廓内，和轮廓有交点，该方法出错）
        /// </summary>
        /// <param name="seg"></param>
        /// <param name="boundary"></param>
        /// <returns></returns>
        public static Polyline ExtendSingleReinforcement(this Polyline subPolyline, out Dictionary<int, bool> dic)
        {
            try
            {
                //  1 的到钢筋的起止线段               
                var subPolylineN = subPolyline.Clone() as Polyline;
                var endSeg = subPolyline.GetLineSegmentAt(subPolyline.NumberOfVertices - 2);
                var startSeg = subPolyline.GetLineSegmentAt(0);
                //  1.1 开始方向反向延伸
                startSeg = new LineSegment3d(startSeg.EndPoint, startSeg.StartPoint);
                //  1.2  得到钢筋延伸方向
                var startDirection = GetDirectionTwoPointInOneLine(subPolyline);
                //  2 延伸起点 终点钢筋
                var extendPointsStart = ExtendEndingReinforcement(startSeg, startDirection, out bool isReinforcementBendingStart);
                var extendPointsEnd = ExtendEndingReinforcement(endSeg, -startDirection, out bool isReinforcementBendingEnd);
                // 3 传递钢筋是否弯折了属性
                Dictionary<int, bool> dictionary = new Dictionary<int, bool>();
                dictionary.Add(1, isReinforcementBendingStart);
                dictionary.Add(2, isReinforcementBendingEnd);
                dic = dictionary;
                //  3 添加延伸点 到钢筋上
                foreach (var point in extendPointsStart)
                {
                    subPolylineN.AddVertexAt(0, point, 0, 0, 0);
                }
                foreach (var point in extendPointsEnd)
                {
                    subPolylineN.AddVertexAt(subPolylineN.NumberOfVertices, point, 0, 0, 0);
                }
                return subPolylineN;
            }
            catch (System.Exception ex)
            {
                var ed = Application.DocumentManager.MdiActiveDocument.Editor;
                ed.WriteMessage($"\n{ex.ToString()}");
                throw;
            }
        }
        public static Point3d GetIntersectionByLinetWithBoundary(this LineSegment3d seg, Polyline boundary)
        {
            var pt1 = seg.EndPoint;
            var pt2 = seg.StartPoint;
            Vector3d vectorSource = pt1 - pt2;
            var endLine = new Line(pt2, pt1);
            //  1 获取交点集合
            var points = new Point3dCollection();
            endLine.IntersectWith(boundary, Intersect.ExtendThis, points, IntPtr.Zero, IntPtr.Zero);
            //  2 获取沿PolyLine方向的所有交点
            List<Point3d> PositiveDirectionpoints = new List<Point3d>();
            int i = 0;
            foreach (var point in points)
            {
                Vector3d vector3D = points[i] - pt1;
                // 内部点判断
                if (vectorSource.GetNormal() == vector3D.GetNormal())
                {
                    PositiveDirectionpoints.Add(points[i]);
                }
                i++;
            }
            if (!PositiveDirectionpoints.Any())
            {
                return points[0];
            }
            //  3 获取沿PolyLine方向的所有交点 中最近的交点
            int minIndex = 0; int j = 0; double min = double.MaxValue;
            foreach (var point in PositiveDirectionpoints)
            {
                var d = pt1.DistanceTo(point);
                if (min > d)
                {
                    min = d;
                    minIndex = j;
                }
                j++;
            }
            var ptMin = PositiveDirectionpoints[minIndex];
            return ptMin;
        }
        //public static Point3d GetIntersectionByLinetWithBoundary(this LineSegment3d seg, Polyline boundary)
        //{
        //    var pt1 = seg.EndPoint;
        //    var pt2 = seg.StartPoint;
        //    Vector3d vectorSource = pt1 - pt2;
        //    var endLine = new Line(pt2, pt1);
        //    // 1. 获取交点集合
        //    var points = new Point3dCollection();
        //    endLine.IntersectWith(boundary, Intersect.ExtendThis, points, IntPtr.Zero, IntPtr.Zero);
        //    // 2. 获取沿 PolyLine 方向的所有交点
        //    var positiveDirectionPoints = points.Cast<Point3d>()
        //                                         .Where(point => (point - pt1).GetNormal() == vectorSource.GetNormal())
        //                                         .ToList();
        //    // 检查是否有正方向交点
        //    if (!positiveDirectionPoints.Any())
        //    {
        //        throw new InvalidOperationException("No intersection points found in the positive direction.");
        //    }
        //    // 3. 获取沿 PolyLine 方向的所有交点中最近的交点
        //    var closestPoint = positiveDirectionPoints
        //                       .OrderBy(point => pt1.DistanceTo(point))
        //                       .First();
        //    return closestPoint;
        //}
        /// <summary>
        /// 延伸钢筋末端到锚固长度
        /// </summary>      
        /// <param name="seg">钢筋末端</param>
        /// <param name="isReinforcementBending">两个钢筋端头在一个边界线时，钢筋弯折方向</param>
        /// <returns>延伸锚固长度的钢筋端点坐标</returns>
        public static List<Point2d> ExtendEndingReinforcement(this LineSegment3d seg, Vector3d?
            direction, out bool isReinforcementBending)
        {
            try
            {
                isReinforcementBending = false;
                List<Point2d> extendPoints = new List<Point2d>();
                //  1.1 获取钢筋的方向向量
                Vector3d direction01 = (seg.EndPoint - seg.StartPoint).GetNormal();
                //  1.2 得到无弯折锚固，钢筋的锚固segment
                LineSegment3d baseAnchorLenth = new LineSegment3d(seg.EndPoint, seg.EndPoint + direction01.GetNormal() * AnchorageLength);
                //  2 得到钢筋延伸到轮廓的segment                
                LineSegment3d extendSeg01 = GetExtendSeg(seg.EndPoint, direction01, out Vector3d direction02);
                //  3 判断长度大小（钢筋延伸到轮廓-2*保护层厚度）<(钢筋直线锚固长度）如果小于，锚固钢筋需要弯折
                LineSegment3d enxtendSeg02 = new LineSegment3d();
                int i = 0;
                if (extendSeg01.Length < baseAnchorLenth.Length)
                {
                    //  4 第一次弯折处理
                    if (direction == null)
                    {
                        enxtendSeg02 = GetExtendSeg(extendSeg01.EndPoint, direction02, out Vector3d direction03);
                        direction = direction02;
                    }
                    else
                    {
                        enxtendSeg02 = GetExtendSeg(extendSeg01.EndPoint, (Vector3d)direction, out Vector3d direction03);
                        direction = (Vector3d)direction;
                    }
                    // （两段钢筋总长度 < 锚固长度）
                    var enxtendSeg02Backup = enxtendSeg02;
                    if ((enxtendSeg02.Length + extendSeg01.Length) < baseAnchorLenth.Length)
                    {
                        enxtendSeg02 = enxtendSeg02Backup;
                        //对不满足锚固长度的节点进行标记 i 计数，有多少个不满足锚固的结果
                        Tools.ZTools.MakeMark(enxtendSeg02.EndPoint, i.ToString(), 120, 100);
                        i++;
                    }
                    else
                    {
                        enxtendSeg02 = new LineSegment3d(extendSeg01.EndPoint, (Point3d)(enxtendSeg02.StartPoint + direction *
                          (AnchorageLength - extendSeg01.Length)));
                        //  4.1 判断锚固长度是否够了
                        // 判断钢筋平直段长度<最小钢筋平直段长度                     
                        if (enxtendSeg02.Length < BendingLineMinLength)
                        {
                            enxtendSeg02 = new LineSegment3d(enxtendSeg02.StartPoint, (Point3d)(enxtendSeg02.StartPoint + direction *
                                BendingLineMinLength));
                        }
                    }
                    //else
                    //{
                    //    enxtendSeg02 = new LineSegment3d(enxtendSeg02.StartPoint, (Point3d)(enxtendSeg02.StartPoint + direction *
                    //        (baseAnchorLenth.Length - extendSeg01.Length)));
                    //}
                    //  6 返回结果
                    extendPoints.Add(enxtendSeg02.StartPoint.Point3dTo2d());
                    extendPoints.Add(enxtendSeg02.EndPoint.Point3dTo2d());
                    isReinforcementBending = true;
                    return (extendPoints);
                }
                else
                {
                    //  7 直接返回结果 无弯折锚固，钢筋的锚固直线的终点
                    extendPoints.Add(baseAnchorLenth.EndPoint.Point3dTo2d());
                    return (extendPoints);
                }
            }
            catch (System.Exception ex)
            {
                var ed = Application.DocumentManager.MdiActiveDocument.Editor;
                ed.WriteMessage($"\n{ex.ToString()}");
                throw;
            }
        }
        /// <summary>
        /// 得到 一根钢筋两个端点在同一条边界线上，起点 钢筋弯折的方向，终点可以反向
        /// </summary>
        /// <param name="subPoly"></param>
        /// <returns></returns>
        public static Vector3d? GetDirectionTwoPointInOneLine(this Polyline subPoly)
        {
            var direction = new Vector3d?();
            //  1 的到钢筋的起止线段
            var endSeg = subPoly.GetLineSegmentAt(subPoly.NumberOfVertices - 2);
            var startSeg = subPoly.GetLineSegmentAt(0);
            //  1.1 开始方向反向延伸
            startSeg = new LineSegment3d(startSeg.EndPoint, startSeg.StartPoint);
            //  2 得到交点
            var boundStartPoint = GetIntersectionByLinetWithBoundary(startSeg, Boundary);
            var boundEndPoint = GetIntersectionByLinetWithBoundary(endSeg, Boundary);
            //  3 得到交点所在线段
            var segStart = GetSegmentInPolyLineByPoint(Boundary, boundStartPoint, out Vector3d directionStart);
            var segEnd = GetSegmentInPolyLineByPoint(Boundary, boundEndPoint, out Vector3d directionEnd);
            //  4 判断 交点是否在一条直线上，如果在修改锚固长度方向（方向改为 两交点连线相反方向）
            if (segEnd.StartPoint == segStart.StartPoint && segEnd.EndPoint == segStart.EndPoint)
            {
                direction = (boundStartPoint - boundEndPoint).GetNormal();
            }
            else
            {
                direction = null;
            }
            return direction;
        }
        /// <summary>
        /// 得到（沿钢筋到轮廓方向）（钢筋终点）至（钢筋到混凝土轮廓交点-2*保护层厚度点）的直线Segment
        /// </summary>
        /// <param name="seg"></param>
        /// <returns></returns>
        public static LineSegment3d GetExtendSeg(Point3d basePoint, Vector3d Priordirection, out Vector3d directionNext)
        {
            //1 得到线段和轮廓交点
            var seg = new LineSegment3d(basePoint, basePoint + Priordirection * 1);
            var boundaryPoint = GetIntersectionByLinetWithBoundary(seg, Boundary);
            //2 新建延长线 通过（原线段终点）（轮廓交点）
            LineSegment3d boundBaseAnchorLine = new LineSegment3d(seg.EndPoint, boundaryPoint);
            //2.2 修正延长线长度（减小2倍保护层厚度）
            var length = boundBaseAnchorLine.Length - 2 * ProtectionThickness;
            //3 新建修正长度后的延长线
            LineSegment3d shortBoundBaseAnchorLine01 = new LineSegment3d(boundBaseAnchorLine.StartPoint,
                boundBaseAnchorLine.StartPoint + Priordirection * length);
            //4 取得下一段线段的延伸方向
            var boundarySegment = GetSegmentInPolyLineByPoint(Boundary, boundaryPoint, out Vector3d directionBend);
            directionNext = directionBend;
            return shortBoundBaseAnchorLine01;
        }
        /// <summary>
        /// 寻找钢筋延伸点，同轮廓交点，所在的轮廓上的segment，附加输出一个方向（segment 被点截断，点到较长端点的方向）
        /// </summary>
        /// <param name="polyLine"></param>
        /// <param name="point3D">钢筋延伸同轮廓相交点</param>
        /// <returns></returns>
        public static LineSegment3d GetSegmentInPolyLineByPoint(Polyline polyLine, Point3d point3D, out Vector3d direction)
        {
            try
            {
                double ParameterResult = polyLine.GetParameterAtPoint(point3D);
                int idxPt1 = Convert.ToInt32(Math.Floor(ParameterResult));
                int idxPt2 = idxPt1 + 1;
                Point3d pt1 = polyLine.GetPoint3dAt(idxPt1);
                var n = polyLine.NumberOfVertices;
                //如果为闭合PolyLine，当到达EndSegment，idxPt2 = 0 也就是起点（没有下一个点了，会出错）
                if (idxPt1 == polyLine.NumberOfVertices - 1)
                {
                    idxPt2 = 0;
                }
                Point3d pt2 = polyLine.GetPoint3dAt(idxPt2);
                LineSegment3d seg = new LineSegment3d(pt1, pt2);
                if (ParameterResult - idxPt1 < 0.5)
                {
                    direction = (pt2 - pt1).GetNormal();
                }
                else
                {
                    direction = (pt1 - pt2).GetNormal();
                }
                return seg;
            }
            catch (System.Exception ex)
            {
                var ed = Application.DocumentManager.MdiActiveDocument.Editor;
                ed.WriteMessage($"\n{ex.ToString()}");
                throw;
            }
        }
        #endregion
        #region 辅助方法3
        /// <summary>
        /// 添加钢筋弯钩
        /// </summary>
        /// <param name="seg">需要添加弯钩的线段</param>
        /// <param name="d">弯钩长度</param>
        /// <param name="angle">弯钩角度(以45度弯钩为例)开始为pi*5/3 ,结束为pi*3/4</param>
        /// <returns></returns>
        public static Point3d AddAnchorToReinforcement(this LineSegment3d seg, bool IsStartPoint)
        {
            Vector3d segDirection = seg.EndPoint - seg.StartPoint;
            if (IsStartPoint)
            {
                var n = segDirection.RotateBy(Math.PI * 5 / 4, Vector3d.ZAxis).GetNormal();
                Point3d pt1 = seg.EndPoint + n * HookLength;
                //EntityTool.MakeMark(seg.EndPoint, "a", 100, 80);
                return pt1;
            }
            else
            {
                var n = segDirection.RotateBy(Math.PI * 3 / 4, Vector3d.ZAxis).GetNormal();
                Point3d pt1 = seg.EndPoint + n * HookLength;
                return pt1;
            }
        }
        /// <summary>
        /// 钢筋弯折后，判断弯钩方向，应当指向混凝土内侧（如果指向混凝土外侧，方向翻转）
        /// </summary>
        /// <param name="seg"></param>
        /// <param name="d"></param>
        /// <param name="IsStartPoint"></param>
        /// <param name="boundary"></param>
        /// <returns></returns>
        public static Point3d AddAnchorToReinforcementIsReverse(this LineSegment3d seg, bool IsStartPoint)
        {
            Vector3d segDirection = seg.EndPoint - seg.StartPoint;
            Point3d point = new Point3d();
            LineSegment3d anchorSeg = new LineSegment3d();
            if (IsStartPoint)
            {
                var n = segDirection.RotateBy(Math.PI * 3 / 4, Vector3d.ZAxis).GetNormal();
                anchorSeg = new LineSegment3d(seg.StartPoint, seg.StartPoint + n * HookLength);
                var anchorSegReverse = new LineSegment3d(anchorSeg.EndPoint, anchorSeg.StartPoint);
                //var boundary = (Polyline)Boundary.GetOffsetCurves(seg.Length)[0];
                var pointa = GetIntersectionByLinetWithBoundary(anchorSeg, Boundary);
                var pointb = GetIntersectionByLinetWithBoundary(anchorSegReverse, Boundary);
                if (seg.StartPoint.DistanceTo(pointa) > seg.StartPoint.DistanceTo(pointb))
                {
                    return point = seg.EndPoint + n * HookLength;
                }
                else
                {
                    var nReverse = segDirection.RotateBy(Math.PI * 5 / 4, Vector3d.ZAxis).GetNormal();
                    point = seg.EndPoint + nReverse * HookLength;
                    return point;
                }
            }
            else
            {
                var n = segDirection.RotateBy(Math.PI * 3 / 4, Vector3d.ZAxis).GetNormal();
                anchorSeg = new LineSegment3d(seg.EndPoint, seg.EndPoint + n * HookLength);
                var anchorSegReverse = new LineSegment3d(anchorSeg.EndPoint, anchorSeg.StartPoint);
                var pointa = GetIntersectionByLinetWithBoundary(anchorSeg, Boundary);
                var pointb = GetIntersectionByLinetWithBoundary(anchorSegReverse, Boundary);
                if (seg.EndPoint.DistanceTo(pointa) > seg.EndPoint.DistanceTo(pointb))
                {
                    return point = seg.EndPoint + n * HookLength;
                }
                else
                {
                    var nReverse = segDirection.RotateBy(Math.PI * 5 / 4, Vector3d.ZAxis).GetNormal();
                    point = seg.EndPoint + nReverse * HookLength;
                    return point;
                }
            }
        }
        #endregion
        #region 辅助方法4
        public static Line[] PolyToLines(this Polyline poly)
        {
            var lines = new List<Line>();
            for (int i = 0; i < poly.NumberOfVertices - 1; i++)
            {
                var line = new Line(poly.GetPoint3dAt(i), poly.GetPoint3dAt(i + 1));
                lines.Add(line);
            }
            if (poly.Closed == true)
            {
                lines.Add(new Line(poly.GetPoint3dAt(poly.NumberOfVertices - 1), poly.GetPoint3dAt(0)));
            }
            return lines.ToArray();
        }
        public static Polyline GetDotReinCenterPoly(this Polyline boundary, double d)
        {
            var list = new List<Point3d>();
            var boundaryoffset = boundary.GetOffsetCurves(-d)[0] as Polyline;
            return boundaryoffset;
        }
        public static Point3d[] GetLineSeparatPoint(this Line line, double separation, double dtStartDistance)
        {
            var ds = GetRangeNumbersUseSEPoint(line.Length, separation, dtStartDistance);
            var point3d = new List<Point3d>();
            foreach (var d in ds)
            {
                var p = line.GetPointAtParameter(d * line.Length);
                point3d.Add(p);
            }
            return point3d.ToArray();
        }
        public static double[] GetRangeNumbersUseSEPoint(double length, double separation, double dtStartDistance)
        {
            var lengthIn = length - dtStartDistance * 2;
            var scale = lengthIn / length;
            var d1 = dtStartDistance / length;
            var list = new List<double>();
            var listIn = GetRangeNumbersByLengthSeparatin(lengthIn, separation);
            if (lengthIn < separation)
            {
                list.Add(0);
                list.Add(1);
                return list.ToArray();
            }
            else
            {
                list.Add(0);
                double tempD = d1;
                foreach (var d in listIn)
                {
                    tempD = d * scale + d1;
                    list.Add(tempD);
                }
                list.Add(1);
                return list.ToArray();
            }
        }
        public static double[] GetRangeNumbersByLengthSeparatin(double length, double separation)
        {
            var start = 0;
            var end = 1;
            separation = separation / length;
            length = 1;
            var divNumberDouble = ((double)end - (double)start) / ((double)separation);
            var divNumber = (int)divNumberDouble;
            var remainder = length - separation * divNumber;
            var outNumbers = new List<double>();
            if (remainder < 1e-6)
            {
                outNumbers.Add(start);
                double temp = start;
                for (int i = 1; i < divNumber; i++)
                {
                    temp = temp + (double)separation;
                    outNumbers.Add(temp);
                }
                outNumbers.Add(1);
                return outNumbers.ToArray();
            }
            else
            {
                var d = (separation - (length - divNumber * separation)) / 2;
                outNumbers.Add(start);
                double temp = separation - d;
                outNumbers.Add(temp);
                for (int i = 2; i < divNumber; i++)
                {
                    temp = temp + (double)separation;
                    outNumbers.Add(temp);
                }
                outNumbers.Add((end - separation + d));
                outNumbers.Add(1);
                return outNumbers.ToArray();
            }
        }
        public static Point3d[] GetLineReducePoints(this Line line, double separation)
        {
            var seg = new LineSegment3d(line.StartPoint, line.EndPoint);
            var point = seg.MidPoint;
            Point3d pa = new Point3d();
            Point3d pm = new Point3d();
            Point3d pb = new Point3d();
            var listPoints = new List<Point3d>();
            if (seg.Length <= separation)
            {
                pa = seg.StartPoint;
                pb = seg.EndPoint;
                listPoints.Add(pa);
                listPoints.Add(pb);
            }
            else if (seg.Length > separation && seg.Length <= separation * 2)
            {
                pa = seg.StartPoint;
                pb = seg.EndPoint;
                pm = seg.MidPoint;
                listPoints.Add(pa);
                listPoints.Add(pm);
                listPoints.Add(pb);
            }
            else
            {
                pa = point - line.Delta.GetNormal() * separation / 2;
                pb = point + line.Delta.GetNormal() * separation / 2;
                listPoints.Add(pa);
                listPoints.Add(pb);
            }
            return listPoints.ToArray();
        }
        public static Polyline[] PointsToDotRein(this IEnumerable<Point3d> points)
        {
            var list = new List<Polyline>();
            foreach (var point in points)
            {
                var poly = Tools.ZTools.CreateSolidCircle(ReinforcementDiameter, point);
                list.Add(poly);
            }
            return list.ToArray();
        }
        #endregion
        #region 辅助方法5
        public static MLeader GetMleaderByPoints(Point3d[] points, double separation, string content)
        {
            var seg = new LineSegment3d(points.First(), points.Last());
            //外侧标注，方向反向后内侧标注
            var vec = points.First() - points.Last();
            vec = vec.RotateBy(Math.PI / 2, Vector3d.ZAxis);
            var mPoint = seg.MidPoint;
            var ePoint = mPoint + vec.GetNormal() * separation;
            var ml = Tools.ZTools.AddMleader(points, Reinforcement.MleaderDistance, content);
            return ml;
        }
        #endregion
    }
    #region 辅助类型
    /// <summary>
    /// 判断封闭图示PolyLine 是顺指针，还是逆时针
    /// </summary>
    public static class AlgebraicArea
    {
        public static double GetArea(Point2d pt1, Point2d pt2, Point2d pt3)
        {
            return (((pt2.X - pt1.X) * (pt3.Y - pt1.Y)) -
                        ((pt3.X - pt1.X) * (pt2.Y - pt1.Y))) / 2.0;
        }
        public static double GetArea(this CircularArc2d arc)
        {
            double rad = arc.Radius;
            double ang = arc.IsClockWise ? arc.StartAngle - arc.EndAngle : arc.EndAngle - arc.StartAngle;
            return rad * rad * (ang - Math.Sin(ang)) / 2.0;
        }
        public static double GetArea(this Polyline pline)
        {
            CircularArc2d arc = new CircularArc2d();
            double area = 0.0;
            int last = pline.NumberOfVertices - 1;
            Point2d p0 = pline.GetPoint2dAt(0);
            if (pline.GetBulgeAt(0) != 0.0)
            {
                area += pline.GetArcSegment2dAt(0).GetArea();
            }
            for (int i = 1; i < last; i++)
            {
                area += GetArea(p0, pline.GetPoint2dAt(i), pline.GetPoint2dAt(i + 1));
                if (pline.GetBulgeAt(i) != 0.0)
                {
                    area += pline.GetArcSegment2dAt(i).GetArea(); ;
                }
            }
            if ((pline.GetBulgeAt(last) != 0.0) && pline.Closed)
            {
                area += pline.GetArcSegment2dAt(last).GetArea();
            }
            return area;
        }
    }
    #endregion
}