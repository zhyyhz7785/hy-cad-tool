using Autodesk.AutoCAD.ApplicationServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Shared.AutoCAD.Session
{
    /// <summary>
    /// 从活动文档的 Database 取会话槽。没有活动图时返回 null，不写共享槽。
    /// </summary>
    public static class ActiveDocumentSession
    {
        public static T GetOrNull<T>() where T : class, new()
        {
            var key = TryGetKey();
            return key == null ? null : DocumentSessionStore.Get<T>(key);
        }

        public static object TryGetKey()
        {
            try
            {
                return AcApp.DocumentManager.MdiActiveDocument?.Database;
            }
            catch
            {
                return null;
            }
        }
    }
}
