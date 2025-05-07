using Autodesk.AutoCAD.DatabaseServices;
namespace HyCADTool
{
    public static partial class BaseRein
    {
        // 新方法：结合选择和分类
        public static void test()
        {
            // 首先调用 SelectEntities 方法获取 ObjectId[]
            ObjectId[] selectedIds = SelectPillarPiers();
            // 如果没有选择任何实体，返回 null
            if (selectedIds == null || selectedIds.Length == 0)
            {
            }
            // 调用 ClassifyEntities 方法对选择的实体进行分类
        }
    }
}
