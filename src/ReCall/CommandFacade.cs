using Autodesk.AutoCAD.Runtime;

[assembly: CommandClass(typeof(HyCADTool.ReCall.CommandFacade))]

namespace HyCADTool.ReCall
{
    /// <summary>
    /// AutoCAD 命令总入口。
    ///
    /// 大多数 <c>[CommandMethod(key)]</c> 只做一件事：转发给 <see cref="ReCallClass.Invoke"/>，
    /// 由 <see cref="CommandTable"/>（commands.json）决定真实目标 Type.Method。
    /// 少数命令（如 <c>hySeg3</c>，命令行大小写不敏感故与 <c>HYSEG3</c> 等价）挂在 <see cref="ReCallClass"/> 上，与 <c>C2</c> 同 DLL，重装 ReCall 后即注册。
    ///
    /// 日常开发：
    /// - 改 Refactored 业务代码 → C2 热重载 → 立即生效（命令名不变）
    /// - 新增命令：Refactored 写 XxxCommand.Execute → 编辑 commands.json 把某 "N?" 槽位指向它 → C2 → 输 N?
    ///   （零关 CAD）
    ///
    /// 仅当要把 N? 升级为正式命令名（如 hyRoadXxx）时，才需要在本文件加新的 [CommandMethod]
    /// 并关 CAD 重 NETLOAD ReCall.dll —— 这是唯一需要重启 AutoCAD 的时刻。
    ///
    /// 道路类命令另注册一组 <c>r*</c> 短别名（与 <c>hyRoad*</c> 共用同一 <c>Invoke</c> 键），仅多一条属性即可。
    ///
    /// ReCall.dll 本身不热重载；本文件的 80+ 业务命令 + 50 占位符是稳定基座。
    /// </summary>
    public class CommandFacade
    {
        #region 面板 (Panel)
        [CommandMethod("hy")]      public void Cmd_hy()       => ReCallClass.Invoke("hy");
        [CommandMethod("HyB")]     public void Cmd_HyB()      => ReCallClass.Invoke("HyB");
        [CommandMethod("_HyExec")] public void Cmd__HyExec()  => ReCallClass.Invoke("_HyExec");
        [CommandMethod("hyLicense")] public void Cmd_hyLicense() => ReCallClass.Invoke("hyLicense");
        #endregion

        #region 钢筋绘制 (Reinforcement - Draw)
        [CommandMethod("gj")]  public void Cmd_gj()  => ReCallClass.Invoke("gj");
        [CommandMethod("gg")]  public void Cmd_gg()  => ReCallClass.Invoke("gg");
        [CommandMethod("ggj")] public void Cmd_ggj() => ReCallClass.Invoke("ggj");
        #endregion

        #region 钢筋修改 (Reinforcement - Modify)
        [CommandMethod("g1")]  public void Cmd_g1()  => ReCallClass.Invoke("g1");
        [CommandMethod("g2")]  public void Cmd_g2()  => ReCallClass.Invoke("g2");
        [CommandMethod("ge")]  public void Cmd_ge()  => ReCallClass.Invoke("ge");
        [CommandMethod("ge1")] public void Cmd_ge1() => ReCallClass.Invoke("ge1");
        [CommandMethod("gd")]  public void Cmd_gd()  => ReCallClass.Invoke("gd");
        #endregion

        #region 钢筋标注 (Reinforcement - Label)
        [CommandMethod("gb")]    public void Cmd_gb()    => ReCallClass.Invoke("gb");
        [CommandMethod("gb1")]   public void Cmd_gb1()   => ReCallClass.Invoke("gb1");
        [CommandMethod("gb2")]   public void Cmd_gb2()   => ReCallClass.Invoke("gb2");
        [CommandMethod("hysrt")] public void Cmd_hysrt() => ReCallClass.Invoke("hysrt");
        #endregion

