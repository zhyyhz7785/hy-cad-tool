using System;
using System.Collections.Generic;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using HyCADTool.Features.SpongeCity.Domain.Models;
using static HyCADTool.Features.SpongeCity.Infrastructure.OpenXmlSheetHelper;

namespace HyCADTool.Features.SpongeCity.Infrastructure
{
    /// <summary>
    /// 7 Sheet 海绵城市计算表 .xlsx 生成器（对齐 gen_sponge_housing_calc.py 行号契约）：
    /// 行号契约见 SKILL.md 同名条目 / 设计大纲 §五~§十。
    /// 关键引用：基本参数!C9 / G6 / G7 / G8 / G12 / G26；下垫面分析!C6~C15 / C16 / E19；
    /// 设施配置!D6~D13 / E6~E13 / E14；污染削减率验算!E16；径流系数与外排!D6~D14。
    /// </summary>
    public static class SpongeCalcWorkbookBuilder
    {
        public static void Build(SpongeProjectInput input, SpongeResult result, string filePath)
        {
            using (var doc = SpreadsheetDocument.Create(filePath, SpreadsheetDocumentType.Workbook))
            {
                var wbPart = doc.AddWorkbookPart();
                wbPart.Workbook = new Workbook();
                var sheets = wbPart.Workbook.AppendChild(new Sheets());

                var stylesPart = wbPart.AddNewPart<WorkbookStylesPart>();
                stylesPart.Stylesheet = BuildStylesheet();
                stylesPart.Stylesheet.Save();

                BuildSheetBasic(wbPart, sheets, input);
                BuildSheetSurface(wbPart, sheets, input);
                BuildSheetRunoff(wbPart, sheets);
                BuildSheetVolume(wbPart, sheets);
                BuildSheetPollution(wbPart, sheets, input);
                BuildSheetFacility(wbPart, sheets, input);
                BuildSheetSummary(wbPart, sheets);

                wbPart.Workbook.Save();
            }
        }

