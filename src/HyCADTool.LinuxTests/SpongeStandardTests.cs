using System;
using System.Globalization;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using HyCADTool.Features.SpongeCity.Domain.Models;
using HyCADTool.Features.SpongeCity.Domain.Services;
using HyCADTool.Features.SpongeCity.Domain.Standards;
using HyCADTool.Features.SpongeCity.Infrastructure;
using Xunit;

namespace HyCADTool.LinuxTests
{
    public class SpongeStandardTests
    {
        [Fact]
        public void Db42_matches_industrial_workbook()
        {
            var input = SpongeProjectInput.CreateFor(SpongeStandardIds.Db42);
            var result = SpongeCalculationService.Compute(input);

            Assert.Equal(SpongeStandardIds.Db42, input.StandardId);
            Assert.Equal(SpongeStandardIds.Db42, result.StandardId);
            Assert.Equal(0.30, input.Surfaces.Single(s => s.Code == "S10").Psi, 9);
            Assert.Equal("HY-海绵-S1", input.Surfaces.Single(s => s.Code == "S1").LayerName);
            Assert.Equal("HY-海绵-S10", input.Surfaces.Single(s => s.Code == "S10").LayerName);
            Assert.Equal("HY-海绵-F5", input.Facilities.Single(f => f.Code == "F5").LayerName);
            Assert.Equal(0.85, input.Facilities.Single(f => f.Code == "F2").EtaSs, 9);
            Assert.Equal(152.86, input.Facilities.Single(f => f.Code == "F2").Quantity, 6);
            Assert.Equal(0.40, input.Facilities.Single(f => f.Code == "F2").UnitVolumeFactor, 9);
            Assert.Equal(30, input.Facilities.Single(f => f.Code == "F6").Quantity, 9);

            Assert.Equal(0.744, result.PsiZ, 6);
            Assert.Equal(26.6, result.DesignVolumeW, 6);
            Assert.Equal(26.6, result.FinalRequiredVolume, 6);
            Assert.Equal(0, result.RequiredVolume522, 9);
            Assert.Equal(91.14, result.ProvidedVolume, 6);
            Assert.Equal(0.768, result.EtaCombined, 6);
            Assert.Equal(0.768, result.EtaCompared, 6);
            Assert.Equal(1.0, result.SinkenGreenRatio, 6);
            Assert.Equal(0, result.PerviousPavementRatio, 9);
            Assert.True(result.AllPassed);

            Assert.Equal("达标", Check(result, SpongeCheckCodes.Volume).VerdictText);
            Assert.Equal("达标", Check(result, SpongeCheckCodes.Ss).VerdictText);
            Assert.Equal("达标", Check(result, SpongeCheckCodes.Sunken).VerdictText);
            Assert.Equal("满足", Check(result, SpongeCheckCodes.Pervious).VerdictText);
            Assert.True(Check(result, SpongeCheckCodes.Pervious).CountsTowardVerdict);
            Assert.False(Check(result, SpongeCheckCodes.Green).CountsTowardVerdict);
            Assert.Equal("—参考", Check(result, SpongeCheckCodes.Green).VerdictText);
            Assert.False(Check(result, SpongeCheckCodes.Psi).CountsTowardVerdict);
            Assert.Equal("—参考", Check(result, SpongeCheckCodes.Psi).VerdictText);
            Assert.False(Check(result, SpongeCheckCodes.Alpha).CountsTowardVerdict);

            var md = SpongeReportGenerator.Generate(input, result);
            Assert.Contains("26.6", md);
            Assert.Contains("0.768", md);
            Assert.Contains("满足", md);
            Assert.Contains(SpongeStandardIds.Db42, md);
            Assert.Contains(result.VerdictText, md);
        }

        [Fact]
        public void Db42_keeps_user_edited_eta()
        {
            var input = SpongeProjectInput.CreateFor(SpongeStandardIds.Db42);
            input.Facilities.Single(f => f.Code == "F2").EtaSs = 0.90;
            SpongeCalculationService.Compute(input);
            Assert.Equal(0.90, input.Facilities.Single(f => f.Code == "F2").EtaSs, 9);
            Assert.Equal(152.86, input.Facilities.Single(f => f.Code == "F2").Quantity, 6);
        }

        [Fact]
        public void Db42_permeable_denominator_excludes_road_and_gravel_and_is_configurable()
        {
            var rule = SpongeStandardRegistry.GetOrDefault(SpongeStandardIds.Db42).PerviousPavement;
            Assert.DoesNotContain("S3", rule.DenominatorCodes);
            Assert.DoesNotContain("S4", rule.DenominatorCodes);
            Assert.Contains("S5", rule.DenominatorCodes);
            Assert.False(string.IsNullOrWhiteSpace(rule.Documentation));

            var input = SpongeProjectInput.CreateFor(SpongeStandardIds.Db42);
            input.Surfaces.Single(s => s.Code == "S5").Area = 30;
            input.Surfaces.Add(new SurfaceItem { Code = "PLAZA", Area = 70, Psi = 0.65 });
            double ratio = SpongeCalculationService.AreaRatio(
                input.Surfaces,
                new[] { "S5" },
                new[] { "S5", "PLAZA" });
            Assert.Equal(0.30, ratio, 9);
        }