        #region 标高 (Elevation)
        [CommandMethod("bg")]      public void Cmd_bg()      => ReCallClass.Invoke("bg");
        [CommandMethod("bgu")]     public void Cmd_bgu()     => ReCallClass.Invoke("bgu");
        [CommandMethod("bgR")]     public void Cmd_bgR()     => ReCallClass.Invoke("bgR");
        [CommandMethod("hybgTCE")] public void Cmd_hybgTCE() => ReCallClass.Invoke("hybgTCE");
        #endregion

        #region 尺寸标注 (Dimension)
        [CommandMethod("dds")]    public void Cmd_dds()    => ReCallClass.Invoke("dds");
        [CommandMethod("ddss")]   public void Cmd_ddss()   => ReCallClass.Invoke("ddss");
        [CommandMethod("sd")]     public void Cmd_sd()     => ReCallClass.Invoke("sd");
        [CommandMethod("ddaa")]   public void Cmd_ddaa()   => ReCallClass.Invoke("ddaa");
        [CommandMethod("hydimA")] public void Cmd_hydimA() => ReCallClass.Invoke("hydimA");
        #endregion

        #region 地脚螺栓 (Anchor Bolt)
        [CommandMethod("hyab")]   public void Cmd_hyab()   => ReCallClass.Invoke("hyab");
        [CommandMethod("hyabA")]  public void Cmd_hyabA()  => ReCallClass.Invoke("hyabA");
        [CommandMethod("hyabC")]  public void Cmd_hyabC()  => ReCallClass.Invoke("hyabC");
        [CommandMethod("hyabCD")] public void Cmd_hyabCD() => ReCallClass.Invoke("hyabCD");
        [CommandMethod("hyabCT")] public void Cmd_hyabCT() => ReCallClass.Invoke("hyabCT");
        [CommandMethod("hyabR")]  public void Cmd_hyabR()  => ReCallClass.Invoke("hyabR");
        #endregion

        #region 设备基础 (Equipment Foundation)
        [CommandMethod("hyef_Base_ConstructBaseData")] public void Cmd_hyef_Base_ConstructBaseData() => ReCallClass.Invoke("hyef_Base_ConstructBaseData");
        [CommandMethod("hyef_Base_HighlightBoltData")] public void Cmd_hyef_Base_HighlightBoltData() => ReCallClass.Invoke("hyef_Base_HighlightBoltData");
        [CommandMethod("hyef_Axis_Construct")]         public void Cmd_hyef_Axis_Construct()         => ReCallClass.Invoke("hyef_Axis_Construct");
        [CommandMethod("hyef_Axis_Initialize")]        public void Cmd_hyef_Axis_Initialize()        => ReCallClass.Invoke("hyef_Axis_Initialize");
        [CommandMethod("hyef_Axis_Display")]           public void Cmd_hyef_Axis_Display()           => ReCallClass.Invoke("hyef_Axis_Display");
        [CommandMethod("hyef_Axis_CreateTable")]       public void Cmd_hyef_Axis_CreateTable()       => ReCallClass.Invoke("hyef_Axis_CreateTable");
        #endregion

        #region 图框 / 布局 (Title Block / Layout)
        [CommandMethod("HYMBRD")]           public void Cmd_HYMBRD()           => ReCallClass.Invoke("HYMBRD");
        [CommandMethod("HYMBRC")]           public void Cmd_HYMBRC()           => ReCallClass.Invoke("HYMBRC");
        [CommandMethod("HY_PackViewports")] public void Cmd_HY_PackViewports() => ReCallClass.Invoke("HY_PackViewports");
        #endregion

        #region 图纸视口 (Paper Viewport)
        [CommandMethod("ph",  CommandFlags.Modal | CommandFlags.UsePickSet | CommandFlags.Redraw)] public void Cmd_ph()  => ReCallClass.Invoke("ph");
        [CommandMethod("pv",  CommandFlags.Modal | CommandFlags.UsePickSet | CommandFlags.Redraw)] public void Cmd_pv()  => ReCallClass.Invoke("pv");
        [CommandMethod("phh", CommandFlags.Modal | CommandFlags.UsePickSet | CommandFlags.Redraw)] public void Cmd_phh() => ReCallClass.Invoke("phh");
        [CommandMethod("pvv", CommandFlags.Modal | CommandFlags.UsePickSet | CommandFlags.Redraw)] public void Cmd_pvv() => ReCallClass.Invoke("pvv");
        #endregion

