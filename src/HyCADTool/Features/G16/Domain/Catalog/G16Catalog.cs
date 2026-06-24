using System;
using System.Collections.Generic;
using HyCADTool.Features.G101.Domain.Catalog;
using HyCADTool.Features.G101.Domain.Geometry;
using HyCADTool.Features.G101.Domain.Tables;
using HyCADTool.Features.G16.Domain.Components.Column;
using HyCADTool.Features.G16.Domain.Components.Wall;
using HyCADTool.Features.G16.Domain.Tables;

namespace HyCADTool.Features.G16.Domain.Catalog
{
    public sealed class G16CatalogItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public AtlasRef AtlasRef { get; set; }
        public bool IsImplemented { get; set; }
        public List<ParamDescriptor> Parameters { get; } = new List<ParamDescriptor>();
        public Func<G16GlobalSettings, Dictionary<string, object>, DetailSketch> Build { get; set; }
    }

    public sealed class G16CatalogGroup
    {
        public string Name { get; set; }
        public List<G16CatalogItem> Items { get; } = new List<G16CatalogItem>();
    }

    public sealed class G16CatalogAtlas
    {
        public string AtlasId { get; set; }
        public string Name { get; set; }
        public List<G16CatalogGroup> Groups { get; } = new List<G16CatalogGroup>();
    }

    /// <summary>16G101 三图集完整目录树（未实现项 IsImplemented=false）。</summary>
    public static class G16Catalog
    {
        public static IReadOnlyList<G16CatalogAtlas> Build()
        {
            return new List<G16CatalogAtlas>
            {
                BuildAtlas101_1(),
                BuildAtlas101_2(),
                BuildAtlas101_3(),
            };
        }

        private static G16CatalogAtlas BuildAtlas101_1()
        {
            var atlas = new G16CatalogAtlas { AtlasId = "16G101-1", Name = "框架/剪力墙/梁/板" };

            var col = new G16CatalogGroup { Name = "柱" };
            col.Items.Add(CreateKzStirrup());
            col.Items.Add(CreateKzTopBar());
            col.Items.Add(Placeholder("kz-transfer", "框支柱构造", "16G101-1", "96", false));

            var wall = new G16CatalogGroup { Name = "剪力墙" };
            wall.Items.Add(Placeholder("wall-h", "剪力墙水平分布筋", "16G101-1", "71", false));
            wall.Items.Add(Placeholder("wall-v", "剪力墙竖向分布筋", "16G101-1", "73", false));
            wall.Items.Add(CreateYbz());
            wall.Items.Add(CreateLl());

            var beam = new G16CatalogGroup { Name = "梁" };
            beam.Items.Add(Placeholder("kl", "楼层框架梁 KL 纵筋", "16G101-1", "84", false));
            beam.Items.Add(Placeholder("wkl", "屋面框架梁 WKL 纵筋", "16G101-1", "85", false));
            beam.Items.Add(Placeholder("xl", "纯悬挑梁 XL 配筋", "16G101-1", "92", false));
            beam.Items.Add(Placeholder("well-beam", "井字/十字梁", "16G101-1", "98", false));

            var slab = new G16CatalogGroup { Name = "板" };
            slab.Items.Add(Placeholder("slab-end", "板端支座锚固", "16G101-1", "93", false));
            slab.Items.Add(Placeholder("slab-hole", "板开洞 BD 加强", "16G101-1", "99", false));
            slab.Items.Add(Placeholder("hjd", "后浇带 HJD 钢筋", "16G101-1", "101", false));

            atlas.Groups.Add(col);
            atlas.Groups.Add(wall);
            atlas.Groups.Add(beam);
            atlas.Groups.Add(slab);
            return atlas;
        }

        private static G16CatalogAtlas BuildAtlas101_2()
        {
            var atlas = new G16CatalogAtlas { AtlasId = "16G101-2", Name = "现浇混凝土板式楼梯" };
            var stair = new G16CatalogGroup { Name = "楼梯" };
            stair.Items.Add(Placeholder("stair-at", "AT 型楼梯板配筋", "16G101-2", "10", false));
            stair.Items.Add(Placeholder("stair-bt", "BT 型楼梯板配筋", "16G101-2", "10", false));
            stair.Items.Add(Placeholder("stair-ct", "CT 型楼梯板配筋", "16G101-2", "12", false));
            stair.Items.Add(Placeholder("tz-tl", "梯柱 TZ / 梯梁 TL", "16G101-2", "41", false));
            atlas.Groups.Add(stair);
            return atlas;
        }

        private static G16CatalogAtlas BuildAtlas101_3()
        {
            var atlas = new G16CatalogAtlas { AtlasId = "16G101-3", Name = "独立/条形/筏形/桩基础" };
            var foundation = new G16CatalogGroup { Name = "基础" };
            foundation.Items.Add(Placeholder("dj", "独立基础 DJ 配筋", "16G101-3", "11", false));
            foundation.Items.Add(Placeholder("col-dowel", "柱纵向钢筋在基础中构造", "16G101-3", "10", false));
            foundation.Items.Add(Placeholder("tjb", "条形基础 TJB 配筋", "16G101-3", "20", false));
            foundation.Items.Add(Placeholder("raft", "筏形基础配筋", "16G101-3", "32", false));
            atlas.Groups.Add(foundation);
            return atlas;
        }

        private static G16CatalogItem CreateKzStirrup()
        {
            var item = new G16CatalogItem
            {
                Id = "kz-stirrup",
                Name = "KZ 箍筋加密区",
                AtlasRef = new AtlasRef("16G101-1", "62", "第62~66页"),
                IsImplemented = true,
                Build = KzStirrupZoneBuilder.Build
            };
            item.Parameters.Add(new ParamDescriptor { Key = "colWidth", Label = "柱宽 bc", Kind = ParamKind.Integer, DefaultInt = 600, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "colDepth", Label = "柱高 hc", Kind = ParamKind.Integer, DefaultInt = 600, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "encryptHeight", Label = "加密区高度 Hn", Kind = ParamKind.Integer, DefaultInt = 500, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "spacingEncrypt", Label = "加密区箍筋间距", Kind = ParamKind.Integer, DefaultInt = 100, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "spacingNormal", Label = "非加密区间距", Kind = ParamKind.Integer, DefaultInt = 150, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "legCount", Label = "纵筋根数", Kind = ParamKind.Integer, DefaultInt = 4 });
            return item;
        }

        private static G16CatalogItem CreateKzTopBar()
        {
            var item = new G16CatalogItem
            {
                Id = "kz-top",
                Name = "KZ 边柱/角柱柱顶纵筋",
                AtlasRef = new AtlasRef("16G101-1", "66", "第66~67页"),
                IsImplemented = true,
                Build = KzTopBarBuilder.Build
            };
            item.Parameters.Add(new ParamDescriptor { Key = "colWidth", Label = "柱宽 bc", Kind = ParamKind.Integer, DefaultInt = 600, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "colDepth", Label = "柱高 hc", Kind = ParamKind.Integer, DefaultInt = 600, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "barCount", Label = "纵筋根数", Kind = ParamKind.Integer, DefaultInt = 8 });
            item.Parameters.Add(new ParamDescriptor { Key = "barSpacing", Label = "纵筋间距", Kind = ParamKind.Integer, DefaultInt = 150, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "extendLen", Label = "柱顶伸出长度", Kind = ParamKind.Integer, DefaultInt = 1200, Unit = "mm" });
            return item;
        }

        private static G16CatalogItem CreateYbz()
        {
            var item = new G16CatalogItem
            {
                Id = "ybz",
                Name = "约束边缘构件 YBZ",
                AtlasRef = new AtlasRef("16G101-1", "75", "第75~76页"),
                IsImplemented = true,
                Build = YbzEdgeMemberBuilder.Build
            };
            item.Parameters.Add(new ParamDescriptor { Key = "wallThickness", Label = "墙厚 tw", Kind = ParamKind.Integer, DefaultInt = 300, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "edgeWidth", Label = "边缘构件宽 bc", Kind = ParamKind.Integer, DefaultInt = 300, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "edgeHeight", Label = "边缘构件高 h", Kind = ParamKind.Integer, DefaultInt = 2520, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "vBarCount", Label = "竖向纵筋根数", Kind = ParamKind.Integer, DefaultInt = 12 });
            item.Parameters.Add(new ParamDescriptor { Key = "hSpacing", Label = "水平箍筋间距", Kind = ParamKind.Integer, DefaultInt = 150, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "vSpacing", Label = "竖向筋间距", Kind = ParamKind.Integer, DefaultInt = 150, Unit = "mm" });
            return item;
        }

        private static G16CatalogItem CreateLl()
        {
            var item = new G16CatalogItem
            {
                Id = "ll",
                Name = "连梁 LL 配筋",
                AtlasRef = new AtlasRef("16G101-1", "78", "第78页"),
                IsImplemented = true,
                Build = LlCouplingBeamBuilder.Build
            };
            item.Parameters.Add(new ParamDescriptor { Key = "beamWidth", Label = "连梁宽 b", Kind = ParamKind.Integer, DefaultInt = 300, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "beamHeight", Label = "连梁高 h", Kind = ParamKind.Integer, DefaultInt = 600, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "topBarCount", Label = "上部纵筋根数", Kind = ParamKind.Integer, DefaultInt = 4 });
            item.Parameters.Add(new ParamDescriptor { Key = "bottomBarCount", Label = "下部纵筋根数", Kind = ParamKind.Integer, DefaultInt = 4 });
            item.Parameters.Add(new ParamDescriptor { Key = "stirrupSpacing", Label = "箍筋间距", Kind = ParamKind.Integer, DefaultInt = 100, Unit = "mm" });
            item.Parameters.Add(new ParamDescriptor { Key = "sideBarCount", Label = "侧面纵筋根数", Kind = ParamKind.Integer, DefaultInt = 0 });
            return item;
        }

        private static G16CatalogItem Placeholder(string id, string name, string atlas, string page, bool implemented)
        {
            return new G16CatalogItem
            {
                Id = id,
                Name = name,
                AtlasRef = new AtlasRef(atlas, page),
                IsImplemented = implemented
            };
        }
    }
}
