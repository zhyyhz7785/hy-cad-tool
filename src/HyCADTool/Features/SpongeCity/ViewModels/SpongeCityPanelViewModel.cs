using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyCADTool.Features.SpongeCity.Domain.Models;
using HyCADTool.Features.SpongeCity.Domain.Services;
using HyCADTool.Presentation.ViewModels;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.SpongeCity.ViewModels
{
    /// <summary>
    /// 海绵城市面板 ViewModel。
    /// 多文档支持：每个 AutoCAD 文档独立一份 VM；UI 通过 <see cref="Current"/> 取当前活动文档实例。
    /// 包含：项目信息 + 设计控制目标 + 10 下垫面 + 8 设施 + 7 项校核结果 + 5 个面板按钮。
    /// </summary>
    public class SpongeCityPanelViewModel : INotifyPropertyChanged
    {
        #region 多文档支持

        private static readonly Dictionary<string, SpongeCityPanelViewModel> _documentViewModels
            = new Dictionary<string, SpongeCityPanelViewModel>();

        public static SpongeCityPanelViewModel Current
        {
            get
            {
                try
                {
                    var doc = AcApp.DocumentManager.MdiActiveDocument;
                    if (doc == null) return null;
                    var docName = doc.Name;
                    if (!_documentViewModels.ContainsKey(docName))
                        _documentViewModels[docName] = new SpongeCityPanelViewModel();
                    return _documentViewModels[docName];
                }
                catch { return null; }
            }
        }

        #endregion

        #region 构造

        public SpongeCityPanelViewModel()
        {
            Input = SpongeProjectInput.CreateDefault();
            Surfaces = new ObservableCollection<SurfaceItem>(Input.Surfaces);
            Facilities = new ObservableCollection<FacilityItem>(Input.Facilities);

            DrawLinesCommand = new RelayCommand(ExecuteDrawLines);
            ReadAreaCommand = new RelayCommand(ExecuteReadArea);
            WriteAreaCommand = new RelayCommand(ExecuteWriteArea);
            CalculateCommand = new RelayCommand(ExecuteCalculate);
            ExportCalcExcelCommand = new RelayCommand(ExecuteExportCalcExcel);
            ExportBudgetExcelCommand = new RelayCommand(ExecuteExportBudgetExcel);
            ExportMdReportCommand = new RelayCommand(ExecuteExportMdReport);
            ResetCommand = new RelayCommand(ExecuteReset);

            // 首次打开自动算一次，让校核区有内容
            ExecuteCalculate();
        }

        #endregion

        #region 模型 + 集合

        /// <summary>底层输入模型（与 Excel Sheet1 参数对应）。</summary>
        public SpongeProjectInput Input { get; private set; }

        /// <summary>10 类下垫面（与 Input.Surfaces 同步）。</summary>
        public ObservableCollection<SurfaceItem> Surfaces { get; private set; }

        /// <summary>8 类设施（与 Input.Facilities 同步）。</summary>
        public ObservableCollection<FacilityItem> Facilities { get; private set; }

        /// <summary>计算结果（每次 ExecuteCalculate 后更新）。</summary>
        public SpongeResult Result { get; private set; } = new SpongeResult();

        /// <summary>校核结果（绑定 DataGrid）。</summary>
        public ObservableCollection<CheckRow> Checks { get; } = new ObservableCollection<CheckRow>();

        #endregion

        #region 项目信息（双向绑定的简化代理）

        public string ProjectName
        {
            get => Input.ProjectName;
            set { Input.ProjectName = value ?? ""; OnPropertyChanged(); }
        }

        public string Location
        {
            get => Input.Location;
            set { Input.Location = value ?? ""; OnPropertyChanged(); }
        }

        /// <summary>地区下拉索引（0=北京 1=天津 2=河北）。</summary>
        public int RegionIndex
        {
            get => (int)Input.Region;
            set
            {
                if ((int)Input.Region == value) return;
                Input.Region = (SpongeRegion)value;
                OnPropertyChanged();
                ExecuteCalculate();
            }
        }

        /// <summary>用地性质索引（与 ProjectType 枚举顺序一致）。</summary>
        public int ProjectTypeIndex
        {
            get => (int)Input.ProjectType;
            set
            {
                if ((int)Input.ProjectType == value) return;
                Input.ProjectType = (ProjectType)value;
                OnPropertyChanged();
                ExecuteCalculate();
            }
        }

        public double TotalAreaM2
        {
            get => Input.TotalAreaM2;
            set { Input.TotalAreaM2 = value; OnPropertyChanged(); ExecuteCalculate(); }
        }

        public double BuildingFootprintM2
        {
            get => Input.BuildingFootprintM2;
            set { Input.BuildingFootprintM2 = value; OnPropertyChanged(); ExecuteCalculate(); }
        }

        public double GreenRatio
        {
            get => Input.GreenRatio;
            set { Input.GreenRatio = value; OnPropertyChanged(); ExecuteCalculate(); }
        }

        public double AlphaTarget
        {
            get => Input.AlphaTarget;
            set { Input.AlphaTarget = value; OnPropertyChanged(); ExecuteCalculate(); }
        }

        public double DesignRainfallMm
        {
            get => Input.DesignRainfallMm;
            set { Input.DesignRainfallMm = value; OnPropertyChanged(); ExecuteCalculate(); }
        }

        public double EtaSsTarget
        {
            get => Input.EtaSsTarget;
            set { Input.EtaSsTarget = value; OnPropertyChanged(); ExecuteCalculate(); }
        }

        public double InitialDiscardMm
        {
            get => Input.InitialDiscardMm;
            set { Input.InitialDiscardMm = value; OnPropertyChanged(); }
        }

        public double UnitSpongeCost
        {
            get => Input.UnitSpongeCost;
            set { Input.UnitSpongeCost = value; OnPropertyChanged(); ExecuteCalculate(); }
        }

        #endregion

        #region 命令

        public ICommand DrawLinesCommand { get; }
        public ICommand ReadAreaCommand { get; }
        public ICommand WriteAreaCommand { get; }
        public ICommand CalculateCommand { get; }
        public ICommand ExportCalcExcelCommand { get; }
        public ICommand ExportBudgetExcelCommand { get; }
        public ICommand ExportMdReportCommand { get; }
        public ICommand ResetCommand { get; }

        #endregion

        #region 状态与结果摘要

        private string _statusMessage = "就绪。点击「画样线 hyscDL」开始。";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string ResultSummary
        {
            get
            {
                if (Result == null) return "—";
                return $"ψ_z = {Result.PsiZ:F3} | W_设计 = {Result.FinalRequiredVolume:F1} m³ | " +
                       $"V_实 = {Result.ProvidedVolume:F1} m³ | η_年 = {Result.EtaAnnual:P1} | " +
                       (Result.AllPassed ? "全部达标 ✓" : "存在不达标 ✗");
            }
        }

        #endregion

        #region CAD 联动缓存

        /// <summary>每个下垫面图层的「样线 a」端点（hyscDL 写入；hyscWA 读取定位合计文字）。</summary>
        public Dictionary<string, Autodesk.AutoCAD.Geometry.Point3d> AnchorPoints { get; }
            = new Dictionary<string, Autodesk.AutoCAD.Geometry.Point3d>(StringComparer.Ordinal);

        /// <summary>每个图层下扫描到的 Polyline {ObjectId, 形心, 面积} 列表（hyscRA 写入；hyscWA 读取）。</summary>
        public Dictionary<string, List<PolylineRecord>> LayerPolylines { get; }
            = new Dictionary<string, List<PolylineRecord>>(StringComparer.Ordinal);

        public class PolylineRecord
        {
            public Autodesk.AutoCAD.DatabaseServices.ObjectId Id;
            public Autodesk.AutoCAD.Geometry.Point3d Centroid;
            public double Area;
        }

        #endregion

        #region 命令实现

        /// <summary>同步 ObservableCollection 到 Input.Surfaces / Facilities，再触发计算。</summary>
        private void SyncInputFromCollections()
        {
            Input.Surfaces.Clear();
            foreach (var s in Surfaces) Input.Surfaces.Add(s);
            Input.Facilities.Clear();
            foreach (var f in Facilities) Input.Facilities.Add(f);
        }

        public void ExecuteCalculate()
        {
            try
            {
                SyncInputFromCollections();
                Result = SpongeCalculationService.Compute(Input);
                Checks.Clear();
                foreach (var c in Result.Checks) Checks.Add(c);

                OnPropertyChanged(nameof(Result));
                OnPropertyChanged(nameof(ResultSummary));
            }
            catch (System.Exception ex)
            {
                StatusMessage = "计算失败：" + ex.Message;
            }
        }

        private void ExecuteDrawLines()
        {
            RunCadCommand("HyCADTool.Features.SpongeCity.Commands.DrawSurfaceLayerLinesCommand", "Execute");
        }

        private void ExecuteReadArea()
        {
            RunCadCommand("HyCADTool.Features.SpongeCity.Commands.ReadSurfaceAreaCommand", "Execute");
        }

        private void ExecuteWriteArea()
        {
            RunCadCommand("HyCADTool.Features.SpongeCity.Commands.WriteSurfaceAreaCommand", "Execute");
        }

        private void ExecuteExportCalcExcel()
        {
            RunCadCommand("HyCADTool.Features.SpongeCity.Commands.ExportCalcExcelCommand", "Execute");
        }

        private void ExecuteExportBudgetExcel()
        {
            RunCadCommand("HyCADTool.Features.SpongeCity.Commands.ExportBudgetExcelCommand", "Execute");
        }

        private void ExecuteExportMdReport()
        {
            RunCadCommand("HyCADTool.Features.SpongeCity.Commands.ExportReportMdCommand", "Execute");
        }

        private void ExecuteReset()
        {
            Input = SpongeProjectInput.CreateDefault();
            Surfaces.Clear();
            foreach (var s in Input.Surfaces) Surfaces.Add(s);
            Facilities.Clear();
            foreach (var f in Input.Facilities) Facilities.Add(f);
            // 触发所有 PropertyChanged
            OnPropertyChanged(string.Empty);
            ExecuteCalculate();
            StatusMessage = "已恢复默认参数。";
        }

        /// <summary>
        /// 反射实例化 Command 类并调用 Execute（避免面板 ViewModel 直接 using AutoCAD API，
        /// 也便于以后命令拆出去而 VM 不动）。
        /// </summary>
        private void RunCadCommand(string typeFullName, string method)
        {
            try
            {
                var type = Type.GetType(typeFullName + ", HyCADTool");
                if (type == null)
                {
                    StatusMessage = "未找到命令：" + typeFullName;
                    return;
                }
                var instance = Activator.CreateInstance(type);
                var mi = type.GetMethod(method);
                if (mi == null)
                {
                    StatusMessage = "未找到方法：" + method;
                    return;
                }
                mi.Invoke(instance, null);
            }
            catch (System.Exception ex)
            {
                var inner = ex.InnerException ?? ex;
                StatusMessage = "执行失败：" + inner.Message;
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }

        #endregion
    }
}