        #region 块操作 (Block)
        [CommandMethod("HYc2bc")] public void Cmd_HYc2bc() => ReCallClass.Invoke("HYc2bc");
        [CommandMethod("HYc2bl")] public void Cmd_HYc2bl() => ReCallClass.Invoke("HYc2bl");
        #endregion

        #region 多边形替换 (Polygon Replace)
        [CommandMethod("abrc")]  public void Cmd_abrc()  => ReCallClass.Invoke("abrc");
        [CommandMethod("abrcs")] public void Cmd_abrcs() => ReCallClass.Invoke("abrcs");
        #endregion

        #region 垫层 (Pad)
        [CommandMethod("HyDcL")] public void Cmd_HyDcL() => ReCallClass.Invoke("HyDcL");
        [CommandMethod("hyDcP")] public void Cmd_hyDcP() => ReCallClass.Invoke("hyDcP");
        #endregion

        #region 设计说明 (Design Spec / Markdown)
        [CommandMethod("hymd")]  public void Cmd_hymd()  => ReCallClass.Invoke("hymd");
        [CommandMethod("hymdE")] public void Cmd_hymdE() => ReCallClass.Invoke("hymdE");
        #endregion

        #region 导出 (Export)
        [CommandMethod("hyex")]     public void Cmd_hyex()     => ReCallClass.Invoke("hyex");
        [CommandMethod("hyex_csv")] public void Cmd_hyex_csv() => ReCallClass.Invoke("hyex_csv");
        [CommandMethod("hyLtCapture")] public void Cmd_hyLtCapture() => ReCallClass.Invoke("hyLtCapture");
        #endregion

        #region 其他工具 (Misc)
        [CommandMethod("hyAxis")] public void Cmd_hyAxis() => ReCallClass.Invoke("hyAxis");
        [CommandMethod("HyRT")]   public void Cmd_HyRT()   => ReCallClass.Invoke("HyRT");
        [CommandMethod("hydl")]   public void Cmd_hydl()   => ReCallClass.Invoke("hydl");
        [CommandMethod("HYBL")]   public void Cmd_HYBL()   => ReCallClass.Invoke("HYBL");
        #endregion

        #region 桩基 (Pile)
        [CommandMethod("HYpile")]   public void Cmd_HYpile()   => ReCallClass.Invoke("HYpile");
        [CommandMethod("HYpileV")]  public void Cmd_HYpileV()  => ReCallClass.Invoke("HYpileV");
        [CommandMethod("HYpileG")]  public void Cmd_HYpileG()  => ReCallClass.Invoke("HYpileG");
        [CommandMethod("HYpileGT")] public void Cmd_HYpileGT() => ReCallClass.Invoke("HYpileGT");
        #endregion

        #region 沉降 (Settlement)
        [CommandMethod("HYJC")] public void Cmd_HYJC() => ReCallClass.Invoke("HYJC");
        [CommandMethod("hySC")] public void Cmd_hySC() => ReCallClass.Invoke("hySC");
        #endregion

