// =============================================================================
// 生产模式命令门面（仅在 Configuration=Production 时编译）
// =============================================================================
//
// 本文件是 ReCall/CommandFacade.cs 的 1:1 镜像，区别仅在：
//   - 命名空间        HyCADTool.ReCall                → HyCADTool.Production
//   - 类名            CommandFacade                   → ProductionCommandFacade
//   - 调用目标        ReCallClass.Invoke              → ProductionDispatcher.Invoke
//
// 维护约定（重要）：
//   ★ 在 ReCall/CommandFacade.cs 增删 [CommandMethod] 后，**必须同步**修改本文件。
//   ★ 同步只是机械文本替换，可用 IDE 多行编辑或直接 PowerShell：
//       (Get-Content ReCall\CommandFacade.cs -Raw) `
//         -replace 'namespace HyCADTool\.ReCall', 'namespace HyCADTool.App.Production' `
//         -replace 'class CommandFacade', 'class ProductionCommandFacade' `
//         -replace 'ReCallClass\.Invoke', 'ProductionDispatcher.Invoke' `
//         -replace '\[assembly: CommandClass.*\]', '' `
//         | Set-Content HyCADTool\Production\ProductionCommandFacade.cs
//   ★ assembly attribute 已经写在 ProductionEntry.cs，本文件不再重复。
//
// 同步基线：与 ReCall/CommandFacade.cs 的 2026-04-25 版本对齐（144 条命令）。
// =============================================================================

#if HYCAD_PRODUCTION

using Autodesk.AutoCAD.Runtime;
using HyCADTool.Shell.Licensing;

namespace HyCADTool.App.Production
{
    /// <summary>生产模式 AutoCAD 命令总入口（NETLOAD 后即注册）。</summary>
    public class ProductionCommandFacade
    {
        #region 面板 (Panel)
        [CommandMethod("hy")]      public void Cmd_hy() => LicenseGate.RunGated("hy", () => ProductionDispatcher.Invoke("hy"));
        [CommandMethod("HyB")]     public void Cmd_HyB() => LicenseGate.RunGated("HyB", () => ProductionDispatcher.Invoke("HyB"));
        [CommandMethod("_HyExec")] public void Cmd__HyExec() => LicenseGate.RunGated("_HyExec", () => ProductionDispatcher.Invoke("_HyExec"));
        [CommandMethod("hyLicense")] public void Cmd_hyLicense() => LicenseGate.RunGated("hyLicense", () => ProductionDispatcher.Invoke("hyLicense"));
        #endregion

        #region 钢筋绘制 (Reinforcement - Draw)
        [CommandMethod("gj")]  public void Cmd_gj() => LicenseGate.RunGated("gj", () => ProductionDispatcher.Invoke("gj"));
        [CommandMethod("gg")]  public void Cmd_gg() => LicenseGate.RunGated("gg", () => ProductionDispatcher.Invoke("gg"));
        [CommandMethod("ggj")] public void Cmd_ggj() => LicenseGate.RunGated("ggj", () => ProductionDispatcher.Invoke("ggj"));
        #endregion

        #region 钢筋修改 (Reinforcement - Modify)
        [CommandMethod("g1")]  public void Cmd_g1() => LicenseGate.RunGated("g1", () => ProductionDispatcher.Invoke("g1"));
        [CommandMethod("g2")]  public void Cmd_g2() => LicenseGate.RunGated("g2", () => ProductionDispatcher.Invoke("g2"));
        [CommandMethod("ge")]  public void Cmd_ge() => LicenseGate.RunGated("ge", () => ProductionDispatcher.Invoke("ge"));
        [CommandMethod("ge1")] public void Cmd_ge1() => LicenseGate.RunGated("ge1", () => ProductionDispatcher.Invoke("ge1"));
        [CommandMethod("gd")]  public void Cmd_gd() => LicenseGate.RunGated("gd", () => ProductionDispatcher.Invoke("gd"));
        #endregion

        #region 钢筋标注 (Reinforcement - Label)
        [CommandMethod("gb")]    public void Cmd_gb() => LicenseGate.RunGated("gb", () => ProductionDispatcher.Invoke("gb"));
        [CommandMethod("gb1")]   public void Cmd_gb1() => LicenseGate.RunGated("gb1", () => ProductionDispatcher.Invoke("gb1"));
        [CommandMethod("gb2")]   public void Cmd_gb2() => LicenseGate.RunGated("gb2", () => ProductionDispatcher.Invoke("gb2"));
        [CommandMethod("hysrt")] public void Cmd_hysrt() => LicenseGate.RunGated("hysrt", () => ProductionDispatcher.Invoke("hysrt"));
        #endregion

