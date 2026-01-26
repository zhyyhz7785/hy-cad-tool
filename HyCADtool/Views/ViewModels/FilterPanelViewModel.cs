using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Colors;
using HyCADTool.HelpClass;
using HyCADTool.Tools;
using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyCADTool.Config;

namespace HyCADTool.ViewModels
{
    public class FilterPanelViewModel : INotifyPropertyChanged
    {
        //private readonly Document CurrentDocument = Application.DocumentManager.MdiActiveDocument;
        //private readonly Editor Editor = Application.DocumentManager.MdiActiveDocument.Editor;

        private Document CurrentDocument => Application.DocumentManager.MdiActiveDocument;
        private Editor Editor => CurrentDocument.Editor;

        private Entity _selectedEntity;
        private ObjectId[] _userSelectedIds = new ObjectId[0];
        private string _selectedPropertyValue;
        public string SelectedPropertyValue
        {
            get => _selectedPropertyValue;
            set { _selectedPropertyValue = value; OnPropertyChanged(); }
        }

        public ICommand SelectSingleEntityCommand { get; }
        public ICommand SelectCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand AddExpressionFilterCommand { get; }
        public ICommand RemoveExpressionFilterCommand { get; }

        public string SelectedType { get => _selectedType; set { _selectedType = value; OnPropertyChanged(); } }
        private string _selectedType;

        public bool TypeChecked { get => _typeChecked; set { _typeChecked = value; OnPropertyChanged(); } }
        public bool LayerChecked { get => _layerChecked; set { _layerChecked = value; OnPropertyChanged(); } }
        public bool ColorChecked { get => _colorChecked; set { _colorChecked = value; OnPropertyChanged(); } }
        public bool LineWeightChecked { get => _lineWeightChecked; set { _lineWeightChecked = value; OnPropertyChanged(); } }
        public bool LineTypeChecked { get => _lineTypeChecked; set { _lineTypeChecked = value; OnPropertyChanged(); } }
        public bool TransparencyChecked { get => _transparencyChecked; set { _transparencyChecked = value; OnPropertyChanged(); } }

        private bool _typeChecked, _layerChecked, _colorChecked, _lineWeightChecked, _lineTypeChecked, _transparencyChecked;

        public ObservableCollection<string> PropertyFields { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Operators { get; } = new ObservableCollection<string> { "==", "!=", ">", "<", ">=", "<=" };
        public ObservableCollection<string> ExpressionFilters { get; } = new ObservableCollection<string>();
        public string SelectedPropertyField
        {
            get => _selectedPropertyField;
            set
            {
                _selectedPropertyField = value;
                OnPropertyChanged();
                UpdateSelectedPropertyValue(); // 新增方法，提取属性值
            }
        }

        public string SelectedOperator { get => _selectedOperator; set { _selectedOperator = value; OnPropertyChanged(); } }
        public string InputValue { get => _inputValue; set { _inputValue = value; OnPropertyChanged(); } }
        public string SelectedExpressionFilter { get => _selectedExpressionFilter; set { _selectedExpressionFilter = value; OnPropertyChanged(); } }

        private string _selectedPropertyField, _selectedOperator, _inputValue, _selectedExpressionFilter;

        public FilterPanelViewModel()
        {
            SelectSingleEntityCommand = new RelayCommand(SelectSingleEntity);
            SelectCommand = new RelayCommand(SelectWithFilters);
            ResetCommand = new RelayCommand(ResetFilters);
            AddExpressionFilterCommand = new RelayCommand(AddExpressionFilter);
            RemoveExpressionFilterCommand = new RelayCommand(RemoveExpressionFilter);
        }

        private void SelectSingleEntity()
        {
            _selectedEntity = ZTools.SelectSingleEntity();
            if (_selectedEntity != null)
            {
                SelectedType = _selectedEntity.GetType().Name.ToChinese();
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


        private void ResetFilters()
        {
            TypeChecked = LayerChecked = ColorChecked = false;
            LineWeightChecked = LineTypeChecked = TransparencyChecked = false;
            ExpressionFilters.Clear();
        }

        //private void SelectWithFilters()
        //{
        //    PromptSelectionResult res = Editor.GetSelection();
        //    if (res.Status != PromptStatus.OK)
        //    {
        //        Editor.WriteMessage("未选择任何对象\n");
        //        return;
        //    }

        //    _userSelectedIds = res.Value.GetObjectIds();
        //    IEnumerable<ObjectId> ids = _userSelectedIds;

        //    if (TypeChecked) ids = ids.Intersect(GetTypeFilteredIds());
        //    if (LayerChecked) ids = ids.Intersect(GetLayerFilteredIds());
        //    if (ColorChecked) ids = ids.Intersect(GetColorFilteredIds());
        //    if (LineWeightChecked) ids = ids.Intersect(GetLineWeightFilteredIds());
        //    if (LineTypeChecked) ids = ids.Intersect(GetLineTypeFilteredIds());
        //    if (TransparencyChecked) ids = ids.Intersect(GetTransparencyFilteredIds());

        //    foreach (var exp in ExpressionFilters)
        //    {
        //        var parts = exp.Split(' ');
        //        if (parts.Length != 3) continue;
        //        string field = parts[0];
        //        string op = parts[1];
        //        string valStr = parts[2];

        //        ids = ids.Intersect(FilterEntitiesByExpression(_userSelectedIds, field, op, valStr));
        //    }

        //    Editor.SetImpliedSelection(new ObjectId[0]);
        //    Editor.SetImpliedSelection(ids.ToArray());
        //}
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

        private void SelectWithFilters()
        {
            // 清空之前的选择状态
            Editor.SetImpliedSelection(new ObjectId[0]);

            // 获取用户新的选择
            PromptSelectionResult res = Editor.GetSelection();
            if (res.Status != PromptStatus.OK)
            {
                Editor.WriteMessage("未选择任何对象\n");
                // 确保清空用户选择的ID集合
                _userSelectedIds = null;
                return;
            }

            // 更新用户选择的对象ID集合
            _userSelectedIds = res.Value.GetObjectIds();

            // 如果没有选择任何对象，直接返回
            if (_userSelectedIds == null || _userSelectedIds.Length == 0)
            {
                Editor.WriteMessage("未选择任何对象\n");
                return;
            }

            // 应用筛选器
            IEnumerable<ObjectId> ids = _userSelectedIds;

            if (TypeChecked) ids = ids.Intersect(GetTypeFilteredIds());

            if (LayerChecked)  ids = ids.Intersect(GetLayerFilteredIds());

            if (ColorChecked)ids = ids.Intersect(GetColorFilteredIds());

            if (LineWeightChecked)
                ids = ids.Intersect(GetLineWeightFilteredIds());

            if (LineTypeChecked)
                ids = ids.Intersect(GetLineTypeFilteredIds());

            if (TransparencyChecked)
                ids = ids.Intersect(GetTransparencyFilteredIds());

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
            Editor.SetImpliedSelection(finalIds);

            // 输出结果信息
            Editor.WriteMessage($"筛选完成，共选中 {finalIds.Length} 个对象\n");
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
                            return Compare(Convert.ToInt32(value), iv, op);
                        case "Double":
                            double dv = double.Parse(valStr);
                            return Compare(Convert.ToDouble(value), dv, op);
                        case "Boolean":
                            bool bv = bool.Parse(valStr);
                            return Compare(Convert.ToBoolean(value), bv, op);
                        case "String":
                            return Compare(value?.ToString(), valStr, op);
                    }
                }
                catch { }
                return false;
            }, ids);
        }


