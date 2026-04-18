using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shell;

namespace HyCAD.BlenderUI.Controls
{
    /// <summary>
    /// Blender 风格的可复用 Window 壳：
    ///  - 顶部自定义标题栏（HeaderTitle + HeaderRightContent + 关闭按钮）
    ///  - 底部命令栏（FooterLeftContent / StatusContent / FooterRightContent）
    ///  - 中部 Window.Content 作为业务区域
    ///
    /// 使用方式（XAML）：
    /// <code>
    /// <bui:BlenderWindow xmlns:bui="clr-namespace:HyCAD.BlenderUI.Controls;assembly=HyCAD.BlenderUI"
    ///                    HeaderTitle="窗口标题"
    ///                    Width="760" Height="520">
    ///     <bui:BlenderWindow.FooterRightContent>
    ///         <StackPanel Orientation="Horizontal">
    ///             <Button Content="确定" Style="{StaticResource BlenderButton}" />
    ///             <Button Content="取消" Style="{StaticResource BlenderButton}" />
    ///         </StackPanel>
    ///     </bui:BlenderWindow.FooterRightContent>
    ///     <Grid>...业务内容...</Grid>
    /// </bui:BlenderWindow>
    /// </code>
    ///
    /// 关闭按钮点击会触发 <see cref="CloseClicked"/> 路由事件；子类可订阅后跑 CancelCommand
    /// 然后由 ViewModel 通过 CloseRequested 事件回调 <see cref="RequestClose(bool?)"/>。
    /// </summary>
    public class BlenderWindow : Window
    {
        public const string PartCloseButton = "PART_CloseButton";

        static BlenderWindow()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(BlenderWindow),
                new FrameworkPropertyMetadata(typeof(BlenderWindow)));
        }

        public BlenderWindow()
        {
            // 必备：自定义标题栏需要去掉原生 Chrome；CornerRadius=0 与 Blender 风格一致
            WindowStyle = WindowStyle.None;
            AllowsTransparency = false;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            ShowInTaskbar = false;

            var chrome = new WindowChrome
            {
                CaptionHeight = 30,
                ResizeBorderThickness = new Thickness(6),
                GlassFrameThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0),
            };
            WindowChrome.SetWindowChrome(this, chrome);
        }

        // =========================================================================
        //  依赖属性：标题栏 / 命令栏 槽位
        // =========================================================================

        public static readonly DependencyProperty HeaderTitleProperty = DependencyProperty.Register(
            nameof(HeaderTitle), typeof(string), typeof(BlenderWindow),
            new PropertyMetadata(string.Empty));

        /// <summary>
        /// 标题栏左侧文字。与 <see cref="Window.Title"/> 解耦：原生 Title 用作任务栏标题，
        /// HeaderTitle 用作 UI 显示，构造时如未单独设置，会回落到 Title。
        /// </summary>
        public string HeaderTitle
        {
            get => (string)GetValue(HeaderTitleProperty);
            set => SetValue(HeaderTitleProperty, value);
        }

        public static readonly DependencyProperty HeaderRightContentProperty = DependencyProperty.Register(
            nameof(HeaderRightContent), typeof(object), typeof(BlenderWindow),
            new PropertyMetadata(null));

        /// <summary>标题栏右侧、关闭按钮左侧的附加内容槽位（如"载入预设"下拉框）。</summary>
        public object HeaderRightContent
        {
            get => GetValue(HeaderRightContentProperty);
            set => SetValue(HeaderRightContentProperty, value);
        }

        public static readonly DependencyProperty FooterLeftContentProperty = DependencyProperty.Register(
            nameof(FooterLeftContent), typeof(object), typeof(BlenderWindow),
            new PropertyMetadata(null));

        /// <summary>底部命令栏左侧槽位（如"JD x/y"等只读统计文字）。</summary>
        public object FooterLeftContent
        {
            get => GetValue(FooterLeftContentProperty);
            set => SetValue(FooterLeftContentProperty, value);
        }

        public static readonly DependencyProperty FooterRightContentProperty = DependencyProperty.Register(
            nameof(FooterRightContent), typeof(object), typeof(BlenderWindow),
            new PropertyMetadata(null));

        /// <summary>底部命令栏右侧槽位（典型放"确定 / 取消"按钮）。</summary>
        public object FooterRightContent
        {
            get => GetValue(FooterRightContentProperty);
            set => SetValue(FooterRightContentProperty, value);
        }

        public static readonly DependencyProperty StatusContentProperty = DependencyProperty.Register(
            nameof(StatusContent), typeof(object), typeof(BlenderWindow),
            new PropertyMetadata(null));

        /// <summary>底部命令栏中段的状态文本槽位（如"全部规范项通过 / 有未通过项"）。</summary>
        public object StatusContent
        {
            get => GetValue(StatusContentProperty);
            set => SetValue(StatusContentProperty, value);
        }

        public static readonly DependencyProperty ShowCloseButtonProperty = DependencyProperty.Register(
            nameof(ShowCloseButton), typeof(bool), typeof(BlenderWindow),
            new PropertyMetadata(true));

        /// <summary>是否显示标题栏右上角的关闭按钮（默认 true）。</summary>
        public bool ShowCloseButton
        {
            get => (bool)GetValue(ShowCloseButtonProperty);
            set => SetValue(ShowCloseButtonProperty, value);
        }

        // =========================================================================
        //  路由事件：标题栏 X 按钮点击
        // =========================================================================

        public static readonly RoutedEvent CloseClickedEvent = EventManager.RegisterRoutedEvent(
            nameof(CloseClicked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(BlenderWindow));

        /// <summary>
        /// 标题栏 X 按钮被点击。子类典型订阅方式：检查 ViewModel.CancelCommand.CanExecute 后执行；
        /// 兜底直接 <see cref="RequestClose(bool?)"/>。
        /// </summary>
        public event RoutedEventHandler CloseClicked
        {
            add => AddHandler(CloseClickedEvent, value);
            remove => RemoveHandler(CloseClickedEvent, value);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (GetTemplateChild(PartCloseButton) is System.Windows.Controls.Button btn)
            {
                btn.Click -= OnCloseButtonClick;
                btn.Click += OnCloseButtonClick;
            }
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            // 先冒泡 CloseClicked，让子类决定走 CancelCommand 还是直接关
            var args = new RoutedEventArgs(CloseClickedEvent, this);
            RaiseEvent(args);

            // 如果子类没处理（args.Handled == false），就直接关
            if (!args.Handled)
            {
                RequestClose(null);
            }
        }

        // =========================================================================
        //  对外辅助：安全关闭
        // =========================================================================

        /// <summary>
        /// 安全设置 <see cref="Window.DialogResult"/> 并 Close()。
        /// AutoCAD 的 <c>Application.ShowModalWindow</c> 不允许设 DialogResult，
        /// 这里吸 <see cref="InvalidOperationException"/>，保持上层调用方约定不变。
        /// </summary>
        protected internal void RequestClose(bool? dialogResult)
        {
            if (dialogResult.HasValue)
            {
                try { DialogResult = dialogResult; }
                catch (InvalidOperationException)
                {
                    // ShowModalWindow 场景：无法设置 DialogResult，忽略即可
                }
            }
            Close();
        }
    }
}