        #region 标高 (Elevation)
        [CommandMethod("bg")]      public void Cmd_bg() => LicenseGate.RunGated("bg", () => ProductionDispatcher.Invoke("bg"));
        [CommandMethod("bgu")]     public void Cmd_bgu() => LicenseGate.RunGated("bgu", () => ProductionDispatcher.Invoke("bgu"));
        [CommandMethod("bgR")]     public void Cmd_bgR() => LicenseGate.RunGated("bgR", () => ProductionDispatcher.Invoke("bgR"));
        [CommandMethod("hybgTCE")] public void Cmd_hybgTCE() => LicenseGate.RunGated("hybgTCE", () => ProductionDispatcher.Invoke("hybgTCE"));
        #endregion

        #region 尺寸标注 (Dimension)
        [CommandMethod("dds")]    public void Cmd_dds() => LicenseGate.RunGated("dds", () => ProductionDispatcher.Invoke("dds"));
        [CommandMethod("ddss")]   public void Cmd_ddss() => LicenseGate.RunGated("ddss", () => ProductionDispatcher.Invoke("ddss"));
        [CommandMethod("sd")]     public void Cmd_sd() => LicenseGate.RunGated("sd", () => ProductionDispatcher.Invoke("sd"));
        [CommandMethod("ddaa")]   public void Cmd_ddaa() => LicenseGate.RunGated("ddaa", () => ProductionDispatcher.Invoke("ddaa"));
        [CommandMethod("hydimA")] public void Cmd_hydimA() => LicenseGate.RunGated("hydimA", () => ProductionDispatcher.Invoke("hydimA"));
        #endregion

        #region 地脚螺栓 (Anchor Bolt)
        [CommandMethod("hyab")]   public void Cmd_hyab() => LicenseGate.RunGated("hyab", () => ProductionDispatcher.Invoke("hyab"));
        [CommandMethod("hyabA")]  public void Cmd_hyabA() => LicenseGate.RunGated("hyabA", () => ProductionDispatcher.Invoke("hyabA"));
        [CommandMethod("hyabC")]  public void Cmd_hyabC() => LicenseGate.RunGated("hyabC", () => ProductionDispatcher.Invoke("hyabC"));
        [CommandMethod("hyabCD")] public void Cmd_hyabCD() => LicenseGate.RunGated("hyabCD", () => ProductionDispatcher.Invoke("hyabCD"));
        [CommandMethod("hyabCT")] public void Cmd_hyabCT() => LicenseGate.RunGated("hyabCT", () => ProductionDispatcher.Invoke("hyabCT"));
        [CommandMethod("hyabR")]  public void Cmd_hyabR() => LicenseGate.RunGated("hyabR", () => ProductionDispatcher.Invoke("hyabR"));
        #endregion

        #region 设备基础 (Equipment Foundation)
        [CommandMethod("hyef_Base_ConstructBaseData")] public void Cmd_hyef_Base_ConstructBaseData() => LicenseGate.RunGated("hyef_Base_ConstructBaseData", () => ProductionDispatcher.Invoke("hyef_Base_ConstructBaseData"));
        [CommandMethod("hyef_Base_HighlightBoltData")] public void Cmd_hyef_Base_HighlightBoltData() => LicenseGate.RunGated("hyef_Base_HighlightBoltData", () => ProductionDispatcher.Invoke("hyef_Base_HighlightBoltData"));
        [CommandMethod("hyef_Axis_Construct")]         public void Cmd_hyef_Axis_Construct() => LicenseGate.RunGated("hyef_Axis_Construct", () => ProductionDispatcher.Invoke("hyef_Axis_Construct"));
        [CommandMethod("hyef_Axis_Initialize")]        public void Cmd_hyef_Axis_Initialize() => LicenseGate.RunGated("hyef_Axis_Initialize", () => ProductionDispatcher.Invoke("hyef_Axis_Initialize"));
        [CommandMethod("hyef_Axis_Display")]           public void Cmd_hyef_Axis_Display() => LicenseGate.RunGated("hyef_Axis_Display", () => ProductionDispatcher.Invoke("hyef_Axis_Display"));
        [CommandMethod("hyef_Axis_CreateTable")]       public void Cmd_hyef_Axis_CreateTable() => LicenseGate.RunGated("hyef_Axis_CreateTable", () => ProductionDispatcher.Invoke("hyef_Axis_CreateTable"));
        #endregion

        #region 图框 / 布局 (Title Block / Layout)
        [CommandMethod("HYMBRD")]           public void Cmd_HYMBRD() => LicenseGate.RunGated("HYMBRD", () => ProductionDispatcher.Invoke("HYMBRD"));
        [CommandMethod("HYMBRC")]           public void Cmd_HYMBRC() => LicenseGate.RunGated("HYMBRC", () => ProductionDispatcher.Invoke("HYMBRC"));
        [CommandMethod("HY_PackViewports")] public void Cmd_HY_PackViewports() => LicenseGate.RunGated("HY_PackViewports", () => ProductionDispatcher.Invoke("HY_PackViewports"));
        #endregion

