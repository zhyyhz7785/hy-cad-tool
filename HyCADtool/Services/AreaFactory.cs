// HyCADTool/Services/AreaFactory.cs
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.HelpClass;
using HyCADTool.Interfaces;
namespace HyCADTool.Services
{
    public class AreaFactory : IAreaFactory
    {
        private readonly ICadService _cadService;
        private readonly IConfigService _configService;
        public AreaFactory(ICadService cadService, IConfigService configService)
        {
            _cadService = cadService;
            _configService = configService;
        }
        public StandardAreaBase CreateRectangularArea(Polyline polyline, PileSectionType sectionType, double diameterOrEdge, double scale)
        {
            var contour = polyline.ToRectNetTopologySuite();
            var insetRect = GeometryUtils.CreateInsetRect(contour, _configService.Margin, _cadService);
            if (contour == null)
            {
                _cadService.WriteMessage("\n警告：Polyline 不闭合");
                return null;
            }
            var pile = new Pile(sectionType, diameterOrEdge);
            return new RectangularStandardArea(
                _cadService,
                pile,
                contour,
                _configService.MinPileCenterDistance,
                _configService.Margin,
                _configService.InputDisplacementRate,
                _configService.InputDistanceFromContour,
                insetRect,
                scale
            );
        }
        public StandardAreaBase CreateCircularArea(Polyline polyline, PileSectionType sectionType, double diameterOrEdge, double scale)
        {
            var contour = polyline.ToRectNetTopologySuite();
            var insetRect = GeometryUtils.CreateInsetRect(contour, _configService.Margin, _cadService);
            if (contour == null)
            {
                _cadService.WriteMessage("\n警告：Polyline 不闭合");
                return null;
            }
            var pile = new Pile(sectionType, diameterOrEdge);
            return new CircularStandardArea(
                _cadService,
                pile,
                contour,
                _configService.MinPileCenterDistance,
                _configService.Margin,
                _configService.InputDisplacementRate,
                _configService.InputDistanceFromContour,
                insetRect,
                scale
            );
        }
    }
}