using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Windows.ToolPalette;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
namespace HyCADTool
{
    public static partial class DimensionForReinforcement
    {
        ////*************
        //Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;//临时
        //Database db = Application.DocumentManager.MdiActiveDocument.Database;//临时
        //var doc = Application.DocumentManager.MdiActiveDocument;//临时
        //var a = EtGpt.CreateLayer("new1", 1);//临时
        //var b = EtGpt.CreateLayer("new2", 2);//临时
        //boundLineNext.ChangeEntitiesProperty(x => x.LayerId = a);//临时
        //boundLineNext.ToSpace();//临时
        ////**************
        #region 属性
        public static Polyline Boundary { get; set; }
        public static double Step { get; set; }
        public static double ToleranceDouble { get; set; }
        public static double DimensionDistanceOutside { get; set; }
        //public static double DimensionDistance { get; set; }
        public static double DimensionDistanceInside { get; set; }
        public static double DimensionDistanceWithDim { get; set; }
        public static bool IsHasTwoDimOutside { get; set; }
        public static double Scale { get; set; }
        public static Point3d XMin { get; set; }
        public static Point3d XMax { get; set; }
        public static Point3d YMin { get; set; }
        public static Point3d YMax { get; set; }
        public static double StartStep { get; set; }
        public static Line[] SecantLineUpDownS { get; set; }
        public static Line[] SecantLineLeftRightS { get; set; }
        public static Line[,] LineIntersectionsListUpDownSS { get; set; }
        public static Line[] LineIntersectionsListUpDownS { get; set; }
        public static Line[,] LineIntersectionsListLeftRightSS { get; set; }
        public static Line[] LineIntersectionsListLeftRightS { get; set; }
        public static RotatedDimension[] DimLeft { get; set; }
        public static RotatedDimension[] DimRight { get; set; }
        public static RotatedDimension[] DimUp { get; set; }
        public static RotatedDimension[] DimDown { get; set; }
        public static RotatedDimension[] DimInLR { get; set; }
        public static RotatedDimension[] DimInUD { get; set; }
        public static Point3d[] PointsLeft { get; set; }
        public static Point3d[] PointsRight { get; set; }
        public static Point3d[] PointsUp { get; set; }
        public static Point3d[] PointsDown { get; set; }
        #endregion
        #region Enum
        public enum MaxMinPoint
        {
            xmin,
            xmax,
            ymin,
            ymax
        }
        public enum SecantLineType
        {
            UpDown,
            LeftRight,
            Both
        }
        public enum DimensionFor
        {
            ForLeft,
            ForRight,
            ForUp,
            ForDown,
        }
        #endregion
        #region 设置生成
        public static bool SetProperties(Polyline boundary)
        {
            Tools.ZTools.SetCurrentLayer("00_hy_3公共_标注1_外");
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            // 1 选择边界            
            if (boundary == null) return false;
            Boundary = boundary;
            // 2 设置常量
            //外轮廓同标注间距
            DimensionDistanceOutside = Reinforcement.DimensionDistanceOutside;
            //平行，且数值相同的标注，距离小于 DimDistanceTolerance的时候删除
            ToleranceDouble = Reinforcement.ToleranceDouble;
            //外轮廓是否双侧标注
            IsHasTwoDimOutside = true;
            // 标注同标注的间距
            DimensionDistanceWithDim = Reinforcement.DimensionDistanceWithDim;
            //内标注同轮廓距离
            DimensionDistanceInside = Reinforcement.DimensionDistanceInside;
            //一般标注（非内外标注）同轮廓间距
            // DimensionDistance = Reinforcement.DimensionDistance;
            //割线间距
            Step = 20;
            //起始割线间距
            StartStep = 10;
            //Scale = Reinforcement.Scale;
            // 3 赋值轮廓标记属性
            #region 给静态属性赋值
            var lines = DimensionForReinforcement.ExplodPoly(boundary);
            var points = lines.GetAllLinePoints();
            //points.Cast<Point3d>().OrderBy(Point3d => Point3d.X ).ToArray();
            XMin = points.GetMaxMinPoint(RecT.MaxMinPoint.xmin);
            XMax = points.GetMaxMinPoint(RecT.MaxMinPoint.xmax);
            YMin = points.GetMaxMinPoint(RecT.MaxMinPoint.ymin);
            YMax = points.GetMaxMinPoint(RecT.MaxMinPoint.ymax);
            // 4 赋值标注属性值
            var dimsLR = GenerateDimensionLeftRightOutside();
            var dimsUD = GenerateDimensionUpDownOutside();
            DimInLR = GetLeftRightDimInside();
            DimInUD = GetUpDownDimInside();
            DimInLR = DimInLR.DeleteNearbyParallelDim(Reinforcement.DimDistanceTolerance);
            DimInUD = DimInUD.DeleteNearbyParallelDim(Reinforcement.DimDistanceTolerance);
            DimLeft = dimsLR[0].DeleteDimensionZero();
            DimLeft = DimLeft.DimVsEqualLength();
            DimRight = dimsLR[1].DeleteDimensionZero();
            DimRight = DimRight.DimVsEqualLength();
            DimUp = dimsUD[0].DeleteDimensionZero();
            DimUp = DimUp.DimVsEqualLength();
            DimDown = dimsUD[1].DeleteDimensionZero();
            DimDown = DimDown.DimVsEqualLength();
            #endregion
            return true;
        }
        public static void GenerateDimension(Polyline boundary)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            using (var docLock = doc.LockDocument())
            {
                var b = SetProperties(boundary);
                if (b == false) return;
                // 得到固定列数的交线 通过交线二维数组
                //var col = LineIntersectionsListUpDownSS.GetLength(1);
                //for (int i = 0; i < col; i++)
                //{
                //    var lineIntersectionByColUpDown1 = LineIntersectionsListUpDownSS.GetDimensionByCol(i);
                //    lineIntersectionByColUpDown1.ChangeEntitiesProperty(x => x.ColorIndex = i);
                //    lineIntersectionByColUpDown1.ToSpace();
                //}
                //var lineIntersectionByColLeftRight = LineIntersectionsListUpDownSS.GetDimensionByCol(0);
                //lineIntersectionByColLeftRight.ChangeEntitiesProperty(x => x.ColorIndex = 1);
                //lineIntersectionByColLeftRight.ToSpace();
                Tools.ZTools.SetCurrentLayer("00_hy_3公共_标注1_外");
                DimLeft.ToSpace();
                DimUp.ToSpace();
                DimDown.ToSpace();
                DimRight.ToSpace();
                DimInLR.ToSpace();
                DimInUD.ToSpace();
            }
        }
        #endregion
        #region 主要流程          
        public static RotatedDimension[] GetLeftRightDimInside()
        {
            var cols = LineIntersectionsListUpDownSS.GetLength(1);
            var boundLinePri = LineIntersectionsListUpDownSS.GetDimensionByCol(0);
            var lDPriR = boundLinePri.GetBoundXYFromEntities(RecT.MaxMinPoint.xmax);
            var dimIns = new List<RotatedDimension[]>();
            var dimsMid = new List<RotatedDimension>();
            var dimsNext = new List<RotatedDimension>();
            var dimsPri = new List<RotatedDimension>();
            for (int i = 1; i < cols - 1; i++)
            {
                //左侧开始
                var boundLineMid = LineIntersectionsListUpDownSS.GetDimensionByCol(i);
                var lDMidL = boundLineMid.GetBoundXYFromEntities(RecT.MaxMinPoint.xmin);
                var lDMidR = boundLineMid.GetBoundXYFromEntities(RecT.MaxMinPoint.xmax);
                var boundLineNext = LineIntersectionsListUpDownSS.GetDimensionByCol(i + 1);
                var lDNextL = boundLineNext.GetBoundXYFromEntities(RecT.MaxMinPoint.xmin);
                if (lDMidL - lDPriR > lDNextL - lDMidR)
                {
                    dimsMid = boundLineMid.GetDimsByLines(DimensionDistanceInside, ZTools.DimensionFor.ForLeft, true).ToList();
                    dimsPri = boundLinePri.GetDimsByLines(DimensionDistanceInside, ZTools.DimensionFor.ForLeft, true).ToList();
                    // dimsMid = CompareDim(dimsMid, dimsPri);
                    dimIns.Add(dimsMid.ToArray());
                    lDPriR = boundLineMid.GetBoundXYFromEntities(RecT.MaxMinPoint.xmax);
                    dimsPri = dimsMid;
                    
                }
                else
                {
                    dimsMid = boundLineMid.GetDimsByLines(DimensionDistanceInside, ZTools.DimensionFor.ForRight, true).ToList();
                    dimsPri = boundLinePri.GetDimsByLines(DimensionDistanceInside, ZTools.DimensionFor.ForRight, true).ToList();
                    //dimsMid = CompareDim(dimsMid, dimsPri);
                    dimIns.Add(dimsMid.ToArray());
                    lDPriR = boundLineMid.GetBoundXYFromEntities(RecT.MaxMinPoint.xmax);
                    dimsPri = dimsMid;
                }
            }
            var dimInsLR = dimIns.Flatten();
            dimInsLR = dimInsLR.DeleteSamePointDim();
            dimInsLR = dimInsLR.DeleteDimensionZero();
            dimInsLR = dimInsLR.GroupBy(x => x, new DimEqualityComparer())
                .Select(x => x.Key);
            return dimInsLR.ToArray();
        }
        public static RotatedDimension[] GetUpDownDimInside()
        {
            var cols = LineIntersectionsListLeftRightSS.GetLength(1);
            var boundLinePri = LineIntersectionsListLeftRightSS.GetDimensionByCol(0);
            var lDPriD = boundLinePri.GetBoundXYFromEntities(RecT.MaxMinPoint.ymax);
            var dimIns = new List<RotatedDimension[]>();
            var dimsMid = new List<RotatedDimension>();
            var dimsNext = new List<RotatedDimension>();
            var dimsPri = new List<RotatedDimension>();
            for (int i = 1; i < cols - 1; i++)
            {
                //左侧开始
                var boundLineMid = LineIntersectionsListLeftRightSS.GetDimensionByCol(i);
                var lDMidD = boundLineMid.GetBoundXYFromEntities(RecT.MaxMinPoint.ymin);
                var lDMidU = boundLineMid.GetBoundXYFromEntities(RecT.MaxMinPoint.ymax);
                var boundLineNext = LineIntersectionsListLeftRightSS.GetDimensionByCol(i + 1);
                var lDNextD = boundLineNext.GetBoundXYFromEntities(RecT.MaxMinPoint.ymin);
                if (lDMidD - lDPriD > lDNextD - lDMidU)
                {
                    dimsMid = boundLineMid.GetDimsByLines(DimensionDistanceInside, ZTools.DimensionFor.ForDown, true).ToList();
                    dimsPri = boundLinePri.GetDimsByLines(DimensionDistanceInside, ZTools.DimensionFor.ForDown, true).ToList();
                    //dimsMid = CompareDim(dimsMid, dimsPri);
                    dimIns.Add(dimsMid.ToArray());
                    lDPriD = boundLineMid.GetBoundXYFromEntities(RecT.MaxMinPoint.xmax);
                    dimsPri = dimsMid;
                }
                else
                {
                    dimsMid = boundLineMid.GetDimsByLines(DimensionDistanceInside, ZTools.DimensionFor.ForUp, true).ToList();
                    dimsPri = boundLinePri.GetDimsByLines(DimensionDistanceInside, ZTools.DimensionFor.ForUp, true).ToList();
                    //dimsMid = CompareDim(dimsMid, dimsPri);
                    dimIns.Add(dimsMid.ToArray());
                    lDPriD = boundLineMid.GetBoundXYFromEntities(RecT.MaxMinPoint.xmax);
                    dimsPri = dimsMid;
                }
            }
            var dimInsUD = dimIns.Flatten();
            dimInsUD = dimInsUD.DeleteSamePointDim();
            dimInsUD = dimInsUD.DeleteDimensionZero();
            dimInsUD = dimInsUD.GroupBy(x => x, new DimEqualityComparer())
                .Select(x => x.Key);
            return dimInsUD.ToArray();
        }
        public static List<RotatedDimension> CompareDim(List<RotatedDimension> current, List<RotatedDimension> pri)
        {
            var list = current.Union(pri);
            var glist = list.GroupBy(x => x.Measurement)
                             .Select(g => g);
            var listN = new List<RotatedDimension>();
            foreach (var g in glist)
            {
                current.Remove(g.FirstOrDefault());
            }
            return current;
        }
        public static RotatedDimension[][] GenerateDimensionUpDownOutside()
        {
            // Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;//临时
            // Database db = Application.DocumentManager.MdiActiveDocument.Database;//临时
            //var doc = Application.DocumentManager.MdiActiveDocument;//临时
            // var a= EtGpt.CreateLayer("new1", 1);//临时
            //var b= EtGpt.CreateLayer("new2", 2);//临时
            //SecantLineLeftRightS.ChangeEntitiesProperty(x => x.LayerId = a);//临时
            //1 得到分割线           
            var lines = GetAllSecantLineLeftRight();
            SecantLineLeftRightS = lines;
            // SecantLineLeftRightS.ChangeEntitiesProperty(x => x.LayerId = a);//临时
            //SecantLineLeftRightS.ToSpace();//临时
            //2 得到交线 （得到交线行列[,]）           
            var cols = LineIntersectionsListLeftRightSS.GetLength(1);
            //3 得到标注轮廓线
            var boundDownLine = LineIntersectionsListLeftRightSS.GetDimensionLeftRight(0)
                .SortLineSEPointForDIm();
            // boundDownLine.ChangeEntitiesProperty(x => x.LayerId = a);//临时
            //boundDownLine.ToSpace();//临时
            var boundUpLine = LineIntersectionsListLeftRightSS.GetDimensionByCol(cols - 1)
                .SortLineSEPointForDIm();
            //boundUpLine.ChangeEntitiesProperty(x => x.LayerId = b);//临时
            //boundUpLine.ToSpace();//临时
            //  4得到 （标注点）（标注）
            var pointsDown = boundDownLine.GetDimensionPoints(DimensionFor.ForDown);
            PointsDown = pointsDown;
            var dimsDown = pointsDown.CreatDimensionFormPointsOutside(DimensionFor.ForDown);
            var pointsUp = boundUpLine.GetDimensionPoints(DimensionFor.ForUp);
            PointsUp = pointsUp;
            var dimsUp = pointsUp.CreatDimensionFormPointsOutside(DimensionFor.ForUp);
            var dims = new RotatedDimension[][] { dimsDown, dimsUp };
            return dims;
        }
        public static RotatedDimension[][] GenerateDimensionLeftRightOutside()
        {
            //1 得到分割线
            var lines = GetAllSecantLineX();
            SecantLineUpDownS = lines;
            //2 得到交线 （得到交线行列[,]）           
            var cols = LineIntersectionsListUpDownSS.GetLength(1);
            // 3 得到外部交线
            var boundLeftLine = LineIntersectionsListUpDownSS.GetDimensionByCol(0)
                .SortLineSEPointForDIm();
            //EtGpt.CreateLayer("绞线", 2);
            //EtGpt.SetCurrentLayer("绞线");
            //boundLeftLine.ToSpace();
            //EtGpt.CreateLayer("绞线1", 7);
            //EtGpt.SetCurrentLayer("绞线1");
            var boundRightLine = LineIntersectionsListUpDownSS.GetDimensionByCol(cols - 1)
                .SortLineSEPointForDIm();
            //EtGpt.CreateLayer("绞线2", 3);
            //EtGpt.SetCurrentLayer("绞线2");
            //boundRightLine.ToSpace();
            //EtGpt.CreateLayer("绞线3", 4);
            //EtGpt.SetCurrentLayer("绞线3");
            //  4得到外部尺寸线的标注点
            var pointsLeft = boundLeftLine.GetDimensionPoints(DimensionFor.ForLeft);
            PointsLeft = pointsLeft;
            var dimsLeft = pointsLeft.CreatDimensionFormPointsOutside(DimensionFor.ForLeft);
            var pointsRight = boundRightLine.GetDimensionPoints(DimensionFor.ForRight);
            PointsRight = pointsRight;
            var dimsRight = pointsRight.CreatDimensionFormPointsOutside(DimensionFor.ForRight);
            var dims = new RotatedDimension[][] { dimsLeft, dimsRight };
            return dims;
        }
        public static RotatedDimension[] CreatDimensionFormPointsOutside(this Point3d[] points, DimensionFor dimensionFor)
        {
            var ds = points.GetBoundFromEntities();
            var basePoint = new Point3d();
            double angele = 0;
            double distance = 0;
            var piontDistance = new Point3d();
            var piontDistanceBoundary = new Point3d();
            var dims = new List<RotatedDimension>();
            for (int i = 0; i < points.Length - 1; i++)
            {
                switch (dimensionFor)
                {
                    case DimensionFor.ForLeft:
                        basePoint = new Point3d(ds[0], ds[2], 0);
                        angele = Math.PI / 2;
                        distance = Math.Abs(basePoint.X - points[i].X);
                        piontDistance = points[i] - new Vector3d(distance + DimensionDistanceOutside, 0, 0);
                        piontDistanceBoundary = basePoint - new Vector3d(DimensionDistanceWithDim + DimensionDistanceOutside, 0, 0);
                        break;
                    case DimensionFor.ForRight:
                        basePoint = new Point3d(ds[1], ds[2], 0);
                        angele = Math.PI / 2;
                        distance = Math.Abs(basePoint.X - points[i].X);
                        piontDistance = points[i] + new Vector3d(distance + DimensionDistanceOutside, 0, 0);
                        piontDistanceBoundary = basePoint + new Vector3d(DimensionDistanceWithDim + DimensionDistanceOutside, 0, 0);
                        break;
                    case DimensionFor.ForUp:
                        basePoint = new Point3d(ds[0], ds[3], 0);
                        distance = Math.Abs(basePoint.Y - points[i].Y);
                        piontDistance = points[i] + new Vector3d(0, distance + DimensionDistanceOutside, 0);
                        piontDistanceBoundary = basePoint + new Vector3d(0, DimensionDistanceWithDim + DimensionDistanceOutside, 0);
                        break;
                    case DimensionFor.ForDown:
                        basePoint = new Point3d(ds[0], ds[2], 0);
                        distance = Math.Abs(basePoint.Y - points[i].Y);
                        piontDistance = points[i] - new Vector3d(0, distance + DimensionDistanceOutside, 0);
                        piontDistanceBoundary = basePoint - new Vector3d(0, DimensionDistanceWithDim + DimensionDistanceOutside, 0);
                        break;
                }
                RotatedDimension acRotDim = new RotatedDimension();
                acRotDim.XLine1Point = points[i];
                acRotDim.XLine2Point = points[i + 1];
                acRotDim.DimLinePoint = piontDistance;
                acRotDim.Rotation = angele;
                dims.Add(acRotDim);
            }
            //如果还有二次标注轮廓
            if (IsHasTwoDimOutside)
            {
                RotatedDimension acRotDim = new RotatedDimension();
                acRotDim.XLine1Point = dims.FirstOrDefault().XLine1Point;
                acRotDim.XLine2Point = dims.LastOrDefault().XLine2Point;
                acRotDim.DimLinePoint = piontDistanceBoundary;
                acRotDim.Rotation = angele;
                dims.Add(acRotDim);
            }
            return dims.ToArray();
        }
        public static RotatedDimension[] CreatDimensionFormPointsInside(this Point3d[] points, DimensionFor dimensionFor)
        {
            var ds = points.GetBoundFromEntities();
            double angele = 0;
            var piontDistance = new Point3d();
            var dims = new List<RotatedDimension>();
            for (int i = 0; i < points.Length - 1; i++)
            {
                var pointIn1 = points[i];
                var pointIn2 = points[i + 1];
                switch (dimensionFor)
                {
                    case DimensionFor.ForLeft:
                        if (points[i].X > points[i + 1].X)
                        {
                            var Point = pointIn2;
                            pointIn2 = pointIn1;
                            pointIn1 = Point;
                        }
                        angele = Math.PI / 2;
                        piontDistance = pointIn1 - new Vector3d(DimensionDistanceInside, 0, 0);
                        break;
                    case DimensionFor.ForRight:
                        if (points[i].X > points[i + 1].X)
                        {
                            var Point = pointIn2;
                            pointIn2 = pointIn1;
                            pointIn1 = Point;
                        }
                        angele = Math.PI / 2;
                        piontDistance = pointIn1 + new Vector3d(DimensionDistanceInside, 0, 0);
                        break;
                    case DimensionFor.ForUp:
                        if (points[i].Y > points[i + 1].Y)
                        {
                            var Point = pointIn2;
                            pointIn2 = pointIn1;
                            pointIn1 = Point;
                        }
                        angele = Math.PI / 2;
                        piontDistance = pointIn1 + new Vector3d(0, DimensionDistanceInside, 0);
                        break;
                    case DimensionFor.ForDown:
                        if (points[i].Y > points[i + 1].Y)
                        {
                            var Point = pointIn2;
                            pointIn2 = pointIn1;
                            pointIn1 = Point;
                        }
                        angele = Math.PI / 2;
                        piontDistance = pointIn1 - new Vector3d(0, DimensionDistanceInside, 0);
                        break;
                }
                RotatedDimension acRotDim = new RotatedDimension();
                acRotDim.XLine1Point = pointIn1;
                acRotDim.XLine2Point = pointIn2;
                acRotDim.DimLinePoint = piontDistance;
                acRotDim.Rotation = angele;
                dims.Add(acRotDim);
            }
            return dims.ToArray();
        }
        #region UpDownSecantLine 上下割线
        /// <summary>
        /// 水平直线，上下移动，切割Boundary
        /// </summary>
        /// <param name="y"></param>
        /// <returns></returns>
        public static Line GetSecantLineX(double y)
        {
            var strPt = new Point3d(XMin.X, y, 0);
            var endPt = new Point3d(XMax.X, y, 0);
            var line = new Line(strPt, endPt);
            return line;
        }
        /// <summary>
        /// 割线同Boundary 的交线，方向调整为y轴正向
        /// </summary>
        /// <param name="lines"></param>
        /// <returns></returns>
        public static Line[] ChangeLineXDirection(this Line[] lines)
        {
            for (int i = 0; i < lines.Count(); i++)
            {
                if (lines[i].StartPoint.Y > lines[i].EndPoint.Y)
                {
                    lines[i] = new Line(lines[i].EndPoint, lines[i].StartPoint);
                }
            }
            return lines;
        }
        
