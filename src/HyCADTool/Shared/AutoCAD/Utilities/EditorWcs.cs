using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System;

namespace HyCADTool.Shared.AutoCAD.Utilities
{
    /// <summary>
    /// <see cref="Editor.GetPoint"/> / <c>GetEntity.PickedPoint</c> 返回 UCS（用户坐标系）；
    /// <c>JigPrompts.AcquirePoint</c>（ObjectARX <c>AcEdJig::acquirePoint</c>）返回 WCS（世界坐标系）。
    /// GetPoint 写入模型前用 <see cref="ToWcs"/>；AcquirePoint 结果不要再 ToWcs。
    /// GetPoint 的橡皮筋 BasePoint 要 UCS（用 <see cref="ToUcs"/>）；
    /// AcquirePoint 的 BasePoint / UseBasePoint 要 WCS，不要先 ToUcs。
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
