using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Selection
{
    /// <summary>
    /// SelectionFilter 构造器
    /// 基于 DXF 过滤条件（类型、图层、颜色、线型、线宽）构建选择过滤器
    /// </summary>
    public class SelectionFilterBuilder
    {
        private readonly List<TypedValue> _conditions = new List<TypedValue>();

        public SelectionFilterBuilder ByType(string dxfType)
        {
            if (!string.IsNullOrEmpty(dxfType))
            {
                _conditions.Add(new TypedValue((int)DxfCode.Start, dxfType));
            }
            return this;
        }

        public SelectionFilterBuilder ByLayer(string layerName)
        {
            if (!string.IsNullOrEmpty(layerName))
            {
                _conditions.Add(new TypedValue((int)DxfCode.LayerName, layerName));
            }
            return this;
        }

        public SelectionFilterBuilder ByColorIndex(short colorIndex)
        {
            _conditions.Add(new TypedValue((int)DxfCode.Color, colorIndex));
            return this;
        }

        public SelectionFilterBuilder ByLinetype(string linetypeName)
        {
            if (!string.IsNullOrEmpty(linetypeName))
            {
                _conditions.Add(new TypedValue((int)DxfCode.LinetypeName, linetypeName));
            }
            return this;
        }

        public SelectionFilterBuilder ByLineWeight(LineWeight lineWeight)
        {
            _conditions.Add(new TypedValue((int)DxfCode.LineWeight, (int)lineWeight));
            return this;
        }

        /// <summary>
        /// 返回用于构建 SelectionFilter 的 TypedValue 数组（用于测试验证）
        /// 多条件时使用 <AND ... AND> 包裹
        /// </summary>
        public TypedValue[] BuildValues()
        {
            if (_conditions.Count == 0)
            {
                return new TypedValue[0];
            }

            if (_conditions.Count == 1)
            {
                return _conditions.ToArray();
            }

            var result = new List<TypedValue>();
            result.Add(new TypedValue((int)DxfCode.Operator, "<AND"));
            result.AddRange(_conditions);
            result.Add(new TypedValue((int)DxfCode.Operator, "AND>"));
            return result.ToArray();
        }

        public SelectionFilter BuildFilter()
        {
            return new SelectionFilter(BuildValues());
        }
    }
}