        public static Line GetSecantLineXNext(Line IntersectionsShortestLine, Line secantLinePri)
        {
            if (secantLinePri == null || Boundary == null)
            {
                throw new ArgumentNullException("secantLinePri or Boundary cannot be null.");
            }

            double height = secantLinePri.EndPoint.Y;
            int countPri = secantLinePri.GetIntersectionPointsByPolyBoundary(Boundary).Count;
            int countNext = countPri;
            Line SecantLineNext;

            // Handle the case when IntersectionsShortestLine is null
            if (IntersectionsShortestLine == null)
            {
                // Create a new line at a small increment from the previous one
                height += Step / 5.0;
                SecantLineNext = GetSecantLineX(height);
                return SecantLineNext;
            }

            // Add a safety counter to prevent infinite loops
            int safetyCounter = 0;
            const int MAX_ITERATIONS = 1000; // Adjust as needed based on your typical model size

            SecantLineNext = new Line();
            while (countPri == countNext && height <= IntersectionsShortestLine.EndPoint.Y && safetyCounter < MAX_ITERATIONS)
            {
                height += Step;
                SecantLineNext = GetSecantLineX(height);

                try
                {
                    countNext = SecantLineNext.GetIntersectionPointsByPolyBoundary(Boundary).Count;
                }
                catch (Exception ex)
                {
                    // Log the exception if possible
                    // System.Diagnostics.Debug.WriteLine($"Error in GetSecantLineXNext: {ex.Message}");

                    // Return the current line as a fallback
                    return SecantLineNext;
                }

                safetyCounter++;
            }

            // If we hit the safety limit, ensure we return something valid
            if (safetyCounter >= MAX_ITERATIONS)
            {
                // Return a line that's guaranteed to be outside the boundary to break the calling loop
                height = Math.Max(YMax.Y + Step, IntersectionsShortestLine.EndPoint.Y + Step);
                SecantLineNext = GetSecantLineX(height);
            }

            return SecantLineNext;
        }
        public static Line[] GetAllSecantLineX()
        {
            var secantLinePri = new Line();
            var secantLineNext = new Line();
            var secantLineUpDown = new List<Line>();
            var lineIntersectionsList = new List<Line[]>();
            //得到第一条割线
            secantLinePri = GetSecantLineX(YMin.Y + StartStep);
            secantLineUpDown.Add(secantLinePri);
            while (secantLinePri.EndPoint.Y <= YMax.Y)
            {
                //  1 得到割线点集合
                // var ptcool = secantLinePri.GetIntersectionPointsByPolyBoundary(Boundary);
                // var ptcool = secantLinePri.GetIntersectionPointsByPolyBoundary(Boundary);
                var ptcool = secantLinePri.GetIntersectionPointsByPolyBoundaryNTS(Boundary);
                //  2 的到 割线同边界的交线
                var lineIntersections = Boundary.GetIntersectionLineByPoints(ptcool);
                lineIntersections = lineIntersections.SortLineByX();
                lineIntersectionsList.Add(lineIntersections);
                //  3 交线方向统一;
                ChangeLineXDirection(lineIntersections);
                //  3 得到最短的交线                
                var IntersectionsShortestLine = lineIntersections.OrderBy(x => x.EndPoint.Y).FirstOrDefault();
                //  4 的到新的割线,
                secantLineNext = GetSecantLineXNext(IntersectionsShortestLine, secantLinePri);
                //  5 迭代变量,
                secantLinePri = secantLineNext;
                //  6 添加数据
                secantLineUpDown.Add(secantLinePri);
            }
            // 7移除最后一条割线（最后一条，没有和轮廓相交）
            secantLineUpDown.RemoveAt(secantLineUpDown.Count() - 1);
            // 8给割线赋值
            LineIntersectionsListUpDownSS = lineIntersectionsList.ToArray().SortForDimension();
            return secantLineUpDown.ToArray();
        }
        public static Line[] GetDimensionByCol(this Line[,] lines, int count)
        {
            var rowCount = lines.GetLength(0);
            var linesNew = new Line[rowCount];
            for (int i = 0; i < rowCount; i++)
            {
                linesNew[i] = lines[i, count];
            }
            linesNew = linesNew.Where(x => x != null).ToArray();
            return linesNew;
        }
        #endregion
        #region LiftRightSecantLine 左右割线
        /// <summary>
        /// 竖直直线，左右移动，切割Boundary
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        public static Line GetSecantLineLeftRight(double x)
        {
            var strPt = new Point3d(x, YMin.Y, 0);
            var endPt = new Point3d(x, YMax.Y, 0);
            var line = new Line(strPt, endPt);
            return line;
        }
        /// <summary>
        /// 割线同Boundary 的交线，方向调整为x轴正向
        /// </summary>
        /// <param name="lines"></param>
        /// <returns></returns>
        public static Line[] ChangeLineDirectionLeftRight(this Line[] lines)
        {
            for (int i = 0; i < lines.Count(); i++)
            {
                if (lines[i].StartPoint.X > lines[i].EndPoint.X)
                {
                    lines[i] = new Line(lines[i].EndPoint, lines[i].StartPoint);
                }
            }
            return lines;
        }
        /// <summary>
        /// 由上一条割线得到下一条割线
        /// </summary>
        /// <param name="IntersectionsShortestLine"></param>
        /// <param name="secantLinePri"></param>
        /// <returns></returns>
       
