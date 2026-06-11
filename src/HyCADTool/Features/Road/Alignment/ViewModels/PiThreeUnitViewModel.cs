using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyCADTool.Domain.Services.Road;
using HyCAD.Geometry;
using HyCADTool.Features.Road.PlanAlignment.Domain;

using HyCADTool.Shell.ViewModels;
namespace HyCADTool.Features.Road.PlanAlignment.ViewModels
{
    /// <summary>
    /// "三单元（缓-圆-缓）平曲线设计"窗口的 ViewModel。
    ///
    /// 职责：
    /// - 暴露主输入（R / Ls / Ls1 / Ls2 / 对称 / 设计速度）给 WPF 双向绑定；
    /// - 主输入每次变更后 <see cref="Recalculate"/>：更新派生量（T1 / T2 / Ly / 转角）+ 规范检查 + 触发预览事件；
    /// - 确认/取消时通过 <see cref="Confirmed"/> / <see cref="Cancelled"/> 事件把结果回传命令层；
    /// - 命令层负责把预览（Polyline3D）投影到 AutoCAD Transient 层。
    ///
    /// 不在本 VM 内：
    /// - AutoCAD Transient 绘制 / Document 切换监听（放到 <c>RoadAlignmentPreviewService</c>）；
    /// - Polyline 落图 / JSON 写盘（由命令层复用 <c>RoadAlignmentService.RebuildCenterline</c>）。
    /// </summary>
    public sealed class PiThreeUnitViewModel : INotifyPropertyChanged
    {
        private List<PiElement> _elements;
        private int _piIndex;
        private bool _isInitializing;

        /// <summary>
        /// 预览事件：Recalculate 成功产出一个 <see cref="PiDesignResult"/> 后触发。
        /// 命令层订阅后把 <see cref="PiDesignResult.Polyline"/> 画成 Transient。
        /// </summary>
        public event EventHandler<PiDesignResult> PreviewRequested;

        /// <summary>用户点【确定】时触发，附带最终的 <see cref="PiElement"/>。</summary>
        public event EventHandler<PiElement> Confirmed;

        /// <summary>用户点【取消】或关闭窗口时触发。</summary>
        public event EventHandler Cancelled;

        /// <summary>请求窗口关闭（由 XAML Code-behind 订阅后调用 <c>Close()</c>）。</summary>
        public event EventHandler<bool?> CloseRequested;

        /// <summary>
        /// 带"有未通过项"提示的确认回调。传入失败项的详细文本，返回 true 表示用户仍然选择强行确定。
        /// 默认实现直接通过（VM 单测场景），UI 层（Code-behind）会替换成 MessageBox。
        /// </summary>
        public Func<string, bool> NonCompliantConfirm { get; set; } = _ => true;

        /// <summary>
        /// 构造 v1：拷贝外部传入的 PI 列表，锁定要编辑的 <paramref name="piIndex"/>。
        /// 调用方一般是 <c>RoadAlignmentEditPiCommand</c>。
        /// </summary>
        public PiThreeUnitViewModel(IReadOnlyList<PiElement> elements, int piIndex)
        {
            if (elements == null) throw new ArgumentNullException(nameof(elements));
            if (elements.Count < 3)
                throw new ArgumentException("PI 数量不足 3 个，无法编辑内部 PI。", nameof(elements));
            if (piIndex <= 0 || piIndex >= elements.Count - 1)
                throw new ArgumentOutOfRangeException(nameof(piIndex),
                    $"piIndex={piIndex} 必须介于 [1, {elements.Count - 2}]。");

            ConfirmCommand = new RelayCommand(ExecuteConfirm);
            CancelCommand = new RelayCommand(ExecuteCancel);

            RebindCore(elements, piIndex, initialDesignSpeed: 60);
        }

