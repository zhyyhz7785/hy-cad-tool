#nullable enable

using System;
using System.Text.RegularExpressions;

namespace HyCADTool.Shared.Drawing.ValueObjects
{
    /// <summary>图题主串清理：去掉历史上拼在 <c>Title.Text</c> 末尾的 " 1:N"（出图时比例改由 <see cref="DrawingSheetTitleDrawer"/> 单独写）。</summary>
    public static class DrawingSheetTitleText
    {
        private static readonly Regex TrailingScale = new Regex(@"\s+1:\d+\s*$", RegexOptions.Compiled);

        /// <summary>去掉行尾空白及末尾的 " 1:分母"。</summary>
        public static string RemoveTrailingScaleInTitle(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            return TrailingScale.Replace(text!.Trim(), string.Empty);
        }
    }
}
