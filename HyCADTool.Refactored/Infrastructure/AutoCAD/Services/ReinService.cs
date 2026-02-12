using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Domain.Services;
using HyCADTool.Refactored.Domain.ValueObjects;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Domain.ValueObjects.Reinforcement;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Converters;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services
{
    /// <summary>
    /// 钢筋服务实现
    /// 将 Domain 层的 ReinforcementResult 写入 AutoCAD 图纸
    /// 对应旧代码 GenerateReinforcement
    /// </summary>
    public class ReinService : IReinService
    {
        private readonly ILayerService _layerService;
        private readonly IStyleService _styleService;

        private const string DefaultTextStyleName = "0_Hy_40";
        private const string DefaultDimStyleName = "0_Hy_40_Dim";
        private const string DefaultMLeaderStyleName = "0_Hy_40_MLeader";
        private const string DefaultTableStyleName = "0_Hy_40_Table";

        private const string LayerLineRein = "01_hy_1钢筋_线钢筋";
        private const string LayerDotRein = "01_hy_1钢筋_点钢筋";
        private const string LayerDimOutside = "00_hy_3公共_标注1_外";
        private const string LayerLeader = "00_hy_3公共_标注3_引线";

        public ReinService(ILayerService layerService, IStyleService styleService)
        {
            _layerService = layerService ?? throw new ArgumentNullException(nameof(layerService));
            _styleService = styleService ?? throw new ArgumentNullException(nameof(styleService));
        }

        /// <summary>
        /// 样式和图层已在 PluginInitializer 初始化时创建，命令执行时无需操作
        /// </summary>
        public void ApplyStyle(ReinParameters parameters)
        {
            // 不再做任何操作 — 图层和样式已在插件启动时一次性创建
        }

        /// <summary>
        /// 将配筋结果写入图纸
        /// 对应旧代码 GenerateReinforcement
        /// </summary>
        public void DrawReinforcement(ReinforcementResult result, ReinParameters parameters)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                // 1. 线钢筋 → 线钢筋图层
                _layerService.SetCurrentLayer(LayerLineRein);
                if (result.FinalReinforcements != null)
                {
                    foreach (var polyDomain in result.FinalReinforcements)
                    {
                        var acadPoly = polyDomain.ToAcadPolyline();
                        acadPoly.Layer = LayerLineRein;
                        btr.AppendEntity(acadPoly);
                        tr.AddNewlyCreatedDBObject(acadPoly, true);
                    }
                }

                // 2. 减少数量点钢筋 → 点钢筋图层
                _layerService.SetCurrentLayer(LayerDotRein);
                double dotDiameter = parameters.ReinforcementDiameter * parameters.Scale;
                if (result.ReduceDotReinPoints != null)
                {
                    foreach (var pt in result.ReduceDotReinPoints)
                    {
                        var circle = CreateSolidCircle(dotDiameter, pt);
                        circle.Layer = LayerDotRein;
                        btr.AppendEntity(circle);
                        tr.AddNewlyCreatedDBObject(circle, true);
                    }
                }

                // 3. 标注 → 引线图层
                _layerService.SetCurrentLayer(LayerLeader);
                if (result.MLeaders != null)
                {
                    foreach (var mlData in result.MLeaders)
                    {
                        var mleader = CreateMLeader(mlData, parameters);
                        if (mleader != null)
                        {
                            mleader.Layer = LayerLeader;
                            btr.AppendEntity(mleader);
                            tr.AddNewlyCreatedDBObject(mleader, true);
                        }
                    }
                }

                tr.Commit();
            }
        }

        /// <summary>
        /// 标注钢筋
        /// </summary>
        public void DimensionRein(int mode, ReinParameters parameters)
        {
            throw new NotImplementedException($"钢筋标注模式 {mode} 功能尚未迁移。");
        }

        #region 辅助方法

        /// <summary>
        /// 创建实心圆（多段线近似）代表点钢筋
        /// </summary>
        private static Polyline CreateSolidCircle(double diameter, Point2D center)
        {
            double radius = diameter / 2.0;
            var poly = new Polyline();

            // 用两段弧近似圆
            poly.AddVertexAt(0, new Point2d(center.X - radius, center.Y), 1.0, 0, 0);
            poly.AddVertexAt(1, new Point2d(center.X + radius, center.Y), 1.0, 0, 0);
            poly.Closed = true;

            return poly;
        }

        /// <summary>
        /// 创建多重引线（直接使用 gb 命令的 AddMleader 逻辑）
        /// </summary>
        private static MLeader CreateMLeader(MLeaderData data, ReinParameters parameters)
        {
            if (data.AnchorPoints == null || data.AnchorPoints.Length < 2)
                return null;

            // 转换为 Point3d[]
            var points = data.AnchorPoints.Select(pt => pt.ToAcadPoint3d()).ToArray();
            
            // 直接调用 gb 命令的扩展方法
            return points.AddMleader(data.LeaderDistance, data.Content);
        }

        #endregion
    }
}
