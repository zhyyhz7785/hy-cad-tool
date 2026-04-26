using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using System;

namespace HyCADTool.Shared.AutoCAD.Selection.EntityFilters
{
    /// <summary>
    /// 基于实体属性的过滤器适配（用于替代旧版 FilterManager 的内聚实现）
    /// 提供从实体提取过滤条件并生成 SelectionFilter 的简化方法
    /// </summary>
    public static class EntityPropertyFilters
    {
        public static SelectionFilter FromLayer(Entity entity)
        {
            if (entity == null || string.IsNullOrEmpty(entity.Layer)) return null;
            var values = new TypedValue[]
            {
                new TypedValue((int)DxfCode.LayerName, entity.Layer)
            };
            return new SelectionFilter(values);
        }

        public static SelectionFilter FromColorIndex(Entity entity)
        {
            if (entity == null || entity.Color.IsByLayer || entity.Color.IsByBlock) return null;
            var values = new TypedValue[]
            {
                new TypedValue((int)DxfCode.Color, entity.ColorIndex)
            };
            return new SelectionFilter(values);
        }

        public static SelectionFilter FromLinetype(Entity entity)
        {
            if (entity == null
                || string.IsNullOrEmpty(entity.Linetype)
                || entity.Linetype.Equals("ByLayer", StringComparison.OrdinalIgnoreCase)
                || entity.Linetype.Equals("ByBlock", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            var values = new TypedValue[]
            {
                new TypedValue((int)DxfCode.LinetypeName, entity.Linetype)
            };
            return new SelectionFilter(values);
        }

        public static SelectionFilter FromLineWeight(Entity entity)
        {
            if (entity == null || entity.LineWeight == LineWeight.ByLayer || entity.LineWeight == LineWeight.ByBlock) return null;
            var values = new TypedValue[]
            {
                new TypedValue((int)DxfCode.LineWeight, (int)entity.LineWeight)
            };
            return new SelectionFilter(values);
        }

        public static SelectionFilter FromType(Entity entity)
        {
            if (entity == null) return null;
            var dxf = entity.GetRXClass().DxfName;
            if (string.IsNullOrEmpty(dxf)) return null;
            var values = new TypedValue[]
            {
                new TypedValue((int)DxfCode.Start, dxf)
            };
            return new SelectionFilter(values);
        }
    }
}


