using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HyCADTool.Features.G101.Domain.Catalog;
using HyCADTool.Features.G101.Domain.Tables;
using Xunit;

namespace HyCADTool.LinuxTests
{
    /// <summary>
    /// G101Catalog 目录树与 G101TableLookup 查表的黄金值。
    /// 数值对应 22G101-1 第 2 章已写入代码的表，不读图集文件。
    /// </summary>
    public class G101CatalogTableTests
    {
        private const string DirectoryGolden =
            "22G101-3|独立/条形/筏形/桩基础|基础|dj-foundation|独立基础 DJj/DJz 底板配筋|2-11|1\n"
            + "22G101-3|独立/条形/筏形/桩基础|基础|col-dowel|柱纵向钢筋在基础中构造|2-10|1\n"
            + "22G101-3|独立/条形/筏形/桩基础|基础|tjb|条形基础底板 TJBj/TJBp 配筋|2-20|1\n"
            + "22G101-3|独立/条形/筏形/桩基础|基础|dj-double|双柱普通独立基础配筋|2-12|1\n"
            + "22G101-3|独立/条形/筏形/桩基础|基础|jl|基础梁 JL 端部钢筋构造|2-25|0\n"
            + "22G101-3|独立/条形/筏形/桩基础|基础|lpb|梁板式筏形基础 LPB 钢筋|2-32|0\n"
            + "22G101-3|独立/条形/筏形/桩基础|基础|bpb|平板式筏形基础 BPB 钢筋|2-35|0\n"
            + "22G101-2|现浇混凝土板式楼梯|楼梯|stair-at|AT 型楼梯板配筋|2-10|1\n"
            + "22G101-2|现浇混凝土板式楼梯|楼梯|stair-bt|BT 型楼梯板配筋|2-10|1\n"
            + "22G101-2|现浇混凝土板式楼梯|楼梯|ct|CT 型楼梯板配筋|2-12|0\n"
            + "22G101-2|现浇混凝土板式楼梯|楼梯|ata|ATa 型滑动支座楼梯|2-26|0\n"
            + "22G101-2|现浇混凝土板式楼梯|楼梯|tz-tl|梯柱 TZ / 梯梁 TL 配筋|2-41|0\n"
            + "22G101-1|框架/剪力墙/梁/板|柱|kz-conn|KZ 纵向钢筋连接|2-9|0\n"
            + "22G101-1|框架/剪力墙/梁/板|柱|kz-stirrup|KZ 箍筋加密区|2-11|0\n"
            + "22G101-1|框架/剪力墙/梁/板|柱|kz-top|KZ 边柱角柱柱顶纵筋|2-14|0\n"
            + "22G101-1|框架/剪力墙/梁/板|剪力墙|wall-h|剪力墙水平分布筋|2-19|0\n"
            + "22G101-1|框架/剪力墙/梁/板|剪力墙|wall-v|剪力墙竖向分布筋|2-21|0\n"
            + "22G101-1|框架/剪力墙/梁/板|剪力墙|yjz|约束边缘构件 YBZ|2-24|0\n"
            + "22G101-1|框架/剪力墙/梁/板|剪力墙|ll|连梁 LL 配筋|2-27|0\n"
            + "22G101-1|框架/剪力墙/梁/板|梁|kl|楼层框架梁 KL 纵筋|2-33|0\n"
            + "22G101-1|框架/剪力墙/梁/板|梁|wkl|屋面框架梁 WKL 纵筋|2-34|0\n"
            + "22G101-1|框架/剪力墙/梁/板|梁|xl|纯悬挑梁 XL 配筋|2-43|0\n"
            + "22G101-1|框架/剪力墙/梁/板|板|slab-end|板端支座锚固|2-50|0\n"
            + "22G101-1|框架/剪力墙/梁/板|板|slab-hole|板开洞 BD 加强|2-62|0\n"
            + "22G101-1|框架/剪力墙/梁/板|板|hjd|后浇带 HJD 钢筋|2-59|0";

