using HyCADTool.Tools;
namespace HyCADTool
{
    public static partial class Reinforcement
    {
        //[CommandMethod("gb")]
        ///截断钢筋
        public static void MleaderRein()
        {
            var ps = Tools.ZTools.GetReinPoints().ToArray();
            var ml = ps.AddMleader(MleaderDistance, $"\\U+E532{Reinforcement.RebarDiameter}@{Reinforcement.RebarSpacing}");
            ml.ToSpace();
        }
    }
}