        // ────────────────────────────────────────────────
        // Sheet1：基本参数（左列 C 存值；右列 G 存值；行号契约固定）
        // ────────────────────────────────────────────────
        private static void BuildSheetBasic(WorkbookPart wbPart, Sheets sheets, SpongeProjectInput input)
        {
            var (ws, data) = AddSheet(wbPart, sheets, "基本参数", 1);
            SetColumnWidths(ws, new[]
            {
                (1, 4.0), (2, 28.0), (3, 14.0), (4, 26.0),
                (5, 4.0), (6, 28.0), (7, 14.0), (8, 26.0),
            });

            // 标题
            var r1 = EnsureRow(data, 1);
            Write(ws, r1, 1, "海绵城市雨水控制利用计算总表  （住宅 / 商业小区）", S_TITLE);
            Merge(ws, 1, 1, 1, 8);
            SetRowHeight(data, 1, 24);

            var r2 = EnsureRow(data, 2);
            Write(ws, r2, 1, "■ 蓝色=可调输入  ■ 灰色=自动计算  ■ 黄色=关键结果  ■ 绿色=达标  ■ 红色=不达标", S_SMALL);
            Merge(ws, 2, 1, 2, 8);

            // 段落标题
            var r4 = EnsureRow(data, 4);
            Write(ws, r4, 1, "一、项目信息", S_SUBTITLE);
            Merge(ws, 4, 1, 4, 4);
            Write(ws, r4, 5, "二、设计控制目标（DB13 §3.0.4）", S_SUBTITLE);
            Merge(ws, 4, 5, 4, 8);

            // 表头
            var r5 = EnsureRow(data, 5);
            string[] hL = { "", "项目要素", "数值/取值", "说明" };
            string[] hR = { "", "控制指标", "数值", "依据" };
            for (int i = 0; i < 4; i++)
            {
                Write(ws, r5, i + 1, hL[i], S_HEADER);
                Write(ws, r5, i + 5, hR[i], S_HEADER);
            }

            // 左列：项目信息（row 6~19）—— 行号契约：C9=F, C11=建筑占地, C13=绿地率, C14=绿地面积
            (string name, object val, string note)[] proj =
            {
                ("项目名称",              input.ProjectName,                       "用户填写"),
                ("建设地点",              input.Location,                          "市/区/县"),
                ("用地类型",              ProjectTypeLabel(input.ProjectType),     "R2/B/B+R 等"),
                ("总用地面积 F (m²)",     input.TotalAreaM2,                       "项目红线内"),         // C9 ★
                ("总用地面积 F (hm²)",    "=ROUND(C9/10000,4)",                    "自动换算"),           // C10
                ("建筑占地面积 (m²)",     input.BuildingFootprintM2,               "建筑屋面投影"),       // C11
                ("建筑密度 (%)",          "=ROUND(C11/C9,4)",                      "自动计算"),           // C12
                ("绿地率 (%)",            input.GreenRatio,                        "GB50180 ≥30%(R2)"),  // C13
                ("绿地面积 (m²)",         "=ROUND(C13*C9,0)",                      "自动"),               // C14
                ("地下车库占地比例 (%)",  0.40,                                    "影响下沉式绿地适用"),
                ("机动车出入口数 (处)",   2.0,                                     "影响透水铺装"),
                ("项目阶段",              "施工图阶段",                            "方案/初设/施工图"),
                ("设计单位",              string.IsNullOrWhiteSpace(input.Designer) ? "—" : input.Designer, "用户填写"),
                ("设计日期",              string.IsNullOrWhiteSpace(input.Date) ? DateTime.Now.ToString("yyyy-MM") : input.Date, "—"),
            };
            for (int i = 0; i < proj.Length; i++)
            {
                uint r = (uint)(6 + i);
                var row = EnsureRow(data, r);
                Write(ws, row, 1, i + 1, S_CALC);
                Write(ws, row, 2, proj[i].name, S_NORMAL);
                bool isFormula = proj[i].val is string s && s.StartsWith("=");
                Write(ws, row, 3, proj[i].val, isFormula ? S_CALC : S_INPUT);
                Write(ws, row, 4, proj[i].note, S_SMALL);
            }

            // 右列：设计控制目标（row 6~19）—— G6=α, G7=h_y, G8=ηSS, G12=V需 ★, G26=下沉深
            (string name, object val, string note)[] tgt =
            {
                ("年径流总量控制率目标 α (%)",        input.AlphaTarget,                                                 "DB13 表3.0.4 取70~85%"),       // G6
                ("对应设计降雨厚度 H (mm)",            input.DesignRainfallMm,                                            "DB13 附录A 衡水/沧州取值"),     // G7
                ("年径流污染削减率 ηSS (%)",          input.EtaSsTarget,                                                 "DB13 §3.0.4 一般≥50%"),        // G8
                ("初期雨水弃流厚度 (mm)",              input.InitialDiscardMm,                                            "DB13 §4.4.4"),                  // G9
                ("雨水回用要求",                        "绿化/道浇/景观",                                                  "用户选择"),                     // G10
                ("外排峰值流量控制重现期 (年)",       (double)input.ReturnPeriodYear,                                    "DB13 §3.0.6"),                  // G11
                ("控制目标体积 V需 (m³)",              "=ROUND(10*G7*'下垫面分析'!E19*C10,2)",                            "V=10·H·ψc·F(hm²)"),            // G12 ★
                ("单位汇水面积需控制水量 (m³/hm²)",   "=IF(C10=0,0,ROUND(G12/C10,2))",                                  "自动"),                         // G13
                ("控制目标对应 SS 削减率",            "=G8",                                                             "等于 ηSS 目标"),                // G14
                ("是否大型公共建筑(≥2万㎡)",          "否",                                                              "DB13 §5.0.5"),
                ("是否分散式雨水利用工程",            "是",                                                              "DB13 §3.0.5"),
                ("是否单独编制专项设计",              "是",                                                              "DB13 §3.0.3"),
                ("设计降雨频率核对",                  "P=年值",                                                          "—"),
                ("是否衡水本地项目",                  "是",                                                              "决定α/H/η取值"),
            };
            for (int i = 0; i < tgt.Length; i++)
            {
                uint r = (uint)(6 + i);
                var row = EnsureRow(data, r);
                Write(ws, row, 5, i + 1, S_CALC);
                Write(ws, row, 6, tgt[i].name, S_NORMAL);
                bool isFormula = tgt[i].val is string s && s.StartsWith("=");
                bool isKey = tgt[i].name.Contains("V需");
                uint style = isKey ? S_RESULT : (isFormula ? S_CALC : S_INPUT);
                Write(ws, row, 7, tgt[i].val, style);
                Write(ws, row, 8, tgt[i].note, S_SMALL);
            }

            // 三、衡水地区参数库（row 21 title, row 22 header, row 23~30 data）
            var r21 = EnsureRow(data, 21);
            Write(ws, r21, 1, "三、衡水地区参数库（DB13 附录A 沧州/石家庄内插）", S_SUBTITLE);
            Merge(ws, 21, 1, 21, 4);

            var r22 = EnsureRow(data, 22);
            string[] hsHead = { "", "参数", "数值", "依据" };
            for (int i = 0; i < 4; i++) Write(ws, r22, i + 1, hsHead[i], S_HEADER);

            (string name, double val, string note)[] hs =
            {
                ("多年平均降雨量 (mm)",              input.AnnualRainfallMm, "衡水气象资料（参考）"),
                ("70%控制率对应 H₇₀ (mm)",           19.7,                   "石家庄20.7/沧州19.0 内插"),
                ("75%控制率对应 H₇₅ (mm)",           22.0,                   "石家庄23.0/沧州21.0 内插"),
                ("80%控制率对应 H₈₀ (mm)",           25.0,                   "石家庄26.0/沧州23.5 内插"),
                ("85%控制率对应 H₈₅ (mm)",           28.0,                   "石家庄29.5/沧州27.0 内插"),
                ("5min设计降雨强度 (mm/min)",        1.45,                   "DB13 附录A1.2 估值"),
                ("暴雨重现期 P=3年雨力 q",           input.StormIntensity,   "L/(s·hm²)，3a-15min"),  // C29
                ("土壤渗透系数典型值 K (mm/h)",      input.SoilPermeability, "粉土，需现场实测复核"), // C30
            };
            for (int i = 0; i < hs.Length; i++)
            {
                uint r = (uint)(23 + i);
                var row = EnsureRow(data, r);
                Write(ws, row, 1, i + 1, S_CALC);
                Write(ws, row, 2, hs[i].name, S_NORMAL);
                Write(ws, row, 3, hs[i].val, S_INPUT);
                Write(ws, row, 4, hs[i].note, S_SMALL);
            }

            // 四、参考造价费率（row 21 right, row 22 header, row 23~29 data）
            Write(ws, r21, 5, "四、参考造价费率（与预算表联动）", S_SUBTITLE);
            Merge(ws, 21, 5, 21, 8);
            string[] feeHead = { "", "费率名称", "费率", "说明" };
            for (int i = 0; i < 4; i++) Write(ws, r22, i + 5, feeHead[i], S_HEADER);

            (string name, double val, string note)[] fees =
            {
                ("单位 m² 海绵投资估算 (元)", input.UnitSpongeCost, "指南附录3 60~120"),  // G23 ★
                ("绿地率限值 (居住区)",        0.30,                 "GB50180 §4.0.5"),
                ("透水铺装率限值",             0.70,                 "DB13 §5.0.6"),
                ("下沉式绿地下沉深度 (mm)",   input.SinkenDepthMm,  "DB13 §5.0.5 一般100~200"), // G26 ★
                ("地下空间顶板覆土厚度 (m)",  1.5,                  "≥1.2m 可作下沉式绿地"),
                ("雨水回用水池容积/m²建面 (L)", 5.0,                "指南建议 3~8"),
                ("溢流口流量系数",             0.62,                 "圆形雨水口取值"),
            };
            for (int i = 0; i < fees.Length; i++)
            {
                uint r = (uint)(23 + i);
                var row = EnsureRow(data, r);
                Write(ws, row, 5, i + 1, S_CALC);
                Write(ws, row, 6, fees[i].name, S_NORMAL);
                Write(ws, row, 7, fees[i].val, S_INPUT);
                Write(ws, row, 8, fees[i].note, S_SMALL);
            }
        }

