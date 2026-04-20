namespace HyCAD.BlenderUI.Screen.Areas
{
    public abstract class AreaTreeNode
    {
    }

    public sealed class AreaSplit : AreaTreeNode
    {
        public AreaTreeNode Left { get; set; }
        public AreaTreeNode Right { get; set; }
        public double SplitFactor { get; set; } = 0.5;
        public bool IsHorizontal { get; set; }
    }

    public sealed class AreaLeaf : AreaTreeNode
    {
        public SpaceTypeId SpaceType { get; set; } = SpaceTypeId.View3D;
    }
}
