using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System;

namespace HyCADTool.Shared.AutoCAD.Utilities
{
    /// <summary>
    /// Editor.GetPoint 返回的是 UCS 点。写入模型空间前要变到 WCS。
    /// 橡皮筋的 BasePoint 仍用 UCS 原值，不要把本方法的结果再喂回去。
    /// </summary>
    public static class EditorWcs
    {
        public static Point3d ToWcs(Editor editor, Point3d ucsPoint)
        {
            if (editor == null)
                throw new ArgumentNullException(nameof(editor));

            return ucsPoint.TransformBy(editor.CurrentUserCoordinateSystem);
        }
    }
}
