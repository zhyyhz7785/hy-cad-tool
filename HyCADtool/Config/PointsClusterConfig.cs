namespace HyCADTool.Config
{
    public static class PointsClusterConfig
    {
        public static double Scale = 40;
        public static double EpsilonX = 1500;
        public static double EpsilonY = 1500;
        public static int MinPoints = 1;
        public static double MarginUp = 300;
        public static double MarginDown = 300;
        public static double MarginLeft = 300;
        public static double MarginRight = 300;
        public static double DistanceThreshold = 6000; // 用于 BaseDimension
        public static bool IsPointsToSpace = true;
        public static bool DrawClusterX = true;
        public static bool DrawClusterY = true;
    }
}