        private const string ParameterGolden =
            "dj-foundation|lengthA|底板长度 A|Integer|2400|mm\n"
            + "dj-foundation|widthB|底板宽度 B|Integer|2400|mm\n"
            + "dj-foundation|heightH|基础高度 H|Integer|600|mm\n"
            + "dj-foundation|colSize|柱截面 bc×hc|Number|600|mm\n"
            + "dj-foundation|spacingX|X 向钢筋间距|Integer|200|mm\n"
            + "dj-foundation|spacingY|Y 向钢筋间距|Integer|200|mm\n"
            + "dj-foundation|drawPlan|绘制平面图|Boolean|true|\n"
            + "dj-foundation|drawSection|绘制剖面图|Boolean|true|\n"
            + "col-dowel|foundationH|基础高度 H|Integer|500|mm\n"
            + "col-dowel|colWidth|柱宽 bc|Integer|600|mm\n"
            + "col-dowel|barCount|纵筋根数|Integer|8|\n"
            + "col-dowel|barSpacing|纵筋间距|Integer|150|mm\n"
            + "tjb|widthB|底板宽度 b|Integer|2000|mm\n"
            + "tjb|heightH1|截面高度 h1|Integer|300|mm\n"
            + "tjb|heightH2|坡形高度 h2|Integer|250|mm\n"
            + "tjb|isSlope|坡形截面 TJBp|Boolean|true|\n"
            + "tjb|segmentLen|平面段长度 L|Integer|6000|mm\n"
            + "tjb|mainSpacing|B 向受力筋间距|Integer|150|mm\n"
            + "tjb|distSpacing|分布筋间距|Integer|250|mm\n"
            + "tjb|beamWidth|基础梁宽|Integer|400|mm\n"
            + "tjb|drawPlan|绘制平面段|Boolean|true|\n"
            + "tjb|drawSection|绘制横剖面|Boolean|true|\n"
            + "dj-double|lengthA|底板长度 A|Integer|3600|mm\n"
            + "dj-double|widthB|底板宽度 B|Integer|2400|mm\n"
            + "dj-double|heightH|基础高度 H|Integer|600|mm\n"
            + "dj-double|colWidth|柱宽 bc|Integer|600|mm\n"
            + "dj-double|colSpacing|柱距|Integer|2400|mm\n"
            + "dj-double|bottomSpacing|底筋间距|Integer|200|mm\n"
            + "dj-double|topBarCount|顶部 T 筋根数|Integer|11|\n"
            + "dj-double|topSpacing|顶部 T 筋间距|Integer|100|mm\n"
            + "stair-at|stepCount|踏步级数 n|Integer|12|\n"
            + "stair-at|riseH|踏步高度 h|Integer|150|mm\n"
            + "stair-at|runB|踏步宽度 b|Integer|280|mm\n"
            + "stair-at|slabT|梯板厚度 t|Integer|120|mm\n"
            + "stair-at|ln|净跨 ln|Integer|3360|mm\n"
            + "stair-at|distSpacing|分布筋间距|Integer|250|mm\n"
            + "stair-bt|lowPlateLen|低端平板长|Integer|900|mm\n"
            + "stair-bt|stepCount|踏步级数 n|Integer|10|\n"
            + "stair-bt|riseH|踏步高度 h|Integer|150|mm\n"
            + "stair-bt|runB|踏步宽度 b|Integer|280|mm\n"
            + "stair-bt|slabT|梯板厚度 t|Integer|120|mm\n"
            + "stair-bt|ln|净跨 ln|Integer|3700|mm\n"
            + "stair-bt|distSpacing|分布筋间距|Integer|250|mm";

        [Fact]
        public void Directory_lists_three_atlases_in_publication_order()
        {
            var built = G101Catalog.Build();

            Assert.Equal(DirectoryGolden, FormatDirectory(built));
            Assert.Equal(DirectoryGolden, FormatDirectory(G101Catalog.Build()));

            var ids = Items(built).Select(i => i.Id).ToList();
            Assert.Equal(25, ids.Count);
            Assert.Equal(25, ids.Distinct().Count());
            Assert.Equal(6, Items(built).Count(i => i.IsImplemented));
        }

        [Fact]
        public void Implemented_items_carry_parameters_and_placeholders_do_not()
        {
            var built = G101Catalog.Build();
            var implemented = Items(built).Where(i => i.IsImplemented).ToList();
            var lines = implemented.SelectMany(FormatParameters);
            Assert.Equal(ParameterGolden, string.Join("\n", lines));

            foreach (var item in implemented)
            {
                Assert.NotNull(item.Build);
                Assert.NotEmpty(item.Parameters);
                Assert.Equal(item.AtlasRef.Display, item.AtlasRef.AtlasId + " P" + item.AtlasRef.Page);
            }

            foreach (var item in Items(built).Where(i => !i.IsImplemented))
            {
                Assert.Null(item.Build);
                Assert.Empty(item.Parameters);
            }
        }

