using Autodesk.AutoCAD.Runtime;
using HyCADTool.Command;
using HyCADTool.Config;
using System.Windows;
using System.Windows.Controls;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Exception = Autodesk.AutoCAD.Runtime.Exception;
[assembly: CommandClass(typeof(HyCADTool.Views.ReinPanel))]
namespace HyCADTool.Views
{
    public partial class ReinPanel : UserControl
    {
        private static ReinPanel _activePanel;
        public static ReinPanel ActivePanel
        {
            get => _activePanel;
            set => _activePanel = value;
        }
        public ReinPanel()
        {
            InitializeComponent();
            ActivePanel = this;
        }
        #region 输入管理
        // 属性值直接等于 TextBox 默认值，未缩放
        public double Scale { get; set; }
        public double AnchorageLength { get; set; } = 500.0;
        public double DotSeparation { get; set; } = 200.0;
        public double BendingLineMinLength { get; set; } = 150.0;
        public double AnchorageJoinLength { get; set; } = 1500.0;
        public double DimDistanceTolerance { get; set; } = 30.0;
        public double TextXScale { get; set; } = 0.7;
        public double RebarDiameter { get; set; } = 14.0;
        public double RebarSpacing { get; set; } = 200.0;
        public double HookLength { get; set; } = 1.0;
        public double ProtectionThickness { get; set; } = 1.0;
        public double ReinforcementDiameter { get; set; } = 0.35;
        public double DotReinOffset { get; set; } = 1.35;
        public double TextSize { get; set; } = 3.0;
        //public double DimensionDistance { get; set; } = 4.0;
        public double DimensionDistanceInside { get; set; } = 6.0;
        public double DimensionDistanceOutside { get; set; } = 14.0;
        public double DimensionDistanceWithDim { get; set; } = 6.0;
        public double MleaderDistance { get; set; } = 6.0;
        private void ScaleText_TextChanged(object sender, TextChangedEventArgs e)
        {
            BaseConfig.Scale = double.TryParse(ScaleText.Text, out double value) && value > 0 ? value : 40.0;
            // Scale 移到 Reinforcement，不在此处理
            UpdateStyleNames();
        }
        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox && double.TryParse(textBox.Text, out double value) && value >= 0)
            {
                switch (textBox.Name)
                {
                    case "AnchorageLengthText": AnchorageLength = value; break;
                    case "DotSeparationText": DotSeparation = value; break;
                    case "BendingLineMinLengthText": BendingLineMinLength = value; break;
                    case "AnchorageJoinLengthText": AnchorageJoinLength = value; break;
                    case "DimDistanceToleranceText": DimDistanceTolerance = value; break;
                    //case "TextXScaleText": TextXScale = value; break;
                    case "RebarDiameterText": RebarDiameter = value; break;
                    case "RebarSpacingText": RebarSpacing = value; break;
                    case "HookLengthText": HookLength = value; break;
                    case "ProtectionThicknessText": ProtectionThickness = value; break;
                    case "ReinforcementDiameterText": ReinforcementDiameter = value; break;
                    case "DotReinOffsetText": DotReinOffset = value; break;
                    //case "TextSizeText": TextSize = value; break;
                    //case "DimensionDistanceText": DimensionDistance = value; break;
                    case "DimensionDistanceInsideText": DimensionDistanceInside = value; break;
                    case "DimensionDistanceOutsideText": DimensionDistanceOutside = value; break;
                    case "DimensionDistanceWithDimText": DimensionDistanceWithDim = value; break;
                    case "MleaderDistanceText": MleaderDistance = value; break;
                }
            }
        }
        #endregion
        #region 用户操作
        public void DrawReinforcement_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateInputData();
                Reinforcement.Rein();
                Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\nReinforcement drawn successfully.\n");
            }
            catch (Exception ex)
            {
                Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage($"\n{ex}\n");
            }
        }
        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            UpdateInputData();
            BaseConfig.InitializeStyle();
            Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\nStyles applied successfully.\n");
        }
        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            ResetToDefaultValues();
        }
        private void DIM1_Click(object sender, RoutedEventArgs e) => HyCommand.MleaderRein();
        private void DIM2_Click(object sender, RoutedEventArgs e) => HyCommand.MleaderReinOne();
        private void DIM3_Click(object sender, RoutedEventArgs e) => HyCommand.MleaderReinTwo();
        #endregion
        private void UpdateStyleNames()
        {
            // 样式名称由 Reinforcement.Scale 控制，此处无需操作
        }
        private void UpdateInputData()
        {
            ScaleText_TextChanged(ScaleText, null);
            foreach (var control in new TextBox[] {
                AnchorageLengthText, DotSeparationText, BendingLineMinLengthText, AnchorageJoinLengthText,
                HookLengthText, ProtectionThicknessText, ReinforcementDiameterText, DotReinOffsetText,
                 DimensionDistanceInsideText,
                DimensionDistanceOutsideText, DimDistanceToleranceText, DimensionDistanceWithDimText,
                MleaderDistanceText, RebarDiameterText, RebarSpacingText})
            {
                if (!string.IsNullOrEmpty(control.Text))
                    TextBox_TextChanged(control, null);
            }
        }
        public void ResetToDefaultValues()
        {
            AnchorageLength = 500.0;
            DotSeparation = 200.0;
            BendingLineMinLength = 150.0;
            AnchorageJoinLength = 1500.0;
            DimDistanceTolerance = 30.0;
            TextXScale = 0.7;
            RebarDiameter = 14.0;
            RebarSpacing = 200.0;
            HookLength = 1.0;
            ProtectionThickness = 1.0;
            ReinforcementDiameter = 0.35;
            DotReinOffset = 1.35;
            TextSize = 3.0;
            // DimensionDistance = 4.0;
            DimensionDistanceInside = 6.0;
            DimensionDistanceOutside = 14.0;
            DimensionDistanceWithDim = 6.0;
            MleaderDistance = 6.0;
            ScaleText.Text = "40"; // Scale 由 Reinforcement 处理，仅同步 UI
            AnchorageLengthText.Text = AnchorageLength.ToString();
            DotSeparationText.Text = DotSeparation.ToString();
            BendingLineMinLengthText.Text = BendingLineMinLength.ToString();
            AnchorageJoinLengthText.Text = AnchorageJoinLength.ToString();
            HookLengthText.Text = HookLength.ToString();
            ProtectionThicknessText.Text = ProtectionThickness.ToString();
            ReinforcementDiameterText.Text = ReinforcementDiameter.ToString();
            DotReinOffsetText.Text = DotReinOffset.ToString();
            //TextSizeText.Text = TextSize.ToString();
            //TextXScaleText.Text = TextXScale.ToString();
            //DimensionDistanceText.Text = DimensionDistance.ToString();
            DimensionDistanceInsideText.Text = DimensionDistanceInside.ToString();
            DimensionDistanceOutsideText.Text = DimensionDistanceOutside.ToString();
            DimDistanceToleranceText.Text = DimDistanceTolerance.ToString();
            DimensionDistanceWithDimText.Text = DimensionDistanceWithDim.ToString();
            MleaderDistanceText.Text = MleaderDistance.ToString();
            RebarSpacingText.Text = RebarSpacing.ToString();
            RebarDiameterText.Text = RebarDiameter.ToString();
        }
    }
}