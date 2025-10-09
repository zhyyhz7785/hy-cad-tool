using System.Windows;
using System.Windows.Controls;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Views
{
    /// <summary>
    /// ReinPanel - 钢筋配置面板
    /// 简化版本：移除了对原系统的直接依赖，保留 UI 交互逻辑
    /// </summary>
    public partial class ReinPanel : UserControl
    {
        public ReinPanel()
        {
            InitializeComponent();
        }

        #region 属性（用于存储用户输入）

        public double Scale => double.TryParse(ScaleText.Text, out double value) && value > 0 ? value : 40.0;
        public double AnchorageLength => double.TryParse(AnchorageLengthText.Text, out double value) && value >= 0 ? value : 500.0;
        public double DotSeparation => double.TryParse(DotSeparationText.Text, out double value) && value >= 0 ? value : 200.0;
        public double BendingLineMinLength => double.TryParse(BendingLineMinLengthText.Text, out double value) && value >= 0 ? value : 150.0;
        public double AnchorageJoinLength => double.TryParse(AnchorageJoinLengthText.Text, out double value) && value >= 0 ? value : 1500.0;
        public double DimDistanceTolerance => double.TryParse(DimDistanceToleranceText.Text, out double value) && value >= 0 ? value : 30.0;
        public double RebarDiameter => double.TryParse(RebarDiameterText.Text, out double value) && value >= 0 ? value : 12.0;
        public double RebarSpacing => double.TryParse(RebarSpacingText.Text, out double value) && value >= 0 ? value : 200.0;
        public double HookLength => double.TryParse(HookLengthText.Text, out double value) && value >= 0 ? value : 1.0;
        public double ProtectionThickness => double.TryParse(ProtectionThicknessText.Text, out double value) && value >= 0 ? value : 1.0;
        public double ReinforcementDiameter => double.TryParse(ReinforcementDiameterText.Text, out double value) && value >= 0 ? value : 0.35;
        public double DotReinOffset => double.TryParse(DotReinOffsetText.Text, out double value) && value >= 0 ? value : 1.35;
        public double DimensionDistanceInside => double.TryParse(DimensionDistanceInsideText.Text, out double value) && value >= 0 ? value : 6.0;
        public double DimensionDistanceOutside => double.TryParse(DimensionDistanceOutsideText.Text, out double value) && value >= 0 ? value : 14.0;
        public double DimensionDistanceWithDim => double.TryParse(DimensionDistanceWithDimText.Text, out double value) && value >= 0 ? value : 6.0;
        public double MleaderDistance => double.TryParse(MleaderDistanceText.Text, out double value) && value >= 0 ? value : 6.0;

        #endregion

        #region 事件处理（占位符 - 后续实现业务逻辑）

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument?.Editor;
            if (ed == null) return;

            ed.WriteMessage($"\n设置样式 - Scale: {Scale}");
            // TODO: 在后续步骤中实现样式设置逻辑
            // 当前仅测试面板功能
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            // 恢复默认值
            ScaleText.Text = "40";
            AnchorageLengthText.Text = "500";
            DotSeparationText.Text = "200";
            BendingLineMinLengthText.Text = "150";
            AnchorageJoinLengthText.Text = "1500";
            HookLengthText.Text = "1";
            ProtectionThicknessText.Text = "1";
            ReinforcementDiameterText.Text = "0.35";
            DotReinOffsetText.Text = "1.35";
            DimensionDistanceInsideText.Text = "6";
            DimensionDistanceOutsideText.Text = "14";
            DimDistanceToleranceText.Text = "30";
            DimensionDistanceWithDimText.Text = "6";
            MleaderDistanceText.Text = "6";
            RebarSpacingText.Text = "200";
            RebarDiameterText.Text = "12";

            var ed = AcApp.DocumentManager.MdiActiveDocument?.Editor;
            ed?.WriteMessage("\n已恢复默认值");
        }

        private void DrawButton_Click(object sender, RoutedEventArgs e)
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument?.Editor;
            if (ed == null) return;

            ed.WriteMessage($"\n绘制钢筋 - 直径: {RebarDiameter}, 间距: {RebarSpacing}");
            // TODO: 在后续步骤中实现绘制逻辑
        }

        private void DIM1Button_Click(object sender, RoutedEventArgs e)
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument?.Editor;
            ed?.WriteMessage("\n标注钢筋 1");
            // TODO: 实现标注逻辑
        }

        private void DIM2Button_Click(object sender, RoutedEventArgs e)
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument?.Editor;
            ed?.WriteMessage("\n标注钢筋 2");
            // TODO: 实现标注逻辑
        }

        private void DIM3Button_Click(object sender, RoutedEventArgs e)
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument?.Editor;
            ed?.WriteMessage("\n标注钢筋 3");
            // TODO: 实现标注逻辑
        }

        #endregion
    }
}