        [Theory]
        [InlineData("dj-foundation", "DJ 底板配筋（22G101-3 P2-11）")]
        [InlineData("col-dowel", "柱插筋弯折锚固（22G101-3 P2-10）")]
        [InlineData("tjb", "TJB 底板配筋（22G101-3 P2-20/21）")]
        [InlineData("dj-double", "双柱独基配筋（22G101-3 P2-12）")]
        [InlineData("stair-at", "AT 型楼梯板配筋（22G101-2 P2-10）")]
        [InlineData("stair-bt", "BT 型楼梯板配筋（22G101-2 P2-10）")]
        public void Implemented_builder_uses_catalog_defaults(string id, string title)
        {
            var item = Items(G101Catalog.Build()).Single(i => i.Id == id);
            var sketch = item.Build(new G101GlobalSettings(), Defaults(item));

            Assert.Equal(title, sketch.Title);
            Assert.NotEmpty(sketch.Polylines);
        }

        [Fact]
        public void Column_dowel_switches_at_labE_plus_cover()
        {
            // 默认 C30 / HRB400 / 二级：labE = 40d = 640。一类环境基础保护层 25。门槛 665。
            var item = Items(G101Catalog.Build()).Single(i => i.Id == "col-dowel");
            var global = new G101GlobalSettings();
            Assert.Equal(640, G101TableLookup.GetLaE(global));
            Assert.Equal(25, G101TableLookup.GetCoverThickness(global.EnvironmentClass));

            var below = Defaults(item);
            below["foundationH"] = 664;
            Assert.Equal(
                "柱插筋弯折锚固（22G101-3 P2-10）",
                item.Build(global, below).Title);

            var at = Defaults(item);
            at["foundationH"] = 665;
            Assert.Equal(
                "柱插筋直锚（22G101-3 P2-10）",
                item.Build(global, at).Title);
        }

        public static IEnumerable<object[]> LabCells()
        {
            // 钢筋, 混凝土, lab, 一二级 labE, 三级 labE。列序 C25→C60 及以上。
            yield return Row(RebarGrade.HPB300, ConcreteGrade.C25, 34, 39, 36);
            yield return Row(RebarGrade.HPB300, ConcreteGrade.C30, 30, 35, 32);
            yield return Row(RebarGrade.HPB300, ConcreteGrade.C35, 28, 32, 29);
            yield return Row(RebarGrade.HPB300, ConcreteGrade.C40, 25, 29, 26);
            yield return Row(RebarGrade.HPB300, ConcreteGrade.C45, 24, 28, 25);
            yield return Row(RebarGrade.HPB300, ConcreteGrade.C50, 23, 26, 24);
            yield return Row(RebarGrade.HPB300, ConcreteGrade.C55, 23, 25, 23);
            yield return Row(RebarGrade.HPB300, ConcreteGrade.C60Plus, 23, 24, 23);

            yield return Row(RebarGrade.HRB400, ConcreteGrade.C25, 40, 46, 42);
            yield return Row(RebarGrade.HRB400, ConcreteGrade.C30, 35, 40, 37);
            yield return Row(RebarGrade.HRB400, ConcreteGrade.C35, 32, 37, 34);
            yield return Row(RebarGrade.HRB400, ConcreteGrade.C40, 29, 33, 30);
            yield return Row(RebarGrade.HRB400, ConcreteGrade.C45, 27, 32, 29);
            yield return Row(RebarGrade.HRB400, ConcreteGrade.C50, 25, 30, 27);
            yield return Row(RebarGrade.HRB400, ConcreteGrade.C55, 25, 30, 27);
            yield return Row(RebarGrade.HRB400, ConcreteGrade.C60Plus, 24, 29, 26);

            yield return Row(RebarGrade.HRB500, ConcreteGrade.C25, 48, 55, 50);
            yield return Row(RebarGrade.HRB500, ConcreteGrade.C30, 43, 49, 45);
            yield return Row(RebarGrade.HRB500, ConcreteGrade.C35, 39, 45, 41);
            yield return Row(RebarGrade.HRB500, ConcreteGrade.C40, 36, 41, 38);
            yield return Row(RebarGrade.HRB500, ConcreteGrade.C45, 34, 39, 36);
            yield return Row(RebarGrade.HRB500, ConcreteGrade.C50, 32, 37, 34);
            yield return Row(RebarGrade.HRB500, ConcreteGrade.C55, 31, 36, 33);
            yield return Row(RebarGrade.HRB500, ConcreteGrade.C60Plus, 30, 35, 32);
        }