        #region 图纸视口 (Paper Viewport)
        [CommandMethod("ph",  CommandFlags.Modal | CommandFlags.UsePickSet | CommandFlags.Redraw)] public void Cmd_ph() => LicenseGate.RunGated("ph", () => ProductionDispatcher.Invoke("ph"));
        [CommandMethod("pv",  CommandFlags.Modal | CommandFlags.UsePickSet | CommandFlags.Redraw)] public void Cmd_pv() => LicenseGate.RunGated("pv", () => ProductionDispatcher.Invoke("pv"));
        [CommandMethod("phh", CommandFlags.Modal | CommandFlags.UsePickSet | CommandFlags.Redraw)] public void Cmd_phh() => LicenseGate.RunGated("phh", () => ProductionDispatcher.Invoke("phh"));
        [CommandMethod("pvv", CommandFlags.Modal | CommandFlags.UsePickSet | CommandFlags.Redraw)] public void Cmd_pvv() => LicenseGate.RunGated("pvv", () => ProductionDispatcher.Invoke("pvv"));
        #endregion

        #region 块操作 (Block)
        [CommandMethod("HYc2bc")] public void Cmd_HYc2bc() => LicenseGate.RunGated("HYc2bc", () => ProductionDispatcher.Invoke("HYc2bc"));
        [CommandMethod("HYc2bl")] public void Cmd_HYc2bl() => LicenseGate.RunGated("HYc2bl", () => ProductionDispatcher.Invoke("HYc2bl"));
        #endregion

        #region 多边形替换 (Polygon Replace)
        [CommandMethod("abrc")]  public void Cmd_abrc() => LicenseGate.RunGated("abrc", () => ProductionDispatcher.Invoke("abrc"));
        [CommandMethod("abrcs")] public void Cmd_abrcs() => LicenseGate.RunGated("abrcs", () => ProductionDispatcher.Invoke("abrcs"));
        #endregion

        #region 垫层 (Pad)
        [CommandMethod("HyDcL")] public void Cmd_HyDcL() => LicenseGate.RunGated("HyDcL", () => ProductionDispatcher.Invoke("HyDcL"));
        [CommandMethod("hyDcP")] public void Cmd_hyDcP() => LicenseGate.RunGated("hyDcP", () => ProductionDispatcher.Invoke("hyDcP"));
        #endregion

        #region 设计说明 (Design Spec / Markdown)
        [CommandMethod("hymd")]  public void Cmd_hymd() => LicenseGate.RunGated("hymd", () => ProductionDispatcher.Invoke("hymd"));
        [CommandMethod("hymdE")] public void Cmd_hymdE() => LicenseGate.RunGated("hymdE", () => ProductionDispatcher.Invoke("hymdE"));
        #endregion

        #region 导出 (Export)
        [CommandMethod("hyex")]        public void Cmd_hyex() => LicenseGate.RunGated("hyex", () => ProductionDispatcher.Invoke("hyex"));
        [CommandMethod("hyex_csv")]    public void Cmd_hyex_csv() => LicenseGate.RunGated("hyex_csv", () => ProductionDispatcher.Invoke("hyex_csv"));
        [CommandMethod("hyLtCapture")] public void Cmd_hyLtCapture() => LicenseGate.RunGated("hyLtCapture", () => ProductionDispatcher.Invoke("hyLtCapture"));
        #endregion

        #region 其他工具 (Misc)
        [CommandMethod("hyAxis")] public void Cmd_hyAxis() => LicenseGate.RunGated("hyAxis", () => ProductionDispatcher.Invoke("hyAxis"));
        [CommandMethod("HyRT")]   public void Cmd_HyRT() => LicenseGate.RunGated("HyRT", () => ProductionDispatcher.Invoke("HyRT"));
        [CommandMethod("hydl")]   public void Cmd_hydl() => LicenseGate.RunGated("hydl", () => ProductionDispatcher.Invoke("hydl"));
        [CommandMethod("HYBL")]   public void Cmd_HYBL() => LicenseGate.RunGated("HYBL", () => ProductionDispatcher.Invoke("HYBL"));
        #endregion

        #region 桩基 (Pile)
        [CommandMethod("HYpile")]   public void Cmd_HYpile() => LicenseGate.RunGated("HYpile", () => ProductionDispatcher.Invoke("HYpile"));
        [CommandMethod("HYpileV")]  public void Cmd_HYpileV() => LicenseGate.RunGated("HYpileV", () => ProductionDispatcher.Invoke("HYpileV"));
        [CommandMethod("HYpileG")]  public void Cmd_HYpileG() => LicenseGate.RunGated("HYpileG", () => ProductionDispatcher.Invoke("HYpileG"));
        [CommandMethod("HYpileGT")] public void Cmd_HYpileGT() => LicenseGate.RunGated("HYpileGT", () => ProductionDispatcher.Invoke("HYpileGT"));
        #endregion

        #region 沉降 (Settlement)
        [CommandMethod("HYJC")] public void Cmd_HYJC() => LicenseGate.RunGated("HYJC", () => ProductionDispatcher.Invoke("HYJC"));
        [CommandMethod("hySC")] public void Cmd_hySC() => LicenseGate.RunGated("hySC", () => ProductionDispatcher.Invoke("hySC"));
        #endregion

