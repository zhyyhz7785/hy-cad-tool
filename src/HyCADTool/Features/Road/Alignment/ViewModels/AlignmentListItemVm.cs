using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Domain.ValueObjects.Road;
using HyCADTool.Features.Road.PlanAlignment.Domain;

using HyCADTool.Shell.ViewModels;
namespace HyCADTool.Features.Road.PlanAlignment.ViewModels
{
    /// <summary>
    /// 路线工作台「线位列表（Outliner）」行 VM。只读快照 + 选择态。
    /// 底层 Alignment 由工作台 VM 在切换文档 / RebindForDocument 后刷新，避免 ObjectId 过期。
    /// </summary>
    public sealed class AlignmentListItemVm : INotifyPropertyChanged
    {
        public AlignmentListItemVm(Alignment alignment)
        {
            Alignment = alignment ?? throw new ArgumentNullException(nameof(alignment));
        }

        public Alignment Alignment { get; }
        public Guid Id => Alignment.Id;
        public string Name => Alignment.Name ?? "-";
        public int PiCount => Alignment.Source?.PiElements?.Count ?? 0;
        public int VertexCount => Alignment.Centerline?.VertexCount ?? 0;
        public double PlanarLengthM => Alignment.Centerline?.GetPlanarLength() ?? 0.0;

        /// <summary>用于 Outliner 单行展示。</summary>
        public string Header =>
            $"{Name}  · PI {PiCount}  · {PlanarLengthM:F2} m";

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyHeaderRefreshed()
        {
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(PiCount));
            OnPropertyChanged(nameof(VertexCount));
            OnPropertyChanged(nameof(PlanarLengthM));
            OnPropertyChanged(nameof(Header));
        }

        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// PI 列表行 VM：展示当前 Alignment 的一个 PI 记录。
    /// </summary>
    public sealed class PiListItemVm : INotifyPropertyChanged
    {
        public PiListItemVm(int index, PiElement element, bool isEndpoint)
        {
            Index = index;
            Element = element;
            IsEndpoint = isEndpoint;
        }

        public int Index { get; }
        public PiElement Element { get; private set; }
        public bool IsEndpoint { get; }

        public double X => Element.P.X;
        public double Y => Element.P.Y;
        public double Radius => Element.Radius;
        public double SpiralIn => Element.SpiralIn;
        public double SpiralOut => Element.SpiralOut;

        /// <summary>用于 Outliner 单行展示：端点前缀「*」，其它带 R/Ls 简写。</summary>
        public string Header
        {
            get
            {
                string tag = IsEndpoint ? "* " : "  ";
                if (IsEndpoint)
                    return $"{tag}PI[{Index}]  ({X:F2}, {Y:F2})";
                return $"{tag}PI[{Index}]  ({X:F2}, {Y:F2})  R={Radius:F1}  Ls={SpiralIn:F1}/{SpiralOut:F1}";
            }
        }

        public void Update(PiElement element)
        {
            Element = element;
            OnPropertyChanged(nameof(X));
            OnPropertyChanged(nameof(Y));
            OnPropertyChanged(nameof(Radius));
            OnPropertyChanged(nameof(SpiralIn));
            OnPropertyChanged(nameof(SpiralOut));
            OnPropertyChanged(nameof(Header));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// 桩号方程表行 VM：BeforeRaw → AheadStation 的一条记录，含显示跳变。
    /// </summary>
    public sealed class StationEquationRowVm
    {
        public StationEquationRowVm(int index, StationEquation eq, double startStation)
        {
            Index = index;
            BeforeRaw = eq.BeforeRaw;
            AheadStation = eq.AheadStation;
            DisplayJump = eq.AheadStation - (startStation + eq.BeforeRaw);
        }

        public int Index { get; }
        public double BeforeRaw { get; }
        public double AheadStation { get; }
        public double DisplayJump { get; }

        public string Header =>
            $"[{Index}]  Raw={BeforeRaw:F3}  →  Ahead={AheadStation:F3}  Δ={DisplayJump:+0.000;-0.000}";
    }
}