        #region hyob 几何对象库 (HyCAD Object Base) — 与 DWG 平行的版本化二进制存储
        // 设计：docs/DataExchange/02..04 三篇。落地：M0 stub → M1 全黑盒最小回路 → ... → M13。
        // 一次性把 12 条命令名注册到 ReCall（关 CAD 重 NETLOAD ReCall.dll 一次），
        // 之后 M1~M13 的实现全走 C2 热重载，零关 CAD。
        [CommandMethod("hyobI")]  public void Cmd_hyobI()  => ReCallClass.Invoke("hyobI");
        [CommandMethod("hyobS")]  public void Cmd_hyobS()  => ReCallClass.Invoke("hyobS");
        [CommandMethod("hyobC")]  public void Cmd_hyobC()  => ReCallClass.Invoke("hyobC");
        [CommandMethod("hyobL")]  public void Cmd_hyobL()  => ReCallClass.Invoke("hyobL");
        [CommandMethod("hyobCo")] public void Cmd_hyobCo() => ReCallClass.Invoke("hyobCo");
        [CommandMethod("hyobD")]  public void Cmd_hyobD()  => ReCallClass.Invoke("hyobD");
        [CommandMethod("hyobR")]  public void Cmd_hyobR()  => ReCallClass.Invoke("hyobR");
        [CommandMethod("hyobG")]  public void Cmd_hyobG()  => ReCallClass.Invoke("hyobG");
        [CommandMethod("hyobB")]  public void Cmd_hyobB()  => ReCallClass.Invoke("hyobB");
        [CommandMethod("hyobM")]  public void Cmd_hyobM()  => ReCallClass.Invoke("hyobM");
        [CommandMethod("hyobP")]  public void Cmd_hyobP()  => ReCallClass.Invoke("hyobP");
        [CommandMethod("hyobES")] public void Cmd_hyobES() => ReCallClass.Invoke("hyobES");
        #endregion

