using System;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using HyCADTool.Features.SpongeCity.Domain.Models;
using static HyCADTool.Features.SpongeCity.Infrastructure.OpenXmlSheetHelper;

namespace HyCADTool.Features.SpongeCity.Infrastructure
{
    /// <summary>
    /// 海绵城市 概算+预算 .xlsx 生成器（对齐 gen_sponge_housing_budget.py 5 Sheet 结构）：
    /// Sheet1 基本参数（工程量 C6~C21 / 费率 G6~G18 / KPI C25~C29）
    /// Sheet2 分项概算（12 大类汇总，每类 1 行综合单价 × 工程量 = 合价万元）
    /// Sheet3 分项预算（12 大类，人材机三费 + 预算费用）
    /// Sheet4 费用汇总（直接费 → 建安 → 总投资）
    /// Sheet5 对比分析（概预算对比 + 大类汇总 + 三费构成）
    /// 与计算表通过 SAVE 后人工放在同目录或显式打开两个文件的工作簿协同使用。
    /// </summary>
    public static class SpongeBudgetWorkbookBuilder
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

                BuildSheetParams(wbPart, sheets, input, result);
                BuildSheetItemized(wbPart, sheets);
                BuildSheetDetailed(wbPart, sheets);
                BuildSheetCostSummary(wbPart, sheets);
                BuildSheetCompare(wbPart, sheets);

