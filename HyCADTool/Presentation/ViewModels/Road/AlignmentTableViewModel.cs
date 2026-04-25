using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Presentation.ViewModels;

namespace HyCADTool.Presentation.ViewModels.Road
{
    /// <summary>
    /// Alignment Sub-Entity 全表窗口的 ViewModel。
    ///
    /// 数据来源：<see cref="AlignmentStationBreakdown.Build"/> 产出的
    /// <see cref="AlignmentBreakdown"/>。
    /// 职责：
    /// <list type="bullet">
    ///   <item>把 <see cref="SegmentRecord"/> / <see cref="GeometryPoint"/> 转为 WPF 可绑定的行（格式化桩号 / 半径 / 偏角）；</item>
    ///   <item>暴露 <see cref="SelectedSegment"/> 给段 DataGrid 双向绑定，选中后通过 <see cref="SegmentSelectionChanged"/> 事件通知命令层去 AutoCAD 里高亮；</item>
    ///   <item>不引用 AutoCAD API：本 VM 可纯单测，AutoCAD 绘图由命令层承担。</item>
    /// </list>
    /// </summary>
    public sealed class AlignmentTableViewModel : INotifyPropertyChanged
    {
        /// <summary>段被选中时触发；value 可能为 null（清空选中）。</summary>
        public event EventHandler<SegmentRow> SegmentSelectionChanged;

        /// <summary>请求窗口关闭（由 Code-behind 订阅后调用 <c>Close()</c>）。</summary>
        public event EventHandler<bool?> CloseRequested;

        public string HeaderTitle { get; }

        public string AlignmentSummary { get; }

        public ObservableCollection<SegmentRow> Segments { get; }

        public ObservableCollection<GeometryPointRow> GeometryPoints { get; }

        public ICommand CloseCommand { get; }

        private SegmentRow _selectedSegment;

        /// <summary>当前选中的段（与 DataGrid.SelectedItem 双向绑定）。</summary>
        public SegmentRow SelectedSegment
        {
            get => _selectedSegment;
            set
            {
                if (ReferenceEquals(_selectedSegment, value)) return;
                _selectedSegment = value;
                OnPropertyChanged();
                SegmentSelectionChanged?.Invoke(this, value);
            }
        }

        public AlignmentTableViewModel(Alignment alignment, AlignmentBreakdown breakdown)
        {
            if (alignment == null) throw new ArgumentNullException(nameof(alignment));
            if (breakdown == null) throw new ArgumentNullException(nameof(breakdown));

            HeaderTitle = $"分段表 — {alignment.Name ?? "(未命名)"}";

            int arcCount = breakdown.Segments.Count(s => s.Kind == SegmentKind.Arc);
            int spiralCount = breakdown.Segments.Count(s => s.Kind == SegmentKind.Spiral);
            int lineCount = breakdown.Segments.Count(s => s.Kind == SegmentKind.Line);

            AlignmentSummary =
                $"起桩 {AlignmentStationBreakdown.FormatStation(breakdown.StartStationM)}   "
                + $"止桩 {AlignmentStationBreakdown.FormatStation(breakdown.EndStationM)}   "
                + $"总长 {breakdown.TotalLengthM:F3} m   "
                + $"段数 {breakdown.Segments.Count}（直 {lineCount} / 缓 {spiralCount} / 圆 {arcCount}）   "
                + $"几何点 {breakdown.GeometryPoints.Count}";

            Segments = new ObservableCollection<SegmentRow>(
                breakdown.Segments.Select(SegmentRow.From));

            GeometryPoints = new ObservableCollection<GeometryPointRow>(
                breakdown.GeometryPoints.Select(GeometryPointRow.From));

            CloseCommand = new RelayCommand(() => CloseRequested?.Invoke(this, true));
        }

