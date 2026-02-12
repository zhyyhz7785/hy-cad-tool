using Autodesk.AutoCAD.ApplicationServices;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services;

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 构建底座数据 (hyef_Base_ConstructBaseData)
    /// 选择螺栓轮廓多段线/圆/编号文字 → 构建 BaseData 写入扩展字典
    /// </summary>
    public class EF_ConstructBaseDataCommand
    {
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            EquipmentFoundationService.ConstructBaseData(doc.Database, doc.Editor);
        }
    }

    /// <summary>
    /// 高亮螺栓数据 (hyef_Base_HighlightBoltData)
    /// 选择底座多段线 → 高亮关联螺栓圆，显示设备信息
    /// </summary>
    public class EF_HighlightBoltDataCommand
    {
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            EquipmentFoundationService.HighlightBoltData(doc.Database, doc.Editor);
        }
    }

    /// <summary>
    /// 构建轴线 (hyef_Axis_Construct)
    /// 选择 Y 向轴线直线/文字 + X 向轴线 → 创建 AxisData，绘制序号圆+文字
    /// </summary>
    public class EF_AxisConstructCommand
    {
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var axes = EquipmentFoundationService.ConstructAxis(doc.Database, doc.Editor);
            if (axes != null && axes.Count > 0)
                doc.Editor.WriteMessage($"\n成功构造 {axes.Count} 条轴线！");
            else
                doc.Editor.WriteMessage("\n轴线构造失败或未选择任何直线！");
        }
    }

    /// <summary>
    /// 初始化轴线 (hyef_Axis_Initialize)
    /// 选择轴线 → 选择底座多段线 → 关联底座到轴线
    /// </summary>
    public class EF_AxisInitializeCommand
    {
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var axis = EquipmentFoundationService.InitializeAxis(doc.Database, doc.Editor);
            if (axis != null)
                doc.Editor.WriteMessage($"\n轴线初始化完成，底座数量: {axis.Bases.Count}");
            else
                doc.Editor.WriteMessage("\n轴线初始化失败或直线没有附加数据！");
        }
    }

    /// <summary>
    /// 显示轴线结构 (hyef_Axis_Display)
    /// 选择轴线 → 高亮关联底座和螺栓
    /// </summary>
    public class EF_AxisDisplayCommand
    {
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            EquipmentFoundationService.DisplayStructure(doc.Database, doc.Editor);
        }
    }

    /// <summary>
    /// 创建轴线表格 (hyef_Axis_CreateTable)
    /// 选择轴线 → 为每个底座的螺栓创建 XY 相对坐标表格
    /// </summary>
    public class EF_AxisCreateTableCommand
    {
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            EquipmentFoundationService.CreateAxisTable(doc.Database, doc.Editor);
        }
    }
}
