using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCAD.Geometry;
using HyCADTool.Features.Fem.Integration;
using HyCADTool.Features.Fem.Renderer;
using HyCADTool.Shell.ViewModels;
using HyCADTool.Shared.AutoCAD.Configuration;
using HyCADTool.Shared.AutoCAD.Services;
using HyCADTool.Shell.Configuration.User;
using HYFEA.Core.Analysis;
using HYFEA.Core.Dofs;
using HYFEA.Core.Loads;
using HYFEA.Core.Model;
using HYFEA.Core.Results;

namespace HyCADTool.Features.Fem.Views
{
    public sealed class BeamMvpPanelViewModel : INotifyPropertyChanged
    {
        public static BeamMvpPanelViewModel Current { get; private set; }

        private readonly RelayCommand _solveCommand;

        public BeamMvpPanelViewModel()
        {
            Current = this;
            _solveCommand = new RelayCommand(ExecuteSolve, () => SourceEntityId.IsValid);
            SolveCommand = _solveCommand;
        }

        public IReadOnlyList<BeamSupportUi> SupportOptions { get; } = new[]
        {
            BeamSupportUi.Free,
            BeamSupportUi.Pin,
            BeamSupportUi.Fixed,
        };

        private ObjectId _sourceEntityId;

        public ObjectId SourceEntityId
        {
            get => _sourceEntityId;
            set
            {
                if (_sourceEntityId == value) return;
                _sourceEntityId = value;
                OnPropertyChanged();
                _solveCommand.RaiseCanExecuteChanged();
            }
        }

        public double YoungsModulus { get; set; } = 2.06e5;

        public double AreaMm2 { get; set; } = 8e4;

        public double InertiaZ { get; set; } = 1.067e9;

        public double Q { get; set; } = 10;

        public bool LoadIsGlobalNegY { get; set; } = true;

        public int Segments { get; set; } = 4;

        public double DeflectionScale { get; set; } = 1.0;

        public bool DrawDeflection { get; set; } = true;

        public bool DrawMoment { get; set; } = true;

        public bool DrawShear { get; set; } = true;

        public bool DrawReactions { get; set; } = true;

        public bool UseSiUnits { get; set; }

        public BeamSupportUi EndA { get; set; } = BeamSupportUi.Fixed;

        public BeamSupportUi EndB { get; set; } = BeamSupportUi.Free;

        public ICommand SolveCommand { get; }

        private UnitSystem Units => UseSiUnits ? UnitSystem.SI : UnitSystem.MmN;

        private void ExecuteSolve()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc?.Editor;
            if (doc == null || ed == null) return;
            if (!SourceEntityId.IsValid)
            {
                ed.WriteMessage("\n未选择梁线。");
                return;
            }

            try
            {
                int segUse = CoerceSegments(Segments, ed);
                if (segUse != Segments)
                    Segments = segUse;

                double eUse = CoercePositiveFinite(YoungsModulus, 2.06e5, "E (MPa)", ed);
                double aUse = CoercePositiveFinite(AreaMm2, 8e4, "A (mm²)", ed);
                double izUse = CoercePositiveFinite(InertiaZ, 1.067e9, "Iz (mm⁴)", ed);
                double qUse = CoercePositiveFinite(Q, 10.0, "q (N/mm)", ed);

                IReadOnlyList<Point2D> axis;
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var obj = tr.GetObject(SourceEntityId, OpenMode.ForRead);
                    var cv = obj as Curve;
                    if (cv == null)
                    {
                        ed.WriteMessage("\n实体不是曲线。");
                        return;
                    }

                    axis = HyfeaGeometryMapper.MapBeam(cv, segUse, Units);
                    tr.Commit();
                }

                double dxChord = axis[axis.Count - 1].X - axis[0].X;
                double dyChord = axis[axis.Count - 1].Y - axis[0].Y;
                double chordLen = Math.Sqrt(dxChord * dxChord + dyChord * dyChord);
                double cChord = chordLen > 1e-12 ? dxChord / chordLen : 1.0;
                double qEffApprox =
                    LoadIsGlobalNegY ? -qUse * cChord : qUse;

                ed.WriteMessage(
                    $"\n[HYFEA] 本次求解: seg={segUse}, q={qUse:g9} N/mm, E={eUse:g9}, A={aUse:g9}, Iz={izUse:g9}, 荷载={(LoadIsGlobalNegY ? "全局 -Y" : "局部法向")}");
                ed.WriteMessage(
                    $"\n[HYFEA] 轴弦(WCS) Δx={dxChord:g6}, Δy={dyChord:g6}, chord≈{chordLen:g6}; GlobalNegY 时 q_eff≈{qEffApprox:g9} N/mm");

