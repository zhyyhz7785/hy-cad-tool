using System;
using System.Windows;
using System.Windows.Controls;
using HyCADTool.Features.OverKill;

namespace HyCADTool.Presentation.Views
{
    /// <summary>
    /// HYOV 参数设置窗口
    /// </summary>
    public partial class HyovSettingsWindow : Window
    {
        private HyovSettings _settings;

        public HyovSettingsWindow()
        {
            var ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument?.Editor;
            var totalWatch = System.Diagnostics.Stopwatch.StartNew();
            
            ed?.WriteMessage("\n  === HyovSettingsWindow 构造函数 ===");
            
            var phase1 = System.Diagnostics.Stopwatch.StartNew();
            InitializeComponent();
            ed?.WriteMessage($"\n  [InitializeComponent] {phase1.ElapsedMilliseconds} 毫秒");
            
            var phase2 = System.Diagnostics.Stopwatch.StartNew();
            _settings = HyovSettings.Instance.Clone();
            ed?.WriteMessage($"\n  [Clone Settings] {phase2.ElapsedMilliseconds} 毫秒");
            
            var phase3 = System.Diagnostics.Stopwatch.StartNew();
            LoadSettings();
            ed?.WriteMessage($"\n  [LoadSettings] {phase3.ElapsedMilliseconds} 毫秒");
            
            totalWatch.Stop();
            ed?.WriteMessage($"\n  [构造函数总时间] {totalWatch.ElapsedMilliseconds} 毫秒");
            
            // 监听 Loaded 事件
            this.Loaded += (s, e) =>
            {
                ed?.WriteMessage("\n  [窗口 Loaded 事件触发]");
            };
        }

        /// <summary>
        /// 加载设置到界面
        /// </summary>
        private void LoadSettings()
        {
            // 绘图单位
            cmbDrawingUnit.SelectedIndex = _settings.Unit == DrawingUnit.Meter ? 1 : 0;

            // OVERKILL 参数
            txtGeometricTolerance.Text = _settings.GeometricTolerance.ToString("G");
            txtParallelMergeDistance.Text = _settings.ParallelMergeDistance.ToString("F3");
            chkEnablePreClean.IsChecked = _settings.EnablePreClean;

            // FILLET 参数
            txtMinLineLength.Text = _settings.MinLineLength.ToString("F3");
            txtMaxExtendDistance.Text = _settings.MaxExtendDistance.ToString("F3");
            chkEnableBreakLines.IsChecked = _settings.EnableBreakLines;
            chkEnableExtendEndpoints.IsChecked = _settings.EnableExtendEndpoints;
            chkEnableExtendToLine.IsChecked = _settings.EnableExtendToLine;

            // 标记参数
            txtIndependentEndpointTolerance.Text = _settings.IndependentEndpointTolerance.ToString("F3");
            txtMarkerScale.Text = _settings.MarkerScale.ToString("F1");
            chkShowIndependentEndpoints.IsChecked = _settings.ShowIndependentEndpoints;
        }

        /// <summary>
        /// 保存界面设置
        /// </summary>
        private bool SaveSettings()
        {
            try
            {
                // OVERKILL 参数
                _settings.GeometricTolerance = double.Parse(txtGeometricTolerance.Text);
                _settings.ParallelMergeDistance = double.Parse(txtParallelMergeDistance.Text);
                _settings.EnablePreClean = chkEnablePreClean.IsChecked ?? true;

                // FILLET 参数
                _settings.MinLineLength = double.Parse(txtMinLineLength.Text);
                _settings.MaxExtendDistance = double.Parse(txtMaxExtendDistance.Text);
                _settings.EnableBreakLines = chkEnableBreakLines.IsChecked ?? true;
                _settings.EnableExtendEndpoints = chkEnableExtendEndpoints.IsChecked ?? true;
                _settings.EnableExtendToLine = chkEnableExtendToLine.IsChecked ?? true;

                // 标记参数
                _settings.IndependentEndpointTolerance = double.Parse(txtIndependentEndpointTolerance.Text);
                _settings.MarkerScale = double.Parse(txtMarkerScale.Text);
                _settings.ShowIndependentEndpoints = chkShowIndependentEndpoints.IsChecked ?? true;

                // 验证参数合法性
                if (_settings.GeometricTolerance <= 0)
                {
                    MessageBox.Show("几何容差必须大于 0", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (_settings.MinLineLength < 0)
                {
                    MessageBox.Show("最小线段长度不能为负数", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (_settings.MaxExtendDistance <= 0)
                {
                    MessageBox.Show("端点延伸最大距离必须大于 0", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (_settings.MarkerScale <= 0)
                {
                    MessageBox.Show("标记缩放倍数必须大于 0", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                return true;
            }
            catch (FormatException)
            {
                MessageBox.Show("请输入有效的数值", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        /// <summary>
        /// 确定按钮点击事件
        /// </summary>
        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            if (SaveSettings())
            {
                // 应用设置到单例
                HyovSettings.Instance.CopyFrom(_settings);
                DialogResult = true;
                Close();
            }
        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 重置默认按钮点击事件
        /// </summary>
        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "确定要重置为默认值吗？", 
                "确认", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _settings.ResetToDefaults();
                LoadSettings();
            }
        }

        /// <summary>
        /// 单位切换事件处理
        /// </summary>
        private void CmbDrawingUnit_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbDrawingUnit.SelectedItem is ComboBoxItem selectedItem && _settings != null)
            {
                var newUnit = selectedItem.Tag.ToString() == "Meter" 
                    ? DrawingUnit.Meter 
                    : DrawingUnit.Millimeter;
                
                if (_settings.Unit != newUnit)
                {
                    _settings.SwitchUnit(newUnit);
                    UpdateUIFromSettings();
                }
            }
        }

        /// <summary>
        /// 从设置更新界面显示
        /// </summary>
        private void UpdateUIFromSettings()
        {
            txtGeometricTolerance.Text = _settings.GeometricTolerance.ToString("G");
            txtParallelMergeDistance.Text = _settings.ParallelMergeDistance.ToString("F3");
            txtMinLineLength.Text = _settings.MinLineLength.ToString("F3");
            txtMaxExtendDistance.Text = _settings.MaxExtendDistance.ToString("F3");
            txtIndependentEndpointTolerance.Text = _settings.IndependentEndpointTolerance.ToString("F3");
            txtMarkerScale.Text = _settings.MarkerScale.ToString("F1");
        }
    }
}

