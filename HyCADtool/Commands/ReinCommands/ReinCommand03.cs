using Autodesk.AutoCAD.Runtime;
using HyCADTool.Tools;
using System.Linq;
[assembly: CommandClass(typeof(HyCADTool.Commands.HyCommand))]
namespace HyCADTool.Commands
{
    public static partial class HyCommand
    {
        [CommandMethod("gb")]
        ///截断钢筋
        public static void MleaderRein()
        {
            Tools.Tools.SetCurrentLayer("00_hy_3公共_标注3_引线");
            var psL = Tools.Tools.GetReinPoints();
            if (psL == null)
            {
                return;
            }
            var ps = psL.ToArray();
            var ml = ps.AddMleader(Reinforcement.MleaderDistance, $"\\U+E532{Reinforcement.RebarDiameter}@{Reinforcement.RebarSpacing}");
            var psDraw = ps.Where((point, index) => index != 1).ToArray().PointsToDotRein();
            var layerId = "01_hy_1钢筋_点钢筋".GetLayerId();
            foreach (var pl in psDraw)
            {
                pl.LayerId = layerId;
            }
            psDraw.ToSpace();
            ml.ToSpace();
        }
        [CommandMethod("gb1")]
        ///截断钢筋
        public static void MleaderReinOne()
        {
            Tools.Tools.SetCurrentLayer("00_hy_3公共_标注3_引线");
            var psL = Tools.Tools.GetReinPoints();
            if (psL == null)
            {
                return;
            }
            var ps = psL.ToArray();
            if (ps.Length == 0)
            {
                return;
            }
            var ml = ps.AddMleaderOne(Reinforcement.MleaderDistance, $"\\U+E532{Reinforcement.RebarDiameter}@{Reinforcement.RebarSpacing}");
            ml.ToSpace();
        }
        [CommandMethod("gb2")]
        ///截断钢筋
        public static void MleaderReinTwo()
        {
            Tools.Tools.SetCurrentLayer("00_hy_3公共_标注3_引线");
            var psL = Tools.Tools.GetReinPointsSix();
            if (psL == null)
            {
                return;
            }
            var ps = psL.ToArray();
            if (ps.Length == 0)
            {
                return;
            }
            var ml = ps.AddMleaderSix(465, $"\\U+E532{Reinforcement.RebarDiameter}@{Reinforcement.RebarSpacing}");
            var psDraw = ps.Where((point, index) => index != 1 && index != 4).ToArray().PointsToDotRein();
            var layerId = "01_hy_1钢筋_点钢筋".GetLayerId();
            foreach (var pl in psDraw)
            {
                pl.LayerId = layerId;
            }
            psDraw.ToSpace();
            ml.ToSpace();
        }
    }
}