        /// <summary>
        /// 非模态面板复用入口：在工作台切换 Alignment / PI 时直接更新内部状态，
        /// 避免销毁 VM 导致事件订阅（PreviewRequested 等）断线。保留 DesignSpeed。
        /// </summary>
        public void Rebind(IReadOnlyList<PiElement> elements, int piIndex)
        {
            if (elements == null) throw new ArgumentNullException(nameof(elements));
            if (elements.Count < 3)
                throw new ArgumentException("PI 数量不足 3 个，无法编辑内部 PI。", nameof(elements));
            if (piIndex <= 0 || piIndex >= elements.Count - 1)
                throw new ArgumentOutOfRangeException(nameof(piIndex),
                    $"piIndex={piIndex} 必须介于 [1, {elements.Count - 2}]。");

            RebindCore(elements, piIndex, initialDesignSpeed: _designSpeed <= 0 ? 60 : _designSpeed);
        }

        private void RebindCore(IReadOnlyList<PiElement> elements, int piIndex, int initialDesignSpeed)
        {
            _elements = new List<PiElement>(elements);
            _piIndex = piIndex;

            _isInitializing = true;
            var current = _elements[piIndex];
            _radius = current.Radius;
            _spiralIn = current.SpiralIn;
            _spiralOut = current.SpiralOut;
            _isSymmetric = Math.Abs(current.SpiralIn - current.SpiralOut) <= AlignmentCodeChecker.SymmetryTolerance;
            _designSpeed = initialDesignSpeed;

            OnPropertyChanged(nameof(Radius));
            OnPropertyChanged(nameof(SpiralIn));
            OnPropertyChanged(nameof(SpiralOut));
            OnPropertyChanged(nameof(IsSymmetric));
            OnPropertyChanged(nameof(DesignSpeed));

            PiIndex = piIndex;
            TotalPi = elements.Count;
            Title = $"三单元平曲线设计 JD{piIndex}";

            PrevTangent = _elements[piIndex - 1].P.DistanceTo(_elements[piIndex].P);
            NextTangent = _elements[piIndex].P.DistanceTo(_elements[piIndex + 1].P);

            ConfirmedElement = null;
            _isInitializing = false;
            Recalculate();
        }

        // ================================ UI 绑定：主输入 ================================

        private double _radius;
        /// <summary>圆曲线半径 R（m）。</summary>
        public double Radius
        {
            get => _radius;
            set { if (SetProperty(ref _radius, SanitizeNonNegative(value))) Recalculate(); }
        }

        private double _spiralIn;
        /// <summary>入侧缓和曲线长 Ls1（m）。</summary>
        public double SpiralIn
        {
            get => _spiralIn;
            set
            {
                var v = SanitizeNonNegative(value);
                if (!SetProperty(ref _spiralIn, v)) return;
                if (_isSymmetric && Math.Abs(_spiralOut - v) > 1e-9)
                {
                    _spiralOut = v;
                    OnPropertyChanged(nameof(SpiralOut));
                }
                Recalculate();
            }
        }

        private double _spiralOut;
        /// <summary>出侧缓和曲线长 Ls2（m）；对称锁打开时等同于 Ls1。</summary>
        public double SpiralOut
        {
            get => _spiralOut;
            set
            {
                var v = SanitizeNonNegative(value);
                if (!SetProperty(ref _spiralOut, v)) return;
                if (_isSymmetric && Math.Abs(_spiralIn - v) > 1e-9)
                {
                    _spiralIn = v;
                    OnPropertyChanged(nameof(SpiralIn));
                }
                Recalculate();
            }
        }

        private bool _isSymmetric;
        /// <summary>对称锁：true 时 Ls1 = Ls2，Ls2 输入框禁用。</summary>
        public bool IsSymmetric
        {
            get => _isSymmetric;
            set
            {
                if (!SetProperty(ref _isSymmetric, value)) return;
                if (value && Math.Abs(_spiralIn - _spiralOut) > 1e-9)
                {
                    _spiralOut = _spiralIn;
                    OnPropertyChanged(nameof(SpiralOut));
                    Recalculate();
                }
            }
        }