        [Theory]
        [MemberData(nameof(LabCells))]
        public void Anchor_multipliers_match_page_2_2(
            RebarGrade rebar,
            ConcreteGrade concrete,
            int lab,
            int labE12,
            int labE3)
        {
            var grade1 = Settings(rebar, concrete, SeismicGrade.Grade1);
            var grade2 = Settings(rebar, concrete, SeismicGrade.Grade2);
            var grade3 = Settings(rebar, concrete, SeismicGrade.Grade3);
            var grade4 = Settings(rebar, concrete, SeismicGrade.Grade4);

            Assert.Equal(lab, G101TableLookup.GetLabMultiplier(grade2));
            Assert.Equal(labE12, G101TableLookup.GetLabEMultiplier(grade1));
            Assert.Equal(labE12, G101TableLookup.GetLabEMultiplier(grade2));
            Assert.Equal(labE3, G101TableLookup.GetLabEMultiplier(grade3));
            Assert.Equal(lab, G101TableLookup.GetLabEMultiplier(grade4));
            Assert.Equal(lab * 16.0, G101TableLookup.GetLab(grade2));
            Assert.Equal(labE12 * 16.0, G101TableLookup.GetLabE(grade2));
            Assert.Equal(G101TableLookup.GetLab(grade2), G101TableLookup.GetLa(grade2));
            Assert.Equal(G101TableLookup.GetLabE(grade4), G101TableLookup.GetLaE(grade4));
        }

        [Fact]
        public void Panel_example_is_hrb400_c30_diameter_16()
        {
            var settings = new G101GlobalSettings();

            Assert.Equal(ConcreteGrade.C30, settings.ConcreteGrade);
            Assert.Equal(RebarGrade.HRB400, settings.RebarGrade);
            Assert.Equal(SeismicGrade.Grade2, settings.SeismicGrade);
            Assert.Equal(16, settings.RebarDiameter);
            Assert.Equal(560, G101TableLookup.GetLab(settings));
            Assert.Equal(640, G101TableLookup.GetLabE(settings));
            Assert.Equal(
                "lab = 560mm（d=16，22G101-1 P2-2）",
                G101TableLookup.FormatLookup("lab", 560, G101TableLookup.RefLab, settings));
            Assert.Equal("22G101-1 P2-2", G101TableLookup.RefLab.Display);
            Assert.Equal("22G101-1 P2-1", G101TableLookup.RefCover.Display);
            Assert.Equal("22G101-1 P2-5", G101TableLookup.RefLl.Display);
            Assert.Equal("22G101-1 P2-6", G101TableLookup.RefLlE.Display);
        }

        [Theory]
        [InlineData(SplicePercent.P0, 1.2, 1.2)]
        [InlineData(SplicePercent.P25, 1.4, 1.4)]
        [InlineData(SplicePercent.P50, 1.6, 1.6)]
        [InlineData(SplicePercent.P100, 2.0, 1.6)]
        public void Splice_length_uses_zeta_and_a_300mm_floor(
            SplicePercent percent,
            double zeta,
            double zetaE)
        {
            var settings = new G101GlobalSettings();
            double la = 560;
            double laE = 640;

            Assert.Equal(System.Math.Max(300, zeta * la), G101TableLookup.GetLl(settings, percent));
            Assert.Equal(System.Math.Max(300, zetaE * laE), G101TableLookup.GetLlE(settings, percent));
        }

        [Fact]
        public void Splice_length_never_drops_below_300()
        {
            var settings = new G101GlobalSettings { RebarDiameter = 1 };
            Assert.Equal(300, G101TableLookup.GetLl(settings, SplicePercent.P0));
            Assert.Equal(300, G101TableLookup.GetLlE(settings, SplicePercent.P0));
        }

        [Fact]
        public void Unknown_concrete_grade_uses_the_c30_column()
        {
            var settings = Settings(RebarGrade.HPB300, (ConcreteGrade)99, SeismicGrade.Grade4);
            Assert.Equal(30, G101TableLookup.GetLabMultiplier(settings));
        }

