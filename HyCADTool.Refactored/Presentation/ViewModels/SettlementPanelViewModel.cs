using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using HyCADTool.Refactored.Domain.Models.Settlement;
using HyCADTool.Refactored.Domain.Services;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.ViewModels
{
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
            AddLayerCommand = new RelayCommand(ExecuteAddLayer);
            DeleteLayerCommand = new RelayCommand(ExecuteDeleteLayer);
            MoveLayerUpCommand = new RelayCommand(ExecuteMoveLayerUp);
            MoveLayerDownCommand = new RelayCommand(ExecuteMoveLayerDown);
            GenerateReportCommand = new RelayCommand(ExecuteGenerateReport);
            CopyReportCommand = new RelayCommand(ExecuteCopyReport);
            LoadSettings();
        }

        #endregion

        #region 只读属性

        public double CurrentScale => SettingsPanelViewModel.Current?.Scale ?? 40.0;

        /// <summary>复合地基模量提高系数 ξ = fspk / fak</summary>
        public double CompositeXi => BearingCapacity > 0 ? CompositeBearingCapacity / BearingCapacity : 0;

        #endregion

        #region 高程参数

        private bool _useAbsoluteElevation;
        public bool UseAbsoluteElevation
        {
            get => _useAbsoluteElevation;
            set
            {
                if (SetProperty(ref _useAbsoluteElevation, value))
                {
                    OnPropertyChanged(nameof(IsRelativeMode));
                    if (value)
                    {
                        _relBoreholeElevation = Math.Round(_absBoreholeElevation - _absStructureZero, 3);
                        OnPropertyChanged(nameof(RelBoreholeElevation));
                    }
                    else
                    {
                        _absBoreholeElevation = 0;
                        _absStructureZero = 0;
                        OnPropertyChanged(nameof(AbsBoreholeElevation));
                        OnPropertyChanged(nameof(AbsStructureZero));
                        OnPropertyChanged(nameof(AbsFoundationDepthElevation));
                    }
                    OnPropertyChanged(nameof(RelativeElevation));
                }
            }
        }

        public bool IsRelativeMode => !UseAbsoluteElevation;

        private double _absBoreholeElevation;
        public double AbsBoreholeElevation
        {
            get => _absBoreholeElevation;
            set
            {
                if (SetProperty(ref _absBoreholeElevation, value))
                {
                    if (UseAbsoluteElevation)
                    {
                        _relBoreholeElevation = Math.Round(_absBoreholeElevation - _absStructureZero, 3);
                        OnPropertyChanged(nameof(RelBoreholeElevation));
                    }
                    OnPropertyChanged(nameof(RelativeElevation));
                }
            }
        }

        private double _absStructureZero;
        public double AbsStructureZero
        {
            get => _absStructureZero;
            set
            {
                if (SetProperty(ref _absStructureZero, value))
                {
                    if (UseAbsoluteElevation)
                    {
                        _relBoreholeElevation = Math.Round(_absBoreholeElevation - _absStructureZero, 3);
                        OnPropertyChanged(nameof(RelBoreholeElevation));
                    }
                    OnPropertyChanged(nameof(RelativeElevation));
                    OnPropertyChanged(nameof(AbsFoundationDepthElevation));
                }
            }
        }

        private double _relBoreholeElevation;
        public double RelBoreholeElevation
        {
            get => _relBoreholeElevation;
            set
            {
                if (SetProperty(ref _relBoreholeElevation, value))
                    OnPropertyChanged(nameof(RelativeElevation));
            }
        }

        private double _foundationDepth = 1.5;
        public double FoundationDepth
        {
            get => _foundationDepth;
            set
            {
                if (SetProperty(ref _foundationDepth, value))
                {
                    RecalcAutoP0();
                    OnPropertyChanged(nameof(AbsFoundationDepthElevation));
                }
            }
        }

        /// <summary>基础底面绝对高程 = ±0.000绝对 - 埋深</summary>
        public double AbsFoundationDepthElevation => AbsStructureZero - FoundationDepth;

        public double RelativeElevation => UseAbsoluteElevation
            ? AbsBoreholeElevation - AbsStructureZero
            : RelBoreholeElevation;

        #endregion

        #region 基础参数

        private bool _isCompositeEnabled;
        public bool IsCompositeEnabled
        {
            get => _isCompositeEnabled;
            set
            {
                if (!SetProperty(ref _isCompositeEnabled, value)) return;
                if (value && _isPileEnabled)
                {
                    _isPileEnabled = false;
                    OnPropertyChanged(nameof(IsPileEnabled));
                    SaveSettings();
                }
                OnPropertyChanged(nameof(FoundationTypeIndex));
            }
        }

        private bool _isPileEnabled;
        public bool IsPileEnabled
        {
            get => _isPileEnabled;
            set
            {
                if (!SetProperty(ref _isPileEnabled, value)) return;
                if (value && _isCompositeEnabled)
                {
                    _isCompositeEnabled = false;
                    OnPropertyChanged(nameof(IsCompositeEnabled));
                    SaveSettings();
                }
                OnPropertyChanged(nameof(FoundationTypeIndex));
            }
        }

        public int FoundationTypeIndex => IsCompositeEnabled ? 1 : (IsPileEnabled ? 2 : 0);

        private bool _isBoxFoundation;
        public bool IsBoxFoundation
        {
            get => _isBoxFoundation;
            set
            {
                if (SetProperty(ref _isBoxFoundation, value))
                {
                    OnPropertyChanged(nameof(IsIsolatedFoundation));
                    RecalcAutoP0();
                }
            }
        }

        public bool IsIsolatedFoundation
        {
            get => !_isBoxFoundation;
            set
            {
                if (value) IsBoxFoundation = false;
            }
        }

        private double _boxConcreteThickness;
        public double BoxConcreteThickness
        {
            get => _boxConcreteThickness;
            set
            {
                if (SetProperty(ref _boxConcreteThickness, value))
                    RecalcAutoP0();
            }
        }

        private double _foundationLength = 3.0;
        public double FoundationLength
        {
            get => _foundationLength;
            set
            {
                if (SetProperty(ref _foundationLength, value))
                    RecalcAutoP0();
            }
        }

        private double _foundationWidth = 2.0;
        public double FoundationWidth
        {
            get => _foundationWidth;
            set
            {
                if (SetProperty(ref _foundationWidth, value))
                    RecalcAutoP0();
            }
        }

        private double _additionalPressure = 100.0;
        public double AdditionalPressure
        {
            get => _additionalPressure;
            set => SetProperty(ref _additionalPressure, value);
        }

        private double _bearingCapacity = 150.0;
        public double BearingCapacity
        {
            get => _bearingCapacity;
            set
            {
                if (SetProperty(ref _bearingCapacity, value))
                    OnPropertyChanged(nameof(CompositeXi));
            }
        }

        #endregion

        #region 荷载参数 (自动/手动 p0 和 ψs)

        private double _axialForce;
        public double AxialForce
        {
            get => _axialForce;
            set
            {
                if (SetProperty(ref _axialForce, value))
                    RecalcAutoP0();
            }
        }

        private double _selfWeightLoad;
        public double SelfWeightLoad
        {
            get => _selfWeightLoad;
            set
            {
                if (SetProperty(ref _selfWeightLoad, value))
                    RecalcAutoP0();
            }
        }

        private bool _isP0Auto;
        public bool IsP0Auto
        {
            get => _isP0Auto;
            set
            {
                if (SetProperty(ref _isP0Auto, value))
                    RecalcAutoP0();
            }
        }

        private bool _isPsiSAuto = true;
        public bool IsPsiSAuto
        {
            get => _isPsiSAuto;
            set => SetProperty(ref _isPsiSAuto, value);
        }

        private double _manualPsiS = 1.0;
        public double ManualPsiS
        {
            get => _manualPsiS;
            set => SetProperty(ref _manualPsiS, value);
        }

        private void RecalcAutoP0()
        {
            if (!IsP0Auto || _isLoading) return;
            double area = FoundationLength * FoundationWidth;
            if (area <= 0) return;

            double gm = GammaM > 0 ? GammaM : 20.0;
            double p0;

            if (IsBoxFoundation)
            {
                double gammaConcrete = 25.0;
                double gk = gammaConcrete * BoxConcreteThickness * area;
                double pk = (AxialForce + gk) / area;
                double overburdenSelfWeight = gm * FoundationDepth;
                p0 = pk - overburdenSelfWeight;
            }
            else
            {
                p0 = (AxialForce + SelfWeightLoad) / area - gm * FoundationDepth;
            }

            if (p0 < 0) p0 = 0;
            _additionalPressure = Math.Round(p0, 1);
            OnPropertyChanged(nameof(AdditionalPressure));
            SaveSettings();
        }

        #endregion

        #region 土层参数（地下水 + fak + γm）

        private bool _hasGroundwater;
        public bool HasGroundwater
        {
            get => _hasGroundwater;
            set => SetProperty(ref _hasGroundwater, value);
        }

        private double _groundwaterDepth;
        public double GroundwaterDepth
        {
            get => _groundwaterDepth;
            set => SetProperty(ref _groundwaterDepth, value);
        }

        private double _gammaM = 20.0;
        public double GammaM
        {
            get => _gammaM;
            set
            {
                if (SetProperty(ref _gammaM, value))
                    RecalcAutoP0();
            }
        }

        #endregion

        #region 回弹再压缩参数

        private bool _enableRebound;
        public bool EnableRebound
        {
            get => _enableRebound;
            set => SetProperty(ref _enableRebound, value);
        }

        private double _eciEsiRatio = 5.0;
        public double EciEsiRatio
        {
            get => _eciEsiRatio;
            set => SetProperty(ref _eciEsiRatio, value);
        }

        private double _psiC = 1.0;
        public double PsiC
        {
            get => _psiC;
            set => SetProperty(ref _psiC, value);
        }

        private double _kappa = 1.19;
        public double Kappa
        {
            get => _kappa;
            set => SetProperty(ref _kappa, value);
        }

        private double _reboundCompletionRatio = 0.5;
        public double ReboundCompletionRatio
        {
            get => _reboundCompletionRatio;
            set => SetProperty(ref _reboundCompletionRatio, value);
        }

        private double _reboundDepthRatio = 0.025;
        public double ReboundDepthRatio
        {
            get => _reboundDepthRatio;
            set => SetProperty(ref _reboundDepthRatio, value);
        }

        #endregion

        #region 复合地基参数

        private double _compositeBearingCapacity = 250.0;
        public double CompositeBearingCapacity
        {
            get => _compositeBearingCapacity;
            set
            {
                if (SetProperty(ref _compositeBearingCapacity, value))
                    OnPropertyChanged(nameof(CompositeXi));
            }
        }

        private double _treatedDepth = 10.0;
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
        public string SoilLayerMarkdown
        {
            get => _soilLayerMarkdown;
            set => SetProperty(ref _soilLayerMarkdown, value);
        }

        private ObservableCollection<SoilLayer> _parsedSoilLayers = new ObservableCollection<SoilLayer>();
        public ObservableCollection<SoilLayer> ParsedSoilLayers
        {
            get => _parsedSoilLayers;
            set => SetProperty(ref _parsedSoilLayers, value);
        }

        private int _selectedSoilLayerIndex = -1;
        public int SelectedSoilLayerIndex
        {
            get => _selectedSoilLayerIndex;
            set => SetProperty(ref _selectedSoilLayerIndex, value);
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
        public string ResultSummary
        {
            get => _resultSummary;
            set => SetProperty(ref _resultSummary, value);
        }

        private string _compressSummary = "";
        public string CompressSummary
        {
            get => _compressSummary;
            set => SetProperty(ref _compressSummary, value);
        }

        private string _reboundSummary = "";
        public string ReboundSummary
        {
            get => _reboundSummary;
            set => SetProperty(ref _reboundSummary, value);
        }

        private string _recompSummary = "";
        public string RecompSummary
        {
            get => _recompSummary;
            set => SetProperty(ref _recompSummary, value);
        }

        public SettlementResult LastResult { get; private set; }

        #endregion

        #region 计算书

        private string _reportText = "";
        public string ReportText
        {
            get => _reportText;
            set => SetProperty(ref _reportText, value);
        }

        #endregion

        #region UI 状态

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

        #endregion

        #region 命令

        public ICommand ParseTableCommand { get; }
        public ICommand CalculateCommand { get; }
        public ICommand DrawTableCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand AddLayerCommand { get; }
        public ICommand DeleteLayerCommand { get; }
        public ICommand MoveLayerUpCommand { get; }
        public ICommand MoveLayerDownCommand { get; }
        public ICommand GenerateReportCommand { get; }
        public ICommand CopyReportCommand { get; }

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
                string subType = IsBoxFoundation ? "箱型基础" : "独立基础";
                double skipD = RelativeElevation + FoundationDepth;
                double p0 = AdditionalPressure;

                if (result.HasRebound)
                {
                    // ━━ 启用回弹再压缩：根据 p₀ 分支显示 ━━

                    // Column 1: 回弹量（负方向，向上）
                    var sbRebound = new StringBuilder();
                    sbRebound.AppendLine($"pc = γm·d = {result.OverburdenPressure:F1} kPa");
                    sbRebound.AppendLine($"p = p₀+pc = {result.TotalReloadPressure:F1} kPa");
                    sbRebound.AppendLine($"R' = p/pc = {result.ReloadRatio:F3}");
                    sbRebound.AppendLine($"ψc={PsiC:F2}  Eci/Esi={EciEsiRatio:F1}");
                    sbRebound.AppendLine($"理论回弹 sc = {result.ReboundSettlement:F3} mm (↑)");
                    sbRebound.AppendLine($"η = {ReboundCompletionRatio:P0}");
                    sbRebound.Append($"实际回弹 η·sc = {result.ActualRebound:F3} mm (↑)");
                    ReboundSummary = "一、回弹量\r\n" + sbRebound.ToString();

                    if (p0 <= 0)
                    {
                        // p₀ ≤ 0: 超补偿/完全补偿，仅回弹再压缩

                        // Column 2: 再压缩（5.3.11 简化）
                        var sbRecomp = new StringBuilder();
                        sbRecomp.AppendLine($"κ = {Kappa:F2}");
                        sbRecomp.AppendLine($"简化 5.3.11:");
                        sbRecomp.AppendLine($"s'c = κ·η·sc·R'");
                        sbRecomp.AppendLine($"   = {Kappa:F2}×{result.ActualRebound:F3}×{result.ReloadRatio:F3}");
                        sbRecomp.Append($"   = {result.RecompressionSettlement:F3} mm (↓)");
                        CompressSummary = "二、再压缩(§5.3.11)\r\n" + sbRecomp.ToString();

                        // Column 3: 净变形
                        var sbNet = new StringBuilder();
                        sbNet.AppendLine($"净变形 = 再压缩 - 实际回弹");
                        sbNet.AppendLine($"  = {result.RecompressionSettlement:F3} - {result.ActualRebound:F3}");
                        sbNet.AppendLine($"  = {result.FinalSettlement:F2} mm");
                        string direction = result.FinalSettlement < 0 ? "(↑ 净隆起)" : result.FinalSettlement > 0 ? "(↓ 净沉降)" : "(无变形)";
                        sbNet.Append(direction);
                        RecompSummary = "三、净变形\r\n" + sbNet.ToString();
                    }
                    else
                    {
                        // p₀ > 0: 非补偿基础 — 压缩 + 滞回附加
                        // Column 2 → 压缩沉降
                        var sbCompress = new StringBuilder();
                        sbCompress.AppendLine($"【{type} · {subType}】");
                        if (skipD > 0)
                            sbCompress.AppendLine($"孔口→基底跳过 {skipD:F2} m");
                        sbCompress.AppendLine($"zn = {result.CalculationDepth:F1} m");
                        sbCompress.AppendLine($"s' = {result.TheoreticalSettlement:F2} mm");
                        sbCompress.AppendLine($"Ēs = {result.EquivalentEs:F1} MPa");
                        sbCompress.AppendLine($"ψs = {result.PsiS:F3}");
                        if (result.PsiE < 1.0)
                            sbCompress.AppendLine($"ψe = {result.PsiE:F3}");
                        sbCompress.Append($"压缩沉降 = {result.CompressionSettlement:F2} mm (↓)");
                        CompressSummary = "二、压缩沉降(§5.3.5)\r\n" + sbCompress.ToString();

                        // Column 3 → 滞回附加 + 最终沉降
                        var sbRecomp = new StringBuilder();
                        sbRecomp.AppendLine($"κ = {Kappa:F2}");
                        sbRecomp.AppendLine($"滞回附加 = η·sc·(κ-1)");
                        sbRecomp.AppendLine($"  = {result.ActualRebound:F3}×{(Kappa - 1):F2}");
                        sbRecomp.AppendLine($"  = {result.NetReboundSettlement:F3} mm (↓)");
                        sbRecomp.AppendLine($"────────────");
                        sbRecomp.AppendLine($"最终沉降 = 压缩 + 滞回");
                        sbRecomp.Append($"  = {result.FinalSettlement:F2} mm");
                        RecompSummary = "三、最终沉降\r\n" + sbRecomp.ToString();
                    }
                }
                else
                {
                    // ━━ 未启用回弹：常规压缩沉降 ━━
                    var sbCompress = new StringBuilder();
                    sbCompress.AppendLine($"【{type} · {subType}】");
                    if (skipD > 0)
                        sbCompress.AppendLine($"孔口→基底跳过 {skipD:F2} m");
                    sbCompress.AppendLine($"计算深度 zn = {result.CalculationDepth:F1} m");
                    sbCompress.AppendLine($"理论沉降 s' = {result.TheoreticalSettlement:F2} mm");
                    sbCompress.AppendLine($"当量模量 Ēs = {result.EquivalentEs:F1} MPa");
                    sbCompress.AppendLine($"经验系数 ψs = {result.PsiS:F3}");
                    if (result.PsiE < 1.0)
                        sbCompress.AppendLine($"等效系数 ψe = {result.PsiE:F3}");
                    sbCompress.Append($"最终沉降 = {result.FinalSettlement:F2} mm");
                    CompressSummary = "一、压缩沉降\r\n" + sbCompress.ToString();
                    ReboundSummary = "";
                    RecompSummary = "";
                }

                ResultSummary = "";

                StatusMessage = $"计算完成：最终沉降 {result.FinalSettlement:F2} mm";

                ReportText = GenerateCalculationReport(result);
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
                UseAbsoluteElevation = false;
                AbsBoreholeElevation = 0;
                AbsStructureZero = 0;
                RelBoreholeElevation = 0;
                FoundationDepth = 1.5;
                IsCompositeEnabled = false;
                IsPileEnabled = false;
                FoundationLength = 3.0;
                FoundationWidth = 2.0;
                AdditionalPressure = 100.0;
                BearingCapacity = 150.0;
                AxialForce = 0;
                SelfWeightLoad = 0;
                IsP0Auto = false;
                IsPsiSAuto = true;
                ManualPsiS = 1.0;
                IsBoxFoundation = false;
                BoxConcreteThickness = 0;
                GammaM = 20.0;
                HasGroundwater = false;
                GroundwaterDepth = 0;
                EnableRebound = false;
                EciEsiRatio = 5.0;
                PsiC = 1.0;
                Kappa = 1.19;
                ReboundCompletionRatio = 0.5;
                ReboundDepthRatio = 0.025;
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
                CompressSummary = "";
                ReboundSummary = "";
                RecompSummary = "";
                ReportText = "";
                StatusMessage = "已恢复默认值";
            }
            finally
            {
                _isLoading = false;
            }
            SaveSettings();
        }

        private void ExecuteAddLayer()
        {
            ParsedSoilLayers.Add(new SoilLayer
            {
                Id = "⑤",
                Name = "新层",
                Thickness = 1.0,
                Es = 10.0,
                Eci = 0
            });
            SelectedSoilLayerIndex = ParsedSoilLayers.Count - 1;
            StatusMessage = "已添加新地层";
        }

        private void ExecuteDeleteLayer()
        {
            if (SelectedSoilLayerIndex < 0 || SelectedSoilLayerIndex >= ParsedSoilLayers.Count)
            {
                StatusMessage = "请先选中要删除的行";
                return;
            }
            ParsedSoilLayers.RemoveAt(SelectedSoilLayerIndex);
            StatusMessage = "已删除选中地层";
        }

        private void ExecuteMoveLayerUp()
        {
            int idx = SelectedSoilLayerIndex;
            if (idx <= 0 || idx >= ParsedSoilLayers.Count) return;
            var item = ParsedSoilLayers[idx];
            ParsedSoilLayers.RemoveAt(idx);
            ParsedSoilLayers.Insert(idx - 1, item);
            SelectedSoilLayerIndex = idx - 1;
        }

        private void ExecuteMoveLayerDown()
        {
            int idx = SelectedSoilLayerIndex;
            if (idx < 0 || idx >= ParsedSoilLayers.Count - 1) return;
            var item = ParsedSoilLayers[idx];
            ParsedSoilLayers.RemoveAt(idx);
            ParsedSoilLayers.Insert(idx + 1, item);
            SelectedSoilLayerIndex = idx + 1;
        }

        private void ExecuteGenerateReport()
        {
            if (LastResult == null || !LastResult.Success)
            {
                StatusMessage = "请先执行计算";
                return;
            }
            ReportText = GenerateCalculationReport(LastResult);
            StatusMessage = "计算书已生成";
        }

        private void ExecuteCopyReport()
        {
            if (string.IsNullOrWhiteSpace(ReportText))
            {
                StatusMessage = "计算书为空，请先生成";
                return;
            }
            try
            {
                Clipboard.SetText(ReportText);
                StatusMessage = "已复制到剪贴板";
            }
            catch
            {
                StatusMessage = "复制失败";
            }
        }

        private SettlementInput BuildInput()
        {
            double boreholeElev = UseAbsoluteElevation ? AbsBoreholeElevation : RelBoreholeElevation;
            double structureZero = UseAbsoluteElevation ? AbsStructureZero : 0;

            return new SettlementInput
            {
                BoreholeElevation = boreholeElev,
                StructureZeroElevation = structureZero,
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
                SoilLayers = ParsedSoilLayers.ToList(),
                IsBoxFoundation = IsBoxFoundation,
                BoxConcreteThickness = BoxConcreteThickness,
                GammaM = GammaM,
                EnableRebound = EnableRebound,
                EciEsiRatio = EciEsiRatio,
                PsiC = PsiC,
                Kappa = Kappa,
                ReboundCompletionRatio = ReboundCompletionRatio,
                ReboundDepthRatio = ReboundDepthRatio
            };
        }

        #endregion

        #region 计算书生成

        private string GenerateCalculationReport(SettlementResult result)
        {
            var sb = new StringBuilder();
            var type = new[] { "天然基础", "复合地基", "桩基础" }[FoundationTypeIndex];

            sb.AppendLine("# 基础沉降计算书");
            sb.AppendLine();
            sb.AppendLine($"计算类型: {type}{(IsBoxFoundation ? "（箱型基础）" : "")}");
            sb.AppendLine($"计算日期: {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine();

            sb.AppendLine("## 一、工程参数");
            sb.AppendLine();
            sb.AppendLine("| 参数 | 数值 | 单位 |");
            sb.AppendLine("|------|------|------|");
            if (UseAbsoluteElevation)
            {
                sb.AppendLine($"| 孔点高程(绝对) | {AbsBoreholeElevation:F3} | m |");
                sb.AppendLine($"| 结构±0.000(绝对) | {AbsStructureZero:F3} | m |");
            }
            sb.AppendLine($"| 孔点相对高程 | {RelativeElevation:F3} | m |");
            sb.AppendLine($"| 基础埋深 d | {FoundationDepth:F2} | m |");
            sb.AppendLine($"| 基础宽度 b | {FoundationWidth:F2} | m |");
            sb.AppendLine($"| 基础长度 l | {FoundationLength:F2} | m |");
            sb.AppendLine($"| l/b | {(FoundationWidth > 0 ? FoundationLength / FoundationWidth : 0):F2} | - |");
            sb.AppendLine($"| γm | {GammaM:F1} | kN/m³ |");
            if (IsBoxFoundation)
                sb.AppendLine($"| 混凝土折算厚度 | {BoxConcreteThickness:F2} | m |");
            sb.AppendLine($"| fak | {BearingCapacity:F1} | kPa |");
            sb.AppendLine($"| p₀ | {AdditionalPressure:F1} | kPa |");

            if (FoundationTypeIndex == 1)
            {
                sb.AppendLine($"| fspk | {CompositeBearingCapacity:F1} | kPa |");
                sb.AppendLine($"| ξ = fspk/fak | {CompositeXi:F3} | - |");
                sb.AppendLine($"| 加固层深度 | {TreatedDepth:F2} | m |");
            }
            else if (FoundationTypeIndex == 2)
            {
                sb.AppendLine($"| 桩长 | {PileLength:F2} | m |");
                sb.AppendLine($"| 桩径 | {PileDiameter:F2} | m |");
                sb.AppendLine($"| 桩距 | {PileSpacing:F2} | m |");
                sb.AppendLine($"| 承台 | {CapLength:F2}×{CapWidth:F2} | m |");
                sb.AppendLine($"| 桩数 | {TotalPileCount} | 根 |");
            }
            double skipDepth = RelativeElevation + FoundationDepth;
            if (skipDepth > 0)
                sb.AppendLine($"| 孔口→基底跳过 | {skipDepth:F2} | m |");
            sb.AppendLine();

            sb.AppendLine("## 二、地层参数");
            sb.AppendLine();
            if (EnableRebound)
            {
                sb.AppendLine("| 编号 | 名称 | 厚度(m) | Es(MPa) | Eci(MPa) | 描述 |");
                sb.AppendLine("|------|------|---------|---------|----------|------|");
                foreach (var layer in ParsedSoilLayers)
                {
                    double eci = layer.Eci > 0 ? layer.Eci : layer.Es * EciEsiRatio;
                    sb.AppendLine($"| {layer.Id} | {layer.Name} | {layer.Thickness:F2} | {layer.Es:F1} | {eci:F1}{(layer.Eci <= 0 ? "*" : "")} | {layer.Description} |");
                }
                sb.AppendLine();
                sb.AppendLine("*: Eci 由 Es×倍率 自动估算");
            }
            else
            {
                sb.AppendLine("| 编号 | 名称 | 厚度(m) | Es(MPa) | 描述 |");
                sb.AppendLine("|------|------|---------|---------|------|");
                foreach (var layer in ParsedSoilLayers)
                    sb.AppendLine($"| {layer.Id} | {layer.Name} | {layer.Thickness:F2} | {layer.Es:F1} | {layer.Description} |");
            }
            sb.AppendLine();

            sb.AppendLine("## 三、计算公式");
            sb.AppendLine();
            sb.AppendLine("依据 GB 50007-2011 第 5.3.5 条，采用分层总和法：");
            sb.AppendLine();
            sb.AppendLine("    s' = Σ p₀/Esᵢ × (zᵢ·ᾱᵢ - z_{i-1}·ᾱ_{i-1})");
            sb.AppendLine("    s  = ψs × s'");
            sb.AppendLine();
            sb.AppendLine("附加应力系数 α 采用 Newmark (1935) 公式；");
            sb.AppendLine("平均附加应力系数 ᾱ 采用解析积分公式 (式3/4)。");
            sb.AppendLine();

            sb.AppendLine("## 四、逐层计算结果");
            sb.AppendLine();
            sb.AppendLine("| # | 地层 | z(m) | l/b | z/b | α | ᾱ | Es(MPa) | Δs'(mm) |");
            sb.AppendLine("|---|------|------|-----|-----|-------|-------|---------|---------|");
            foreach (var r in result.LayerResults)
            {
                sb.AppendLine($"| {r.Index} | {r.LayerId} | {r.Zi:F2} | {r.M:F2} | {r.N:F2} | {r.Alpha:F4} | {r.AlphaBar:F4} | {r.Es:F1} | {r.DeltaS:F3} |");
            }
            sb.AppendLine();

            sb.AppendLine("## 五、计算结果汇总");
            sb.AppendLine();

            double p0 = AdditionalPressure;

            if (p0 > 0)
            {
                sb.AppendLine($"- 理论沉降 s' = {result.TheoreticalSettlement:F2} mm");
                sb.AppendLine($"- 当量模量 Ēs = {result.EquivalentEs:F1} MPa");
                sb.AppendLine($"- 经验系数 ψs = {result.PsiS:F3}");
                if (result.PsiE < 1.0)
                    sb.AppendLine($"- 等效系数 ψe = {result.PsiE:F3}");
                sb.AppendLine($"- 压缩沉降 = {result.CompressionSettlement:F2} mm");
                sb.AppendLine($"- 计算深度 zn = {result.CalculationDepth:F1} m");
            }
            else
            {
                sb.AppendLine($"- p₀ = {p0:F1} kPa ≤ 0，无附加应力压缩沉降");
            }
            sb.AppendLine();

            if (result.HasRebound)
            {
                sb.AppendLine("## 六、回弹再压缩计算（GB 50007 §5.3.10~5.3.11）");
                sb.AppendLine();
                sb.AppendLine("### 6.1 回弹参数");
                sb.AppendLine();
                sb.AppendLine($"- 覆土平均重度 γm = {GammaM:F1} kN/m³");
                sb.AppendLine($"- 覆土自重压力 pc = γm × d = {GammaM:F1} × {FoundationDepth:F2} = {result.OverburdenPressure:F1} kPa");
                sb.AppendLine($"- 再加荷总压力 p = p₀ + pc = {p0:F1} + {result.OverburdenPressure:F1} = {result.TotalReloadPressure:F1} kPa");
                sb.AppendLine($"- 再加荷比 R' = p/pc = {result.ReloadRatio:F3}");
                sb.AppendLine($"- Eci/Esi 默认倍率 = {EciEsiRatio:F1}");
                sb.AppendLine($"- 回弹经验系数 ψc = {PsiC:F2}");
                sb.AppendLine($"- 再压缩增大系数 κ = {Kappa:F2}");
                sb.AppendLine($"- 施工期回弹完成率 η = {ReboundCompletionRatio:P0}");
                sb.AppendLine($"- 深度终止比值 = {ReboundDepthRatio}");
                sb.AppendLine();
                sb.AppendLine("### 6.2 回弹量（§5.3.10）");
                sb.AppendLine();
                sb.AppendLine($"- 理论回弹量 sc = {result.ReboundSettlement:F3} mm (方向↑)");
                sb.AppendLine($"- 实际回弹量 η·sc = {result.ActualRebound:F3} mm");
                sb.AppendLine();

                if (p0 <= 0)
                {
                    sb.AppendLine("### 6.3 再压缩（§5.3.11 简化，R'≤1）");
                    sb.AppendLine();
                    sb.AppendLine($"- s'c = κ·η·sc·R' = {Kappa:F2}×{result.ActualRebound:F3}×{result.ReloadRatio:F3} = {result.RecompressionSettlement:F3} mm (方向↓)");
                    sb.AppendLine($"- 净变形 = s'c - η·sc = {result.RecompressionSettlement:F3} - {result.ActualRebound:F3} = {result.FinalSettlement:F2} mm");
                    string dir = result.FinalSettlement < 0 ? "（净隆起↑）" : "（净沉降↓）";
                    sb.AppendLine($"- {dir}");
                }
                else
                {
                    sb.AppendLine("### 6.3 滞回附加（p₀>0，R'>1）");
                    sb.AppendLine();
                    sb.AppendLine($"- 滞回附加 = η·sc·(κ-1) = {result.ActualRebound:F3}×{(Kappa - 1):F2} = {result.NetReboundSettlement:F3} mm (方向↓)");
                    sb.AppendLine($"- 压缩沉降 = {result.CompressionSettlement:F2} mm");
                    sb.AppendLine($"- 最终沉降 = 压缩 + 滞回 = {result.CompressionSettlement:F2} + {result.NetReboundSettlement:F3} = {result.FinalSettlement:F2} mm");
                }
                sb.AppendLine();
            }

            sb.AppendLine($"- **最终沉降 s = {result.FinalSettlement:F2} mm**");
            sb.AppendLine();

            if (p0 > 0)
            {
                sb.AppendLine($"## {(result.HasRebound ? "七" : "六")}、深度判定");
                sb.AppendLine();
                sb.AppendLine("依据 GB 50007-2011 式5.3.7：Δs'n ≤ 0.025 × Σ Δs'i");
                sb.AppendLine();
            }

            return sb.ToString();
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
                    BoreholeElevation = AbsBoreholeElevation,
                    StructureZeroElevation = AbsStructureZero,
                    FoundationDepth = FoundationDepth,
                    UseAbsoluteElevation = UseAbsoluteElevation,
                    RelBoreholeElevation = RelBoreholeElevation,
                    FoundationTypeIndex = FoundationTypeIndex,
                    IsCompositeEnabled = IsCompositeEnabled,
                    IsPileEnabled = IsPileEnabled,
                    IsBoxFoundation = IsBoxFoundation,
                    BoxConcreteThickness = BoxConcreteThickness,
                    FoundationLength = FoundationLength,
                    FoundationWidth = FoundationWidth,
                    AdditionalPressure = AdditionalPressure,
                    BearingCapacity = BearingCapacity,
                    AxialForce = AxialForce,
                    SelfWeightLoad = SelfWeightLoad,
                    IsP0Auto = IsP0Auto,
                    IsPsiSAuto = IsPsiSAuto,
                    ManualPsiS = ManualPsiS,
                    GammaM = GammaM,
                    HasGroundwater = HasGroundwater,
                    GroundwaterDepth = GroundwaterDepth,
                    EnableRebound = EnableRebound,
                    EciEsiRatio = EciEsiRatio,
                    PsiC = PsiC,
                    Kappa = Kappa,
                    ReboundCompletionRatio = ReboundCompletionRatio,
                    ReboundDepthRatio = ReboundDepthRatio,
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

                AbsBoreholeElevation = data.BoreholeElevation;
                AbsStructureZero = data.StructureZeroElevation;
                FoundationDepth = data.FoundationDepth;
                UseAbsoluteElevation = data.UseAbsoluteElevation;
                RelBoreholeElevation = data.RelBoreholeElevation;

                if (data.IsCompositeEnabled || data.IsPileEnabled)
                {
                    IsCompositeEnabled = data.IsCompositeEnabled;
                    IsPileEnabled = data.IsPileEnabled;
                }
                else if (data.FoundationTypeIndex == 1)
                    IsCompositeEnabled = true;
                else if (data.FoundationTypeIndex == 2)
                    IsPileEnabled = true;
                IsBoxFoundation = data.IsBoxFoundation;
                BoxConcreteThickness = data.BoxConcreteThickness;
                FoundationLength = data.FoundationLength;
                FoundationWidth = data.FoundationWidth;
                AdditionalPressure = data.AdditionalPressure;
                BearingCapacity = data.BearingCapacity;
                AxialForce = data.AxialForce;
                SelfWeightLoad = data.SelfWeightLoad;
                IsP0Auto = data.IsP0Auto;
                IsPsiSAuto = data.IsPsiSAuto;
                ManualPsiS = data.ManualPsiS;
                GammaM = data.GammaM > 0 ? data.GammaM : 20.0;
                HasGroundwater = data.HasGroundwater;
                GroundwaterDepth = data.GroundwaterDepth;
                EnableRebound = data.EnableRebound;
                EciEsiRatio = data.EciEsiRatio > 0 ? data.EciEsiRatio : 5.0;
                PsiC = data.PsiC > 0 ? data.PsiC : 1.0;
                Kappa = data.Kappa > 0 ? data.Kappa : 1.19;
                ReboundCompletionRatio = data.ReboundCompletionRatio;
                ReboundDepthRatio = data.ReboundDepthRatio > 0 ? data.ReboundDepthRatio : 0.025;
                CompositeBearingCapacity = data.CompositeBearingCapacity;
                TreatedDepth = data.TreatedDepth;
                PileLength = data.PileLength;
                PileDiameter = data.PileDiameter;
                PileSpacing = data.PileSpacing;
                CapLength = data.CapLength;
                CapWidth = data.CapWidth;
                TotalPileCount = data.TotalPileCount;
                SoilLayerMarkdown = data.SoilLayerMarkdown ?? "";

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
            public bool UseAbsoluteElevation { get; set; }
            public double RelBoreholeElevation { get; set; }
            public int FoundationTypeIndex { get; set; }
            public bool IsCompositeEnabled { get; set; }
            public bool IsPileEnabled { get; set; }
            public bool IsBoxFoundation { get; set; }
            public double BoxConcreteThickness { get; set; }
            public double FoundationLength { get; set; } = 3.0;
            public double FoundationWidth { get; set; } = 2.0;
            public double AdditionalPressure { get; set; } = 100.0;
            public double BearingCapacity { get; set; } = 150.0;
            public double AxialForce { get; set; }
            public double SelfWeightLoad { get; set; }
            public bool IsP0Auto { get; set; }
            public bool IsPsiSAuto { get; set; } = true;
            public double ManualPsiS { get; set; } = 1.0;
            public double GammaM { get; set; } = 20.0;
            public bool HasGroundwater { get; set; }
            public double GroundwaterDepth { get; set; }
            public bool EnableRebound { get; set; }
            public double EciEsiRatio { get; set; } = 5.0;
            public double PsiC { get; set; } = 1.0;
            public double Kappa { get; set; } = 1.19;
            public double ReboundCompletionRatio { get; set; } = 0.5;
            public double ReboundDepthRatio { get; set; } = 0.025;
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
                            && propertyName != nameof(CompressSummary)
                            && propertyName != nameof(ReboundSummary)
                            && propertyName != nameof(RecompSummary)
                            && propertyName != nameof(CalculationResults)
                            && propertyName != nameof(ReportText)
                            && propertyName != nameof(SelectedTabIndex)
                            && propertyName != nameof(SelectedSoilLayerIndex))
                SaveSettings();
            return true;
        }

        #endregion
    }
}
