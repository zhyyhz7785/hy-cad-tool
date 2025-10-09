using Autodesk.AutoCAD.Windows;
using Autofac;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms.Integration;

namespace HyCADTool.Refactored.Presentation
{
    /// <summary>
    /// 面板管理器 - 统一管理所有工具面板的创建、显示和生命周期
    /// 单例模式，通过依赖注入提供
    /// </summary>
    public class PanelManager
    {
        private readonly Dictionary<Type, PaletteSet> _paletteSets;
        private readonly Dictionary<Type, object> _panelInstances;
        private readonly IComponentContext _componentContext;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="componentContext">Autofac 组件上下文，用于解析面板依赖</param>
        public PanelManager(IComponentContext componentContext)
        {
            _componentContext = componentContext ?? throw new ArgumentNullException(nameof(componentContext));
            _paletteSets = new Dictionary<Type, PaletteSet>();
            _panelInstances = new Dictionary<Type, object>();
        }

        /// <summary>
        /// 显示指定类型的面板
        /// </summary>
        /// <typeparam name="TPanel">面板类型（必须是 WPF UserControl）</typeparam>
        /// <param name="title">面板标题</param>
        /// <param name="guid">面板唯一标识符</param>
        public void ShowPanel<TPanel>(string title, Guid guid) where TPanel : System.Windows.Controls.UserControl
        {
            var panelType = typeof(TPanel);

            // 如果面板已存在，直接显示
            if (_paletteSets.ContainsKey(panelType))
            {
                _paletteSets[panelType].Visible = true;
                return;
            }

            // 创建新的 PaletteSet
            var paletteSet = new PaletteSet(title, guid)
            {
                Size = new System.Drawing.Size(280, 600),
                MinimumSize = new System.Drawing.Size(200, 400),
                DockEnabled = (DockSides)((int)DockSides.Left | (int)DockSides.Right),
                Style = PaletteSetStyles.ShowCloseButton | 
                        PaletteSetStyles.ShowAutoHideButton |
                        PaletteSetStyles.Snappable
            };

            // 创建面板实例（通过依赖注入）
            TPanel panelInstance;
            try
            {
                // 尝试从容器解析
                if (_componentContext.TryResolve<TPanel>(out panelInstance))
                {
                    // 成功从容器解析
                }
                else
                {
                    // 如果 DI 容器中没有注册，尝试手动创建
                    panelInstance = Activator.CreateInstance<TPanel>();
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"无法创建面板实例 {panelType.Name}。请确保面板已在 AutofacModule 中注册。", ex);
            }

            // 添加到 PaletteSet（需要用 ElementHost 包装 WPF UserControl）
            var elementHost = new ElementHost
            {
                AutoSize = true,
                Dock = System.Windows.Forms.DockStyle.Fill,
                Child = panelInstance
            };
            paletteSet.Add(title, elementHost);

            // 保存引用
            _paletteSets[panelType] = paletteSet;
            _panelInstances[panelType] = panelInstance;

            // 显示面板
            paletteSet.Visible = true;
        }

        /// <summary>
        /// 隐藏指定类型的面板
        /// </summary>
        /// <typeparam name="TPanel">面板类型</typeparam>
        public void HidePanel<TPanel>() where TPanel : System.Windows.Controls.UserControl
        {
            var panelType = typeof(TPanel);
            if (_paletteSets.ContainsKey(panelType))
            {
                _paletteSets[panelType].Visible = false;
            }
        }

        /// <summary>
        /// 切换面板显示状态（显示/隐藏）
        /// </summary>
        /// <typeparam name="TPanel">面板类型</typeparam>
        /// <param name="title">面板标题（首次创建时使用）</param>
        /// <param name="guid">面板唯一标识符（首次创建时使用）</param>
        public void TogglePanel<TPanel>(string title, Guid guid) where TPanel : System.Windows.Controls.UserControl
        {
            var panelType = typeof(TPanel);
            if (_paletteSets.ContainsKey(panelType))
            {
                _paletteSets[panelType].Visible = !_paletteSets[panelType].Visible;
            }
            else
            {
                ShowPanel<TPanel>(title, guid);
            }
        }

        /// <summary>
        /// 获取面板实例
        /// </summary>
        /// <typeparam name="TPanel">面板类型</typeparam>
        /// <returns>面板实例，如果不存在则返回 null</returns>
        public TPanel GetPanelInstance<TPanel>() where TPanel : System.Windows.Controls.UserControl
        {
            var panelType = typeof(TPanel);
            if (_panelInstances.ContainsKey(panelType))
            {
                return (TPanel)_panelInstances[panelType];
            }
            return null;
        }

        /// <summary>
        /// 检查面板是否可见
        /// </summary>
        /// <typeparam name="TPanel">面板类型</typeparam>
        /// <returns>true 如果面板存在且可见，否则 false</returns>
        public bool IsPanelVisible<TPanel>() where TPanel : System.Windows.Controls.UserControl
        {
            var panelType = typeof(TPanel);
            return _paletteSets.ContainsKey(panelType) && _paletteSets[panelType].Visible;
        }

        /// <summary>
        /// 关闭并销毁指定类型的面板
        /// </summary>
        /// <typeparam name="TPanel">面板类型</typeparam>
        public void ClosePanel<TPanel>() where TPanel : System.Windows.Controls.UserControl
        {
            var panelType = typeof(TPanel);
            if (_paletteSets.ContainsKey(panelType))
            {
                var paletteSet = _paletteSets[panelType];
                paletteSet.Visible = false;
                paletteSet.Dispose();
                _paletteSets.Remove(panelType);
                _panelInstances.Remove(panelType);
            }
        }

        /// <summary>
        /// 关闭所有面板
        /// </summary>
        public void CloseAllPanels()
        {
            foreach (var paletteSet in _paletteSets.Values)
            {
                paletteSet.Visible = false;
                paletteSet.Dispose();
            }
            _paletteSets.Clear();
            _panelInstances.Clear();
        }
    }
}