        [Fact]
        public void Db13_default_keeps_residential_behavior()
        {
            var input = SpongeProjectInput.CreateDefault();
            Assert.Equal(SpongeStandardIds.Db13, input.StandardId);
            var result = SpongeCalculationService.Compute(input);

            Assert.Equal(0.555, result.PsiZ, 6);
            Assert.Equal(610.5, result.DesignVolumeW, 4);
            Assert.Equal(625, result.RequiredVolume522, 6);
            Assert.Equal(625, result.FinalRequiredVolume, 6);
            Assert.Equal(1135, result.ProvidedVolume, 6);
            Assert.Equal(817.0 / 1135.0, result.EtaCombined, 9);
            Assert.Equal(0.75 * (817.0 / 1135.0), result.EtaAnnual, 9);
            Assert.Equal(result.EtaAnnual, result.EtaCompared, 9);
            Assert.Equal(2500.0 / 20500.0, result.SinkenGreenRatio, 9);
            Assert.Equal(0.20, result.PerviousPavementRatio, 9);
            Assert.False(result.AllPassed);

            Assert.Equal(600, input.Facilities.Single(f => f.Code == "F2").Quantity, 9);
            Assert.Equal(12500, input.Surfaces.Single(s => s.Code == "S7").Area, 9);
            Assert.Equal(0.50, input.Surfaces.Single(s => s.Code == "S10").Psi, 9);
            Assert.Equal(0.80, input.Facilities.Single(f => f.Code == "F2").EtaSs, 9);

            Assert.True(Check(result, SpongeCheckCodes.Volume).Passed);
            Assert.True(Check(result, SpongeCheckCodes.Ss).Passed);
            Assert.True(Check(result, SpongeCheckCodes.Green).Passed);
            Assert.True(Check(result, SpongeCheckCodes.Green).CountsTowardVerdict);
            Assert.False(Check(result, SpongeCheckCodes.Sunken).Passed);
            Assert.False(Check(result, SpongeCheckCodes.Pervious).Passed);
            Assert.Equal("不达标", Check(result, SpongeCheckCodes.Psi).VerdictText);
            Assert.True(Check(result, SpongeCheckCodes.Psi).CountsTowardVerdict);

            var den = SpongeStandardRegistry.GetOrDefault(SpongeStandardIds.Db13).PerviousPavement.DenominatorCodes;
            Assert.Contains("S3", den);
            Assert.Contains("S4", den);
            Assert.Contains("S5", den);
        }

        [Fact]
        public void Db42_workbook_stamps_engine_numbers()
        {
            var input = SpongeProjectInput.CreateFor(SpongeStandardIds.Db42);
            var result = SpongeCalculationService.Compute(input);
            string path = Path.Combine(Path.GetTempPath(), "sponge-db42-" + Guid.NewGuid().ToString("N") + ".xlsx");
            try
            {
                SpongeCalcWorkbookBuilder.Build(input, result, path);
                using (var doc = SpreadsheetDocument.Open(path, false))
                {
                    Assert.Equal(26.6, Number(doc, "基本参数", "G12"), 6);
                    Assert.Equal(0.744, Number(doc, "下垫面分析", "E19"), 6);
                    Assert.Equal(26.6, Number(doc, "调蓄容积需求", "D10"), 6);
                    Assert.Equal(61.14, Number(doc, "设施配置", "E7"), 6);
                    Assert.Equal(91.14, Number(doc, "设施配置", "E14"), 6);
                    Assert.Equal(0.768, Number(doc, "污染削减率验算", "E16"), 6);
                    Assert.Contains("达标", Text(doc, "校核与汇总", "A33"));
                    Assert.Equal("满足", Text(doc, "设施配置", "E19"));
                    Assert.Contains(SpongeStandardIds.Db42, Text(doc, "基本参数", "A1"));
                }
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        private static CheckRow Check(SpongeResult result, string code)
        {
            return result.Checks.Single(c => c.Code == code);
        }

        private static double Number(SpreadsheetDocument doc, string sheet, string address)
        {
            string text = Text(doc, sheet, address);
            return double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static string Text(SpreadsheetDocument doc, string sheetName, string address)
        {
            var workbook = doc.WorkbookPart;
            var sheet = workbook.Workbook.Sheets.Elements<Sheet>().First(s => s.Name == sheetName);
            var part = (WorksheetPart)workbook.GetPartById(sheet.Id);
            foreach (var cell in part.Worksheet.Descendants<Cell>())
            {
                if (cell.CellReference == null || cell.CellReference.Value != address) continue;
                if (cell.InlineString != null && cell.InlineString.Text != null)
                    return cell.InlineString.Text.Text ?? "";
                if (cell.CellValue != null) return cell.CellValue.Text ?? "";
                return cell.InnerText ?? "";
            }
            throw new InvalidOperationException("缺少单元格 " + sheetName + "!" + address);
        }
    }
}
