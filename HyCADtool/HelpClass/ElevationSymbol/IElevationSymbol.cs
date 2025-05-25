using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
namespace HyCADTool.HelpClass.ElevationSymbol
{
    public enum ElevationSymbolState
    {
        Normal,
        FlipVertical,
        FlipHorizontal,
        FlipBoth
    }
    public interface IElevationSymbol
    {
        Point3d CurrentPoint { get; }
        Point3d BasePoint { get; }
        ElevationSymbolState State { get; set; }
        void UpdateSymbol(double elevation, bool isBasePoint = false);
        void SetLabelProperties(DBText sourceText);
        void AddToDatabase(Transaction tr, BlockTableRecord btr);
        PromptResult StartJig(Editor editor); // 新增方法，启动 Jig 交互
    }
}