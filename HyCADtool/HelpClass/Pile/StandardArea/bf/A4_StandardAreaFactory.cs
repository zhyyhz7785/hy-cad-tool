using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using NetTopologySuite.Geometries;

namespace HyCADTool.HelpClass
{
    public static class StandardAreaFactory
    {
        public static RectangularStandardArea CreateRectangularArea(Polyline polyline, SectionType sectionType, double diameterOrEdge)
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;

            var contour = polyline.ToRectNetTopologySuite();
            var insetRect = GeometryUtils.CreateInsetRect(contour, HyCADTool.PileConfig.Instance.Margin);

            if (contour == null)
            {
                ed.WriteMessage("\n警告：Polyline 不闭合");
                return null;
            }

            Pile pile = new Pile(sectionType, diameterOrEdge);
            return new RectangularStandardArea(
                pile, contour, HyCADTool.PileConfig.Instance.MinPileCenterDistance,
                HyCADTool.PileConfig.Instance.Margin, HyCADTool.PileConfig.Instance.InputDisplacementRate,
                HyCADTool.PileConfig.Instance.InputDistanceFromContour, insetRect
            );
        }

        public static CircularStandardArea CreateCircularArea(Polyline polyline, SectionType sectionType, double diameterOrEdge)
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;

            var contour = polyline.ToRectNetTopologySuite();
            var insetRect = GeometryUtils.CreateInsetRect(contour, HyCADTool.PileConfig.Instance.Margin);

            if (contour == null)
            {
                ed.WriteMessage("\n警告：Polyline 不闭合");
                return null;
            }

            Pile pile = new Pile(sectionType, diameterOrEdge);
            return new CircularStandardArea(
                pile, contour, HyCADTool.PileConfig.Instance.MinPileCenterDistance,
                HyCADTool.PileConfig.Instance.Margin, HyCADTool.PileConfig.Instance.InputDisplacementRate,
                HyCADTool.PileConfig.Instance.InputDistanceFromContour, insetRect
            );
        }
    }
}