using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Extensions
{
    /// <summary>
    /// MLeader 创建扩展方法（对应旧代码 ZTools.AddMleader / AddMleaderOne / AddMleaderSix）
    /// </summary>
    public static class MLeaderExtensions
    {
        /// <summary>
        /// 创建多引线标注（对应旧 gb 命令）
        /// 多根引线汇集到集中点，文字沿线段方向旋转
        /// </summary>
        public static MLeader AddMleader(this Point3d[] points, double distance, string content)
        {
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            var startP = points.First();
            var endP = points.Last();

            MLeader ml = new MLeader();

            var vecH = (endP - startP).GetNormal();
            var line = new Line(startP, endP);
            double angle = line.Angle;

            // 调整引线方向（第二三象限时翻转）
            // 注意：vecH 和 endP 保持原始值不变，centralPoint 基于原始方向计算
            if (angle > Math.PI / 2 && angle <= Math.PI * 3 / 2)
            {
                points = points.Reverse().ToArray();
                line = new Line(points.First(), points.Last());
                // 文字角度翻转 180 度，避免倒置
                angle = line.Angle;
            }

            var vecV = vecH.RotateBy(-Math.PI / 2, Vector3d.ZAxis);
            var centralPoint = endP + vecV * distance;

            foreach (var point in points)
            {
                int leaderIndex = ml.AddLeader();
                int leaderLineIndex = ml.AddLeaderLine(leaderIndex);
                ml.AddFirstVertex(leaderLineIndex, point);
                ml.AddLastVertex(leaderLineIndex, centralPoint);
            }

            ml.MLeaderStyle = db.MLeaderstyle;

            MText mt = new MText();
            mt.TextStyleId = ml.TextStyleId;
            mt.Color = ml.TextColor;
            mt.TextHeight = ml.TextHeight;
            mt.Contents = content;
            mt.Rotation = angle;  // 使用调整后的角度
            ml.MText = mt;

            return ml;
        }

        /// <summary>
        /// 创建单引线标注（对应旧 gb1 命令）
        /// 单根引线从 points[1]（中心点）引出到集中点
        /// </summary>
        public static MLeader AddMleaderOne(this Point3d[] points, double distance, string content)
        {
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            var startP = points.First();
            var endP = points.Last();
            var centerP = MidPoint(startP, endP);

            MLeader ml = new MLeader();

            var vecH = (endP - startP).GetNormal();
            var line = new Line(startP, endP);
            double angle = line.Angle;

            // 调整引线方向（第二三象限时翻转）
            // 注意：vecH 和 centerP 保持原始值不变
            if (angle > Math.PI / 2 && angle <= Math.PI * 3 / 2)
            {
                points = points.Reverse().ToArray();
                line = new Line(points.First(), points.Last());
                // 文字角度翻转 180 度，避免倒置
                angle = line.Angle;
            }

            var vecV = vecH.RotateBy(-Math.PI / 2, Vector3d.ZAxis);
            var centralPoint = centerP + vecV * distance;

            // 单引线：仅从 points[1] 引出
            int leaderIndex = ml.AddLeader();
            int leaderLineIndex = ml.AddLeaderLine(leaderIndex);
            ml.AddFirstVertex(leaderLineIndex, points[1]);
            ml.AddLastVertex(leaderLineIndex, centralPoint);

            ml.MLeaderStyle = db.MLeaderstyle;

            MText mt = new MText();
            mt.TextStyleId = ml.TextStyleId;
            mt.Color = ml.TextColor;
            mt.TextHeight = ml.TextHeight;
            mt.Contents = content;
            mt.Rotation = angle;  // 使用调整后的角度
            ml.MText = mt;

            return ml;
        }

        /// <summary>
        /// 创建六点引线标注（对应旧 gb2 命令）
        /// 六根引线汇集到 points[1] 与 points[4] 中点的水平偏移处，文字水平
        /// </summary>
        public static MLeader AddMleaderSix(this Point3d[] points, double distance, string content)
        {
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            var startP = points.First();
            var endP = points.Last();
            var centerP = MidPoint(points[1], points[4]);

            MLeader ml = new MLeader();

            var vecH = (endP - startP).GetNormal();
            var line = new Line(startP, endP);

            // 调整引线方向（第二三象限时翻转）
            if (line.Angle > Math.PI / 2 && line.Angle <= Math.PI * 3 / 2)
            {
                points = points.Reverse().ToArray();
                line = new Line(points.First(), points.Last());
            }

            // 集中点：中心点水平偏移 distance
            var centralPoint = centerP + Vector3d.XAxis * distance;

            foreach (var point in points)
            {
                int leaderIndex = ml.AddLeader();
                int leaderLineIndex = ml.AddLeaderLine(leaderIndex);
                ml.AddFirstVertex(leaderLineIndex, point);
                ml.AddLastVertex(leaderLineIndex, centralPoint);
            }

            ml.MLeaderStyle = db.MLeaderstyle;

            MText mt = new MText();
            mt.TextStyleId = ml.TextStyleId;
            mt.Color = ml.TextColor;
            mt.TextHeight = ml.TextHeight;
            mt.Contents = content;
            mt.Rotation = 0; // 六点标注文字水平
            ml.MText = mt;

            return ml;
        }

        /// <summary>
        /// 创建单点引线标注（用于圆分组标注）
        /// </summary>
        /// <param name="startPoint">引线起点</param>
        /// <param name="endPoint">引线终点（文字位置）</param>
        /// <param name="content">文字内容</param>
        public static MLeader CreateMLeaderSinglePoint(Point3d startPoint, Point3d endPoint, string content)
        {
            return CreateMLeaderSinglePoint(startPoint, endPoint, content, ObjectId.Null);
        }

        /// <summary>
        /// 单点引线；若 <paramref name="mleaderStyleId"/> 有效则先挂样式再生成 MText，
        /// 否则与无参版相同（使用当前文档多重引线样式）。
        /// </summary>
        public static MLeader CreateMLeaderSinglePoint(Point3d startPoint, Point3d endPoint, string content, ObjectId mleaderStyleId)
        {
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            MLeader ml = new MLeader();

            int leaderIndex = ml.AddLeader();
            int leaderLineIndex = ml.AddLeaderLine(leaderIndex);
            ml.AddFirstVertex(leaderLineIndex, startPoint);
            ml.AddLastVertex(leaderLineIndex, endPoint);

            ml.MLeaderStyle = mleaderStyleId.IsNull ? db.MLeaderstyle : mleaderStyleId;

            MText mt = new MText();
            mt.TextStyleId = ml.TextStyleId;
            mt.Color = ml.TextColor;
            mt.TextHeight = ml.TextHeight;
            mt.Contents = content;
            mt.Rotation = 0;
            ml.MText = mt;
            ApplyTightDoglegForSinglePointMLeader(ml, content);
            return ml;
        }

        /// <summary>
        /// 单点引线时收紧「基线/狗腿」水平段（特性中常显示为基线距离/狗腿长）。
        /// 未设置时 CAD 易用很大的默认，数字左侧会拉出很长白线，需进特性手改 <see cref="MLeader.DoglegLength"/>。
        /// </summary>
        private static void ApplyTightDoglegForSinglePointMLeader(MLeader ml, string content)
        {
            if (ml == null) return;
            double th = ml.TextHeight;
            if (th < 1e-9) th = 0.15;
            int n = 1;
            if (!string.IsNullOrEmpty(content))
            {
                foreach (var line in content.Split(new[] { "\\P" }, StringSplitOptions.None))
                {
                    int L = Math.Min((line ?? string.Empty).Length, 20);
                    n = Math.Max(n, Math.Max(1, L));
                }
            }
            // 与字高、字长成比例并设上界，避免长横线
            double d = Math.Max(th * 0.12, Math.Min(th * 0.52, n * th * 0.11 + th * 0.08));
            try
            {
                ml.SetDoglegLength(0, d);
            }
            catch
            {
                // 老版本/异常时略过
            }
        }

        /// <summary>计算两点中点</summary>
        private static Point3d MidPoint(Point3d p1, Point3d p2)
        {
            return new Point3d((p1.X + p2.X) / 2.0, (p1.Y + p2.Y) / 2.0, (p1.Z + p2.Z) / 2.0);
        }
    }
}
