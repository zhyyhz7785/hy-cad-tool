using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Features.G101.Domain.Catalog;
using HyCADTool.Features.G101.Domain.Geometry;
using HyCADTool.Features.G101.Domain.Tables;
using HyCADTool.Features.G16.Domain.Catalog;
using HyCADTool.Features.G16.Domain.Tables;
using HyCADTool.Features.G16.Services;
using HyCADTool.Shell.ViewModels;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.G16.ViewModels
{
    public sealed class G16ParamValueVm : INotifyPropertyChanged
    {
        private readonly ParamDescriptor _desc;
        private object _value;

        public G16ParamValueVm(ParamDescriptor desc, object initial)
        {
            _desc = desc;
            _value = initial;
        }

        public string Label => _desc.Label + (string.IsNullOrEmpty(_desc.Unit) ? "" : $" ({_desc.Unit})");
        public ParamKind Kind => _desc.Kind;
        public string Key => _desc.Key;

        public object Value
        {
            get => _value;
            set { if (Equals(_value, value)) return; _value = value; OnPropertyChanged(); }
        }

        public string NumberText
        {
            get => Value?.ToString() ?? "0";
            set
            {
                if (double.TryParse(value, out var d)) Value = d;
                else if (int.TryParse(value, out var i)) Value = i;
                OnPropertyChanged();
            }
        }

        public bool BoolValue
        {
            get => Value is bool b && b;
            set { Value = value; OnPropertyChanged(); OnPropertyChanged(nameof(BoolValue)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public sealed class G16CatalogTreeItemVm
    {
        public string DisplayName { get; set; }
        public bool IsImplemented { get; set; }
        public bool IsGroup { get; set; }
        public G16CatalogItem Item { get; set; }
        public ObservableCollection<G16CatalogTreeItemVm> Children { get; } = new ObservableCollection<G16CatalogTreeItemVm>();
    }

    public class G16PanelViewModel : INotifyPropertyChanged
    {
        private static readonly Dictionary<string, G16PanelViewModel> _byDoc = new Dictionary<string, G16PanelViewModel>();

        public static G16PanelViewModel Current
        {
            get
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                if (doc == null) return null;
                var key = doc.Name;
                if (!_byDoc.TryGetValue(key, out var vm))
                {
                    vm = new G16PanelViewModel();
                    _byDoc[key] = vm;
                }
                return vm;
            }
        }

        public G16GlobalSettings Global { get; } = new G16GlobalSettings();
        public ObservableCollection<G16CatalogTreeItemVm> CatalogTree { get; } = new ObservableCollection<G16CatalogTreeItemVm>();
        public ObservableCollection<G16ParamValueVm> Parameters { get; } = new ObservableCollection<G16ParamValueVm>();

        public Array ConcreteGradeChoices => Enum.GetValues(typeof(ConcreteGrade));
        public Array RebarGradeChoices => Enum.GetValues(typeof(RebarGrade));
        public Array SeismicGradeChoices => Enum.GetValues(typeof(SeismicGrade));
        public Array EnvironmentClassChoices => Enum.GetValues(typeof(EnvironmentClass));

        private G16CatalogTreeItemVm _selectedNode;
        private DetailSketch _previewSketch;
        private string _lookupSummary = string.Empty;
        private string _atlasRefText = string.Empty;
        private string _statusMessage = string.Empty;

        public G16CatalogTreeItemVm SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (_selectedNode == value) return;
                _selectedNode = value;
                OnPropertyChanged();
                LoadParametersFromSelection();
            }
        }

        public DetailSketch PreviewSketch
        {
            get => _previewSketch;
            private set { _previewSketch = value; OnPropertyChanged(); }
        }

        public string LookupSummary
        {
            get => _lookupSummary;
            private set { _lookupSummary = value; OnPropertyChanged(); }
        }

        public string AtlasRefText
        {
            get => _atlasRefText;
            private set { _atlasRefText = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set { _statusMessage = value; OnPropertyChanged(); }
        }

        public bool CanDraw => SelectedNode?.Item?.IsImplemented == true && SelectedNode.Item.Build != null;

        public ICommand DrawCommand { get; }
        public ICommand RefreshPreviewCommand { get; }
        private readonly RelayCommand _drawRelay;

        public G16PanelViewModel()
        {
            BuildCatalogTree();
            Global.PropertyChanged += (_, __) => RefreshLookupSummary();
            _drawRelay = new RelayCommand(DrawToCad, () => CanDraw);
            DrawCommand = _drawRelay;
            RefreshPreviewCommand = new RelayCommand(RefreshPreview);
            RefreshLookupSummary();
        }

        private void BuildCatalogTree()
        {
            CatalogTree.Clear();
            foreach (var atlas in G16Catalog.Build())
            {
                var atlasNode = new G16CatalogTreeItemVm
                {
                    DisplayName = atlas.AtlasId + " " + atlas.Name,
                    IsGroup = true
                };
                foreach (var group in atlas.Groups)
                {
                    var groupNode = new G16CatalogTreeItemVm
                    {
                        DisplayName = group.Name,
                        IsGroup = true
                    };
                    foreach (var item in group.Items)
                    {
                        groupNode.Children.Add(new G16CatalogTreeItemVm
                        {
                            DisplayName = item.Name + (item.IsImplemented ? "" : "（规划中）"),
                            IsImplemented = item.IsImplemented,
                            Item = item
                        });
                    }
                    atlasNode.Children.Add(groupNode);
                }
                CatalogTree.Add(atlasNode);
            }
        }

        private void LoadParametersFromSelection()
        {
            Parameters.Clear();
            var item = SelectedNode?.Item;
            if (item == null)
            {
                PreviewSketch = null;
                AtlasRefText = string.Empty;
                OnPropertyChanged(nameof(CanDraw));
                _drawRelay?.RaiseCanExecuteChanged();
                return;
            }

            AtlasRefText = item.AtlasRef != null ? $"依据：{item.AtlasRef.Display}" : string.Empty;
            foreach (var pd in item.Parameters)
            {
                object val;
                switch (pd.Kind)
                {
                    case ParamKind.Boolean: val = pd.DefaultBool; break;
                    case ParamKind.Integer: val = pd.DefaultInt; break;
                    default: val = pd.DefaultNumber; break;
                }
                var pvm = new G16ParamValueVm(pd, val);
                pvm.PropertyChanged += (_, __) => RefreshPreview();
                Parameters.Add(pvm);
            }

            OnPropertyChanged(nameof(CanDraw));
            _drawRelay?.RaiseCanExecuteChanged();
            RefreshPreview();
        }

        private void RefreshLookupSummary()
        {
            var laE = G16TableLookup.GetLaE(Global);
            var lab = G16TableLookup.GetLab(Global);
            var cover = G16TableLookup.GetCoverThickness(Global.EnvironmentClass, 2);
            LookupSummary = string.Join("  |  ",
                G16TableLookup.FormatLookup("lab", lab, G16TableLookup.RefLab, Global),
                G16TableLookup.FormatLookup("laE", laE, G16TableLookup.RefLa, Global),
                $"保护层={cover}mm（{G16TableLookup.RefCover.Display}）");
            RefreshPreview();
        }

        public void RefreshPreview()
        {
            var item = SelectedNode?.Item;
            if (item?.Build == null || !item.IsImplemented)
            {
                PreviewSketch = null;
                return;
            }

            try
            {
                PreviewSketch = item.Build(Global, CollectParams());
            }
            catch (Exception ex)
            {
                StatusMessage = "预览失败：" + ex.Message;
                PreviewSketch = null;
            }
        }

        private Dictionary<string, object> CollectParams()
        {
            var dict = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var p in Parameters)
                dict[p.Key] = p.Value;
            return dict;
        }

        private void DrawToCad()
        {
            var item = SelectedNode?.Item;
            if (item?.Build == null) return;

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                StatusMessage = "无活动文档";
                return;
            }

            SettingsPanelViewModel.PendingCommand = ExecuteDrawInCommandContext;
            try
            {
                doc.SendStringToExecute("_HyExec\n", true, false, false);
            }
            catch (Exception ex)
            {
                StatusMessage = "发送命令失败：" + ex.Message;
                SettingsPanelViewModel.PendingCommand = null;
            }
        }

        private void ExecuteDrawInCommandContext()
        {
            var item = SelectedNode?.Item;
            if (item?.Build == null) return;

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc?.Editor;
            if (ed == null) return;

            var ppr = ed.GetPoint(new PromptPointOptions("\n指定大样插入点: "));
            if (ppr.Status != PromptStatus.OK) return;

            try
            {
                var sketch = item.Build(Global, CollectParams());
                var ids = G16DetailSketchRenderer.Render(sketch, ppr.Value, 1.0);
                StatusMessage = $"已绘制 {ids.Count} 个实体 — {item.Name}";
                ed.WriteMessage($"\n[G16] {StatusMessage}（{item.AtlasRef?.Display}）");
            }
            catch (Exception ex)
            {
                StatusMessage = "绘制失败：" + ex.Message;
                ed.WriteMessage($"\n[G16] {StatusMessage}");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
