using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.HelpClass;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
namespace HyCADTool.Views
{
    public partial class FilterPanel : UserControl
    {
        private Dictionary<string, bool> filterStatusMap = new Dictionary<string, bool>();
        // 记录上次选择集的字典，键为过滤类型，值为ObjectId数组
        private Dictionary<string, ObjectId[]> previousIdsMap = new Dictionary<string, ObjectId[]>();
        //private Dictionary<string, (string Value, string Type)> entityProperties;
        private List<string> mainTypes = new List<string> { "Boolean", "Double", "Int32", "String", "Point3d" }; // 主类型
        private static Entity SelectedEntity { get; set; }
        private static SelectionFilter Filter { get; set; }
        private static ObjectId[] BaseIds { get; set; }
        private static ObjectId[] CurrentIds { get; set; }
        private ObjectId[] TypeIds = new ObjectId[0];
        private ObjectId[] LayerIds = new ObjectId[0];
        private ObjectId[] ColorIds = new ObjectId[0];
        private ObjectId[] LineWeightdIds = new ObjectId[0];
        private ObjectId[] LineTypeIds = new ObjectId[0];
        private ObjectId[] TransparencyIds = new ObjectId[0];
        private ObjectId[] IdsAll = new ObjectId[0];
        public FilterPanel()
        {
            InitializeComponent();
            InitializeIdsAll();
        }
        private void InitializeIdsAll()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = trans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                IdsAll = btr.Cast<ObjectId>().ToArray();
                ed.WriteMessage($"全部选择集共有{IdsAll.Length}个图形");
                trans.Commit();
            }
        }
        private void ResetCheckBox()
        {
            TypeCheckBox.IsChecked = false;
            LayerCheckBox.IsChecked = false;
            ColorCheckBox.IsChecked = false;
            LineWidthCheckBox.IsChecked = false;
            LineTypeCheckBox.IsChecked = false;
            TransparencyCheckBox.IsChecked = false;
        }
        private void SelectSingleEntityButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ResetCheckBox();
                InitializeIdsAll();
                CurrentIds = IdsAll;
                Document doc = Application.DocumentManager.MdiActiveDocument;
                Editor ed = doc.Editor;
                SelectedEntity = doc.Database.SelectSingleEntity();
                if (SelectedEntity != null)
                {
                    string entityType = SelectedEntity.GetType().Name;
                    string entityTypeChinese = entityType.ToChinese(); // 使用扩展方法转换为中文类型
                    SelectedTypeTextBox.Text = entityTypeChinese; // 在文本框中显示中文类型
                                                                  // entityProperties = SelectedEntity.GetEntityProperties();
                    PopulateComboBoxWithTypes();
                }
            }
            catch (System.Exception)
            {
                throw;
            }
        }
        private void PopulateComboBoxWithTypes()
        {
            TypeFilterComboBox.Items.Clear();
            TypeFilterComboBox.Items.Add(new ComboBoxItem { Content = "All", IsSelected = true });
            foreach (var type in mainTypes)
            {
                TypeFilterComboBox.Items.Add(new ComboBoxItem { Content = type });
            }
            TypeFilterComboBox.Items.Add(new ComboBoxItem { Content = "Others" });
        }
        private void TypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //string selectedType = (TypeFilterComboBox.SelectedItem as ComboBoxItem).Content.ToString();
            //PropertiesListbox.Items.Clear();
            //foreach (var prop in entityProperties)
            //{
            //    if (selectedType == "All" || (mainTypes.Contains(prop.Value.Type) && prop.Value.Type == selectedType) || (selectedType == "Others" && !mainTypes.Contains(prop.Value.Type)))
            //    {
            //        PropertiesListbox.Items.Add(new ListBoxItem { Content = $"{prop.Key}: {prop.Value.Value} ({prop.Value.Type})" });
            //    }
            //}
        }
        private void PropertiesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PropertiesListbox.SelectedItem != null)
            {
                FunctionListBox.Items.Clear();
                FunctionListBox.Items.Add(new ListBoxItem { Content = PropertiesListbox.SelectedItem.ToString() });
            }
        }
        private void AddFilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (PropertiesListbox.SelectedItem != null)
            {
                ShowFilterListBox.Items.Add(new ListBoxItem { Content = PropertiesListbox.SelectedItem.ToString() });
            }
        }
        private void RemoveFilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (ShowFilterListBox.SelectedItem != null)
            {
                ShowFilterListBox.Items.Remove(ShowFilterListBox.SelectedItem);
            }
        }
        private void HighlightFilteredEntities(ObjectId[] ids)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            ed.SetImpliedSelection(ids);
        }
        #region CheckBox 打开关闭      
        /// <summary>
        /// 应用或移除过滤器，根据过滤器类型和CheckBox的选中状态更新CurrentIds。
        /// </summary>
        /// <param name="filterFunc">生成过滤结果的函数</param>
        /// <param name="isChecked">CheckBox是否被选中</param>
        /// <param name="filterType">过滤器类型的字符串标识</param>
        private void ApplyFilter(Func<ObjectId[]> filterFunc, bool isChecked, string filterType)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            // 调用过滤函数获取过滤后的对象ID集合
            var ids = filterFunc?.Invoke();
            if (ids == null)
            {
                doc.Editor.WriteMessage("未能选中任何对象。\n");
                return;
            }
            // 如果当前选择集为空或未初始化，则初始化为所有对象ID
            if (CurrentIds == null || CurrentIds.Length == 0)
            {
                CurrentIds = IdsAll;
            }
            var currentIds = CurrentIds.AsEnumerable();
            if (isChecked)
            {
                // 如果CheckBox被选中，记录当前的选择集到字典中
                if (!previousIdsMap.ContainsKey(filterType))
                {
                    previousIdsMap[filterType] = CurrentIds.ToArray();
                }
                // 将当前选择集与过滤结果取交集
                CurrentIds = currentIds.Intersect(ids).ToArray();
            }
            else
            {
                // 如果CheckBox未被选中，恢复之前的选择集
                if (previousIdsMap.ContainsKey(filterType))
                {
                    CurrentIds = previousIdsMap[filterType];
                    previousIdsMap.Remove(filterType);
                }
                else
                {
                    // 如果字典中没有之前的选择集，则将当前选择集与过滤结果取并集
                    CurrentIds = currentIds.Union(ids).ToArray();
                }
            }
            // 检查当前选择集是否等于所有对象ID集合
            if (CurrentIds.SequenceEqual(IdsAll))
            {
                // 如果过滤后选择集等于所有对象ID集合，则不高亮显示
                using (DocumentLock docLock = doc.LockDocument())
                {
                    doc.Editor.SetImpliedSelection(new ObjectId[0]);
                }
            }
            else if (CurrentIds.Length > 0)
            {
                // 如果选择集不为空，则高亮显示选择集
                using (DocumentLock docLock = doc.LockDocument())
                {
                    doc.Editor.SetImpliedSelection(new ObjectId[0]);
                    doc.Editor.SetImpliedSelection(CurrentIds);
                }
            }
        }
        #region CheckBox 打开关闭
        private void ExecuteWithExceptionHandling(Action action, string errorMessage)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            try
            {
                action();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                doc.Editor.WriteMessage($"{errorMessage}: {ex.Message}\n");
            }
            catch (System.Exception ex)
            {
                doc.Editor.WriteMessage($"{errorMessage}: {ex.Message}\n");
            }
        }
        private bool CheckSelectedEntity()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (SelectedEntity == null)
            {
                doc.Editor.WriteMessage("SelectedEntity 为 null。\n");
                return false;
            }
            return true;
        }
        private void TypeCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (!CheckSelectedEntity()) return;
            UpdateFilterStatus("Type", true);
            ExecuteWithExceptionHandling(() =>
            {
                Func<ObjectId[]> filter = () =>
                {
                    return SelectedEntity.GetType().Name.ToChinese().ToType().GetfilterWithString().Getfilter().SelectWithFilterAll();
                };
                ApplyFilter(filter, true, "Type");
            }, "处理TypeCheckBox_Checked事件时发生错误");
        }
        private void TypeCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!CheckSelectedEntity()) return;
            UpdateFilterStatus("Type", false);
            ExecuteWithExceptionHandling(() =>
            {
                Func<ObjectId[]> filter = () => { return SelectedEntity.GetType().Name.ToChinese().ToType().GetfilterWithString().Getfilter().SelectWithFilterAll(); };
                ApplyFilter(filter, false, "Type");
            }, "处理TypeCheckBox_Unchecked事件时发生错误");
        }
        private void LayerCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (!CheckSelectedEntity()) return;
            UpdateFilterStatus("Layer", true);
            ExecuteWithExceptionHandling(() =>
            {
                Func<ObjectId[]> filter = () => { return SelectedEntity.Layer.ToString().GetLayerFilter().Getfilter().SelectWithFilterAll(); };
                ApplyFilter(filter, true, "Layer");
            }, "处理LayerCheckBox_Checked事件时发生错误");
        }
        private void LayerCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!CheckSelectedEntity()) return;
            UpdateFilterStatus("Layer", false);
            ExecuteWithExceptionHandling(() =>
            {
                Func<ObjectId[]> filter = () => { return SelectedEntity.Layer.ToString().GetLayerFilter().Getfilter().SelectWithFilterAll(); };
                ApplyFilter(filter, false, "Layer");
            }, "处理LayerCheckBox_Unchecked事件时发生错误");
        }
        private void ColorCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (!CheckSelectedEntity()) return;
            UpdateFilterStatus("Color", true);
            ExecuteWithExceptionHandling(() =>
            {
                var color = SelectedEntity.GetTrueColor();
                ColorIds = color.GetEntitiesWithMatchingColor(doc);
                ApplyFilter(() => ColorIds, true, "Color");
            }, "处理ColorCheckBox_Checked事件时发生错误");
        }
        private void ColorCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (!CheckSelectedEntity()) return;
            UpdateFilterStatus("Color", false);
            ExecuteWithExceptionHandling(() =>
            {
                var color = SelectedEntity.GetTrueColor();
                ColorIds = color.GetEntitiesWithMatchingColor(doc);
                ApplyFilter(() => ColorIds, false, "Color");
            }, "处理ColorCheckBox_Unchecked事件时发生错误");
        }
        private void LineWidthCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (!CheckSelectedEntity()) return;
            UpdateFilterStatus("LineWeight", true);
            ExecuteWithExceptionHandling(() =>
            {
                Func<ObjectId[]> filter = () => { return SelectedEntity.GetTrueLineWeight().GetLineWeightFilter().Getfilter().SelectWithFilterAll(); };
                ApplyFilter(filter, true, "LineWeight");
            }, "处理LineWidthCheckBox_Checked事件时发生错误");
        }
        private void LineWidthCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!CheckSelectedEntity()) return;
            UpdateFilterStatus("LineWeight", false);
            ExecuteWithExceptionHandling(() =>
            {
                Func<ObjectId[]> filter = () => { return SelectedEntity.GetTrueLineWeight().GetLineWeightFilter().Getfilter().SelectWithFilterAll(); };
                ApplyFilter(filter, false, "LineWeight");
            }, "处理LineWidthCheckBox_Unchecked事件时发生错误");
        }
        private void LineTypeCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (!CheckSelectedEntity()) return;
            UpdateFilterStatus("LineType", true);
            ExecuteWithExceptionHandling(() =>
            {
                var linetype = SelectedEntity.GetTrueLinetype();
                LineTypeIds = linetype.GetEntitiesWithMatchingLinetype(doc, CurrentIds);
                ApplyFilter(() => LineTypeIds, true, "LineType");
            }, "处理LineTypeCheckBox_Checked事件时发生错误");
        }
        private void LineTypeCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (!CheckSelectedEntity()) return;
            UpdateFilterStatus("LineType", false);
            ExecuteWithExceptionHandling(() =>
            {
                var linetype = SelectedEntity.GetTrueLinetype();
                LineTypeIds = linetype.GetEntitiesWithMatchingLinetype(doc, CurrentIds);
                ApplyFilter(() => LineTypeIds, false, "LineType");
            }, "处理LineTypeCheckBox_Unchecked事件时发生错误");
        }
        private void TransparencyCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (!CheckSelectedEntity()) return;
            UpdateFilterStatus("Transparency", true);
            ExecuteWithExceptionHandling(() =>
            {
                var transparency = SelectedEntity.GetTrueTransparency();
                TransparencyIds = transparency.GetEntitiesWithMatchingTransparency(doc, CurrentIds);
                ApplyFilter(() => TransparencyIds, true, "Transparency");
            }, "处理TransparencyCheckBox_Checked事件时发生错误");
        }
        private void TransparencyCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (!CheckSelectedEntity()) return;
            UpdateFilterStatus("Transparency", false);
            ExecuteWithExceptionHandling(() =>
            {
                var transparency = SelectedEntity.GetTrueTransparency();
                TransparencyIds = transparency.GetEntitiesWithMatchingTransparency(doc, CurrentIds);
                ApplyFilter(() => TransparencyIds, false, "Transparency");
            }, "处理TransparencyCheckBox_Unchecked事件时发生错误");
        }
        #endregion
        #endregion
        #region 用户应用过滤器自主选择
        private void UpdateFilterStatus(string filterType, bool isChecked)
        {
            filterStatusMap[filterType] = isChecked;
        }
        private ObjectId[] ApplySelectedFilters()
        {
            var filteredIds = IdsAll.AsEnumerable();
            foreach (var filter in filterStatusMap)
            {
                if (filter.Value) // 如果过滤器被选中
                {
                    Func<ObjectId[]> filterFunc = GetFilterFunction(filter.Key);
                    var ids = filterFunc?.Invoke();
                    if (ids != null)
                    {
                        filteredIds = filteredIds.Intersect(ids);
                    }
                }
            }
            return filteredIds.ToArray();
        }
        // 获取过滤函数的方法
        private Func<ObjectId[]> GetFilterFunction(string filterType)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            switch (filterType)
            {
                case "Type":
                    return () => SelectedEntity.GetType().Name.ToChinese().ToType().GetfilterWithString().Getfilter().SelectWithFilterAll();
                case "Layer":
                    return () => SelectedEntity.Layer.ToString().GetLayerFilter().Getfilter().SelectWithFilterAll();
                case "Color":
                    return () =>
                    {
                        var color = SelectedEntity.GetTrueColor();
                        return color.GetEntitiesWithMatchingColor(doc);
                    };
                case "LineWeight":
                    return () => SelectedEntity.GetTrueLineWeight().GetLineWeightFilter().Getfilter().SelectWithFilterAll();
                case "LineType":
                    return () =>
                    {
                        var linetype = SelectedEntity.GetTrueLinetype();
                        return linetype.GetEntitiesWithMatchingLinetype(doc, CurrentIds);
                    };
                case "Transparency":
                    return () =>
                    {
                        var transparency = SelectedEntity.GetTrueTransparency();
                        return transparency.GetEntitiesWithMatchingTransparency(doc, CurrentIds);
                    };
                default:
                    return null;
            }
        }
        private void ApplyUserSelectionWithFilters()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            // 用户从 CurrentIds 中选择对象
            // 假设用户选择的对象ID存储在 userSelectedIds 中
            ObjectId[] userSelectedIds = GetUserSelectedIds();
            // 应用所有选中的过滤器
            var filteredIds = ApplySelectedFilters().Intersect(userSelectedIds).ToArray();
            if (filteredIds.Length > 0)
            {
                using (DocumentLock docLock = doc.LockDocument())
                {
                    doc.Editor.SetImpliedSelection(new ObjectId[0]);
                    doc.Editor.SetImpliedSelection(filteredIds);
                }
            }
            else
            {
                using (DocumentLock docLock = doc.LockDocument())
                {
                    doc.Editor.SetImpliedSelection(new ObjectId[0]);
                }
                doc.Editor.WriteMessage("未能选中任何对象。\n");
            }
        }
        // 模拟用户从 CurrentIds 中选择对象的方法
        private ObjectId[] GetUserSelectedIds()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            ObjectId[] selectedIds = new ObjectId[0];
            using (DocumentLock docLock = doc.LockDocument())
            {
                // 清空预先选择集
                ed.SetImpliedSelection(new ObjectId[0]);
                // 设置选择提示
                ed.WriteMessage("\n请选择要过滤的图形对象:");
                // 使用选择过滤器获取选择集
                PromptSelectionResult res = ed.GetSelection();
                // 检查选择结果状态
                if (res.Status == PromptStatus.OK)
                {
                    // 输出选择的对象数量
                    ed.WriteMessage($"选中了 {res.Value.Count} 个图形对象。\n");
                    // 获取当前的选择集
                    SelectionSet ss = res.Value;
                    selectedIds = ss.GetObjectIds();
                }
                else
                {
                    ed.WriteMessage("未能选中任何对象。\n");
                }
            }
            return selectedIds;
        }
        #endregion
        private void Select_Click(object sender, RoutedEventArgs e)
        {
            ExecuteWithExceptionHandling(() =>
            {
                ApplyUserSelectionWithFilters();
            },
            "处理用户自主通过过滤器选择事件时发生错误"
            );
        }
    }
}