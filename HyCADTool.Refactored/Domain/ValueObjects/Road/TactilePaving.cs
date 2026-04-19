using System;
using System.Collections.Generic;
using System.Linq;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;

namespace HyCADTool.Refactored.Domain.ValueObjects.Road
{
    /// <summary>
    /// 盲道（<see cref="TactilePaving"/>）类型枚举 —— GB 50763-2012 §3.3。
    ///
    /// <list type="bullet">
    /// <item><see cref="Advance"/>：行进盲道（长条纹地砖）—— 指引前进方向，沿人行道纵向连续铺设；
    /// 宽度 0.25~0.50 m（本项目默认 <see cref="TactilePaving.DefaultAdvanceWidth"/> = 0.30 m）。</item>
    /// <item><see cref="Stop"/>：提示盲道（圆点地砖）—— 在坡道前 / 斑马线前 / 障碍物周围<b>提示停止或转向</b>；
    /// 宽度 0.30~0.60 m（本项目默认 <see cref="TactilePaving.DefaultStopWidth"/> = 0.60 m）。</item>
    /// </list>
    /// </summary>
    public enum TactilePavingKind
    {
        Advance = 0,
        Stop = 1,
    }

    /// <summary>
    /// 盲道（TactilePaving）—— 沿人行道布置的条带状地砖路径，为视障人士提供步行导向 / 危险提示。
    ///
    /// <para><b>几何语义</b></para>
    /// <list type="bullet">
    /// <item><see cref="Centerline"/>：条带中心线折线（至少 2 个点，首尾相连直线段）；</item>
    /// <item><see cref="Width"/>：条带宽度（m），垂直于各段切线方向扩展；</item>
    /// <item>Infrastructure 层可按中心线 + 半宽偏移生成闭合多段线 / 矩形序列。</item>
    /// </list>
    ///
    /// <para><b>规范约束（GB 50763 §3.3）</b></para>
    /// <list type="bullet">
    /// <item>行进盲道：沿人行道中心线布置，颜色黄色；与盲人主要行走路径方向一致；</item>
    /// <item>提示盲道：布置于坡道起点 / 终点、斑马线前、楼梯前、台阶前，长度覆盖整个提示区域宽度；</item>
    /// <item>距缘石 / 侧石内边 ≥ 0.25 m；不应铺设在车行道内。</item>
    /// </list>
    ///
    /// <para><b>不可变语义</b></para>
    /// 采用 class（非 struct）持有 <see cref="Centerline"/> 列表；构造后通过 With* 方法派生新实例，
    /// 参与 JSON 持久化（<see cref="Models.Road.Intersection.TactilePavings"/>）。
    /// </summary>
    public sealed class TactilePaving
    {
        /// <summary>行进盲道默认宽度（m）—— GB 50763 §3.3.1 下限偏中。</summary>
        public const double DefaultAdvanceWidth = 0.30;

        /// <summary>提示盲道默认宽度（m）—— GB 50763 §3.3.1 上限。</summary>
        public const double DefaultStopWidth = 0.60;

        /// <summary>距缘石内边最小净距（m）—— GB 50763 §3.3.1。</summary>
        public const double MinDistanceFromKerb = 0.25;

        /// <summary>稳定 Id（跨 Save/Load 保留）。</summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>盲道类型。</summary>
        public TactilePavingKind Kind { get; set; }

        /// <summary>中心线折线顶点（米）；至少 2 个点。</summary>
        public List<Point2D> Centerline { get; set; } = new List<Point2D>();

        /// <summary>条带宽度（m，&gt; 0）。</summary>
        public double Width { get; set; }

        /// <summary>（可选）所属 <c>Intersection.CornerArcs</c> 索引；仅 <see cref="TactilePavingKind.Stop"/> 在坡道前布置时使用；
        /// <see cref="TactilePavingKind.Advance"/> 置 <c>-1</c>。</summary>
        public int CornerArcIndex { get; set; } = -1;

        /// <summary>中心线弧长（m）= 各段首尾点欧氏距离之和。</summary>
        public double Length
        {
            get
            {
                if (Centerline == null || Centerline.Count < 2) return 0;
                double sum = 0;
                for (int i = 1; i < Centerline.Count; i++)
                    sum += Centerline[i - 1].DistanceTo(Centerline[i]);
                return sum;
            }
        }

        /// <summary>顶点数（便于 JSON 回读校验）。</summary>
        public int VertexCount => Centerline?.Count ?? 0;

        /// <summary>几何有效性：至少 2 个不同点 + 宽度 &gt; 0。</summary>
        public bool IsValid
            => Centerline != null
               && Centerline.Count >= 2
               && Width > 0
               && !Centerline.Skip(1).All(p => p.IsEqualTo(Centerline[0], 1e-9));

        /// <summary>便捷构造。</summary>
        public static TactilePaving Create(
            TactilePavingKind kind,
            IEnumerable<Point2D> centerline,
            double? width = null,
            int cornerArcIndex = -1)
        {
            if (centerline == null) throw new ArgumentNullException(nameof(centerline));
            var pts = centerline.ToList();
            if (pts.Count < 2) throw new ArgumentException("Centerline 至少需要 2 个顶点", nameof(centerline));

            double w = width ?? (kind == TactilePavingKind.Advance ? DefaultAdvanceWidth : DefaultStopWidth);
            if (w <= 0) throw new ArgumentOutOfRangeException(nameof(width), w, "Width 必须 > 0");

            return new TactilePaving
            {
                Kind = kind,
                Centerline = pts,
                Width = w,
                CornerArcIndex = cornerArcIndex,
            };
        }

        public override string ToString()
            => $"TactilePaving[{Kind}, W={Width:F2}, V={VertexCount}, L={Length:F2}, arc#{CornerArcIndex}]";
    }
}
