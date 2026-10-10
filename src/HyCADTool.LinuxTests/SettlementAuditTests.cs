using System.Collections.Generic;
using System.IO;
using HyCADTool.Features.Settlement;
using HyCADTool.Features.Settlement.Domain.Services;
using Xunit;

namespace HyCADTool.LinuxTests
{
    public class SettlementAuditTests
    {
        [Fact]
        public void Zero_additional_pressure_without_rebound_is_not_success()
        {
            var result = SettlementCalculationService.Calculate(BaseInput(additionalPressure: 0, enableRebound: false));

            Assert.False(result.Success);
            Assert.Contains("p₀", result.Message);
        }

        [Fact]
        public void Zero_additional_pressure_with_failed_rebound_is_not_success()
        {
            var input = BaseInput(additionalPressure: 0, enableRebound: true);
            input.FoundationDepth = 0;

            var result = SettlementCalculationService.Calculate(input);

            Assert.False(result.Success);
            Assert.False(result.HasRebound);
            Assert.Contains("没有得出结果", result.Message);
        }

        [Fact]
        public void Zero_additional_pressure_with_rebound_succeeds_without_claiming_compression()
        {
            var result = SettlementCalculationService.Calculate(BaseInput(additionalPressure: 0, enableRebound: true));

            Assert.True(result.Success);
            Assert.True(result.HasRebound);
            Assert.Contains("未计算附加应力沉降", result.Message);
            Assert.Equal(0, result.CompressionSettlement, 9);
        }

        [Fact]
        public void Positive_additional_pressure_still_marks_compression_success()
        {
            var result = SettlementCalculationService.Calculate(BaseInput(additionalPressure: 80, enableRebound: false));

            Assert.True(result.Success);
            Assert.False(result.HasRebound);
            Assert.True(result.CompressionSettlement > 0);
        }

        [Fact]
        public void Pile_rebound_uses_layers_below_the_pile_tip()
        {
            var upper = new SoilLayer { Name = "桩身段", Thickness = 10, Es = 8, Eci = 40 };
            var lower = new SoilLayer { Name = "桩端下", Thickness = 6, Es = 30, Eci = 150 };

            var pile = BaseInput(additionalPressure: 0, enableRebound: true);
            pile.Type = FoundationType.Pile;
            pile.PileLength = upper.Thickness;
            pile.PileDiameter = 0.5;
            pile.SoilLayers = new List<SoilLayer> { upper, lower };

            var onlyLower = BaseInput(additionalPressure: 0, enableRebound: true);
            onlyLower.Type = FoundationType.Natural;
            onlyLower.SoilLayers = new List<SoilLayer> { lower };

            var allLayers = BaseInput(additionalPressure: 0, enableRebound: true);
            allLayers.Type = FoundationType.Natural;
            allLayers.SoilLayers = new List<SoilLayer> { upper, lower };

            var pileResult = SettlementCalculationService.Calculate(pile);
            var lowerResult = SettlementCalculationService.Calculate(onlyLower);
            var allResult = SettlementCalculationService.Calculate(allLayers);

            Assert.True(pileResult.HasRebound);
            Assert.Equal(lowerResult.ReboundSettlement, pileResult.ReboundSettlement, 6);
            Assert.NotEqual(allResult.ReboundSettlement, pileResult.ReboundSettlement);
        }

        [Fact]
        public void Composite_bearing_check_compares_pk_with_fspk()
        {
            var bearing = new BearingCapacityInput
            {
                Fsk = 100,
                Beta = 1,
                Lambda = 1,
                CompQp = 0,
                CompPileDiameter = 0.5,
                CompPileSpacing = 1.5
            };

            var heavy = new SettlementInput
            {
                FoundationWidth = 2,
                FoundationLength = 2,
                AdditionalPressure = 500,
                GammaM = 20,
                FoundationDepth = 1,
                SoilLayers = new List<SoilLayer> { new SoilLayer { Thickness = 5, Es = 10 } }
            };
            var light = new SettlementInput
            {
                FoundationWidth = 2,
                FoundationLength = 2,
                AdditionalPressure = 0,
                GammaM = 0,
                FoundationDepth = 0
            };

            var fail = BearingCapacityService.CalculateComposite(heavy, bearing);
            var pass = BearingCapacityService.CalculateComposite(light, bearing);

            Assert.True(fail.Pk > fail.Fspk);
            Assert.False(fail.BearingCheck);
            Assert.True(pass.BearingCheck);
        }

        [Fact]
        public void Prestressed_crack_failure_is_in_the_conclusion()
        {
            var settlement = new SettlementInput { PileDiameter = 0.5 };
            var input = new PileStructuralInput
            {
                AxialForceDesign = 1,
                PsiC = 1,
                Phi = 1,
                Fc = 100000,
                SteelArea = 0,
                IsPrestressed = true,
                SigmaPc = 100,
                SigmaTp = 0,
                Ftk = 10
            };

            var result = PileStructuralService.Calculate(settlement, input);

            Assert.False(result.CrackCheck);
            Assert.Contains("抗裂", result.Conclusion);
        }

        [Fact]
        public void Settlement_table_picks_the_point_before_the_write_transaction()
        {
            var text = File.ReadAllText(Path.Combine(
                RepoFile.Root(), "src", "HyCADTool", "Features", "Settlement", "Commands", "SettlementCalculationCommand.cs"));
            int getPoint = text.IndexOf("GetPoint", System.StringComparison.Ordinal);
            int transaction = text.IndexOf("StartTransaction", System.StringComparison.Ordinal);

            Assert.True(getPoint >= 0 && transaction > getPoint);
            Assert.DoesNotContain("new Point3d(0, 0, 0)", text);
            Assert.Contains("已取消", text);
        }

        [Fact]
        public void Sponge_panel_sends_registered_commands_instead_of_invoking_on_the_ui_thread()
        {
            var text = File.ReadAllText(Path.Combine(
                RepoFile.Root(), "src", "HyCADTool", "Features", "SpongeCity", "ViewModels", "SpongeCityPanelViewModel.cs"));

            Assert.Contains("CommandDispatcher.Send", text);
            Assert.Contains("\"hyscDL\"", text);
            Assert.DoesNotContain("mi.Invoke", text);
            Assert.DoesNotContain("Activator.CreateInstance", text);
        }

        private static SettlementInput BaseInput(double additionalPressure, bool enableRebound)
        {
            return new SettlementInput
            {
                BoreholeElevation = 0,
                StructureZeroElevation = 2,
                FoundationDepth = 2,
                FoundationWidth = 2,
                FoundationLength = 4,
                AdditionalPressure = additionalPressure,
                GammaM = 20,
                EnableRebound = enableRebound,
                PsiC = 1,
                Type = FoundationType.Natural,
                SoilLayers = new List<SoilLayer>
                {
                    new SoilLayer { Name = "粉质黏土", Thickness = 6, Es = 10, Eci = 50 }
                }
            };
        }
    }

    internal static class RepoFile
    {
        public static string Root()
        {
            var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "HyCADtoolGpt.sln")))
                dir = dir.Parent;
            if (dir == null)
                throw new System.InvalidOperationException("找不到 HyCADtoolGpt.sln");
            return dir.FullName;
        }
    }
}
