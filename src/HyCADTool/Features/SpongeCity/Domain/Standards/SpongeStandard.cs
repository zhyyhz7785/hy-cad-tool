using System.Collections.Generic;
using HyCADTool.Features.SpongeCity.Domain.Models;
using HyCADTool.Features.SpongeCity.Domain.Services;

namespace HyCADTool.Features.SpongeCity.Domain.Standards
{
    /// <summary>
    /// 数据型规范。新规范优先 new 一份填规则；校核或配建对不上时再派生子类覆盖
    /// <see cref="BuildChecks"/> / <see cref="AllocationVolume"/>。
    /// </summary>
    public class SpongeStandard : ISpongeStandard
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Citation { get; set; } = "";
        public string WorkbookTitle { get; set; } = "海绵城市雨水控制利用计算总表";
        public string PassVerdict { get; set; } = "";
        public string FailVerdict { get; set; } = "";
        public List<SurfaceSpec> Surfaces { get; set; } = new List<SurfaceSpec>();
        public List<FacilitySpec> Facilities { get; set; } = new List<FacilitySpec>();
        public SpongeRounding Rounding { get; set; } = new SpongeRounding();
        public AreaRatioRule SunkenGreen { get; set; } = new AreaRatioRule();
        public AreaRatioRule PerviousPavement { get; set; } = new AreaRatioRule();
        public GreenRatioRule GreenRatio { get; set; } = new GreenRatioRule();
        public PsiOutflowRule PsiOutflow { get; set; } = new PsiOutflowRule();
        public bool MultiplySsByAlpha { get; set; }
        public bool UseHardeningAllocation { get; set; }
        public SpongeProjectDefaults Defaults { get; set; } = new SpongeProjectDefaults();

        IReadOnlyList<SurfaceSpec> ISpongeStandard.Surfaces => Surfaces;
        IReadOnlyList<FacilitySpec> ISpongeStandard.Facilities => Facilities;

        public virtual double AllocationVolume(SpongeProjectInput input, double hardeningAreaM2)
        {
            if (!UseHardeningAllocation || input == null) return 0;
            return SpongeCalculationService.CalculateRequiredVolume522(input.Region, hardeningAreaM2);
        }

        public virtual List<CheckRow> BuildChecks(SpongeProjectInput input, SpongeResult result)
        {
            return SpongeCalculationService.BuildDefaultChecks(this, input, result);
        }

        public SpongeProjectInput CreateInput()
        {
            var d = Defaults ?? new SpongeProjectDefaults();
            var input = new SpongeProjectInput
            {
                StandardId = Id,
                ProjectName = d.ProjectName,
                Location = d.Location,
                Region = d.Region,
                ProjectType = d.ProjectType,
                TotalAreaM2 = d.TotalAreaM2,
                BuildingFootprintM2 = d.BuildingFootprintM2,
                GreenRatio = d.GreenRatio,
                AlphaTarget = d.AlphaTarget,
                DesignRainfallMm = d.DesignRainfallMm,
                EtaSsTarget = d.EtaSsTarget,
                InitialDiscardMm = d.InitialDiscardMm,
                ReturnPeriodYear = d.ReturnPeriodYear,
                AnnualRainfallMm = d.AnnualRainfallMm,
                StormIntensity = d.StormIntensity,
                SoilPermeability = d.SoilPermeability,
                UnitSpongeCost = d.UnitSpongeCost,
                SinkenDepthMm = d.SinkenDepthMm,
                Surfaces = new List<SurfaceItem>(),
                Facilities = new List<FacilityItem>(),
            };
            foreach (var s in Surfaces)
            {
                input.Surfaces.Add(new SurfaceItem
                {
                    Code = s.Code,
                    Name = s.Name,
                    LayerName = string.IsNullOrEmpty(s.LayerName) ? SpongeLayerNames.Surface(s.Code) : s.LayerName,
                    Area = s.DefaultArea,
                    Psi = s.DefaultPsi,
                    Note = s.Note,
                    AciColor = s.AciColor,
                });
            }
            foreach (var f in Facilities)
                input.Facilities.Add(ToFacilityItem(f));
            return input;
        }

        public static FacilityItem ToFacilityItem(FacilitySpec f)
        {
            return new FacilityItem
            {
                Code = f.Code,
                Name = f.Name,
                Unit = f.Unit,
                Quantity = f.DefaultQuantity,
                UnitVolumeFactor = f.UnitVolumeFactor,
                EtaSs = f.EtaSs,
                Note = f.Note,
                LayerName = f.LayerName ?? "",
                AciColor = f.AciColor,
            };
        }
    }
}