        private int _designSpeed;
        /// <summary>设计速度（km/h），窗口内临时值；不持久化到 Domain。</summary>
        public int DesignSpeed
        {
            get => _designSpeed;
            set { if (SetProperty(ref _designSpeed, value)) Recalculate(); }
        }

        /// <summary>可选的设计速度列表（驱动 ComboBox）。</summary>
        public IReadOnlyList<int> AvailableSpeeds => AlignmentCodeChecker.SupportedSpeeds;

        // ================================ UI 绑定：标签 / 派生量 ================================

        private string _title;
        public string Title
        {
            get => _title;
            private set => SetProperty(ref _title, value);
        }

        private int _piIndexProp;
        public int PiIndex
        {
            get => _piIndexProp;
            private set => SetProperty(ref _piIndexProp, value);
        }

        private int _totalPi;
        public int TotalPi
        {
            get => _totalPi;
            private set => SetProperty(ref _totalPi, value);
        }

        private double _prevTangent;
        /// <summary>前直线段长（m）。</summary>
        public double PrevTangent
        {
            get => _prevTangent;
            private set => SetProperty(ref _prevTangent, value);
        }

        private double _nextTangent;
        /// <summary>后直线段长（m）。</summary>
        public double NextTangent
        {
            get => _nextTangent;
            private set => SetProperty(ref _nextTangent, value);
        }

        private double _turnRad;
        public double TurnRad { get => _turnRad; private set => SetProperty(ref _turnRad, value); }

        /// <summary>转角（度），带符号：左偏 &gt; 0 / 右偏 &lt; 0。</summary>
        public double TurnAngleDeg => TurnRad * 180.0 / Math.PI;

        /// <summary>转角描述文本，如 "左偏41.187°"。</summary>
        public string TurnAngleText => RoadGeometryFormulas.FormatTurn(TurnRad);

        private double _t1;
        public double T1 { get => _t1; private set => SetProperty(ref _t1, value); }

        private double _t2;
        public double T2 { get => _t2; private set => SetProperty(ref _t2, value); }

        private double _ly;
        public double Ly { get => _ly; private set => SetProperty(ref _ly, value); }

        // ================================ UI 绑定：规范检查 ================================

        /// <summary>6 项规范检查结果；WPF ItemsControl 通过 DataTrigger 把 Passed=false 的项染红。</summary>
        public ObservableCollection<CodeCheckItem> CheckItems { get; } = new ObservableCollection<CodeCheckItem>();

        private bool _allChecksPassed = true;
        public bool AllChecksPassed { get => _allChecksPassed; private set => SetProperty(ref _allChecksPassed, value); }

        private CodeCheckReport _lastReport;

        // ================================ UI 绑定：命令 ================================

        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        // ================================ 对外：最终结果 ================================

        /// <summary>
        /// 用户点【确定】后产出的新 <see cref="PiElement"/>；【取消】时为 null。
        /// 命令层在窗口关闭后取这个值调 Designer + Service。
        /// </summary>
        public PiElement? ConfirmedElement { get; private set; }

        // ================================ 核心重算 ================================