                if (LoadIsGlobalNegY && chordLen > 1e-9 && Math.Abs(qEffApprox) < Math.Max(1e-12, 1e-9 * Math.Abs(qUse)))
                {
                    ed.WriteMessage(
                        "\n[HYFEA] 提示: 均布方向为全局 -Y 时横向强度 q_eff = -q·cosθ（θ 为梁轴相对 +X 的角）。梁轴接近竖直时 cosθ≈0，等价于无横向荷载，弯矩与位移为 0。请使用 WCS 下近似水平的梁线，或改用「局部法向」均布。");
                }

                var dir = LoadIsGlobalNegY ? BeamLoadDirection.GlobalNegY : BeamLoadDirection.LocalPerpendicular;
                var prob = HyfeaBeamProblemBuilder.Build(
                    axis,
                    eUse,
                    aUse,
                    izUse,
                    qUse,
                    dir,
                    EndA,
                    EndB,
                    Units);

                var sol = new LinearStaticAnalysis().Run(prob);
                if (!sol.Success || sol.Displacements == null || sol.Layout == null)
                {
                    ed.WriteMessage("\n求解失败：" + (sol.Error != null ? sol.Error.Message : "未知"));
                    return;
                }

                var layer = UserLayerNameResolver.Get(LayerSemanticIds.HyfeaResult, LayerBuiltinDefaults.HyfeaResult);
                var opt = new BeamRenderOptions(
                    layer,
                    DeflectionScale,
                    DrawDeflection,
                    DrawMoment,
                    DrawShear,
                    DrawReactions,
                    Units);

                BeamResultRenderer.Render(doc, axis, sol, opt);

                double du = MaxTranslationalDispFromUFull(sol);
                double mMax = MaxMomentMagnitude(sol);
                string lenUnit = UseSiUnits ? "m" : "mm";
                ed.WriteMessage("\n[HYFEA] max(|ux|,|uy|) ≈ " + FormatDisp(du) + " " + lenUnit);
                ed.WriteMessage($"\n[HYFEA] |M|_max ≈ {mMax:E3} N·{lenUnit}");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[HYFEA] 异常：{ex.Message}");
            }
        }

        private static int CoerceSegments(int s, Editor ed)
        {
            if (s >= 1 && s <= 10000)
                return s;
            int fix = s < 1 ? 4 : 10000;
            ed.WriteMessage($"\n[HYFEA] Segments={s} 非法，已用 {fix}。");
            return fix;
        }

        private static double CoercePositiveFinite(double v, double fallback, string label, Editor ed)
        {
            if (double.IsNaN(v) || double.IsInfinity(v) || v <= 0)
            {
                ed.WriteMessage($"\n[HYFEA] {label} 无效（≤0 或非数），已用默认值 {fallback:g9}。");
                return fallback;
            }

            return v;
        }

        /// <summary>
        /// 用 <see cref="FemResult.UFull"/> + <see cref="DofLayout"/> 扫描平动分量。
        /// 避免仅依赖 <see cref="NodalValueField"/> 字典键名（Node/Dof）在部分运行时下与其它路径不一致导致恒为 0。
        /// </summary>
        private static double MaxTranslationalDispFromUFull(FemResult sol)
        {
            if (sol.UFull == null || sol.Layout == null)
                return 0;
            var u = sol.UFull;
            var layout = sol.Layout;
            double m = 0;
            for (int g = 0; g < layout.TotalDofCount; g++)
            {
                var dof = layout.GetPair(g).dof;
                if (dof != DofType.UX && dof != DofType.UY)
                    continue;
                double v = u[g];
                double av = Math.Abs(v);
                if (av > m) m = av;
            }

            return m;
        }

        private static string FormatDisp(double v)
        {
            double av = Math.Abs(v);
            if (av == 0) return "0";
            if (av < 1e-3 || av >= 1e7)
                return v.ToString("G6");
            return v.ToString("0.###");
        }

        private static double MaxMomentMagnitude(FemResult sol)
        {
            if (sol.BeamEndForces == null) return 0;
            double max = 0;
            foreach (var kv in sol.BeamEndForces.AsReadOnly())
            {
                var v = kv.Value;
                max = Math.Max(max, Math.Abs(v.MA));
                max = Math.Max(max, Math.Abs(v.MB));
            }

            return max;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
