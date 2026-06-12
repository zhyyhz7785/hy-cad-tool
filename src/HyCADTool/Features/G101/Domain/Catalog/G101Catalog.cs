using System;
using System.Collections.Generic;
using HyCADTool.Features.G101.Domain.Geometry;
using HyCADTool.Features.G101.Domain.Tables;

namespace HyCADTool.Features.G101.Domain.Catalog
{
    public enum ParamKind { Number, Integer, Choice, Boolean, ReadOnly }

    public sealed class ParamDescriptor
    {
        public string Key { get; set; }
        public string Label { get; set; }
        public ParamKind Kind { get; set; }
        public double DefaultNumber { get; set; }
        public int DefaultInt { get; set; }
        public bool DefaultBool { get; set; }
        public string Unit { get; set; }
        public string[] Choices { get; set; }
        public string Tooltip { get; set; }
    }

    public sealed class G101CatalogItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public AtlasRef AtlasRef { get; set; }
        public bool IsImplemented { get; set; }
        public List<ParamDescriptor> Parameters { get; } = new List<ParamDescriptor>();
        public Func<G101GlobalSettings, Dictionary<string, object>, DetailSketch> Build { get; set; }
    }

    public sealed class G101CatalogGroup
    {
        public string Name { get; set; }
        public List<G101CatalogItem> Items { get; } = new List<G101CatalogItem>();
    }

    public sealed class G101CatalogAtlas
    {
        public string AtlasId { get; set; }
        public string Name { get; set; }
        public List<G101CatalogGroup> Groups { get; } = new List<G101CatalogGroup>();
    }

    /// <summary>22G101 三图集完整目录树（未实现项 IsImplemented=false）。</summary>
    public static class G101Catalog
    {
        public static IReadOnlyList<G101CatalogAtlas> Build()
        {
            var list = new List<G101CatalogAtlas>
            {
                BuildAtlas101_3(),
                BuildAtlas101_2(),
                BuildAtlas101_1(),
            };
            return list;
        }

        private static G101CatalogAtlas BuildAtlas101_3()
        {
            var atlas = new G101CatalogAtlas { AtlasId = "22G101-3", Name = "独立/条形/筏形/桩基础" };
            var foundation = new G101CatalogGroup { Name = "基础" };

            foundation.Items.Add(CreateDjFoundation());
            foundation.Items.Add(Placeholder("col-dowel", "柱纵向钢筋在基础中构造", "22G101-3", "2-10", true));
            foundation.Items.Add(CreateTjbFoundation());
            foundation.Items.Add(CreateDoubleColumnDj());
            foundation.Items.Add(Placeholder("jl", "基础梁 JL 端部钢筋构造", "22G101-3", "2-25", false));
            foundation.Items.Add(Placeholder("lpb", "梁板式筏形基础 LPB 钢筋", "22G101-3", "2-32", false));
            foundation.Items.Add(Placeholder("bpb", "平板式筏形基础 BPB 钢筋", "22G101-3", "2-35", false));

            atlas.Groups.Add(foundation);
            return atlas;
        }

        private static G101CatalogAtlas BuildAtlas101_2()
        {
            var atlas = new G101CatalogAtlas { AtlasId = "22G101-2", Name = "现浇混凝土板式楼梯" };
            var stair = new G101CatalogGroup { Name = "楼梯" };

            stair.Items.Add(CreateAtStair());
            stair.Items.Add(CreateBtStair());
            stair.Items.Add(Placeholder("ct", "CT 型楼梯板配筋", "22G101-2", "2-12", false));
            stair.Items.Add(Placeholder("ata", "ATa 型滑动支座楼梯", "22G101-2", "2-26", false));
            stair.Items.Add(Placeholder("tz-tl", "梯柱 TZ / 梯梁 TL 配筋", "22G101-2", "2-41", false));

            atlas.Groups.Add(stair);
            return atlas;
        }

        private static G101CatalogAtlas BuildAtlas101_1()
        {
            var atlas = new G101CatalogAtlas { AtlasId = "22G101-1", Name = "框架/剪力墙/梁/板" };
            var col = new G101CatalogGroup { Name = "柱" };
            col.Items.Add(Placeholder("kz-conn", "KZ 纵向钢筋连接", "22G101-1", "2-9", false));
            col.Items.Add(Placeholder("kz-stirrup", "KZ 箍筋加密区", "22G101-1", "2-11", false));
            col.Items.Add(Placeholder("kz-top", "KZ 边柱角柱柱顶纵筋", "22G101-1", "2-14", false));

            var wall = new G101CatalogGroup { Name = "剪力墙" };
            wall.Items.Add(Placeholder("wall-h", "剪力墙水平分布筋", "22G101-1", "2-19", false));
            wall.Items.Add(Placeholder("wall-v", "剪力墙竖向分布筋", "22G101-1", "2-21", false));
            wall.Items.Add(Placeholder("yjz", "约束边缘构件 YBZ", "22G101-1", "2-24", false));
            wall.Items.Add(Placeholder("ll", "连梁 LL 配筋", "22G101-1", "2-27", false));

            var beam = new G101CatalogGroup { Name = "梁" };
            beam.Items.Add(Placeholder("kl", "楼层框架梁 KL 纵筋", "22G101-1", "2-33", false));
            beam.Items.Add(Placeholder("wkl", "屋面框架梁 WKL 纵筋", "22G101-1", "2-34", false));
            beam.Items.Add(Placeholder("xl", "纯悬挑梁 XL 配筋", "22G101-1", "2-43", false));

            var slab = new G101CatalogGroup { Name = "板" };
            slab.Items.Add(Placeholder("slab-end", "板端支座锚固", "22G101-1", "2-50", false));
            slab.Items.Add(Placeholder("slab-hole", "板开洞 BD 加强", "22G101-1", "2-62", false));
            slab.Items.Add(Placeholder("hjd", "后浇带 HJD 钢筋", "22G101-1", "2-59", false));

            atlas.Groups.Add(col);
            atlas.Groups.Add(wall);
            atlas.Groups.Add(beam);
            atlas.Groups.Add(slab);
            return atlas;
        }

        private static G101CatalogItem CreateDjFoundation()
        {
            var item = new G101CatalogItem
            {
                Id = "dj-foundation",
                Name = "独立基础 DJj/DJz 底板配筋",
                AtlasRef = new AtlasRef("22G101-3", "2-11"),
                IsImplemented = true,
                Build = Components.Foundation.DjFoundationBuilder.Build
            };
            item.Parameters.Add(new ParamDescriptor { Key = "lengthA", Label = "底板长度 A", Kind = ParamKind.Integer, DefaultInt = 2400, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "widthB", Label = "底板宽度 B", Kind = ParamKind.Integer, DefaultInt = 2400, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "heightH", Label = "基础高度 H", Kind = ParamKind.Integer, DefaultInt = 600, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "colSize", Label = "柱截面 bc×hc", Kind = ParamKind.Number, DefaultNumber = 600, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "spacingX", Label = "X 向钢筋间距", Kind = ParamKind.Integer, DefaultInt = 200, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "spacingY", Label = "Y 向钢筋间距", Kind = ParamKind.Integer, DefaultInt = 200, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "drawPlan", Label = "绘制平面图", Kind = ParamKind.Boolean, DefaultBool = true });
            item.Parameters.Add(new ParamDescriptor { Key = "drawSection", Label = "绘制剖面图", Kind = ParamKind.Boolean, DefaultBool = true });
            return item;
        }

        private static G101CatalogItem CreateAtStair()
        {
            var item = new G101CatalogItem
            {
                Id = "stair-at",
                Name = "AT 型楼梯板配筋",
                AtlasRef = new AtlasRef("22G101-2", "2-10"),
                IsImplemented = true,
                Build = Components.Stair.AtStairBuilder.Build
            };
            item.Parameters.Add(new ParamDescriptor { Key = "stepCount", Label = "踏步级数 n", Kind = ParamKind.Integer, DefaultInt = 12 });
            item.Parameters.Add(new ParamDescriptor { Key = "riseH", Label = "踏步高度 h", Kind = ParamKind.Integer, DefaultInt = 150, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "runB", Label = "踏步宽度 b", Kind = ParamKind.Integer, DefaultInt = 280, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "slabT", Label = "梯板厚度 t", Kind = ParamKind.Integer, DefaultInt = 120, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "ln", Label = "净跨 ln", Kind = ParamKind.Integer, DefaultInt = 3360, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "distSpacing", Label = "分布筋间距", Kind = ParamKind.Integer, DefaultInt = 250, Unit = "mm" });
            return item;
        }

        private static G101CatalogItem CreateTjbFoundation()
        {
            var item = new G101CatalogItem
            {
                Id = "tjb",
                Name = "条形基础底板 TJBj/TJBp 配筋",
                AtlasRef = new AtlasRef("22G101-3", "2-20"),
                IsImplemented = true,
                Build = Components.Foundation.TjbFoundationBuilder.Build
            };
            item.Parameters.Add(new ParamDescriptor { Key = "widthB", Label = "底板宽度 b", Kind = ParamKind.Integer, DefaultInt = 2000, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "heightH1", Label = "截面高度 h1", Kind = ParamKind.Integer, DefaultInt = 300, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "heightH2", Label = "坡形高度 h2", Kind = ParamKind.Integer, DefaultInt = 250, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "isSlope", Label = "坡形截面 TJBp", Kind = ParamKind.Boolean, DefaultBool = true });
            item.Parameters.Add(new ParamDescriptor { Key = "segmentLen", Label = "平面段长度 L", Kind = ParamKind.Integer, DefaultInt = 6000, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "mainSpacing", Label = "B 向受力筋间距", Kind = ParamKind.Integer, DefaultInt = 150, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "distSpacing", Label = "分布筋间距", Kind = ParamKind.Integer, DefaultInt = 250, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "beamWidth", Label = "基础梁宽", Kind = ParamKind.Integer, DefaultInt = 400, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "drawPlan", Label = "绘制平面段", Kind = ParamKind.Boolean, DefaultBool = true });
            item.Parameters.Add(new ParamDescriptor { Key = "drawSection", Label = "绘制横剖面", Kind = ParamKind.Boolean, DefaultBool = true });
            return item;
        }

        private static G101CatalogItem CreateDoubleColumnDj()
        {
            var item = new G101CatalogItem
            {
                Id = "dj-double",
                Name = "双柱普通独立基础配筋",
                AtlasRef = new AtlasRef("22G101-3", "2-12"),
                IsImplemented = true,
                Build = Components.Foundation.DoubleColumnDjBuilder.Build
            };
            item.Parameters.Add(new ParamDescriptor { Key = "lengthA", Label = "底板长度 A", Kind = ParamKind.Integer, DefaultInt = 3600, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "widthB", Label = "底板宽度 B", Kind = ParamKind.Integer, DefaultInt = 2400, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "heightH", Label = "基础高度 H", Kind = ParamKind.Integer, DefaultInt = 600, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "colWidth", Label = "柱宽 bc", Kind = ParamKind.Integer, DefaultInt = 600, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "colSpacing", Label = "柱距", Kind = ParamKind.Integer, DefaultInt = 2400, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "bottomSpacing", Label = "底筋间距", Kind = ParamKind.Integer, DefaultInt = 200, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "topBarCount", Label = "顶部 T 筋根数", Kind = ParamKind.Integer, DefaultInt = 11 });
            item.Parameters.Add(new ParamDescriptor { Key = "topSpacing", Label = "顶部 T 筋间距", Kind = ParamKind.Integer, DefaultInt = 100, Unit = "mm" });
            return item;
        }

        private static G101CatalogItem CreateBtStair()
        {
            var item = new G101CatalogItem
            {
                Id = "stair-bt",
                Name = "BT 型楼梯板配筋",
                AtlasRef = new AtlasRef("22G101-2", "2-10"),
                IsImplemented = true,
                Build = Components.Stair.BtStairBuilder.Build
            };
            item.Parameters.Add(new ParamDescriptor { Key = "lowPlateLen", Label = "低端平板长", Kind = ParamKind.Integer, DefaultInt = 900, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "stepCount", Label = "踏步级数 n", Kind = ParamKind.Integer, DefaultInt = 10 });
            item.Parameters.Add(new ParamDescriptor { Key = "riseH", Label = "踏步高度 h", Kind = ParamKind.Integer, DefaultInt = 150, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "runB", Label = "踏步宽度 b", Kind = ParamKind.Integer, DefaultInt = 280, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "slabT", Label = "梯板厚度 t", Kind = ParamKind.Integer, DefaultInt = 120, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "ln", Label = "净跨 ln", Kind = ParamKind.Integer, DefaultInt = 3700, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "distSpacing", Label = "分布筋间距", Kind = ParamKind.Integer, DefaultInt = 250, Unit = "mm" });
            return item;
        }

        private static G101CatalogItem Placeholder(string id, string name, string atlas, string page, bool implemented)
        {
            var item = new G101CatalogItem
            {
                Id = id,
                Name = name,
                AtlasRef = new AtlasRef(atlas, page),
                IsImplemented = implemented
            };
            if (id == "col-dowel")
            {
                item.Build = Components.Foundation.ColumnDowelBuilder.Build;
                item.Parameters.Add(new ParamDescriptor { Key = "foundationH", Label = "基础高度 H", Kind = ParamKind.Integer, DefaultInt = 500, Unit = "mm" });
                item.Parameters.Add(new ParamDescriptor { Key = "colWidth", Label = "柱宽 bc", Kind = ParamKind.Integer, DefaultInt = 600, Unit = "mm" });
                item.Parameters.Add(new ParamDescriptor { Key = "barCount", Label = "纵筋根数", Kind = ParamKind.Integer, DefaultInt = 8 });
                item.Parameters.Add(new ParamDescriptor { Key = "barSpacing", Label = "纵筋间距", Kind = ParamKind.Integer, DefaultInt = 150, Unit = "mm" });
            }
            return item;
        }
    }
}
