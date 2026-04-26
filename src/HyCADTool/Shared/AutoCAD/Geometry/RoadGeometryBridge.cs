using System;
using System.Collections.Generic;
using AcadGeom = Autodesk.AutoCAD.Geometry;
using AcadDb = Autodesk.AutoCAD.DatabaseServices;
using DomainGeom = HyCADTool.Shared.Geometry;
using DomainRoad = HyCADTool.Domain.Models.Road;

namespace HyCADTool.Shared.AutoCAD.Geometry
{
    /// <summary>
    /// AutoCAD 几何类型 ↔ Domain 几何值对象的双向桥接。
    ///
    /// Domain 层不引用 <c>Autodesk.AutoCAD.*</c>；所有跨界转换集中在本类：
    /// - Point3d ↔ Point3D
    /// - Vector3d ↔ Vector3D
    /// - Polyline / Polyline3d → Polyline3D（反向转换在 v2 真实绘图时再按需实现）
    /// - Arc → ArcData
    /// - Infrastructure <c>CrosswalkService.RoadArm</c> → Domain <see cref="DomainRoad.RoadArm"/>
    ///
    /// P0 阶段仅提供单向（AutoCAD → Domain）和最基础的反向（Point）转换，
    /// 满足事件发布 + JSON 持久化链路，不涉及复杂反序列化。
    /// </summary>
    public static class RoadGeometryBridge
    {
        // ===== Point =====

        public static DomainGeom.Point3D ToDomain(AcadGeom.Point3d p)
            => new DomainGeom.Point3D(p.X, p.Y, p.Z);

        public static AcadGeom.Point3d ToAutoCad(DomainGeom.Point3D p)
            => new AcadGeom.Point3d(p.X, p.Y, p.Z);

        // ===== Vector =====

        public static DomainGeom.Vector3D ToDomain(AcadGeom.Vector3d v)
            => new DomainGeom.Vector3D(v.X, v.Y, v.Z);

        public static AcadGeom.Vector3d ToAutoCad(DomainGeom.Vector3D v)
            => new AcadGeom.Vector3d(v.X, v.Y, v.Z);

        // ===== Polyline =====

