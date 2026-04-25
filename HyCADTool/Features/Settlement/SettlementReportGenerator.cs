using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HyCADTool.Features.Settlement
{
    /// <summary>
    /// 计算书上下文，封装所有生成报告所需的输入、结果和元数据
    /// </summary>
    public class ReportContext
    {
        public SettlementInput Input { get; set; }
        public SettlementResult Result { get; set; }

        public string ProjectName { get; set; } = "";
        public string Author { get; set; } = "";
        public string Reviewer { get; set; } = "";
        public string Date { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");

        public bool UseAbsoluteElevation { get; set; }
        public double AbsBoreholeElevation { get; set; }
        public double AbsStructureZero { get; set; }

        /// <summary>用户界面输入的原始土层（从孔口起算）</summary>
        public List<SoilLayer> OriginalSoilLayers { get; set; } = new List<SoilLayer>();

        /// <summary>承载力验算结果（Phase 2 扩展）</summary>
        public BearingCapacityResult BearingResult { get; set; }

        /// <summary>桩身结构验算结果（Phase 3 扩展）</summary>
        public PileStructuralResult StructuralResult { get; set; }

        #region P0 自动/手动模式

        /// <summary>p₀ 是否由程序自动计算</summary>
        public bool IsP0Auto { get; set; }

        /// <summary>上部结构传来的竖向力 Fk (kN)（自动模式用）</summary>
        public double AxialForce { get; set; }

        /// <summary>独基：基础及回填土重 Gk (kN)（自动模式用）</summary>
        public double SelfWeightLoad { get; set; }

        /// <summary>是否存在地下水</summary>
        public bool HasGroundwater { get; set; }

        /// <summary>地下水埋深 (m)</summary>
        public double GroundwaterDepth { get; set; }

        #endregion
    }

    /// <summary>承载力验算结果（Phase 2 占位）</summary>
    public class BearingCapacityResult
    {
        public double Ra { get; set; }
        public double Qsk { get; set; }
        public double Qpk { get; set; }
        public double Quk { get; set; }
        public double Fspk { get; set; }
        public double RequiredFcu { get; set; }
        public double ActualFcu { get; set; }
        public bool FcuCheck { get; set; }
        public bool BearingCheck { get; set; }

        public double Lambda { get; set; } = 1.0;
        public double Beta { get; set; } = 0.5;
        public double AreaReplacementRatio { get; set; }
        public double Fsk { get; set; }
        public double AlphaP { get; set; } = 1.0;
        public double QpValue { get; set; }
        public double Nk { get; set; }

        public List<SideResistanceRow> SideResistanceRows { get; set; } = new List<SideResistanceRow>();
    }

    public class SideResistanceRow
    {
        public string LayerName { get; set; } = "";
        public string SoilType { get; set; } = "";
        public double Thickness { get; set; }
        public double Qsi { get; set; }
        public double Product => Qsi * Thickness;
    }

    /// <summary>桩身结构验算结果（Phase 3 占位）</summary>
    public class PileStructuralResult
    {
        public double AxialForceDesign { get; set; }
        public double PsiC { get; set; } = 0.7;
        public double Phi { get; set; } = 1.0;
        public double Fc { get; set; }
        public double CompressCapacity { get; set; }
        public bool CompressCheck { get; set; }
        public double SteelArea { get; set; }
        public double SteelRatio { get; set; }
        public bool SteelRatioCheck { get; set; }
        public bool IsPrestressed { get; set; }
        public bool CrackCheck { get; set; }
        public string Conclusion { get; set; } = "";
    }

    /// <summary>
    /// 基础沉降计算书组装引擎
    /// 根据 ReportContext 中的基础子类型 x 地基类型组合，自动组装对应章节
    /// </summary>
    public class SettlementReportGenerator
    {
        public string Generate(ReportContext ctx)
        {
            var sb = new StringBuilder();
            sb.Append(GenerateCover(ctx));
            sb.Append(GenerateP0Section(ctx));

            switch (ctx.Input.Type)
            {
                case FoundationType.Natural:
                    sb.Append(GenerateNaturalSettlement(ctx));
                    break;
                case FoundationType.Composite:
                    if (ctx.BearingResult != null)
                        sb.Append(GenerateCompositeBearing(ctx));
                    sb.Append(GenerateCompositeSettlement(ctx));
                    break;
                case FoundationType.Pile:
                    if (ctx.BearingResult != null)
                        sb.Append(GeneratePileBearing(ctx));
                    sb.Append(GeneratePileSettlement(ctx));
                    if (ctx.StructuralResult != null)
                        sb.Append(GeneratePileStructural(ctx));
                    break;
            }

            if (ctx.Result.HasRebound)
                sb.Append(GenerateRebound(ctx));

            sb.Append(GenerateConclusion(ctx));
            return sb.ToString();
        }

        #region §00 封面与工程概况

        private string GenerateCover(ReportContext ctx)
        {
            var input = ctx.Input;
            var sb = new StringBuilder();

            string typeLabel;
            switch (input.Type)
            {
                case FoundationType.Natural: typeLabel = "天然地基"; break;
                case FoundationType.Composite: typeLabel = "复合地基"; break;
                case FoundationType.Pile: typeLabel = "桩基础"; break;
                default: typeLabel = "天然地基"; break;
            }
            string subTypeLabel = input.IsBoxFoundation ? "箱型基础" : "独立基础";

            sb.AppendLine("# 基础沉降计算书");
            sb.AppendLine();
            sb.AppendLine("## 工程信息");
            sb.AppendLine();
            sb.AppendLine("| 项目 | 内容 |");
            sb.AppendLine("|------|------|");
            if (!string.IsNullOrWhiteSpace(ctx.ProjectName))
                sb.AppendLine($"| 工程名称 | {ctx.ProjectName} |");
            sb.AppendLine($"| 计算类型 | {typeLabel}（{subTypeLabel}） |");
            if (!string.IsNullOrWhiteSpace(ctx.Author))
                sb.AppendLine($"| 计算人 | {ctx.Author} |");
            if (!string.IsNullOrWhiteSpace(ctx.Reviewer))
                sb.AppendLine($"| 审核人 | {ctx.Reviewer} |");
            sb.AppendLine($"| 计算日期 | {ctx.Date} |");
            sb.AppendLine();

            // 计算依据
            sb.AppendLine("## 计算依据");
            sb.AppendLine();
            sb.AppendLine("- GB 50007-2011《建筑地基基础设计规范》§5.3");
            sb.AppendLine("- GB 55003-2021《建筑与市政地基基础通用规范》§4.1.1, §4.2.6");
            if (input.Type == FoundationType.Composite)
                sb.AppendLine("- JGJ 79-2012《建筑地基处理技术规范》§7.1");
            if (input.Type == FoundationType.Pile)
                sb.AppendLine("- JGJ 94-2008《建筑桩基技术规范》§5.2~5.5, §5.8~5.9");
            sb.AppendLine();

            // 工程参数
            sb.AppendLine("## 工程参数");
            sb.AppendLine();
            sb.AppendLine("| 参数 | 数值 | 单位 |");
            sb.AppendLine("|------|------|------|");
            if (ctx.UseAbsoluteElevation)
            {
                sb.AppendLine($"| 孔点高程（绝对） | {ctx.AbsBoreholeElevation:F3} | m |");
                sb.AppendLine($"| 结构±0.000（绝对） | {ctx.AbsStructureZero:F3} | m |");
            }
            sb.AppendLine($"| 孔点相对高程 | {input.RelativeElevation:F3} | m |");
            sb.AppendLine($"| 基础埋深 d | {input.FoundationDepth:F2} | m |");
            sb.AppendLine($"| 基础宽度 b | {input.FoundationWidth:F2} | m |");
            sb.AppendLine($"| 基础长度 l | {input.FoundationLength:F2} | m |");
            double lbRatio = input.FoundationWidth > 0 ? input.FoundationLength / input.FoundationWidth : 0;
            sb.AppendLine($"| l/b | {lbRatio:F2} | — |");
            sb.AppendLine($"| 覆土平均重度 γm | {input.GammaM:F1} | kN/m³ |");
            if (input.IsBoxFoundation)
                sb.AppendLine($"| 混凝土折算厚度 tc | {input.BoxConcreteThickness:F2} | m |");
            sb.AppendLine($"| 地基承载力特征值 fak | {input.BearingCapacity:F1} | kPa |");
            sb.AppendLine($"| 基底附加压力 p₀ | {input.AdditionalPressure:F1} | kPa |");

            if (input.Type == FoundationType.Composite)
            {
                sb.AppendLine($"| 复合地基承载力特征值 fspk | {input.CompositeBearingCapacity:F1} | kPa |");
                sb.AppendLine($"| 模量提高系数 ξ = fspk/fak | {input.Xi:F3} | — |");
                sb.AppendLine($"| 加固层深度 | {input.TreatedDepth:F2} | m |");
            }
            else if (input.Type == FoundationType.Pile)
            {
                sb.AppendLine($"| 桩长 | {input.PileLength:F2} | m |");
                sb.AppendLine($"| 桩径 | {input.PileDiameter:F2} | m |");
                sb.AppendLine($"| 桩距 | {input.PileSpacing:F2} | m |");
                sb.AppendLine($"| 承台尺寸 | {input.CapLength:F2}×{input.CapWidth:F2} | m |");
                sb.AppendLine($"| 总桩数 | {input.TotalPileCount} | 根 |");
            }

            double skipDepth = input.RelativeElevation + input.FoundationDepth;
            if (skipDepth > 0.001)
                sb.AppendLine($"| 孔口→基底跳过 | {skipDepth:F2} | m |");
            sb.AppendLine();

            // 地层参数表
            sb.AppendLine("## 地层参数");
            sb.AppendLine();
            var layers = ctx.OriginalSoilLayers.Count > 0 ? ctx.OriginalSoilLayers : input.SoilLayers;
            if (input.EnableRebound)
            {
                sb.AppendLine("| 编号 | 名称 | 厚度(m) | Es(MPa) | Eci(MPa) | 描述 |");
                sb.AppendLine("|------|------|---------|---------|----------|------|");
                foreach (var layer in layers)
                {
                    double eci = layer.Eci > 0 ? layer.Eci : layer.Es * input.EciEsiRatio;
                    string eciMark = layer.Eci <= 0 ? "*" : "";
                    sb.AppendLine($"| {layer.Id} | {layer.Name} | {layer.Thickness:F2} | {layer.Es:F1} | {eci:F1}{eciMark} | {layer.Description} |");
                }
                sb.AppendLine();
                sb.AppendLine("*: Eci 由 Es×倍率 自动估算");
            }
            else
            {
                sb.AppendLine("| 编号 | 名称 | 厚度(m) | Es(MPa) | 描述 |");
                sb.AppendLine("|------|------|---------|---------|------|");
                foreach (var layer in layers)
                    sb.AppendLine($"| {layer.Id} | {layer.Name} | {layer.Thickness:F2} | {layer.Es:F1} | {layer.Description} |");
            }
            sb.AppendLine();

            return sb.ToString();
        }

        #endregion

        #region §08/09 基底附加压力

        private string GenerateP0Section(ReportContext ctx)
        {
            var input = ctx.Input;
            var sb = new StringBuilder();

            double area = input.FoundationLength * input.FoundationWidth;
            double gammaMd = input.GammaM * input.FoundationDepth;
            string subType = input.IsBoxFoundation ? "箱型基础" : "独立基础";

            // ── γm 计算过程 ──
            sb.AppendLine("## 基底以上土的加权平均重度 γm");
            sb.AppendLine();
            sb.AppendLine("> γm——基础底面以上土的加权平均重度 (kN/m³)，位于地下水位以下的土层取有效重度");
            sb.AppendLine();
            AppendGammaMCalcDetail(sb, ctx);

            sb.AppendLine($"## 基底附加压力计算（{subType}）");
            sb.AppendLine();
            sb.AppendLine("> 依据 GB 50007-2011 第 5.2.4 条，荷载按准永久组合（§3.0.5 第 2 款）");
            sb.AppendLine();

            if (!ctx.IsP0Auto)
            {
                sb.AppendLine($"p₀ 取值方式：**用户直接输入**");
                sb.AppendLine();
                sb.AppendLine("| 参数 | 数值 | 单位 |");
                sb.AppendLine("|------|------|------|");
                sb.AppendLine($"| 基础面积 A = l×b | {input.FoundationLength:F2}×{input.FoundationWidth:F2} = {area:F2} | m² |");
                sb.AppendLine($"| 基础埋深 d | {input.FoundationDepth:F2} | m |");
                sb.AppendLine($"| γm | {input.GammaM:F2} | kN/m³ |");
                sb.AppendLine($"| **基底附加压力 p₀** | **{input.AdditionalPressure:F1}** | **kPa** |");
                sb.AppendLine();
                sb.AppendLine($"覆土自重压力 γm·d = {input.GammaM:F2}×{input.FoundationDepth:F2} = {gammaMd:F1} kPa");
                sb.AppendLine();
            }
            else if (input.IsBoxFoundation)
            {
                double gammaConcrete = 25.0;
                double boxWeight = gammaConcrete * input.BoxConcreteThickness * area;
                double fk = ctx.AxialForce;
                double pk = area > 0 ? (fk + boxWeight) / area : 0;

                sb.AppendLine($"p₀ 取值方式：**程序自动计算**");
                sb.AppendLine();
                sb.AppendLine("### 基础尺寸与荷载");
                sb.AppendLine();
                sb.AppendLine("| 参数 | 数值 | 单位 |");
                sb.AppendLine("|------|------|------|");
                sb.AppendLine($"| 基础长度 l | {input.FoundationLength:F2} | m |");
                sb.AppendLine($"| 基础宽度 b | {input.FoundationWidth:F2} | m |");
                sb.AppendLine($"| 基底面积 A = l×b | {area:F2} | m² |");
                sb.AppendLine($"| 基础埋深 d | {input.FoundationDepth:F2} | m |");
                sb.AppendLine($"| 上部结构竖向力 Fk | {fk:F1} | kN |");
                sb.AppendLine($"| 混凝土折算厚度 tc | {input.BoxConcreteThickness:F2} | m |");
                sb.AppendLine($"| 混凝土重度 γc | {gammaConcrete:F1} | kN/m³ |");
                sb.AppendLine($"| γm | {input.GammaM:F2} | kN/m³ |");
                sb.AppendLine();

                sb.AppendLine("### 箱型基础自重");
                sb.AppendLine();
                sb.AppendLine($"    Gk = γc·tc·A = {gammaConcrete:F1}×{input.BoxConcreteThickness:F2}×{area:F2} = {boxWeight:F1} kN");
                sb.AppendLine();

                sb.AppendLine("### 基底压力");
                sb.AppendLine();
                sb.AppendLine($"    pk = (Fk + Gk) / A = ({fk:F1} + {boxWeight:F1}) / {area:F2} = {pk:F1} kPa");
                sb.AppendLine();

                sb.AppendLine("### 基底附加压力");
                sb.AppendLine();
                sb.AppendLine($"    p₀ = pk − γm·d = {pk:F1} − {input.GammaM:F2}×{input.FoundationDepth:F2} = {pk:F1} − {gammaMd:F1} = {input.AdditionalPressure:F1} kPa");
                sb.AppendLine();
            }
            else
            {
                double fk = ctx.AxialForce;
                double gk = ctx.SelfWeightLoad;
                double pk = area > 0 ? (fk + gk) / area : 0;
                double dh = input.IndoorOutdoorDiff;
                double dEff = input.FoundationDepth - dh * 0.5;
                if (dEff < 0) dEff = 0;

                sb.AppendLine($"p₀ 取值方式：**程序自动计算**");
                sb.AppendLine();
                sb.AppendLine("### 基础尺寸与荷载");
                sb.AppendLine();
                sb.AppendLine("| 参数 | 数值 | 单位 |");
                sb.AppendLine("|------|------|------|");
                sb.AppendLine($"| 基础长度 l | {input.FoundationLength:F2} | m |");
                sb.AppendLine($"| 基础宽度 b | {input.FoundationWidth:F2} | m |");
                sb.AppendLine($"| 基底面积 A = l×b | {area:F2} | m² |");
                sb.AppendLine($"| 基础埋深 d | {input.FoundationDepth:F2} | m |");
                sb.AppendLine($"| 室内外高差 Δh | {dh:F2} | m |");
                sb.AppendLine($"| 上部结构竖向力 Fk | {fk:F1} | kN |");
                sb.AppendLine($"| γm | {input.GammaM:F2} | kN/m³ |");
                sb.AppendLine();

                sb.AppendLine("### 基础及覆土自重 Gk");
                sb.AppendLine();
                sb.AppendLine($"覆土平均厚度考虑室内外高差：d_eff = d − Δh/2 = {input.FoundationDepth:F2} − {dh:F2}/2 = {dEff:F2} m");
                sb.AppendLine();
                sb.AppendLine($"    Gk = γm × A × d_eff = {input.GammaM:F2} × {area:F2} × {dEff:F2} = {gk:F1} kN");
                sb.AppendLine();

                sb.AppendLine("### 基底压力");
                sb.AppendLine();
                sb.AppendLine($"    pk = (Fk + Gk) / A = ({fk:F1} + {gk:F1}) / {area:F2} = {pk:F1} kPa");
                sb.AppendLine();

                sb.AppendLine("### 基底附加压力");
                sb.AppendLine();
                sb.AppendLine($"    p₀ = pk − γm·d = {pk:F1} − {input.GammaM:F2}×{input.FoundationDepth:F2} = {pk:F1} − {gammaMd:F1} = {input.AdditionalPressure:F1} kPa");
                sb.AppendLine();
            }

            // p₀ 特殊值提示
            if (input.AdditionalPressure < -0.1)
            {
                sb.AppendLine("**注意**：p₀ < 0，基础为超补偿基础（挖除土重 > 建筑物荷载），");
                sb.AppendLine("地基沉降主要由回弹再压缩控制，应按 GB 50007 §5.3.10～5.3.11 计算。");
                sb.AppendLine();
            }
            else if (Math.Abs(input.AdditionalPressure) <= 0.1)
            {
                sb.AppendLine("**注意**：p₀ ≈ 0，基础为完全补偿基础，附加应力极小，沉降由回弹再压缩控制。");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        #endregion

        #region §01 天然地基沉降

        private string GenerateNaturalSettlement(ReportContext ctx)
        {
            var input = ctx.Input;
            var result = ctx.Result;
            var sb = new StringBuilder();

            sb.AppendLine("## 天然地基沉降计算");
            sb.AppendLine();
            sb.AppendLine("> 依据 GB 50007-2011 第 5.3.5 条");
            sb.AppendLine();

            // 公式
            sb.AppendLine("### 计算公式");
            sb.AppendLine();
            sb.AppendLine("采用分层总和法计算地基最终变形量：");
            sb.AppendLine();
            sb.AppendLine("    s = ψs·s' = ψs·Σ(p₀/Esi)·(zi·ᾱi - z(i-1)·ᾱ(i-1))    (5.3.5)");
            sb.AppendLine();
            sb.AppendLine("附加应力系数 α 采用 Newmark (1935) 公式；平均附加应力系数 ᾱ 采用解析积分公式。");
            sb.AppendLine();

            // 逐层计算表
            AppendLayerResultTable(sb, input, result);

            // 计算深度判定
            sb.AppendLine("### 计算深度判定");
            sb.AppendLine();
            sb.AppendLine("依据 GB 50007-2011 式 5.3.7：Δs'n ≤ 0.025·Σ Δs'i");
            sb.AppendLine();
            double b = input.FoundationWidth;
            sb.AppendLine($"简化公式（式 5.3.8）：zn = b(2.5 - 0.4·ln b) = {b:F2}×(2.5 - 0.4×ln{b:F2}) = {result.SimplifiedDepth:F1} m");
            sb.AppendLine();
            sb.AppendLine($"实际计算深度：zn = {result.CalculationDepth:F1} m");
            sb.AppendLine();

            // 当量模量
            sb.AppendLine("### 压缩模量当量值");
            sb.AppendLine();
            sb.AppendLine($"依据 GB 50007-2011 式 5.3.6：Ēs = ΣAi / Σ(Ai/Esi) = {result.EquivalentEs:F1} MPa");
            sb.AppendLine();

            // 经验系数
            AppendPsiSTable_GB50007(sb, result);

            // 结果
            sb.AppendLine("### 天然地基沉降结果");
            sb.AppendLine();
            sb.AppendLine($"- 理论沉降 s' = {result.TheoreticalSettlement:F2} mm");
            sb.AppendLine($"- 经验系数 ψs = {result.PsiS:F3}");
            sb.AppendLine($"- **压缩沉降 s = ψs × s' = {result.PsiS:F3} × {result.TheoreticalSettlement:F2} = {result.CompressionSettlement:F2} mm**");
            sb.AppendLine();

            return sb.ToString();
        }

        #endregion

        #region §02 复合地基承载力

        private string GenerateCompositeBearing(ReportContext ctx)
        {
            var br = ctx.BearingResult;
            if (br == null) return "";

            var input = ctx.Input;
            var sb = new StringBuilder();

            sb.AppendLine("## 复合地基承载力验算");
            sb.AppendLine();
            sb.AppendLine("> 依据 JGJ 79-2012 第 7.1.5 条");
            sb.AppendLine();

            double pileDiameter = input.PileDiameter > 0 ? input.PileDiameter : 0.5;
            double pileArea = Math.PI / 4 * pileDiameter * pileDiameter;
            double pilePerimeter = Math.PI * pileDiameter;

            // 单桩承载力
            sb.AppendLine("### 增强体单桩竖向承载力特征值（式 7.1.5-3）");
            sb.AppendLine();
            sb.AppendLine($"up = π×{pileDiameter:F3} = {pilePerimeter:F3} m，Ap = π/4×{pileDiameter:F3}² = {pileArea:F4} m²");
            sb.AppendLine();

            if (br.SideResistanceRows.Count > 0)
            {
                sb.AppendLine("| 土层 | qsi (kPa) | lpi (m) | qsi·lpi (kN/m) |");
                sb.AppendLine("|------|-----------|---------|----------------|");
                foreach (var row in br.SideResistanceRows)
                    sb.AppendLine($"| {row.LayerName} | {row.Qsi:F1} | {row.Thickness:F2} | {row.Product:F1} |");
                sb.AppendLine();
            }

            sb.AppendLine($"Ra = {br.Ra:F1} kN");
            sb.AppendLine();

            // 复合地基承载力
            sb.AppendLine("### 复合地基承载力特征值（式 7.1.5-2）");
            sb.AppendLine();
            sb.AppendLine($"fspk = λ·m·Ra/Ap + β·(1-m)·fsk = {br.Fspk:F1} kPa");
            sb.AppendLine();
            sb.AppendLine($"ξ = fspk/fak = {br.Fspk:F1}/{input.BearingCapacity:F1} = {input.Xi:F3}");
            sb.AppendLine();

            // 桩体强度
            sb.AppendLine("### 桩体强度验算（式 7.1.6-1）");
            sb.AppendLine();
            sb.AppendLine($"fcu ≥ 4·λ·Ra/Ap = 4×{br.Lambda:F2}×{br.Ra:F1}/{pileArea:F4} = {br.RequiredFcu:F1} kPa");
            sb.AppendLine();
            if (br.ActualFcu > 0)
            {
                sb.AppendLine($"实际 fcu = {br.ActualFcu:F1} kPa → {(br.FcuCheck ? "✓ 满足" : "✗ 不满足")}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        #endregion

        #region §03 复合地基沉降

        private string GenerateCompositeSettlement(ReportContext ctx)
        {
            var input = ctx.Input;
            var result = ctx.Result;
            var sb = new StringBuilder();

            sb.AppendLine("## 复合地基沉降计算");
            sb.AppendLine();
            sb.AppendLine("> 依据 JGJ 79-2012 第 7.1.7～7.1.8 条");
            sb.AppendLine();

            // 模量提高系数
            sb.AppendLine("### 压缩模量提高系数");
            sb.AppendLine();
            sb.AppendLine($"ξ = fspk/fak = {input.CompositeBearingCapacity:F1}/{input.BearingCapacity:F1} = {input.Xi:F3}（式 7.1.7）");
            sb.AppendLine();
            sb.AppendLine($"加固层深度 = {input.TreatedDepth:F2} m，加固层内各层 Esi 乘以 ξ = {input.Xi:F3}");
            sb.AppendLine();

            // 套用 §5.3.5
            sb.AppendLine("### 计算公式");
            sb.AppendLine();
            sb.AppendLine("套用 GB 50007-2011 式 5.3.5，加固层内 Esi,eff = ξ·Esi，加固层以下 Esi,eff = Esi");
            sb.AppendLine();

            // 逐层计算表
            AppendLayerResultTable(sb, input, result);

            // 当量模量
            sb.AppendLine("### 当量模量（加固层 + 下卧层）");
            sb.AppendLine();
            sb.AppendLine($"依据 JGJ 79-2012 式 7.1.8：Ēs = {result.EquivalentEs:F1} MPa");
            sb.AppendLine();

            // 经验系数 (JGJ 79 表 7.1.8)
            sb.AppendLine("### 沉降计算经验系数");
            sb.AppendLine();
            sb.AppendLine("依据 JGJ 79-2012 表 7.1.8：");
            sb.AppendLine();
            sb.AppendLine("| Es (MPa) | 4.0 | 7.0 | 15.0 | 20.0 | 35.0 |");
            sb.AppendLine("|----------|-----|-----|------|------|------|");
            sb.AppendLine("| ψs       | 1.0 | 0.7 | 0.4  | 0.25 | 0.2  |");
            sb.AppendLine();
            sb.AppendLine($"插值得：ψs = {result.PsiS:F3}");
            sb.AppendLine();

            // 结果
            sb.AppendLine("### 复合地基沉降结果");
            sb.AppendLine();
            sb.AppendLine($"- 理论沉降 s' = {result.TheoreticalSettlement:F2} mm");
            sb.AppendLine($"- 经验系数 ψs = {result.PsiS:F3}");
            sb.AppendLine($"- **压缩沉降 s = ψs × s' = {result.PsiS:F3} × {result.TheoreticalSettlement:F2} = {result.CompressionSettlement:F2} mm**");
            sb.AppendLine($"- 计算深度 zn = {result.CalculationDepth:F1} m");
            sb.AppendLine();

            return sb.ToString();
        }

        #endregion

        #region §04 桩基承载力

        private string GeneratePileBearing(ReportContext ctx)
        {
            var br = ctx.BearingResult;
            if (br == null) return "";

            var input = ctx.Input;
            var sb = new StringBuilder();

            sb.AppendLine("## 桩基承载力验算");
            sb.AppendLine();
            sb.AppendLine("> 依据 JGJ 94-2008 第 5.2～5.4 条");
            sb.AppendLine();

            double pilePerimeter = Math.PI * input.PileDiameter;
            double pileArea = Math.PI / 4 * input.PileDiameter * input.PileDiameter;

            // 单桩极限承载力
            sb.AppendLine("### 单桩竖向极限承载力标准值（式 5.3.5）");
            sb.AppendLine();
            sb.AppendLine($"u = π×{input.PileDiameter:F3} = {pilePerimeter:F3} m，Ap = π/4×{input.PileDiameter:F3}² = {pileArea:F4} m²");
            sb.AppendLine();

            if (br.SideResistanceRows.Count > 0)
            {
                sb.AppendLine("| 土层 | 土类 | 厚度 li (m) | qsi (kPa) | qsi·li (kN/m) |");
                sb.AppendLine("|------|------|-------------|-----------|---------------|");
                foreach (var row in br.SideResistanceRows)
                    sb.AppendLine($"| {row.LayerName} | {row.SoilType} | {row.Thickness:F2} | {row.Qsi:F1} | {row.Product:F1} |");
                sb.AppendLine();
            }

            sb.AppendLine($"Qsk = u·Σqsi·li = {br.Qsk:F1} kN");
            sb.AppendLine();
            sb.AppendLine($"Qpk = qp·Ap = {br.QpValue:F1}×{pileArea:F4} = {br.Qpk:F1} kN");
            sb.AppendLine();
            sb.AppendLine($"Quk = Qsk + Qpk = {br.Qsk:F1} + {br.Qpk:F1} = {br.Quk:F1} kN");
            sb.AppendLine();

            // 承载力特征值
            sb.AppendLine("### 单桩竖向承载力特征值（式 5.2.2）");
            sb.AppendLine();
            sb.AppendLine($"Ra = Quk/K = {br.Quk:F1}/2 = {br.Ra:F1} kN（安全系数 K = 2）");
            sb.AppendLine();

            // 验算
            sb.AppendLine("### 承载力验算");
            sb.AppendLine();
            if (br.Nk > 0)
            {
                string check = br.BearingCheck ? "≤" : ">";
                string result = br.BearingCheck ? "✓ 满足" : "✗ 不满足";
                sb.AppendLine($"Nk = {br.Nk:F1} kN {check} Ra = {br.Ra:F1} kN → {result}");
            }
            else
            {
                sb.AppendLine($"单桩竖向承载力特征值 Ra = {br.Ra:F1} kN");
            }
            sb.AppendLine();

            return sb.ToString();
        }

        #endregion

        #region §05 桩基沉降

        private string GeneratePileSettlement(ReportContext ctx)
        {
            var input = ctx.Input;
            var result = ctx.Result;
            var sb = new StringBuilder();

            sb.AppendLine("## 桩基沉降计算");
            sb.AppendLine();
            sb.AppendLine("> 依据 JGJ 94-2008 第 5.5.6～5.5.11 条（等效作用分层总和法）");
            sb.AppendLine();

            // 等效沉降系数
            sb.AppendLine("### 桩基等效沉降系数 ψe");
            sb.AppendLine();
            sb.AppendLine("依据 JGJ 94-2008 式 5.5.9：");
            sb.AppendLine();
            sb.AppendLine($"ψe = {result.PsiE:F3}");
            sb.AppendLine();

            // 计算公式
            sb.AppendLine("### 计算公式");
            sb.AppendLine();
            sb.AppendLine("    s = ψ·ψe·s' = ψ·ψe·Σ p₀/Esi·(zi·ᾱi - z(i-1)·ᾱ(i-1))    (5.5.6)");
            sb.AppendLine();
            sb.AppendLine("等效作用面位于桩端平面，等效作用面积为承台投影面积。");
            sb.AppendLine();

            // 逐层计算表
            AppendLayerResultTable(sb, input, result);

            // 深度判定
            sb.AppendLine("### 沉降计算深度判定");
            sb.AppendLine();
            sb.AppendLine("依据 JGJ 94-2008 式 5.5.8（应力比法）：σz ≤ 0.2σc");
            sb.AppendLine();
            sb.AppendLine($"计算深度：zn = {result.CalculationDepth:F1} m（从桩端平面起算）");
            sb.AppendLine();

            // 经验系数
            sb.AppendLine("### 桩基沉降计算经验系数");
            sb.AppendLine();
            sb.AppendLine("依据 JGJ 94-2008 表 5.5.11：");
            sb.AppendLine();
            sb.AppendLine("| Ēs (MPa) | ≤10 | 15 | 20 | 35 | ≥50 |");
            sb.AppendLine("|----------|-----|-----|-----|-----|-----|");
            sb.AppendLine("| ψ        | 2.0 | 0.9 | 0.65| 0.50| 0.40|");
            sb.AppendLine();
            sb.AppendLine($"当量模量 Ēs = {result.EquivalentEs:F1} MPa，插值得 ψ = {result.PsiS:F3}");
            sb.AppendLine();

            // 结果
            sb.AppendLine("### 桩基沉降结果");
            sb.AppendLine();
            sb.AppendLine($"- 理论沉降 s' = {result.TheoreticalSettlement:F2} mm");
            sb.AppendLine($"- 经验系数 ψ = {result.PsiS:F3}");
            sb.AppendLine($"- 等效系数 ψe = {result.PsiE:F3}");
            sb.AppendLine($"- **压缩沉降 s = ψ × ψe × s' = {result.PsiS:F3} × {result.PsiE:F3} × {result.TheoreticalSettlement:F2} = {result.CompressionSettlement:F2} mm**");
            sb.AppendLine();

            return sb.ToString();
        }

        #endregion

        #region §06 桩身结构

        private string GeneratePileStructural(ReportContext ctx)
        {
            var sr = ctx.StructuralResult;
            if (sr == null) return "";

            var input = ctx.Input;
            var sb = new StringBuilder();

            sb.AppendLine("## 桩身结构验算");
            sb.AppendLine();
            sb.AppendLine("> 依据 JGJ 94-2008 第 5.8～5.9 条");
            sb.AppendLine();

            // 受压
            sb.AppendLine("### 桩身受压承载力验算（式 5.8.2）");
            sb.AppendLine();
            sb.AppendLine("N ≤ ψc·φ·fc·A");
            sb.AppendLine();

            double pileArea = Math.PI / 4 * input.PileDiameter * input.PileDiameter;
            sb.AppendLine($"| 参数 | 数值 | 单位 |");
            sb.AppendLine($"|------|------|------|");
            sb.AppendLine($"| N | {sr.AxialForceDesign:F1} | kN |");
            sb.AppendLine($"| ψc | {sr.PsiC:F2} | — |");
            sb.AppendLine($"| φ | {sr.Phi:F2} | — |");
            sb.AppendLine($"| fc | {sr.Fc:F1} | kPa |");
            sb.AppendLine($"| A | {pileArea:F4} | m² |");
            sb.AppendLine();
            sb.AppendLine($"ψc·φ·fc·A = {sr.CompressCapacity:F1} kN");
            sb.AppendLine();
            string compCheck = sr.CompressCheck ? "✓ 满足" : "✗ 不满足";
            sb.AppendLine($"验算：N = {sr.AxialForceDesign:F1} kN → {compCheck}");
            sb.AppendLine();

            // 配筋
            if (sr.SteelArea > 0)
            {
                sb.AppendLine("### 桩身配筋");
                sb.AppendLine();
                sb.AppendLine($"配筋面积 As = {sr.SteelArea:F0} mm²，配筋率 ρ = {sr.SteelRatio:F2}%");
                sb.AppendLine();
                string steelCheck = sr.SteelRatioCheck ? "✓ 满足" : "✗ 不满足";
                sb.AppendLine($"{steelCheck}最小配筋率要求");
                sb.AppendLine();
            }

            // 结论
            if (!string.IsNullOrWhiteSpace(sr.Conclusion))
            {
                sb.AppendLine("### 桩身结构验算结论");
                sb.AppendLine();
                sb.AppendLine(sr.Conclusion);
                sb.AppendLine();
            }

            return sb.ToString();
        }

        #endregion

        #region §07 回弹再压缩

        private string GenerateRebound(ReportContext ctx)
        {
            var input = ctx.Input;
            var result = ctx.Result;
            var sb = new StringBuilder();

            sb.AppendLine("## 回弹再压缩变形计算");
            sb.AppendLine();
            sb.AppendLine("> 依据 GB 50007-2011 第 5.3.10～5.3.11 条");
            sb.AppendLine();

            // 物理过程说明
            sb.AppendLine("### 物理过程");
            sb.AppendLine();
            sb.AppendLine("1. **卸荷阶段**：基坑开挖移除覆土，基底处应力从 pc 降至 0，土体理论回弹量为 sc");
            sb.AppendLine("2. **施工期**：开挖到回填期间，实际完成回弹 η₁·sc；其中 η₂ 比例被基底整平清除（永久损失）");
            sb.AppendLine("3. **再加荷阶段**：基础施工后总压力为 p = p₀ + pc，产生再压缩 s'c (§5.3.11)");
            sb.AppendLine("4. **净变形** = s'c − η₂·η₁·sc（清除的回弹不可恢复）");
            sb.AppendLine();

            // 回弹参数
            sb.AppendLine("### 回弹参数");
            sb.AppendLine();
            sb.AppendLine($"- 覆土平均重度 γm = {input.GammaM:F1} kN/m³");
            sb.AppendLine($"- 覆土自重压力 pc = γm×d = {input.GammaM:F1}×{input.FoundationDepth:F2} = {result.OverburdenPressure:F1} kPa");
            sb.AppendLine($"- 再加荷总压力 p = p₀ + pc = {input.AdditionalPressure:F1} + {result.OverburdenPressure:F1} = {result.TotalReloadPressure:F1} kPa");
            sb.AppendLine($"- 再加荷比 R' = p/pc = {result.ReloadRatio:F3}");
            sb.AppendLine($"- Eci/Esi 默认倍率 = {input.EciEsiRatio:F1}");
            sb.AppendLine($"- 回弹经验系数 ψc = {input.PsiC:F2}");
            sb.AppendLine($"- 再压缩增大系数 κ = {input.Kappa:F2}");
            sb.AppendLine($"- 回弹完成比 η₁ = {input.Eta1:F2}");
            sb.AppendLine($"- 回弹清除比 η₂ = {input.Eta2:F2}");
            sb.AppendLine($"- §5.3.11 临界再加荷比 R'₀ = {input.R0Prime:F2}");
            sb.AppendLine($"- §5.3.11 临界再压缩比率 r'₀ = {input.r0Prime:F2}");
            sb.AppendLine($"- 深度终止比值 = {input.ReboundDepthRatio}");
            sb.AppendLine();

            // 回弹量
            sb.AppendLine("### 回弹量（§5.3.10）");
            sb.AppendLine();
            sb.AppendLine("$$s_c = \\psi_c \\sum \\frac{p_c}{E_{ci}} (z_i \\bar{\\alpha}_i - z_{i-1} \\bar{\\alpha}_{i-1})$$");
            sb.AppendLine();
            sb.AppendLine($"- 理论回弹量 sc = {result.ReboundSettlement:F3} mm");
            sb.AppendLine($"- 实际完成回弹 η₁·sc = {result.CompletedRebound:F3} mm");
            sb.AppendLine($"- 被清除（永久损失）η₂·η₁·sc = {result.ClearedRebound:F3} mm");
            sb.AppendLine();

            // 再压缩
            double p0 = input.AdditionalPressure;
            double p = result.TotalReloadPressure;
            double pc = result.OverburdenPressure;

            if (p <= pc)
            {
                double remainingRebound = result.ReboundSettlement - result.ClearedRebound;

                sb.AppendLine("### 再压缩（p ≤ pc，补偿/超补偿基础，§5.3.11 分段公式）");
                sb.AppendLine();
                sb.AppendLine($"R' = {result.ReloadRatio:F3}");
                sb.AppendLine();
                sb.AppendLine("§5.3.11 分段线性公式：");
                sb.AppendLine();
                if (result.ReloadRatio < input.R0Prime)
                    sb.AppendLine($"R' < R'₀，采用: s'c = r'₀·sc · R'/R'₀");
                else
                    sb.AppendLine($"R'₀ ≤ R' ≤ 1，采用: s'c = sc·[r'₀ + (κ-r'₀)/(1-R'₀)·(R'-R'₀)]");
                sb.AppendLine();
                sb.AppendLine($"**s'c = {result.RecompressionSettlement:F3} mm（方向↓）**");
                sb.AppendLine();
                sb.AppendLine("### 净沉降计算");
                sb.AppendLine();
                sb.AppendLine("土中剩余回弹 = 理论回弹 − 清除量（已永久移除的土体不参与再压缩）：");
                sb.AppendLine();
                sb.AppendLine($"$$剩余回弹 = s_c - \\eta_2 \\eta_1 s_c = {result.ReboundSettlement:F3} - {result.ClearedRebound:F3} = {remainingRebound:F3} \\text{{ mm}}$$");
                sb.AppendLine();
                sb.AppendLine("s'c 先抵消剩余回弹（恢复原始应力状态），仅超出部分为实际沉降：");
                sb.AppendLine();
                sb.AppendLine($"$$净沉降 = s'_c - 剩余回弹 = {result.RecompressionSettlement:F3} - {remainingRebound:F3} = {result.FinalSettlement:F2} \\text{{ mm}}$$");
                sb.AppendLine();
                string dir = result.FinalSettlement < 0 ? "净隆起↑" : result.FinalSettlement > 0 ? "净沉降↓" : "无变形";
                sb.AppendLine($"**最终沉降 s = {result.FinalSettlement:F2} mm（{dir}）**");
            }
            else
            {
                sb.AppendLine("### 非补偿基础沉降（p > pc）");
                sb.AppendLine();
                sb.AppendLine("#### 段 1：回弹再压缩净沉降（0 → pc）");
                sb.AppendLine();
                sb.AppendLine("再加荷从 0 恢复至原始覆土应力 pc。虽然应力恢复到开挖前水平，但施工期间被整平清除的回弹土体已永久移除，地面无法回到原始标高，因此产生净沉降：");
                sb.AppendLine();
                double sPrimeCFull = result.RecompressionSettlement;
                double remainRb = result.ReboundSettlement - result.ClearedRebound;
                sb.AppendLine($"$$s'_c(R'=1) = s_c \\cdot \\kappa = {result.ReboundSettlement:F3} \\times {input.Kappa:F2} = {sPrimeCFull:F3} \\text{{ mm}}$$");
                sb.AppendLine();
                sb.AppendLine($"$$剩余回弹 = s_c - \\eta_2 \\eta_1 s_c = {result.ReboundSettlement:F3} - {result.ClearedRebound:F3} = {remainRb:F3} \\text{{ mm}}$$");
                sb.AppendLine();
                sb.AppendLine($"$$段1净沉降 = s'_c - 剩余回弹 = {sPrimeCFull:F3} - {remainRb:F3} = {result.NetReboundSettlement:F3} \\text{{ mm}}$$");
                sb.AppendLine();
                sb.AppendLine("#### 段 2：附加应力沉降（§5.3.5）");
                sb.AppendLine();
                sb.AppendLine($"超出原始覆土压力的部分：p − pc = p₀ = {input.AdditionalPressure:F1} kPa");
                sb.AppendLine();
                sb.AppendLine($"按 §5.3.5 分层总和法，以 p₀ = {input.AdditionalPressure:F1} kPa 计算基底以下各土层变形：");
                sb.AppendLine();
                sb.AppendLine($"段2压缩沉降 = {result.CompressionSettlement:F2} mm");
                sb.AppendLine();
                sb.AppendLine($"#### 最终沉降");
                sb.AppendLine();
                sb.AppendLine($"$$s = 段1 + 段2 = {result.NetReboundSettlement:F3} + {result.CompressionSettlement:F2} = {result.FinalSettlement:F2} \\text{{ mm}}$$");
                sb.AppendLine();
                sb.AppendLine($"**最终沉降 s = {result.FinalSettlement:F2} mm**");
            }
            sb.AppendLine();

            // 汇总
            sb.AppendLine("### 回弹再压缩汇总");
            sb.AppendLine();
            sb.AppendLine("| 参数 | 数值 | 单位 | 说明 |");
            sb.AppendLine("|------|------|------|------|");
            sb.AppendLine($"| 理论回弹 sc | {result.ReboundSettlement:F3} | mm | §5.3.10 |");
            sb.AppendLine($"| 完成回弹 η₁sc | {result.CompletedRebound:F3} | mm | 施工期实际回弹 |");
            sb.AppendLine($"| 清除量 η₂η₁sc | {result.ClearedRebound:F3} | mm | 基底整平永久损失 |");
            sb.AppendLine($"| 剩余回弹 | {(result.ReboundSettlement - result.ClearedRebound):F3} | mm | sc − 清除量 |");
            sb.AppendLine($"| 再压缩 s'c | {result.RecompressionSettlement:F3} | mm | §5.3.11 |");
            if (p <= pc)
                sb.AppendLine($"| 净沉降 | {result.NetReboundSettlement:F3} | mm | s'c − 剩余回弹 |");
            else
            {
                sb.AppendLine($"| 段1净沉降 | {result.NetReboundSettlement:F3} | mm | s'c − 剩余回弹 |");
                sb.AppendLine($"| 段2压缩 | {result.CompressionSettlement:F2} | mm | p₀ 引起(§5.3.5) |");
            }
            sb.AppendLine($"| **最终沉降 s** | **{result.FinalSettlement:F2}** | **mm** | — |");
            sb.AppendLine();

            return sb.ToString();
        }

        #endregion

        #region §10 结论

        private string GenerateConclusion(ReportContext ctx)
        {
            var input = ctx.Input;
            var result = ctx.Result;
            var sb = new StringBuilder();

            sb.AppendLine("## 计算结论与建议");
            sb.AppendLine();

            // 结果汇总表
            sb.AppendLine("### 计算结果汇总");
            sb.AppendLine();
            sb.AppendLine("| 计算项目 | 结果 | 单位 | 规范条文 |");
            sb.AppendLine("|----------|------|------|----------|");

            string standard;
            switch (input.Type)
            {
                case FoundationType.Natural: standard = "GB 50007 §5.3.5"; break;
                case FoundationType.Composite: standard = "JGJ 79 §7.1.7"; break;
                case FoundationType.Pile: standard = "JGJ 94 §5.5.6"; break;
                default: standard = "GB 50007 §5.3.5"; break;
            }

            if (input.AdditionalPressure > 0)
                sb.AppendLine($"| 压缩沉降 | {result.CompressionSettlement:F2} | mm | {standard} |");

            if (result.HasRebound)
            {
                sb.AppendLine($"| 理论回弹 sc | {result.ReboundSettlement:F3} | mm | GB 50007 §5.3.10 |");
                sb.AppendLine($"| 清除量 η₂η₁sc | {result.ClearedRebound:F3} | mm | — |");
                sb.AppendLine($"| 再压缩 s'c | {result.RecompressionSettlement:F3} | mm | GB 50007 §5.3.11 |");
                sb.AppendLine($"| 净回弹段 | {result.NetReboundSettlement:F3} | mm | — |");
            }

            sb.AppendLine($"| **最终沉降** | **{result.FinalSettlement:F2}** | **mm** | — |");
            sb.AppendLine();

            // 变形验算
            sb.AppendLine("### 变形验算");
            sb.AppendLine();
            sb.AppendLine("依据 GB 55003-2021 §4.2.6：地基变形计算值不应大于地基变形允许值。");
            sb.AppendLine();
            sb.AppendLine($"计算沉降量 = {result.FinalSettlement:F2} mm");
            sb.AppendLine();
            sb.AppendLine("变形允许值应根据上部结构对地基变形的适应能力和使用要求确定。");
            sb.AppendLine();

            // 建议
            sb.AppendLine("### 设计建议");
            sb.AppendLine();
            if (result.FinalSettlement < 0)
                sb.AppendLine("计算结果为负值（净隆起），建议关注施工期间基底隆起对上部结构的影响。");
            else if (result.FinalSettlement < 10)
                sb.AppendLine("沉降量较小，地基变形满足一般建筑物要求。");
            else if (result.FinalSettlement < 50)
                sb.AppendLine("沉降量中等，建议核实上部结构对变形的适应能力。");
            else
                sb.AppendLine("沉降量较大，建议优化地基处理方案或调整基础型式。");
            sb.AppendLine();

            sb.AppendLine("---");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(ctx.Author) || !string.IsNullOrWhiteSpace(ctx.Reviewer))
                sb.AppendLine($"计算人：{ctx.Author}　　审核人：{ctx.Reviewer}");
            sb.AppendLine($"日期：{ctx.Date}");
            sb.AppendLine();

            return sb.ToString();
        }

        #endregion

        #region 辅助方法

        /// <summary>输出 γm 加权平均重度的逐层计算过程</summary>
        private void AppendGammaMCalcDetail(StringBuilder sb, ReportContext ctx)
        {
            var input = ctx.Input;
            var layers = ctx.OriginalSoilLayers.Count > 0 ? ctx.OriginalSoilLayers : input.SoilLayers;
            if (layers == null || layers.Count == 0)
            {
                sb.AppendLine($"γm = {input.GammaM:F2} kN/m³（无土层数据，取用户设定值）");
                sb.AppendLine();
                return;
            }

            double relElev = input.RelativeElevation;
            double d = input.FoundationDepth;
            double targetStart = relElev;
            double targetEnd = relElev + d;
            double gwDepth = ctx.HasGroundwater ? ctx.GroundwaterDepth : double.MaxValue;
            const double gammaW = 9.8;
            const double defaultGamma = 18.0;

            sb.AppendLine("$$\\gamma_m = \\frac{\\sum \\gamma_i \\cdot h_i}{\\sum h_i}$$");
            sb.AppendLine();
            if (ctx.HasGroundwater)
                sb.AppendLine($"地下水位埋深 dw = {ctx.GroundwaterDepth:F2} m，水位以下取有效重度 γ' = γ − γw（γw = 9.8 kN/m³）");
            else
                sb.AppendLine("无地下水，各层取天然重度");
            sb.AppendLine();
            sb.AppendLine("| 地层 | 深度 (m) | γ (kN/m³) | γ' (kN/m³) | 计算γ | h (m) | γ×h |");
            sb.AppendLine("|------|----------|-----------|------------|-------|-------|------|");

            double totalWeight = 0;
            double totalThick = 0;
            double currentDepth = 0;

            foreach (var layer in layers)
            {
                if (layer.Thickness <= 0) continue;
                double layerTop = currentDepth;
                double layerBottom = currentDepth + layer.Thickness;
                currentDepth = layerBottom;

                if (layerBottom <= targetStart) continue;
                if (layerTop >= targetEnd) break;

                double top = Math.Max(layerTop, targetStart);
                double bot = Math.Min(layerBottom, targetEnd);
                if (bot - top <= 0) continue;

                double gamma = layer.Gamma > 0 ? layer.Gamma : defaultGamma;
                double gammaEff = Math.Max(gamma - gammaW, 1.0);
                string name = $"{layer.Id} {layer.Name}";

                if (gwDepth >= bot)
                {
                    double h = bot - top;
                    double contrib = gamma * h;
                    totalWeight += contrib;
                    totalThick += h;
                    sb.AppendLine($"| {name} | {top:F2}～{bot:F2} | {gamma:F1} | — | {gamma:F1} | {h:F2} | {contrib:F1} |");
                }
                else if (gwDepth <= top)
                {
                    double h = bot - top;
                    double contrib = gammaEff * h;
                    totalWeight += contrib;
                    totalThick += h;
                    sb.AppendLine($"| {name} | {top:F2}～{bot:F2} | {gamma:F1} | {gammaEff:F1} | {gammaEff:F1} | {h:F2} | {contrib:F1} |");
                }
                else
                {
                    double hAbove = gwDepth - top;
                    double hBelow = bot - gwDepth;
                    double contribAbove = gamma * hAbove;
                    double contribBelow = gammaEff * hBelow;
                    totalWeight += contribAbove + contribBelow;
                    totalThick += hAbove + hBelow;
                    sb.AppendLine($"| {name}(水位上) | {top:F2}～{gwDepth:F2} | {gamma:F1} | — | {gamma:F1} | {hAbove:F2} | {contribAbove:F1} |");
                    sb.AppendLine($"| {name}(水位下) | {gwDepth:F2}～{bot:F2} | {gamma:F1} | {gammaEff:F1} | {gammaEff:F1} | {hBelow:F2} | {contribBelow:F1} |");
                }
            }

            sb.AppendLine($"| **合计** | | | | | **{totalThick:F2}** | **{totalWeight:F1}** |");
            sb.AppendLine();

            if (totalThick > 0)
            {
                double calcGm = totalWeight / totalThick;
                sb.AppendLine($"γm = {totalWeight:F1} / {totalThick:F2} = **{calcGm:F2} kN/m³**");
            }
            else
            {
                sb.AppendLine($"γm = {input.GammaM:F2} kN/m³（无有效土层参与计算）");
            }
            sb.AppendLine();
        }

        private void AppendLayerResultTable(StringBuilder sb, SettlementInput input, SettlementResult result)
        {
            if (result.LayerResults == null || result.LayerResults.Count == 0) return;

            double lbRatio = input.FoundationWidth > 0 ? input.FoundationLength / input.FoundationWidth : 0;

            sb.AppendLine("### 逐层计算结果（表 5-7 格式）");
            sb.AppendLine();
            double halfB = input.FoundationWidth * 0.5;
            sb.AppendLine($"p₀ = {input.AdditionalPressure:F1} kPa，b = {input.FoundationWidth:F2} m，0.5b = {halfB:F2} m，l/b = {lbRatio:F2}");
            sb.AppendLine();
            sb.AppendLine("| # | 地层 | z(m) | l/b | z/0.5b | ᾱ | 4ᾱ | 4ᾱz(mm) | Δ4ᾱz | p₀/Es | Δs' | Σ(mm) |");
            sb.AppendLine("|---|------|------|-----|--------|------|------|---------|------|-------|-----|-------|");

            foreach (var r in result.LayerResults)
            {
                string marker = r.WithinDepth ? "" : " *";
                string ratio = r.DepthCheckRatio > 0 ? $" ({r.DepthCheckRatio:F4})" : "";
                sb.AppendLine($"| {r.Index} | {r.LayerId} | {r.Zi:F2} | {r.M:F2} | {r.NHalf:F2} | {r.AlphaBarCorner:F4} | {r.AlphaBar:F4} | {r.AlphaBarZi:F1} | {r.ZAlphaBarDiff:F1} | {r.P0overEs:F4} | {r.DeltaS:F2}{marker} | {r.CumulativeDeltaS:F2}{ratio} |");
            }
            sb.AppendLine();
            if (result.LayerResults.Any(r => !r.WithinDepth))
                sb.AppendLine("*: 超出计算深度，仅供参考");

            sb.AppendLine();
            sb.AppendLine($"Ēs = ΣAi / Σ(Ai/Esi) = {result.EquivalentEs:F2} MPa");
            sb.AppendLine();
        }

        private void AppendPsiSTable_GB50007(StringBuilder sb, SettlementResult result)
        {
            sb.AppendLine("### 沉降计算经验系数");
            sb.AppendLine();
            sb.AppendLine("依据 GB 50007-2011 表 5.3.5：");
            sb.AppendLine();
            sb.AppendLine("| Ēs (MPa) | 2.5 | 4.0 | 7.0 | 15.0 | 20.0 |");
            sb.AppendLine("|----------|-----|-----|-----|------|------|");
            sb.AppendLine("| p₀ > 0.75fak | 1.4 | 1.3 | 1.0 | 0.4  | 0.2  |");
            sb.AppendLine("| p₀ ≤ 0.75fak | 1.1 | 1.0 | 0.7 | 0.4  | 0.2  |");
            sb.AppendLine();
            string pressureDesc = result.PressureRatio > 0.75
                ? $"p₀/fak = {result.PressureRatio:F2}（> 0.75，取高压行）"
                : $"p₀/fak = {result.PressureRatio:F2}（≤ 0.75，取低压行）";
            sb.AppendLine(pressureDesc);
            sb.AppendLine();
            sb.AppendLine($"插值得：ψs = {result.PsiS:F3}");
            sb.AppendLine();
        }

        #endregion
    }
}
