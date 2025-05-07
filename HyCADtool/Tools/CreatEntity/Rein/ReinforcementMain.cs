//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.Geometry;
//using Autodesk.AutoCAD.Runtime;
//using HyCADTool.Config;
//using HyCADTool.Log;
//using HyCADTool.Tools;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using System.Timers;
//namespace HyCADTool
//{
//    public static partial class Reinforcement
//    {
//        public static double ToleranceDouble => BaseConfig.ToleranceDouble;
//        public static Tolerance ToleranceVec => BaseConfig.ToleranceVec;
//        public static Tolerance TolerancePoint => BaseConfig.TolerancePoint;
//       // 使用CADSettings的Scale为基础
//        public static double Scale
//        {
//            get => BaseConfig.Scale;
//            set
//            {
//                if (Math.Abs(BaseConfig.Scale - value) > 1e-9)
//                {
//                    BaseConfig.Scale = value;
//                    _pendingScale = value;
//                    // 当Scale变化时启动计时器延迟应用
//                    _scaleTimer?.Start();
//                }
//            }
//        }
//       #region 基准值字段定义       
//        private static double _textSize = 3;   
//        private static double _dimensionDistanceOutside = 14;
//        private static double _dimensionDistance = 4;
//        private static double _dimensionDistanceInside = 6;
//        private static double _dimensionDistanceWithDim = 6;
//        private static double _mleaderDistance = 6;
//        private static double _reinforcementDiameter = 0.35;
//        private static double _dotReinOffset = 1.35;
//        private static double _hookLength = 1;
//        private static double _protectionThickness = 1;
//        #endregion
//       #region 属性区域
//       public static double RebarDiameter { get; set; } = 16;
//        public static double RebarSpacing { get; set; } = 200;
//        public static double AnchorageLength { get; set; } = 500;
//       public static double DotSeparation { get; set; } = 200;
//       public static double BendingLineMinLength { get; set; } = 150;
//        public static double BendingLineLength { get; set; } = 200;
//       public static double AnchorageJoinLength { get; set; } = 1500;
//       public static double TextSize
//        {
//            get => _textSize * Scale;
//            set => _textSize = value / Scale;
//        }
//        public static double TextXScale { get; set; } = 0.7;
//       public static double DimDistanceTolerance { get; set; } = 30;
//       public static double DimensionDistanceOutside
//        {
//            get => _dimensionDistanceOutside * Scale;
//            set => _dimensionDistanceOutside = value / Scale;
//        }
//        public static double DimensionDistance
//        {
//            get => _dimensionDistance * Scale;
//            set => _dimensionDistance = value / Scale;
//        }
//        public static double DimensionDistanceInside
//        {
//            get => _dimensionDistanceInside * Scale;
//            set => _dimensionDistanceInside = value / Scale;
//        }
//        public static double DimensionDistanceWithDim
//        {
//            get => _dimensionDistanceWithDim * Scale;
//            set => _dimensionDistanceWithDim = value / Scale;
//        }
//        public static double MleaderDistance
//        {
//            get => _mleaderDistance * Scale;
//            set => _mleaderDistance = value / Scale;
//        }
//        public static double ReinforcementDiameter
//        {
//            get => _reinforcementDiameter * Scale;
//            set => _reinforcementDiameter = value / Scale;
//        }
//        public static double DotReinOffset
//        {
//            get => _dotReinOffset * Scale;
//            set => _dotReinOffset = value / Scale;
//        }
//        public static double HookLength
//        {
//            get => _hookLength * Scale;
//            set => _hookLength = value / Scale;
//        }
//        public static double ProtectionThickness
//        {
//            get => _protectionThickness * Scale;
//            set => _protectionThickness = value / Scale;
//        }
//       public static string[] LayerNames { get; set; }
//        private static ObjectId[] LayerIds { get; set; }
//        #endregion
//       #region 其他属性
//        public static Polyline[] SubReinforcements { get; set; }
//        public static Polyline[] SubReinforcementWithAnchors { get; set; }
//        public static Polyline[] SubReinforcementWithAnchorsAddhooks { get; set; }
//        public static List<Dictionary<int, bool>> IsReinforcementBending { get; set; }
//        public static Polyline Boundary { get; set; }
//        public static Polyline[] DotRein { get; set; }
//        public static Polyline DotReinCenterPoly { get; set; }
//        public static Polyline[] ReduceDotRein { get; set; }
//        public static Point3d[] DotReinPoints { get; set; }
//        public static Point3d[] ReduceDotReinPoints { get; set; }
//        public static double DotStartDistance { get; set; }
//        public static MLeader[] Mleaders { get; set; }
//        #endregion
//       #region 计时器与事件字段
//        private static System.Timers.Timer _scaleTimer;
//        private static double _pendingScale;
//       // 为事件委托创建静态字段，确保引用不被GC回收
//        private static DocumentCollectionEventHandler docCreatedHandler = DocumentManager_DocumentCreated;
//        private static DocumentCollectionEventHandler docActivatedHandler = DocumentManager_DocumentActivated;
//        private static ElapsedEventHandler scaleTimerElapsedHandler = OnScaleTimerElapsed;
//        #endregion
//       #region 静态构造函数
//        static Reinforcement()
//        {
//            // 初始化Scale（此时Scale=40）
//            Scale = 40;
//           // 初始化计时器并绑定事件
//            _scaleTimer = new System.Timers.Timer(1000);
//            _scaleTimer.Elapsed += scaleTimerElapsedHandler; // 使用静态字段引用事件处理程序
//            _scaleTimer.AutoReset = false;
//           // 初始化图层和样式
//            ApplyScale(Scale);
//           // 使用静态字段引用事件处理程序
//            Application.DocumentManager.DocumentCreated += docCreatedHandler;
//            Application.DocumentManager.DocumentActivated += docActivatedHandler;
//        }
//        #endregion
//       #region 事件处理
//        private static void DocumentManager_DocumentCreated(object sender, DocumentCollectionEventArgs e)
//        {
//            Initialize(e.Document);
//        }
//       private static void DocumentManager_DocumentActivated(object sender, DocumentCollectionEventArgs e)
//        {
//            Initialize(e.Document);
//        }
//       private static void OnScaleTimerElapsed(object sender, ElapsedEventArgs e)
//        {
//            // 当计时结束时正式应用Scale
//            double newScale = _pendingScale;
//            ApplyScale(newScale);
//            Initialize(Application.DocumentManager.MdiActiveDocument);
//        }
//        #endregion
//       #region 初始化方法
//        private static void Initialize(Document doc)
//        {
//            if (doc == null || doc.Database == null)
//                return;
//            var db = doc.Database;
//            // 可在此根据doc需要进行初始化
//        }
//        #endregion
//       #region 方法区域
//        private static void ApplyScale(double newScale)
//        {
//            // 创建或更新图层
//            EtGpt.CreateMultipleLayers(
//                ("01_hy_1钢筋_线钢筋", 1),
//                ("01_hy_2钢筋_点钢筋", 5),
//                ("00_hy_3公共_标注1_外", 3),
//                ("00-Hy-标注", 92)
//            );
//           // 根据Scale创建/更新文字、标注和引线样式
//            string textStyleName = $"Reinforcement_Text_{newScale}";
//            EtGpt.CreateTextStyle(textStyleName);
//           string dimStyleName = $"Reinforcement_Dim_{newScale}";
//            EtGpt.CreateDimStyle(dimStyleName);
//           string mleaderStyleName = $"Reinforcement_Mle_{newScale}";
//            EtGpt.CreateMLeaderStyle(mleaderStyleName);
//        }
//       public static void Rein()
//        {
//            var doc = Application.DocumentManager.MdiActiveDocument;
//            var db = doc.Database;
//            var ed = doc.Editor;
//           // 提示用户选择Polyline
//            var poly = db.SelectAEntity<Polyline>();
//            if (poly == null)
//            if (poly == null)
//                {
//                ed.WriteMessage("\n请重新选择 Polyline.");
//                return;
//            }
//            SimpleLogger.Log("Starting Rein method");
//            SimpleLogger.LogElapsedTime("GenerateReinforcement", () =>
//            {
//                Reinforcement.GenerateReinforcement(poly);
//            });
//            SimpleLogger.LogElapsedTime("GenerateDimension", () =>
//            {
//                DimensionForReinforcement.GenerateDimension(poly);
//            });
//            SimpleLogger.Log("Rein method completed");
//        }
//        #endregion
//    }
//}
