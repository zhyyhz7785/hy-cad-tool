using HyCAD.BlenderUI.Screen.Areas;

namespace HyCAD.BlenderUI.WM.Operators
{
    /// <summary>wmOperator 上下文（阶段 6）。</summary>
    public class OperatorContext
    {
        public AreaTreeModel Screen { get; set; }
        public SpaceTypeId? ActiveSpace { get; set; }
        public object Selection { get; set; }
    }
}
