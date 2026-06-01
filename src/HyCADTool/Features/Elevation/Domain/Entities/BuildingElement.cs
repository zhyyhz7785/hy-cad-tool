using System;
using HyCADTool.Features.Elevation.Domain.ValueObjects;
using HyCAD.Geometry;

namespace HyCADTool.Features.Elevation.Domain.Entities
{
    /// <summary>
    /// 建筑元素基类
    /// 所有3D建筑构件的抽象基类
    /// </summary>
    public abstract class BuildingElement
    {
        /// <summary>
        /// 元素唯一标识
        /// </summary>
        public Guid Id { get; protected set; }
        
        /// <summary>
        /// 元素名称
        /// </summary>
        public string Name { get; protected set; }
        
        /// <summary>
        /// 图层名称
        /// </summary>
        public string LayerName { get; protected set; }
        
        /// <summary>
        /// 底部标高
        /// </summary>
        public ElevationValue BottomElevation { get; protected set; }
        
        /// <summary>
        /// 顶部标高
        /// </summary>
        public ElevationValue TopElevation { get; protected set; }
        
        /// <summary>
        /// 元素高度（mm）
        /// </summary>
        public double Height => TopElevation.Value - BottomElevation.Value;
        
        /// <summary>
        /// 是否有效（高度 > 0）
        /// </summary>
        public bool IsValid => Height > 0;
        
        protected BuildingElement(
            string name,
            string layerName,
            ElevationValue bottomElevation,
            ElevationValue topElevation)
        {
            Id = Guid.NewGuid();
            Name = name ?? throw new ArgumentNullException(nameof(name));
            LayerName = layerName ?? throw new ArgumentNullException(nameof(layerName));
            BottomElevation = bottomElevation ?? throw new ArgumentNullException(nameof(bottomElevation));
            TopElevation = topElevation ?? throw new ArgumentNullException(nameof(topElevation));
            
            if (!IsValid)
            {
                throw new ArgumentException(
                    $"建筑元素高度必须大于0，当前：底部={bottomElevation.ToFormattedString()}, 顶部={topElevation.ToFormattedString()}");
            }
        }
        
        /// <summary>
        /// 验证元素的有效性
        /// </summary>
        public virtual bool Validate(out string errorMessage)
        {
            if (!IsValid)
            {
                errorMessage = $"元素高度无效：{Height}mm";
                return false;
            }
            
            errorMessage = null;
            return true;
        }
        
        public override string ToString()
        {
            return $"{GetType().Name} '{Name}' (高度: {Height:F0}mm, 图层: {LayerName})";
        }
    }
}