        /// <summary>
        /// 根据 Domain <see cref="DomainGeom.Polyline3D"/> 构造一个 AutoCAD 2D <c>Polyline</c>（未加入数据库）。
        /// - XY 取 <see cref="DomainGeom.Point3D.X"/>/<see cref="DomainGeom.Point3D.Y"/>；
        /// - Z 统一取第一个顶点的 Z 作为 <c>Elevation</c>（v1 假设单一高程，P3 引入分段标高）；
        /// - 每顶点携带 <see cref="DomainGeom.Polyline3D.Bulges"/>，支持 AutoCAD 原生弧段（tan(θ/4)）；
        /// - 调用方负责 <c>BlockTableRecord.AppendEntity</c> 与 Xdata 挂载。
        /// </summary>
        public static AcadDb.Polyline ToAutoCadPolyline(DomainGeom.Polyline3D src)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));

            var polyline = new AcadDb.Polyline();
            double elevation = src.VertexCount > 0 ? src.GetPointAt(0).Z : 0;
            polyline.Elevation = elevation;

            for (int i = 0; i < src.VertexCount; i++)
            {
                var p = src.GetPointAt(i);
                double bulge = src.GetBulgeAt(i);
                polyline.AddVertexAt(i, new AcadGeom.Point2d(p.X, p.Y), bulge, 0, 0);
            }
            polyline.Closed = src.IsClosed;
            return polyline;
        }

        /// <summary>
        /// 就地把 AutoCAD 2D <c>Polyline</c> 的几何替换为 Domain <see cref="DomainGeom.Polyline3D"/>。
        /// 保留 ObjectId / 图层 / Xdata / 用户自定义属性，只替换几何数据（顶点 + bulge + Elevation + Closed）。
        ///
        /// 使用前提：调用方已将 <paramref name="target"/> 以 <c>ForWrite</c> 打开（或通过 <c>UpgradeOpen</c>），
        /// 并持有一个未 Commit 的 Transaction。
        ///
        /// 场景：<c>hyRoadLoad</c> 从 JSON 反向对齐 DWG 时，已经挂载 HY_ROAD/ID 的 polyline 需要更新形状
        /// 但不能重建（否则 ObjectId 变化会击穿选择集、Xdata 以外的用户数据也会丢失）。
        /// </summary>
        public static void UpdateAutoCadPolyline(AcadDb.Polyline target, DomainGeom.Polyline3D src)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (src == null) throw new ArgumentNullException(nameof(src));

            int newN = src.VertexCount;
            if (newN == 0)
            {
                throw new ArgumentException(
                    "Cannot update an AutoCAD Polyline from an empty Polyline3D (at least one vertex is required).",
                    nameof(src));
            }

            double elevation = src.GetPointAt(0).Z;
            target.Elevation = elevation;

            int oldN = target.NumberOfVertices;
            while (oldN > newN)
            {
                target.RemoveVertexAt(oldN - 1);
                oldN--;
            }

            while (oldN < newN)
            {
                var p = src.GetPointAt(oldN);
                double bulge = src.GetBulgeAt(oldN);
                target.AddVertexAt(oldN, new AcadGeom.Point2d(p.X, p.Y), bulge, 0, 0);
                oldN++;
            }

            for (int i = 0; i < newN; i++)
            {
                var p = src.GetPointAt(i);
                double bulge = src.GetBulgeAt(i);
                target.SetPointAt(i, new AcadGeom.Point2d(p.X, p.Y));
                target.SetBulgeAt(i, bulge);
            }

            target.Closed = src.IsClosed;
        }

        /// <summary>
        /// 从 AutoCAD 2D <c>Polyline</c> 提取顶点序列 + bulge 数组，生成 Domain <see cref="DomainGeom.Polyline3D"/>（Z=Elevation）。
        /// <c>polyline.GetBulgeAt(i)</c> 的 AutoCAD 语义 = 顶点 i 到下一顶点那段的凸度；域模型沿用同一约定。
        /// </summary>
        public static DomainGeom.Polyline3D ToDomain(AcadDb.Polyline polyline)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            int n = polyline.NumberOfVertices;
            var verts = new List<DomainGeom.Point3D>(n);
            var bulges = new List<double>(n);
            for (int i = 0; i < n; i++)
            {
                var p2 = polyline.GetPoint2dAt(i);
                verts.Add(new DomainGeom.Point3D(p2.X, p2.Y, polyline.Elevation));
                bulges.Add(polyline.GetBulgeAt(i));
            }
            return new DomainGeom.Polyline3D(verts, polyline.Closed, bulges);
        }

        /// <summary>
        /// 从 AutoCAD 3D <c>Polyline3d</c> 提取顶点序列。
        /// 注意：Polyline3d 顶点访问需要在调用方持有事务时通过 vertexId 读取；
        /// 本重载接受预收集好的 <see cref="AcadGeom.Point3d"/> 序列，以避免本桥接类与 Transaction 耦合。
        /// </summary>
        public static DomainGeom.Polyline3D ToDomain(IEnumerable<AcadGeom.Point3d> points, bool isClosed)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));

            var verts = new List<DomainGeom.Point3D>();
            foreach (var p in points) verts.Add(ToDomain(p));
            return new DomainGeom.Polyline3D(verts, isClosed);
        }

        // ===== Arc =====

        /// <summary>
        /// AutoCAD <c>Arc</c> → Domain <see cref="DomainRoad.ArcData"/>。
        /// </summary>
        public static DomainRoad.ArcData ToDomain(AcadDb.Arc arc)
        {
            if (arc == null) throw new ArgumentNullException(nameof(arc));

            return new DomainRoad.ArcData
            {
                Center = ToDomain(arc.Center),
                Radius = arc.Radius,
                StartAngle = arc.StartAngle,
                EndAngle = arc.EndAngle
            };
        }
    }
}
