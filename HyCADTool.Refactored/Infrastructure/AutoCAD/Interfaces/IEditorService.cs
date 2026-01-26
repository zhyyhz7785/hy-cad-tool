using Autodesk.AutoCAD.Geometry;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Interfaces
{
    /// <summary>
    /// 编辑器服务接口
    /// 封装 AutoCAD Editor 交互操作
    /// </summary>
    public interface IEditorService
    {
        /// <summary>
        /// 输出消息到命令行
        /// </summary>
        void WriteMessage(string message);

        /// <summary>
        /// 输出警告消息
        /// </summary>
        void WriteWarning(string message);

        /// <summary>
        /// 输出错误消息
        /// </summary>
        void WriteError(string message);

        /// <summary>
        /// 获取用户输入的点
        /// </summary>
        Point3d? GetPoint(string prompt);

        /// <summary>
        /// 获取相对于基点的用户输入点
        /// </summary>
        Point3d? GetPoint(string prompt, Point3d basePoint);

        /// <summary>
        /// 获取角度点
        /// </summary>
        Point3d? GetAnglePoint(string prompt, Point3d basePoint);

        /// <summary>
        /// 获取距离
        /// </summary>
        double? GetDistance(string prompt);

        /// <summary>
        /// 获取角度
        /// </summary>
        double? GetAngle(string prompt);

        /// <summary>
        /// 获取整数
        /// </summary>
        int? GetInteger(string prompt);

        /// <summary>
        /// 获取双精度数
        /// </summary>
        double? GetDouble(string prompt);

        /// <summary>
        /// 获取字符串
        /// </summary>
        string GetString(string prompt, bool allowSpaces = true);

        /// <summary>
        /// 获取关键字
        /// </summary>
        string GetKeyword(string prompt, params string[] keywords);

        /// <summary>
        /// 获取带默认值的关键字
        /// </summary>
        string GetKeyword(string prompt, string defaultKeyword, params string[] keywords);

        /// <summary>
        /// 获取是否确认
        /// </summary>
        bool GetYesNo(string prompt, bool defaultValue = true);
    }
}