        #region 道路 (Road) — hyRoad* + r* 短别名
        [CommandMethod("hyRoadTree")][CommandMethod("rTree")]               public void Cmd_hyRoadTree() => LicenseGate.RunGated("hyRoadTree", () => ProductionDispatcher.Invoke("hyRoadTree"));
        [CommandMethod("hyRoad")][CommandMethod("rCx")]                     public void Cmd_hyRoad() => LicenseGate.RunGated("hyRoad", () => ProductionDispatcher.Invoke("hyRoad"));
        [CommandMethod("hyRoadA")][CommandMethod("rLa")]                    public void Cmd_hyRoadA() => LicenseGate.RunGated("hyRoadA", () => ProductionDispatcher.Invoke("hyRoadA"));
        [CommandMethod("hyRoadAName")][CommandMethod("rAn")]                public void Cmd_hyRoadAName() => LicenseGate.RunGated("hyRoadAName", () => ProductionDispatcher.Invoke("hyRoadAName"));
        [CommandMethod("hyRoadAlnAssign")][CommandMethod("rAsg")]           public void Cmd_hyRoadAlnAssign() => LicenseGate.RunGated("hyRoadAlnAssign", () => ProductionDispatcher.Invoke("hyRoadAlnAssign"));
        [CommandMethod("hyRoadAlnPlan")][CommandMethod("rPlp")]             public void Cmd_hyRoadAlnPlan() => LicenseGate.RunGated("hyRoadAlnPlan", () => ProductionDispatcher.Invoke("hyRoadAlnPlan"));
        [CommandMethod("hyRoadAutoIntersection")][CommandMethod("rIa")]     public void Cmd_hyRoadAutoIntersection() => LicenseGate.RunGated("hyRoadAutoIntersection", () => ProductionDispatcher.Invoke("hyRoadAutoIntersection"));
        [CommandMethod("hyRoadAw")][CommandMethod("rLaw")]                  public void Cmd_hyRoadAw() => LicenseGate.RunGated("hyRoadAw", () => ProductionDispatcher.Invoke("hyRoadAw"));
        [CommandMethod("hyRoadAlnByPi")][CommandMethod("rPi")]              public void Cmd_hyRoadAlnByPi() => LicenseGate.RunGated("hyRoadAlnByPi", () => ProductionDispatcher.Invoke("hyRoadAlnByPi"));
        [CommandMethod("hyRoadAlnEditPi")][CommandMethod("rWk")]            public void Cmd_hyRoadAlnEditPi() => LicenseGate.RunGated("hyRoadAlnEditPi", () => ProductionDispatcher.Invoke("hyRoadAlnEditPi"));
        [CommandMethod("hyRoadAlnStation")][CommandMethod("rSt")]           public void Cmd_hyRoadAlnStation() => LicenseGate.RunGated("hyRoadAlnStation", () => ProductionDispatcher.Invoke("hyRoadAlnStation"));
        [CommandMethod("hyRoadAlnTable")][CommandMethod("rSe")]             public void Cmd_hyRoadAlnTable() => LicenseGate.RunGated("hyRoadAlnTable", () => ProductionDispatcher.Invoke("hyRoadAlnTable"));
        [CommandMethod("hyRoadAlnGeomPt")][CommandMethod("rGp")]            public void Cmd_hyRoadAlnGeomPt() => LicenseGate.RunGated("hyRoadAlnGeomPt", () => ProductionDispatcher.Invoke("hyRoadAlnGeomPt"));
        [CommandMethod("hyRoadAlnExportPi")][CommandMethod("rEp")]          public void Cmd_hyRoadAlnExportPi() => LicenseGate.RunGated("hyRoadAlnExportPi", () => ProductionDispatcher.Invoke("hyRoadAlnExportPi"));
        [CommandMethod("hyRoadAlnExportFrame")][CommandMethod("rEf")]       public void Cmd_hyRoadAlnExportFrame() => LicenseGate.RunGated("hyRoadAlnExportFrame", () => ProductionDispatcher.Invoke("hyRoadAlnExportFrame"));
        [CommandMethod("hyRoadAlnDefaults")][CommandMethod("rDd")]          public void Cmd_hyRoadAlnDefaults() => LicenseGate.RunGated("hyRoadAlnDefaults", () => ProductionDispatcher.Invoke("hyRoadAlnDefaults"));
        [CommandMethod("hyRoadAlnInsertPi")][CommandMethod("rIns")]         public void Cmd_hyRoadAlnInsertPi() => LicenseGate.RunGated("hyRoadAlnInsertPi", () => ProductionDispatcher.Invoke("hyRoadAlnInsertPi"));
        [CommandMethod("hyRoadAlnDeletePi")][CommandMethod("rDp")]          public void Cmd_hyRoadAlnDeletePi() => LicenseGate.RunGated("hyRoadAlnDeletePi", () => ProductionDispatcher.Invoke("hyRoadAlnDeletePi"));
        [CommandMethod("hyRoadAlnStaEq")][CommandMethod("rEq")]             public void Cmd_hyRoadAlnStaEq() => LicenseGate.RunGated("hyRoadAlnStaEq", () => ProductionDispatcher.Invoke("hyRoadAlnStaEq"));
        [CommandMethod("hyRoadAlnReverse")][CommandMethod("rRv")]           public void Cmd_hyRoadAlnReverse() => LicenseGate.RunGated("hyRoadAlnReverse", () => ProductionDispatcher.Invoke("hyRoadAlnReverse"));
        [CommandMethod("hyRoadAlnOffset")][CommandMethod("rOf")]            public void Cmd_hyRoadAlnOffset() => LicenseGate.RunGated("hyRoadAlnOffset", () => ProductionDispatcher.Invoke("hyRoadAlnOffset"));
        [CommandMethod("hyRoadAlnUserPickDraw")]                            public void Cmd_hyRoadAlnUserPickDraw() => LicenseGate.RunGated("hyRoadAlnUserPickDraw", () => ProductionDispatcher.Invoke("hyRoadAlnUserPickDraw"));
        [CommandMethod("hyRoadAlnUserPickDrawWB")]                          public void Cmd_hyRoadAlnUserPickDrawWB() => LicenseGate.RunGated("hyRoadAlnUserPickDrawWB", () => ProductionDispatcher.Invoke("hyRoadAlnUserPickDrawWB"));
        [CommandMethod("hyRoadAlnRawShow")]                                 public void Cmd_hyRoadAlnRawShow() => LicenseGate.RunGated("hyRoadAlnRawShow", () => ProductionDispatcher.Invoke("hyRoadAlnRawShow"));
        [CommandMethod("hyRoadAlnRawHide")]                                 public void Cmd_hyRoadAlnRawHide() => LicenseGate.RunGated("hyRoadAlnRawHide", () => ProductionDispatcher.Invoke("hyRoadAlnRawHide"));
        [CommandMethod("hyRoadAlnUserPickRegister")][CommandMethod("rLap")] public void Cmd_hyRoadAlnUserPickRegister() => LicenseGate.RunGated("hyRoadAlnUserPickRegister", () => ProductionDispatcher.Invoke("hyRoadAlnUserPickRegister"));
        [CommandMethod("hyRoadAlnCommit")][CommandMethod("rLac")]           public void Cmd_hyRoadAlnCommit() => LicenseGate.RunGated("hyRoadAlnCommit", () => ProductionDispatcher.Invoke("hyRoadAlnCommit"));
        [CommandMethod("hyRoadAlnDeletePick")]                              public void Cmd_hyRoadAlnDeletePick() => LicenseGate.RunGated("hyRoadAlnDeletePick", () => ProductionDispatcher.Invoke("hyRoadAlnDeletePick"));
        [CommandMethod("hyRoadAlnExportXml")][CommandMethod("rXo")]         public void Cmd_hyRoadAlnExportXml() => LicenseGate.RunGated("hyRoadAlnExportXml", () => ProductionDispatcher.Invoke("hyRoadAlnExportXml"));
        [CommandMethod("hyRoadAlnImportXml")][CommandMethod("rXi")]         public void Cmd_hyRoadAlnImportXml() => LicenseGate.RunGated("hyRoadAlnImportXml", () => ProductionDispatcher.Invoke("hyRoadAlnImportXml"));
        [CommandMethod("hyRoadIntersection")][CommandMethod("rIs")]         public void Cmd_hyRoadIntersection() => LicenseGate.RunGated("hyRoadIntersection", () => ProductionDispatcher.Invoke("hyRoadIntersection"));
        [CommandMethod("hyRoadIntersectionEdit")][CommandMethod("rIe")]     public void Cmd_hyRoadIntersectionEdit() => LicenseGate.RunGated("hyRoadIntersectionEdit", () => ProductionDispatcher.Invoke("hyRoadIntersectionEdit"));
        [CommandMethod("hyRoadIntersectionKerbChain")][CommandMethod("rIk")] public void Cmd_hyRoadIntersectionKerbChain() => LicenseGate.RunGated("hyRoadIntersectionKerbChain", () => ProductionDispatcher.Invoke("hyRoadIntersectionKerbChain"));
        [CommandMethod("hyRoadIntersectionCrosswalk")][CommandMethod("rIw")] public void Cmd_hyRoadIntersectionCrosswalk() => LicenseGate.RunGated("hyRoadIntersectionCrosswalk", () => ProductionDispatcher.Invoke("hyRoadIntersectionCrosswalk"));
        [CommandMethod("hyRoadCurbRamp")][CommandMethod("rCr")]             public void Cmd_hyRoadCurbRamp() => LicenseGate.RunGated("hyRoadCurbRamp", () => ProductionDispatcher.Invoke("hyRoadCurbRamp"));
        [CommandMethod("hyRoadTactilePaving")][CommandMethod("rTp")]        public void Cmd_hyRoadTactilePaving() => LicenseGate.RunGated("hyRoadTactilePaving", () => ProductionDispatcher.Invoke("hyRoadTactilePaving"));
        [CommandMethod("hyRoadStopLine")][CommandMethod("rSl")]             public void Cmd_hyRoadStopLine() => LicenseGate.RunGated("hyRoadStopLine", () => ProductionDispatcher.Invoke("hyRoadStopLine"));
        [CommandMethod("hyRoadLaneMarking")][CommandMethod("rLm")]          public void Cmd_hyRoadLaneMarking() => LicenseGate.RunGated("hyRoadLaneMarking", () => ProductionDispatcher.Invoke("hyRoadLaneMarking"));
        [CommandMethod("hyRoadArrow")][CommandMethod("rAr")]                public void Cmd_hyRoadArrow() => LicenseGate.RunGated("hyRoadArrow", () => ProductionDispatcher.Invoke("hyRoadArrow"));
        [CommandMethod("hyRoadP")][CommandMethod("rPr")]                    public void Cmd_hyRoadP() => LicenseGate.RunGated("hyRoadP", () => ProductionDispatcher.Invoke("hyRoadP"));
        [CommandMethod("hyRoadProfFG")][CommandMethod("rFg")]               public void Cmd_hyRoadProfFG() => LicenseGate.RunGated("hyRoadProfFG", () => ProductionDispatcher.Invoke("hyRoadProfFG"));
        [CommandMethod("hyRoadProfEG")][CommandMethod("rEg")]               public void Cmd_hyRoadProfEG() => LicenseGate.RunGated("hyRoadProfEG", () => ProductionDispatcher.Invoke("hyRoadProfEG"));
        [CommandMethod("hyRoadProfLabel")][CommandMethod("rPl")]            public void Cmd_hyRoadProfLabel() => LicenseGate.RunGated("hyRoadProfLabel", () => ProductionDispatcher.Invoke("hyRoadProfLabel"));
        [CommandMethod("hyRoadT")][CommandMethod("rT1")]                    public void Cmd_hyRoadT() => LicenseGate.RunGated("hyRoadT", () => ProductionDispatcher.Invoke("hyRoadT"));
        [CommandMethod("hyRoadCs")][CommandMethod("rCs")]                   public void Cmd_hyRoadCs() => LicenseGate.RunGated("hyRoadCs", () => ProductionDispatcher.Invoke("hyRoadCs"));
        [CommandMethod("hyRoadCsLoad")][CommandMethod("rCsL")]              public void Cmd_hyRoadCsLoad() => LicenseGate.RunGated("hyRoadCsLoad", () => ProductionDispatcher.Invoke("hyRoadCsLoad"));
        [CommandMethod("hyRoadCsQuick")][CommandMethod("rCsQ")]             public void Cmd_hyRoadCsQuick() => LicenseGate.RunGated("hyRoadCsQuick", () => ProductionDispatcher.Invoke("hyRoadCsQuick"));
        [CommandMethod("hyRoadC")][CommandMethod("rCo")]                    public void Cmd_hyRoadC() => LicenseGate.RunGated("hyRoadC", () => ProductionDispatcher.Invoke("hyRoadC"));
        [CommandMethod("hyRoadSave")][CommandMethod("rSv")]                 public void Cmd_hyRoadSave() => LicenseGate.RunGated("hyRoadSave", () => ProductionDispatcher.Invoke("hyRoadSave"));
        [CommandMethod("hyRoadLoad")][CommandMethod("rLd")]                 public void Cmd_hyRoadLoad() => LicenseGate.RunGated("hyRoadLoad", () => ProductionDispatcher.Invoke("hyRoadLoad"));
        [CommandMethod("hyRoad3dExportGltf")][CommandMethod("rGf")]         public void Cmd_hyRoad3dExportGltf() => LicenseGate.RunGated("hyRoad3dExportGltf", () => ProductionDispatcher.Invoke("hyRoad3dExportGltf"));
        [CommandMethod("hyRoadSeg3")][CommandMethod("r3s")]                 public void Cmd_hyRoadSeg3() => LicenseGate.RunGated("hyRoadSeg3", () => ProductionDispatcher.Invoke("hyRoadSeg3"));
        #endregion

