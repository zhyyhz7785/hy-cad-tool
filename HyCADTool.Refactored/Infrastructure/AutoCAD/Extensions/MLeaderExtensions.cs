using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Extensions
{
    /// <summary>
    /// MLeader 创建扩展方法（对应旧代码 ZTools.AddMleader）
    /// </summary>
    public static class MLeaderExtensions
    {
        /// <summary>
        /// 创建多引线标注（与旧代码 AddMleader 完全一致）
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
    }
}
