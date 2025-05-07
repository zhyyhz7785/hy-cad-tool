using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using static HyCADTool.BaseRein;
namespace HyCADTool.Views
{
    /// <summary>
    /// BaseReinPanel.xaml 的交互逻辑
    /// </summary>
    public partial class BaseReinPanel : UserControl
    {
        public BaseReinPanel()
        {
            InitializeComponent();
            // 从XAML中读取控件的初始值
            BaseRein.ExistingRebar = ExistingRebarCheckBox.IsChecked ?? false;
            BaseRein.AddAnchorLength = AddAnchorLengthCheckBox.IsChecked ?? false;
            BaseRein.DimAll = DimAllCheckBox.IsChecked ?? false;
            // 检查并确保 AnchorFactorText 已经有一个默认值
            if (string.IsNullOrEmpty(AnchorFactorText.Text))
            {
                // 设置一个合理的默认值，例如 "35"
                AnchorFactorText.Text = "35";
            }
            // 初始化 AnchorFactor
            #region 初始化BaseRein参数
            if (double.TryParse(PlateThicknessText.Text, out double plateThicknessText))
            {
                BaseRein.PlateThickness = plateThicknessText;
            }
            if (double.TryParse(AnchorFactorText.Text, out double anchorFactor))
            {
                AnchorFactor = anchorFactor;
                BaseRein.AnchorFactor = anchorFactor;
            }
            else
            {
                // 处理解析失败的情况
                AnchorFactor = 0;
            }
            if (double.TryParse(ReinforceTextDistanceXText.Text, out double reinforceTextDistanceX))
            {
                BaseRein.ReinforceTextDistanceX = reinforceTextDistanceX;
            }
            if (double.TryParse(ReinforceTextDistanceYText.Text, out double reinforceTextDistanceY))
            {
                BaseRein.ReinforceTextDistanceY = reinforceTextDistanceY;
            }
            if (double.TryParse(ReinforceDistanceText.Text, out double reinforceDistance))
            {
                BaseRein.ReinforceDistance = reinforceDistance;
            }
            if (double.TryParse(DimensionDistanceWithDimText.Text, out double dimensionDistanceWithDim))
            {
                BaseRein.DimensionDistanceWithDim = dimensionDistanceWithDim;
            }
            if (double.TryParse(AxisExtendText.Text, out double axisExtend))
            {
                BaseRein.AxisExtend = axisExtend;
            }
            if (double.TryParse(IntervalText.Text, out double interval))
            {
                BaseRein.Interval = interval;
            }
            if (double.TryParse(proximityThresholdText.Text, out double proximityThreshold))
            {
                BaseRein.proximityThreshold = proximityThreshold;
            }
            if (double.TryParse(ReinforceSafetyText.Text, out double reinforceSafety))
            {
                BaseRein.ReinforceSafety = reinforceSafety;
            }
            if (double.TryParse(MinAdditionalDiameterText.Text, out double minAdditionalDiameter))
            {
                BaseRein.MinAdditionalDiameter = minAdditionalDiameter;
            }
            if (double.TryParse(AdditionalSpacingText.Text, out double additionalSpacing))
            {
                BaseRein.AdditionalSpacing = additionalSpacing;
            }
            if (double.TryParse(ScaleText.Text, out double scale))
            {
                BaseRein.Scale = scale;
            }
            if (double.TryParse(RebarDiameterText.Text, out double rebarDiameter))
            {
                BaseRein.RebarDiameter = rebarDiameter;
            }
            if (double.TryParse(RebarSpacingText.Text, out double rebarSpacing))
            {
                BaseRein.RebarSpacing = rebarSpacing;
            }
            if (double.TryParse(TextToLineDistanceText.Text, out double textToLineDistance))
            {
                BaseRein.TextToLineDistance = textToLineDistance;
            }
            if (double.TryParse(HookLengthText.Text, out double hookLength))
            {
                BaseRein.HookLength = hookLength;
            }
            if (double.TryParse(PolylineWidthText.Text, out double polylineWidth))
            {
                BaseRein.PolylineWidth = polylineWidth;
            }
            #endregion
        }
        #region 输入管理
        private void ScaleText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (double.TryParse(ScaleText.Text, out double value))
            {
                BaseRein.Scale = value;
            }
        }
        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (double.TryParse(textBox.Text, out double value))
                {
                    switch (textBox.Name)
                    {
                        case "ReinforceTextDistanceXText":
                            BaseRein.ReinforceTextDistanceX = value;
                            break;
                        case "ReinforceTextDistanceYText":
                            BaseRein.ReinforceTextDistanceY = value;
                            break;
                        case "IntervalText":
                            BaseRein.Interval = value;
                            break;
                        case "ReinforceSafetyText":
                            BaseRein.ReinforceSafety = value;
                            break;
                        case "MinAdditionalDiameterText":
                            BaseRein.MinAdditionalDiameter = value;
                            break;
                        case "AdditionalSpacingText":
                            BaseRein.AdditionalSpacing = value;
                            break;
                        case "AnchorFactorText":
                            BaseRein.AnchorFactor = value;
                            break;
                        case "RebarDiameterText":
                            BaseRein.RebarDiameter = value;
                            break;
                        case "RebarSpacingText":
                            BaseRein.RebarSpacing = value;
                            break;
                        case "TextToLineDistanceText":
                            BaseRein.TextToLineDistance = value;
                            break;
                        case "HookLengthText":
                            BaseRein.HookLength = value;
                            break;
                        case "PolylineWidthText":
                            BaseRein.PolylineWidth = value;
                            break;
                        case "proximityThresholdText":
                            BaseRein.proximityThreshold = value;
                            break;
                        case "AxisExtendText":
                            BaseRein.AxisExtend = value;
                            break;
                        case "DimensionDistanceWithDimText":
                            BaseRein.DimensionDistanceWithDim = value;
                            break;
                        case "ReinforceDistanceText":
                            BaseRein.ReinforceDistance = value;
                            break;
                    }
                }
            }
        }
        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            BaseRein.SetUp();
        }
        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            // 恢复默认值
            PlateThicknessText.Text = "350";
            ExistingRebarCheckBox.IsChecked = true;
            ReinforceDistanceText.Text = "3";
            AddAnchorLengthCheckBox.IsChecked = true;
            DimAllCheckBox.IsChecked = false;
            ReinforceTextDistanceXText.Text = "0";
            ReinforceTextDistanceYText.Text = "0";
            DimensionDistanceWithDimText.Text = "6";
            AxisExtendText.Text = "15000";
            IntervalText.Text = "100";
            proximityThresholdText.Text = "1000";
            ReinforceSafetyText.Text = "1";
            FilterValuesTextBox.Text = "<5.65 7.5 9 12";
            MinAdditionalDiameterText.Text = "8";
            AdditionalSpacingText.Text = "200";
            DirectionComboBoxTry.SelectedItem = RebarDirection.TopX; // 设置为枚举的默认值
            ScaleText.Text = "100";
            AnchorFactorText.Text = "35";
            RebarDiameterText.Text = "12";
            RebarSpacingText.Text = "200";
            TextToLineDistanceText.Text = "1";
            HookLengthText.Text = "1";
            PolylineWidthText.Text = "0.4";
        }
        private void DrawReinforcement_Click(object sender, RoutedEventArgs e)
        {
            // 这里可以添加绘制钢筋的代码
            MessageBox.Show("绘制钢筋的功能尚未实现");
        }
        #endregion
        //整理底图
        private void StepOne_Click(object sender, RoutedEventArgs e)
        {
            BaseRein.OptimizeBasemap();
        }
        private void StepTwo_Click(object sender, RoutedEventArgs e)
        {
            string inputValues = FilterValuesTextBox.Text;
            char[] delimiters = new char[] { ' ', ',', '，' };
            List<string> fixedValues = inputValues.Split(delimiters, StringSplitOptions.RemoveEmptyEntries).ToList();
            BaseRein.FixedValues = fixedValues;
            BaseRein.SelectNoUseText();
        }
        private void StepFour_Click(object sender, RoutedEventArgs e)
        {
            BaseRein.SourceTextAndFinitePoly = BaseRein.SelectFiniteElementGrid();
            BaseRein.SourceTextAndEnvelopePoly = BaseRein.DrawBoundingPolyline(BaseRein.SourceTextAndFinitePoly);//4a;
            BaseRein.GroupByTextAndEnvelopePoly = BaseRein.GroupBySpatialProximity(BaseRein.SourceTextAndEnvelopePoly, BaseRein.proximityThreshold);//4b;
            BaseRein.CreateOptimizedBoundingPolygonFromPolygons(BaseRein.GroupByTextAndEnvelopePoly, "00_hy_调整配筋轮廓");//4c;     
        }
        private void StepFive_Click(object sender, RoutedEventArgs e)
        {
            //得到配筋区域的面积，和配筋区域的统计数据
            var a = BaseRein.ReinforcementStepA();
            //生成配筋
            if (DimAll)
            {
                BaseRein.ReinforcementAll(a);
            }
            else
            {
                BaseRein.ReinforcementStepB(a);
            }
        }
        private void StepSix_Click(object sender, RoutedEventArgs e)
        {
            BaseRein.Poly4Dim();
        }
        private void AddAnchorLengthCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            AddAnchorLength = true;
            // 从 AnchorFactorText 中读取用户输入的值
            if (AnchorFactorText != null && double.TryParse(AnchorFactorText.Text, out double anchorFactor))
            {
                AnchorFactor = anchorFactor;
            }
            else
            {
                // 如果解析失败，您可以设置一个默认值或处理异常
                AnchorFactor = 0;
            }
        }
        private void AddAnchorLengthCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            AddAnchorLength = false;
            // 未选中时，将 AnchorFactor 设置为 0
            AnchorFactor = 0;
        }
        private void DimAllCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            DimAll = true;
        }
        private void DimAllCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            DimAll = false;
        }
        private void ExistingRebarCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            ExistingRebar = true;
            RebarDiameterText.IsEnabled = true;
            RebarSpacingText.IsEnabled = true;
        }
        private void ExistingRebarCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            ExistingRebar = false;
            RebarDiameterText.IsEnabled = false;
            RebarSpacingText.IsEnabled = false;
        }
        private void DirectionComboBoxTry_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DirectionComboBoxTry.SelectedItem != null)
            {
                var selectedComboBoxItem = DirectionComboBoxTry.SelectedItem as ComboBoxItem;
                if (selectedComboBoxItem != null && selectedComboBoxItem.Tag != null)
                {
                    // 将选定的枚举值更新到 BaseRein.Direction
                    BaseRein.Direction = (RebarDirection)Enum.Parse(typeof(RebarDirection), selectedComboBoxItem.Tag.ToString());
                }
            }
        }
        private void DimDirectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DimDirectionComboBox.SelectedItem != null)
            {
                var selectedComboBoxItem = DimDirectionComboBox.SelectedItem as ComboBoxItem;
                if (selectedComboBoxItem != null && selectedComboBoxItem.Tag != null)
                {
                    // 将选定的枚举值更新到 BaseRein.Direction
                    BaseRein.InterDirection = (IntersectionsDirection)Enum.Parse(typeof(IntersectionsDirection), selectedComboBoxItem.Tag.ToString());
                }
            }
        }
    }
}
