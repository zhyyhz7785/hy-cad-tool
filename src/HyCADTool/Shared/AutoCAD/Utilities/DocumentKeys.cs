using Autodesk.AutoCAD.ApplicationServices;

namespace HyCADTool.Shared.AutoCAD.Utilities
{
    /// <summary>
    /// 文档缓存键：组合非托管句柄与文件名，避免多个未保存图同为 Drawing1.dwg 时键冲突。
    /// </summary>
    public static class DocumentKeys
    {
        public static string GetKey(Document doc)
        {
            if (doc == null) return null;
            long handle = doc.UnmanagedObject.ToInt64();
            string name = doc.Name ?? "default";
            return $"{handle}:{name}";
        }
    }
}
