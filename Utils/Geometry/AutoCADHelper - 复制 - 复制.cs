using Autodesk.AutoCAD.DatabaseServices;    // AutoCAD数据库服务
using Autodesk.AutoCAD.EditorInput;         // AutoCAD编辑器输入

namespace CadUtils
{
    /// <summary>
    /// 选择辅助工具类，提供用户输入的获取方法
    /// </summary>
    public static class SelectionUtil
    {
        /// <summary>
        /// 获取用户选择的剖面线
        /// </summary>
        /// <param name="ed">编辑器</param>
        /// <param name="tr">事务</param>
        /// <returns>剖面线，若未选择则返回null</returns>
        public static Line GetSectionLine(Editor ed, Transaction tr)
        {
            if (ed == null) throw new ArgumentNullException(nameof(ed));         // 检查编辑器是否为空
            if (tr == null) throw new ArgumentNullException(nameof(tr));         // 检查事务是否为空

            var options = new PromptEntityOptions("\n请选择一条直线作为剖面线："); // 设置选择提示
            options.SetRejectMessage("\n所选对象不是直线，请重新选择。\n");     // 设置拒绝消息
            options.AddAllowedClass(typeof(Line), true);                        // 限制为Line类型

            var result = ed.GetEntity(options);                                 // 获取用户选择
            if (result.Status != PromptStatus.OK)                               // 检查选择是否成功
            {
                ed.WriteMessage("未选择有效的剖面线。\n");
                return null;
            }

            var line = tr.GetObject(result.ObjectId, OpenMode.ForRead) as Line; // 获取Line对象
            if (line == null || line.StartPoint.Equals(line.EndPoint))          // 检查线是否有效
            {
                ed.WriteMessage("选择的剖面线无效。\n");
                return null;
            }

            return line;                                                        // 返回剖面线
        }

        /// <summary>
        /// 获取用户输入的偏移距离
        /// </summary>
        /// <param name="ed">编辑器</param>
        /// <returns>偏移距离，默认值为0</returns>
        public static double GetOffsetDistance(Editor ed)
        {
            if (ed == null) throw new ArgumentNullException(nameof(ed));         // 检查编辑器是否为空

            var options = new PromptDoubleOptions("\n请输入偏移距离（单位：mm，默认0）：") // 设置输入提示
            {
                AllowNegative = false,                                           // 不允许负值
                AllowZero = true,                                               // 允许零值
                DefaultValue = 0.0                                              // 默认值为0
            };

            var result = ed.GetDouble(options);                                 // 获取用户输入
            if (result.Status != PromptStatus.OK)                               // 检查输入是否成功
            {
                ed.WriteMessage("使用默认偏移距离：0mm。\n");
                return 0.0;
            }

            return result.Value;                                                // 返回偏移距离
        }
    }
}