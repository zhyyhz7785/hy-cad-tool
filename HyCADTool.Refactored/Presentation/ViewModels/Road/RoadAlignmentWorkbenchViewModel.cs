using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Autodesk.AutoCAD.ApplicationServices;
using HyCADTool.Refactored.Domain.Events.Road;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Domain.ValueObjects.Road;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata;
using HyCADTool.Refactored.Infrastructure.Configuration;
using HyCADTool.Refactored.Presentation.Commands;
using HyCADTool.Refactored.Presentation.Commands.Road;
using HyCADTool.Refactored.Presentation.ViewModels;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.ViewModels.Road
{
    /// <summary>
    /// 「路线工作台」面板的根 ViewModel。聚合当前 DWG 的 Alignment 列表 + 选中线位的 PI 列表 +
    /// 复用 <see cref="PiThreeUnitViewModel"/> 做 PI 参数编辑 + 只读分段/方程/几何点表。
    ///
    /// <para><b>工作流（简化版 v2 · 2026-04-21）</b>：拾取 → PI 调整（仅瞬态黄）→ 应用定稿。</para>
    ///
    /// <para><b>两层预览 + 一层存档</b>（DesignPreview 自动彩色机制已下线）：</para>
    /// <list type="number">
    ///   <item>
    ///     <b>瞬态黄</b>（<see cref="_preview"/>.Update）— PI 参数每次变化同步刷新 TransientManager；
    ///     不进 DWG 实体，切 Alignment / Apply 即清。
    ///   </item>
    ///   <item>
    ///     <b>用户快照</b>（<see cref="RoadAlignmentLivePreviewService"/>，KIND=AlignmentLivePreview，
    ///     绘在 <c>05_hy_道路_预览</c>）— 用户点<b>预览</b>按钮时手动追加一批分段彩色 Polyline；
    ///     面板关闭 / 切 Alignment / Apply 都<b>不会</b>自动清，由用户 <c>ERASE</c> / <c>LAYOFF</c> 自管。
    ///   </item>
    /// </list>
    ///
    /// <para><b>存档</b>：<c>05_hy_道路_平面线位</c>（KIND=Alignment）是唯一进 <c>.roaddesign.json</c> 的真实体，
    /// 只由「应用」按钮（<see cref="ApplyAlignmentCmd"/> → <see cref="RoadAlignmentApplyService"/>）产出 / 更新。</para>
    ///
    /// <para><c>05_hy_道路_原线</c>（KIND=AlignmentRawPick）启动即锁定，只容纳 252 本色拾取档案；
    /// 工作台本身<b>不</b>再向该层写入设计态彩色，Apply 定稿时会临时解锁清掉本 Id 的所有 HY_ROAD 实体。</para>
    ///
    /// <para>生命周期：<see cref="HideAllWorkbenchArtifacts"/>（PaletteSet Visible=false）擦瞬态；
    /// <b>不</b>碰 RawPick 永久档案、<b>不</b>碰用户快照、<b>不</b>碰正式线位。</para>
    /// </summary>
    public sealed class RoadAlignmentWorkbenchViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly RoadAlignmentPreviewService _preview;
        private readonly IDisposable _alignmentChangedSub;
        private bool _disposed;
        private bool _suspendPiEditorEvents;

        public RoadAlignmentWorkbenchViewModel()
        {
            _preview = new RoadAlignmentPreviewService();

            PickAlignmentCmd = new RelayCommand(PickAlignmentOnCanvas);
            DrawLivePreviewCmd = new RelayCommand(DrawLivePreviewSnapshot, () => SelectedAlignment != null);
            ReverseCmd = new RelayCommand(ReverseSelected, () => SelectedAlignment != null);
            OffsetCmd = new RelayCommand(RunOffset, () => SelectedAlignment != null);
            ExportPiCsvCmd = new RelayCommand(() => ExportCsv(PiCsvKind.PiTable), () => SelectedAlignment != null);
            ExportFrameCsvCmd = new RelayCommand(() => ExportCsv(PiCsvKind.Frame), () => SelectedAlignment != null);

            InsertPiCmd = new RelayCommand(InsertPi, () => SelectedAlignment != null);
            DeletePiCmd = new RelayCommand(DeletePi, CanDeleteSelectedPi);
            // 「应用」不再依赖 SelectedPi — 只要当前 Alignment 有可构几何的 PI 表（≥ 2）就允许一键定稿。
            ApplyAlignmentCmd = new RelayCommand(ApplyAlignment, CanApplyAlignment);
            RevertPiEditCmd = new RelayCommand(RevertPiEdit, () => PiEditorVm != null && SelectedPi != null && !SelectedPi.IsEndpoint);

            DrawStationLabelsCmd = new RelayCommand<object>(p => DrawStationLabels(p is bool b && b));
            DrawGeometryPointsCmd = new RelayCommand(DrawGeometryPoints);

            AddStationEquationCmd = new RelayCommand(AddStationEquation, () => SelectedAlignment != null);
            DeleteStationEquationCmd = new RelayCommand(DeleteSelectedStationEquation,
                () => SelectedAlignment != null && SelectedStationEquation != null);

            try
            {
                var bus = ServiceLocator.Resolve<IRoadEventBus>();
                _alignmentChangedSub = bus.Subscribe<AlignmentChangedEvent>(OnAlignmentChangedFromBus);
            }
            catch { /* 测试环境无 bus 时容错 */ }

            RefreshAlignments();
        }

        // =============================== Alignment 列表 ===============================

        public ObservableCollection<AlignmentListItemVm> Alignments { get; } = new ObservableCollection<AlignmentListItemVm>();

        private AlignmentListItemVm _selectedAlignment;
        public AlignmentListItemVm SelectedAlignment
        {
            get => _selectedAlignment;
            set
            {
                if (!SetProperty(ref _selectedAlignment, value)) return;
                OnSelectedAlignmentChanged();
                RefreshCommandStates();
            }
        }

        // =============================== PI 列表 ===============================

        public ObservableCollection<PiListItemVm> PiItems { get; } = new ObservableCollection<PiListItemVm>();

        private PiListItemVm _selectedPi;
        public PiListItemVm SelectedPi
        {
            get => _selectedPi;
            set
            {
                if (ReferenceEquals(_selectedPi, value)) return;

                var previous = _selectedPi;
                if (ShouldFlushPiEditorBeforeSelectChange(previous))
                    FlushPiEditorWorkingStateToPiItems();

                _selectedPi = value;
                OnPropertyChanged(nameof(SelectedPi));
                OnSelectedPiChanged();
                RefreshCommandStates();
            }
        }

        // =============================== PI 编辑子 VM ===============================

        private PiThreeUnitViewModel _piEditorVm;
        public PiThreeUnitViewModel PiEditorVm
        {
            get => _piEditorVm;
            private set
            {
                if (!SetProperty(ref _piEditorVm, value)) return;
                RefreshCommandStates();
            }
        }

        // =============================== 只读表 ===============================

        public ObservableCollection<SegmentRecordRowVm> Segments { get; } = new ObservableCollection<SegmentRecordRowVm>();
        public ObservableCollection<StationEquationRowVm> StationEquations { get; } = new ObservableCollection<StationEquationRowVm>();
        public ObservableCollection<GeometryPointRowVm> GeometryPoints { get; } = new ObservableCollection<GeometryPointRowVm>();

        private StationEquationRowVm _selectedStationEquation;
        public StationEquationRowVm SelectedStationEquation
        {
            get => _selectedStationEquation;
            set
            {
                if (!SetProperty(ref _selectedStationEquation, value)) return;
                RefreshCommandStates();
            }
        }

        // =============================== 状态文本 ===============================

        private string _statusText = "就绪。";
        public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }

        private string _headerInfo = "-";
        public string HeaderInfo { get => _headerInfo; private set => SetProperty(ref _headerInfo, value); }

        /// <summary>最近一次「拾取」在 DWG 中选中的 Polyline 句柄（十六进制字符串）；便于与 AutoCAD 交互对照。</summary>
        public string LastPickedPolylineHandle { get; private set; }

        // =============================== Commands ===============================

        /// <summary>顶栏「拾取」— 派发 <c>hyRoadAlnUserPickRegister</c>，内部做 HY_ROAD 分流 + 原线层转存。</summary>
        public ICommand PickAlignmentCmd { get; }
        /// <summary>顶栏「预览」— 把当前 alignment 追加到 <c>05_hy_道路_预览</c>（用户快照，不自动清）。</summary>
        public ICommand DrawLivePreviewCmd { get; }
        /// <summary>顶栏「应用 / 一键定稿」— 落到 <c>05_hy_道路_平面线位</c> + <c>.roaddesign.json</c>，是唯一的真实保存动作。</summary>
        public ICommand ApplyAlignmentCmd { get; }
        public ICommand ReverseCmd { get; }
        public ICommand OffsetCmd { get; }
        public ICommand ExportPiCsvCmd { get; }
        public ICommand ExportFrameCsvCmd { get; }
        public ICommand InsertPiCmd { get; }
        public ICommand DeletePiCmd { get; }
        public ICommand RevertPiEditCmd { get; }
        public ICommand DrawStationLabelsCmd { get; }
        public ICommand DrawGeometryPointsCmd { get; }
        public ICommand AddStationEquationCmd { get; }
        public ICommand DeleteStationEquationCmd { get; }

        // =============================== 外部 API（由 PanelManager 调用）===============================

        /// <summary>
        /// 扫当前活动文档的 RoadDesign，把 Alignment 列表刷到 UI。PanelManager.DocumentActivated 钩子调用。
        /// 保留当前选中的 Alignment（如果仍存在）。
        /// </summary>
        public void RefreshAlignments()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            Guid keepId = _selectedAlignment?.Id ?? Guid.Empty;

            Alignments.Clear();
            if (doc == null)
            {
                SelectedAlignment = null;
                HeaderInfo = "（无活动图纸）";
                StatusText = "无活动图纸";
                ClearDetails();
                return;
            }

            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
            var svc = ServiceLocator.Resolve<RoadAlignmentService>();
            try
            {
                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    svc.RebindForDocument(doc.Name, tr, doc.Database);
                    tr.Commit();
                }
            }
            catch
            {
                // 文档被关闭等场景下 RebindForDocument 可能抛，忽略以保持 UI 不崩
            }

            // v1.2 自动加载兜底：若 Registry 内存里还没 design（首次打开工作台 / 冷启动 AutoCAD
            // 尚未跑任何 hy 命令的场景），且磁盘上存在同名 .roaddesign.json，就把它加载进内存。
            // UserPicked 草稿与 PiTable 线位在 JSON 里都有完整 Centerline/PI 表，VM 编辑不依赖 DWG 里的 Polyline，
            // 所以这里只把 JSON 灌进 Registry，不做 RedrawCenterlines（反向绘制走 hyRoadLoad，用户主动）。
            if (!registry.TryGet(doc.Name, out var design) || design == null || design.Alignments.Count == 0)
            {
                try
                {
                    var exporter = ServiceLocator.Resolve<RoadJsonExportService>();
                    var jsonPath = RoadJsonExportService.GetDefaultJsonPath(doc.Name);
                    if (!string.IsNullOrWhiteSpace(jsonPath))
                    {
                        var loaded = exporter.Load(jsonPath);
                        if (loaded != null && loaded.Alignments.Count > 0)
                        {
                            registry.Replace(doc.Name, loaded);
                            design = loaded;
                        }
                    }
                }
                catch
                {
                    // JSON 损坏 / IO 异常：不阻断 UI，继续走"未登记"分支
                }
            }

            if (design == null || design.Alignments.Count == 0)
            {
                SelectedAlignment = null;
                HeaderInfo = $"（{Path.GetFileName(doc.Name) ?? "-"}：未登记平面线位）";
                StatusText = "当前图纸未登记任何 Alignment";
                ClearDetails();
                return;
            }

            AlignmentListItemVm restore = null;
            foreach (var a in design.Alignments)
            {
                var row = new AlignmentListItemVm(a);
                Alignments.Add(row);
                if (keepId != Guid.Empty && a.Id == keepId) restore = row;
            }

            HeaderInfo = $"{Path.GetFileName(doc.Name) ?? "-"}  ·  共 {Alignments.Count} 条";
            if (restore != null)
            {
                _selectedAlignment = null; // 强制触发 setter 刷新
                SelectedAlignment = restore;
            }
            else
            {
                SelectedAlignment = Alignments.FirstOrDefault();
            }
        }

        /// <summary>
        /// PaletteSet 关闭（<c>Visible</c> 由 true 变 false）时由 <c>PanelManager</c> 调用：
        /// 把工作台<b>过程态</b>的所有临时图形一次性清零，让"关闭面板 = 屏幕干净"成为可预期交互。
        ///
        /// <para>清理范围（幂等、异常安全）：</para>
        /// <list type="bullet">
        ///   <item><b>瞬态黄线</b>（PI 实时预览 Transient）</item>
        ///   <item><b>设计态彩色</b>（<c>05_hy_道路_原线</c> 上 KIND=AlignmentDesignPreview 的实体）</item>
        ///   <item>debounce timer 停转 —— 关面板期间不应有延迟重绘残留</item>
        /// </list>
        ///
        /// <para><b>不清理</b>的三类（重要）：</para>
        /// <list type="bullet">
        ///   <item><b>原线 RawPick</b>（<c>05_hy_道路_原线</c>，KIND=AlignmentRawPick）— 永久档案；</item>
        ///   <item><b>用户快照</b>（<c>05_hy_道路_预览</c>，KIND=AlignmentLivePreview）— 用户自管；</item>
        ///   <item><b>正式线位</b>（<c>05_hy_道路_平面线位</c>，KIND=Alignment）— 唯一真实保存实体。</item>
        /// </list>
        /// </summary>
        public void HideAllWorkbenchArtifacts()
        {
            if (_disposed) return;

            try { _preview.Clear(); } catch { /* ignore */ }
        }

        /// <summary>
        /// PaletteSet 再次显示时调用：v2 工作流不再有自动彩色预览，保留桩方法避免宿主旧调用点链式崩。
        /// 瞬态黄由滑块事件重新驱动；用户快照 / RawPick / 正式线位都不在本方法负责范围。
        /// </summary>
        public void RestoreWorkbenchArtifacts()
        {
            if (_disposed) return;
            // no-op（v2：DesignPreview 已下线）
        }

        /// <summary>
        /// 由命令层（hyRoadAlnEditPi / hyRoadA / hyRoadAlnByPi 收尾）调用：
        /// 预选指定 Alignment 并可选预选内部 PI。会触发 <see cref="RefreshAlignments"/>。
        /// </summary>
        public void SelectAlignment(Guid alignmentId, int? piIndex = null)
        {
            RefreshAlignments();
            var target = Alignments.FirstOrDefault(a => a.Id == alignmentId);
            if (target == null) return;
            SelectedAlignment = target;
            if (piIndex.HasValue)
            {
                int idx = piIndex.Value;
                if (idx >= 0 && idx < PiItems.Count)
                {
                    SelectedPi = PiItems[idx];
                }
            }
        }

        // =============================== 内部：Alignment / PI 切换 ===============================

        private void OnSelectedAlignmentChanged()
        {
            PiItems.Clear();
            Segments.Clear();
            StationEquations.Clear();
            GeometryPoints.Clear();
            SelectedStationEquation = null;

            try { _preview.Clear(); } catch { /* ignore */ }

            if (_selectedAlignment == null)
            {
                SelectedPi = null;
                PiEditorVm = null;
                StatusText = "未选中 Alignment。";
                return;
            }

            var aln = _selectedAlignment.Alignment;
            var pis = aln.Source?.PiElements;
            if (pis == null || pis.Count < 2)
            {
                SelectedPi = null;
                PiEditorVm = null;
                StatusText = $"{aln.Name}：非 PI 法创建，PI 表不可用。请先跑 hyRoadAlnByPi。";
                BuildReadModels(aln, piDesignerFallback: true);
                return;
            }

            for (int i = 0; i < pis.Count; i++)
            {
                bool isEndpoint = (i == 0 || i == pis.Count - 1);
                var el = new PiElement(pis[i].P, pis[i].Radius, pis[i].SpiralIn, pis[i].SpiralOut, pis[i].Tag);
                PiItems.Add(new PiListItemVm(i, el, isEndpoint));
            }

            BuildReadModels(aln, piDesignerFallback: false);

            var firstInternal = PiItems.FirstOrDefault(p => !p.IsEndpoint);
            SelectedPi = firstInternal;
            StatusText = $"已切换到 {aln.Name}（PI {pis.Count}，长 {aln.Centerline?.GetPlanarLength() ?? 0:F2} m）。";
        }

        /// <summary>
        /// 切换列表选中 PI 前，把三单元编辑器里当前 PI 的未提交修改写回 <see cref="PiItems"/>。
        /// 否则 <see cref="OnSelectedPiChanged"/> 用旧快照 Rebind，上一 PI 的调整会丢。
        /// </summary>
        private bool ShouldFlushPiEditorBeforeSelectChange(PiListItemVm previous)
        {
            if (previous == null || previous.IsEndpoint || PiEditorVm == null) return false;
            if (PiItems.Count < 3) return false;
            int i = previous.Index;
            if (i <= 0 || i >= PiItems.Count - 1) return false;
            return ReferenceEquals(PiItems[i], previous);
        }

        private void FlushPiEditorWorkingStateToPiItems()
        {
            if (PiEditorVm == null || PiItems.Count == 0) return;
            var working = PiEditorVm.GetWorkingElements();
            if (working.Count != PiItems.Count) return;
            for (int i = 0; i < working.Count; i++)
                PiItems[i].Update(working[i]);
        }

        private void OnSelectedPiChanged()
        {
            if (_selectedAlignment == null || _selectedPi == null || _selectedPi.IsEndpoint)
            {
                PiEditorVm = null;
                return;
            }

            var elements = PiItems.Select(p => p.Element).ToList();
            if (elements.Count < 3) { PiEditorVm = null; return; }

            int idx = _selectedPi.Index;
            if (idx <= 0 || idx >= elements.Count - 1) { PiEditorVm = null; return; }

            if (PiEditorVm == null)
            {
                var vm = new PiThreeUnitViewModel(elements, idx);
                vm.NonCompliantConfirm = _ => true; // 面板内不弹窗，由用户自担
                vm.PreviewRequested += OnPiEditorPreviewRequested;
                PiEditorVm = vm;
            }
            else
            {
                _suspendPiEditorEvents = true;
                try { PiEditorVm.Rebind(elements, idx); }
                finally { _suspendPiEditorEvents = false; }
            }
        }

        /// <summary>
        /// PI 参数每次变化（滑块 / 输入框）— 同步刷新 Transient 黄线（即时反馈）。
        /// v2 工作流：不再自动刷新 <c>05_hy_道路_原线</c> 的彩色 DesignPreview；
        /// 用户需要在 DWG 查看分段彩色请点顶栏「预览」追加到 <c>05_hy_道路_预览</c>。
        /// </summary>
        private void OnPiEditorPreviewRequested(object sender, PiDesignResult result)
        {
            if (_suspendPiEditorEvents) return;
            if (result.Polyline == null) return;
            try { _preview.Update(result.Polyline); } catch { /* 文档关闭等场景忽略 */ }
        }

        private void ClearDetails()
        {
            PiItems.Clear();
            Segments.Clear();
            StationEquations.Clear();
            GeometryPoints.Clear();
            SelectedStationEquation = null;
            SelectedPi = null;
            PiEditorVm = null;
            try { _preview.Clear(); } catch { /* ignore */ }
        }

        // =============================== 右列：分段 / 方程 / 几何点 ===============================

        private void BuildReadModels(Alignment aln, bool piDesignerFallback)
        {
            Segments.Clear();
            StationEquations.Clear();
            GeometryPoints.Clear();

            if (aln == null) return;

            if (aln.StationEquations != null)
            {
                for (int i = 0; i < aln.StationEquations.Count; i++)
                {
                    StationEquations.Add(new StationEquationRowVm(i, aln.StationEquations[i], aln.StartStation));
                }
            }

            if (piDesignerFallback) return;

            try
            {
                var elements = aln.Source.PiElements
                    .Select(e => new PiElement(e.P, e.Radius, e.SpiralIn, e.SpiralOut, e.Tag))
                    .ToList();
                var breakdown = AlignmentStationBreakdown.Build(
                    elements,
                    aln.StartStation,
                    new PiDesignOptions(),
                    aln.StationEquations);

                foreach (var s in breakdown.Segments) Segments.Add(new SegmentRecordRowVm(s));
                foreach (var g in breakdown.GeometryPoints) GeometryPoints.Add(new GeometryPointRowVm(g));
            }
            catch (Exception ex)
            {
                StatusText = $"分段分解失败：{ex.Message}";
            }
        }

        // =============================== 顶栏操作 ===============================

        private void PickAlignmentOnCanvas()
        {
            if (AcApp.DocumentManager.MdiActiveDocument == null) return;
            // 非模态 WPF 线程禁止直接 LockDocument / 走 PromptEntity → 排队到命令线程的 hyRoadAlnUserPickRegister。
            // 命令收尾会发布 AlignmentChangedEvent（Added / Updated），由 OnAlignmentChangedFromBus 自动刷新 + 选中。
            CommandDispatcher.Send("hyRoadAlnUserPickRegister");
            StatusText = "请在命令行拾取一条 Polyline（任意图层）…";
        }

        /// <summary>
        /// 「预览」按钮 — 把当前选中 alignment 追加一批分段彩色 Polyline 到 <c>05_hy_道路_预览</c> 图层。
        /// <para>与 DesignPreview 的差别：</para>
        /// <list type="bullet">
        ///   <item>DesignPreview（自动）→ <c>05_hy_道路_原线</c>，每次 PI 调整 debounce 都重刷；</item>
        ///   <item>LivePreview（手动）→ <c>05_hy_道路_预览</c>，追加模式，用户自管；Apply 不会动。</item>
        /// </list>
        /// </summary>
        private void DrawLivePreviewSnapshot()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null || _selectedAlignment == null) return;
            int n = RoadAlignmentLivePreviewService.DrawForAlignment(doc, _selectedAlignment.Alignment);
            StatusText = n > 0
                ? $"已在 05_hy_道路_预览 追加 {n} 条分段彩色 Polyline（用户快照，需手动 ERASE 清理）。"
                : "预览追加失败：Alignment 中心线可能无效。";
        }

        private void OnAlignmentChangedFromBus(AlignmentChangedEvent evt)
        {
            // 弱引用回调可能在任意线程，UI 操作必须 marshal 回 Dispatcher。
            try
            {
                var disp = System.Windows.Application.Current?.Dispatcher;
                if (disp != null && !disp.CheckAccess())
                {
                    disp.BeginInvoke(new Action(() => OnAlignmentChangedFromBus(evt)));
                    return;
                }
            }
            catch { /* 设计期 / 单元测试无 Dispatcher */ }

            if (_disposed) return;
            // 收到任意变更先刷新；若是新增 / 提交后改了 Source.Kind，外加预选目标 Alignment。
            try { RefreshAlignments(); } catch { /* ignore */ }
            try
            {
                var target = Alignments.FirstOrDefault(a => a.Id == evt.AlignmentId);
                if (target != null && !ReferenceEquals(target, _selectedAlignment))
                    SelectedAlignment = target;
            }
            catch { /* ignore */ }
        }

        private void ReverseSelected()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null || _selectedAlignment == null) return;
            var aln = _selectedAlignment.Alignment;
            if (aln.Source?.PiElements == null || aln.Source.PiElements.Count < 2) return;

            var elements = aln.Source.PiElements
                .Select(e => new PiElement(e.P, e.Radius, e.SpiralIn, e.SpiralOut, e.Tag))
                .ToList();
            var reversed = AlignmentReverser.ReversePiElements(elements);

            double totalRaw = aln.Centerline?.GetPlanarLength() ?? 0.0;
            if (totalRaw > 0 && aln.StationEquations != null && aln.StationEquations.Count > 0)
            {
                var mapped = AlignmentReverser.ReverseStationEquations(aln.StationEquations, totalRaw);
                aln.StationEquations.Clear();
                foreach (var eq in mapped) aln.StationEquations.Add(eq);
            }

            if (Commands.Road.RoadAlignmentPiPipeline.RebuildAndPersist(
                    doc, aln, reversed, $"已反转 {aln.Name} 方向（面板）。"))
            {
                StatusText = $"已反转 {aln.Name}。桩号 / 几何点标注需重新生成。";
                ReloadSelectedAlignmentFromDomain();
            }
            else
            {
                StatusText = "反转失败（详情见命令行）。";
            }
        }

        private void RunOffset()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null || _selectedAlignment == null) return;
            StatusText = "偏移距离请在命令行输入…";
            try { doc.SendStringToExecute("hyRoadAlnOffset ", true, false, true); }
            catch (Exception ex) { StatusText = $"偏移命令调度失败：{ex.Message}"; }
        }

        private enum PiCsvKind { PiTable, Frame }

        private void ExportCsv(PiCsvKind kind)
        {
            if (_selectedAlignment == null) return;
            var aln = _selectedAlignment.Alignment;
            if (aln.Source?.PiElements == null || aln.Source.PiElements.Count < 2)
            {
                StatusText = "当前 Alignment 无 PI 表，无法导出。";
                return;
            }
            string suffix = kind == PiCsvKind.PiTable ? "_交点表.csv" : "_复测表.csv";
            string tag = kind == PiCsvKind.PiTable ? "交点表(PI 表)" : "复测表(几何框架表)";
            string defaultFileName = SanitizeFileName(aln.Name ?? "Alignment") + suffix;
            var dlg = new System.Windows.Forms.SaveFileDialog
            {
                Filter = "CSV 文件|*.csv",
                Title = $"导出 {tag}",
                FileName = defaultFileName,
                DefaultExt = "csv",
                AddExtension = true,
            };
            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) { StatusText = "已取消。"; return; }
            try
            {
                string csv = kind == PiCsvKind.PiTable
                    ? AlignmentReportService.BuildPiTableCsv(aln)
                    : AlignmentReportService.BuildFrameTableCsv(aln);
                File.WriteAllBytes(dlg.FileName, AlignmentReportService.ToUtf8BomBytes(csv));
                StatusText = $"{tag} 已导出：{dlg.FileName}";
            }
            catch (Exception ex)
            {
                StatusText = $"导出失败：{ex.Message}";
            }
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Alignment";
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Trim();
        }

        // =============================== 顶栏：应用 / 一键定稿 ===============================

        private bool CanApplyAlignment()
        {
            if (_selectedAlignment == null) return false;
            // 需要至少 2 个 PI 点（端点）才能重建几何。
            int count = 0;
            if (PiItems.Count >= 2)
            {
                count = PiItems.Count;
            }
            else
            {
                var src = _selectedAlignment.Alignment?.Source?.PiElements;
                count = src?.Count ?? 0;
            }
            return count >= 2;
        }

        /// <summary>
        /// 「应用」按钮 — 一键定稿：把当前 PI 表（或 PI 编辑器工作副本）写入 <c>05_hy_道路_平面线位</c>
        /// 的正式 Polyline（新建 / 就地更新），同步 <c>.roaddesign.json</c>。
        ///
        /// <para>优先使用 <see cref="PiEditorVm"/> 的工作副本（包含用户未提交的滑块调整）；
        /// 无编辑器时退回用 <see cref="PiItems"/>。</para>
        /// </summary>
        private void ApplyAlignment()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null || _selectedAlignment == null) return;

            List<PiElement> elements;
            if (PiEditorVm != null)
            {
                elements = PiEditorVm.GetWorkingElements().ToList();
            }
            else
            {
                elements = PiItems.Select(p => p.Element).ToList();
            }
            if (elements.Count < 2)
            {
                StatusText = "PI 数量 < 2，无法定稿。";
                return;
            }

            try { _preview.Clear(); } catch { /* ignore */ }

            try
            {
                var apply = ServiceLocator.Resolve<RoadAlignmentApplyService>();
                var result = apply.Apply(doc, _selectedAlignment.Alignment, elements);
                if (!result.Success)
                {
                    StatusText = "应用失败：" + (result.Message ?? "未知错误。");
                    return;
                }

                int? preservePi = _selectedPi?.Index;
                ReloadSelectedAlignmentFromDomain(preservePiIndex: preservePi);
                StatusText = string.IsNullOrEmpty(result.Message)
                    ? "已应用到 05_hy_道路_平面线位。"
                    : result.Message;

                if (!string.IsNullOrEmpty(result.JsonPath))
                {
                    StatusText += $"  JSON: {result.JsonPath}";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"应用失败：{ex.Message}";
            }
        }

        private void RevertPiEdit()
        {
            if (_selectedPi == null || PiEditorVm == null) return;
            var elements = PiItems.Select(p => p.Element).ToList();
            _suspendPiEditorEvents = true;
            try { PiEditorVm.Rebind(elements, _selectedPi.Index); }
            finally { _suspendPiEditorEvents = false; }
            StatusText = "已撤销未提交的修改。";
        }

        private void InsertPi()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            StatusText = "在命令行中输入新 PI 位置与参数…";
            try { doc.SendStringToExecute("hyRoadAlnInsertPi ", true, false, true); }
            catch (Exception ex) { StatusText = $"插入命令调度失败：{ex.Message}"; }
        }

        private bool CanDeleteSelectedPi()
        {
            if (_selectedAlignment == null || _selectedPi == null || _selectedPi.IsEndpoint) return false;
            return PiItems.Count >= 4;
        }

        private void DeletePi()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null || _selectedAlignment == null || _selectedPi == null || _selectedPi.IsEndpoint) return;

            int idx = _selectedPi.Index;
            var confirm = System.Windows.MessageBox.Show(
                $"确认删除 PI[{idx}] ({_selectedPi.X:F3}, {_selectedPi.Y:F3})？",
                "路线工作台 — 删除 PI",
                System.Windows.MessageBoxButton.OKCancel,
                System.Windows.MessageBoxImage.Warning);
            if (confirm != System.Windows.MessageBoxResult.OK) { StatusText = "已取消删除。"; return; }

            var aln = _selectedAlignment.Alignment;
            var elements = PiItems.Select(p => p.Element).ToList();
            if (idx <= 0 || idx >= elements.Count - 1) return;
            elements.RemoveAt(idx);

            if (Commands.Road.RoadAlignmentPiPipeline.RebuildAndPersist(
                    doc, aln, elements, $"已删除 PI[{idx}]（面板）。"))
            {
                StatusText = $"已删除 PI[{idx}]。";
                ReloadSelectedAlignmentFromDomain();
            }
            else
            {
                StatusText = "删除失败（详情见命令行）。";
            }
        }

        // =============================== 底栏：标注 ===============================

        private void DrawStationLabels(bool openConfig)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            try
            {
                string cmd = openConfig ? "hyRoadAlnStation C " : "hyRoadAlnStation A ";
                doc.SendStringToExecute(cmd, true, false, true);
                StatusText = openConfig ? "已请求打开桩号配置…" : "已应用桩号标注…";
            }
            catch (Exception ex)
            {
                StatusText = $"桩号标注调度失败：{ex.Message}";
            }
        }

        private void DrawGeometryPoints()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            try
            {
                doc.SendStringToExecute("hyRoadAlnGeomPt ", true, false, true);
                StatusText = "已请求几何点标注…";
            }
            catch (Exception ex)
            {
                StatusText = $"几何点标注调度失败：{ex.Message}";
            }
        }

        // =============================== 右列：桩号方程 ===============================

        private void AddStationEquation()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null || _selectedAlignment == null) return;
            try
            {
                doc.SendStringToExecute("hyRoadAlnStaEq ", true, false, true);
                StatusText = "在命令行继续桩号方程编辑（结束后自动刷新）…";
            }
            catch (Exception ex)
            {
                StatusText = $"桩号方程命令调度失败：{ex.Message}";
            }
        }

        private void DeleteSelectedStationEquation()
        {
            if (_selectedAlignment == null || _selectedStationEquation == null) return;
            var aln = _selectedAlignment.Alignment;
            if (aln.StationEquations == null || aln.StationEquations.Count == 0) return;
            int i = _selectedStationEquation.Index;
            if (i < 0 || i >= aln.StationEquations.Count) return;

            var confirm = System.Windows.MessageBox.Show(
                $"确认删除桩号方程 [{i}]：Raw={_selectedStationEquation.BeforeRaw:F3} → Ahead={_selectedStationEquation.AheadStation:F3}？",
                "路线工作台 — 删除桩号方程",
                System.Windows.MessageBoxButton.OKCancel,
                System.Windows.MessageBoxImage.Warning);
            if (confirm != System.Windows.MessageBoxResult.OK) return;

            aln.StationEquations.RemoveAt(i);
            try
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
                    var exporter = ServiceLocator.Resolve<RoadJsonExportService>();
                    if (registry.TryGet(doc.Name, out var design))
                        exporter.SaveForDocument(design, doc.Name);
                }
            }
            catch (Exception ex)
            {
                StatusText = $"保存桩号方程失败：{ex.Message}";
                return;
            }
            BuildReadModels(aln, piDesignerFallback: aln.Source?.PiElements == null || aln.Source.PiElements.Count < 2);
            StatusText = $"已删除桩号方程 [{i}]。";
        }

        // =============================== Helpers ===============================

        private void ReloadSelectedAlignmentFromDomain(int? preservePiIndex = null)
        {
            if (_selectedAlignment == null) return;
            var id = _selectedAlignment.Id;
            _selectedAlignment.NotifyHeaderRefreshed();
            // 重新展开 PI 列表 / 右列
            _selectedAlignment = null;
            SelectedAlignment = Alignments.FirstOrDefault(a => a.Id == id);
            if (preservePiIndex.HasValue)
            {
                int idx = preservePiIndex.Value;
                if (idx >= 0 && idx < PiItems.Count) SelectedPi = PiItems[idx];
            }
        }

        // =============================== Command CanExecute 刷新 ===============================

        /// <summary>
        /// 统一把所有「条件依赖 VM 状态」的命令重新查一遍 <see cref="ICommand.CanExecute"/>，
        /// 让绑定它的 <see cref="System.Windows.Controls.Button"/> 刷新 <c>IsEnabled</c>。
        ///
        /// <para>背景：本项目 <see cref="RelayCommand"/> 刻意不挂 <c>CommandManager.RequerySuggested</c>，
        /// 所以 <c>SelectedAlignment / SelectedPi / PiEditorVm / SelectedStationEquation</c> 变化时
        /// 需要显式调用本方法，否则顶栏「应用」/ 底栏「撤销」等按钮会一直停留在构造时评估的灰态。
        /// 纯静态命令（<c>PickAlignmentCmd</c>）无 canExecute，也安全调用。</para>
        /// </summary>
        private void RefreshCommandStates()
        {
            (DrawLivePreviewCmd as RelayCommand)?.RaiseCanExecuteChanged();
            (ApplyAlignmentCmd as RelayCommand)?.RaiseCanExecuteChanged();
            (ReverseCmd as RelayCommand)?.RaiseCanExecuteChanged();
            (OffsetCmd as RelayCommand)?.RaiseCanExecuteChanged();
            (ExportPiCsvCmd as RelayCommand)?.RaiseCanExecuteChanged();
            (ExportFrameCsvCmd as RelayCommand)?.RaiseCanExecuteChanged();
            (InsertPiCmd as RelayCommand)?.RaiseCanExecuteChanged();
            (DeletePiCmd as RelayCommand)?.RaiseCanExecuteChanged();
            (RevertPiEditCmd as RelayCommand)?.RaiseCanExecuteChanged();
            (AddStationEquationCmd as RelayCommand)?.RaiseCanExecuteChanged();
            (DeleteStationEquationCmd as RelayCommand)?.RaiseCanExecuteChanged();
        }

        // =============================== INotifyPropertyChanged ===============================

        public event PropertyChangedEventHandler PropertyChanged;

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // =============================== IDisposable ===============================

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (PiEditorVm != null) PiEditorVm.PreviewRequested -= OnPiEditorPreviewRequested;
                _alignmentChangedSub?.Dispose();
                _preview.Dispose();
                // v2：DesignPreview 自动彩色机制已下线，这里无需清任何 DWG 实体。
                // RawPick / LivePreview / 正式线位皆为用户资产，不碰。
            }
            catch { /* ignore */ }
        }
    }

    /// <summary>右列「分段表」行 VM（只读快照，避免 UI 引用可变 struct）。</summary>
    public sealed class SegmentRecordRowVm
    {
        public SegmentRecordRowVm(SegmentRecord src)
        {
            Source = src;
            Index = src.Index;
            KindLabel = src.KindLabel();
            StationStartM = src.StationStartM;
            StationEndM = src.StationEndM;
            LengthM = src.LengthM;
            Radius = src.Radius;
            SpiralA = src.SpiralA;
            PiIndex = src.PiIndex;
        }
        public SegmentRecord Source { get; }
        public int Index { get; }
        public string KindLabel { get; }
        public double StationStartM { get; }
        public double StationEndM { get; }
        public double LengthM { get; }
        public double Radius { get; }
        public double SpiralA { get; }
        public int PiIndex { get; }

        public string Header =>
            $"[{Index}] {KindLabel}  {StationStartM:F3}→{StationEndM:F3}  L={LengthM:F3}"
            + (double.IsNaN(Radius) ? string.Empty : $"  R={Radius:F2}");
    }

    /// <summary>右列「几何点表」行 VM。</summary>
    public sealed class GeometryPointRowVm
    {
        public GeometryPointRowVm(GeometryPoint gp)
        {
            PiIndex = gp.PiIndex;
            KindLabel = gp.KindLabel();
            StationM = gp.StationM;
            X = gp.Point.X;
            Y = gp.Point.Y;
        }
        public int PiIndex { get; }
        public string KindLabel { get; }
        public double StationM { get; }
        public double X { get; }
        public double Y { get; }

        public string Header => $"{KindLabel}  K={StationM:F3}  ({X:F2}, {Y:F2})  PI={PiIndex}";
    }
}
