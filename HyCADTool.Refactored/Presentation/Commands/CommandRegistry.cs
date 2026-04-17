using Autodesk.AutoCAD.Runtime;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Utilities;
using HyCADTool.Refactored.Presentation.ViewModels;
using System.Reflection;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(HyCADTool.Refactored.Presentation.Commands.CommandRegistry))]

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 命令注册中心：将所有命令以 [CommandMethod] 注册到 AutoCAD。
    /// 直接加载 DLL 时由 AutoCAD 扫描发现；C2 热重载时不会重复注册。
    /// 
    /// 重要：C2 热重载后，CommandRegistry 仍属于首次加载的程序集，
    /// 而面板 ViewModel 属于最新程序集。两者的静态字典隔离。
    /// 因此 Run() 在执行前从 hy-settings.json 加载最新参数，
    /// 面板属性变更会即时写入该文件（SetProperty → SaveSettings）。
    /// </summary>
    public class CommandRegistry
    {
        // ================================================================
        //  辅助方法
        // ================================================================

        /// <summary>
        /// 统一命令执行包装：
        /// 1. 从 hy-settings.json 同步最新面板参数（跨程序集安全）
        /// 2. 确保样式已应用
        /// 3. 异常捕获
        /// </summary>
        private static void Run(System.Action action)
        {
            try
            {
                #region agent log
                AgentDebugLogger.Log("routing", "H6", "CommandRegistry.Run", "registry run entered",
                    new { actionType = action?.Method?.DeclaringType?.FullName, actionName = action?.Method?.Name });
                #endregion

                // 强制提交面板中正在编辑的 TextBox 值（LostFocus 模式下命令行输入不会触发）
                SettingsPanelViewModel.CommitFocusedTextBoxValue();

                // 从文件加载最新参数（面板改值会即时写入文件）
                var vm = SettingsPanelViewModel.Current;
                vm?.LoadSettings();
                vm?.EnsureStylesApplied();

                action();
            }
            catch (System.Exception ex)
            {
                AcApp.DocumentManager.MdiActiveDocument?.Editor
                    ?.WriteMessage($"\n错误：{ex.Message}");
            }
        }

        private static bool IsHotReloadedAssembly()
        {
            return string.IsNullOrWhiteSpace(Assembly.GetExecutingAssembly().Location);
        }

        private static void RouteThroughC1(string commandKey, System.Action fallbackAction)
        {
            if (!IsHotReloadedAssembly())
            {
                Run(fallbackAction);
                return;
            }

            try
            {
                #region agent log
                AgentDebugLogger.Log("post-fix", "H13", "CommandRegistry.RouteThroughC1", "routing command alias through c1",
                    new { commandKey });
                #endregion

                SettingsPanelViewModel.CommitFocusedTextBoxValue();
                var vm = SettingsPanelViewModel.Current;
                vm?.LoadSettings();
                vm?.EnsureStylesApplied();

                CommandRelayStore.Save(commandKey);
                AcApp.DocumentManager.MdiActiveDocument?.SendStringToExecute("C1\n", true, false, false);
            }
            catch (System.Exception ex)
            {
                AcApp.DocumentManager.MdiActiveDocument?.Editor
                    ?.WriteMessage($"\n命令转发失败：{ex.Message}");
            }
        }

        // ================================================================
        //  面板 (Panel)
        // ================================================================

        /// <summary>打开 HY 工具面板</summary>
        [CommandMethod("hy")]
        public void Cmd_hy() => ShowPanelCommand.ShowHyToolPanel();

        // ================================================================
        //  钢筋绘制 (Reinforcement - Draw)
        // ================================================================

        /// <summary>绘制钢筋 (gj)</summary>
        [CommandMethod("gj")]
        public void Cmd_gj() => Run(() => new DrawReinforcementCommand().Execute());

        /// <summary>绘制偏移多段线 (gg)</summary>
        [CommandMethod("gg")]
        public void Cmd_gg()
        {
            #region agent log
            AgentDebugLogger.Log("routing", "H5", "CommandRegistry.Cmd_gg", "gg alias entered", new { });
            #endregion
            RouteThroughC1("gg", () => new DrawOffsetPolylineCommand().Execute());
        }

        /// <summary>外侧钢筋 (ggj)</summary>
        [CommandMethod("ggj")]
        public void Cmd_ggj() => Run(() => new ReinOutsideCommand().Execute());

        // ================================================================
        //  钢筋修改 (Reinforcement - Modify)
        // ================================================================

        /// <summary>添加水平锚固 (g1)</summary>
        [CommandMethod("g1")]
        public void Cmd_g1()
        {
            #region agent log
            AgentDebugLogger.Log("routing", "H5", "CommandRegistry.Cmd_g1", "g1 alias entered", new { });
            #endregion
            RouteThroughC1("g1", () => new ReinAddAnchorCommand(isVertical: false).Execute());
        }

        /// <summary>添加竖直锚固 (g2)</summary>
        [CommandMethod("g2")]
        public void Cmd_g2()
        {
            #region agent log
            AgentDebugLogger.Log("routing", "H5", "CommandRegistry.Cmd_g2", "g2 alias entered", new { });
            #endregion
            RouteThroughC1("g2", () => new ReinAddAnchorCommand(isVertical: true).Execute());
        }

        /// <summary>钢筋延伸 (ge)</summary>
        [CommandMethod("ge")]
        public void Cmd_ge()
        {
            #region agent log
            AgentDebugLogger.Log("routing", "H5", "CommandRegistry.Cmd_ge", "ge alias entered", new { });
            #endregion
            RouteThroughC1("ge", () => new ReinExtendCommand().Execute());
        }

        /// <summary>钢筋快速延伸 (ge1)</summary>
        [CommandMethod("ge1")]
        public void Cmd_ge1() => Run(() => new ReinQuickExtendCommand().Execute());

        /// <summary>钢筋裁剪 (gd)</summary>
        [CommandMethod("gd")]
        public void Cmd_gd() => Run(() => new ReinCutCommand().Execute());

        // ================================================================
        //  钢筋标注 (Reinforcement - Label)
        // ================================================================

        /// <summary>钢筋引线标注 - 标准 (gb)</summary>
        [CommandMethod("gb")]
        public void Cmd_gb() => Run(() => new MleaderReinCommand(MleaderReinCommand.Mode.Standard).Execute());

        /// <summary>钢筋引线标注 - 单根 (gb1)</summary>
        [CommandMethod("gb1")]
        public void Cmd_gb1() => Run(() => new MleaderReinCommand(MleaderReinCommand.Mode.Single).Execute());

        /// <summary>钢筋引线标注 - 六点 (gb2)</summary>
        [CommandMethod("gb2")]
        public void Cmd_gb2() => Run(() => new MleaderReinCommand(MleaderReinCommand.Mode.Six).Execute());

        /// <summary>选择钢筋文字 (hysrt)</summary>
        [CommandMethod("hysrt")]
        public void Cmd_hysrt() => Run(() => new SelectReinTextCommand().Execute());

        // ================================================================
        //  标高 (Elevation)
        // ================================================================

        /// <summary>绘制标高符号 (bg)</summary>
        [CommandMethod("bg")]
        public void Cmd_bg() => Run(() => new DrawElevationCommand().Execute());

        /// <summary>更新标高文字 (bgu)</summary>
        [CommandMethod("bgu")]
        public void Cmd_bgu() => Run(() => new UpdateElevationTextCommand().Execute());

        /// <summary>旋转标高符号 (bgR)</summary>
        [CommandMethod("bgR")]
        public void Cmd_bgR() => Run(() => new RotateElevationCommand().Execute());

        /// <summary>文字创建标高 (hybgTCE)</summary>
        [CommandMethod("hybgTCE")]
        public void Cmd_hybgTCE() => Run(() => new TextsCreateElevationCommand().Execute());

        // ================================================================
        //  尺寸标注 (Dimension)
        // ================================================================

        /// <summary>配筋尺寸标注 - 单个 (dds)</summary>
        [CommandMethod("dds")]
        public void Cmd_dds() => Run(() => new DimensionForReinforcementCommand().Execute());

        /// <summary>配筋尺寸标注 - 批量 (ddss)</summary>
        [CommandMethod("ddss")]
        public void Cmd_ddss() => Run(() => new DimensionForReinforcementBatchCommand().Execute());

        /// <summary>拆分尺寸标注 (sd)</summary>
        [CommandMethod("sd")]
        public void Cmd_sd() => Run(() => new SplitDimensionCommand().Execute());

        /// <summary>标注文字防重叠 (ddaa)</summary>
        [CommandMethod("ddaa")]
        public void Cmd_ddaa() => Run(() => new DimensionAlignCommand().Execute());

        /// <summary>交叉点添加顶点 (hydimA)</summary>
        [CommandMethod("hydimA")]
        public void Cmd_hydimA() => Run(() => new AddVertexAtIntersectionsCommand().Execute());

        // ================================================================
        //  地脚螺栓 (Anchor Bolt)
        // ================================================================

        /// <summary>附加地脚螺栓 (hyab)</summary>
        [CommandMethod("hyab")]
        public void Cmd_hyab() => Run(() => new AnchorBoltCommand().Execute());

        /// <summary>地脚螺栓对齐 (hyabA)</summary>
        [CommandMethod("hyabA")]
        public void Cmd_hyabA() => Run(() => new AnchorBoltAlignCommand().Execute());

        /// <summary>地脚螺栓计算 (hyabC)</summary>
        [CommandMethod("hyabC")]
        public void Cmd_hyabC() => Run(() => new AnchorBoltCalcCommand().Execute());

        /// <summary>地脚螺栓切换显示 (hyabCD)</summary>
        [CommandMethod("hyabCD")]
        public void Cmd_hyabCD() => Run(() => new AnchorBoltToggleDisplayCommand().Execute());

        /// <summary>地脚螺栓表格 (hyabCT)</summary>
        [CommandMethod("hyabCT")]
        public void Cmd_hyabCT() => Run(() => new AnchorBoltTableCommand().Execute());

        /// <summary>地脚螺栓截面 (hyabR)</summary>
        [CommandMethod("hyabR")]
        public void Cmd_hyabR() => Run(() => new AnchorBoltSectionCommand().Execute());

        // ================================================================
        //  设备基础 (Equipment Foundation)
        // ================================================================

        /// <summary>构建底座数据 (hyef_Base_ConstructBaseData)</summary>
        [CommandMethod("hyef_Base_ConstructBaseData")]
        public void Cmd_hyef_Base_ConstructBaseData() => Run(() => new EF_ConstructBaseDataCommand().Execute());

        /// <summary>高亮螺栓数据 (hyef_Base_HighlightBoltData)</summary>
        [CommandMethod("hyef_Base_HighlightBoltData")]
        public void Cmd_hyef_Base_HighlightBoltData() => Run(() => new EF_HighlightBoltDataCommand().Execute());

        /// <summary>构建轴线 (hyef_Axis_Construct)</summary>
        [CommandMethod("hyef_Axis_Construct")]
        public void Cmd_hyef_Axis_Construct() => Run(() => new EF_AxisConstructCommand().Execute());

        /// <summary>初始化轴线 (hyef_Axis_Initialize)</summary>
        [CommandMethod("hyef_Axis_Initialize")]
        public void Cmd_hyef_Axis_Initialize() => Run(() => new EF_AxisInitializeCommand().Execute());

        /// <summary>显示轴线结构 (hyef_Axis_Display)</summary>
        [CommandMethod("hyef_Axis_Display")]
        public void Cmd_hyef_Axis_Display() => Run(() => new EF_AxisDisplayCommand().Execute());

        /// <summary>创建轴线表格 (hyef_Axis_CreateTable)</summary>
        [CommandMethod("hyef_Axis_CreateTable")]
        public void Cmd_hyef_Axis_CreateTable() => Run(() => new EF_AxisCreateTableCommand().Execute());

        // ================================================================
        //  图框 / 布局 (Title Block / Layout)
        // ================================================================

        /// <summary>绘制图框 (HYMBRD)</summary>
        [CommandMethod("HYMBRD")]
        public void Cmd_HYMBRD() => Run(() => new DrawTitleBlockCommand().Execute());

        /// <summary>创建布局视口 (HYMBRC)</summary>
        [CommandMethod("HYMBRC")]
        public void Cmd_HYMBRC() => Run(() => new CreateLayoutViewportsCommand().Execute());

        /// <summary>打包视口 (HY_PackViewports)</summary>
        [CommandMethod("HY_PackViewports")]
        public void Cmd_HY_PackViewports() => Run(() => new PackViewportsCommand().Execute());

        // ================================================================
        //  块操作 (Block)
        // ================================================================

        /// <summary>块颜色修改 (HYc2bc)</summary>
        [CommandMethod("HYc2bc")]
        public void Cmd_HYc2bc() => Run(() => new BlockColorCommand().Execute());

        /// <summary>块转引线 (HYc2bl)</summary>
        [CommandMethod("HYc2bl")]
        public void Cmd_HYc2bl() => Run(() => new BlockToMLeaderCommand().Execute());

        // ================================================================
        //  多边形替换 (Polygon Replace)
        // ================================================================

        /// <summary>替换多边形 - 单个 (abrc)</summary>
        [CommandMethod("abrc")]
        public void Cmd_abrc() => Run(() => new ReplacePolygonCommand().Execute());

        /// <summary>替换多边形 - 批量 (abrcs)</summary>
        [CommandMethod("abrcs")]
        public void Cmd_abrcs() => Run(() => new ReplacePolygonBatchCommand().Execute());

        // ================================================================
        //  垫层 (Pad)
        // ================================================================

        /// <summary>从直线创建垫层 (HyDcL)</summary>
        [CommandMethod("HyDcL")]
        public void Cmd_HyDcL() => Run(() => new CreatePadFromLineCommand().Execute());

        /// <summary>从多段线创建垫层 (hyDcP)</summary>
        [CommandMethod("hyDcP")]
        public void Cmd_hyDcP() => Run(() => new CreatePadFromPolylineCommand().Execute());

        // ================================================================
        //  设计说明 (Design Spec / Markdown Editor)
        // ================================================================

        /// <summary>Markdown 设计说明排版 (hymd)</summary>
        [CommandMethod("hymd")]
        public void Cmd_hymd() => Run(() => new DesignSpecCommand().Execute());

        /// <summary>二次编辑 Markdown 设计说明 (hymdE)</summary>
        [CommandMethod("hymdE")]
        public void Cmd_hymdE() => Run(() => new DesignSpecEditCommand().Execute());

        // ================================================================
        //  导出 (Export)
        // ================================================================

        /// <summary>导出 Markdown 表格 (hyex)</summary>
        [CommandMethod("hyex")]
        public void Cmd_hyex() => Run(() => new ExportMarkdownTableCommand().Execute());

        /// <summary>导出实体属性到 CSV (hyex_csv)</summary>
        [CommandMethod("hyex_csv")]
        public void Cmd_hyex_csv() => Run(() => new ExportEntityPropertiesToCsvCommand().Execute());

        // ================================================================
        //  其他工具 (Misc)
        // ================================================================

        /// <summary>轴线文字对齐 (hyAxis)</summary>
        [CommandMethod("hyAxis")]
        public void Cmd_hyAxis() => Run(() => new AlignedAxisTextCommand().Execute());

        /// <summary>筏板厚度文字 (HyRT)</summary>
        [CommandMethod("HyRT")]
        public void Cmd_HyRT() => Run(() => new RaftThicknessTextCommand().Execute());

        /// <summary>每层绘制直线 (hydl)</summary>
        [CommandMethod("hydl")]
        public void Cmd_hydl() => Run(() => new DrawLinesOnEachLayerCommand().Execute());

        /// <summary>断线 (HYBL)</summary>
        [CommandMethod("HYBL")]
        public void Cmd_HYBL() => Run(() => new BreakLinesCommand().Execute());

        // ================================================================
        //  桩基 (Pile)
        // ================================================================

        /// <summary>绘制桩基 (HYpile)</summary>
        [CommandMethod("HYpile")]
        public void Cmd_HYpile() => Run(() => new DrawPilesCommand().Execute());

        /// <summary>桩基 Voronoi 优化 (HYpileV)</summary>
        [CommandMethod("HYpileV")]
        public void Cmd_HYpileV() => Run(() => new PileVoronoiOptimizationCommand().Execute());

        /// <summary>按标高分组圆 (HYpileG)</summary>
        [CommandMethod("HYpileG")]
        public void Cmd_HYpileG() => Run(() => new GroupCirclesByElevationCommand().Execute());

        /// <summary>在圆心/形心写入标高文字 (HYpileGT)</summary>
        [CommandMethod("HYpileGT")]
        public void Cmd_HYpileGT() => Run(() => new GroupCirclesByElevationCommand().ExecutePlaceElevationTextAtCentroids());

        // ================================================================
        //  沉降计算 (Settlement)
        // ================================================================

        /// <summary>
        /// 基础沉降计算：打开独立窗口（计算后可落图表格）。
        /// C2 热重载后通过 RouteThroughC1 转发，避免旧程序集 XAML 资源失效。
        /// </summary>
        [CommandMethod("HYJC")]
        public void Cmd_HYJC() => RouteThroughC1("hyjc", () => new SettlementCalculationCommand().Execute());

        /// <summary>同 HYJC（短别名）</summary>
        [CommandMethod("hySC")]
        public void Cmd_hySC() => RouteThroughC1("hyjc", () => new SettlementCalculationCommand().Execute());

        // ================================================================
        //  道路 (Road)
        // ================================================================

        /// <summary>绘制人行横道 (hyRoad)</summary>
        [CommandMethod("hyRoad")]
        public void Cmd_hyRoad() => Run(() => new DrawCrosswalkCommand().Execute());

        // --- P0 市政道路设计（Alignment / Profile / Template / Corridor）---
        //
        // 注意：以下命令均走 RouteThroughC1 转发。
        // [CommandMethod] 只在程序集 "首次 NETLOAD" 时被 AutoCAD 扫描注册；
        // C2 热重载用 Assembly.Load(byte[]) 把新程序集挂进来，AutoCAD **不会** 重扫新程序集的
        // [CommandMethod]。所以直接 new XxxCommand().Execute() 会被 AutoCAD 路由到"旧程序集"里的
        // 同名类型（代码还是改动前那份）。
        //
        // RouteThroughC1 模式：旧程序集里的 Cmd_* 方法只做一件事——把 commandKey 落到临时文件
        // (CommandRelayStore)，然后 SendStringToExecute("C1\n")；AutoCAD 执行 C1 → ReCall._c1Action
        // 指向的就是 *最新程序集* 里 TestCommand.Run，它读 CommandRelayStore 并分派到对应的
        // new RoadXxxCommand().Execute()——此时 "new" 出来的是最新程序集里的类型，代码即刻生效。
        //
        // 与 gg / g1 / g2 / ge / HYJC 等命令完全一致。

        /// <summary>新建平面线位（P0 占位，P1 支持拾取多段线）</summary>
        [CommandMethod("hyRoadA")]
        public void Cmd_hyRoadA() => RouteThroughC1("hyRoadA", () => new Road.RoadAlignmentCommand().Execute());

        /// <summary>为当前 DWG 全部 Alignment 生成桩号标注（P1，主 20m + 副 5m，幂等可重跑）</summary>
        [CommandMethod("hyRoadAlnStation")]
        public void Cmd_hyRoadAlnStation() => RouteThroughC1("hyRoadAlnStation", () => new Road.RoadAlignmentStationCommand().Execute());

        /// <summary>为首条 Alignment 创建设计纵断面（P0 占位，P3 扩展）</summary>
        [CommandMethod("hyRoadP")]
        public void Cmd_hyRoadP() => RouteThroughC1("hyRoadP", () => new Road.RoadProfileCommand().Execute());

        /// <summary>创建横断面模板（P0 占位，P2 上线模板编辑器）</summary>
        [CommandMethod("hyRoadT")]
        public void Cmd_hyRoadT() => RouteThroughC1("hyRoadT", () => new Road.RoadTemplateCommand().Execute());

        /// <summary>创建走廊 Corridor（P0 占位，P4 上线分段/目标映射）</summary>
        [CommandMethod("hyRoadC")]
        public void Cmd_hyRoadC() => RouteThroughC1("hyRoadC", () => new Road.RoadCorridorCommand().Execute());

        /// <summary>立即将 RoadDesign 同步落盘到 .roaddesign.json（v1.1 已无防抖）</summary>
        [CommandMethod("hyRoadSave")]
        public void Cmd_hyRoadSave() => RouteThroughC1("hyRoadSave", () => new Road.RoadOpenJsonCommand().Execute());

        /// <summary>从 .roaddesign.json 重新加载 RoadDesign</summary>
        [CommandMethod("hyRoadLoad")]
        public void Cmd_hyRoadLoad() => RouteThroughC1("hyRoadLoad", () => new Road.RoadImportJsonCommand().Execute());

        /// <summary>
        /// 预留命令（v1 调用将得到"v2 启用"提示）：glTF 三维导出。
        /// v2（P7）在 Autofac 中将 IThreeDExportPort 切换为真实实现后立即可用。
        /// </summary>
        [CommandMethod("hyRoad3dExportGltf")]
        public void Cmd_hyRoad3dExportGltf() => RouteThroughC1("hyRoad3dExportGltf", () => new Road.Road3dExportGltfCommand().Execute());

        // ================================================================
        //  面板内部执行命令（直接加载 DLL 时替代 ReCall 的 C1）
        // ================================================================

        /// <summary>
        /// 面板按钮执行入口：消费 PendingCommand 并执行。
        /// 直接 NETLOAD 时，面板通过 SendStringToExecute("_HyExec\n") 路由到此命令。
        /// 通过 C2 热重载时，ReCall 的 C1 → TestCommand.Run() 处理，不走此路径。
        /// </summary>
        [CommandMethod("_HyExec")]
        public void Cmd_HyExec()
        {
            #region agent log
            AgentDebugLogger.Log("routing", "H6", "CommandRegistry.Cmd_HyExec", "hyexec entered", new { });
            #endregion
            var command = SettingsPanelViewModel.ConsumePendingCommand();
            if (command != null)
            {
                try
                {
                    command();
                }
                catch (System.Exception ex)
                {
                    AcApp.DocumentManager.MdiActiveDocument?.Editor
                        ?.WriteMessage($"\n错误：{ex.Message}");
                }
            }
        }
    }
}
