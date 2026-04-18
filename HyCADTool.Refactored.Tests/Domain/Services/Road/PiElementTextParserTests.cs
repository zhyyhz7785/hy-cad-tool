using FluentAssertions;
using HyCADTool.Refactored.Domain.Services.Road;
using Xunit;

namespace HyCADTool.Refactored.Tests.Domain.Services.Road
{
    public class PiElementTextParserTests
    {
        [Fact]
        public void TryParse_MinimalCoordsOnly_Succeeds()
        {
            var text = "0,0\n100,0\n100,100";

            var ok = PiElementTextParser.TryParse(text, out var elements, out var errors);

            ok.Should().BeTrue();
            errors.Should().BeEmpty();
            elements.Should().HaveCount(3);
            elements[0].P.X.Should().Be(0);
            elements[1].P.X.Should().Be(100);
            elements[2].P.Y.Should().Be(100);
            elements[1].Radius.Should().Be(0);
        }

        [Fact]
        public void TryParse_WithRadiusAndSpirals_AssignsParameters()
        {
            var text = "0,0,0\n100,0,20,5,5,PI_A\n100,100,0";

            var ok = PiElementTextParser.TryParse(text, out var elements, out var errors);

            ok.Should().BeTrue();
            errors.Should().BeEmpty();
            elements[1].Radius.Should().Be(20);
            elements[1].SpiralIn.Should().Be(5);
            elements[1].SpiralOut.Should().Be(5);
            elements[1].Tag.Should().Be("PI_A");
        }

        [Fact]
        public void TryParse_WithHeaderRow_SkipsHeader()
        {
            var text = "X,Y,R,Ls_in,Ls_out,Tag\n0,0\n100,0,15\n100,100";

            var ok = PiElementTextParser.TryParse(text, out var elements, out _);

            ok.Should().BeTrue();
            elements.Should().HaveCount(3);
            elements[1].Radius.Should().Be(15);
        }

        [Fact]
        public void TryParse_TabSeparatedFromExcel_Works()
        {
            var text = "0\t0\n100\t0\t20\n100\t100";

            var ok = PiElementTextParser.TryParse(text, out var elements, out _);

            ok.Should().BeTrue();
            elements.Should().HaveCount(3);
            elements[1].Radius.Should().Be(20);
        }

        [Fact]
        public void TryParse_ChineseCommaAndMixedWhitespace_Works()
        {
            var text = "0 , 0\n100，0，20\n100\u3000100"; // 全角空格做分隔可能失败；主要验中文逗号 & 空格

            var ok = PiElementTextParser.TryParse(text, out var elements, out _);

            ok.Should().BeTrue();
            elements[1].Radius.Should().Be(20);
        }

        [Fact]
        public void TryParse_EmptyAndCommentLines_Ignored()
        {
            var text = "# 坐标表\n\n0,0\n// 注释\n100,0\n-- dash\n100,100\n";

            var ok = PiElementTextParser.TryParse(text, out var elements, out var errors);

            ok.Should().BeTrue();
            errors.Should().BeEmpty();
            elements.Should().HaveCount(3);
        }

        [Fact]
        public void TryParse_OptionalDashAndNA_TreatedAsZero()
        {
            var text = "X,Y,R,Ls_in,Ls_out\n0,0,-,-,-\n100,0,20,na,na\n100,100";

            var ok = PiElementTextParser.TryParse(text, out var elements, out _);

            ok.Should().BeTrue();
            elements[1].SpiralIn.Should().Be(0);
            elements[1].SpiralOut.Should().Be(0);
        }

        [Fact]
        public void TryParse_InvalidNumber_ReportsErrorAndSkips()
        {
            var text = "0,0\nfoo,0,20\n100,100";

            var ok = PiElementTextParser.TryParse(text, out var elements, out var errors);

            ok.Should().BeFalse();
            errors.Should().ContainSingle()
                .Which.Should().Contain("X 坐标");
            elements.Should().HaveCount(2); // 坏行被跳过，仍留 2 行
        }

        [Fact]
        public void TryParse_OnlyOneValidRow_ReturnsFalse()
        {
            var text = "0,0";

            var ok = PiElementTextParser.TryParse(text, out var elements, out var errors);

            ok.Should().BeFalse();
            errors.Should().Contain(e => e.Contains("PI 点不足"));
            elements.Should().HaveCount(1);
        }

        [Fact]
        public void TryParse_NullOrEmpty_ReturnsFalse()
        {
            PiElementTextParser.TryParse(null, out _, out var e1).Should().BeFalse();
            e1.Should().NotBeEmpty();

            PiElementTextParser.TryParse("   \n  ", out _, out var e2).Should().BeFalse();
            e2.Should().NotBeEmpty();
        }

        [Fact]
        public void TryParse_Utf8Bom_Removed()
        {
            var text = "\uFEFFX,Y\n0,0\n100,0\n100,100";

            var ok = PiElementTextParser.TryParse(text, out var elements, out _);

            ok.Should().BeTrue();
            elements.Should().HaveCount(3);
        }
    }
}
