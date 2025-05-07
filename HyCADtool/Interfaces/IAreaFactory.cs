// HyCADTool/Interfaces/IAreaFactory.cs
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.HelpClass;
using PileSectionType = HyCADTool.HelpClass.PileSectionType;
namespace HyCADTool.Interfaces
{
    public interface IAreaFactory
    {
        StandardAreaBase CreateRectangularArea(Polyline polyline, PileSectionType sectionType, double diameterOrEdge, double scale);
        StandardAreaBase CreateCircularArea(Polyline polyline, PileSectionType sectionType, double diameterOrEdge, double scale);
    }
}