        [Theory]
        [InlineData(EnvironmentClass.ClassI, 15, 20, 25)]
        [InlineData(EnvironmentClass.ClassIIa, 20, 25, 30)]
        [InlineData(EnvironmentClass.ClassIIb, 25, 35, 40)]
        [InlineData(EnvironmentClass.ClassIIIa, 30, 40, 40)]
        [InlineData(EnvironmentClass.ClassIIIb, 40, 50, 50)]
        public void Cover_thickness_matches_page_2_1(
            EnvironmentClass environment,
            int slab,
            int wall,
            int foundation)
        {
            Assert.Equal(slab, G101TableLookup.GetCoverThickness(environment, 0));
            Assert.Equal(wall, G101TableLookup.GetCoverThickness(environment, 1));
            Assert.Equal(foundation, G101TableLookup.GetCoverThickness(environment, 2));
            Assert.Equal(foundation, G101TableLookup.GetCoverThickness(environment));
            Assert.Equal(slab, G101TableLookup.GetCoverThickness(environment, -1));
            Assert.Equal(foundation, G101TableLookup.GetCoverThickness(environment, 9));
        }

        [Theory]
        [InlineData(RebarGrade.HPB300, 20, 50)]
        [InlineData(RebarGrade.HRB400, 20, 80)]
        [InlineData(RebarGrade.HRB500, 20, 120)]
        [InlineData(RebarGrade.HRB500, 24, 144)]
        [InlineData(RebarGrade.HRB500, 25, 175)]
        [InlineData(RebarGrade.HRB500, 28, 196)]
        public void Bend_diameter_matches_page_2_2(RebarGrade grade, int diameter, double expected)
        {
            Assert.Equal(expected, G101TableLookup.GetBendDiameter(grade, diameter));
        }

        [Fact]
        public void Unknown_rebar_grade_bends_at_four_diameters()
        {
            Assert.Equal(80, G101TableLookup.GetBendDiameter((RebarGrade)99, 20));
        }

        [Theory]
        [InlineData(5, 75)]
        [InlineData(6, 75)]
        [InlineData(7, 84)]
        [InlineData(8, 96)]
        [InlineData(16, 192)]
        public void Stirrup_hook_matches_page_2_7(int diameter, double expected)
        {
            Assert.Equal(expected, G101TableLookup.GetStirrupHookLength(diameter));
        }

        private static object[] Row(
            RebarGrade rebar,
            ConcreteGrade concrete,
            int lab,
            int labE12,
            int labE3)
        {
            return new object[] { rebar, concrete, lab, labE12, labE3 };
        }

        private static G101GlobalSettings Settings(
            RebarGrade rebar,
            ConcreteGrade concrete,
            SeismicGrade seismic)
        {
            return new G101GlobalSettings
            {
                RebarGrade = rebar,
                ConcreteGrade = concrete,
                SeismicGrade = seismic,
                RebarDiameter = 16
            };
        }

        private static IEnumerable<G101CatalogItem> Items(IReadOnlyList<G101CatalogAtlas> atlases)
        {
            return atlases.SelectMany(a => a.Groups).SelectMany(g => g.Items);
        }

        private static string FormatDirectory(IReadOnlyList<G101CatalogAtlas> atlases)
        {
            var lines = new List<string>();
            foreach (var atlas in atlases)
            {
                foreach (var group in atlas.Groups)
                {
                    foreach (var item in group.Items)
                    {
                        lines.Add(string.Join("|",
                            atlas.AtlasId,
                            atlas.Name,
                            group.Name,
                            item.Id,
                            item.Name,
                            item.AtlasRef.Page,
                            item.IsImplemented ? "1" : "0"));
                    }
                }
            }

            return string.Join("\n", lines);
        }

        private static IEnumerable<string> FormatParameters(G101CatalogItem item)
        {
            foreach (var parameter in item.Parameters)
            {
                string value;
                if (parameter.Kind == ParamKind.Boolean)
                    value = parameter.DefaultBool ? "true" : "false";
                else if (parameter.Kind == ParamKind.Number)
                    value = parameter.DefaultNumber.ToString("0.###", CultureInfo.InvariantCulture);
                else
                    value = parameter.DefaultInt.ToString(CultureInfo.InvariantCulture);

                yield return string.Join("|",
                    item.Id,
                    parameter.Key,
                    parameter.Label,
                    parameter.Kind,
                    value,
                    parameter.Unit ?? "");
            }
        }

        private static Dictionary<string, object> Defaults(G101CatalogItem item)
        {
            var values = new Dictionary<string, object>();
            foreach (var parameter in item.Parameters)
            {
                if (parameter.Kind == ParamKind.Boolean)
                    values[parameter.Key] = parameter.DefaultBool;
                else if (parameter.Kind == ParamKind.Number)
                    values[parameter.Key] = parameter.DefaultNumber;
                else
                    values[parameter.Key] = parameter.DefaultInt;
            }

            return values;
        }
    }
}