        // ────────────────────────────────────────────────
        // Sheet2：下垫面分析（C6~C15 面积；C16 合计；E19 ψc ★）
        // ────────────────────────────────────────────────
        private static void BuildSheetSurface(WorkbookPart wbPart, Sheets sheets, SpongeProjectInput input)
        {
            var (ws, data) = AddSheet(wbPart, sheets, "下垫面分析", 2);
            SetColumnWidths(ws, new[]
            {
                (1, 4.0), (2, 28.0), (3, 14.0), (4, 12.0), (5, 14.0), (6, 14.0), (7, 32.0),
            });

            var r1 = EnsureRow(data, 1);
            Write(ws, r1, 1, "下垫面分类与综合径流系数 ψc", S_TITLE);
            Merge(ws, 1, 1, 1, 7);
            SetRowHeight(data, 1, 24);

            var r3 = EnsureRow(data, 3);
            Write(ws, r3, 1, "依据：DB13(J)8457 表4.2.1-3；指南表4-3；GB50014 表4.2.1", S_SMALL);
            Merge(ws, 3, 1, 3, 7);

            var r5 = EnsureRow(data, 5);
            string[] head = { "序号", "下垫面类型", "面积 A (m²)", "占比", "雨量径流系数 ψ", "面积·ψ (m²)", "备注" };
            for (int i = 0; i < head.Length; i++) Write(ws, r5, i + 1, head[i], S_HEADER);

            // 10 类下垫面 row 6~15
            var entries = SurfaceCatalog.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                uint r = (uint)(6 + i);
                var row = EnsureRow(data, r);
                var ent = entries[i];
                // 找面板/输入里此 code 的实际值
                double area = 0; double psi = ent.DefaultPsi;
                foreach (var s in input.Surfaces)
                {
                    if (s.Code == ent.Code) { area = s.Area; psi = s.Psi; break; }
                }
                Write(ws, row, 1, ent.Code, S_CALC);
                Write(ws, row, 2, ent.Name, S_NORMAL);
                Write(ws, row, 3, area, S_INPUT);
                Write(ws, row, 4, $"=IF($C$16=0,0,C{r}/$C$16)", S_CALC);
                Write(ws, row, 5, psi, S_INPUT);
                Write(ws, row, 6, $"=C{r}*E{r}", S_CALC);
                Write(ws, row, 7, ent.Note, S_SMALL);
            }

            // 合计行 row 16
            var r16 = EnsureRow(data, 16);
            Write(ws, r16, 2, "合 计", S_BOLD);
            Write(ws, r16, 3, "=SUM(C6:C15)", S_RESULT);
            Write(ws, r16, 4, "=SUM(D6:D15)", S_RESULT);
            Write(ws, r16, 5, "—", S_BOLD);
            Write(ws, r16, 6, "=SUM(F6:F15)", S_RESULT);
            Write(ws, r16, 7, "Σ(Ai·ψi)", S_BOLD);

            // 一致性校核 row 17
            var r17 = EnsureRow(data, 17);
            Write(ws, r17, 2, "与基本参数 F 一致性校核", S_BOLD);
            Write(ws, r17, 3, "=C16-基本参数!C9", S_CALC);
            Write(ws, r17, 4, "=IF(C17=0,\"OK\",\"差额≠0\")", S_BOLD);
            Write(ws, r17, 5, "应=0", S_SMALL);

            // 标题 row 18
            var r18 = EnsureRow(data, 18);
            Write(ws, r18, 1, "下垫面综合径流系数 ψc = Σ(Ai·ψi) / ΣAi", S_SUBTITLE);
            Merge(ws, 18, 1, 18, 7);