        private static bool Compare<T>(T a, T b, string op) where T : IComparable<T>
        {
            switch (op)
            {
                case "==": return a.CompareTo(b) == 0;
                case "!=": return a.CompareTo(b) != 0;
                case ">": return a.CompareTo(b) > 0;
                case "<": return a.CompareTo(b) < 0;
                case ">=": return a.CompareTo(b) >= 0;
                case "<=": return a.CompareTo(b) <= 0;
                default: return false;
            }
        }

        private void AddExpressionFilter()
        {
            if (!string.IsNullOrWhiteSpace(SelectedPropertyField)
                && !string.IsNullOrWhiteSpace(SelectedOperator)
                && !string.IsNullOrWhiteSpace(InputValue))
            {
                // 提取 PropertyName（去除 DisplayName 部分）
                string field = ExtractPropertyName(SelectedPropertyField);
                ExpressionFilters.Add($"{field} {SelectedOperator} {InputValue}");
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


        private void RemoveExpressionFilter()
        {
            if (SelectedExpressionFilter != null)
            {
                ExpressionFilters.Remove(SelectedExpressionFilter);
            }
        }

        private IEnumerable<ObjectId> GetTypeFilteredIds()
        {
            if (_selectedEntity == null || string.IsNullOrWhiteSpace(SelectedType)) return Enumerable.Empty<ObjectId>();
            return SelectedType.ToType().GetfilterWithString().Getfilter().SelectWithFilterAll().Intersect(_userSelectedIds);
        }

        private IEnumerable<ObjectId> GetLayerFilteredIds() => _selectedEntity?.Layer.GetLayerFilter().Getfilter().SelectWithFilterAll().Intersect(_userSelectedIds) ?? Enumerable.Empty<ObjectId>();
        private IEnumerable<ObjectId> GetColorFilteredIds() => _selectedEntity?.GetTrueColor().GetEntitiesWithMatchingColorInputIds(CurrentDocument, _userSelectedIds) ?? Enumerable.Empty<ObjectId>();
        private IEnumerable<ObjectId> GetLineWeightFilteredIds() => _selectedEntity?.GetTrueLineWeight().GetLineWeightFilter().Getfilter().SelectWithFilterAll().Intersect(_userSelectedIds) ?? Enumerable.Empty<ObjectId>();
        private IEnumerable<ObjectId> GetLineTypeFilteredIds() => _selectedEntity?.GetTrueLinetype().GetEntitiesWithMatchingLinetype(CurrentDocument, _userSelectedIds) ?? Enumerable.Empty<ObjectId>();
        private IEnumerable<ObjectId> GetTransparencyFilteredIds() => _selectedEntity?.GetTrueTransparency().GetEntitiesWithMatchingTransparency(CurrentDocument, _userSelectedIds) ?? Enumerable.Empty<ObjectId>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
