using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Log;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
namespace HyCADTool.HelpClass.CreatBase
{
    public partial class ElevationModelGenerator
    {
        #region 枚举和属性
        public enum TopFixity { Fixed, Hinged, Cantilever }
        public List<WallData> AllWalls { get; set; }
        public static ObjectId Bufferid2 { get; set; }
        public static ObjectId Bufferid1 { get; set; }
        public static ObjectId Bufferid3 { get; set; }
        public static ObjectId Bufferid4 { get; set; }
        public static ObjectId Bufferid_Region { get; set; }
        public static ObjectId Bufferid_Base { get; set; }
        public static ObjectId Bufferid_Wall { get; set; }
        public double WallThickness { get; set; } = 300;
        public double BaseThickness { get; set; } = 400;
        public double MinBaseThickness { get; set; } = 300;
        public double ExtendLength { get; set; } = 400;
        public Dictionary<Polyline, List<Line>> OverlappingEdgesDic { get; set; }
        public Dictionary<Polyline, Polyline> EnclosingPolygon { get; set; }
        public List<Polyline> Polygons { get; set; }
        public Dictionary<Polyline, double> ElevationsDic { get; set; }
        public List<Polyline> OuterContours { get; set; }
        public Dictionary<Polyline, List<BoundaryCondition>> BoundaryConditions { get; set; }
        public List<GeometryData> GeometryDatas { get; set; }
        public bool UseAverageSpan { get; set; } = false;
        public bool IsInitialized { get; set; } = false;
        public TopFixity TopFixityOption { get; set; } = TopFixity.Cantilever;
        #endregion
        #region 构造函数
        public ElevationModelGenerator()
        {
            Bufferid1 = Tools.ZTools.CreateLayer("00_Hy_buffer_1", 36);
            Bufferid2 = Tools.ZTools.CreateLayer("00_Hy_buffer_2", 140);
            Bufferid3 = Tools.ZTools.CreateLayer("00_Hy_buffer_3", 72);
            Bufferid4 = Tools.ZTools.CreateLayer("00_Hy_buffer_4", 150);
            Bufferid_Region = Tools.ZTools.CreateLayer("00_Hy_buffer_Bufferid_Region", 66);
            Bufferid_Base = Tools.ZTools.CreateLayer("00_Hy_buffer_Bufferid_Base", 137);
            Bufferid_Wall = Tools.ZTools.CreateLayer("00_Hy_buffer_Bufferid_Wall", 253);
            try
            {
                SimpleLogger.LogElapsedTime("ElevationModelGenerator 初始化", Initialize);
                IsInitialized = true;
            }
            catch (Exception ex)
            {
                var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
                ed?.WriteMessage($"\n初始化 ElevationModelGenerator 失败: {ex.Message}\n{ex.StackTrace}");
                SimpleLogger.LogError("ElevationModelGenerator 初始化失败", ex);
                throw;
            }
        }
        private void Initialize()
        {
            SimpleLogger.LogElapsedTime("获取标高字典", () =>
            {
                ElevationsDic = GetPolygonTextDictionary(out List<Polyline> outerContours);
                OuterContours = outerContours;
            });
            if (ElevationsDic == null || ElevationsDic.Count == 0)
            {
                SimpleLogger.LogWarning("ElevationModelGenerator 初始化失败：未能获取标高字典。");
                IsInitialized = false;
                return;
            }
            SimpleLogger.LogElapsedTime("初始化多边形及字典", () =>
            {
                Polygons = new List<Polyline>(ElevationsDic.Keys);
                OverlappingEdgesDic = new Dictionary<Polyline, List<Line>>();
                EnclosingPolygon = new Dictionary<Polyline, Polyline>();
            });
            SimpleLogger.LogElapsedTime("分析边界条件", () =>
            {
                BoundaryConditions = AnalyzeBoundaryConditions();
            });
            SimpleLogger.LogElapsedTime("初始化几何数据", () =>
            {
                GeometryDatas = InitializeGeometryData(BoundaryConditions);
            });
            SimpleLogger.LogElapsedTime("计算底板与围墙厚度", () =>
            {
                CalculateBaseAndWallThickness(GeometryDatas);
            });
        }
        #endregion
        private List<GeometryData> InitializeGeometryData(Dictionary<Polyline, List<BoundaryCondition>> boundaryConditions)
        {
            var geometryDataList = new List<GeometryData>();
            var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            foreach (var polygon in Polygons)
            {
                if (!ElevationsDic.ContainsKey(polygon)) continue;
                double elevation = ElevationsDic[polygon];
                var geomData = new GeometryData(polygon, MinBaseThickness);
                geomData.Elevation = elevation;
                var conditions = boundaryConditions.ContainsKey(polygon) ? boundaryConditions[polygon] : null;
                if (conditions != null)
                {
                    foreach (var condition in conditions)
                    {
                        double innerElevation = elevation;
                        double outerElevation = condition.IsSoilBoundary ? 0.000 :
                            (condition.AdjacentPolygon != null && ElevationsDic.ContainsKey(condition.AdjacentPolygon) ?
                            ElevationsDic[condition.AdjacentPolygon] : 0.000);
                        var wallData = new WallData(condition.Edge, innerElevation, outerElevation, 0, condition, condition.CoincidentEdge);
                        geomData.AddWallData(wallData);
                    }
                }
                geometryDataList.Add(geomData);
            }
            return geometryDataList;
        }
        public static void RunTest()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            var ed = doc.Editor;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                var generator = new ElevationModelGenerator();
                //var overlappingEdgesDic = generator.OverlappingEdgesDic;
                //var wall = generator.AllWalls;
                SimpleLogger.LogElapsedTime("初始化几何数据", () =>
                {
                    //generator. ExtendWallsAndGenerateRegions();
                });
                //foreach (var geomData in generator.GeometryDatas)
                //{
                //    SimpleLogger.LogElapsedTime("初始化几何数据", () =>
                //    {
                //        //DebugPrintGeometryData(geomData, ed);
                //        //generator.GenerateExtendedWallRegions(200);
                //        //ElevationModelGenerator.OffsetPolylineEdges(geomData);
                //    });
                //    //pl.ToAutoCadPolyline().ToSpace();
                //}
                tr.Commit();
            }
        }
    }
}