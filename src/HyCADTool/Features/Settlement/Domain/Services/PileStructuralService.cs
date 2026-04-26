using System;
using HyCADTool.Features.Settlement;

namespace HyCADTool.Features.Settlement.Domain.Services
{
    /// <summary>
    /// 桩身结构验算计算引擎（纯数学，平台无关）
    /// JGJ 94-2008 §5.8~5.9
    /// </summary>
    public static class PileStructuralService
    {
        /// <summary>
        /// 计算桩身结构验算
        /// </summary>
        public static PileStructuralResult Calculate(SettlementInput settlement, PileStructuralInput input)
        {
            var result = new PileStructuralResult();

            double d = settlement.PileDiameter;
            double pileArea = Math.PI / 4.0 * d * d;
            double pileAreaMm2 = pileArea * 1e6;

            // §5.8.2 桩身受压承载力
            double compressCapacity = input.PsiC * input.Phi * input.Fc * pileArea;
            bool compressCheck = input.AxialForceDesign <= compressCapacity;

            result.AxialForceDesign = input.AxialForceDesign;
            result.PsiC = input.PsiC;
            result.Phi = input.Phi;
            result.Fc = input.Fc;
            result.CompressCapacity = Math.Round(compressCapacity, 1);
            result.CompressCheck = compressCheck;

            // §5.8.3 配筋率
            if (input.SteelArea > 0)
            {
                result.SteelArea = input.SteelArea;
                result.SteelRatio = Math.Round(input.SteelArea / pileAreaMm2 * 100, 2);
                double minRatio = input.PileLength > 10 ? 0.4 : 0.2;
                result.SteelRatioCheck = result.SteelRatio >= minRatio;
            }

            // §5.9.2 预应力管桩抗裂
            result.IsPrestressed = input.IsPrestressed;
            if (input.IsPrestressed)
            {
                result.CrackCheck = (input.SigmaPc - input.SigmaTp) <= input.Ftk;
            }

            // 结论
            if (compressCheck && (input.SteelArea <= 0 || result.SteelRatioCheck))
                result.Conclusion = "桩身结构满足承载力和配筋要求。";
            else if (!compressCheck)
                result.Conclusion = "桩身受压承载力不满足要求，需增大桩径或提高混凝土等级。";
            else
                result.Conclusion = "配筋率不满足最小要求，需增加纵向钢筋。";

            return result;
        }
    }
}
