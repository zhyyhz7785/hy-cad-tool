using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using EquipmentFoundation.Models;
using HyCADTool.Log;
using HyCADTool.Tools;
namespace EquipmentFoundation
{
    public partial class ElevationModelGenerator
    {
        private List<GeometryData> GeometryDatas { get; set; }
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
        public bool UseAverageSpan { get; set; } = false;
        public bool IsInitialized { get; set; } = false;
        public TopFixity TopFixityOption { get; set; } = TopFixity.Cantilever;
        #endregion
        public ElevationModelGenerator()
        {
            Bufferid1 = EtGpt.CreateLayer("00_Hy_buffer_1", 36);
            Bufferid2 = EtGpt.CreateLayer("00_Hy_buffer_2", 140);
            Bufferid3 = EtGpt.CreateLayer("00_Hy_buffer_3", 72);
            Bufferid4 = EtGpt.CreateLayer("00_Hy_buffer_4", 150);
            Bufferid_Region = EtGpt.CreateLayer("00_Hy_buffer_Bufferid_Region", 66);
            Bufferid_Base = EtGpt.CreateLayer("00_Hy_buffer_Bufferid_Base", 137);
            Bufferid_Wall = EtGpt.CreateLayer("00_Hy_buffer_Bufferid_Wall", 253);
        }
        public void GenerateModel()
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage("\n开始生成3D模型...\n");
            // 1. 从 AutoCAD 选择数据并填充 GeometryInput
            var input = SelectGeometryInputFromAutoCAD();
            if (input.InnerPolygons.Count == 0)
            {
                ed.WriteMessage("\n错误: 未选择任何封闭的多边形，模型生成中止。\n");
                return;
            }
            // 2. 先转换为 GeometryData，包含标高数据
            var geometryDatas = ConvertToGeometryData(input);
            if (geometryDatas.Count == 0)
            {
                ed.WriteMessage("\n错误: 未生成有效的 GeometryData（可能缺少标高数据），模型生成中止。\n");
                return;
            }
            // 3. 检查标高数据是否有效
            bool hasValidElevation = false;
            foreach (var geomData in geometryDatas)
            {
                if (geomData.Elevation != 0.0) // 假设标高为 0 表示未赋值或无效
                {
                    hasValidElevation = true;
                    break;
                }
            }
            if (!hasValidElevation)
            {
                ed.WriteMessage("\n错误: 所有 GeometryData 的标高均为 0 或未赋值，模型生成中止。\n");
                return;
            }
            // 4. 计算厚度和边界条件
            CalculateBaseAndWallThickness(geometryDatas);
            // 5. 生成3D图形
            using (var tr = HostApplicationServices.WorkingDatabase.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(HostApplicationServices.WorkingDatabase.BlockTableId, OpenMode.ForRead);
                var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                var anchorBolt = AnchorBoltFactory.CreateBolt("1");
                foreach (var geomData in geometryDatas)
                {
                    GenerateWalls(geometryDatas);
                    GenerateRaftAndBase(geometryDatas,400);
                   // GenerateBoltHoles(geomData, btr, tr, anchorBolt);
                }
                tr.Commit();
                ed.WriteMessage("\n3D模型生成完成。\n");
            }
        }
        private void GenerateWalls(GeometryData geomData, BlockTableRecord btr, Transaction tr)
        {
            foreach (var wall in geomData.Walls.Where(w => w.IsWall))
            {
                var solid = CreateWallSolid(wall); // 自定义方法生成墙体3D实体
                solid.Layer = "Walls";
                btr.AppendEntity(solid);
                tr.AddNewlyCreatedDBObject(solid, true);
            }
        }
        private void GenerateBase(GeometryData geomData, BlockTableRecord btr, Transaction tr)
        {
            var solid = CreateBaseSolid(geomData); // 自定义方法生成基础3D实体
            solid.Layer = "Base";
            btr.AppendEntity(solid);
            tr.AddNewlyCreatedDBObject(solid, true);
        }
        private void GenerateBolts(GeometryData geomData, List<Circle> bolts, BlockTableRecord btr, Transaction tr)
        {
            foreach (var bolt in bolts.Where(b => IsPointInsidePolygon(b.Center, geomData.Polygon)))
            {
                var solid = CreateBoltSolid(bolt); // 自定义方法生成螺栓3D实体
                solid.Layer = "Bolts";
                btr.AppendEntity(solid);
                tr.AddNewlyCreatedDBObject(solid, true);
            }
        }
        private void GenerateEmbeddedPlates(GeometryData geomData, BlockTableRecord btr, Transaction tr)
        {
            // 假设预埋板基于螺栓位置生成
            foreach (var bolt in geomData.Walls.SelectMany(w => w.Boundary.CoincidentEdge != null ? new[] { w } : new WallData[0]))
            {
                var solid = CreatePlateSolid(bolt); // 自定义方法生成预埋板3D实体
                solid.Layer = "EmbeddedPlates";
                btr.AppendEntity(solid);
                tr.AddNewlyCreatedDBObject(solid, true);
            }
        }
        // 占位方法，需根据实际需求实现
        private Solid3d CreateWallSolid(WallData wall) => new Solid3d();
        private Solid3d CreateBaseSolid(GeometryData geomData) => new Solid3d();
        private Solid3d CreateBoltSolid(Circle bolt) => new Solid3d();
        private Solid3d CreatePlateSolid(WallData wall) => new Solid3d();
    }
}
