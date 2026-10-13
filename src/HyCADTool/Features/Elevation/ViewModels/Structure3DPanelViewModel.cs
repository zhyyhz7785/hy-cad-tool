using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyCADTool.Shared.AutoCAD.Session;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using HyCADTool.Shared.Host;
using HyCADTool.Shared.UI;

namespace HyCADTool.Features.Elevation.ViewModels
{
    /// <summary>
    /// 「3D结构」面板 ViewModel。
    ///
    /// 工作流（三步）：
    /// 1. 整理底图（HYDCEL）：从选定曲线构建 DCEL 图，生成 dcelOuter / dcelInner 闭合多段线；
    /// 2. 生成 3D 模型（HY3）：选 dcel 多边形 + 标高文本，按面板墙厚 / 筏板厚生成 Solid3d；
    /// 3. 生成剖面（旧 hy3C_CreateSection）：选剖切线 + 3D 实体，生成 2D 剖面设计图
    ///    （含剖切符号与「N-N 剖面图」标签）。
    ///
    /// 注意：各步都涉及 AutoCAD 选集 / 写库，必须经
    /// <see cref="IHostSettings.PendingCommand"/> + _HyExec 路由到命令上下文执行
    /// （modeless 面板直接写库会 eLockViolation，参照 G101PanelViewModel 同款做法）。
    /// </summary>
    public class Structure3DPanelViewModel : INotifyPropertyChanged
    {
        /// <summary>按活动文档隔离的当前 ViewModel（Database 为键，另存为后仍是同一槽）。</summary>
        public static Structure3DPanelViewModel Current
        {
            get { return ActiveDocumentSession.GetOrNull<Structure3DPanelViewModel>(); }
        }

        /// <summary>文档关闭时由 <see cref="DocumentSessionStore.Forget"/> 统一释放。</summary>
        public static void RemoveDocument(object documentKey)
        {
        }

        public Structure3DPanelViewModel()
        {
            StepOneCommand = new RelayCommand(ExecuteStepOne);
            StepTwoCommand = new RelayCommand(ExecuteStepTwo);
            StepThreeCommand = new RelayCommand(ExecuteStepThree);
        }

        #region 参数属性

        private string _wallThicknessText = "350";
        private string _raftThicknessText = "500";
        private string _sectionDisplacementText = "12000";
        private string _statusMessage = string.Empty;

        /// <summary>墙体厚度（实际 mm，默认 350）。</summary>
        public string WallThicknessText
        {
            get => _wallThicknessText;
            set { if (_wallThicknessText == value) return; _wallThicknessText = value; OnPropertyChanged(); }
        }

        /// <summary>筏板厚度（实际 mm，默认 500）。</summary>
        public string RaftThicknessText
        {
            get => _raftThicknessText;
            set { if (_raftThicknessText == value) return; _raftThicknessText = value; OnPropertyChanged(); }
        }

        /// <summary>剖面图排布间距（mm，默认 12000）。</summary>
        public string SectionDisplacementText
        {
            get => _sectionDisplacementText;
            set { if (_sectionDisplacementText == value) return; _sectionDisplacementText = value; OnPropertyChanged(); }
        }

        /// <summary>状态栏消息。</summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set { if (_statusMessage == value) return; _statusMessage = value; OnPropertyChanged(); }
        }

        #endregion

        #region 命令

        /// <summary>Step1：整理底图（HYDCEL，生成 dcelOuter / dcelInner）。</summary>
        public ICommand StepOneCommand { get; }

        /// <summary>Step2：生成 3D 模型（HY3，按面板墙厚 / 筏板厚）。</summary>
        public ICommand StepTwoCommand { get; }

        /// <summary>Step3：生成剖面设计图（选剖切线 + 3D 实体）。</summary>
        public ICommand StepThreeCommand { get; }

        private void ExecuteStepOne()
        {
            StatusMessage = "整理底图：请按命令行提示选择曲线…";
            SendCommand("整理底图", () =>
            {
                var active = AcApp.DocumentManager.MdiActiveDocument;
                active?.SendStringToExecute("HYDCEL\n", true, false, false);
            }, "HYDCEL");
        }

        private void ExecuteStepTwo()
        {
            if (!TryParsePositive(WallThicknessText, out double wall)
                || !Domain.ValueObjects.WallThickness.TryCreate(wall, out _))
            {
                StatusMessage = "墙厚无效：请输入 50–3000（mm）。";
                return;
            }
            if (!TryParsePositive(RaftThicknessText, out double raft)
                || !Domain.ValueObjects.SlabThickness.TryCreate(raft, out _))
            {
                StatusMessage = "筏板厚无效：请输入 100–2000（mm）。";
                return;
            }

            StatusMessage = $"生成 3D：墙厚 {wall:F0}mm，筏板厚 {raft:F0}mm…";
            SendCommand("生成 3D", () => new Elevation3DCommand(wall, raft).Execute(), "HY3");
        }

        private void ExecuteStepThree()
        {
            if (!TryParsePositive(SectionDisplacementText, out double displacement))
            {
                StatusMessage = "剖面位移无效：请输入正数（mm）。";
                return;
            }

            // 剖切符号 / 标签比例取设置面板出图比例，无则 50
            double scale = CadHost.Settings?.Scale ?? 50;
            if (scale <= 0) scale = 50;

            StatusMessage = $"生成剖面：位移 {displacement:F0}mm，比例 {scale:F0}…";
            SendCommand("生成剖面", () => new CreateSectionCommand(displacement, scale).Execute(), "N14");
        }

        private static bool TryParsePositive(string text, out double value)
        {
            return double.TryParse(text, out value) && value > 0;
        }

        #endregion

        #region 命令路由（_HyExec）

        /// <summary>
        /// 通过 PendingCommand → _HyExec 路由到 AutoCAD 命令上下文执行；
        /// 命令上下文在 UI 线程，完成 / 失败后直接回写 <see cref="StatusMessage"/>。
        /// </summary>
        private void SendCommand(string stepName, Action commandAction, string commandKey)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                StatusMessage = "无活动文档";
                return;
            }

            CadHost.Commands.EnqueuePendingCommand(() =>
            {
                try
                {
                    commandAction();
                    StatusMessage = stepName + "：已执行完毕。";
                }
                catch (System.Exception ex)
                {
                    StatusMessage = stepName + " 失败：" + ex.Message;
                    throw;
                }
            }, commandKey);
            try
            {
                doc.SendStringToExecute("_HyExec\n", true, false, false);
            }
            catch (System.Exception ex)
            {
                CadHost.Commands.ClearPendingCommand();
                StatusMessage = "发送命令失败：" + ex.Message;
            }
        }

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
