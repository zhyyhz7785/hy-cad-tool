using System;

namespace HyCAD.BlenderUI.Screen.Areas
{
    /// <summary>Screen 二叉区域树（阶段 4a）。</summary>
    public sealed class AreaTreeModel
    {
        public AreaTreeNode Root { get; set; } = new AreaLeaf();

        public event EventHandler Changed;

        public void SplitRoot(bool horizontal)
        {
            var leaf = Root as AreaLeaf ?? new AreaLeaf();
            Root = new AreaSplit
            {
                IsHorizontal = horizontal,
                SplitFactor = 0.5,
                Left = leaf,
                Right = new AreaLeaf { SpaceType = SpaceTypeId.Properties },
            };
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void SetSpaceType(AreaLeaf leaf, SpaceTypeId id)
        {
            if (leaf == null) return;
            leaf.SpaceType = id;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
