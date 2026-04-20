using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Autodesk.AutoCAD.ApplicationServices;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Domain.ValueObjects.Road;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road;
using HyCADTool.Refactored.Infrastructure.Configuration;
using HyCADTool.Refactored.Presentation.ViewModels;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.ViewModels.Road
{
    /// <summary>
    /// 「路线工作台」面板的根 ViewModel。聚合当前 DWG 的 Alignment 列表 + 选中线位的 PI 列表 +
    /// 复用 <see cref="PiThreeUnitViewModel"/> 做 PI 参数编辑 + 只读分段/方程/几何点表。
    ///
    /// 设计原则：
    /// - 所有对 DWG 的写入都走命令层管线（<see cref="Commands.Road.RoadAlignmentPiPipeline"/>），
    ///   VM 只做 UI 状态、预览与一次性 orchestration；
    /// - 切换 Alignment / PI 时尽量复用 <see cref="PiThreeUnitViewModel.Rebind"/>，避免预览订阅断线；
    /// - <see cref="RefreshAlignments"/> 在 PanelManager 的 DocumentActivated 钩子里被调用（工作台 WPF 窗口可见时），
    ///   以此替代"每命令重新打开窗口"的传统做法；
    /// - VM 持有 <see cref="RoadAlignmentPreviewService"/>，Dispose 时释放 Transient 句柄。
    /// </summary>
    public sealed class RoadAlignmentWorkbenchViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly RoadAlignmentPreviewService _preview;
        private bool _disposed;
        private bool _suspendPiEditorEvents;

        public RoadAlignmentWorkbenchViewModel()
        {
            _preview = new RoadAlignmentPreviewService();

            PickAlignmentCmd = new RelayCommand(PickAlignmentOnCanvas);
            ReverseCmd = new RelayCommand(ReverseSelected, () => SelectedAlignment != null);
            OffsetCmd = new RelayCommand(RunOffset, () => SelectedAlignment != null);
            ExportPiCsvCmd = new RelayCommand(() => ExportCsv(PiCsvKind.PiTable), () => SelectedAlignment != null);
            ExportFrameCsvCmd = new RelayCommand(() => ExportCsv(PiCsvKind.Frame), () => SelectedAlignment != null);

            InsertPiCmd = new RelayCommand(InsertPi, () => SelectedAlignment != null);
            DeletePiCmd = new RelayCommand(DeletePi, CanDeleteSelectedPi);
            ApplyPiEditCmd = new RelayCommand(ApplyPiEdit, () => PiEditorVm != null && SelectedPi != null && !SelectedPi.IsEndpoint);
            RevertPiEditCmd = new RelayCommand(RevertPiEdit, () => PiEditorVm != null && SelectedPi != null && !SelectedPi.IsEndpoint);

            DrawStationLabelsCmd = new RelayCommand<object>(p => DrawStationLabels(p is bool b && b));
            DrawGeometryPointsCmd = new RelayCommand(DrawGeometryPoints);

            AddStationEquationCmd = new RelayCommand(AddStationEquation, () => SelectedAlignment != null);
            DeleteStationEquationCmd = new RelayCommand(DeleteSelectedStationEquation,
                () => SelectedAlignment != null && SelectedStationEquation != null);

            RefreshCmd = new RelayCommand(RefreshAlignments);

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
            }
        }

        // =============================== PI 编辑子 VM ===============================

        private PiThreeUnitViewModel _piEditorVm;
        public PiThreeUnitViewModel PiEditorVm
        {
            get => _piEditorVm;
            private set => SetProperty(ref _piEditorVm, value);
        }

        // =============================== 只读表 ===============================

        public ObservableCollection<SegmentRecordRowVm> Segments { get; } = new ObservableCollection<SegmentRecordRowVm>();
        public ObservableCollection<StationEquationRowVm> StationEquations { get; } = new ObservableCollection<StationEquationRowVm>();
        public ObservableCollection<GeometryPointRowVm> GeometryPoints { get; } = new ObservableCollection<GeometryPointRowVm>();

        private StationEquationRowVm _selectedStationEquation;
        public StationEquationRowVm SelectedStationEquation
        {
            get => _selectedStationEquation;
            set => SetProperty(ref _selectedStationEquation, value);
        }

        // =============================== 状态文本 ===============================

        private string _statusText = "就绪。";
        public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }

        private string _headerInfo = "-";
        public string HeaderInfo { get => _headerInfo; private set => SetProperty(ref _headerInfo, value); }

        // =============================== Commands ===============================

        public ICommand PickAlignmentCmd { get; }
        public ICommand ReverseCmd { get; }
        public ICommand OffsetCmd { get; }
        public ICommand ExportPiCsvCmd { get; }
        public ICommand ExportFrameCsvCmd { get; }
        public ICommand InsertPiCmd { get; }
        public ICommand DeletePiCmd { get; }
        public ICommand ApplyPiEditCmd { get; }
        public ICommand RevertPiEditCmd { get; }
        public ICommand DrawStationLabelsCmd { get; }
        public ICommand DrawGeometryPointsCmd { get; }
        public ICommand AddStationEquationCmd { get; }
        public ICommand DeleteStationEquationCmd { get; }
        public ICommand RefreshCmd { get; }

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

            if (!registry.TryGet(doc.Name, out var design) || design.Alignments.Count == 0)
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

            if (_selectedAlignment == null)
            {
                SelectedPi = null;
                PiEditorVm = null;
                _preview.Clear();
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

            UpdatePreviewFromDomain(aln);
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

        private void OnPiEditorPreviewRequested(object sender, PiDesignResult result)
        {
            if (_suspendPiEditorEvents) return;
            if (result.Polyline == null) return;
            try { _preview.Update(result.Polyline); } catch { /* 文档关闭等场景忽略 */ }
        }

        private void UpdatePreviewFromDomain(Alignment aln)
        {
            if (aln?.Centerline == null || aln.Centerline.VertexCount < 2) return;
            try { _preview.Update(aln.Centerline); } catch { /* ignore */ }
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
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            if (!Commands.Road.RoadAlignmentPiPipeline.PickAlignment(doc, out var alignment, out var _)) return;
            RefreshAlignments();
            var target = Alignments.FirstOrDefault(a => a.Id == alignment.Id);
            if (target != null) SelectedAlignment = target;
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

        // =============================== 中列：PI 编辑 ===============================

        private void ApplyPiEdit()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null || _selectedAlignment == null || _selectedPi == null || _selectedPi.IsEndpoint) return;
            if (PiEditorVm == null) return;

            var aln = _selectedAlignment.Alignment;
            var elements = PiEditorVm.GetWorkingElements().ToList();

            try
            {
                if (Commands.Road.RoadAlignmentPiPipeline.RebuildAndPersist(
                        doc, aln, elements, $"PI[{_selectedPi.Index}] 已更新（面板）。"))
                {
                    StatusText = $"PI[{_selectedPi.Index}] 已应用。";
                    ReloadSelectedAlignmentFromDomain(preservePiIndex: _selectedPi.Index);
                }
                else
                {
                    StatusText = "应用失败（详情见命令行）。";
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
                _preview.Dispose();
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