        #region 道路 (Road) — hyRoad* + r* 短别名
        // 045 / M6：项目树命令
        [CommandMethod("hyRoadTree")][CommandMethod("rTree")]       public void Cmd_hyRoadTree()         => ReCallClass.Invoke("hyRoadTree");
        [CommandMethod("hyRoad")][CommandMethod("rCx")]             public void Cmd_hyRoad()             => ReCallClass.Invoke("hyRoad");
        [CommandMethod("hyRoadA")][CommandMethod("rLa")]            public void Cmd_hyRoadA()            => ReCallClass.Invoke("hyRoadA");
        [CommandMethod("hyRoadAName")][CommandMethod("rAn")]         public void Cmd_hyRoadAName()         => ReCallClass.Invoke("hyRoadAName");
        [CommandMethod("hyRoadAlnAssign")][CommandMethod("rAsg")]   public void Cmd_hyRoadAlnAssign()   => ReCallClass.Invoke("hyRoadAlnAssign");
        [CommandMethod("hyRoadAlnPlan")][CommandMethod("rPlp")]     public void Cmd_hyRoadAlnPlan()     => ReCallClass.Invoke("hyRoadAlnPlan");
        [CommandMethod("hyRoadAutoIntersection")][CommandMethod("rIa")] public void Cmd_hyRoadAutoIntersection() => ReCallClass.Invoke("hyRoadAutoIntersection");
        [CommandMethod("hyRoadAw")][CommandMethod("rLaw")]          public void Cmd_hyRoadAw()           => ReCallClass.Invoke("hyRoadAw");
        [CommandMethod("hyRoadAlnByPi")][CommandMethod("rPi")]      public void Cmd_hyRoadAlnByPi()      => ReCallClass.Invoke("hyRoadAlnByPi");
        [CommandMethod("hyRoadAlnEditPi")][CommandMethod("rWk")]    public void Cmd_hyRoadAlnEditPi()    => ReCallClass.Invoke("hyRoadAlnEditPi");
        [CommandMethod("hyRoadAlnStation")][CommandMethod("rSt")]   public void Cmd_hyRoadAlnStation()   => ReCallClass.Invoke("hyRoadAlnStation");
        [CommandMethod("hyRoadAlnTable")][CommandMethod("rSe")]     public void Cmd_hyRoadAlnTable()     => ReCallClass.Invoke("hyRoadAlnTable");
        [CommandMethod("hyRoadAlnGeomPt")][CommandMethod("rGp")]    public void Cmd_hyRoadAlnGeomPt()    => ReCallClass.Invoke("hyRoadAlnGeomPt");
        [CommandMethod("hyRoadAlnExportPi")][CommandMethod("rEp")]  public void Cmd_hyRoadAlnExportPi()  => ReCallClass.Invoke("hyRoadAlnExportPi");
        [CommandMethod("hyRoadAlnExportFrame")][CommandMethod("rEf")] public void Cmd_hyRoadAlnExportFrame() => ReCallClass.Invoke("hyRoadAlnExportFrame");
        [CommandMethod("hyRoadAlnDefaults")][CommandMethod("rDd")]  public void Cmd_hyRoadAlnDefaults()  => ReCallClass.Invoke("hyRoadAlnDefaults");
        [CommandMethod("hyRoadAlnInsertPi")][CommandMethod("rIns")]  public void Cmd_hyRoadAlnInsertPi()  => ReCallClass.Invoke("hyRoadAlnInsertPi");
        [CommandMethod("hyRoadAlnDeletePi")][CommandMethod("rDp")]  public void Cmd_hyRoadAlnDeletePi()  => ReCallClass.Invoke("hyRoadAlnDeletePi");
        [CommandMethod("hyRoadAlnStaEq")][CommandMethod("rEq")]     public void Cmd_hyRoadAlnStaEq()     => ReCallClass.Invoke("hyRoadAlnStaEq");
        [CommandMethod("hyRoadAlnReverse")][CommandMethod("rRv")]   public void Cmd_hyRoadAlnReverse()   => ReCallClass.Invoke("hyRoadAlnReverse");
        [CommandMethod("hyRoadAlnOffset")][CommandMethod("rOf")]    public void Cmd_hyRoadAlnOffset()    => ReCallClass.Invoke("hyRoadAlnOffset");
        // 用户拾取 → 草稿登记 → 提交（v1.2 路线工作台全工作流）
        // hyRoadAlnUserPickDraw / hyRoadAlnUserPickDrawWB 已存在 commands.json，作为预览命令直接走面板按钮排队的 SendStringToExecute；
        // hyRoadAlnUserPickRegister + hyRoadAlnCommit 新增正式 [CommandMethod]，用户也可直接命令行调用。
        [CommandMethod("hyRoadAlnUserPickDraw")]                        public void Cmd_hyRoadAlnUserPickDraw()    => ReCallClass.Invoke("hyRoadAlnUserPickDraw");
        [CommandMethod("hyRoadAlnUserPickDrawWB")]                      public void Cmd_hyRoadAlnUserPickDrawWB()  => ReCallClass.Invoke("hyRoadAlnUserPickDrawWB");
        [CommandMethod("hyRoadAlnRawShow")]                             public void Cmd_hyRoadAlnRawShow()         => ReCallClass.Invoke("hyRoadAlnRawShow");
        [CommandMethod("hyRoadAlnRawHide")]                             public void Cmd_hyRoadAlnRawHide()         => ReCallClass.Invoke("hyRoadAlnRawHide");
        [CommandMethod("hyRoadAlnUserPickRegister")][CommandMethod("rLap")] public void Cmd_hyRoadAlnUserPickRegister() => ReCallClass.Invoke("hyRoadAlnUserPickRegister");
        [CommandMethod("hyRoadAlnCommit")][CommandMethod("rLac")]       public void Cmd_hyRoadAlnCommit()          => ReCallClass.Invoke("hyRoadAlnCommit");
        [CommandMethod("hyRoadAlnDeletePick")]                         public void Cmd_hyRoadAlnDeletePick()      => ReCallClass.Invoke("hyRoadAlnDeletePick");
        [CommandMethod("hyRoadAlnExportXml")][CommandMethod("rXo")] public void Cmd_hyRoadAlnExportXml() => ReCallClass.Invoke("hyRoadAlnExportXml");
        [CommandMethod("hyRoadAlnImportXml")][CommandMethod("rXi")] public void Cmd_hyRoadAlnImportXml() => ReCallClass.Invoke("hyRoadAlnImportXml");
        [CommandMethod("hyRoadIntersection")][CommandMethod("rIs")]            public void Cmd_hyRoadIntersection()            => ReCallClass.Invoke("hyRoadIntersection");
        [CommandMethod("hyRoadIntersectionEdit")][CommandMethod("rIe")]        public void Cmd_hyRoadIntersectionEdit()        => ReCallClass.Invoke("hyRoadIntersectionEdit");
        [CommandMethod("hyRoadIntersectionKerbChain")][CommandMethod("rIk")]   public void Cmd_hyRoadIntersectionKerbChain()   => ReCallClass.Invoke("hyRoadIntersectionKerbChain");
        [CommandMethod("hyRoadIntersectionCrosswalk")][CommandMethod("rIw")] public void Cmd_hyRoadIntersectionCrosswalk() => ReCallClass.Invoke("hyRoadIntersectionCrosswalk");
        [CommandMethod("hyRoadCurbRamp")][CommandMethod("rCr")]      public void Cmd_hyRoadCurbRamp()      => ReCallClass.Invoke("hyRoadCurbRamp");
        [CommandMethod("hyRoadTactilePaving")][CommandMethod("rTp")] public void Cmd_hyRoadTactilePaving() => ReCallClass.Invoke("hyRoadTactilePaving");
        [CommandMethod("hyRoadStopLine")][CommandMethod("rSl")]      public void Cmd_hyRoadStopLine()      => ReCallClass.Invoke("hyRoadStopLine");
        [CommandMethod("hyRoadLaneMarking")][CommandMethod("rLm")]   public void Cmd_hyRoadLaneMarking()   => ReCallClass.Invoke("hyRoadLaneMarking");
        [CommandMethod("hyRoadArrow")][CommandMethod("rAr")]         public void Cmd_hyRoadArrow()         => ReCallClass.Invoke("hyRoadArrow");
        [CommandMethod("hyRoadP")][CommandMethod("rPr")]            public void Cmd_hyRoadP()            => ReCallClass.Invoke("hyRoadP");
        // 鸿业风格的纵断面 3 件套：FG 设计线（与 hyRoadP 等价）/ EG 地面线（v1.1）/ Label 标注到 DWG（v1.1）
        [CommandMethod("hyRoadProfFG")][CommandMethod("rFg")]       public void Cmd_hyRoadProfFG()       => ReCallClass.Invoke("hyRoadProfFG");
        [CommandMethod("hyRoadProfEG")][CommandMethod("rEg")]       public void Cmd_hyRoadProfEG()       => ReCallClass.Invoke("hyRoadProfEG");
        [CommandMethod("hyRoadProfLabel")][CommandMethod("rPl")]    public void Cmd_hyRoadProfLabel()    => ReCallClass.Invoke("hyRoadProfLabel");
        // hyRoadT（v1，已转发到 v2 hyRoadCs）+ hyRoadCs（直接开 WPF）+ hyRoadCsLoad / hyRoadCsQuick（原 L/C 分支）
        // 关 CAD → NETLOAD ReCall.dll 后均可用；commands.json 对应 RoadCrossSectionDrawCommand 各方法。
        [CommandMethod("hyRoadT")][CommandMethod("rT1")]            public void Cmd_hyRoadT()            => ReCallClass.Invoke("hyRoadT");
        [CommandMethod("hyRoadCs")][CommandMethod("rCs")]           public void Cmd_hyRoadCs()           => ReCallClass.Invoke("hyRoadCs");
        [CommandMethod("hyRoadCsLoad")][CommandMethod("rCsL")]      public void Cmd_hyRoadCsLoad()       => ReCallClass.Invoke("hyRoadCsLoad");
        [CommandMethod("hyRoadCsQuick")][CommandMethod("rCsQ")]     public void Cmd_hyRoadCsQuick()      => ReCallClass.Invoke("hyRoadCsQuick");
        [CommandMethod("hyRoadC")][CommandMethod("rCo")]            public void Cmd_hyRoadC()            => ReCallClass.Invoke("hyRoadC");
        [CommandMethod("hyRoadSave")][CommandMethod("rSv")]         public void Cmd_hyRoadSave()         => ReCallClass.Invoke("hyRoadSave");
        [CommandMethod("hyRoadLoad")][CommandMethod("rLd")]         public void Cmd_hyRoadLoad()         => ReCallClass.Invoke("hyRoadLoad");
        [CommandMethod("hyRoad3dExportGltf")][CommandMethod("rGf")] public void Cmd_hyRoad3dExportGltf() => ReCallClass.Invoke("hyRoad3dExportGltf");
        [CommandMethod("hyRoadSeg3")][CommandMethod("r3s")]        public void Cmd_hyRoadSeg3()        => ReCallClass.Invoke("hyRoadSeg3");
        #endregion

