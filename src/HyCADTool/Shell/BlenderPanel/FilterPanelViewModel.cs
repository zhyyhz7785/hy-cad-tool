using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Shared.AutoCAD.Entities;
using HyCADTool.Shared.AutoCAD.Metadata;
using HyCADTool.Shared.AutoCAD.Selection;
using HyCADTool.Shared.AutoCAD.Selection.Rules;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using TypeNameConverter = HyCADTool.Shared.AutoCAD.Extensions.TypeNameConverter;

namespace HyCADTool.Shell.ViewModels
{
    /// <summary>
    /// FilterPanel 的独立 ViewModel
    /// </summary>
    public class FilterPanelViewModel : INotifyPropertyChanged
    {
        private Document CurrentDocument => AcApp.DocumentManager.MdiActiveDocument;
        private Editor Editor => CurrentDocument?.Editor;

        private ObjectId _selectedEntityId = ObjectId.Null;
        private SampleEntitySnapshot _sampleSnapshot;
        private ObjectId[] _userSelectedIds = new ObjectId[0];
        private ObjectId[] _lastFilterResultIds = new ObjectId[0];

        private static readonly string[] NumericOperators = { "==", "!=", ">", "<", ">=", "<=" };
        private static readonly string[] TextOperators = { "==", "!=", "contains", "like" };
        private static readonly string[] BooleanOperators = { "==", "!=" };

        /// <summary>与公共属性勾选框语义重复，不在属性值过滤器列表中显示。</summary>
        private static readonly HashSet<string> HiddenPanelPropertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "DxfType",
            "EntityType",
            "Layer",
            "TrueColor",
            "ColorSource",
            "TrueLineWeight",
            "TrueLinetype",
            "TrueTransparency"
        };

        private sealed class SampleEntitySnapshot
        {
            public string TypeName { get; set; }
            public string DxfName { get; set; }
            public string Layer { get; set; }
            public Color TrueColor { get; set; }
            public int TrueLineWeight { get; set; }
            public ObjectId TrueLinetypeId { get; set; }
            public int TrueTransparency { get; set; }
        }

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

        private string _selectedOperator = "==";
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

