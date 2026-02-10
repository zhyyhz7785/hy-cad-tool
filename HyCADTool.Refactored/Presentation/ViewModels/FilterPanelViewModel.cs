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
using HyCADTool.Refactored.Infrastructure.AutoCAD.Extensions;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Metadata;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Selection;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using TypeNameConverter = HyCADTool.Refactored.Infrastructure.AutoCAD.Extensions.TypeNameConverter;

namespace HyCADTool.Refactored.Presentation.ViewModels
{
    /// <summary>
    /// FilterPanel 的独立 ViewModel
    /// 从旧项目 HyCADtool/Views/ViewModels/FilterPanelViewModel.cs 迁移
    /// 提供图形过滤选择功能
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

        #region 构造函数

        public FilterPanelViewModel()
        {
            SelectSingleEntityCommand = new RelayCommand(SelectSingleEntity);
            SelectCommand = new RelayCommand(SelectWithFilters);
            ResetCommand = new RelayCommand(ResetFilters);
            AddExpressionFilterCommand = new RelayCommand(AddExpressionFilter);
            RemoveExpressionFilterCommand = new RelayCommand(RemoveExpressionFilter);
        }

        #endregion

        #region 命令实现

        private void SelectSingleEntity()
        {
            _selectedEntity = SelectionHelper.SelectSingleEntity();
            if (_selectedEntity != null)
            {
                // 使用 TypeNameConverter 将类型名转换为中文
                SelectedType = TypeNameConverter.ToChinese(_selectedEntity.GetType().Name);
                TypeChecked = true;

                // 绑定可筛选属性列表（DisplayName 中文名）
                PropertyFields.Clear();
                var typeName = _selectedEntity.GetType().Name;
                var props = FilterablePropertyMetadataProvider.GetMetadataList()
                                .Where(p => p.EntityType == typeName)
                                .Select(p => $"{p.DisplayName} ({p.PropertyName})");

                foreach (var item in props)
                    PropertyFields.Add(item);
            }
        }

        private void SelectWithFilters()
        {
            // 清空之前的选择状态
            Editor?.SetImpliedSelection(new ObjectId[0]);

            // 获取用户新的选择
            PromptSelectionResult res = Editor?.GetSelection();
            if (res == null || res.Status != PromptStatus.OK)
            {
                Editor?.WriteMessage("未选择任何对象\n");
                _userSelectedIds = new ObjectId[0];
                return;
            }

            // 更新用户选择的对象ID集合
            _userSelectedIds = res.Value.GetObjectIds();

            // 如果没有选择任何对象，直接返回
            if (_userSelectedIds == null || _userSelectedIds.Length == 0)
            {
                Editor?.WriteMessage("未选择任何对象\n");
                return;
            }

            // 应用筛选器
            IEnumerable<ObjectId> ids = _userSelectedIds;

            if (TypeChecked) ids = ids.Intersect(GetTypeFilteredIds());
            if (LayerChecked) ids = ids.Intersect(GetLayerFilteredIds());
            if (ColorChecked) ids = ids.Intersect(GetColorFilteredIds());
            if (LineWeightChecked) ids = ids.Intersect(GetLineWeightFilteredIds());
            if (LineTypeChecked) ids = ids.Intersect(GetLineTypeFilteredIds());
            if (TransparencyChecked) ids = ids.Intersect(GetTransparencyFilteredIds());

            // 应用表达式筛选器
            foreach (var exp in ExpressionFilters)
            {
                var parts = exp.Split(' ');
                if (parts.Length != 3) continue;

                string field = parts[0];
                string op = parts[1];
                string valStr = parts[2];

                ids = ids.Intersect(FilterEntitiesByExpression(_userSelectedIds, field, op, valStr));
            }

            // 设置最终的选择结果
            ObjectId[] finalIds = ids.ToArray();
            Editor?.SetImpliedSelection(finalIds);

            // 输出结果信息
            Editor?.WriteMessage($"筛选完成，共选中 {finalIds.Length} 个对象\n");
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
            catch (System.Exception ex)
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

        private IEnumerable<ObjectId> FilterEntitiesByExpression(ObjectId[] ids, string field, string op, string valStr)
        {
            return CurrentDocument.FilterEntitiesBy(e =>
            {
                var props = e.GetFilterableProperties();
                if (!props.ContainsKey(field)) return false;
                var (value, type) = props[field];
                try
                {
                    switch (type)
                    {
                        case "Int32":
                            int iv = int.Parse(valStr);
                            return FilterExtensions.Compare(Convert.ToInt32(value), iv, op);
                        case "Double":
                            double dv = double.Parse(valStr);
                            return FilterExtensions.Compare(Convert.ToDouble(value), dv, op);
                        case "Boolean":
                            bool bv = bool.Parse(valStr);
                            return FilterExtensions.Compare(Convert.ToBoolean(value), bv, op);
                        case "String":
                            return FilterExtensions.Compare(value?.ToString(), valStr, op);
                    }
                }
                catch { }
                return false;
            }, ids);
        }

        private IEnumerable<ObjectId> GetTypeFilteredIds()
        {
            if (_selectedEntity == null || string.IsNullOrWhiteSpace(SelectedType)) 
                return Enumerable.Empty<ObjectId>();
            
            // 使用 TypeNameConverter 将中文转回英文类型名
            string typeName = TypeNameConverter.ToType(SelectedType);
            return typeName.GetfilterWithString().Getfilter().SelectWithFilterAll().Intersect(_userSelectedIds);
        }

        private IEnumerable<ObjectId> GetLayerFilteredIds() => 
            _selectedEntity?.Layer.GetLayerFilter().Getfilter().SelectWithFilterAll().Intersect(_userSelectedIds) 
            ?? Enumerable.Empty<ObjectId>();

        private IEnumerable<ObjectId> GetColorFilteredIds() => 
            _selectedEntity?.GetTrueColor().GetEntitiesWithMatchingColorInputIds(CurrentDocument, _userSelectedIds) 
            ?? Enumerable.Empty<ObjectId>();

        private IEnumerable<ObjectId> GetLineWeightFilteredIds() => 
            _selectedEntity?.GetTrueLineWeight().GetLineWeightFilter().Getfilter().SelectWithFilterAll().Intersect(_userSelectedIds) 
            ?? Enumerable.Empty<ObjectId>();

        private IEnumerable<ObjectId> GetLineTypeFilteredIds() => 
            _selectedEntity?.GetTrueLinetype().GetEntitiesWithMatchingLinetype(CurrentDocument, _userSelectedIds) 
            ?? Enumerable.Empty<ObjectId>();

        private IEnumerable<ObjectId> GetTransparencyFilteredIds() => 
            _selectedEntity?.GetTrueTransparency().GetEntitiesWithMatchingTransparency(CurrentDocument, _userSelectedIds) 
            ?? Enumerable.Empty<ObjectId>();

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