        #region 几何工具 (Geometry Tools)
        [CommandMethod("HYDCEL")]    public void Cmd_HYDCEL()    => ReCallClass.Invoke("HYDCEL");
        [CommandMethod("HYDCELSET")] public void Cmd_HYDCELSET() => ReCallClass.Invoke("HYDCELSET");
        [CommandMethod("HYMBR")]     public void Cmd_HYMBR()     => ReCallClass.Invoke("HYMBR");
        [CommandMethod("HYJP")]      public void Cmd_HYJP()      => ReCallClass.Invoke("HYJP");
        [CommandMethod("HYOV")]      public void Cmd_HYOV()      => ReCallClass.Invoke("HYOV");
        [CommandMethod("HYOVSET")]   public void Cmd_HYOVSET()   => ReCallClass.Invoke("HYOVSET");
        [CommandMethod("HYBC")]      public void Cmd_HYBC()      => ReCallClass.Invoke("HYBC");
        // hySeg3：注册在 ReCallClass（Recall.cs），与 C2 同程序集；勿再写 HYSEG3 属性（与 hySeg3 全局名冲突 → eDuplicateKey）。
        #endregion

        #region 三维建模 (3D Modeling)
        [CommandMethod("HY3")] public void Cmd_HY3() => ReCallClass.Invoke("HY3");
        #endregion

