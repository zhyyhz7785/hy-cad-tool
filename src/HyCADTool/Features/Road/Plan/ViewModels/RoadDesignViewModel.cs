using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyCADTool.Features.Road;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.App.Bootstrap;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Presentation.ViewModels
{
    /// <summary>
    /// 道路设计 ViewModel（P0 占位）。
    ///
    /// 职责（P0）：
    /// - 作为 HyToolPanel.xaml "道路" Tab 的新增区域（市政道路设计）的数据上下文；
    /// - 提供 P0 阶段的 7 个命令按钮绑定（Alignment / Profile / Template / Corridor / Gltf / OpenJson / ImportJson）；
    /// - 展示当前文档 RoadDesign 的基本计数（诊断）。
    ///
    /// 后续扩展（P1~P5）：
    /// - 增加参数面板（目标映射、超高、代码检测开关）；
    /// - 订阅 <c>IRoadEventBus</c>，实时更新计数；
    /// - 面板写盘状态：v1.1 起命令收尾同步写盘，由命令直接在 Editor 打印结果，面板可另行订阅事件总线扩展刷新。
    /// </summary>
    public class RoadDesignViewModel : INotifyPropertyChanged
    {
        public RoadDesignViewModel()
        {
            CmdAlignment = new RelayCommand(() => SendCommand(() => new RoadAlignmentCommand().Execute()));
            CmdProfile = new RelayCommand(() => SendCommand(() => new RoadProfileCommand().Execute()));
            // hyRoadCs（v2 横断面绘制）替换 hyRoadT；统一面板的 "横断面" 按钮直接走新命令，避免触发 [Obsolete] 转发壳。
            CmdTemplate = new RelayCommand(() => SendCommand(() => new RoadCrossSectionDrawCommand().Execute()));
            CmdCorridor = new RelayCommand(() => SendCommand(() => new RoadCorridorCommand().Execute()));
            CmdExportGltf = new RelayCommand(() => SendCommand(() => new Road3dExportGltfCommand().Execute()));
            CmdOpenJson = new RelayCommand(() => SendCommand(() => new RoadOpenJsonCommand().Execute()));
            CmdImportJson = new RelayCommand(() => SendCommand(() => new RoadImportJsonCommand().Execute()));
            CmdRefreshCounts = new RelayCommand(RefreshCounts);

            RefreshCounts();
        }

        // ===== 命令 =====

        public ICommand CmdAlignment { get; }
        public ICommand CmdProfile { get; }
        public ICommand CmdTemplate { get; }
        public ICommand CmdCorridor { get; }
        public ICommand CmdExportGltf { get; }
        public ICommand CmdOpenJson { get; }
        public ICommand CmdImportJson { get; }
        public ICommand CmdRefreshCounts { get; }

        // ===== 诊断属性 =====

        private int _alignmentCount;
        public int AlignmentCount { get => _alignmentCount; set => SetProperty(ref _alignmentCount, value); }

        private int _templateCount;
        public int TemplateCount { get => _templateCount; set => SetProperty(ref _templateCount, value); }

        private int _corridorCount;
        public int CorridorCount { get => _corridorCount; set => SetProperty(ref _corridorCount, value); }

        private string _projectName;
        public string ProjectName { get => _projectName; set => SetProperty(ref _projectName, value); }

        private string _statusMessage;
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        public void RefreshCounts()
        {
            try
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                if (doc == null)
                {
                    ProjectName = "(无活动文档)";
                    AlignmentCount = TemplateCount = CorridorCount = 0;
                    return;
                }

                var registry = ServiceLocator.TryResolve<RoadDesignRegistry>();
                if (registry == null)
                {
                    StatusMessage = "RoadDesignRegistry 未注入，请先 NETLOAD。";
                    return;
                }

                // 只查不建：避免面板刷新时在 Registry 中留下空壳 design；
                // 空壳会扰乱 RebindForDocument 的 Guid 匹配，也会让 hyRoadSave 的 Registry snapshot 显示无谓的 "(empty)" 条目。
                if (!registry.TryGet(doc.Name, out var design))
                {
                    ProjectName = System.IO.Path.GetFileNameWithoutExtension(doc.Name) ?? "(未命名)";
                    AlignmentCount = TemplateCount = CorridorCount = 0;
                    StatusMessage = "当前文档尚未拾取任何道路对象（hyRoadA 开始）。";
                    return;
                }

                ProjectName = design.ProjectName ?? "(未命名)";
                AlignmentCount = design.Alignments.Count;
                TemplateCount = design.Templates.Count;
                CorridorCount = design.Corridors.Count;
                StatusMessage = $"Schema={design.Schema}, 最近修改 {design.LastModifiedUtc:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"刷新失败：{ex.Message}";
            }
        }

        // ===== 命令路由（与 SettingsPanelViewModel 一致：PendingCommand → _HyExec）=====

        private static void SendCommand(Action command)
        {
            SettingsPanelViewModel.PendingCommand = command;
            AcApp.DocumentManager.MdiActiveDocument?.SendStringToExecute("_HyExec\n", true, false, false);
        }

        // ===== INotifyPropertyChanged =====

        public event PropertyChangedEventHandler PropertyChanged;

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}
