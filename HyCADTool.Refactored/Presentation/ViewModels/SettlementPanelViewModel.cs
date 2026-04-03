using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyCADTool.Refactored.Domain.Models.Settlement;
using HyCADTool.Refactored.Domain.Services;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.ViewModels
{
    /// <summary>
    /// 沉降计算面板 ViewModel（自含式，参照 PilePanelViewModel 模式）
    /// 参数自管理 + JSON 持久化，通过 SendCommand → C1 路由到 AutoCAD 命令线程
    /// </summary>
    public class SettlementPanelViewModel : INotifyPropertyChanged
    {
        #region 多文档支持

        private static readonly Dictionary<string, SettlementPanelViewModel> _documentViewModels
            = new Dictionary<string, SettlementPanelViewModel>();

        public static SettlementPanelViewModel Current
        {
            get
            {
                try
                {
                    var doc = AcApp.DocumentManager.MdiActiveDocument;
                    if (doc == null) return null;
                    var docName = doc.Name;
                    if (!_documentViewModels.ContainsKey(docName))
                        _documentViewModels[docName] = new SettlementPanelViewModel();
                    return _documentViewModels[docName];
                }
                catch { return null; }
            }
        }

        public static SettlementPanelViewModel GetOrCreate(string documentName)
        {
            if (!_documentViewModels.ContainsKey(documentName))
                _documentViewModels[documentName] = new SettlementPanelViewModel();
            return _documentViewModels[documentName];
        }

        public static void RemoveDocument(string documentName)
        {
            _documentViewModels.Remove(documentName);
        }

        #endregion

        #region 构造函数

        public SettlementPanelViewModel()
        {
            ParseTableCommand = new RelayCommand(ExecuteParseTable);
            CalculateCommand = new RelayCommand(ExecuteCalculate);
            DrawTableCommand = new RelayCommand(ExecuteDrawTable);
            ResetCommand = new RelayCommand(ExecuteReset);
            LoadSettings();
        }

        #endregion

        #region 只读属性

        public double CurrentScale => SettingsPanelViewModel.Current?.Scale ?? 40.0;

        #endregion

        #region 高程参数

        private double _boreholeElevation;
        /// <summary>孔点高程 (m)</summary>
        public double BoreholeElevation
        {
            get => _boreholeElevation;
            set
            {
                if (SetProperty(ref _boreholeElevation, value))
                    OnPropertyChanged(nameof(RelativeElevation));
            }
        }

        private double _structureZeroElevation;
        /// <summary>结构正负零高程 (m)</summary>
        public double StructureZeroElevation
        {
            get => _structureZeroElevation;
            set
            {
                if (SetProperty(ref _structureZeroElevation, value))
                    OnPropertyChanged(nameof(RelativeElevation));
            }
        }

        private double _foundationDepth = 1.5;
        /// <summary>基础埋置深度 (m)</summary>
        public double FoundationDepth
        {
            get => _foundationDepth;
            set => SetProperty(ref _foundationDepth, value);
        }

        /// <summary>相对高程 (m) = 孔点高程 - 结构正负零高程</summary>
        public double RelativeElevation => BoreholeElevation - StructureZeroElevation;

        #endregion

        #region 基础参数

        private int _foundationTypeIndex;
        /// <summary>计算类型索引：0=天然, 1=复合, 2=桩基</summary>
        public int FoundationTypeIndex
        {
            get => _foundationTypeIndex;
            set
            {
                if (SetProperty(ref _foundationTypeIndex, value))
                {
                    OnPropertyChanged(nameof(IsCompositeVisible));
                    OnPropertyChanged(nameof(IsPileVisible));
                }
            }
        }

        public bool IsCompositeVisible => FoundationTypeIndex == 1;
        public bool IsPileVisible => FoundationTypeIndex == 2;

        private double _foundationLength = 3.0;
        /// <summary>基础长度 l (m)</summary>
        public double FoundationLength
        {
            get => _foundationLength;
            set => SetProperty(ref _foundationLength, value);
        }

        private double _foundationWidth = 2.0;
        /// <summary>基础宽度 b (m)</summary>
        public double FoundationWidth
        {
            get => _foundationWidth;
            set => SetProperty(ref _foundationWidth, value);
        }

        private double _additionalPressure = 100.0;
        /// <summary>基底附加压力 p₀ (kPa)</summary>
        public double AdditionalPressure
        {
            get => _additionalPressure;
            set => SetProperty(ref _additionalPressure, value);
        }

        private double _bearingCapacity = 150.0;
        /// <summary>地基承载力特征值 fak (kPa)</summary>
        public double BearingCapacity
        {
            get => _bearingCapacity;
            set => SetProperty(ref _bearingCapacity, value);
        }

        #endregion

        #region 复合地基参数

        private double _compositeBearingCapacity = 250.0;
        /// <summary>复合地基承载力 fspk (kPa)</summary>
        public double CompositeBearingCapacity
        {
            get => _compositeBearingCapacity;
            set => SetProperty(ref _compositeBearingCapacity, value);
        }

        private double _treatedDepth = 10.0;
        /// <summary>加固层深度 (m)</summary>
        public double TreatedDepth
        {
            get => _treatedDepth;
            set => SetProperty(ref _treatedDepth, value);
        }

        #endregion

        #region 桩基参数

        private double _pileLength = 15.0;
        public double PileLength
        {
            get => _pileLength;
            set => SetProperty(ref _pileLength, value);
        }

        private double _pileDiameter = 0.5;
        public double PileDiameter
        {
            get => _pileDiameter;
            set => SetProperty(ref _pileDiameter, value);
        }

        private double _pileSpacing = 3.0;
        public double PileSpacing
        {
            get => _pileSpacing;
            set => SetProperty(ref _pileSpacing, value);
        }

        private double _capLength = 6.0;
        public double CapLength
        {
            get => _capLength;
            set => SetProperty(ref _capLength, value);
        }

        private double _capWidth = 4.0;
        public double CapWidth
        {
            get => _capWidth;
            set => SetProperty(ref _capWidth, value);
        }

        private int _totalPileCount = 9;
        public int TotalPileCount
        {
            get => _totalPileCount;
            set => SetProperty(ref _totalPileCount, value);
        }

        #endregion

        #region 地层数据

        private string _soilLayerMarkdown = "";
        /// <summary>原始 Markdown 表格文本（持久化）</summary>
        public string SoilLayerMarkdown
        {
            get => _soilLayerMarkdown;
            set => SetProperty(ref _soilLayerMarkdown, value);
        }

        private ObservableCollection<SoilLayer> _parsedSoilLayers = new ObservableCollection<SoilLayer>();
        /// <summary>解析后的土层列表（DataGrid 绑定）</summary>
        public ObservableCollection<SoilLayer> ParsedSoilLayers
        {
            get => _parsedSoilLayers;
            set => SetProperty(ref _parsedSoilLayers, value);
        }

        #endregion

        #region 计算结果

        private ObservableCollection<SettlementLayerResult> _calculationResults
            = new ObservableCollection<SettlementLayerResult>();
        public ObservableCollection<SettlementLayerResult> CalculationResults
        {
            get => _calculationResults;
            set => SetProperty(ref _calculationResults, value);
        }

        private string _resultSummary = "";
        /// <summary>结果汇总文本（s', ψ_s, s 等）</summary>
        public string ResultSummary
        {
            get => _resultSummary;
            set => SetProperty(ref _resultSummary, value);
        }

        /// <summary>最近一次的完整计算结果（供 Command 绘制表格用）</summary>
        public SettlementResult LastResult { get; private set; }

        #endregion

        #region 状态

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        #endregion

        #region 命令

        public ICommand ParseTableCommand { get; }
        public ICommand CalculateCommand { get; }
        public ICommand DrawTableCommand { get; }
        public ICommand ResetCommand { get; }

        #endregion

        #region 命令实现

        private void ExecuteParseTable()
        {
            try
            {
                var layers = MarkdownTableParser.Parse(SoilLayerMarkdown);
                ParsedSoilLayers = new ObservableCollection<SoilLayer>(layers);
                StatusMessage = $"解析成功：{layers.Count} 层";
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"解析失败: {ex.Message}";
            }
        }

        private void ExecuteCalculate()
        {
            try
            {
                if (!ParsedSoilLayers.Any())
                {
                    StatusMessage = "请先解析地层数据";
                    return;
                }

                var input = BuildInput();
                var result = SettlementCalculationService.Calculate(input);
                LastResult = result;

                if (!result.Success)
                {
                    StatusMessage = result.Message;
                    return;
                }

                CalculationResults = new ObservableCollection<SettlementLayerResult>(result.LayerResults);

                var type = new[] { "天然基础", "复合地基", "桩基础" }[FoundationTypeIndex];
                ResultSummary = $"【{type}】\n"
                    + $"理论沉降 s' = {result.TheoreticalSettlement:F2} mm\n"
                    + $"当量模量 Ēs = {result.EquivalentEs:F1} MPa\n"
                    + $"经验系数 ψs = {result.PsiS:F3}\n"
                    + (result.PsiE < 1.0 ? $"等效系数 ψe = {result.PsiE:F3}\n" : "")
                    + $"最终沉降 s = {result.FinalSettlement:F2} mm\n"
                    + $"计算深度 zn = {result.CalculationDepth:F1} m";

                StatusMessage = $"计算完成：最终沉降 {result.FinalSettlement:F2} mm";
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"计算失败: {ex.Message}";
            }
        }

        private void ExecuteDrawTable()
        {
            if (LastResult == null || !LastResult.Success)
            {
                StatusMessage = "请先执行计算";
                return;
            }
            SendCommand(() => new Commands.SettlementCalculationCommand().Execute());
        }

        private void ExecuteReset()
        {
            _isLoading = true;
            try
            {
                BoreholeElevation = 0;
                StructureZeroElevation = 0;
                FoundationDepth = 1.5;
                FoundationTypeIndex = 0;
                FoundationLength = 3.0;
                FoundationWidth = 2.0;
                AdditionalPressure = 100.0;
                BearingCapacity = 150.0;
                CompositeBearingCapacity = 250.0;
                TreatedDepth = 10.0;
                PileLength = 15.0;
                PileDiameter = 0.5;
                PileSpacing = 3.0;
                CapLength = 6.0;
                CapWidth = 4.0;
                TotalPileCount = 9;
                SoilLayerMarkdown = "";
                ParsedSoilLayers = new ObservableCollection<SoilLayer>();
                CalculationResults = new ObservableCollection<SettlementLayerResult>();
                ResultSummary = "";
                StatusMessage = "已恢复默认值";
            }
            finally
            {
                _isLoading = false;
            }
            SaveSettings();
        }

        private SettlementInput BuildInput()
        {
            return new SettlementInput
            {
                BoreholeElevation = BoreholeElevation,
                StructureZeroElevation = StructureZeroElevation,
                FoundationDepth = FoundationDepth,
                Type = (FoundationType)FoundationTypeIndex,
                FoundationLength = FoundationLength,
                FoundationWidth = FoundationWidth,
                AdditionalPressure = AdditionalPressure,
                BearingCapacity = BearingCapacity,
                CompositeBearingCapacity = CompositeBearingCapacity,
                TreatedDepth = TreatedDepth,
                PileLength = PileLength,
                PileDiameter = PileDiameter,
                PileSpacing = PileSpacing,
                CapLength = CapLength,
                CapWidth = CapWidth,
                TotalPileCount = TotalPileCount,
                SoilLayers = ParsedSoilLayers.ToList()
            };
        }

        #endregion

        #region 命令路由

        private void SendCommand(Action commandAction)
        {
            SaveSettings();
            SettingsPanelViewModel.PendingCommand = () =>
            {
                var settingsVm = SettingsPanelViewModel.Current;
                settingsVm?.EnsureStylesApplied();
                commandAction();
            };
            try
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                if (doc == null)
                {
                    StatusMessage = "无活动文档";
                    SettingsPanelViewModel.PendingCommand = null;
                    return;
                }
                doc.SendStringToExecute("C1\n", true, false, false);
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"发送命令失败: {ex.Message}";
                SettingsPanelViewModel.PendingCommand = null;
            }
        }

        #endregion

        #region 持久化：hy-settlement-settings.json

        private static string _settingsFilePath;

        private static string GetSettingsFilePath()
        {
            if (_settingsFilePath != null) return _settingsFilePath;
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HyCADTool");
            Directory.CreateDirectory(appDataDir);
            _settingsFilePath = Path.Combine(appDataDir, "hy-settlement-settings.json");
            return _settingsFilePath;
        }

        public void SaveSettings()
        {
            if (_isLoading) return;
            try
            {
                var data = new SettlementSettingsData
                {
                    BoreholeElevation = BoreholeElevation,
                    StructureZeroElevation = StructureZeroElevation,
                    FoundationDepth = FoundationDepth,
                    FoundationTypeIndex = FoundationTypeIndex,
                    FoundationLength = FoundationLength,
                    FoundationWidth = FoundationWidth,
                    AdditionalPressure = AdditionalPressure,
                    BearingCapacity = BearingCapacity,
                    CompositeBearingCapacity = CompositeBearingCapacity,
                    TreatedDepth = TreatedDepth,
                    PileLength = PileLength,
                    PileDiameter = PileDiameter,
                    PileSpacing = PileSpacing,
                    CapLength = CapLength,
                    CapWidth = CapWidth,
                    TotalPileCount = TotalPileCount,
                    SoilLayerMarkdown = SoilLayerMarkdown
                };

                var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(GetSettingsFilePath(), json);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存沉降设置失败: {ex.Message}");
            }
        }

        public void LoadSettings()
        {
            _isLoading = true;
            try
            {
                var path = GetSettingsFilePath();
                if (!File.Exists(path)) return;

                var json = File.ReadAllText(path);
                var data = JsonConvert.DeserializeObject<SettlementSettingsData>(json);
                if (data == null) return;

                BoreholeElevation = data.BoreholeElevation;
                StructureZeroElevation = data.StructureZeroElevation;
                FoundationDepth = data.FoundationDepth;
                FoundationTypeIndex = data.FoundationTypeIndex;
                FoundationLength = data.FoundationLength;
                FoundationWidth = data.FoundationWidth;
                AdditionalPressure = data.AdditionalPressure;
                BearingCapacity = data.BearingCapacity;
                CompositeBearingCapacity = data.CompositeBearingCapacity;
                TreatedDepth = data.TreatedDepth;
                PileLength = data.PileLength;
                PileDiameter = data.PileDiameter;
                PileSpacing = data.PileSpacing;
                CapLength = data.CapLength;
                CapWidth = data.CapWidth;
                TotalPileCount = data.TotalPileCount;
                SoilLayerMarkdown = data.SoilLayerMarkdown ?? "";

                // 自动解析已保存的 Markdown 表格
                if (!string.IsNullOrWhiteSpace(SoilLayerMarkdown))
                {
                    var layers = MarkdownTableParser.Parse(SoilLayerMarkdown);
                    ParsedSoilLayers = new ObservableCollection<SoilLayer>(layers);
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载沉降设置失败: {ex.Message}");
            }
            finally
            {
                _isLoading = false;
            }
        }

        private class SettlementSettingsData
        {
            public double BoreholeElevation { get; set; }
            public double StructureZeroElevation { get; set; }
            public double FoundationDepth { get; set; } = 1.5;
            public int FoundationTypeIndex { get; set; }
            public double FoundationLength { get; set; } = 3.0;
            public double FoundationWidth { get; set; } = 2.0;
            public double AdditionalPressure { get; set; } = 100.0;
            public double BearingCapacity { get; set; } = 150.0;
            public double CompositeBearingCapacity { get; set; } = 250.0;
            public double TreatedDepth { get; set; } = 10.0;
            public double PileLength { get; set; } = 15.0;
            public double PileDiameter { get; set; } = 0.5;
            public double PileSpacing { get; set; } = 3.0;
            public double CapLength { get; set; } = 6.0;
            public double CapWidth { get; set; } = 4.0;
            public int TotalPileCount { get; set; } = 9;
            public string SoilLayerMarkdown { get; set; } = "";
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        private bool _isLoading;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            if (!_isLoading && propertyName != nameof(StatusMessage)
                            && propertyName != nameof(ResultSummary)
                            && propertyName != nameof(CalculationResults))
                SaveSettings();
            return true;
        }

        #endregion
    }
}
