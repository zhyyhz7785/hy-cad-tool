using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Domain.ValueObjects.Road;
using Xunit;

namespace HyCADTool.Refactored.Tests.Domain.Services.Road
{
    /// <summary>
    /// v1.2 —— <see cref="CurbRampDesigner"/> 的 <b>物理方向</b>断言（非"实现等价"断言）。
    ///
    /// <para>修复 <see cref="IntersectionDesigner.TryBuildCornerArc"/> 的镜像 bug 后，CornerArc 正确凸向人行道外侧，
    /// 圆心（<see cref="CornerArc.Center"/>）落在交叉口内部。此时原实现 <c>OutwardNormal = FrontCenter → Center</c>
    /// 把坡道 <b>上口</b> 指向了交叉口内部（车道方向），而不是人行道 —— 属于跨服务语义错位。
    /// 本文件写成 <b>物理</b> 断言（上口远离交叉口中心、人行道侧，在外边线延长线之外），
    /// 而不是假绿的"等价于现在实现的"断言。</para>
    /// </summary>
    public class CurbRampDesignerDirectionTests
    {
        private static Alignment MakeStraight(Point2D start, Point2D end, string name = null)
        {
            var cl = new Polyline3D(
                new[] { new Point3D(start.X, start.Y, 0), new Point3D(end.X, end.Y, 0) },
                isClosed: false,
                bulges: new[] { 0.0, 0.0 });
            return new Alignment
            {
                Name = name ?? $"{start}->{end}",
                StartStation = 0,
                Centerline = cl,
            };
        }

        private static Intersection MakeCross(double r = 8, double hw = 7.5)
        {
            var als = new List<Alignment>
            {
                MakeStraight(new Point2D(-100, 0), new Point2D(0, 0), "W"),
                MakeStraight(new Point2D(100, 0), new Point2D(0, 0), "E"),
                MakeStraight(new Point2D(0, 100), new Point2D(0, 0), "N"),
                MakeStraight(new Point2D(0, -100), new Point2D(0, 0), "S"),
            };
            return IntersectionDesigner.ComputeFromAlignments(als, new Point2D(0, 0), r, hw);
        }

        [Fact]
        public void BackCenter_Is_Further_From_IntersectionCenter_Than_FrontCenter()
        {
            // 人行道在路缘外侧（远离交叉口中心）；BackCenter（上口）应 <b>远离</b> 交叉口中心
            // 而不是靠近它。以交叉口中心 (0, 0) 为参考。
            var ix = MakeCross();
            CurbRampDesigner.LayoutRampsOnCornerArcs(ix);

            var center = new Point2D(0, 0);
            foreach (var ramp in ix.CurbRamps)
            {
                double dFront = ramp.FrontCenter.DistanceTo(center);
                double dBack = ramp.BackCenter.DistanceTo(center);
                dBack.Should().BeGreaterThan(dFront,
                    $"Ramp arc#{ramp.CornerArcIndex}：上口（BackCenter）应比前沿（FrontCenter）更远离交叉口中心。"
                    + $" front={ramp.FrontCenter}, back={ramp.BackCenter}, centerRef={center}");
            }
        }

        [Fact]
        public void OutwardNormal_Points_Away_From_IntersectionCenter()
        {
            // OutwardNormal 的物理定义 = 指向人行道。对外凸 CornerArc（圆心在交叉口内），
            // 人行道在弧外侧（远离圆心）。
            var ix = MakeCross();
            CurbRampDesigner.LayoutRampsOnCornerArcs(ix);

            foreach (var ramp in ix.CurbRamps)
            {
                var arc = ix.CornerArcs[ramp.CornerArcIndex];
                var fromCenterToFront = arc.Center.VectorTo(ramp.FrontCenter);
                double dot = ramp.OutwardNormal.X * fromCenterToFront.X
                           + ramp.OutwardNormal.Y * fromCenterToFront.Y;
                dot.Should().BeGreaterThan(0,
                    "OutwardNormal 应与 Center→FrontCenter 同向（指向人行道，远离圆心），"
                    + "不应等于 FrontCenter→Center（那样上口会走到车道里）");
            }
        }
    }
}