        /// <summary>
        /// 更新派生量、规范检查、触发预览事件。
        /// 每次主输入变更都应该被调用一次（直接 setter 调用，也在构造时预跑一次）。
        /// </summary>
        public void Recalculate()
        {
            if (_isInitializing) return;

            var current = _elements[_piIndex];
            var ephemeral = new PiElement(current.P, _radius, _spiralIn, _spiralOut, current.Tag);
            var scratch = new List<PiElement>(_elements);
            scratch[_piIndex] = ephemeral;

            // 1) 计算转角
            TurnRad = RoadGeometryFormulas.TurnAngle(
                _elements[_piIndex - 1].P,
                _elements[_piIndex].P,
                _elements[_piIndex + 1].P);
            OnPropertyChanged(nameof(TurnAngleDeg));
            OnPropertyChanged(nameof(TurnAngleText));

            // 2) 解析式派生量
            T1 = RoadGeometryFormulas.TangentInLength(_radius, _spiralIn, TurnRad);
            T2 = RoadGeometryFormulas.TangentOutLength(_radius, _spiralOut, TurnRad);
            Ly = RoadGeometryFormulas.CircularArcLength(_radius, _spiralIn, _spiralOut, TurnRad);

            // 3) 规范检查
            _lastReport = AlignmentCodeChecker.Check(
                _designSpeed,
                TurnRad,
                _radius,
                _spiralIn,
                _spiralOut,
                PrevTangent,
                NextTangent);

            CheckItems.Clear();
            foreach (var it in _lastReport.Items) CheckItems.Add(it);
            AllChecksPassed = _lastReport.AllPassed;

            // 4) 预览重建（几何失败就不发事件；UI 规范检查会提示问题）
            try
            {
                var result = AlignmentPiDesigner.Build(scratch, new PiDesignOptions());
                PreviewRequested?.Invoke(this, result);
            }
            catch (Exception)
            {
                // 几何无法构造（例如用户临时输入了非法值），忽略预览请求；规范检查项已反映。
            }
        }

        /// <summary>
        /// 从外部强制同步（如单元测试）。
        /// </summary>
        public CodeCheckReport LastReport => _lastReport;

        // ================================ Command Handlers ================================

        private void ExecuteConfirm()
        {
            // v1：有未通过项时先弹二次确认（不是硬卡）。用户自担责（临时方案、后期修改的常见场景）。
            if (_lastReport != null && !_lastReport.AllPassed)
            {
                var summary = BuildNonCompliantSummary(_lastReport);
                if (!NonCompliantConfirm(summary))
                {
                    return;
                }
            }

            var current = _elements[_piIndex];
            ConfirmedElement = new PiElement(current.P, _radius, _spiralIn, _spiralOut, current.Tag);
            Confirmed?.Invoke(this, ConfirmedElement.Value);
            CloseRequested?.Invoke(this, true);
        }

        /// <summary>
        /// 面板式（非模态）使用：取当前 PI 参数快照，不触发关闭事件、不弹未通过确认，
        /// 由调用方（工作台 VM）决定是否落盘。
        /// </summary>
        public PiElement SnapshotCurrent()
        {
            var current = _elements[_piIndex];
            return new PiElement(current.P, _radius, _spiralIn, _spiralOut, current.Tag);
        }

        /// <summary>
        /// 返回当前工作副本（完整 PI 表），供工作台 VM 在非模态场景下重建几何。
        /// </summary>
        public IReadOnlyList<PiElement> GetWorkingElements()
        {
            var result = new List<PiElement>(_elements);
            result[_piIndex] = SnapshotCurrent();
            return result;
        }

        private static string BuildNonCompliantSummary(CodeCheckReport report)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("当前方案存在未通过的规范项，继续确定将按当前参数写入图纸。");
            sb.AppendLine();
            foreach (var it in report.Items)
            {
                if (it.Passed) continue;
                sb.Append("• ").Append(it.Name).Append("：").AppendLine(it.Message);
                if (it.HasSuggestion) sb.Append("   → ").AppendLine(it.Suggestion);
            }
            sb.AppendLine();
            sb.Append("是否仍然继续？");
            return sb.ToString();
        }

        private void ExecuteCancel()
        {
            ConfirmedElement = null;
            Cancelled?.Invoke(this, EventArgs.Empty);
            CloseRequested?.Invoke(this, false);
        }

        private static double SanitizeNonNegative(double v) => v < 0 ? 0 : v;

        // ================================ INotifyPropertyChanged ================================

        public event PropertyChangedEventHandler PropertyChanged;

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
