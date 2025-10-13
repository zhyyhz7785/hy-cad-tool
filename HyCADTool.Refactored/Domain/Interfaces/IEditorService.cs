using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace HyCADTool.Refactored.Domain.Interfaces
{
    /// <summary>
    /// 编辑器交互服务接口 (Editor Interaction Service Interface)
    /// 负责处理用户输入、消息输出等编辑器交互操作
    /// </summary>
    /// <remarks>
    /// 设计原则 (Design Principles):
    /// 1. 统一交互接口 (Unified Interaction Interface) - 封装 AutoCAD Editor 的复杂 API
    /// 2. 类型安全 (Type Safety) - 返回明确的类型而非 PromptResult
    /// 3. 用户体验 (User Experience) - 提供友好的提示和错误处理
    /// </remarks>
    public interface IEditorService
    {
        #region 消息输出 (Message Output)

        /// <summary>
        /// 输出普通消息 (Write Message)
        /// </summary>
        /// <param name="message">消息内容 (Message Content)</param>
        void WriteMessage(string message);

        /// <summary>
        /// 输出警告消息 (Write Warning)
        /// </summary>
        /// <param name="message">警告内容 (Warning Content)</param>
        void WriteWarning(string message);

        /// <summary>
        /// 输出错误消息 (Write Error)
        /// </summary>
        /// <param name="message">错误内容 (Error Content)</param>
        void WriteError(string message);

        #endregion

        #region 点输入 (Point Input)

        /// <summary>
        /// 获取用户输入的点 (Get Point from User)
        /// </summary>
        /// <param name="prompt">提示信息 (Prompt Message)</param>
        /// <returns>用户选择的点，取消则返回 null</returns>
        Point3d? GetPoint(string prompt);

        /// <summary>
        /// 获取用户输入的点（基于基点） (Get Point Based on Base Point)
        /// </summary>
        /// <param name="prompt">提示信息 (Prompt Message)</param>
        /// <param name="basePoint">基点 (Base Point)</param>
        /// <returns>用户选择的点，取消则返回 null</returns>
        Point3d? GetPoint(string prompt, Point3d basePoint);

        /// <summary>
        /// 获取用户输入的角度点 (Get Angle Point)
        /// </summary>
        /// <param name="prompt">提示信息 (Prompt Message)</param>
        /// <param name="basePoint">基点 (Base Point)</param>
        /// <returns>用户选择的点，取消则返回 null</returns>
        Point3d? GetAnglePoint(string prompt, Point3d basePoint);

        #endregion

        #region 数值输入 (Numeric Input)

        /// <summary>
        /// 获取用户输入的距离 (Get Distance from User)
        /// </summary>
        /// <param name="prompt">提示信息 (Prompt Message)</param>
        /// <returns>用户输入的距离，取消则返回 null</returns>
        double? GetDistance(string prompt);

        /// <summary>
        /// 获取用户输入的角度 (Get Angle from User)
        /// </summary>
        /// <param name="prompt">提示信息 (Prompt Message)</param>
        /// <returns>用户输入的角度（弧度），取消则返回 null</returns>
        double? GetAngle(string prompt);

        /// <summary>
        /// 获取用户输入的整数 (Get Integer from User)
        /// </summary>
        /// <param name="prompt">提示信息 (Prompt Message)</param>
        /// <returns>用户输入的整数，取消则返回 null</returns>
        int? GetInteger(string prompt);

        /// <summary>
        /// 获取用户输入的浮点数 (Get Double from User)
        /// </summary>
        /// <param name="prompt">提示信息 (Prompt Message)</param>
        /// <returns>用户输入的浮点数，取消则返回 null</returns>
        double? GetDouble(string prompt);

        #endregion

        #region 字符串输入 (String Input)

        /// <summary>
        /// 获取用户输入的字符串 (Get String from User)
        /// </summary>
        /// <param name="prompt">提示信息 (Prompt Message)</param>
        /// <param name="allowSpaces">是否允许空格 (Allow Spaces)</param>
        /// <returns>用户输入的字符串，取消则返回 null</returns>
        string GetString(string prompt, bool allowSpaces = true);

        #endregion

        #region 关键字选择 (Keyword Selection)

        /// <summary>
        /// 获取用户选择的关键字 (Get Keyword from User)
        /// </summary>
        /// <param name="prompt">提示信息 (Prompt Message)</param>
        /// <param name="keywords">关键字数组 (Keyword Array)</param>
        /// <returns>用户选择的关键字，取消则返回 null</returns>
        string GetKeyword(string prompt, params string[] keywords);

        /// <summary>
        /// 获取用户选择的关键字（带默认值） (Get Keyword with Default)
        /// </summary>
        /// <param name="prompt">提示信息 (Prompt Message)</param>
        /// <param name="defaultKeyword">默认关键字 (Default Keyword)</param>
        /// <param name="keywords">关键字数组 (Keyword Array)</param>
        /// <returns>用户选择的关键字或默认值，取消则返回 null</returns>
        string GetKeyword(string prompt, string defaultKeyword, params string[] keywords);

        #endregion

        #region 确认输入 (Confirmation Input)

        /// <summary>
        /// 获取用户的是/否确认 (Get Yes/No Confirmation)
        /// </summary>
        /// <param name="prompt">提示信息 (Prompt Message)</param>
        /// <param name="defaultValue">默认值 (Default Value)</param>
        /// <returns>用户选择：true=是，false=否</returns>
        bool GetYesNo(string prompt, bool defaultValue = true);

        #endregion
    }
}

