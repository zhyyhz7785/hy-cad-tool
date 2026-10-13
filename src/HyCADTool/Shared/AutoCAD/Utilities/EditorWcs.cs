using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System;

namespace HyCADTool.Shared.AutoCAD.Utilities
{
    /// <summary>
    /// Editor.GetPoint / GetEntity.PickedPoint / Jig 采样点都在 UCS（用户坐标系）。
    /// 写入模型或拿去和实体几何比较之前用 <see cref="ToWcs"/>。
    /// 橡皮筋 BasePoint 用 <see cref="ToUcs"/> 从已存的世界点变回来，不要把 <see cref="ToWcs"/> 的结果再喂回去。
    /// </summary>
    public static class EditorWcs
    {
        public static Point3d ToWcs(Editor editor, Point3d ucsPoint)
        {
            if (editor == null)
                throw new ArgumentNullException(nameof(editor));

            return ucsPoint.TransformBy(editor.CurrentUserCoordinateSystem);
        }

        public static Point3d ToUcs(Editor editor, Point3d wcsPoint)
        {
            if (editor == null)
                throw new ArgumentNullException(nameof(editor));

            return wcsPoint.TransformBy(editor.CurrentUserCoordinateSystem.Inverse());
        }

        /// <summary>
        /// 没有 Editor 时原样返回，避免 Jig 在文档关闭瞬间抛空引用。
        /// </summary>
        public static Point3d ToWcsOrSame(Editor editor, Point3d ucsPoint)
        {
            return editor == null ? ucsPoint : ToWcs(editor, ucsPoint);
        }

        /// <summary>
        /// 没有 Editor 时原样返回，避免 Jig 在文档关闭瞬间抛空引用。
        /// </summary>
        public static Point3d ToUcsOrSame(Editor editor, Point3d wcsPoint)
        {
            return editor == null ? wcsPoint : ToUcs(editor, wcsPoint);
        }
    }
}