            // ψc row 19 ★（E19 是引用契约）
            var r19 = EnsureRow(data, 19);
            Write(ws, r19, 1, "★", S_RESULT);
            Write(ws, r19, 2, "综合雨量径流系数 ψc", S_RESULT);
            Write(ws, r19, 5, "=IF(C16=0,0,ROUND(F16/C16,3))", S_RESULT);
            Write(ws, r19, 6, "← 引用至 调蓄/校核", S_SMALL);
        }

        // ────────────────────────────────────────────────
        // Sheet3：径流系数与外排（D6~D16；D11 = Q ★）
        // ────────────────────────────────────────────────
        private static void BuildSheetRunoff(WorkbookPart wbPart, Sheets sheets)
        {
            var (ws, data) = AddSheet(wbPart, sheets, "径流系数与外排", 3);
            SetColumnWidths(ws, new[] { (1, 4.0), (2, 32.0), (3, 30.0), (4, 18.0), (5, 32.0) });

            var r1 = EnsureRow(data, 1);
            Write(ws, r1, 1, "径流系数与外排流量计算  Q = Ψm′·q·F", S_TITLE);
            Merge(ws, 1, 1, 1, 5);
            SetRowHeight(data, 1, 24);

            var r3 = EnsureRow(data, 3);
            Write(ws, r3, 1, "依据：DB13(J)8457 §4.2.2~4.2.6；GB50014 §4.2", S_SMALL);

            var r5 = EnsureRow(data, 5);
            string[] head = { "序号", "项目", "公式说明", "结果", "备注" };
            for (int i = 0; i < head.Length; i++) Write(ws, r5, i + 1, head[i], S_HEADER);

            (string seq, string name, string desc, string formula, string note)[] items =
            {
                ("1",  "项目总面积 F (hm²)",            "F = 基本参数!C9/10000",       "=基本参数!C10",                                "由 F/10000"),
                ("2",  "综合雨量径流系数 ψc",           "ψc = 下垫面!E19",             "=下垫面分析!E19",                              "下垫面加权"),
                ("3",  "综合流量径流系数 Ψm",           "Ψm = 1.1·ψc",                 "=ROUND(D7*1.1,3)",                             "GB50014 系数 1.1~1.3"),
                ("4",  "Ψm 上限保护",                   "Ψm′ = MIN(Ψm, 0.95)",         "=MIN(D8,0.95)",                                "DB13 工程上限"),
                ("5",  "暴雨强度 q  P=3年, t=15min",    "q = 基本参数!C29",            "=基本参数!C29",                                "L/(s·hm²)"),
                ("6",  "外排设计流量 Q",                "Q = Ψm′·q·F",                 "=ROUND(D9*D10*D6,1)",                          "L/s ★用于雨水管渠"),
                ("7",  "外排控制流量 Q排（允排）",      "Q排 = Q × 允排比 0.7",        "=ROUND(D11*0.7,1)",                            "L/s"),
                ("8",  "调蓄削峰流量差 ΔQ",             "ΔQ = Q − Q排",                "=ROUND(D11-D12,1)",                            "L/s"),
                ("9",  "项目年径流总量 W₀",             "W₀ = F·H/1000·ψc",            "=ROUND(基本参数!C9*基本参数!G7/1000*D7,1)",   "m³ 近似法"),
                ("10", "目标控制水量 W控",              "W控 = 基本参数!G12",          "=基本参数!G12",                                "m³"),
                ("11", "外排率",                         "= 1 − α",                     "=ROUND(1-基本参数!G6,3)",                      "实际外排比例"),
            };
            for (int i = 0; i < items.Length; i++)
            {
                uint r = (uint)(6 + i);
                var row = EnsureRow(data, r);
                Write(ws, row, 1, items[i].seq, S_CALC);
                Write(ws, row, 2, items[i].name, S_NORMAL);
                Write(ws, row, 3, items[i].desc, S_SMALL);
                bool isKey = items[i].seq == "6" || items[i].seq == "9" || items[i].seq == "10" || items[i].seq == "11";
                Write(ws, row, 4, items[i].formula, isKey ? S_RESULT : S_CALC);
                Write(ws, row, 5, items[i].note, S_SMALL);
            }
        }