        #region 几何工具 (Geometry Tools)
        [CommandMethod("HYDCEL")]    public void Cmd_HYDCEL() => LicenseGate.RunGated("HYDCEL", () => ProductionDispatcher.Invoke("HYDCEL"));
        [CommandMethod("HYDCELSET")] public void Cmd_HYDCELSET() => LicenseGate.RunGated("HYDCELSET", () => ProductionDispatcher.Invoke("HYDCELSET"));
        [CommandMethod("HYMBR")]     public void Cmd_HYMBR() => LicenseGate.RunGated("HYMBR", () => ProductionDispatcher.Invoke("HYMBR"));
        [CommandMethod("HYJP")]      public void Cmd_HYJP() => LicenseGate.RunGated("HYJP", () => ProductionDispatcher.Invoke("HYJP"));
        [CommandMethod("HYOV")]      public void Cmd_HYOV() => LicenseGate.RunGated("HYOV", () => ProductionDispatcher.Invoke("HYOV"));
        [CommandMethod("HYOVSET")]   public void Cmd_HYOVSET() => LicenseGate.RunGated("HYOVSET", () => ProductionDispatcher.Invoke("HYOVSET"));
        [CommandMethod("HYBC")]      public void Cmd_HYBC() => LicenseGate.RunGated("HYBC", () => ProductionDispatcher.Invoke("HYBC"));
        // 注：hySeg3 在开发模式注册在 ReCallClass 上（与 ReCall.dll 同程序集）。
        // 生产模式 ReCall.dll 不存在，hySeg3 也通过 commands.json 路由（如未在 commands.json 中，请新增一条占位）。
        [CommandMethod("hySeg3")]    public void Cmd_hySeg3() => LicenseGate.RunGated("hySeg3", () => ProductionDispatcher.Invoke("hySeg3"));
        #endregion

