using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyCADTool.Refactored.Domain.Interfaces;

namespace HyCADTool.Refactored.Presentation.ViewModels
{
    /// <summary>
    /// HY 设置面板 ViewModel
    /// 管理文字样式、标注样式、引线样式的全部参数
    /// 样式名称根据 Scale 动态生成，与旧代码保持一致
    /// </summary>
    public class SettingsPanelViewModel : INotifyPropertyChanged
    {
        private readonly IStyleService _styleService;

        #region 构造函数

        public SettingsPanelViewModel(IStyleService styleService)
        {
            _styleService = styleService;

            ApplyStyleCommand = new RelayCommand(ApplyStyle);
            ResetCommand = new RelayCommand(ResetToDefaults);
        }

        /// <summary>
        /// 无参构造函数（设计器用）
        /// </summary>
        public SettingsPanelViewModel()
        {
            ApplyStyleCommand = new RelayCommand(() => { });
            ResetCommand = new RelayCommand(() => { });
        }

        #endregion

        #region 基础属性

        private double _scale = 40.0;
        /// <summary>
        /// 主图形比例（核心参数，驱动所有样式名称）
        /// </summary>
        public double Scale
        {
            get => _scale;
            set
            {
                if (SetProperty(ref _scale, value))
                {
                    // Scale 变化时通知所有动态名称更新
                    OnPropertyChanged(nameof(TextStyleName));
                    OnPropertyChanged(nameof(DimStyleName));
                    OnPropertyChanged(nameof(MLeaderStyleName));
                    OnPropertyChanged(nameof(TableStyleName));
                    OnPropertyChanged(nameof(ActualTextHeight));
                    OnPropertyChanged(nameof(ActualMLeaderArrowSize));
                    OnPropertyChanged(nameof(ActualMLeaderLandingGap));
                }
            }
        }

        #endregion

        #region 动态样式名称（只读计算属性）

        /// <summary>文字样式名称: 0_Hy_{Scale}</summary>
        public string TextStyleName => $"0_Hy_{Scale}";

        /// <summary>标注样式名称: 0_Hy_{Scale}_Dim</summary>
        public string DimStyleName => $"0_Hy_{Scale}_Dim";

        /// <summary>引线样式名称: 0_Hy_{Scale}_Mleader</summary>
        public string MLeaderStyleName => $"0_Hy_{Scale}_Mleader";

        /// <summary>表格样式名称: 0_Hy_{Scale}_Table</summary>
        public string TableStyleName => $"0_Hy_{Scale}_Table";

        #endregion

        #region 文字样式属性

        private string _fontFileName = "tssdeng.shx";
        public string FontFileName
        {
            get => _fontFileName;
            set => SetProperty(ref _fontFileName, value);
        }

        private string _bigFontFileName = "hztxt.shx";
        public string BigFontFileName
        {
            get => _bigFontFileName;
            set => SetProperty(ref _bigFontFileName, value);
        }

        private double _textSize = 2.5;
        /// <summary>基础文字高度（实际高度 = TextSize * Scale）</summary>
        public double TextSize
        {
            get => _textSize;
            set
            {
                if (SetProperty(ref _textSize, value))
                    OnPropertyChanged(nameof(ActualTextHeight));
            }
        }

        private double _textXScale = 0.7;
        /// <summary>文字宽度比例</summary>
        public double TextXScale
        {
            get => _textXScale;
            set => SetProperty(ref _textXScale, value);
        }

        /// <summary>实际文字高度（只读预览）= TextSize * Scale</summary>
        public double ActualTextHeight => TextSize * Scale;

        #endregion

        #region 标注样式属性

        private double _dimtxt = 2.5;
        public double Dimtxt
        {
            get => _dimtxt;
            set => SetProperty(ref _dimtxt, value);
        }

        private double _dimexo = 1.0;
        public double Dimexo
        {
            get => _dimexo;
            set => SetProperty(ref _dimexo, value);
        }

        private double _dimexe = 1.0;
        public double Dimexe
        {
            get => _dimexe;
            set => SetProperty(ref _dimexe, value);
        }

        private double _dimdle = 0.5;
        public double Dimdle
        {
            get => _dimdle;
            set => SetProperty(ref _dimdle, value);
        }

        private double _dimgap = 1.0;
        public double Dimgap
        {
            get => _dimgap;
            set => SetProperty(ref _dimgap, value);
        }

        private double _dimasz = 1.0;
        public double Dimasz
        {
            get => _dimasz;
            set => SetProperty(ref _dimasz, value);
        }

