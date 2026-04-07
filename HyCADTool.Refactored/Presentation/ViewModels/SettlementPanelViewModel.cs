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
                    RecalcGammaM();
                    RecalcAutoP0();
                    OnPropertyChanged(nameof(AbsFoundationDepthElevation));
                }
            }
        }

        /// <summary>基础底面绝对高程 = ±0.000绝对 - 埋深</summary>
        public double AbsFoundationDepthElevation => AbsStructureZero - FoundationDepth;

        private double _indoorOutdoorDiff;
        /// <summary>室内外高差 Δh (m)</summary>
        public double IndoorOutdoorDiff
        {
            get => _indoorOutdoorDiff;
            set
            {
                if (SetProperty(ref _indoorOutdoorDiff, value))
                    RecalcAutoP0();
            }
        }

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
            set => SetProperty(ref _selfWeightLoad, value);
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
                p0 = pk - gm * FoundationDepth;
            }
            else
            {
                double dEff = FoundationDepth - IndoorOutdoorDiff * 0.5;
                if (dEff < 0) dEff = 0;
                double g = gm * area * dEff;

                _selfWeightLoad = Math.Round(g, 1);
                OnPropertyChanged(nameof(SelfWeightLoad));

                double pk = (AxialForce + g) / area;
                p0 = pk - gm * FoundationDepth;
            }

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
            set
            {
                if (SetProperty(ref _hasGroundwater, value))
                    RecalcGammaM();
            }
        }

        private double _groundwaterDepth;
        public double GroundwaterDepth
        {
            get => _groundwaterDepth;
            set
            {
                if (SetProperty(ref _groundwaterDepth, value))
                    RecalcGammaM();
            }
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

        /// <summary>
        /// 从已解析土层计算 γm：基底以上土的加权平均重度，地下水位以下取有效重度 γ'=γ−10。
        /// 无可用 γ 数据时保持当前值不变。
        /// </summary>
        private void RecalcGammaM()
        {
            if (_isLoading || ParsedSoilLayers == null || ParsedSoilLayers.Count == 0) return;

            double relElev = RelativeElevation;
            double d = FoundationDepth;
            double targetStart = relElev;
            double targetEnd = relElev + d;
            if (targetEnd <= 0) return;

            double gwDepthFromBorehole = HasGroundwater ? GroundwaterDepth : double.MaxValue;
            const double gammaWater = 9.8;
            const double defaultGamma = 18.0;

            double totalWeight = 0;
            double totalThick = 0;
            double currentDepth = 0;

            foreach (var layer in ParsedSoilLayers)
            {
                if (layer.Thickness <= 0) continue;
                double layerTop = currentDepth;
                double layerBottom = currentDepth + layer.Thickness;
                currentDepth = layerBottom;

                if (layerBottom <= targetStart) continue;
                if (layerTop >= targetEnd) break;

                double top = Math.Max(layerTop, targetStart);
                double bot = Math.Min(layerBottom, targetEnd);
                double h = bot - top;
                if (h <= 0) continue;

                double gamma = layer.Gamma > 0 ? layer.Gamma : defaultGamma;

                if (gwDepthFromBorehole >= bot)
                {
                    totalWeight += gamma * h;
                }
                else if (gwDepthFromBorehole <= top)
                {
                    totalWeight += Math.Max(gamma - gammaWater, 1.0) * h;
                }
                else
                {
                    double above = gwDepthFromBorehole - top;
                    double below = bot - gwDepthFromBorehole;
                    totalWeight += gamma * above + Math.Max(gamma - gammaWater, 1.0) * below;
                }
                totalThick += h;
            }

            if (totalThick > 0)
            {
                double calc = Math.Round(totalWeight / totalThick, 2);
                _gammaM = calc;
                OnPropertyChanged(nameof(GammaM));
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

        private double _eta1 = 0.5;
        public double Eta1
        {
            get => _eta1;
            set => SetProperty(ref _eta1, value);
        }

        private double _eta2 = 0.8;
        public double Eta2
        {
            get => _eta2;
            set => SetProperty(ref _eta2, value);
        }

        private double _r0Prime = 0.4;
        public double R0PrimeR
        {
            get => _r0Prime;
            set => SetProperty(ref _r0Prime, value);
        }

        private double _R0Prime = 0.3;
        public double R0PrimeRatio
        {
            get => _R0Prime;
            set => SetProperty(ref _R0Prime, value);
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

        private double _compLambda = 1.0;
        public double CompLambda
        {
            get => _compLambda;
            set => SetProperty(ref _compLambda, value);
        }

        private double _compBeta = 0.5;
        public double CompBeta
        {
            get => _compBeta;
            set => SetProperty(ref _compBeta, value);
        }

        private double _compFsk;
        public double CompFsk
        {
            get => _compFsk;
            set => SetProperty(ref _compFsk, value);
        }

        private double _compFcu;
        public double CompFcu
        {
            get => _compFcu;
            set => SetProperty(ref _compFcu, value);
        }

        private double _compAlphaP = 1.0;
        public double CompAlphaP
        {
            get => _compAlphaP;
            set => SetProperty(ref _compAlphaP, value);
        }

        private double _compQp;
        public double CompQp
        {
            get => _compQp;
            set => SetProperty(ref _compQp, value);
        }

        private double _compPileDiameter = 0.5;
        public double CompPileDiameter
        {
            get => _compPileDiameter;
            set => SetProperty(ref _compPileDiameter, value);
        }

        private double _compPileSpacing = 1.5;
        public double CompPileSpacing
        {
            get => _compPileSpacing;
            set => SetProperty(ref _compPileSpacing, value);
        }

        private int _compArrangementType = 1;
        public int CompArrangementType
        {
            get => _compArrangementType;
            set => SetProperty(ref _compArrangementType, value);
        }

        private string _compSideResistanceText = "";
        public string CompSideResistanceText
        {
            get => _compSideResistanceText;
            set => SetProperty(ref _compSideResistanceText, value);
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

        private double _pileQp;
        public double PileQp
        {
            get => _pileQp;
            set => SetProperty(ref _pileQp, value);
        }

        private double _pileNk;
        public double PileNk
        {
            get => _pileNk;
            set => SetProperty(ref _pileNk, value);
        }

        private string _pileSideResistanceText = "";
        public string PileSideResistanceText
        {
            get => _pileSideResistanceText;
            set => SetProperty(ref _pileSideResistanceText, value);
        }

        #endregion

        #region 桩身结构参数

        private double _pileAxialForceDesign;
        public double PileAxialForceDesign
        {
            get => _pileAxialForceDesign;
            set => SetProperty(ref _pileAxialForceDesign, value);
        }

        private double _pilePsiC = 0.7;
        public double PilePsiC
        {
            get => _pilePsiC;
            set => SetProperty(ref _pilePsiC, value);
        }

        private double _pileFc = 14300;
        public double PileFc
        {
            get => _pileFc;
            set => SetProperty(ref _pileFc, value);
        }

        private string _pileConcreteGrade = "C30";
        public string PileConcreteGrade
        {
            get => _pileConcreteGrade;
            set => SetProperty(ref _pileConcreteGrade, value);
        }

        private double _pileSteelArea;
        public double PileSteelArea
        {
            get => _pileSteelArea;
            set => SetProperty(ref _pileSteelArea, value);
        }

        #endregion

        #region 地层数据

        private string _soilLayerMarkdown = "";
        public string SoilLayerMarkdown
        {
            get => _soilLayerMarkdown;
            set
            {
                if (!SetProperty(ref _soilLayerMarkdown, value)) return;
                if (string.IsNullOrWhiteSpace(value))
                {
                    ParsedSoilLayers = new ObservableCollection<SoilLayer>();
                    SelectedSoilLayerIndex = -1;
                }
            }
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
                var parseResult = MarkdownTableParser.ParseAll(SoilLayerMarkdown);
                if (parseResult.Layers.Count == 0 && parseResult.DetectedTableTypes.Count == 0)
                {
                    StatusMessage = "未检测到有效表格";
                    return;
                }

                if (ParsedSoilLayers.Count > 0 && parseResult.Layers.Count > 0)
                {
                    MergeIntoExisting(parseResult.Layers);
                }
                else if (parseResult.Layers.Count > 0)
                {
                    ParsedSoilLayers = new ObservableCollection<SoilLayer>(parseResult.Layers);
                }

                if (parseResult.GroundwaterDepth.HasValue)
                {
                    HasGroundwater = true;
                    GroundwaterDepth = parseResult.GroundwaterDepth.Value;
                }

                RecalcGammaM();

                StatusMessage = $"解析成功：{parseResult.Summary}";
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"解析失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 将新解析的土层数据增量合并到已有列表（按地层编号匹配）
        /// </summary>
        private void MergeIntoExisting(List<SoilLayer> incoming)
        {
            var existing = ParsedSoilLayers.ToDictionary(
                l => MarkdownTableParser.CanonicalLayerKey(string.IsNullOrEmpty(l.Id) ? l.Name : l.Id),
                l => l, StringComparer.Ordinal);

            foreach (var newLayer in incoming)
            {
                string key = MarkdownTableParser.CanonicalLayerKey(
                    string.IsNullOrEmpty(newLayer.Id) ? newLayer.Name : newLayer.Id);
                if (string.IsNullOrEmpty(key)) continue;

                if (existing.TryGetValue(key, out SoilLayer target))
                {
                    if (string.IsNullOrEmpty(target.Name) && !string.IsNullOrEmpty(newLayer.Name))
                        target.Name = newLayer.Name;
                    if (target.Thickness <= 0 && newLayer.Thickness > 0)
                        target.Thickness = newLayer.Thickness;
                    if (target.Es <= 0 && newLayer.Es > 0)
                        target.Es = newLayer.Es;
                    if (target.Eci <= 0 && newLayer.Eci > 0)
                        target.Eci = newLayer.Eci;
                    if (target.Fak <= 0 && newLayer.Fak > 0)
                        target.Fak = newLayer.Fak;
                    if (target.Gamma <= 0 && newLayer.Gamma > 0)
                        target.Gamma = newLayer.Gamma;
                    if (target.Nspt <= 0 && newLayer.Nspt > 0)
                        target.Nspt = newLayer.Nspt;
                    if (target.Qsik <= 0 && newLayer.Qsik > 0)
                        target.Qsik = newLayer.Qsik;
                    if (target.Qpk <= 0 && newLayer.Qpk > 0)
                        target.Qpk = newLayer.Qpk;
                    if (string.IsNullOrEmpty(target.Description) && !string.IsNullOrEmpty(newLayer.Description))
                        target.Description = newLayer.Description;
                }
                else
                {
                    ParsedSoilLayers.Add(newLayer);
                    existing[key] = newLayer;
                }
            }

            OnPropertyChanged(nameof(ParsedSoilLayers));
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
                    // ━━ Column 1: 回弹量 ━━
                    var sbRebound = new StringBuilder();
                    sbRebound.AppendLine($"pc = γm·d = {result.OverburdenPressure:F1} kPa");
                    sbRebound.AppendLine($"p = p₀+pc = {result.TotalReloadPressure:F1} kPa");
                    sbRebound.AppendLine($"R' = p/pc = {result.ReloadRatio:F3}");
                    sbRebound.AppendLine($"ψc={PsiC:F2}  Eci/Esi={EciEsiRatio:F1}");
                    sbRebound.AppendLine($"理论回弹 sc = {result.ReboundSettlement:F3} mm");
                    sbRebound.AppendLine($"η₁={Eta1:F2} η₂={Eta2:F2}");
                    sbRebound.AppendLine($"完成回弹 η₁·sc = {result.CompletedRebound:F3} mm");
                    sbRebound.Append($"清除量 η₂η₁sc = {result.ClearedRebound:F3} mm");
                    ReboundSummary = "一、回弹量(§5.3.10)\r\n" + sbRebound.ToString();

                    if (p0 <= 0)
                    {
                        // p ≤ pc: 补偿/超补偿，全程 §5.3.11
                        double remainRebound = result.ReboundSettlement - result.ClearedRebound;
                        var sbRecomp = new StringBuilder();
                        sbRecomp.AppendLine($"§5.3.11 分段公式:");
                        sbRecomp.AppendLine($"R'₀={R0PrimeRatio:F2} r'₀={R0PrimeR:F2} κ={Kappa:F2}");
                        sbRecomp.AppendLine($"s'c = {result.RecompressionSettlement:F3} mm (↓)");
                        sbRecomp.AppendLine($"────────────");
                        sbRecomp.AppendLine($"剩余回弹 = sc−清除");
                        sbRecomp.AppendLine($"  = {result.ReboundSettlement:F3}−{result.ClearedRebound:F3}");
                        sbRecomp.Append($"  = {remainRebound:F3} mm");
                        CompressSummary = "二、再压缩(§5.3.11)\r\n" + sbRecomp.ToString();

                        var sbNet = new StringBuilder();
                        sbNet.AppendLine($"净沉降 = s'c − 剩余回弹");
                        sbNet.AppendLine($"  = {result.RecompressionSettlement:F3} − {remainRebound:F3}");
                        sbNet.AppendLine($"  = {result.FinalSettlement:F2} mm");
                        string direction = result.FinalSettlement < 0 ? "(↑ 净隆起)" : result.FinalSettlement > 0 ? "(↓ 净沉降)" : "(无变形)";
                        sbNet.Append(direction);
                        RecompSummary = "三、净变形\r\n" + sbNet.ToString();
                    }
                    else
                    {
                        // p > pc: 非补偿，段1净沉降 + 段2压缩
                        var sbCompress = new StringBuilder();
                        sbCompress.AppendLine($"【{type} · {subType}】");
                        if (skipD > 0)
                            sbCompress.AppendLine($"孔口→基底跳过 {skipD:F2} m");
                        sbCompress.AppendLine($"段1(0→pc): 再压缩净沉降");
                        sbCompress.AppendLine($"  s'c(R'=1) = sc·κ = {result.RecompressionSettlement:F3} mm");
                        double remainRb = result.ReboundSettlement - result.ClearedRebound;
                        sbCompress.AppendLine($"  剩余回弹 = sc−清除 = {remainRb:F3} mm");
                        sbCompress.AppendLine($"  段1净沉降 = {result.NetReboundSettlement:F3} mm");
                        sbCompress.AppendLine($"────────────");
                        sbCompress.AppendLine($"段2: p−pc = p₀ = {p0:F1} kPa");
                        sbCompress.AppendLine($"zn = {result.CalculationDepth:F1} m");
                        sbCompress.AppendLine($"s' = {result.TheoreticalSettlement:F2} mm");
                        sbCompress.AppendLine($"ψs = {result.PsiS:F3}");
                        if (result.PsiE < 1.0)
                            sbCompress.AppendLine($"ψe = {result.PsiE:F3}");
                        sbCompress.Append($"段2压缩 = {result.CompressionSettlement:F2} mm");
                        CompressSummary = "二、附加应力(§5.3.5)\r\n" + sbCompress.ToString();

                        var sbTotal = new StringBuilder();
                        sbTotal.AppendLine($"段1(0→pc)净沉降 = {result.NetReboundSettlement:F3} mm");
                        sbTotal.AppendLine($"段2(p₀)压缩沉降 = {result.CompressionSettlement:F2} mm");
                        sbTotal.AppendLine($"────────────");
                        sbTotal.Append($"最终沉降 = {result.FinalSettlement:F2} mm");
                        RecompSummary = "三、最终沉降\r\n" + sbTotal.ToString();
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

                ReportText = GenerateFullReport(result);
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
                IndoorOutdoorDiff = 0;
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
                Eta1 = 0.5;
                Eta2 = 0.8;
                R0PrimeRatio = 0.3;
                R0PrimeR = 0.4;
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
            ReportText = GenerateFullReport(LastResult);
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
                IndoorOutdoorDiff = IndoorOutdoorDiff,
                EnableRebound = EnableRebound,
                EciEsiRatio = EciEsiRatio,
                PsiC = PsiC,
                Kappa = Kappa,
                Eta1 = Eta1,
                Eta2 = Eta2,
                R0Prime = R0PrimeRatio,
                r0Prime = R0PrimeR,
                ReboundDepthRatio = ReboundDepthRatio
            };
        }

        #endregion

        #region 计算书生成

        private ReportContext BuildReportContext(SettlementResult result)
        {
            var input = BuildInput();
            var ctx = new ReportContext
            {
                Input = input,
                Result = result,
                Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                UseAbsoluteElevation = UseAbsoluteElevation,
                AbsBoreholeElevation = AbsBoreholeElevation,
                AbsStructureZero = AbsStructureZero,
                OriginalSoilLayers = ParsedSoilLayers.ToList(),
                IsP0Auto = IsP0Auto,
                AxialForce = AxialForce,
                SelfWeightLoad = SelfWeightLoad,
                HasGroundwater = HasGroundwater,
                GroundwaterDepth = GroundwaterDepth
            };

            // Phase 2: 承载力验算
            try
            {
                if (FoundationTypeIndex == 1 && CompFsk > 0)
                {
                    var bearingInput = BuildCompositeBearingInput();
                    ctx.BearingResult = BearingCapacityService.CalculateComposite(input, bearingInput);
                }
                else if (FoundationTypeIndex == 2 && PileQp > 0)
                {
                    var bearingInput = BuildPileBearingInput();
                    ctx.BearingResult = BearingCapacityService.CalculatePile(input, bearingInput);
                }
            }
            catch { }

            // Phase 3: 桩身结构验算
            try
            {
                if (FoundationTypeIndex == 2 && PileAxialForceDesign > 0 && PileFc > 0)
                {
                    var structInput = BuildPileStructuralInput();
                    ctx.StructuralResult = PileStructuralService.Calculate(input, structInput);
                }
            }
            catch { }

            return ctx;
        }

        private BearingCapacityInput BuildCompositeBearingInput()
        {
            return new BearingCapacityInput
            {
                Lambda = CompLambda,
                Beta = CompBeta,
                Fsk = CompFsk,
                Fcu = CompFcu,
                AlphaP = CompAlphaP,
                CompQp = CompQp,
                CompPileDiameter = CompPileDiameter,
                CompPileSpacing = CompPileSpacing,
                ArrangementType = CompArrangementType,
                CompSideResistance = ParseSideResistance(CompSideResistanceText)
            };
        }

        private BearingCapacityInput BuildPileBearingInput()
        {
            return new BearingCapacityInput
            {
                PileQp = PileQp,
                Nk = PileNk,
                PileSideResistance = ParseSideResistance(PileSideResistanceText)
            };
        }

        private PileStructuralInput BuildPileStructuralInput()
        {
            return new PileStructuralInput
            {
                AxialForceDesign = PileAxialForceDesign,
                PsiC = PilePsiC,
                Fc = PileFc,
                ConcreteGrade = PileConcreteGrade,
                SteelArea = PileSteelArea,
                PileLength = PileLength
            };
        }

        /// <summary>
        /// 解析侧阻力文本，格式为每行一层：土层名称,厚度,qsi
        /// 例如：粉质黏土,3.0,30
        /// </summary>
        private List<SideResistanceLayer> ParseSideResistance(string text)
        {
            var result = new List<SideResistanceLayer>();
            if (string.IsNullOrWhiteSpace(text)) return result;

            foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split(new[] { ',', '，', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3) continue;
                if (!double.TryParse(parts[1].Trim(), out double thickness)) continue;
                if (!double.TryParse(parts[2].Trim(), out double qsi)) continue;

                result.Add(new SideResistanceLayer
                {
                    LayerName = parts[0].Trim(),
                    SoilType = parts.Length > 3 ? parts[3].Trim() : "",
                    Thickness = thickness,
                    Qsi = qsi
                });
            }
            return result;
        }

        private string GenerateFullReport(SettlementResult result)
        {
            var generator = new SettlementReportGenerator();
            var ctx = BuildReportContext(result);
            return generator.Generate(ctx);
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
                    IndoorOutdoorDiff = IndoorOutdoorDiff,
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
                    Eta1 = Eta1,
                    Eta2 = Eta2,
                    R0PrimeRatio = R0PrimeRatio,
                    R0PrimeR = R0PrimeR,
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
                IndoorOutdoorDiff = data.IndoorOutdoorDiff;
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
                Eta1 = data.Eta1 > 0 ? data.Eta1 : 0.5;
                Eta2 = data.Eta2 > 0 ? data.Eta2 : 0.8;
                R0PrimeRatio = data.R0PrimeRatio > 0 ? data.R0PrimeRatio : 0.3;
                R0PrimeR = data.R0PrimeR > 0 ? data.R0PrimeR : 0.4;
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
                else
                    ParsedSoilLayers = new ObservableCollection<SoilLayer>();
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
            public double IndoorOutdoorDiff { get; set; }
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
            public double Eta1 { get; set; } = 0.5;
            public double Eta2 { get; set; } = 0.8;
            public double R0PrimeRatio { get; set; } = 0.3;
            public double R0PrimeR { get; set; } = 0.4;
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
