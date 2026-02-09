using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Domain.Services;
using HyCADTool.Refactored.Domain.ValueObjects;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Domain.ValueObjects.Reinforcement;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Converters;
using System;
using System.Collections.Generic;

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
        /// 应用样式设置
        /// </summary>
        public void ApplyStyle(ReinParameters parameters)
        {
            _layerService.CreateMultipleLayers(
                (LayerLineRein, 1),
                (LayerDotRein, 5),
                (LayerDimOutside, 3),
                (LayerLeader, 92)
            );

            _styleService.CreateTextStyle(
                DefaultTextStyleName, "tssdeng.shx", "hztxt.shx",
                parameters.TextSize * parameters.Scale, parameters.TextXScale);

            _styleService.CreateDimensionStyle(DefaultDimStyleName, DefaultTextStyleName, parameters.Scale);
            _styleService.CreateMLeaderStyle(DefaultMLeaderStyleName, DefaultTextStyleName, parameters.Scale);
            _styleService.CreateTableStyle(DefaultTableStyleName, DefaultTextStyleName);
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
        /// 创建多重引线（与旧项目 ZTools.AddMleader 一致：多根引线汇集到文字集中点）
        /// </summary>
        private static MLeader CreateMLeader(MLeaderData data, ReinParameters parameters)
        {
            if (data.AnchorPoints == null || data.AnchorPoints.Length < 2)
                return null;

            var db = Application.DocumentManager.MdiActiveDocument.Database;
            var startP = data.AnchorPoints[0].ToAcadPoint3d();
            var endP = data.AnchorPoints[data.AnchorPoints.Length - 1].ToAcadPoint3d();

            // 线段方向与垂直方向（文字在垂直方向偏移）
            var vecH = (endP - startP).GetNormal();
            var vecV = vecH.TransformBy(Matrix3d.Rotation(-Math.PI / 2, Vector3d.ZAxis, Point3d.Origin)).GetNormal();

            // 集中点 = 线段中点 + 垂直方向 * 引线距离
            var midP = new Point3d(
                (startP.X + endP.X) / 2,
                (startP.Y + endP.Y) / 2,
                0);
            var centralPoint = midP + vecV * data.LeaderDistance;

            var mleader = new MLeader();
            mleader.MLeaderStyle = db.MLeaderstyle;

            // 每个锚点一根引线，汇集到 centralPoint
            foreach (var pt in data.AnchorPoints)
            {
                int leaderIndex = mleader.AddLeader();
                int lineIndex = mleader.AddLeaderLine(leaderIndex);
                var p3 = pt.ToAcadPoint3d();
                mleader.AddFirstVertex(lineIndex, p3);
                mleader.AddLastVertex(lineIndex, centralPoint);
            }

            // 文字：内容、高度、旋转（与线段平行）
            var line = new Line(startP, endP);
            double angle = line.Angle;
            line.Dispose();

            var mtext = new MText();
            mtext.Contents = data.Content;
            mtext.TextHeight = parameters.TextSize * parameters.Scale;
            mtext.Rotation = angle;
            mleader.MText = mtext;

            return mleader;
        }

        #endregion
    }
}