        // ────────────────────────────────────────────────
        // Sheet4：调蓄容积需求（D6~D13；D10 = V需 ★）
        // ────────────────────────────────────────────────
        private static void BuildSheetVolume(WorkbookPart wbPart, Sheets sheets)
        {
            var (ws, data) = AddSheet(wbPart, sheets, "调蓄容积需求", 4);
            SetColumnWidths(ws, new[] { (1, 4.0), (2, 32.0), (3, 28.0), (4, 16.0), (5, 32.0) });

            var r1 = EnsureRow(data, 1);
            Write(ws, r1, 1, "调蓄容积需求计算（容积法）  V = 10·H·ψc·F(hm²)", S_TITLE);
            Merge(ws, 1, 1, 1, 5);
            SetRowHeight(data, 1, 24);

            var r3 = EnsureRow(data, 3);
            Write(ws, r3, 1, "依据：DB13(J)8457 §4.3.1；指南 §4.4.1", S_SMALL);

            var r5 = EnsureRow(data, 5);
            string[] head = { "序号", "参数", "符号 / 公式", "数值 / 结果", "单位 / 说明" };
            for (int i = 0; i < head.Length; i++) Write(ws, r5, i + 1, head[i], S_HEADER);

            (string seq, string name, string sym, string formula, string unit)[] rows =
            {
                ("1", "项目总面积",            "F = 基本参数!C9",                       "=基本参数!C9",                        "m² (红线)"),
                ("2", "项目总面积（换算）",    "F = 基本参数!C10",                      "=基本参数!C10",                       "hm²"),
                ("3", "设计降雨厚度",          "H = 基本参数!G7",                       "=基本参数!G7",                        "mm"),
                ("4", "综合雨量径流系数",      "ψc = 下垫面!E19",                       "=下垫面分析!E19",                     "—"),
                ("5", "★ 雨水控制利用总容积","V需 = 10·H·ψc·F(hm²)",                  "=ROUND(10*D8*D9*D7,2)",               "m³ 关键控制目标"),
                ("6", "复核 基本参数!G12",     "=基本参数!G12",                         "=基本参数!G12",                       "m³（应一致）"),
                ("7", "差值校核",              "= D10 − D11",                           "=ROUND(D10-D11,3)",                   "应=0"),
                ("8", "单位面积控制水量",      "v = V需/F(hm²)",                        "=IF(D7=0,0,ROUND(D10/D7,2))",         "m³/hm² 参考 200~400"),
            };
            for (int i = 0; i < rows.Length; i++)
            {
                uint r = (uint)(6 + i);
                var row = EnsureRow(data, r);
                Write(ws, row, 1, rows[i].seq, S_CALC);
                Write(ws, row, 2, rows[i].name, S_NORMAL);
                Write(ws, row, 3, rows[i].sym, S_SMALL);
                Write(ws, row, 4, rows[i].formula, rows[i].seq == "5" ? S_RESULT : S_CALC);
                Write(ws, row, 5, rows[i].unit, S_SMALL);
            }
        }

        // ────────────────────────────────────────────────
        // Sheet5：污染削减率验算（E16 = ηSS 实际 ★）
        // ────────────────────────────────────────────────
        private static void BuildSheetPollution(WorkbookPart wbPart, Sheets sheets, SpongeProjectInput input)
        {
            var (ws, data) = AddSheet(wbPart, sheets, "污染削减率验算", 5);
            SetColumnWidths(ws, new[] { (1, 4.0), (2, 28.0), (3, 14.0), (4, 14.0), (5, 14.0), (6, 16.0), (7, 28.0) });

            var r1 = EnsureRow(data, 1);
            Write(ws, r1, 1, "雨水径流污染削减率验算  ηSS = Σ(ηi·Vi) / V总", S_TITLE);
            Merge(ws, 1, 1, 1, 7);
            SetRowHeight(data, 1, 24);

            var r3 = EnsureRow(data, 3);
            Write(ws, r3, 1, "依据：DB13(J)8457 §4.3.3；指南表4-3", S_SMALL);

            var r5 = EnsureRow(data, 5);
            string[] head = { "序号", "设施类型", "承担调蓄量 Vi (m³)", "占比", "SS 削减率 ηi", "贡献 ηi·Vi", "依据 / 备注" };
            for (int i = 0; i < head.Length; i++) Write(ws, r5, i + 1, head[i], S_HEADER);

            // 8 设施 row 6~13；E 列 ηi 取面板输入或默认
            for (int i = 0; i < input.Facilities.Count && i < 8; i++)
            {
                uint r = (uint)(6 + i);
                var row = EnsureRow(data, r);
                var f = input.Facilities[i];
                Write(ws, row, 1, (i + 1).ToString(), S_CALC);
                Write(ws, row, 2, f.Name, S_NORMAL);
                Write(ws, row, 3, $"='设施配置'!E{6 + i}", S_CALC);
                Write(ws, row, 4, $"=IF($C$14=0,0,C{r}/$C$14)", S_CALC);
                Write(ws, row, 5, f.EtaSs, S_INPUT);
                Write(ws, row, 6, $"=ROUND(C{r}*E{r},2)", S_CALC);
                Write(ws, row, 7, f.Note, S_SMALL);
            }

            // 合计 row 14
            var r14 = EnsureRow(data, 14);
            Write(ws, r14, 2, "调蓄合计（=设施配置!E14）", S_BOLD);
            Write(ws, r14, 3, "=SUM(C6:C13)", S_RESULT);
            Write(ws, r14, 4, 1.0, S_BOLD);
            Write(ws, r14, 5, "—", S_BOLD);
            Write(ws, r14, 6, "=SUM(F6:F13)", S_RESULT);

            var r15 = EnsureRow(data, 15);
            Write(ws, r15, 1, "综合雨水径流污染削减率", S_SUBTITLE);
            Merge(ws, 15, 1, 15, 7);

            // ★ row 16 E16 综合 ηSS（关键引用）
            var r16 = EnsureRow(data, 16);
            Write(ws, r16, 1, "★", S_RESULT);
            Write(ws, r16, 2, "综合 SS 削减率 ηSS（实际）", S_RESULT);
            Write(ws, r16, 5, "=IF(C14=0,0,ROUND(F14/C14,3))", S_RESULT);
            Write(ws, r16, 6, "← 引用至 校核汇总", S_SMALL);

            // row 17 目标
            var r17 = EnsureRow(data, 17);
            Write(ws, r17, 2, "目标 SS 削减率 ηSS,目标", S_BOLD);
            Write(ws, r17, 5, "=基本参数!G8", S_CALC);
            Write(ws, r17, 6, "基本参数 ηSS 目标", S_SMALL);

            // row 18 判定
            var r18 = EnsureRow(data, 18);
            Write(ws, r18, 2, "达标判定", S_BOLD);
            Write(ws, r18, 5, "=IF(E16>=E17,\"达标\",\"不达标\")", S_RESULT);
            Write(ws, r18, 6, "ηSS,实 ≥ ηSS,目标 ?", S_SMALL);
        }