        /// <summary>纯 VM 单测用的构造：跳过 Alignment，只绑定 breakdown。</summary>
        public AlignmentTableViewModel(AlignmentBreakdown breakdown, string title)
        {
            if (breakdown == null) throw new ArgumentNullException(nameof(breakdown));
            HeaderTitle = title ?? "分段表";
            AlignmentSummary = $"总长 {breakdown.TotalLengthM:F3} m，段数 {breakdown.Segments.Count}";
            Segments = new ObservableCollection<SegmentRow>(breakdown.Segments.Select(SegmentRow.From));
            GeometryPoints = new ObservableCollection<GeometryPointRow>(breakdown.GeometryPoints.Select(GeometryPointRow.From));
            CloseCommand = new RelayCommand(() => CloseRequested?.Invoke(this, true));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    /// <summary>
    /// 段表的单行。字段字符串化后直接 DataGrid 显示，同时保留 <see cref="Source"/>
    /// 以便命令层按原始段几何做高亮。
    /// </summary>
    public sealed class SegmentRow
    {
        /// <summary>段序号（0 基，但 UI 显示 1 基由 <see cref="DisplayIndex"/> 提供）。</summary>
        public int Index { get; set; }

        /// <summary>段序号的 UI 显示值（1 基）。</summary>
        public int DisplayIndex => Index + 1;

        /// <summary>段类型中文短标签（直 / 缓 / 圆）。</summary>
        public string KindLabel { get; set; }

        /// <summary>格式化后的起桩号（K0+000.000）。</summary>
        public string StationStart { get; set; }

        /// <summary>格式化后的止桩号。</summary>
        public string StationEnd { get; set; }

        /// <summary>段长（m，保留 3 位小数）。</summary>
        public string Length { get; set; }

        /// <summary>半径（圆 / 缓和段：m 字符串；直线段：空）。</summary>
        public string Radius { get; set; }

        /// <summary>Ls 或 A（缓和段：Ls=xx A=yy；圆：R=xx；直线：空）。</summary>
        public string LsOrA { get; set; }

        /// <summary>该段首末切线方位角之差（度，带符号）。</summary>
        public string DeflectionDeg { get; set; }

        /// <summary>原始段记录（命令层高亮时用）。</summary>
        public SegmentRecord Source { get; set; }

        public static SegmentRow From(SegmentRecord r)
        {
            string radius = double.IsNaN(r.Radius) ? string.Empty : r.Radius.ToString("F3");
            string lsOrA;
            switch (r.Kind)
            {
                case SegmentKind.Spiral:
                    lsOrA = $"Ls={r.SpiralLs:F3}  A={r.SpiralA:F3}";
                    break;
                case SegmentKind.Arc:
                    lsOrA = $"R={r.Radius:F3}";
                    break;
                default:
                    lsOrA = string.Empty;
                    break;
            }

            double deflection = NormalizePi(r.EndBearingRad - r.StartBearingRad) * 180.0 / Math.PI;

            return new SegmentRow
            {
                Index = r.Index,
                KindLabel = r.KindLabel(),
                StationStart = AlignmentStationBreakdown.FormatStation(r.StationStartM),
                StationEnd = AlignmentStationBreakdown.FormatStation(r.StationEndM),
                Length = r.LengthM.ToString("F3"),
                Radius = radius,
                LsOrA = lsOrA,
                DeflectionDeg = deflection.ToString("F3"),
                Source = r,
            };
        }

        /// <summary>把 bearing 差归一化到 (-π, π]，避免跨 ±180° 时出现假大数。</summary>
        private static double NormalizePi(double rad)
        {
            while (rad > Math.PI) rad -= 2 * Math.PI;
            while (rad <= -Math.PI) rad += 2 * Math.PI;
            return rad;
        }
    }

    /// <summary>几何点表的单行。</summary>
    public sealed class GeometryPointRow
    {
        /// <summary>几何点类型（BP / EP / BC / EC / TS / SC / CS / ST / PI）。</summary>
        public string Kind { get; set; }

        /// <summary>格式化桩号。</summary>
        public string Station { get; set; }

        /// <summary>X 坐标（m，保留 3 位小数）。</summary>
        public string X { get; set; }

        /// <summary>Y 坐标（m，保留 3 位小数）。</summary>
        public string Y { get; set; }

        /// <summary>所属 PI 索引（0 = BP、Count-1 = EP）。</summary>
        public int PiIndex { get; set; }

        public static GeometryPointRow From(GeometryPoint p)
        {
            return new GeometryPointRow
            {
                Kind = p.Kind.ToString(),
                Station = AlignmentStationBreakdown.FormatStation(p.StationM),
                X = p.Point.X.ToString("F3"),
                Y = p.Point.Y.ToString("F3"),
                PiIndex = p.PiIndex,
            };
        }
    }
}
