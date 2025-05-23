// ✅ 统一过滤器逻辑实现（单文件整合）并支持外部访问
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.HelpClass;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Filters
{
    public interface IEntityFilter
    {
        string Key { get; }
        ObjectId[] Apply(Entity selectedEntity, ObjectId[] baseIds);
    }

    public class FilterManager
    {
        private readonly Dictionary<string, IEntityFilter> _registry = new Dictionary<string, IEntityFilter>();
        public Entity SelectedEntity { get; set; }
        public ObjectId[] BaseIds { get; set; } = new ObjectId[0];
        public ObjectId[] CurrentIds { get; set; } = new ObjectId[0];
        public Dictionary<string, ObjectId[]> PreviousMap { get; } = new Dictionary<string, ObjectId[]>();

        public FilterManager()
        {
            Register(new LayerFilter());
            Register(new ColorFilter());
            Register(new LineTypeFilter());
            Register(new LineWeightFilter());
            Register(new TransparencyFilter());
            Register(new TypeFilter());
        }

        public void Register(IEntityFilter filter)
        {
            if (!_registry.ContainsKey(filter.Key))
                _registry[filter.Key] = filter;
        }

        public void Reset()
        {
            CurrentIds = BaseIds;
            PreviousMap.Clear();
        }

        public void ApplyAndUpdate(string key, bool isChecked)
        {
            if (SelectedEntity == null || !_registry.ContainsKey(key)) return;

            var ids = _registry[key].Apply(SelectedEntity, BaseIds);
            if (ids == null) return;

            if (CurrentIds == null || CurrentIds.Length == 0)
                CurrentIds = BaseIds;

            var current = CurrentIds.AsEnumerable();
            if (isChecked)
            {
                if (!PreviousMap.ContainsKey(key))
                    PreviousMap[key] = CurrentIds.ToArray();
                CurrentIds = current.Intersect(ids).ToArray();
            }
            else
            {
                if (PreviousMap.ContainsKey(key))
                {
                    CurrentIds = PreviousMap[key];
                    PreviousMap.Remove(key);
                }
                else
                {
                    CurrentIds = current.Union(ids).ToArray();
                }
            }
        }

        public IEnumerable<string> RegisteredKeys => _registry.Keys;
    }

    public class LayerFilter : IEntityFilter
    {
        public string Key => "Layer";
        public ObjectId[] Apply(Entity selectedEntity, ObjectId[] baseIds)
        {
            return selectedEntity.Layer.GetLayerFilter().Getfilter().SelectWithFilterAll();
        }
    }

    public class ColorFilter : IEntityFilter
    {
        public string Key => "Color";
        public ObjectId[] Apply(Entity selectedEntity, ObjectId[] baseIds)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            return doc.FilterByColor(selectedEntity.GetTrueColor(), baseIds);
        }
    }

    public class LineTypeFilter : IEntityFilter
    {
        public string Key => "LineType";
        public ObjectId[] Apply(Entity selectedEntity, ObjectId[] baseIds)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            return doc.FilterByLinetype(selectedEntity.GetTrueLinetype(), baseIds);
        }
    }

    public class LineWeightFilter : IEntityFilter
    {
        public string Key => "LineWeight";
        public ObjectId[] Apply(Entity selectedEntity, ObjectId[] baseIds)
        {
            return selectedEntity.GetTrueLineWeight().GetLineWeightFilter().Getfilter().SelectWithFilterAll();
        }
    }

    public class TransparencyFilter : IEntityFilter
    {
        public string Key => "Transparency";
        public ObjectId[] Apply(Entity selectedEntity, ObjectId[] baseIds)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            return doc.FilterByTransparency(selectedEntity.GetTrueTransparency(), baseIds);
        }
    }

    public class TypeFilter : IEntityFilter
    {
        public string Key => "Type";
        public ObjectId[] Apply(Entity selectedEntity, ObjectId[] baseIds)
        {
            return selectedEntity.GetType().Name.ToChinese().ToType().GetfilterWithString().Getfilter().SelectWithFilterAll();
        }
    }
}