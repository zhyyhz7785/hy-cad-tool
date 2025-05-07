using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Config;
using HyCADTool.Tools;
using System.Collections.Generic;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
[assembly: CommandClass(typeof(HyCADTool.BaseRein))]
namespace HyCADTool
{
    public static partial class BaseRein
    {
        public enum IntersectionType
        {
            NegativeExtensionIntersection, // 负延交点
            StartPoint,                // 起点
            InsideLowerIntersection,   // 内下交点
            InsideUpperIntersection,   // 内上交点
            EndPoint,                  // 端点
            PositiveExtensionIntersection, // 正延交点        
        }
        public enum DimensionFor
        {
            ForLeft,
            ForRight,
            ForUp,
            ForDown,
        }
        public enum IntersectionsDirection
        {
            LeftRight,
            UpDown,
        }
        public enum RebarDirection
        {
            TopX,
            TopY,
            BottomX,
            BottomY
        }
        public static IntersectionsDirection InterDirection { get; set; } = IntersectionsDirection.LeftRight;
        public static DimensionFor DimDirection { get; set; } = DimensionFor.ForLeft;
        // 画上皮纲吉还是下皮钢筋，x向还是y向
        public static RebarDirection Direction { get; set; } = RebarDirection.TopX;
        private static readonly double[] AllowedDiameters = { 6, 8, 10, 12, 14, 16, 18, 20, 22, 25 };
        public static double ReinforceSafety { get; set; } = 1;
        //proximityThreshold 把有限元网格分组调整数据，如果间距小于1000mm就把它们连接在一组
        public static double proximityThreshold { get; set; } = 2000;
        //PillarPiersThreshold 调整柱墩尺逐步缩小
        public static double PillarPiersDistance { get; set; } = 2500;
        //输入标注有小数时 标注尺寸的取整间隔
        public static double Interval { get; set; } = 100;
        public static double AxisExtend { get; set; } = 15000;
        public static double DimensionDistanceWithDim = 6;
        //钢筋标注之间的距离
        public static double ReinforceDistance = 3;
        //钢筋标注文件间y向距离
        public static double ReinforceTextDistanceY = 0;
        public static double ReinforceTextDistanceX = 0;
        //程序生成数据存储
        public static List<Polyline> PillarPiers;
        //第三步 删除多余有限元网格，返回包含有效数据的有限元网格 
        public static Dictionary<DBText, ObjectId> SourceTextAndFinitePoly;
        //第四步 4.1 调整有限元网格为正四边形Polyline 返回配筋文字和正四边形网格
        public static Dictionary<DBText, ObjectId> SourceTextAndEnvelopePoly;
        //第四步 4.2 根据空间相邻关系对对象进行分组。 返回配筋区域数组 每一个数据包括配筋面积。和配筋正四边形网格。
        public static List<Dictionary<DBText, ObjectId>> GroupByTextAndEnvelopePoly;
        //第四步 4.3 根据分组结果创建包围多边形  返回字典<配筋区域,配筋区域内的配筋文字 >
        public static Dictionary<Polyline, List<DBText>> ReinAreaAndText;
        // 定义属性来存储用户输入的最小钢筋直径
        public static double MinAdditionalDiameter { get; set; } = 8;
        public static double AdditionalDiameter { get; set; }
        public static List<double> AdditionalDiameterList { get; set; }
        public static double AdditionalSpacing { get; set; } = 200;
        public static double PlateThickness { get; set; } = 350;
        public static List<string> FixedValues { get; set; } = new List<string>() { "<5.65", "7.5", "9", "12" };
        public static double Scale { get; set; } = 100;
        public static double AnchorFactor { get; set; } = 35;
        public static double RebarDiameter { get; set; } = 12;
        public static double RebarSpacing { get; set; } = 200;
        public static double TextToLineDistance { get; set; } = 1.5;
        public static double HookLength { get; set; } = 1.5;
        public static double PolylineWidth { get; set; } = 0.4;
        public static bool AddAnchorLength = true;
        public static bool ExistingRebar = true;
        public static bool DimAll = true;
        public static ObjectId TextID { get; set; }
        public static ObjectId ReinforcementPolylineLayerId { get; set; }
        // 构造函数
        static BaseRein()
        {
        }
        public static void SetUp()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            // 创建所需的图层          
            EtGpt.CreateMultipleLayers(
                ("00_hy_配筋轮廓", 143), // 红色
                 ("00_hy_调整配筋轮廓", 1), // 绿色  
                ("00_hy_筏板附加配筋x_上", 1), // 红色
                ("00_hy_筏板附加配筋y_上", 1), // 红色
                ("00_hy_筏板附加配筋x_下", 20), // 红色
                ("00_hy_筏板附加配筋y_下", 20), // 红色
                ("00_hy_筏板附加配筋文字_x", 7), // 白色              
                ("00_hy_筏板附加配筋文字_y", 7), // 白色              
                ("00_hy_调整配筋轮廓_上x", 142), // 绿色                
                ("00_hy_调整配筋轮廓_上y", 72), // 绿色               
               ("00_hy_调整配筋轮廓_下", 78), // 绿色                
                ("00_hy_筏板附加配筋x_标注", 3), // 绿色                
                ("00_hy_筏板附加配筋Y_标注", 3),// 绿色                
                ("00_hy_表格", 39),// 绿色                
                ("00_hy_表格_索引", 7) // 绿色                
            );
            BaseConfig.Scale = Scale;
            EtGpt.CreateTextStyle(BaseConfig.TextStyleConfig.Name);
            EtGpt.CreateDimStyle(BaseConfig.DimStyleConfig.Name);
            EtGpt.CreateMLeaderStyle(BaseConfig.MLeaderStyleConfig.Name);
        }
    }
}
