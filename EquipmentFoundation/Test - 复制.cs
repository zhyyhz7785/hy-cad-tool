using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;

namespace EquipmentFoundationTest
{
    public class PluginEntry : IExtensionApplication
    {
        public void Initialize()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc != null)
            {
                doc.Editor.WriteMessage("\n[插件已成功加载：EquipmentFoundationTest]\n");
            }
        }

        public void Terminate()
        {
            // 插件关闭前清理逻辑（可选）
        }
    }
}
