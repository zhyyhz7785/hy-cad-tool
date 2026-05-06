using System.IO;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Features.DataExchange.Hyob.Domain.Codec;

namespace HyCADTool.Features.DataExchange.Hyob.Infrastructure.AutoCadMirror
{
    /// <summary>
    /// AutoCAD Entity 的最小快照：handle + rxClass + layer + 包围盒。
    /// M1-D 把它编码到 <c>HyobOpaqueObject.RawDxf</c> 字段，作为 hyob 端"对象身份指纹"。
    ///
    /// 完美字节级 DXF binary round-trip 需要 AutoCAD DwgFiler / DxfFiler，复杂度极高，
    /// 留到 M1-E / M5 落地。当前阶段 hyob 只保证：
    ///   - handle 不丢（每个 OpaqueObject 锁一个原 DWG handle）
    ///   - 类型不丢（rxClass 名）
    ///   - 图层不丢
    ///   - 包围盒不丢（如果有 GeometricExtents）
    /// 这些足够让 hyob 记录 DWG 演进、做 AI 分析、和未来升级到真正 round-trip 的迁移路径。
    ///
    /// 二进制 schema v1（嵌在 OpaqueObject.RawDxf 内部）：
    /// <code>
    ///   [ utf8   rx_class_name ]
    ///   [ utf8   layer_name ]
    ///   [ byte   has_bounds  (0/1) ]
    ///   if has_bounds:
    ///     [ double min_x, min_y, min_z, max_x, max_y, max_z ]
    /// </code>
    /// </summary>
    internal sealed class EntitySnapshot
    {
        public string Handle { get; }
        public string RxClassName { get; }
        public string LayerName { get; }
        public bool HasBounds { get; }
        public double MinX { get; }
        public double MinY { get; }
        public double MinZ { get; }
        public double MaxX { get; }
        public double MaxY { get; }
        public double MaxZ { get; }

        public EntitySnapshot(string handle, string rxClassName, string layerName,
                              bool hasBounds = false,
                              double minX = 0, double minY = 0, double minZ = 0,
                              double maxX = 0, double maxY = 0, double maxZ = 0)
        {
            Handle = handle ?? string.Empty;
            RxClassName = rxClassName ?? string.Empty;
            LayerName = layerName ?? string.Empty;
            HasBounds = hasBounds;
            MinX = minX; MinY = minY; MinZ = minZ;
            MaxX = maxX; MaxY = maxY; MaxZ = maxZ;
        }

        /// <summary>从 AutoCAD Entity 读取快照（事务上下文中调用）。</summary>
        public static EntitySnapshot FromEntity(Entity ent)
        {
            string handle = ent.Handle.Value.ToString("X");
            string rx = ent.GetRXClass()?.Name ?? "";
            string layer = ent.Layer ?? "";

            bool has = false;
            double minX = 0, minY = 0, minZ = 0, maxX = 0, maxY = 0, maxZ = 0;
            try
            {
                var ext = ent.GeometricExtents;
                has = true;
                minX = ext.MinPoint.X; minY = ext.MinPoint.Y; minZ = ext.MinPoint.Z;
                maxX = ext.MaxPoint.X; maxY = ext.MaxPoint.Y; maxZ = ext.MaxPoint.Z;
            }
            catch
            {
                // 部分对象（空 BlockReference / 未渲染对象）取 GeometricExtents 抛 eNullExtents
                // 视为无包围盒
            }

            return new EntitySnapshot(handle, rx, layer, has, minX, minY, minZ, maxX, maxY, maxZ);
        }

        public byte[] EncodeRawDxf()
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                HyobBinary.WriteUtf8(w, RxClassName);
                HyobBinary.WriteUtf8(w, LayerName);
                w.Write((byte)(HasBounds ? 1 : 0));
                if (HasBounds)
                {
                    w.Write(MinX); w.Write(MinY); w.Write(MinZ);
                    w.Write(MaxX); w.Write(MaxY); w.Write(MaxZ);
                }
                w.Flush();
                return ms.ToArray();
            }
        }
    }
}