                wbPart.Workbook.Save();
            }
        }

        // ────────────────────────────────────────────────
        // Sheet1：基本参数（工程量 C6~C21 / 概算费率 G6~G12 / 预算费率 G15~G18 / KPI C25~C29）
        // ────────────────────────────────────────────────
        private static void BuildSheetParams(WorkbookPart wbPart, Sheets sheets, SpongeProjectInput input, SpongeResult result)
        {
            var (ws, data) = AddSheet(wbPart, sheets, "基本参数", 1);
            SetColumnWidths(ws, new[]
            {
                (1, 4.0), (2, 30.0), (3, 16.0), (4, 26.0),
                (5, 4.0), (6, 28.0), (7, 16.0), (8, 24.0),
            });

            var r1 = EnsureRow(data, 1);
            Write(ws, r1, 1, "海绵城市住宅/商业小区  概算+预算 计算总表", S_TITLE);
            Merge(ws, 1, 1, 1, 8);
            SetRowHeight(data, 1, 24);

            var r2 = EnsureRow(data, 2);
            Write(ws, r2, 1, "■ 蓝色=可调输入  ■ 灰色=自动计算  ■ 黄色=关键结果", S_SMALL);
            Merge(ws, 2, 1, 2, 8);

            // 一、工程量基础参数（左 A~D）& 二、概算费率（右 E~H）
            var r4 = EnsureRow(data, 4);
            Write(ws, r4, 1, "一、工程量基础参数（与计算表联动）", S_SUBTITLE);
            Merge(ws, 4, 1, 4, 4);
            Write(ws, r4, 5, "二、概算阶段费率参数", S_SUBTITLE);
            Merge(ws, 4, 5, 4, 8);

            var r5 = EnsureRow(data, 5);
            string[] hL = { "", "参数名称", "数值", "单位 / 说明" };
            string[] hR = { "", "费率名称", "费率", "计费基础" };
            for (int i = 0; i < 4; i++)
            {
                Write(ws, r5, i + 1, hL[i], S_HEADER);
                Write(ws, r5, i + 5, hR[i], S_HEADER);
            }

            // 工程量 C6~C21（来自面板数据）
            double areaGreenSink = ValueByCode(input, "S6");
            double areaPervious = ValueByCode(input, "S5");
            double areaPaveConcrete = ValueByCode(input, "S3"); // 用作透水沥青/混凝土近似（无独立项时）
            (string name, object val, string note)[] qty =
            {
                ("项目总用地面积 F (m²)",         input.TotalAreaM2,                       "项目红线"),                  // C6
                ("项目总用地面积 F (hm²)",        "=ROUND(C6/10000,4)",                    "自动"),                      // C7
                ("建筑屋面面积 (m²)",             input.BuildingFootprintM2,               "硬质屋面+绿色屋顶"),         // C8
                ("绿地面积 (m²)",                 "=ROUND(C6*0.30,0)",                     "估算（按绿地率）"),          // C9
                ("下沉式绿地面积 (m²)",           areaGreenSink,                           "深度 150mm"),                // C10
                ("雨水花园面积 (m²)",             FacilityQty(input, "F2"),                "深度 0.20~0.30m"),           // C11
                ("透水砖铺装面积 (m²)",           areaPervious,                            "人行/广场"),                 // C12
                ("透水沥青/混凝土面积 (m²)",      0.0,                                     "消防车道/小区道路"),         // C13
                ("绿色屋顶面积 (m²)",             FacilityQty(input, "F4"),                "简单式/花园式"),             // C14
                ("雨水蓄水池容积 (m³)",           FacilityQty(input, "F5"),                "钢混或模块"),                // C15
                ("植草沟容积 (m³)",               FacilityQty(input, "F6"),                "断面≤1m²"),                  // C16
                ("湿塘/雨水湿地容积 (m³)",        FacilityQty(input, "F7"),                "调蓄水位<1.5m"),             // C17
                ("渗透塘/渗透井容积 (m³)",        FacilityQty(input, "F8"),                "土壤渗透好区"),              // C18
                ("雨水管道总长 (m)",              "=ROUND(SQRT(C6)*1.6,0)",                "DN300~DN800 估算"),          // C19
                ("雨水检查井数量 (座)",           "=ROUND(C19/40,0)",                      "30~40m 一座"),               // C20
                ("溢流口数量 (处)",               "=ROUND(C10/200,0)+ROUND(C11/50,0)",     "下沉绿地+雨水花园连管"),     // C21
            };
            for (int i = 0; i < qty.Length; i++)
            {
                uint r = (uint)(6 + i);
                var row = EnsureRow(data, r);
                Write(ws, row, 1, i + 1, S_CALC);
                Write(ws, row, 2, qty[i].name, S_NORMAL);
                bool isFormula = qty[i].val is string s && s.StartsWith("=");
                Write(ws, row, 3, qty[i].val, isFormula ? S_CALC : S_INPUT);
                Write(ws, row, 4, qty[i].note, S_SMALL);
            }

            // 概算费率 G6~G12
            (string name, double val, string note)[] fees =
            {
                ("措施费率",            0.10,  "按直接费"),
                ("企业管理费率",        0.055, "按直接费"),
                ("利润费率",            0.04,  "按直接费"),
                ("规费费率",            0.04,  "按直接费"),
                ("增值税率",            0.09,  "按税前造价"),     // G10
                ("工程建设其他费率",    0.10,  "按建安费"),
                ("预备费率",            0.05,  "按(建安+其他费)"),
            };
            for (int i = 0; i < fees.Length; i++)
            {
                uint r = (uint)(6 + i);
                var row = EnsureRow(data, r);
                Write(ws, row, 5, i + 1, S_CALC);
                Write(ws, row, 6, fees[i].name, S_NORMAL);
                Write(ws, row, 7, fees[i].val, S_INPUT);
                Write(ws, row, 8, fees[i].note, S_SMALL);
            }

            // 三、预算费率 row 13/14 标题与表头，G15~G18 数值
            var r13 = EnsureRow(data, 13);
            Write(ws, r13, 5, "三、预算阶段费率（以 人工费+机械费 为基数）", S_SUBTITLE);
            Merge(ws, 13, 5, 13, 8);
            var r14 = EnsureRow(data, 14);
            string[] hB = { "", "费率名称", "费率", "计费基础" };
            for (int i = 0; i < 4; i++) Write(ws, r14, i + 5, hB[i], S_HEADER);

            (string name, double val, string note)[] bFees =
            {
                ("企业管理费率(预算)", 0.11,  "人工费+机械费"),  // G15
                ("利润费率(预算)",     0.055, "人工费+机械费"),
                ("安全文明施工费率",   0.035, "直+企+利"),
                ("规费费率(预算)",     0.04,  "人工费+机械费"),  // G18
            };
            for (int i = 0; i < bFees.Length; i++)
            {
                uint r = (uint)(15 + i);
                var row = EnsureRow(data, r);
                Write(ws, row, 5, i + 1, S_CALC);
                Write(ws, row, 6, bFees[i].name, S_NORMAL);
                Write(ws, row, 7, bFees[i].val, S_INPUT);
                Write(ws, row, 8, bFees[i].note, S_SMALL);
            }

            // 四、海绵关键技术指标 C25~C29
            var r23 = EnsureRow(data, 23);
            Write(ws, r23, 1, "四、海绵关键技术指标（与计算表联动，仅作显示）", S_SUBTITLE);
            Merge(ws, 23, 1, 23, 4);
            var r24 = EnsureRow(data, 24);
            string[] kHd = { "", "指标名称", "数值", "说明" };
            for (int i = 0; i < 4; i++) Write(ws, r24, i + 1, kHd[i], S_HEADER);

            (string name, double val, string note)[] kpis =
            {
                ("年径流总量控制率 α",        input.AlphaTarget,           "目标"),                    // C25
                ("控制目标体积 V需 (m³)",     result?.FinalRequiredVolume ?? 0, "容积法计算"),
                ("综合径流系数 ψc",           result?.PsiZ ?? 0,           "下垫面加权"),
                ("综合 SS 削减率 ηSS",        input.EtaSsTarget,           "目标"),
                ("设计降雨厚度 H (mm)",       input.DesignRainfallMm,      "衡水取值"),                // C29
            };
            for (int i = 0; i < kpis.Length; i++)
            {
                uint r = (uint)(25 + i);
                var row = EnsureRow(data, r);
                Write(ws, row, 1, i + 1, S_CALC);
                Write(ws, row, 2, kpis[i].name, S_NORMAL);
                Write(ws, row, 3, kpis[i].val, S_INPUT);
                Write(ws, row, 4, kpis[i].note, S_SMALL);
            }
        }

        // ────────────────────────────────────────────────
        // Sheet2：分项概算（12 大类，每类一行汇总）
        // ────────────────────────────────────────────────
        private static void BuildSheetItemized(WorkbookPart wbPart, Sheets sheets)
        {
            var (ws, data) = AddSheet(wbPart, sheets, "分项概算", 2);
            SetColumnWidths(ws, new[]
            {
                (1, 6.0), (2, 44.0), (3, 8.0), (4, 14.0), (5, 16.0), (6, 16.0), (7, 32.0),
            });

            var r1 = EnsureRow(data, 1);
            Write(ws, r1, 1, "海绵城市住宅/商业小区  分部分项工程概算汇总表", S_TITLE);
            Merge(ws, 1, 1, 1, 7);
            SetRowHeight(data, 1, 24);

            var r2 = EnsureRow(data, 2);
            Write(ws, r2, 1, "单位：万元    定额：DB13(J)/T 8513/8512/8556 （每大类按综合单价估算）", S_SMALL);
            Merge(ws, 2, 1, 2, 7);

            var r3 = EnsureRow(data, 3);
            string[] head = { "序号", "工程名称", "单位", "数量", "综合单价(元)", "合价(万元)", "工程量基础" };
            for (int i = 0; i < head.Length; i++) Write(ws, r3, i + 1, head[i], S_HEADER);

            // 12 大类汇总（综合单价为示例，覆盖：土方+材料+人工的整包估算）
            (string seq, string name, string unit, object qtyRef, double unitPrice, string note)[] groups =
            {
                ("1",  "下沉式绿地工程综合",      "m²", (object)"=基本参数!C10",                    280.0,   "DB13 §5.0.5 含开挖/找平/植草/防渗"),
                ("2",  "雨水花园（生物滞留）工程", "m²", (object)"=基本参数!C11",                    650.0,   "指南 §6.1.3 含滤料/盲管/植物"),
                ("3",  "透水砖铺装工程",          "m²", (object)"=基本参数!C12",                    350.0,   "面层+基层+土基"),
                ("4",  "透水沥青/混凝土路面",     "m²", (object)"=基本参数!C13",                    540.0,   "上下面层 + 基层"),
                ("5",  "绿色屋顶（简单式）工程",   "m²", (object)"=基本参数!C14",                    420.0,   "防水/排水/种植土/植物"),
                ("6",  "雨水蓄水池及配套设备",     "m³", (object)"=基本参数!C15",                    2200.0,  "钢混池+设备综合"),
                ("7",  "植草沟（转输+渗透）",      "m³", (object)"=基本参数!C16",                    280.0,   "断面 ≤1m²"),
                ("8",  "雨水管渠及附属",          "m",  (object)"=基本参数!C19",                    280.0,   "DN300~DN800 平均"),
                ("9",  "渗透塘 / 渗透井设施",     "m³", (object)"=基本参数!C18",                    650.0,   "含格栅+砾石+土工布"),
                ("10", "初期雨水弃流装置",        "套", (object)"=MAX(基本参数!C14/500+1,2)",       16000.0, "旋流弃流过滤器"),
                ("11", "自动控制与监测系统",      "项", (object)1.0,                                 85000.0, "液位+流量+雨量+PLC"),
                ("12", "海绵标识与附属设施",      "项", (object)1.0,                                 35000.0, "标识 + 取水栓 + 措施费"),
            };

            uint startR = 4;
            uint endR = 0;
            for (int i = 0; i < groups.Length; i++)
            {
                uint r = startR + (uint)i;
                var row = EnsureRow(data, r);
                Write(ws, row, 1, groups[i].seq, S_CALC);
                Write(ws, row, 2, groups[i].name, S_NORMAL);
                Write(ws, row, 3, groups[i].unit, S_CALC);
                Write(ws, row, 4, groups[i].qtyRef, S_INPUT);
                Write(ws, row, 5, groups[i].unitPrice, S_INPUT);
                Write(ws, row, 6, $"=ROUND(D{r}*E{r}/10000,2)", S_RESULT);
                Write(ws, row, 7, groups[i].note, S_SMALL);
                endR = r;
            }

            // 直接费合计
            uint totalR = endR + 1;
            var tot = EnsureRow(data, totalR);
            Write(ws, tot, 2, "★ 直接工程费合计（万元）", S_BOLD);
            Write(ws, tot, 6, $"=SUM(F{startR}:F{endR})", S_RESULT);
            Write(ws, tot, 7, "分项概算合计", S_BOLD);
        }

        // ────────────────────────────────────────────────
        // Sheet3：分项预算（同 12 大类 + 人材机三费 + 预算费用）
        // ────────────────────────────────────────────────
        private static void BuildSheetDetailed(WorkbookPart wbPart, Sheets sheets)
        {
            var (ws, data) = AddSheet(wbPart, sheets, "分项预算", 3);
            SetColumnWidths(ws, new[]
            {
                (1, 6.0), (2, 38.0), (3, 8.0), (4, 14.0),
                (5, 14.0), (6, 14.0), (7, 14.0), (8, 14.0), (9, 16.0), (10, 30.0),
            });

            var r1 = EnsureRow(data, 1);
            Write(ws, r1, 1, "海绵城市住宅/商业小区  分项预算（人工+材料+机械 三费分析）", S_TITLE);
            Merge(ws, 1, 1, 1, 10);
            SetRowHeight(data, 1, 24);

            var r3 = EnsureRow(data, 3);
            string[] head = { "序号", "工程名称", "单位", "数量", "人工费(元)", "材料费(元)", "机械费(元)", "综合单价(元)", "合价(万元)", "备注" };
            for (int i = 0; i < head.Length; i++) Write(ws, r3, i + 1, head[i], S_HEADER);

            (string seq, string name, string unit, object qtyRef, double labor, double mat, double mach, string note)[] rows =
            {
                ("1",  "下沉式绿地工程",         "m²", (object)"=基本参数!C10",                     45.0,   195.0,  35.0,  "DB13 §5.0.5"),
                ("2",  "雨水花园（生物滞留）",     "m²", (object)"=基本参数!C11",                     85.0,   480.0,  75.0,  "指南 §6.1.3"),
                ("3",  "透水砖铺装",             "m²", (object)"=基本参数!C12",                     55.0,   250.0,  35.0,  "面层 60mm"),
                ("4",  "透水沥青/混凝土路面",    "m²", (object)"=基本参数!C13",                     65.0,   420.0,  45.0,  "上+下基层"),
                ("5",  "绿色屋顶（简单式）",      "m²", (object)"=基本参数!C14",                     65.0,   300.0,  40.0,  "防水/排水/植物"),
                ("6",  "雨水蓄水池及配套",        "m³", (object)"=基本参数!C15",                     350.0,  1500.0, 280.0, "钢混 + 设备"),
                ("7",  "植草沟",                  "m³", (object)"=基本参数!C16",                     45.0,   200.0,  25.0,  "DB13 §5.0.7"),
                ("8",  "雨水管渠及附属",          "m",  (object)"=基本参数!C19",                     55.0,   175.0,  45.0,  "DN300~DN800 综合"),
                ("9",  "渗透塘 / 渗透井",         "m³", (object)"=基本参数!C18",                     85.0,   480.0,  70.0,  ""),
                ("10", "初期雨水弃流装置",        "套", (object)"=MAX(基本参数!C14/500+1,2)",        2500.0, 11000.0, 2500.0, ""),
                ("11", "自动控制与监测系统",      "项", (object)1.0,                                 9000.0, 60000.0, 5000.0, ""),
                ("12", "海绵标识与附属设施",      "项", (object)1.0,                                 5000.0, 25000.0, 3000.0, ""),
            };

            uint startR = 4, endR = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                uint r = startR + (uint)i;
                var row = EnsureRow(data, r);
                Write(ws, row, 1, rows[i].seq, S_CALC);
                Write(ws, row, 2, rows[i].name, S_NORMAL);
                Write(ws, row, 3, rows[i].unit, S_CALC);
                Write(ws, row, 4, rows[i].qtyRef, S_INPUT);
                Write(ws, row, 5, rows[i].labor, S_INPUT);
                Write(ws, row, 6, rows[i].mat, S_INPUT);
                Write(ws, row, 7, rows[i].mach, S_INPUT);
                Write(ws, row, 8, $"=E{r}+F{r}+G{r}", S_CALC);
                Write(ws, row, 9, $"=ROUND(D{r}*H{r}/10000,2)", S_RESULT);
                Write(ws, row, 10, rows[i].note, S_SMALL);
                endR = r;
            }

            uint totalR = endR + 1;
            var tot = EnsureRow(data, totalR);
            Write(ws, tot, 2, "★ 直接工程费合计（万元）", S_BOLD);
            Write(ws, tot, 9, $"=SUM(I{startR}:I{endR})", S_RESULT);

            // 三费拆分
            uint laborR = totalR + 1;
            uint matR = totalR + 2;
            uint machR = totalR + 3;
            var lr = EnsureRow(data, laborR);
            Write(ws, lr, 2, "其中：人工费合计（万元）", S_BOLD);
            Write(ws, lr, 9, $"=ROUND(SUMPRODUCT(D{startR}:D{endR},E{startR}:E{endR})/10000,2)", S_RESULT);

            var mr = EnsureRow(data, matR);
            Write(ws, mr, 2, "其中：材料费合计（万元）", S_BOLD);
            Write(ws, mr, 9, $"=ROUND(SUMPRODUCT(D{startR}:D{endR},F{startR}:F{endR})/10000,2)", S_RESULT);

            var jr = EnsureRow(data, machR);
            Write(ws, jr, 2, "其中：机械费合计（万元）", S_BOLD);
            Write(ws, jr, 9, $"=ROUND(SUMPRODUCT(D{startR}:D{endR},G{startR}:G{endR})/10000,2)", S_RESULT);

            // 预算费用计算（人材机基数）
            uint feeStart = machR + 2;
            var fh = EnsureRow(data, feeStart);
            Write(ws, fh, 1, "预算费用计算（企业管理费/利润以 人工费+机械费 为基数）", S_SUBTITLE);
            Merge(ws, (int)feeStart, 1, (int)feeStart, 10);

            uint feeHd = feeStart + 1;
            var fhRow = EnsureRow(data, feeHd);
            string[] fHead = { "序号", "费用名称", "计算公式", "", "", "", "", "费率", "金额(万元)", "备注" };
            for (int i = 0; i < fHead.Length; i++) Write(ws, fhRow, i + 1, fHead[i], S_HEADER);

            uint d_r = feeHd + 1;  // 直接费
            uint mgmt_r = d_r + 1;
            uint profit_r = d_r + 2;
            uint safety_r = d_r + 3;
            uint rule_r = d_r + 4;
            uint pretax_r = d_r + 5;
            uint tax_r = d_r + 6;
            uint ba_r = d_r + 7;

            void Row(uint r, string seq, string name, string desc, object rate, string amountFormula, string note)
            {
                var rr = EnsureRow(data, r);
                Write(ws, rr, 1, seq, S_CALC);
                Write(ws, rr, 2, name, S_NORMAL);
                Write(ws, rr, 3, desc, S_SMALL);
                Write(ws, rr, 8, rate, S_CALC);
                Write(ws, rr, 9, amountFormula, S_RESULT);
                Write(ws, rr, 10, note, S_SMALL);
            }

            Row(d_r,      "1", "直接工程费",       "分项合计",                "/",              $"=I{totalR}", "见上");
            Row(mgmt_r,   "2", "企业管理费",       "(人工+机械)×费率",       "=基本参数!G15",  $"=ROUND((I{laborR}+I{machR})*基本参数!G15,2)", "");
            Row(profit_r, "3", "利润",              "(人工+机械)×费率",       "=基本参数!G16",  $"=ROUND((I{laborR}+I{machR})*基本参数!G16,2)", "");
            Row(safety_r, "4", "安全文明施工费",    "(直+企+利)×费率",        "=基本参数!G17",  $"=ROUND((I{d_r}+I{mgmt_r}+I{profit_r})*基本参数!G17,2)", "");
            Row(rule_r,   "5", "规费",              "(人工+机械)×费率",       "=基本参数!G18",  $"=ROUND((I{laborR}+I{machR})*基本参数!G18,2)", "社保/公积金/工伤");

            // 税前
            var preRow = EnsureRow(data, pretax_r);
            Write(ws, preRow, 2, "税前工程造价", S_BOLD);
            Write(ws, preRow, 3, "=1+2+3+4+5", S_SMALL);
            Write(ws, preRow, 9, $"=SUM(I{d_r}:I{rule_r})", S_RESULT);

            // 增值税
            Row(tax_r, "6", "增值税", "税前造价×税率", "=基本参数!G10", $"=ROUND(I{pretax_r}*基本参数!G10,2)", "");

            // 建安费
            var baRow = EnsureRow(data, ba_r);
            Write(ws, baRow, 2, "★ 建筑安装工程费（预算）", S_BOLD);
            Write(ws, baRow, 3, "税前+增值税", S_SMALL);
            Write(ws, baRow, 9, $"=I{pretax_r}+I{tax_r}", S_RESULT);
        }

        // ────────────────────────────────────────────────
        // Sheet4：费用汇总
        // ────────────────────────────────────────────────
        private static void BuildSheetCostSummary(WorkbookPart wbPart, Sheets sheets)
        {
            var (ws, data) = AddSheet(wbPart, sheets, "费用汇总", 4);
            SetColumnWidths(ws, new[] { (1, 6.0), (2, 32.0), (3, 22.0), (4, 14.0), (5, 20.0), (6, 22.0) });

            var r1 = EnsureRow(data, 1);
            Write(ws, r1, 1, "工程费用汇总表（直接费 → 建安费 → 总投资）", S_TITLE);
            Merge(ws, 1, 1, 1, 6);
            SetRowHeight(data, 1, 24);

            var r2 = EnsureRow(data, 2);
            Write(ws, r2, 1, "全部联动基本参数与分项概算，修改单价后自动更新", S_SMALL);
            Merge(ws, 2, 1, 2, 6);

            var r3 = EnsureRow(data, 3);
            string[] head = { "序号", "名称", "计算公式/基数", "费率", "金额(万元)", "备注" };
            for (int i = 0; i < head.Length; i++) Write(ws, r3, i + 1, head[i], S_HEADER);

            // 概算合计行号引用：分项概算!F16（12 行 + 合计 = row 16）
            string DIRECT = "=分项概算!F16";
            (string seq, string name, string desc, object rate, string amount, string note)[] items =
            {
                ("1", "直接工程费", "分项概算合计", "/",            DIRECT,                                  "见分项概算"),
                ("2", "措施费",     "直接费×费率",  "=基本参数!G6", "=ROUND(E4*基本参数!G6,2)",              "安全文明施工等"),
                ("3", "企业管理费", "直接费×费率",  "=基本参数!G7", "=ROUND(E4*基本参数!G7,2)",              ""),
                ("4", "利润",       "直接费×费率",  "=基本参数!G8", "=ROUND(E4*基本参数!G8,2)",              ""),
                ("5", "规费",       "直接费×费率",  "=基本参数!G9", "=ROUND(E4*基本参数!G9,2)",              "社保/公积金/工伤"),
            };
            uint startR = 4;
            for (int i = 0; i < items.Length; i++)
            {
                uint r = startR + (uint)i;
                var row = EnsureRow(data, r);
                Write(ws, row, 1, items[i].seq, S_CALC);
                Write(ws, row, 2, items[i].name, S_NORMAL);
                Write(ws, row, 3, items[i].desc, S_SMALL);
                Write(ws, row, 4, items[i].rate, S_CALC);
                Write(ws, row, 5, items[i].amount, S_CALC);
                Write(ws, row, 6, items[i].note, S_SMALL);
            }
            uint endR = startR + (uint)items.Length - 1; // = 8

            // 税前 row 9
            uint preR = endR + 1;
            var preRow = EnsureRow(data, preR);
            Write(ws, preRow, 2, "税前工程造价", S_BOLD);
            Write(ws, preRow, 3, "=1+2+3+4+5", S_SMALL);
            Write(ws, preRow, 5, $"=SUM(E{startR}:E{endR})", S_RESULT);

            // 增值税 row 10
            uint taxR = preR + 1;
            var taxRow = EnsureRow(data, taxR);
            Write(ws, taxRow, 1, "6", S_CALC);
            Write(ws, taxRow, 2, "增值税", S_NORMAL);
            Write(ws, taxRow, 3, "税前×税率", S_SMALL);
            Write(ws, taxRow, 4, "=基本参数!G10", S_CALC);
            Write(ws, taxRow, 5, $"=ROUND(E{preR}*基本参数!G10,2)", S_CALC);

            // 建安费 row 11
            uint baR = taxR + 1;
            var baRow = EnsureRow(data, baR);
            Write(ws, baRow, 2, "★ 建筑安装工程费（概算）", S_BOLD);
            Write(ws, baRow, 5, $"=E{preR}+E{taxR}", S_RESULT);

            // 工程建设其他费 row 12
            uint otherR = baR + 1;
            var otherRow = EnsureRow(data, otherR);
            Write(ws, otherRow, 1, "7", S_CALC);
            Write(ws, otherRow, 2, "工程建设其他费", S_NORMAL);
            Write(ws, otherRow, 3, "建安费×费率", S_SMALL);
            Write(ws, otherRow, 4, "=基本参数!G11", S_CALC);
            Write(ws, otherRow, 5, $"=ROUND(E{baR}*基本参数!G11,2)", S_CALC);

            // 预备费 row 13
            uint prepR = otherR + 1;
            var prepRow = EnsureRow(data, prepR);
            Write(ws, prepRow, 1, "8", S_CALC);
            Write(ws, prepRow, 2, "预备费", S_NORMAL);
            Write(ws, prepRow, 3, "(建安+其他费)×费率", S_SMALL);
            Write(ws, prepRow, 4, "=基本参数!G12", S_CALC);
            Write(ws, prepRow, 5, $"=ROUND((E{baR}+E{otherR})*基本参数!G12,2)", S_CALC);

            // 总投资
            uint totR = prepR + 1;
            var totRow = EnsureRow(data, totR);
            Write(ws, totRow, 2, "★ 工程总投资（概算）", S_BOLD);
            Write(ws, totRow, 5, $"=E{baR}+E{otherR}+E{prepR}", S_RESULT);

            // 经济指标
            uint kpiR = totR + 2;
            var kHead = EnsureRow(data, kpiR);
            Write(ws, kHead, 1, "经济指标（单方造价）", S_SUBTITLE);
            Merge(ws, (int)kpiR, 1, (int)kpiR, 6);

            uint k1 = kpiR + 1, k2 = kpiR + 2, k3 = kpiR + 3;
            var r1k = EnsureRow(data, k1);
            Write(ws, r1k, 2, "单方海绵投资 (元/m²)", S_BOLD);
            Write(ws, r1k, 5, $"=ROUND(E{totR}*10000/基本参数!C6,2)", S_RESULT);

            var r2k = EnsureRow(data, k2);
            Write(ws, r2k, 2, "单位 V需 投资 (元/m³)", S_BOLD);
            Write(ws, r2k, 5, $"=IF(基本参数!C26=0,0,ROUND(E{totR}*10000/基本参数!C26,2))", S_RESULT);

            var r3k = EnsureRow(data, k3);
            Write(ws, r3k, 2, "每亩海绵投资 (万元/亩)", S_BOLD);
            Write(ws, r3k, 5, $"=ROUND(E{totR}/(基本参数!C6/666.67),4)", S_RESULT);
        }

        // ────────────────────────────────────────────────
        // Sheet5：对比分析（概预算对比）
        // ────────────────────────────────────────────────
        private static void BuildSheetCompare(WorkbookPart wbPart, Sheets sheets)
        {
            var (ws, data) = AddSheet(wbPart, sheets, "对比分析", 5);
            SetColumnWidths(ws, new[] { (1, 30.0), (2, 18.0), (3, 18.0), (4, 14.0), (5, 30.0) });

            var r1 = EnsureRow(data, 1);
            Write(ws, r1, 1, "概算 vs 预算 对比与 KPI 速览", S_TITLE);
            Merge(ws, 1, 1, 1, 5);
            SetRowHeight(data, 1, 24);

            var r3 = EnsureRow(data, 3);
            string[] head = { "对比项", "概算 (万元)", "预算 (万元)", "比值", "说明" };
            for (int i = 0; i < head.Length; i++) Write(ws, r3, i + 1, head[i], S_HEADER);

            // 概算建安费在「费用汇总」E11；预算建安费在「分项预算」I 的 ba_r 行 ≈ 25 行
            // 因 Sheet3 行号是动态的（12 项 + 三费拆分 + 8 项预算费用），我们直接以最终行号近似：
            // 工程量 12 行（4~15）+ 总计 16 + 三费 17/18/19 + 标题 21 + 表头 22 + 数据 23~30
            // 直接费 23、mgmt 24、profit 25、safety 26、规费 27、税前 28、增值税 29、建安 30
            string preBA = "=分项预算!I30";
            string esBA = "=费用汇总!E11";

            var r4 = EnsureRow(data, 4);
            Write(ws, r4, 1, "建筑安装工程费", S_NORMAL);
            Write(ws, r4, 2, esBA, S_CALC);
            Write(ws, r4, 3, preBA, S_CALC);
            Write(ws, r4, 4, "=IF(B4=0,0,C4/B4)", S_RESULT);
            Write(ws, r4, 5, "比值=预算/概算（参考 0.9~1.1）", S_SMALL);

            // KPI 区
            var r6 = EnsureRow(data, 6);
            Write(ws, r6, 1, "KPI 速览（与计算表/基本参数联动）", S_SUBTITLE);
            Merge(ws, 6, 1, 6, 5);

            var r7 = EnsureRow(data, 7);
            string[] kHd = { "KPI 名称", "数值", "", "单位", "说明" };
            for (int i = 0; i < kHd.Length; i++) Write(ws, r7, i + 1, kHd[i], S_HEADER);

            (string name, string formula, string unit, string note)[] kpis =
            {
                ("α (年径流总量控制率)",     "=基本参数!C25",                            "",     "目标"),
                ("V需 控制目标体积",          "=基本参数!C26",                            "m³",   "容积法"),
                ("ψc 综合径流系数",           "=基本参数!C27",                            "—",    "下垫面加权"),
                ("ηSS 综合 SS 削减率",        "=基本参数!C28",                            "",     "目标"),
                ("H 设计降雨厚度",            "=基本参数!C29",                            "mm",   "α 对应"),
                ("单方海绵投资",              "=费用汇总!E16",                            "元/m²", "经济指标"),
            };
            for (int i = 0; i < kpis.Length; i++)
            {
                uint r = (uint)(8 + i);
                var row = EnsureRow(data, r);
                Write(ws, row, 1, kpis[i].name, S_NORMAL);
                Write(ws, row, 2, kpis[i].formula, S_RESULT);
                Write(ws, row, 4, kpis[i].unit, S_CALC);
                Write(ws, row, 5, kpis[i].note, S_SMALL);
            }
        }

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

        private static double ValueByCode(SpongeProjectInput input, string code)
        {
            if (input?.Surfaces == null) return 0;
            foreach (var s in input.Surfaces)
                if (s.Code == code) return s.Area;
            return 0;
        }

        private static double FacilityQty(SpongeProjectInput input, string code)
        {
            if (input?.Facilities == null) return 0;
            foreach (var f in input.Facilities)
                if (f.Code == code) return f.Quantity;
            return 0;
        }
    }
}
