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

            // 调整引线方向（第二三象限时翻转）
            // 注意：vecH 和 endP 保持原始值不变，centralPoint 基于原始方向计算
            if (line.Angle > Math.PI / 2 && line.Angle <= Math.PI * 3 / 2)
            {
                points = points.Reverse().ToArray();
                line = new Line(points.First(), points.Last());
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
            mt.Rotation = line.Angle;
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

            // 调整引线方向（第二三象限时翻转）
            // 注意：vecH 和 centerP 保持原始值不变
            if (line.Angle > Math.PI / 2 && line.Angle <= Math.PI * 3 / 2)
            {
                points = points.Reverse().ToArray();
                line = new Line(points.First(), points.Last());
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
            mt.Rotation = line.Angle;
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

        /// <summary>计算两点中点</summary>
        private static Point3d MidPoint(Point3d p1, Point3d p2)
        {
            return new Point3d((p1.X + p2.X) / 2.0, (p1.Y + p2.Y) / 2.0, (p1.Z + p2.Z) / 2.0);
        }
    }
}
