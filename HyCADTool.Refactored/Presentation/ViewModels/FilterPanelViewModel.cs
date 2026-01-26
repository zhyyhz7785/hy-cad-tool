using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.ViewModels
{
    /// <summary>
    /// FilterPanel 的 ViewModel，实现 MVVM 模式的数据绑定和命令
    /// 注意：此为阶段 5.0.3 的简化版本，仅保留 UI 框架
    /// 业务逻辑（筛选功能）将在后续阶段迁移到 Application/Domain 层
    /// </summary>
    public class FilterPanelViewModel : INotifyPropertyChanged
    {
        private Document CurrentDocument => AcApp.DocumentManager.MdiActiveDocument;
        private Editor Editor => CurrentDocument?.Editor;

        private Entity _selectedEntity;
        private ObjectId[] _userSelectedIds = new ObjectId[0];

        #region 属性

        private string _selectedType;
        public string SelectedType
        {
            get => _selectedType;
            set { _selectedType = value; OnPropertyChanged(); }
        }

        private string _selectedPropertyValue;
        public string SelectedPropertyValue
        {
            get => _selectedPropertyValue;
            set { _selectedPropertyValue = value; OnPropertyChanged(); }
        }

        private bool _typeChecked;
        public bool TypeChecked
        {
            get => _typeChecked;
            set { _typeChecked = value; OnPropertyChanged(); }
        }

        private bool _layerChecked;
        public bool LayerChecked
        {
            get => _layerChecked;
            set { _layerChecked = value; OnPropertyChanged(); }
        }

        private bool _colorChecked;
        public bool ColorChecked
        {
            get => _colorChecked;
            set { _colorChecked = value; OnPropertyChanged(); }
        }

        private bool _lineWeightChecked;
        public bool LineWeightChecked
        {
            get => _lineWeightChecked;
            set { _lineWeightChecked = value; OnPropertyChanged(); }
        }

        private bool _lineTypeChecked;
        public bool LineTypeChecked
        {
            get => _lineTypeChecked;
            set { _lineTypeChecked = value; OnPropertyChanged(); }
        }

        private bool _transparencyChecked;
        public bool TransparencyChecked
        {
            get => _transparencyChecked;
            set { _transparencyChecked = value; OnPropertyChanged(); }
        }

        private string _selectedPropertyField;
        public string SelectedPropertyField
        {
            get => _selectedPropertyField;
            set
            {
                _selectedPropertyField = value;
                OnPropertyChanged();
                UpdateSelectedPropertyValue();
            }
        }

        private string _selectedOperator;
        public string SelectedOperator
        {
            get => _selectedOperator;
            set { _selectedOperator = value; OnPropertyChanged(); }
        }

        private string _inputValue;
        public string InputValue
        {
            get => _inputValue;
            set { _inputValue = value; OnPropertyChanged(); }
        }

        private string _selectedExpressionFilter;
        public string SelectedExpressionFilter
        {
            get => _selectedExpressionFilter;
            set { _selectedExpressionFilter = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> PropertyFields { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Operators { get; } = new ObservableCollection<string> { "==", "!=", ">", "<", ">=", "<=" };
        public ObservableCollection<string> ExpressionFilters { get; } = new ObservableCollection<string>();

        #endregion

        #region 命令

        public ICommand SelectSingleEntityCommand { get; }
        public ICommand SelectCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand AddExpressionFilterCommand { get; }
        public ICommand RemoveExpressionFilterCommand { get; }

        #endregion

        public FilterPanelViewModel()
        {
            SelectSingleEntityCommand = new RelayCommand(SelectSingleEntity);
            SelectCommand = new RelayCommand(SelectWithFilters);
            ResetCommand = new RelayCommand(ResetFilters);
            AddExpressionFilterCommand = new RelayCommand(AddExpressionFilter);
            RemoveExpressionFilterCommand = new RelayCommand(RemoveExpressionFilter);
        }

        #region 命令实现（简化版本，仅输出消息）

        private void SelectSingleEntity()
        {
            Editor?.WriteMessage("\n[FilterPanel] 选择单个图形功能暂未迁移\n");
            Editor?.WriteMessage("提示：此功能将在后续阶段迁移到 Application 层\n");
            
            // TODO: 在阶段 5.1 中迁移到 Application/Domain 层
            // 原逻辑：使用 ZTools.SelectSingleEntity() 获取实体
            // 然后提取实体的可筛选属性列表
        }

        private void SelectWithFilters()
        {
            Editor?.WriteMessage("\n[FilterPanel] 自行选择功能暂未迁移\n");
            Editor?.WriteMessage($"当前筛选器状态：\n");
            Editor?.WriteMessage($"  - 类型过滤：{TypeChecked}\n");
            Editor?.WriteMessage($"  - 图层过滤：{LayerChecked}\n");
            Editor?.WriteMessage($"  - 颜色过滤：{ColorChecked}\n");
            Editor?.WriteMessage($"  - 线宽过滤：{LineWeightChecked}\n");
            Editor?.WriteMessage($"  - 线型过滤：{LineTypeChecked}\n");
            Editor?.WriteMessage($"  - 透明度过滤：{TransparencyChecked}\n");
            Editor?.WriteMessage($"  - 表达式过滤器数量：{ExpressionFilters.Count}\n");

            // TODO: 在阶段 5.1 中迁移到 Application/Domain 层
            // 原逻辑：
            // 1. Editor.GetSelection() 获取用户选择
            // 2. 应用各种筛选器（类型、图层、颜色等）
            // 3. 应用表达式筛选器
            // 4. Editor.SetImpliedSelection() 设置最终选择
        }

        private void ResetFilters()
        {
            TypeChecked = false;
            LayerChecked = false;
            ColorChecked = false;
            LineWeightChecked = false;
            LineTypeChecked = false;
            TransparencyChecked = false;
            ExpressionFilters.Clear();
            SelectedType = string.Empty;
            SelectedPropertyValue = string.Empty;
            SelectedPropertyField = null;
            InputValue = string.Empty;

            Editor?.WriteMessage("\n[FilterPanel] 所有筛选器已重置\n");
        }

        private void AddExpressionFilter()
        {
            if (!string.IsNullOrWhiteSpace(SelectedPropertyField)
                && !string.IsNullOrWhiteSpace(SelectedOperator)
                && !string.IsNullOrWhiteSpace(InputValue))
            {
                string field = ExtractPropertyName(SelectedPropertyField);
                string expression = $"{field} {SelectedOperator} {InputValue}";
                ExpressionFilters.Add(expression);
                Editor?.WriteMessage($"\n[FilterPanel] 已添加过滤器：{expression}\n");
            }
            else
            {
                Editor?.WriteMessage("\n[FilterPanel] 请完整填写属性、操作符和值\n");
            }
        }

        private void RemoveExpressionFilter()
        {
            if (SelectedExpressionFilter != null)
            {
                ExpressionFilters.Remove(SelectedExpressionFilter);
                Editor?.WriteMessage($"\n[FilterPanel] 已删除过滤器：{SelectedExpressionFilter}\n");
            }
            else
            {
                Editor?.WriteMessage("\n[FilterPanel] 请先选择要删除的过滤器\n");
            }
        }

        #endregion

        #region 辅助方法

        private void UpdateSelectedPropertyValue()
        {
            if (_selectedEntity == null || string.IsNullOrWhiteSpace(SelectedPropertyField))
            {
                SelectedPropertyValue = string.Empty;
                return;
            }

            try
            {
                string propertyName = ExtractPropertyName(SelectedPropertyField);
                var prop = _selectedEntity.GetType().GetProperty(propertyName);
                if (prop != null)
                {
                    var value = prop.GetValue(_selectedEntity);
                    SelectedPropertyValue = value?.ToString() ?? "(null)";
                }
                else
                {
                    SelectedPropertyValue = "(属性不存在)";
                }
            }
            catch (Exception ex)
            {
                SelectedPropertyValue = $"(读取失败: {ex.Message})";
            }
        }

        private string ExtractPropertyName(string field)
        {
            if (field.Contains("(") && field.Contains(")"))
            {
                int start = field.IndexOf('(') + 1;
                int end = field.IndexOf(')');
                return field.Substring(start, end - start).Trim();
            }
            return field;
        }

        #endregion

        #region INotifyPropertyChanged 实现

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion
    }
}

