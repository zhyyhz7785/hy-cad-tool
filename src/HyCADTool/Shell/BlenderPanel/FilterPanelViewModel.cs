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
using HyCADTool.Shared.AutoCAD.Extensions;
using HyCADTool.Shared.AutoCAD.Metadata;
using HyCADTool.Shared.AutoCAD.Selection;
using HyCADTool.Shared.AutoCAD.Selection.Rules;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using TypeNameConverter = HyCADTool.Shared.AutoCAD.Extensions.TypeNameConverter;

namespace HyCADTool.Shell.ViewModels
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
        public ObservableCollection<string> RuleQueryModes { get; } = new ObservableCollection<string> { "条件树", "表达式", "类 SQL" };
        public ObservableCollection<RuleCompletionItem> RuleCompletionItems { get; } = new ObservableCollection<RuleCompletionItem>();
        public ObservableCollection<string> RuleQuickSnippets { get; } = new ObservableCollection<string>
        {
            "同图层",
            "同类型",
            "长度 >",
            "半径区间",
            "文字包含",
            "图层通配",
            "块名 =",
            "有 XData"
        };

        private string _selectedRuleQueryMode = "表达式";
        public string SelectedRuleQueryMode
        {
            get => _selectedRuleQueryMode;
            set
            {
                _selectedRuleQueryMode = value;
                OnPropertyChanged();
                RefreshRuleDiagnostics();
            }
        }

        private string _ruleQueryText;
        public string RuleQueryText
        {
            get => _ruleQueryText;
            set
            {
                _ruleQueryText = value;
                OnPropertyChanged();
                RefreshRuleCompletion();
                RefreshRuleDiagnostics();
            }
        }

        private string _ruleDiagnosticText = "选择样例后可使用快速输入和字段提示。";
        public string RuleDiagnosticText
        {
            get => _ruleDiagnosticText;
            set { _ruleDiagnosticText = value; OnPropertyChanged(); }
        }

        private string _ruleAstPreview = "(尚未生成 Predicate)";
        public string RuleAstPreview
        {
            get => _ruleAstPreview;
            set { _ruleAstPreview = value; OnPropertyChanged(); }
        }

        private string _ruleExportPreview = "点击导出按钮生成 C# / WHERE / 伪 Python 对照。";
        public string RuleExportPreview
        {
            get => _ruleExportPreview;
            set { _ruleExportPreview = value; OnPropertyChanged(); }
        }

        #endregion

        #region 命令

        public ICommand SelectSingleEntityCommand { get; }
        public ICommand SelectCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand AddExpressionFilterCommand { get; }
        public ICommand RemoveExpressionFilterCommand { get; }
        public ICommand InsertRuleSnippetCommand { get; }
        public ICommand ApplyRuleQueryCommand { get; }
        public ICommand RefreshRuleCompletionCommand { get; }
        public ICommand ExportRuleQueryCommand { get; }
        public ICommand InsertRuleCompletionCommand { get; }

        #endregion

        #region 构造函数

        public FilterPanelViewModel()
        {
            SelectSingleEntityCommand = new RelayCommand(SelectSingleEntity);
            SelectCommand = new RelayCommand(SelectWithFilters);
            ResetCommand = new RelayCommand(ResetFilters);
            AddExpressionFilterCommand = new RelayCommand(AddExpressionFilter);
            RemoveExpressionFilterCommand = new RelayCommand(RemoveExpressionFilter);
            InsertRuleSnippetCommand = new RelayCommand<string>(InsertRuleSnippet);
            ApplyRuleQueryCommand = new RelayCommand(ApplyRuleQuery);
            RefreshRuleCompletionCommand = new RelayCommand(RefreshRuleCompletion);
            ExportRuleQueryCommand = new RelayCommand<string>(ExportRuleQuery);
            InsertRuleCompletionCommand = new RelayCommand<RuleCompletionItem>(InsertRuleCompletion);
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
                var props = RulePropertyCatalog.GetDescriptors(_selectedEntity)
                                .Select(p => $"{p.DisplayName} ({p.PropertyName})");

                foreach (var item in props)
                    PropertyFields.Add(item);

                RefreshRuleCompletion();
                RefreshRuleDiagnostics();
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
                ids = ids.Intersect(FilterEntitiesByRule(ids.ToArray(), exp));
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
            RuleQueryText = string.Empty;
            RuleCompletionItems.Clear();
            RuleAstPreview = "(尚未生成 Predicate)";
            RuleDiagnosticText = "选择样例后可使用快速输入和字段提示。";
            RuleExportPreview = "点击导出按钮生成 C# / WHERE / 伪 Python 对照。";
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

        private void InsertRuleSnippet(string snippet)
        {
            if (string.IsNullOrWhiteSpace(snippet))
                return;

            RuleQueryText = BuildSnippet(snippet);
            RefreshRuleDiagnostics();
        }

        private void InsertRuleCompletion(RuleCompletionItem item)
        {
            RuleQueryText = RuleCompletionProvider.ApplyCompletion(RuleQueryText, item);
            RefreshRuleDiagnostics();
        }

        private void ApplyRuleQuery()
        {
            if (string.IsNullOrWhiteSpace(RuleQueryText))
            {
                RuleDiagnosticText = "规则查询为空。";
                return;
            }

            if (!RuleQueryEvaluator.CanParse(RuleQueryText))
            {
                RuleDiagnosticText = "当前规则暂不能解析，请检查字段名、操作符和值。";
                return;
            }

            var expression = RuleQueryText.Trim();
            ExpressionFilters.Add(expression);
            RuleAstPreview = RuleQueryEvaluator.Preview(expression);
            RuleDiagnosticText = $"已添加规则：{expression}";
            Editor?.WriteMessage($"\n[FilterPanel] 已添加规则查询：{expression}\n");
        }

        private void ExportRuleQuery(string exportKind)
        {
            if (string.IsNullOrWhiteSpace(RuleQueryText))
            {
                RuleExportPreview = "规则查询为空，无法导出。";
                return;
            }

            if (!RuleQueryEvaluator.CanParse(RuleQueryText))
            {
                RuleExportPreview = "当前规则暂不能解析，修正后再导出。";
                return;
            }

            RuleExportPreview = RuleQueryExporter.Export(RuleQueryText, exportKind);
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
                var descriptor = RulePropertyCatalog.GetDescriptors(_selectedEntity, includeAdvanced: true)
                    .FirstOrDefault(p => string.Equals(p.PropertyName, propertyName, StringComparison.OrdinalIgnoreCase));
                if (descriptor != null)
                {
                    var value = descriptor.GetValue(_selectedEntity);
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

        private void RefreshRuleCompletion()
        {
            RuleCompletionItems.Clear();

            foreach (var item in RuleCompletionProvider.GetCompletions(_selectedEntity, RuleQueryText, includeAdvanced: true))
                RuleCompletionItems.Add(item);
        }

        private void RefreshRuleDiagnostics()
        {
            if (RuleCompletionItems.Count == 0)
                RefreshRuleCompletion();

            if (string.IsNullOrWhiteSpace(RuleQueryText))
            {
                RuleDiagnosticText = _selectedEntity == null
                    ? "选择样例后可使用快速输入和字段提示。"
                    : "可输入字段名，或点击快速输入片段。";
                RuleAstPreview = "(尚未生成 Predicate)";
                return;
            }

            RuleAstPreview = BuildAstPreview(RuleQueryText);
            RuleDiagnosticText = DiagnoseRuleText(RuleQueryText);
        }

        private string BuildSnippet(string snippet)
        {
            var layer = _selectedEntity?.Layer ?? "0";
            var dxf = _selectedEntity?.GetRXClass()?.DxfName ?? "LINE";
            var typeName = _selectedEntity?.GetType().Name ?? string.Empty;

            switch (snippet)
            {
                case "同图层":
                    return SelectedRuleQueryMode == "类 SQL" ? $"Layer = '{layer}'" : $"Layer == \"{layer}\"";
                case "同类型":
                    return SelectedRuleQueryMode == "类 SQL" ? $"DxfType = '{dxf}'" : $"DxfType == \"{dxf}\"";
                case "长度 >":
                    return "Length > 100";
                case "半径区间":
                    return SelectedRuleQueryMode == "类 SQL" ? "Radius BETWEEN 100 AND 300" : "Radius between 100 and 300";
                case "文字包含":
                    return SelectedRuleQueryMode == "类 SQL" ? "TextString LIKE '%说明%'" : "TextString contains \"说明\"";
                case "图层通配":
                    return SelectedRuleQueryMode == "类 SQL" ? "Layer LIKE '*-road-*'" : "Layer like \"*-road-*\"";
                case "块名 =":
                    return SelectedRuleQueryMode == "类 SQL" ? "BlockName = 'A1'" : "BlockName == \"A1\"";
                case "有 XData":
                    return "HasXData(\"HYROAD\")";
                default:
                    return typeName == "Circle" ? "Radius > 100" : "Layer == \"0\"";
            }
        }

        private string NormalizeRuleQueryToLegacyExpression(string query)
        {
            var text = (query ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text) || text.Contains("&&") || text.Contains("||") || text.Contains("("))
                return null;

            if (SelectedRuleQueryMode == "类 SQL")
            {
                text = text.Replace(" = ", " == ");
                var andIndex = text.IndexOf(" AND ", StringComparison.OrdinalIgnoreCase);
                if (andIndex >= 0)
                    text = text.Substring(0, andIndex).Trim();
            }

            var normalized = text
                .Replace(" contains ", " contains ")
                .Replace(" like ", " contains ");

            var parts = normalized.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3)
                return null;

            var fieldExists = _selectedEntity != null && RulePropertyCatalog.GetDescriptors(_selectedEntity, includeAdvanced: true)
                .Any(p => string.Equals(p.PropertyName, parts[0], StringComparison.OrdinalIgnoreCase));

            if (!fieldExists)
                return null;

            return $"{parts[0]} {parts[1]} {TrimQuotes(parts[2])}";
        }

        private string BuildAstPreview(string query)
        {
            return RuleQueryEvaluator.Preview(query);
        }

        private string DiagnoseRuleText(string query)
        {
            if (_selectedEntity == null)
                return "请先选择样例对象，才能提供字段提示。";

            if (query.TrimStart().StartsWith("Has", StringComparison.OrdinalIgnoreCase))
                return "函数/组合规则将在 Predicate 引擎中执行；当前可预览。";

            var descriptors = RulePropertyCatalog.GetDescriptors(_selectedEntity, includeAdvanced: true);
            var fields = RuleQueryEvaluator.GetFieldNames(query);
            if (fields.Count == 0)
                return "暂未识别出字段条件，请使用：Layer == \"0\"、Length > 100、Radius between 100 and 300。";

            var unknownField = fields.FirstOrDefault(field =>
                !descriptors.Any(p => string.Equals(p.PropertyName, field, StringComparison.OrdinalIgnoreCase)));
            if (!string.IsNullOrWhiteSpace(unknownField))
            {
                var suggestion = descriptors.FirstOrDefault(p => p.PropertyName.StartsWith(unknownField, StringComparison.OrdinalIgnoreCase));
                return suggestion != null
                    ? $"字段不存在，是否为 {suggestion.PropertyName}？"
                    : $"字段不存在：{unknownField}";
            }

            return RuleQueryEvaluator.CanParse(query)
                ? "规则语法可识别；可添加到过滤器并参与筛选。"
                : "字段可识别，但操作符或值暂不能解析。";
        }

        private string TrimQuotes(string value)
        {
            return value?.Trim().Trim('"', '\'') ?? string.Empty;
        }

        private IEnumerable<ObjectId> FilterEntitiesByRule(ObjectId[] ids, string query)
        {
            return RuleQueryEvaluator.Filter(CurrentDocument?.Database, ids, query);
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
            _selectedEntity?.GetTrueLineWeight().GetEntitiesWithMatchingLineWeight(CurrentDocument, _userSelectedIds) 
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