        public static Line GetSecantLineNextLeftRight(Line IntersectionsShortestLine, Line secantLinePri)
        {
            if (secantLinePri == null || Boundary == null)
            {
                throw new ArgumentNullException("secantLinePri or Boundary cannot be null.");
            }

            double height = secantLinePri.EndPoint.X;
            int countPri = secantLinePri.GetIntersectionPointsByPolyBoundary(Boundary).Count;
            int countNext = countPri;
            Line SecantLineNext;

            // Handle the case when IntersectionsShortestLine is null
            if (IntersectionsShortestLine == null)
            {
                // Create a new line at a small increment from the previous one
                height += Step / 5.0;
                SecantLineNext = GetSecantLineLeftRight(height);
                return SecantLineNext;
            }

            // Add a safety counter to prevent infinite loops
            int safetyCounter = 0;
            const int MAX_ITERATIONS = 1000; // Adjust as needed based on your typical model size

            SecantLineNext = new Line();
            while (countPri == countNext && height <= IntersectionsShortestLine.EndPoint.X && safetyCounter < MAX_ITERATIONS)
            {
                height += Step;
                SecantLineNext = GetSecantLineLeftRight(height);

                try
                {
                    countNext = SecantLineNext.GetIntersectionPointsByPolyBoundary(Boundary).Count;
                }
                catch (Exception ex)
                {
                    // Log the exception if possible
                    // System.Diagnostics.Debug.WriteLine($"Error in GetSecantLineNextLeftRight: {ex.Message}");

                    // Return the current line as a fallback
                    return SecantLineNext;
                }

                safetyCounter++;
            }

            // If we hit the safety limit, ensure we return something valid
            if (safetyCounter >= MAX_ITERATIONS)
            {
                // Return a line that's guaranteed to be outside the boundary to break the calling loop
                height = Math.Max(XMax.X + Step, IntersectionsShortestLine.EndPoint.X + Step);
                SecantLineNext = GetSecantLineLeftRight(height);
            }

            return SecantLineNext;
        }
        public static Line[] GetAllSecantLineLeftRight()
        {
            var secantLinePri = new Line();
            var secantLineNext = new Line();
            var secantLineLiftRight = new List<Line>();
            var lineIntersectionsList = new List<Line[]>();
            //得到第一条割线
            secantLinePri = GetSecantLineLeftRight(XMin.X + StartStep);
            secantLineLiftRight.Add(secantLinePri);
            while (secantLinePri.EndPoint.X <= XMax.X)
            {
                //  1 得到割线点集合
                // var ptcool = secantLinePri.GetIntersectionPointsByPolyBoundary(Boundary);
                // var ptcool = secantLinePri.GetIntersectionPointsByPolyBoundary(Boundary);
                var ptcool = secantLinePri.GetIntersectionPointsByPolyBoundaryNTS(Boundary);
                //  2 的到 割线同边界的交线
                var lineIntersections = Boundary.GetIntersectionLineByPoints(ptcool);
                lineIntersections = lineIntersections.SortLineByY();
                lineIntersectionsList.Add(lineIntersections);
                //  3 交线方向统一;
                ChangeLineDirectionLeftRight(lineIntersections);
                //  3 得到最短的交线
                var IntersectionsShortestLine = lineIntersections.OrderBy(x => x.EndPoint.X).FirstOrDefault();
                //  4 的到新的割线,
                secantLineNext = GetSecantLineNextLeftRight(IntersectionsShortestLine, secantLinePri);
                //  5 迭代变量,
                secantLinePri = secantLineNext;
                //  6 添加数据
                secantLineLiftRight.Add(secantLinePri);
            }
            // 7移除最后一条割线（最后一条，没有和轮廓相交）
            //secantLineLiftRight.RemoveAt(secantLineLiftRight.Count() - 1);
            // 8给交线赋值
            LineIntersectionsListLeftRightSS = lineIntersectionsList.ToArray().SortForDimension();
            return secantLineLiftRight.ToArray();
        }
        public static Line[] GetDimensionLeftRight(this Line[,] lines, int count)
        {
            var rowCount = lines.GetLength(0);
            var linesNew = new Line[rowCount];
            for (int i = 0; i < rowCount; i++)
            {
                linesNew[i] = lines[i, count];
            }
            linesNew = linesNew.Where(x => x != null).ToArray();
            return linesNew;
        }
        public static Line GetShortestLineInLinesLeftRight(this Line[] lines)
        {
            var ls = lines.OrderBy(x => x.EndPoint.Y);
            return ls.FirstOrDefault();
        }
        #endregion
        #endregion
        #region 辅助方法
        public static Line[] SortLineByX(this Line[] priLines)
        {
            var segs = priLines.GetLineSegment3d();
            var nSegs = segs.OrderBy(x => x.MidPoint.X).ToArray();
            return nSegs.GetLines();
        }
        public static Line[] SortLineByY(this Line[] priLines)
        {
            var segs = priLines.GetLineSegment3d();
            var nSegs = segs.OrderBy(x => x.MidPoint.Y).ToArray();
            return nSegs.GetLines();
        }
        public static Point3d[] GetDimensionPoints(this Line[] lines, DimensionFor dimensionFor)
        {
            var points = new Point3d[lines.Count() * 2];
            int i = 0;
            foreach (var line in lines)
            {
                points[i] = line.StartPoint;
                points[i + 1] = line.EndPoint;
                i = i + 2;
            }
            points = points.Distinct().ToArray();
            switch (dimensionFor)
            {
                case DimensionFor.ForLeft:
                    points = points.SortPointForDimensionForXLeft();
                    break;
                case DimensionFor.ForRight:
                    points = points.SortPointForDimensionForXRight();
                    break;
                case DimensionFor.ForUp:
                    points = points.SortPointForDimensionForYUp();
                    break;
                case DimensionFor.ForDown:
                    points = points.SortPointForDimensionForYDown();
                    break;
            }
            return points;
        }
        public static Line[,] SortForDimension(this Line[][] lineIntersectionsList)
        {
            //var countMax= lineIntersectionsList.Where(x => x.Count() == int.MaxValue).FirstOrDefault();
            var countMax = lineIntersectionsList.Max(x => x.Count());
            var list = new List<Line[]>();
            foreach (var lineIntersections in lineIntersectionsList)
            {
                //var linesVertical = new Line[countMax];
                var linesHorizontal = lineIntersections.Fillarray(countMax);
                list.Add(linesHorizontal);
            }
            var resoult = Tools.ZTools.TransformArrayTo2d(list.ToArray());
            return resoult;
        }
        public static Line[] FlattenList(this IEnumerable<Line[]> lineIntersectionsList)
        {
            var list = new List<Line>();
            foreach (var lineIntersections in lineIntersectionsList)
            {
                foreach (var line in lineIntersections)
                {
                    list.Add(line);
                }
            }
            return list.ToArray();
        }
        public static Line[] ExplodPoly(this Polyline pline)
        {
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            DBObjectCollection acDBObjColl = new DBObjectCollection();
            pline.Explode(acDBObjColl);
            var lines = new List<Line>();
            foreach (Entity acEnt in acDBObjColl)
            {
                if (acEnt is Line)
                {
                    lines.Add((Line)acEnt);
                }
            }
            return lines.ToArray();
        }
        public static Point3d[] GetAllLinePoints(this IEnumerable<Line> lines)
        {
            var list = new List<Point3d>();
            foreach (var line in lines)
            {
                list.Add(line.StartPoint);
                list.Add(line.EndPoint);
            }
            return list.ToArray();
        }
        /// <summary>
        /// 获取直线同PolyLine的交叉点集合
        /// </summary>
        /// <param name="line"></param>
        /// <param name="boundary"></param>
        /// <returns>点集合</returns>
        /// <summary>
        /// 点 Y向从下到上，X向从左到右排列且（只取第一个点）
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static Point3d[] SortPointForDimensionForXLeft(this Point3d[] points)
        {
            var numberGroups = points.OrderBy(p => p.Y)
                .ThenBy(p => p.X)
                //.GroupBy(p => p.Y)
                .Select(x => x)
                .ToList();
            return numberGroups.ToArray();
        }
        /// <summary>
        /// 点 Y向从下到上，X向从右到左排列且（只取第一个点）
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static Point3d[] SortPointForDimensionForXRight(this Point3d[] points)
        {
            var numberGroups = points.OrderBy(p => p.Y)
                .ThenByDescending(p => p.X)
                //.GroupBy(p => p.Y)
                .Select(x => x)
                .ToList();
            return numberGroups.ToArray();
        }
        /// <summary>
        /// 点 X向从左到右，Y向从上到下排列且（只取第一个点）
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static Point3d[] SortPointForDimensionForYUp(this Point3d[] points)
        {
            var numberGroups = points.OrderBy(p => p.X)
                .ThenByDescending(p => p.Y)
                //这里之所以不用GroupBy，主要是因为Cad中的误差。
                // .GroupBy(p => p.X)
                .Select(p => p)
                .ToList();
            return numberGroups.ToArray();
        }
        /// <summary>
        /// 点 X向从左到右，Y向从下到上排列且（只取第一个点）
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static Point3d[] SortPointForDimensionForYDown(this Point3d[] points)
        {
            var numberGroups = points.OrderBy(p => p.X)
                .ThenBy(p => p.Y)
                //.GroupBy(p => p.X)
                .Select(p => p)
                .ToList();
            return numberGroups.ToArray();
        }
        #endregion
    }
}