        // ────────────────────────────────────────────────
        // Sheet6：设施配置（D6~D13 规模；E6~E13 Vi；E14 = V实 ★）
        // ────────────────────────────────────────────────
        private static void BuildSheetFacility(WorkbookPart wbPart, Sheets sheets, SpongeProjectInput input)
        {
            var (ws, data) = AddSheet(wbPart, sheets, "设施配置", 6);
            SetColumnWidths(ws, new[]
            {
                (1, 4.0), (2, 28.0), (3, 10.0), (4, 14.0), (5, 16.0), (6, 14.0), (7, 16.0), (8, 30.0),
            });

            var r1 = EnsureRow(data, 1);
            Write(ws, r1, 1, "LID 设施配置与规模  （住宅 / 商业小区）", S_TITLE);
            Merge(ws, 1, 1, 1, 8);
            SetRowHeight(data, 1, 24);

            var r3 = EnsureRow(data, 3);
            Write(ws, r3, 1, "依据：DB13(J)8457 §5；指南 §6", S_SMALL);

            var r5 = EnsureRow(data, 5);
            string[] head = { "序号", "设施名称", "单位", "数量 / 规模", "调蓄容积 Vi (m³)", "占总目标", "覆盖下垫面", "技术要点 / 依据" };
            for (int i = 0; i < head.Length; i++) Write(ws, r5, i + 1, head[i], S_HEADER);

            // Vi 公式：F1 用 D6*G26/1000；F2 用 D7*0.30；F3 用 D8*0.10；F4 用 D9*0.05；F5~F8 直接 = Dx
            string[] viFormulas =
            {
                "=ROUND(D6*基本参数!G26/1000,2)",
                "=ROUND(D7*0.30,2)",
                "=ROUND(D8*0.10,2)",
                "=ROUND(D9*0.05,2)",
                "=D10",
                "=D11",
                "=D12",
                "=D13",
            };
            string[] scopes = { "绿地内", "屋面+路面汇入", "人行/广场", "屋面源头", "屋面+路面", "路面沿线", "区域低洼", "土壤渗透性好区" };

            for (int i = 0; i < input.Facilities.Count && i < 8; i++)
            {
                uint r = (uint)(6 + i);
                var row = EnsureRow(data, r);
                var f = input.Facilities[i];
                Write(ws, row, 1, (i + 1).ToString(), S_CALC);
                Write(ws, row, 2, f.Name, S_NORMAL);
                Write(ws, row, 3, f.Unit, S_CALC);
                Write(ws, row, 4, f.Quantity, S_INPUT);
                Write(ws, row, 5, viFormulas[i], S_RESULT);
                Write(ws, row, 6, $"=IF(基本参数!G12=0,0,E{r}/基本参数!G12)", S_CALC);
                Write(ws, row, 7, scopes[i], S_SMALL);
                Write(ws, row, 8, f.Note, S_SMALL);
            }

            // 合计 V实 row 14 ★
            var r14 = EnsureRow(data, 14);
            Write(ws, r14, 2, "★ 设施调蓄合计 V实", S_BOLD);
            Write(ws, r14, 5, "=SUM(E6:E13)", S_RESULT);
            Write(ws, r14, 6, "=IF(基本参数!G12=0,0,E14/基本参数!G12)", S_BOLD);
            Write(ws, r14, 7, "V实 / V需", S_BOLD);
            Write(ws, r14, 8, "应 ≥ 100%", S_BOLD);

            // 校核块 row 15~19
            var r15 = EnsureRow(data, 15);
            Write(ws, r15, 1, "设施容积达标判定", S_SUBTITLE);
            Merge(ws, 15, 1, 15, 8);

            var r16 = EnsureRow(data, 16);
            string[] chkHead = { "", "校核项", "实际值", "阈值", "判定", "差额", "建议措施", "" };
            for (int i = 0; i < chkHead.Length; i++) Write(ws, r16, i + 1, chkHead[i], S_HEADER);

            // ① row 17 V实 ≥ V需
            var r17 = EnsureRow(data, 17);
            Write(ws, r17, 1, "①", S_CALC);
            Write(ws, r17, 2, "V实 ≥ V需", S_NORMAL);
            Write(ws, r17, 3, "=E14", S_CALC);
            Write(ws, r17, 4, "=基本参数!G12", S_CALC);
            Write(ws, r17, 5, "=IF(C17>=D17,\"达标\",\"不达标\")", S_RESULT);
            Write(ws, r17, 6, "=ROUND(C17-D17,2)", S_CALC);
            Write(ws, r17, 7, "不达标→增设下沉式绿地/蓄水池", S_SMALL);

            // ② row 18 下沉式绿地率 ≥ 50%（用绿地合计 = C12+C13 即 S7 + S8；S6 下沉 = D6）
            var r18 = EnsureRow(data, 18);
            Write(ws, r18, 1, "②", S_CALC);
            Write(ws, r18, 2, "下沉式绿地率 ≥ 50% 绿地", S_NORMAL);
            // 绿地总 = 下垫面 S2+S6+S7+S8 = C7+C11+C12+C13
            Write(ws, r18, 3, "=IF((下垫面分析!C7+下垫面分析!C11+下垫面分析!C12+下垫面分析!C13)=0,0,D6/(下垫面分析!C7+下垫面分析!C11+下垫面分析!C12+下垫面分析!C13))", S_CALC);
            Write(ws, r18, 4, 0.50, S_CALC);
            Write(ws, r18, 5, "=IF(C18>=D18,\"达标\",\"不达标\")", S_RESULT);
            Write(ws, r18, 6, "=ROUND(C18-D18,3)", S_CALC);
            Write(ws, r18, 7, "DB13 §5.0.5", S_SMALL);

            // ③ row 19 透水铺装率 ≥ 70%（透水砖 / (混凝土+块石+透水砖) = C10/(C8+C9+C10)）
            var r19 = EnsureRow(data, 19);
            Write(ws, r19, 1, "③", S_CALC);
            Write(ws, r19, 2, "透水铺装率 ≥ 70%", S_NORMAL);
            Write(ws, r19, 3, "=IF((下垫面分析!C8+下垫面分析!C9+下垫面分析!C10)=0,0,下垫面分析!C10/(下垫面分析!C8+下垫面分析!C9+下垫面分析!C10))", S_CALC);
            Write(ws, r19, 4, 0.70, S_CALC);
            Write(ws, r19, 5, "=IF(C19>=D19,\"达标\",\"不达标\")", S_RESULT);
            Write(ws, r19, 6, "=ROUND(C19-D19,3)", S_CALC);
            Write(ws, r19, 7, "DB13 §5.0.6", S_SMALL);
        }