        private string _ruleConditionsPreview = "（尚未添加条件，请在上方选择属性并点击「添加条件」）";
        public string RuleConditionsPreview
        {
            get => _ruleConditionsPreview;
            set { _ruleConditionsPreview = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> PropertyFields { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Operators { get; } = new ObservableCollection<string>(NumericOperators);
        public ObservableCollection<string> Connectors { get; } = new ObservableCollection<string> { "与", "或" };
        public ObservableCollection<RuleConditionItem> RuleConditions { get; } = new ObservableCollection<RuleConditionItem>();
        public ObservableCollection<SelectionSetSlot> SelectionSets { get; } = new ObservableCollection<SelectionSetSlot>();
        public ObservableCollection<string> SetOperators { get; } = new ObservableCollection<string> { "替换", "叠加", "减去", "筛选" };

        private SelectionSetSlot _setOperandLeft;
        public SelectionSetSlot SetOperandLeft
        {
            get => _setOperandLeft;
            set { _setOperandLeft = value; OnPropertyChanged(); RefreshSetOperationPreview(); }
        }

        private SelectionSetSlot _setOperandRight;
        public SelectionSetSlot SetOperandRight
        {
            get => _setOperandRight;
            set { _setOperandRight = value; OnPropertyChanged(); RefreshSetOperationPreview(); }
        }

        private string _selectedSetOperator = "叠加";
        public string SelectedSetOperator
        {
            get => _selectedSetOperator;
            set
            {
                _selectedSetOperator = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSetOperandRightEnabled));
                RefreshSetOperationPreview();
            }
        }

        public bool IsSetOperandRightEnabled => SelectedSetOperator != "替换";

        private string _setOperationPreview = "（请选择集合与运算符）";
        public string SetOperationPreview
        {
            get => _setOperationPreview;
            set { _setOperationPreview = value; OnPropertyChanged(); }
        }

        #endregion

        #region 命令

        public ICommand SelectSingleEntityCommand { get; }
        public ICommand SelectCommand { get; }
        public ICommand AddConditionCommand { get; }
        public ICommand RemoveConditionCommand { get; }
        public ICommand SlotClickCommand { get; }
        public ICommand SlotClearCommand { get; }
        public ICommand SelectSetOperatorCommand { get; }
        public ICommand ApplySetOperationCommand { get; }

        #endregion

        #region 构造函数

        public FilterPanelViewModel()
        {
            RuleConditions.CollectionChanged += (_, __) => OnRuleConditionsChanged();

            for (var i = 1; i <= 5; i++)
            {
                var slot = new SelectionSetSlot(i);
                slot.PropertyChanged += OnSelectionSetSlotChanged;
                SelectionSets.Add(slot);
            }

            SelectSingleEntityCommand = new RelayCommand(SelectSingleEntity);
            SelectCommand = new RelayCommand(SelectWithFilters);
            AddConditionCommand = new RelayCommand(AddCondition);
            RemoveConditionCommand = new RelayCommand<RuleConditionItem>(RemoveCondition);
            SlotClickCommand = new RelayCommand<SelectionSetSlot>(OnSlotClick);
            SlotClearCommand = new RelayCommand<SelectionSetSlot>(OnSlotClear);
            SelectSetOperatorCommand = new RelayCommand<string>(SelectSetOperator);
            ApplySetOperationCommand = new RelayCommand(ApplySetOperation);
        }

        #endregion

        #region 命令实现

        private void SelectSingleEntity()
        {
            var entityId = SelectionHelper.SelectSingleEntity();
            if (entityId.IsNull)
                return;

            if (!CaptureSampleSnapshot(entityId))
            {
                Editor?.WriteMessage("\n[FilterPanel] 无法读取样例对象属性\n");
                return;
            }

            ResetFilterState();
            SelectedType = TypeNameConverter.ToChinese(_sampleSnapshot.TypeName);
            TypeChecked = true;
            RebuildPropertyFields();
        }

        private void SelectWithFilters()
        {
            Editor?.SetImpliedSelection(new ObjectId[0]);

            PromptSelectionResult res = Editor?.GetSelection();
            if (res == null || res.Status != PromptStatus.OK)
            {
                Editor?.WriteMessage("未选择任何对象\n");
                _userSelectedIds = new ObjectId[0];
                return;
            }

            _userSelectedIds = res.Value.GetObjectIds();
            if (_userSelectedIds == null || _userSelectedIds.Length == 0)
            {
                Editor?.WriteMessage("未选择任何对象\n");
                return;
            }

            var doc = CurrentDocument;
            if (doc == null)
                return;

            var conditions = RuleConditions.ToList();
            var filteredIds = new List<ObjectId>();

            using (var docLock = doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                foreach (var id in _userSelectedIds)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead, false) is Entity ent))
                        continue;

                    if (!PassesCommonFilters(ent, tr))
                        continue;

                    if (!PassesRuleConditions(ent, conditions))
                        continue;

