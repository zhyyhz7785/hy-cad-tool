namespace HyCADTool.Features.Reinforcement.Domain.Components
{
    /// <summary>
    /// 截面构件类型（用于识别预览与分区配筋）。
    /// </summary>
    public enum ComponentType
    {
        Slab = 0,
        Wall = 1,
        BottomSlab = 2,
        MassConcrete = 3,
        Beam = 4
    }

    public static class ComponentTypeExtensions
    {
        public static ComponentType Next(this ComponentType type)
        {
            int next = ((int)type + 1) % 5;
            return (ComponentType)next;
        }

        public static string DisplayName(this ComponentType type)
        {
            switch (type)
            {
                case ComponentType.Slab: return "楼板";
                case ComponentType.Wall: return "墙体";
                case ComponentType.BottomSlab: return "底板";
                case ComponentType.MassConcrete: return "大体积混凝土";
                case ComponentType.Beam: return "梁";
                default: return type.ToString();
            }
        }

        /// <summary>ACI 颜色：楼板=4 青、墙=3 绿、底板=5 蓝、大体积=1 红、梁=2 黄。</summary>
        public static short ColorIndex(this ComponentType type)
        {
            switch (type)
            {
                case ComponentType.Slab: return 4;
                case ComponentType.Wall: return 3;
                case ComponentType.BottomSlab: return 5;
                case ComponentType.MassConcrete: return 1;
                case ComponentType.Beam: return 2;
                default: return 8;
            }
        }
    }
}