        // ────────────────────────────────────────────────
        // Sheet7：校核与汇总
        // ────────────────────────────────────────────────
        private static void BuildSheetSummary(WorkbookPart wbPart, Sheets sheets)
        {
            var (ws, data) = AddSheet(wbPart, sheets, "校核与汇总", 7);
            SetColumnWidths(ws, new[] { (1, 4.0), (2, 32.0), (3, 18.0), (4, 18.0), (5, 14.0), (6, 30.0) });

            var r1 = EnsureRow(data, 1);
            Write(ws, r1, 1, "海绵城市建设达标核算与成果汇总", S_TITLE);
            Merge(ws, 1, 1, 1, 6);
            SetRowHeight(data, 1, 24);

            var r4 = EnsureRow(data, 4);
            Write(ws, r4, 1, "一、强制性指标判定（DB13 §3 / 设计目标核算）", S_SUBTITLE);
            Merge(ws, 4, 1, 4, 6);

            var r5 = EnsureRow(data, 5);
            string[] head = { "", "校核项", "实际值", "目标值/阈值", "判定", "依据 / 备注" };
            for (int i = 0; i < head.Length; i++) Write(ws, r5, i + 1, head[i], S_HEADER);

            // 7 项校核 row 6~12（与 py 一致 + 自洽公式）
            (string seq, string name, string real, object target, string chk, string note)[] checks =
            {
                ("1", "年径流总量控制率 α",       "=基本参数!G6", "=基本参数!G6", "NA", "目标自定"),
                ("2", "调蓄容积 V实 ≥ V需",      "='设施配置'!E14", "=基本参数!G12", "GE", "≥V需 即达标"),
                ("3", "径流污染削减率 ηSS",       "='污染削减率验算'!E16", "=基本参数!G8", "GE", "≥目标 即达标"),
                ("4", "绿地率 ≥ 30% (R2)",        "=基本参数!C13", 0.30,           "GE", "GB50180 §4.0.5"),
                ("5", "下沉式绿地率 ≥ 50%",       "='设施配置'!C18", 0.50,         "GE", "DB13 §5.0.5"),
                ("6", "透水铺装率 ≥ 70%",         "='设施配置'!C19", 0.70,         "GE", "DB13 §5.0.6"),
                ("7", "外排径流系数 ≤ 0.50",      "=下垫面分析!E19", 0.50,         "LE", "DB13 §3.0.7 一般≤0.5"),
            };
            for (int i = 0; i < checks.Length; i++)
            {
                uint r = (uint)(6 + i);
                var row = EnsureRow(data, r);
                Write(ws, row, 1, checks[i].seq, S_CALC);
                Write(ws, row, 2, checks[i].name, S_NORMAL);
                Write(ws, row, 3, checks[i].real, S_CALC);
                Write(ws, row, 4, checks[i].target, S_CALC);
                string vJudge;
                if (checks[i].chk == "GE") vJudge = $"=IF(C{r}>=D{r},\"达标\",\"不达标\")";
                else if (checks[i].chk == "LE") vJudge = $"=IF(C{r}<=D{r},\"达标\",\"不达标\")";
                else vJudge = "—";
                Write(ws, row, 5, vJudge, S_RESULT);
                Write(ws, row, 6, checks[i].note, S_SMALL);
            }

            // 二、关键技术指标汇总 row 14~28
            var r14 = EnsureRow(data, 14);
            Write(ws, r14, 1, "二、关键技术指标汇总", S_SUBTITLE);
            Merge(ws, 14, 1, 14, 6);

            var r15 = EnsureRow(data, 15);
            string[] mhead = { "", "指标", "数值", "单位", "", "说明" };
            for (int i = 0; i < mhead.Length; i++) Write(ws, r15, i + 1, mhead[i], S_HEADER);

            (string seq, string name, string formula, string unit, string note)[] metrics =
            {
                ("1",  "项目用地面积 F",             "=基本参数!C9",                            "m²",   "项目红线"),
                ("2",  "综合径流系数 ψc",            "=下垫面分析!E19",                         "—",    "下垫面加权"),
                ("3",  "年径流总量控制率 α",          "=基本参数!G6",                            "",     "设计目标"),
                ("4",  "设计降雨厚度 H",              "=基本参数!G7",                            "mm",   "对应 α"),
                ("5",  "雨水控制利用容积 V需",        "=基本参数!G12",                           "m³",   "容积法"),
                ("6",  "设施实际调蓄量 V实",          "='设施配置'!E14",                         "m³",   "Σ各设施"),
                ("7",  "V实/V需 余度",                "=IF(基本参数!G12=0,0,'设施配置'!E14/基本参数!G12)", "", "≥100% 达标"),
                ("8",  "综合 SS 削减率（实际）",      "='污染削减率验算'!E16",                  "",     "加权"),
                ("9",  "外排设计流量 Q",              "='径流系数与外排'!D11",                  "L/s",  "管渠计算"),
                ("10", "外排控制流量 Q排",            "='径流系数与外排'!D12",                  "L/s",  "允许排放"),
                ("11", "年径流总量 W₀",               "='径流系数与外排'!D14",                  "m³",   "近似总径流"),
                ("12", "下沉式绿地面积",              "='设施配置'!D6",                          "m²",   ""),
                ("13", "透水铺装面积",                "=下垫面分析!C10",                         "m²",   ""),
                ("14", "蓄水池容积",                  "='设施配置'!D10",                         "m³",   ""),
                ("15", "海绵投资估算",                "=ROUND(基本参数!G23*基本参数!C9/10000,2)","万元", "可与预算表对照"),
            };
            for (int i = 0; i < metrics.Length; i++)
            {
                uint r = (uint)(16 + i);
                var row = EnsureRow(data, r);
                Write(ws, row, 1, metrics[i].seq, S_CALC);
                Write(ws, row, 2, metrics[i].name, S_NORMAL);
                Write(ws, row, 3, metrics[i].formula, S_RESULT);
                Write(ws, row, 4, metrics[i].unit, S_CALC);
                Write(ws, row, 6, metrics[i].note, S_SMALL);
            }

            // 三、综合判定结论
            uint rJ = (uint)(16 + metrics.Length + 1);
            var rJrow = EnsureRow(data, rJ);
            Write(ws, rJrow, 1, "三、综合判定结论", S_SUBTITLE);
            Merge(ws, (int)rJ, 1, (int)rJ, 6);

            uint rJj = rJ + 1;
            var rJjRow = EnsureRow(data, rJj);
            string judgeFormula =
                "=IF(AND($E$7=\"达标\",$E$8=\"达标\",$E$9=\"达标\",$E$10=\"达标\",$E$11=\"达标\",$E$12=\"达标\"),"
                + "\"★ 海绵指标全部达标，可进入施工图深化\","
                + "\"✗ 存在不达标项，请回到 设施配置 调整规模\")";
            Write(ws, rJjRow, 1, judgeFormula, S_RESULT);
            Merge(ws, (int)rJj, 1, (int)rJj, 6);
            SetRowHeight(data, rJj, 28);
        }