        #region 三维建模 (3D Modeling)
        [CommandMethod("HY3")] public void Cmd_HY3() => LicenseGate.RunGated("HY3", () => ProductionDispatcher.Invoke("HY3"));
        #endregion

        #region 诊断 (Diagnostic)
        [CommandMethod("HYLOCATETIF")] public void Cmd_HYLOCATETIF() => LicenseGate.RunGated("HYLOCATETIF", () => ProductionDispatcher.Invoke("HYLOCATETIF"));
        [CommandMethod("CHECKWPF")]    public void Cmd_CHECKWPF() => LicenseGate.RunGated("CHECKWPF", () => ProductionDispatcher.Invoke("CHECKWPF"));
        [CommandMethod("hyCmdList")]   public void Cmd_hyCmdList() => LicenseGate.RunGated("hyCmdList", () => ProductionDispatcher.Invoke("hyCmdList"));
        #endregion

        #region 占位符 (Placeholders N1~N50)
        [CommandMethod("N1")]  public void Cmd_N1() => LicenseGate.RunGated("N1", () => ProductionDispatcher.Invoke("N1"));
        [CommandMethod("N2")]  public void Cmd_N2() => LicenseGate.RunGated("N2", () => ProductionDispatcher.Invoke("N2"));
        [CommandMethod("N3")]  public void Cmd_N3() => LicenseGate.RunGated("N3", () => ProductionDispatcher.Invoke("N3"));
        [CommandMethod("N4")]  public void Cmd_N4() => LicenseGate.RunGated("N4", () => ProductionDispatcher.Invoke("N4"));
        [CommandMethod("N5")]  public void Cmd_N5() => LicenseGate.RunGated("N5", () => ProductionDispatcher.Invoke("N5"));
        [CommandMethod("N6")]  public void Cmd_N6() => LicenseGate.RunGated("N6", () => ProductionDispatcher.Invoke("N6"));
        [CommandMethod("N7")]  public void Cmd_N7() => LicenseGate.RunGated("N7", () => ProductionDispatcher.Invoke("N7"));
        [CommandMethod("N8")]  public void Cmd_N8() => LicenseGate.RunGated("N8", () => ProductionDispatcher.Invoke("N8"));
        [CommandMethod("N9")]  public void Cmd_N9() => LicenseGate.RunGated("N9", () => ProductionDispatcher.Invoke("N9"));
        [CommandMethod("N10")] public void Cmd_N10() => LicenseGate.RunGated("N10", () => ProductionDispatcher.Invoke("N10"));
        [CommandMethod("N11")] public void Cmd_N11() => LicenseGate.RunGated("N11", () => ProductionDispatcher.Invoke("N11"));
        [CommandMethod("N12")] public void Cmd_N12() => LicenseGate.RunGated("N12", () => ProductionDispatcher.Invoke("N12"));
        [CommandMethod("N13")] public void Cmd_N13() => LicenseGate.RunGated("N13", () => ProductionDispatcher.Invoke("N13"));
        [CommandMethod("N14")] public void Cmd_N14() => LicenseGate.RunGated("N14", () => ProductionDispatcher.Invoke("N14"));
        [CommandMethod("N15")] public void Cmd_N15() => LicenseGate.RunGated("N15", () => ProductionDispatcher.Invoke("N15"));
        [CommandMethod("N16")] public void Cmd_N16() => LicenseGate.RunGated("N16", () => ProductionDispatcher.Invoke("N16"));
        [CommandMethod("N17")] public void Cmd_N17() => LicenseGate.RunGated("N17", () => ProductionDispatcher.Invoke("N17"));
        [CommandMethod("N18")] public void Cmd_N18() => LicenseGate.RunGated("N18", () => ProductionDispatcher.Invoke("N18"));
        [CommandMethod("N19")] public void Cmd_N19() => LicenseGate.RunGated("N19", () => ProductionDispatcher.Invoke("N19"));
        [CommandMethod("N20")] public void Cmd_N20() => LicenseGate.RunGated("N20", () => ProductionDispatcher.Invoke("N20"));
        [CommandMethod("N21")] public void Cmd_N21() => LicenseGate.RunGated("N21", () => ProductionDispatcher.Invoke("N21"));
        [CommandMethod("N22")] public void Cmd_N22() => LicenseGate.RunGated("N22", () => ProductionDispatcher.Invoke("N22"));
        [CommandMethod("N23")] public void Cmd_N23() => LicenseGate.RunGated("N23", () => ProductionDispatcher.Invoke("N23"));
        [CommandMethod("N24")] public void Cmd_N24() => LicenseGate.RunGated("N24", () => ProductionDispatcher.Invoke("N24"));
        [CommandMethod("N25")] public void Cmd_N25() => LicenseGate.RunGated("N25", () => ProductionDispatcher.Invoke("N25"));
        [CommandMethod("N26")] public void Cmd_N26() => LicenseGate.RunGated("N26", () => ProductionDispatcher.Invoke("N26"));
        [CommandMethod("N27")] public void Cmd_N27() => LicenseGate.RunGated("N27", () => ProductionDispatcher.Invoke("N27"));
        [CommandMethod("N28")] public void Cmd_N28() => LicenseGate.RunGated("N28", () => ProductionDispatcher.Invoke("N28"));
        [CommandMethod("N29")] public void Cmd_N29() => LicenseGate.RunGated("N29", () => ProductionDispatcher.Invoke("N29"));
        [CommandMethod("N30")] public void Cmd_N30() => LicenseGate.RunGated("N30", () => ProductionDispatcher.Invoke("N30"));
        [CommandMethod("N31")] public void Cmd_N31() => LicenseGate.RunGated("N31", () => ProductionDispatcher.Invoke("N31"));
        [CommandMethod("N32")] public void Cmd_N32() => LicenseGate.RunGated("N32", () => ProductionDispatcher.Invoke("N32"));
        [CommandMethod("N33")] public void Cmd_N33() => LicenseGate.RunGated("N33", () => ProductionDispatcher.Invoke("N33"));
        [CommandMethod("N34")] public void Cmd_N34() => LicenseGate.RunGated("N34", () => ProductionDispatcher.Invoke("N34"));
        [CommandMethod("N35")] public void Cmd_N35() => LicenseGate.RunGated("N35", () => ProductionDispatcher.Invoke("N35"));
        [CommandMethod("N36")] public void Cmd_N36() => LicenseGate.RunGated("N36", () => ProductionDispatcher.Invoke("N36"));
        [CommandMethod("N37")] public void Cmd_N37() => LicenseGate.RunGated("N37", () => ProductionDispatcher.Invoke("N37"));
        [CommandMethod("N38")] public void Cmd_N38() => LicenseGate.RunGated("N38", () => ProductionDispatcher.Invoke("N38"));
        [CommandMethod("N39")] public void Cmd_N39() => LicenseGate.RunGated("N39", () => ProductionDispatcher.Invoke("N39"));
        [CommandMethod("N40")] public void Cmd_N40() => LicenseGate.RunGated("N40", () => ProductionDispatcher.Invoke("N40"));
        [CommandMethod("N41")] public void Cmd_N41() => LicenseGate.RunGated("N41", () => ProductionDispatcher.Invoke("N41"));
        [CommandMethod("N42")] public void Cmd_N42() => LicenseGate.RunGated("N42", () => ProductionDispatcher.Invoke("N42"));
        [CommandMethod("N43")] public void Cmd_N43() => LicenseGate.RunGated("N43", () => ProductionDispatcher.Invoke("N43"));
        [CommandMethod("N44")] public void Cmd_N44() => LicenseGate.RunGated("N44", () => ProductionDispatcher.Invoke("N44"));
        [CommandMethod("N45")] public void Cmd_N45() => LicenseGate.RunGated("N45", () => ProductionDispatcher.Invoke("N45"));
        [CommandMethod("N46")] public void Cmd_N46() => LicenseGate.RunGated("N46", () => ProductionDispatcher.Invoke("N46"));
        [CommandMethod("N47")] public void Cmd_N47() => LicenseGate.RunGated("N47", () => ProductionDispatcher.Invoke("N47"));
        [CommandMethod("N48")] public void Cmd_N48() => LicenseGate.RunGated("N48", () => ProductionDispatcher.Invoke("N48"));
        [CommandMethod("N49")] public void Cmd_N49() => LicenseGate.RunGated("N49", () => ProductionDispatcher.Invoke("N49"));
        [CommandMethod("N50")] public void Cmd_N50() => LicenseGate.RunGated("N50", () => ProductionDispatcher.Invoke("N50"));
        #endregion
    }
}

#endif