                    filteredIds.Add(id);
                }

                tr.Commit();
            }

            var finalIds = SanitizeIds(filteredIds.ToArray());
            _lastFilterResultIds = finalIds;
            Editor?.SetImpliedSelection(finalIds);
            Editor?.WriteMessage($"筛选完成，共选中 {finalIds.Length} 个对象\n");
        }

        private void OnSlotClick(SelectionSetSlot slot)
        {
            if (slot == null)
                return;

            if (!slot.IsEmpty)
            {
                var activeIds = SanitizeIds(slot.Ids);
                Editor?.SetImpliedSelection(activeIds);
                Editor?.WriteMessage($"\n[FilterPanel] 已激活槽位 {slot.Index}，共 {activeIds.Length} 个\n");
                return;
            }

            var idsToStore = GetCurrentImpliedSelection();
            if (idsToStore.Length == 0)
                idsToStore = _lastFilterResultIds ?? Array.Empty<ObjectId>();

            if (idsToStore.Length == 0)
            {
                Editor?.WriteMessage("\n[FilterPanel] 请先点击「选择过滤范围」获得筛选结果\n");
                return;
            }

            var storedIds = SanitizeIds(idsToStore);
            slot.SetIds(storedIds);
            Editor?.WriteMessage($"\n[FilterPanel] 已存储到槽位 {slot.Index}，共 {storedIds.Length} 个\n");
        }

        private void OnSlotClear(SelectionSetSlot slot)
        {
            if (slot == null || slot.IsEmpty)
                return;

            slot.Clear();
            ClearOperandIfMatches(slot);
            Editor?.WriteMessage($"\n[FilterPanel] 槽位 {slot.Index} 已清空\n");
        }

        private void SelectSetOperator(string op)
        {
            if (string.IsNullOrWhiteSpace(op))
                return;

            SelectedSetOperator = op;
        }

        private void ApplySetOperation()
        {
            if (SetOperandLeft == null || SetOperandLeft.IsEmpty)
            {
                Editor?.WriteMessage("\n[FilterPanel] 请选择非空的集合 A\n");
                return;
            }

            if (SelectedSetOperator != "替换")
            {
                if (SetOperandRight == null || SetOperandRight.IsEmpty)
                {
                    Editor?.WriteMessage("\n[FilterPanel] 请选择非空的集合 B\n");
                    return;
                }
            }

            var leftIds = SanitizeIds(SetOperandLeft.Ids);
            var rightIds = SetOperandRight == null ? Array.Empty<ObjectId>() : SanitizeIds(SetOperandRight.Ids);
            var result = CombineIdArrays(leftIds, rightIds, SelectedSetOperator);

            Editor?.SetImpliedSelection(result);
            Editor?.WriteMessage($"\n[FilterPanel] 运算完成：{SetOperationPreview}，共 {result.Length} 个\n");
        }

        private void OnSelectionSetSlotChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SelectionSetSlot.IsEmpty)
                || e.PropertyName == nameof(SelectionSetSlot.Count))
            {
                if (sender is SelectionSetSlot slot && slot.IsEmpty)
                    ClearOperandIfMatches(slot);

                RefreshSetOperationPreview();
            }
        }

        private void ClearOperandIfMatches(SelectionSetSlot slot)
        {
            if (SetOperandLeft == slot)
                SetOperandLeft = null;
            if (SetOperandRight == slot)
                SetOperandRight = null;
        }

        private void RefreshSetOperationPreview()
        {
            if (SetOperandLeft == null)
            {
                SetOperationPreview = "（请选择集合与运算符）";
                return;
            }

            if (SelectedSetOperator == "替换")
            {
                SetOperationPreview = SetOperandLeft.IsEmpty
                    ? "（集合 A 为空）"
                    : SetOperandLeft.Index.ToString();
                return;
            }

            var symbol = GetSetOperatorSymbol(SelectedSetOperator);
            if (SetOperandRight == null)
            {
                SetOperationPreview = $"{SetOperandLeft.Index} {symbol} ?";
                return;
            }

            SetOperationPreview = $"{SetOperandLeft.Index} {symbol} {SetOperandRight.Index}";
        }

        private static string GetSetOperatorSymbol(string op)
        {
            switch (op)
            {
                case "叠加": return "∪";
                case "减去": return "−";
                case "筛选": return "∩";
                default: return "=";
            }
        }

        private void ResetFilterState()
        {
            TypeChecked = false;
            LayerChecked = false;
            ColorChecked = false;
            LineWeightChecked = false;
            LineTypeChecked = false;
            TransparencyChecked = false;
            RuleConditions.Clear();
            SelectedPropertyValue = string.Empty;
            SelectedPropertyField = null;
            InputValue = string.Empty;
            SelectedOperator = "==";
            RefreshRuleConditionsPreview();
        }

        private void AddCondition()
        {
            if (string.IsNullOrWhiteSpace(SelectedPropertyField)
                || string.IsNullOrWhiteSpace(SelectedOperator)
                || string.IsNullOrWhiteSpace(InputValue))
            {
                Editor?.WriteMessage("\n[FilterPanel] 请完整填写属性、操作符和值\n");
                return;
            }

            var propertyName = ExtractPropertyName(SelectedPropertyField);
            var fieldDisplay = ExtractFieldDisplayName(SelectedPropertyField);

            var item = new RuleConditionItem
            {
                FieldDisplay = fieldDisplay,
                PropertyName = propertyName,
                Operator = SelectedOperator,
                Value = InputValue.Trim(),
                Connector = "与"
            };
            item.PropertyChanged += OnConditionItemPropertyChanged;

            RuleConditions.Add(item);
            Editor?.WriteMessage($"\n[FilterPanel] 已添加条件：{item.DisplayText}\n");
        }

        private void RemoveCondition(RuleConditionItem item)
        {
            if (item == null || !RuleConditions.Contains(item))
                return;

            item.PropertyChanged -= OnConditionItemPropertyChanged;
            RuleConditions.Remove(item);
            Editor?.WriteMessage($"\n[FilterPanel] 已删除条件：{item.DisplayText}\n");
        }

        #endregion

        #region 辅助方法

        private void OnRuleConditionsChanged()
        {
            UpdateConditionRowFlags();
            RefreshRuleConditionsPreview();
        }

        private void OnConditionItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RuleConditionItem.Connector)
                || e.PropertyName == nameof(RuleConditionItem.DisplayText))
            {
                RefreshRuleConditionsPreview();
            }
        }

        private void UpdateConditionRowFlags()
        {
            for (var i = 0; i < RuleConditions.Count; i++)
                RuleConditions[i].IsLast = i == RuleConditions.Count - 1;
        }

        private void RefreshRuleConditionsPreview()
        {
            if (RuleConditions.Count == 0)
            {
                RuleConditionsPreview = "（尚未添加条件，请在上方选择属性并点击「添加条件」）";
                return;
            }

            var parts = new List<string>();
            for (var i = 0; i < RuleConditions.Count; i++)
            {
                var condition = RuleConditions[i];
                parts.Add(condition.DisplayText);
                if (i < RuleConditions.Count - 1)
                    parts.Add(condition.Connector);
            }

            RuleConditionsPreview = string.Join(" ", parts);
        }

        private static bool PassesRuleConditions(Entity entity, IReadOnlyList<RuleConditionItem> conditions)
        {
            if (conditions == null || conditions.Count == 0)
                return true;

            var orGroups = new List<List<RuleConditionItem>>();
            var currentGroup = new List<RuleConditionItem> { conditions[0] };

            for (var i = 1; i < conditions.Count; i++)
            {
                if (string.Equals(conditions[i - 1].Connector, "或", StringComparison.Ordinal))
                {
                    orGroups.Add(currentGroup);
                    currentGroup = new List<RuleConditionItem>();
                }

                currentGroup.Add(conditions[i]);
            }

            orGroups.Add(currentGroup);

            return orGroups.Any(group => group.All(c =>
                RuleQueryEvaluator.MatchesCondition(entity, c.PropertyName, c.Operator, c.Value)));
        }

        private void RebuildPropertyFields()
        {
            PropertyFields.Clear();
            if (_sampleSnapshot == null)
                return;

            var indexed = RulePropertyCatalog.GetDescriptors(_sampleSnapshot.TypeName)
                .Select((descriptor, index) => new { descriptor, index })
                .Where(x => !HiddenPanelPropertyNames.Contains(x.descriptor.PropertyName))
                .OrderBy(x => x.descriptor.EntityType == "*" ? 1 : 0)
                .ThenBy(x => x.index)
                .Select(x => x.descriptor);

            foreach (var descriptor in indexed)
                PropertyFields.Add($"{descriptor.DisplayName} ({descriptor.PropertyName})");
        }

        private bool CaptureSampleSnapshot(ObjectId entityId)
        {
            var doc = CurrentDocument;
            if (doc == null || entityId.IsNull)
                return false;

            try
            {
                using (var docLock = doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    if (!(tr.GetObject(entityId, OpenMode.ForRead, false) is Entity ent))
                        return false;

                    _selectedEntityId = entityId;
                    _sampleSnapshot = new SampleEntitySnapshot
                    {
                        TypeName = ent.GetType().Name,
                        DxfName = ent.GetRXClass()?.DxfName ?? string.Empty,
                        Layer = ent.Layer,
                        TrueColor = EntityAppearanceResolver.GetTrueColor(ent, tr),
                        TrueLineWeight = EntityAppearanceResolver.GetTrueLineWeight(ent, tr),
                        TrueLinetypeId = EntityAppearanceResolver.GetTrueLinetype(ent, tr),
                        TrueTransparency = EntityAppearanceResolver.GetTrueTransparency(ent, tr)
                    };
                    tr.Commit();
                    return true;
                }
            }
            catch
            {
                _selectedEntityId = ObjectId.Null;
                _sampleSnapshot = null;
                return false;
            }
        }

        private bool TryReadSelectedEntity<T>(out T result, Func<Entity, T> read)
        {
            result = default;
            var doc = CurrentDocument;
            if (doc == null || _selectedEntityId.IsNull || !_selectedEntityId.IsValid || _selectedEntityId.IsErased)
                return false;

            try
            {
                using (var docLock = doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    if (!(tr.GetObject(_selectedEntityId, OpenMode.ForRead, false) is Entity ent))
                        return false;

                    result = read(ent);
                    tr.Commit();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private bool PassesCommonFilters(Entity ent, Transaction tr)
        {
            var needsSnapshot = TypeChecked || LayerChecked || ColorChecked
                || LineWeightChecked || LineTypeChecked || TransparencyChecked;
            if (needsSnapshot && _sampleSnapshot == null)
                return false;

            if (TypeChecked)
            {
                var typeName = ent.GetType().Name;
                var dxfName = ent.GetRXClass()?.DxfName ?? string.Empty;
                if (!string.Equals(typeName, _sampleSnapshot.TypeName, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(dxfName, _sampleSnapshot.DxfName, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            if (LayerChecked && !string.Equals(ent.Layer, _sampleSnapshot.Layer, StringComparison.OrdinalIgnoreCase))
                return false;

            if (ColorChecked
                && !EntityAppearanceResolver.ColorsEqual(
                    EntityAppearanceResolver.GetTrueColor(ent, tr),
                    _sampleSnapshot.TrueColor))
            {
                return false;
            }

            if (LineWeightChecked
                && EntityAppearanceResolver.GetTrueLineWeight(ent, tr) != _sampleSnapshot.TrueLineWeight)
            {
                return false;
            }

            if (LineTypeChecked
                && EntityAppearanceResolver.GetTrueLinetype(ent, tr) != _sampleSnapshot.TrueLinetypeId)
            {
                return false;
            }

            if (TransparencyChecked
                && EntityAppearanceResolver.GetTrueTransparency(ent, tr) != _sampleSnapshot.TrueTransparency)
            {
                return false;
            }

            return true;
        }

        private void UpdateSelectedPropertyValue()
        {
            if (_selectedEntityId.IsNull || string.IsNullOrWhiteSpace(SelectedPropertyField))
            {
                SelectedPropertyValue = string.Empty;
                InputValue = string.Empty;
                return;
            }

            try
            {
                var propertyName = ExtractPropertyName(SelectedPropertyField);
                if (!TryReadSelectedEntity(out RulePropertyDescriptor descriptor, ent =>
                    {
                        return RulePropertyCatalog.GetDescriptors(ent, includeAdvanced: true)
                            .FirstOrDefault(p => string.Equals(p.PropertyName, propertyName, StringComparison.OrdinalIgnoreCase));
                    }))
                {
                    SelectedPropertyValue = "(读取失败)";
                    InputValue = string.Empty;
                    return;
                }

                if (descriptor == null)
                {
                    SelectedPropertyValue = "(属性不存在)";
                    InputValue = string.Empty;
                    return;
                }

                RebuildOperatorsForType(descriptor.PropertyType);

                if (!TryReadSelectedEntity(out object value, ent => descriptor.GetValue(ent)))
                {
                    SelectedPropertyValue = "(读取失败)";
                    InputValue = string.Empty;
                    return;
                }

                SelectedPropertyValue = value?.ToString() ?? "(null)";
                InputValue = FormatFilterInputValue(value, descriptor.PropertyType);
            }
            catch (System.Exception ex)
            {
                SelectedPropertyValue = $"(读取失败: {ex.Message})";
                InputValue = string.Empty;
            }
        }

        private void RebuildOperatorsForType(string propertyType)
        {
            var ops = GetOperatorsForPropertyType(propertyType);
            Operators.Clear();
            foreach (var op in ops)
                Operators.Add(op);

            if (!Operators.Contains(SelectedOperator))
                SelectedOperator = "==";
        }

        private static IEnumerable<string> GetOperatorsForPropertyType(string propertyType)
        {
            if (IsNumericPropertyType(propertyType))
                return NumericOperators;

            if (string.Equals(propertyType, "Boolean", StringComparison.OrdinalIgnoreCase))
                return BooleanOperators;

            return TextOperators;
        }

        private static string FormatFilterInputValue(object value, string propertyType)
        {
            if (value == null)
                return string.Empty;

            if (IsNumericPropertyType(propertyType))
            {
                if (value is IFormattable formattable)
                    return formattable.ToString("0.######", CultureInfo.InvariantCulture);
                return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            }

            if (string.Equals(propertyType, "Boolean", StringComparison.OrdinalIgnoreCase))
                return value.ToString().ToLowerInvariant();

            var text = value.ToString() ?? string.Empty;
            return "\"" + text.Replace("\"", "\\\"") + "\"";
        }

        private static bool IsNumericPropertyType(string propertyType)
        {
            return string.Equals(propertyType, "Double", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyType, "Int32", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyType, "Int64", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyType, "Single", StringComparison.OrdinalIgnoreCase);
        }

        private static string ExtractPropertyName(string field)
        {
            var start = field.LastIndexOf('(');
            var end = field.LastIndexOf(')');
            if (start >= 0 && end > start)
                return field.Substring(start + 1, end - start - 1).Trim();
            return field;
        }

        private static string ExtractFieldDisplayName(string field)
        {
            var start = field.LastIndexOf('(');
            if (start > 0)
                return field.Substring(0, start).Trim();
            return field;
        }

        private ObjectId[] GetCurrentImpliedSelection()
        {
            var res = Editor?.SelectImplied();
            if (res?.Status == PromptStatus.OK && res.Value != null)
                return SanitizeIds(res.Value.GetObjectIds());

            return Array.Empty<ObjectId>();
        }

        private ObjectId[] SanitizeIds(ObjectId[] ids)
        {
            if (ids == null || ids.Length == 0)
                return Array.Empty<ObjectId>();

            var doc = CurrentDocument;
            if (doc == null)
                return ids.Where(id => id.IsValid && !id.IsNull).ToArray();

            var valid = new List<ObjectId>();
            using (var docLock = doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                foreach (var id in ids)
                {
                    if (!id.IsValid || id.IsNull || id.IsErased)
                        continue;

                    if (tr.GetObject(id, OpenMode.ForRead, false) is Entity)
                        valid.Add(id);
                }

                tr.Commit();
            }

            return valid.ToArray();
        }

        private static ObjectId[] CombineIdArrays(ObjectId[] left, ObjectId[] right, string op)
        {
            switch (op)
            {
                case "叠加":
                {
                    var set = new HashSet<ObjectId>(left);
                    foreach (var id in right)
                        set.Add(id);
                    return set.ToArray();
                }
                case "减去":
                {
                    var remove = new HashSet<ObjectId>(right);
                    return left.Where(id => !remove.Contains(id)).ToArray();
                }
                case "筛选":
                {
                    var keep = new HashSet<ObjectId>(left);
                    return right.Where(id => keep.Contains(id)).ToArray();
                }
                case "替换":
                default:
                    return left;
            }
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