        private string _dimArrowName = "_ARCHTICK";
        public string DimArrowName
        {
            get => _dimArrowName;
            set => SetProperty(ref _dimArrowName, value);
        }

        #endregion

        #region 引线样式属性

        private double _mleaderArrowSize = 2.0;
        /// <summary>引线箭头大小（基础值，实际 = 值 * Scale）</summary>
        public double MLeaderArrowSize
        {
            get => _mleaderArrowSize;
            set
            {
                if (SetProperty(ref _mleaderArrowSize, value))
                    OnPropertyChanged(nameof(ActualMLeaderArrowSize));
            }
        }

        private string _mleaderArrowName = "_DotSmall";
        public string MLeaderArrowName
        {
            get => _mleaderArrowName;
            set => SetProperty(ref _mleaderArrowName, value);
        }

        private double _mleaderLandingGap = 0.5;
        /// <summary>引线着陆间距（基础值，实际 = 值 * Scale）</summary>
        public double MLeaderLandingGap
        {
            get => _mleaderLandingGap;
            set
            {
                if (SetProperty(ref _mleaderLandingGap, value))
                    OnPropertyChanged(nameof(ActualMLeaderLandingGap));
            }
        }

        private int _mleaderTextColorIndex = 7;
        public int MLeaderTextColorIndex
        {
            get => _mleaderTextColorIndex;
            set => SetProperty(ref _mleaderTextColorIndex, value);
        }

        /// <summary>实际引线箭头大小 = MLeaderArrowSize * Scale</summary>
        public double ActualMLeaderArrowSize => MLeaderArrowSize * Scale;

        /// <summary>实际引线着陆间距 = MLeaderLandingGap * Scale</summary>
        public double ActualMLeaderLandingGap => MLeaderLandingGap * Scale;

        #endregion

        #region 状态信息

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        #endregion

        #region 命令

        public ICommand ApplyStyleCommand { get; }
        public ICommand ResetCommand { get; }

        #endregion

        #region 命令实现

        private void ApplyStyle()
        {
            try
            {
                if (_styleService == null)
                {
                    StatusMessage = "StyleService 未初始化";
                    return;
                }

                // 1. 创建/更新文字样式
                _styleService.CreateTextStyle(
                    TextStyleName,
                    FontFileName,
                    BigFontFileName,
                    TextSize * Scale,
                    TextXScale);
                _styleService.SetCurrentTextStyle(TextStyleName);

                // 2. 创建/更新标注样式（传入面板参数）
                _styleService.CreateDimensionStyle(
                    DimStyleName,
                    TextStyleName,
                    Scale,
                    Dimtxt, Dimexo, Dimexe, Dimdle, Dimgap, Dimasz);
                _styleService.SetCurrentDimensionStyle(DimStyleName);

                // 3. 创建/更新引线样式（传入面板参数）
                _styleService.CreateMLeaderStyle(
                    MLeaderStyleName,
                    TextStyleName,
                    Scale,
                    MLeaderArrowSize, MLeaderLandingGap, TextSize, MLeaderTextColorIndex);
                _styleService.SetCurrentMLeaderStyle(MLeaderStyleName);

                // 4. 创建/更新表格样式
                _styleService.CreateTableStyle(
                    TableStyleName,
                    TextStyleName);
                _styleService.SetCurrentTableStyle(TableStyleName);

                StatusMessage = $"样式应用成功 (Scale={Scale})";

                var ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument?.Editor;
                ed?.WriteMessage($"\n样式设置完成: {TextStyleName}, {DimStyleName}, {MLeaderStyleName}");
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"失败: {ex.Message}";
                var ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument?.Editor;
                ed?.WriteMessage($"\n样式设置失败: {ex.Message}");
            }
        }

        private void ResetToDefaults()
        {
            Scale = 40.0;

            // 文字样式
            FontFileName = "tssdeng.shx";
            BigFontFileName = "hztxt.shx";
            TextSize = 2.5;
            TextXScale = 0.7;

            // 标注样式
            Dimtxt = 2.5;
            Dimexo = 1.0;
            Dimexe = 1.0;
            Dimdle = 0.5;
            Dimgap = 1.0;
            Dimasz = 1.0;
            DimArrowName = "_ARCHTICK";

            // 引线样式
            MLeaderArrowSize = 2.0;
            MLeaderArrowName = "_DotSmall";
            MLeaderLandingGap = 0.5;
            MLeaderTextColorIndex = 7;

            StatusMessage = "已恢复默认值";
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
                return false;

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }
}
