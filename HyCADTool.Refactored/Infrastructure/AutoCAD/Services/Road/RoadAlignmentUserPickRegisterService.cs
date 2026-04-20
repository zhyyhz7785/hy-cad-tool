using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Domain.Events.Road;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Geometry;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road
{
    /// <summary>
    /// 「用户拾取登记」服务：把任意图层上的 AutoCAD Polyline / Polyline2d / Polyline3d 顶点
    /// 转换为 <see cref="PiElement"/> 意义下的 PI 表（R / Ls 均为 0），登记为一条
    /// <see cref="AlignmentSourceKind.UserPicked"/> 来源的 <see cref="Alignment"/>。
    ///
    /// 关键约束（与 Commit 分工）：
    /// - <b>不</b>写原 Polyline 的 HY_ROAD Xdata；
    /// - <b>不</b>迁移原 Polyline 所在图层（用户明确要求"无论选择什么图层"）。
    ///
    /// 提交阶段（<see cref="RoadAlignmentCommitService"/>）才会在 <c>05_hy_道路_平面线位</c>
    /// 新建正式 HY_ROAD Polyline，对应完整 Xdata。
    /// </summary>
    public sealed class RoadAlignmentUserPickRegisterService
    {
        private readonly RoadDesignRegistry _registry;
        private readonly IRoadEventBus _eventBus;

        public RoadAlignmentUserPickRegisterService(RoadDesignRegistry registry, IRoadEventBus eventBus)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        /// <summary>
        /// 读取拾取的 Polyline 顶点，登记为一条 UserPicked 源的 <see cref="Alignment"/>，并广播 Created 事件。
        /// 调用方需自行开 <paramref name="transaction"/> 并在结束后 <c>Commit</c>；本方法不写入 DWG 实体。
        /// </summary>
        /// <returns>新登记的 Alignment；如读取失败（不是 Polyline 类 / 顶点不足）返回 <c>null</c>。</returns>
        public Alignment RegisterFromPickedEntity(
            string documentName,
            Transaction transaction,
            Database database,
            ObjectId pickedId,
            string displayName = null)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (pickedId.IsNull) return null;

            var ent = transaction.GetObject(pickedId, OpenMode.ForRead);
            if (ent == null) return null;

            var centerline = TryReadCenterline(ent, transaction);
            if (centerline == null || centerline.VertexCount < 2) return null;

            // 直接按顶点转 PI 表；bulge 弧段走 TryCreatePiTableFromBulgeCenterline。
            AlignmentSource source;
            if (centerline.HasArcs)
            {
                source = AlignmentSource.TryCreatePiTableFromBulgeCenterline(centerline)
                         ?? AlignmentSource.TryCreatePiTableFromStraightCenterline(centerline);
            }
            else
            {
                source = AlignmentSource.TryCreatePiTableFromStraightCenterline(centerline);
            }

            if (source == null)
            {
                // 顶点少于 2 或含环状弧段等极端情况：回退为"纯端点 PI"，保证 Alignment 仍可用。
                var pts = new List<AlignmentPiInput>();
                for (int i = 0; i < centerline.VertexCount; i++)
                {
                    var p = centerline.GetPointAt(i);
                    pts.Add(new AlignmentPiInput
                    {
                        P = new HyCADTool.Refactored.Domain.ValueObjects.Geometry.Point2D(p.X, p.Y),
                        Radius = 0,
                        SpiralIn = 0,
                        SpiralOut = 0,
                        Tag = null,
                    });
                }
                if (pts.Count < 2) return null;
                source = new AlignmentSource { PiElements = pts };
            }

            // UserPicked 来源：区别于 PiTable 的已编辑态。
            source.Kind = AlignmentSourceKind.UserPicked;

            var design = _registry.GetOrCreate(documentName);
            var alignment = new Alignment
            {
                Name = string.IsNullOrWhiteSpace(displayName)
                    ? $"UserPick {design.Alignments.Count + 1}"
                    : displayName,
                Centerline = centerline,
                Source = source,
            };
            design.Alignments.Add(alignment);
            design.LastModifiedUtc = DateTime.UtcNow;

            _eventBus.Publish(new AlignmentChangedEvent(design.Id, alignment.Id, RoadChangeKind.Created));
            return alignment;
        }

        /// <summary>
        /// 从 <see cref="Polyline"/> / <see cref="Polyline2d"/> / <see cref="Polyline3d"/> 读取顶点序列。
        /// 非 Polyline 类型返回 null；Polyline2d/3d 需事务内逐顶点打开。
        /// </summary>
        private static Polyline3D TryReadCenterline(DBObject ent, Transaction tr)
        {
            if (ent is Polyline pl)
                return RoadGeometryBridge.ToDomain(pl);

            if (ent is Polyline2d p2d)
            {
                var pts = new List<Point3d>();
                foreach (ObjectId vid in p2d)
                {
                    var v = tr.GetObject(vid, OpenMode.ForRead) as Vertex2d;
                    if (v == null) continue;
                    pts.Add(v.Position);
                }
                if (pts.Count < 2) return null;
                return RoadGeometryBridge.ToDomain(pts, p2d.Closed);
            }

            if (ent is Polyline3d p3d)
            {
                var pts = new List<Point3d>();
                foreach (ObjectId vid in p3d)
                {
                    var v = tr.GetObject(vid, OpenMode.ForRead) as PolylineVertex3d;
                    if (v == null) continue;
                    pts.Add(v.Position);
                }
                if (pts.Count < 2) return null;
                return RoadGeometryBridge.ToDomain(pts, p3d.Closed);
            }

            return null;
        }
    }
}