        #region 诊断 (Diagnostic)
        [CommandMethod("HYLOCATETIF")] public void Cmd_HYLOCATETIF() => ReCallClass.Invoke("HYLOCATETIF");
        [CommandMethod("CHECKWPF")]    public void Cmd_CHECKWPF()    => ReCallClass.Invoke("CHECKWPF");
        [CommandMethod("hyCmdList")]   public void Cmd_hyCmdList()   => ReCallClass.Invoke("hyCmdList");
        #endregion

        #region 占位符 (Placeholders N1~N50)
        // 使用：在 commands.json 把某个 "N?" 从 null 改为 { "type": "...", "method": "..." }，
        // 然后 AutoCAD 里 C2 一下、输入 N? 即可执行。零关 CAD。
        [CommandMethod("N1")]  public void Cmd_N1()  => ReCallClass.Invoke("N1");
        [CommandMethod("N2")]  public void Cmd_N2()  => ReCallClass.Invoke("N2");
        [CommandMethod("N3")]  public void Cmd_N3()  => ReCallClass.Invoke("N3");
        [CommandMethod("N4")]  public void Cmd_N4()  => ReCallClass.Invoke("N4");
        [CommandMethod("N5")]  public void Cmd_N5()  => ReCallClass.Invoke("N5");
        [CommandMethod("N6")]  public void Cmd_N6()  => ReCallClass.Invoke("N6");
        [CommandMethod("N7")]  public void Cmd_N7()  => ReCallClass.Invoke("N7");
        [CommandMethod("N8")]  public void Cmd_N8()  => ReCallClass.Invoke("N8");
        [CommandMethod("N9")]  public void Cmd_N9()  => ReCallClass.Invoke("N9");
        [CommandMethod("N10")] public void Cmd_N10() => ReCallClass.Invoke("N10");
        [CommandMethod("N11")] public void Cmd_N11() => ReCallClass.Invoke("N11");
        [CommandMethod("N12")] public void Cmd_N12() => ReCallClass.Invoke("N12");
        [CommandMethod("N13")] public void Cmd_N13() => ReCallClass.Invoke("N13");
        [CommandMethod("N14")] public void Cmd_N14() => ReCallClass.Invoke("N14");
        [CommandMethod("N15")] public void Cmd_N15() => ReCallClass.Invoke("N15");
        [CommandMethod("N16")] public void Cmd_N16() => ReCallClass.Invoke("N16");
        [CommandMethod("N17")] public void Cmd_N17() => ReCallClass.Invoke("N17");
        [CommandMethod("N18")] public void Cmd_N18() => ReCallClass.Invoke("N18");
        [CommandMethod("N19")] public void Cmd_N19() => ReCallClass.Invoke("N19");
        [CommandMethod("N20")] public void Cmd_N20() => ReCallClass.Invoke("N20");
        [CommandMethod("N21")] public void Cmd_N21() => ReCallClass.Invoke("N21");
        [CommandMethod("N22")] public void Cmd_N22() => ReCallClass.Invoke("N22");
        [CommandMethod("N23")] public void Cmd_N23() => ReCallClass.Invoke("N23");
        [CommandMethod("N24")] public void Cmd_N24() => ReCallClass.Invoke("N24");
        [CommandMethod("N25")] public void Cmd_N25() => ReCallClass.Invoke("N25");
        [CommandMethod("N26")] public void Cmd_N26() => ReCallClass.Invoke("N26");
        [CommandMethod("N27")] public void Cmd_N27() => ReCallClass.Invoke("N27");
        [CommandMethod("N28")] public void Cmd_N28() => ReCallClass.Invoke("N28");
        [CommandMethod("N29")] public void Cmd_N29() => ReCallClass.Invoke("N29");
        [CommandMethod("N30")] public void Cmd_N30() => ReCallClass.Invoke("N30");
        [CommandMethod("N31")] public void Cmd_N31() => ReCallClass.Invoke("N31");
        [CommandMethod("N32")] public void Cmd_N32() => ReCallClass.Invoke("N32");
        [CommandMethod("N33")] public void Cmd_N33() => ReCallClass.Invoke("N33");
        [CommandMethod("N34")] public void Cmd_N34() => ReCallClass.Invoke("N34");
        [CommandMethod("N35")] public void Cmd_N35() => ReCallClass.Invoke("N35");
        [CommandMethod("N36")] public void Cmd_N36() => ReCallClass.Invoke("N36");
        [CommandMethod("N37")] public void Cmd_N37() => ReCallClass.Invoke("N37");
        [CommandMethod("N38")] public void Cmd_N38() => ReCallClass.Invoke("N38");
        [CommandMethod("N39")] public void Cmd_N39() => ReCallClass.Invoke("N39");
        [CommandMethod("N40")] public void Cmd_N40() => ReCallClass.Invoke("N40");
        [CommandMethod("N41")] public void Cmd_N41() => ReCallClass.Invoke("N41");
        [CommandMethod("N42")] public void Cmd_N42() => ReCallClass.Invoke("N42");
        [CommandMethod("N43")] public void Cmd_N43() => ReCallClass.Invoke("N43");
        [CommandMethod("N44")] public void Cmd_N44() => ReCallClass.Invoke("N44");
        [CommandMethod("N45")] public void Cmd_N45() => ReCallClass.Invoke("N45");
        [CommandMethod("N46")] public void Cmd_N46() => ReCallClass.Invoke("N46");
        [CommandMethod("N47")] public void Cmd_N47() => ReCallClass.Invoke("N47");
        [CommandMethod("N48")] public void Cmd_N48() => ReCallClass.Invoke("N48");
        [CommandMethod("N49")] public void Cmd_N49() => ReCallClass.Invoke("N49");
        [CommandMethod("N50")] public void Cmd_N50() => ReCallClass.Invoke("N50");
        #endregion
    }
}
