using System.Collections.Generic;

namespace HyCADTool.Features.SpongeCity.Domain.Models
{
    /// <summary>
    /// 10 类下垫面默认表（对齐 gen_sponge_housing_calc.py items）。
    /// 默认图层名 = 中文名；文字图层 = {Name}-文字；ACI 颜色用于 hyscDL 自动建层。
    /// 规范依据：DB13(J) 8457-2022 表 4.2.1-3；指南 表 4-3；GB 50014 表 4.2.1。
    /// </summary>
    public sealed class SurfaceCatalogEntry
    {
        public string Code { get; }
        public string Name { get; }
        public double DefaultPsi { get; }
        public short AciColor { get; }
        public string Note { get; }

        public SurfaceCatalogEntry(string code, string name, double defaultPsi, short aciColor, string note)
        {
            Code = code;
            Name = name;
            DefaultPsi = defaultPsi;
            AciColor = aciColor;
            Note = note;
        }

        /// <summary>文字图层名约定：{Name}-文字</summary>
        public string TextLayerName => Name + "-文字";
    }

    public static class SurfaceCatalog
    {
        public static readonly IReadOnlyList<SurfaceCatalogEntry> Entries = new[]
        {
            new SurfaceCatalogEntry("S1",  "硬质屋面",         0.90, 1,   "硬质屋面/未铺石子的平屋面"),
            new SurfaceCatalogEntry("S2",  "种植屋面",         0.40, 3,   "DB13 §5.0.5 ≥2 万㎡公建强制"),
            new SurfaceCatalogEntry("S3",  "混凝土路面",       0.90, 7,   "车行道/消防车道"),
            new SurfaceCatalogEntry("S4",  "块石广场",         0.65, 6,   "园路/活动广场"),
            new SurfaceCatalogEntry("S5",  "透水砖",           0.35, 2,   "DB13 §5.0.6 人行 ≥70%"),
            new SurfaceCatalogEntry("S6",  "下沉绿地",         0.15, 92,  "DB13 §5.0.5 下沉 100~200mm"),
            new SurfaceCatalogEntry("S7",  "普通绿地",         0.15, 84,  "草坪/地被"),
            new SurfaceCatalogEntry("S8",  "顶板薄覆土绿地",   0.30, 44,  "覆土<600mm 顶板绿地"),
            new SurfaceCatalogEntry("S9",  "水面",             1.00, 5,   "DB13 ψ=1.0 景观水体"),
            new SurfaceCatalogEntry("S10", "其他",             0.50, 8,   "嵌草砖 / 裸土等"),
        };
    }
}