        // ────────────────────────────────────────────────
        // 共用：建一个新 Sheet
        // ────────────────────────────────────────────────
        private static (Worksheet ws, SheetData data) AddSheet(WorkbookPart wbPart, Sheets sheets, string name, uint sheetId)
        {
            var part = wbPart.AddNewPart<WorksheetPart>();
            var data = new SheetData();
            part.Worksheet = new Worksheet(data);

            sheets.AppendChild(new Sheet
            {
                Id = wbPart.GetIdOfPart(part),
                SheetId = sheetId,
                Name = name,
            });
            return (part.Worksheet, data);
        }

        private static string ProjectTypeLabel(ProjectType t)
        {
            switch (t)
            {
                case ProjectType.NewResidential: return "二类居住用地(R2)";
                case ProjectType.NewCommercialHighGreen: return "商业用地(B 绿≥25%)";
                case ProjectType.NewCommercialLowGreen: return "商业用地(B 绿<25%)";
                case ProjectType.RebuildOldResidential: return "改扩建居住区(老旧)";
                case ProjectType.RebuildOtherResidential: return "改扩建居住区(其他)";
                case ProjectType.RebuildPublic: return "改扩建公共建筑";
                default: return "—";
            }
        }

        // SpongeResult 当前未直接写入；公式按引用链动态计算。保留接口以便后续追加"快照值列"。
        private static void TouchResult(SpongeResult result) { _ = result; }
    }
}
