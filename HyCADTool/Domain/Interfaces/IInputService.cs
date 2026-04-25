using System.Collections.Generic;

namespace HyCADTool.Domain.Interfaces
{
    /// <summary>
    /// 用户输入服务接口（平台无关）
    /// 定义用户交互输入的抽象操作
    /// </summary>
    public interface IInputService
    {
        // === 点输入 ===

        /// <summary>
        /// 获取用户输入的点
        /// </summary>
        /// <param name="promptMessage">提示信息</param>
        /// <returns>用户输入的点，取消返回null</returns>
        (double X, double Y, double Z)? GetUserPoint(string promptMessage = "请输入一个点: ");

        /// <summary>
        /// 获取相对于基点的用户输入点
        /// </summary>
        /// <param name="basePoint">基点</param>
        /// <param name="promptMessage">提示信息</param>
        /// <returns>用户输入的点，取消返回null</returns>
        (double X, double Y, double Z)? GetUserPointFromBase((double X, double Y, double Z) basePoint, string promptMessage = "请输入相对点: ");

        /// <summary>
        /// 获取多个连续的点
        /// </summary>
        /// <param name="promptMessage">提示信息</param>
        /// <param name="allowClose">是否允许闭合</param>
        /// <returns>点的集合</returns>
        List<(double X, double Y, double Z)> GetUserPoints(string promptMessage = "请输入点: ", bool allowClose = false);

        // === 数值输入 ===

        /// <summary>
        /// 获取用户输入的距离
        /// </summary>
        /// <param name="promptMessage">提示信息</param>
        /// <param name="basePoint">基点（可选）</param>
        /// <returns>距离值，取消返回null</returns>
        double? GetUserDistance(string promptMessage = "请输入距离: ", (double X, double Y, double Z)? basePoint = null);

        /// <summary>
        /// 获取用户输入的角度
        /// </summary>
        /// <param name="promptMessage">提示信息</param>
        /// <param name="basePoint">基点（可选）</param>
        /// <returns>角度值（弧度），取消返回null</returns>
        double? GetUserAngle(string promptMessage = "请输入角度: ", (double X, double Y, double Z)? basePoint = null);

        /// <summary>
        /// 获取用户输入的整数
        /// </summary>
        /// <param name="promptMessage">提示信息</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>整数值，取消返回null</returns>
        int? GetUserInteger(string promptMessage = "请输入整数: ", int? defaultValue = null);

        /// <summary>
        /// 获取用户输入的浮点数
        /// </summary>
        /// <param name="promptMessage">提示信息</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>浮点数值，取消返回null</returns>
        double? GetUserDouble(string promptMessage = "请输入数值: ", double? defaultValue = null);

        // === 文字输入 ===

        /// <summary>
        /// 获取用户输入的字符串
        /// </summary>
        /// <param name="promptMessage">提示信息</param>
        /// <param name="defaultValue">默认值</param>
        /// <param name="allowSpaces">是否允许空格</param>
        /// <returns>字符串，取消返回null</returns>
        string GetUserString(string promptMessage = "请输入字符串: ", string defaultValue = null, bool allowSpaces = true);

        /// <summary>
        /// 获取用户选择的关键字
        /// </summary>
        /// <param name="promptMessage">提示信息</param>
        /// <param name="keywords">可选关键字列表</param>
        /// <param name="defaultKeyword">默认关键字</param>
        /// <returns>选择的关键字，取消返回null</returns>
        string GetUserKeyword(string promptMessage, string[] keywords, string defaultKeyword = null);

        // === 实体选择 ===

        /// <summary>
        /// 获取用户选择的单个实体
        /// </summary>
        /// <param name="promptMessage">提示信息</param>
        /// <param name="entityType">限制的实体类型（可选）</param>
        /// <returns>实体ID，取消返回null</returns>
        string GetUserEntity(string promptMessage = "请选择实体: ", string entityType = null);

        /// <summary>
        /// 获取用户选择的多个实体
        /// </summary>
        /// <param name="promptMessage">提示信息</param>
        /// <param name="entityTypes">限制的实体类型（可选）</param>
        /// <returns>实体ID列表</returns>
        List<string> GetUserEntities(string promptMessage = "请选择实体: ", string[] entityTypes = null);

        // === 特殊输入 ===

        /// <summary>
        /// 获取是否确认
        /// </summary>
        /// <param name="promptMessage">提示信息</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>用户确认结果</returns>
        bool GetUserConfirmation(string promptMessage = "是否确认? ", bool defaultValue = true);

        /// <summary>
        /// 获取文件路径
        /// </summary>
        /// <param name="promptMessage">提示信息</param>
        /// <param name="fileExtension">文件扩展名</param>
        /// <param name="isOpen">是否为打开文件对话框</param>
        /// <returns>文件路径，取消返回null</returns>
        string GetUserFilePath(string promptMessage = "请选择文件: ", string fileExtension = "*.*", bool isOpen = true);

        /// <summary>
        /// 获取窗口选择
        /// </summary>
        /// <param name="promptMessage">提示信息</param>
        /// <returns>窗口的两个角点，取消返回null</returns>
        ((double X, double Y, double Z) Point1, (double X, double Y, double Z) Point2)? GetUserWindow(string promptMessage = "请选择窗口: ");
    }
}

