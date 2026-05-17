using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace HyCADTool.Features.TextEdit.Services
{
    /// <summary>
    /// MText / MLeader.Contents 的"控制码剥离 / 还原"编解码器，让用户在 hyed 编辑框里看到的是
    /// **纯文本**（不被 <c>\T1.137;</c>、<c>{\L...}</c>、<c>{\C1;...}</c> 等控制串干扰），
    /// 提交时把外层控制码原样拼回。
    /// <para>
    /// 处理范围（按用户实际场景命中率从高到低）：
    /// <list type="bullet">
    ///   <item>前缀格式控制码：<c>\T...;</c> <c>\W...;</c> <c>\Q...;</c> <c>\H...;</c> <c>\F...;</c> <c>\A...;</c></item>
    ///   <item>外层 group brace：<c>{\L...}</c> <c>{\O...}</c> <c>{\K...}</c> <c>{\C\d+;...}</c>（可多层嵌套）</item>
    ///   <item>字面转义：<c>\P</c>→换行、<c>\~</c>→不间断空格、<c>\\</c>→反斜杠、<c>\{</c><c>\}</c>→大括号</item>
    /// </list>
    /// 不可剥离的情况（如中间穿插的 <c>{\C1;红}{\C2;蓝}</c> 多色文字）：<see cref="MTextWrap.RawFallback"/>=true，
    /// 编辑框直接显示原始控制串，编码时原样写回（保持安全，不破坏复杂格式）。
    /// </para>
    /// </summary>
    public static class MTextCodec
    {
        /// <summary>
        /// 前缀控制码正则：0 或多个 <c>\X...;</c>（X 单字母，参数不含 <c>; \ { }</c>）。
        /// 例如 <c>\T1.137;</c>、<c>\W0.8;</c>、<c>\Q15;</c>、<c>\H2.5x;</c>。
        /// </summary>
        private static readonly Regex PrefixRegex = new Regex(
            @"^((?:\\[A-Za-z](?:[^;\\{}]*;)?)*)",
            RegexOptions.Compiled);

        /// <summary>
        /// group brace 控制头正则：连续多个 <c>\X[args];</c> 或单字母无参 <c>\L</c><c>\l</c><c>\O</c><c>\o</c><c>\K</c><c>\k</c>。
        /// 例如 <c>{\L 消防}</c> 的 brace 头是 <c>\L </c>；<c>{\C1;红}</c> 的 brace 头是 <c>\C1;</c>。
        /// </summary>
        private static readonly Regex BraceHeadRegex = new Regex(
            @"^((?:\\[LlOoKk]|\\[A-Za-z][^;\\{}]*;)+)",
            RegexOptions.Compiled);

        /// <summary>
        /// 解析 MText 原始 Contents → 可编辑纯文本 + 可还原的 wrap 信息。
        /// 任何剥不干净的情况均回退为 <see cref="MTextWrap.RawFallback"/>=true，保证可逆。
        /// </summary>
        public static void Decode(string raw, out string editable, out MTextWrap wrap)
        {
            wrap = new MTextWrap { Original = raw ?? string.Empty };
            if (string.IsNullOrEmpty(raw))
            {
                editable = string.Empty;
                return;
            }

            string s = raw;

            var pre = PrefixRegex.Match(s);
            if (pre.Success && pre.Length > 0)
            {
                wrap.Prefix = pre.Value;
                s = s.Substring(pre.Length);
            }

            // 递归剥外层 group brace —— 每剥一层把"{头部"压栈。
            // 不在剥的循环里检查 after 内容；让循环自己处理嵌套（after 仍形如 {...} 时自然继续剥），
            // 剥不动后再统一 final-check：若剩余串含未转义控制码或 brace 则整体 fallback。
            while (s.Length >= 2 && s[0] == '{' && s[s.Length - 1] == '}' && IsOuterBraceBalanced(s))
            {
                string inner = s.Substring(1, s.Length - 2);
                var headMatch = BraceHeadRegex.Match(inner);
                if (!headMatch.Success || headMatch.Length == 0)
                    break;

                string head = headMatch.Value;
                wrap.BraceOpens.Add("{" + head);
                s = inner.Substring(head.Length);
            }

            if (ContainsControlCodeOrBrace(s))
            {
                // 残留控制码 → 整体回退：清空已剥信息，编辑框原样显示，Encode 时也原样写回。
                wrap.Prefix = string.Empty;
                wrap.BraceOpens.Clear();
                wrap.RawFallback = true;
                editable = raw;
                return;
            }

            editable = UnescapeMText(s);
        }

        /// <summary>把编辑后的纯文本按 wrap 信息组装回 MText Contents。</summary>
        public static string Encode(string editable, MTextWrap wrap)
        {
            if (wrap == null || wrap.RawFallback)
                return editable ?? string.Empty;

            string body = EscapeMText(editable ?? string.Empty);
            for (int i = wrap.BraceOpens.Count - 1; i >= 0; i--)
                body = wrap.BraceOpens[i] + body + "}";

            return wrap.Prefix + body;
        }

        /// <summary>
        /// 判定 <paramref name="s"/> 的外层一对 <c>{ }</c> 是否包含整个串。<c>\X</c> 之后单字符跳过，避免误判 <c>\{</c><c>\}</c>。
        /// </summary>
        private static bool IsOuterBraceBalanced(string s)
        {
            int depth = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    i++;
                    continue;
                }
                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && i != s.Length - 1)
                        return false;
                    if (depth < 0)
                        return false;
                }
            }
            return depth == 0;
        }

        /// <summary>
        /// 判定 <paramref name="s"/> 是否还含**未转义**的 <c>{</c> <c>}</c> 或带参/无参控制码 <c>\X</c>（X 是字母）。
        /// 字面转义 <c>\P</c> <c>\~</c> <c>\\</c> <c>\{</c> <c>\}</c> 不计。
        /// </summary>
        private static bool ContainsControlCodeOrBrace(string s)
        {
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char next = s[i + 1];
                    if (next == 'P' || next == '~' || next == '\\' || next == '{' || next == '}')
                    {
                        i += 2;
                        continue;
                    }
                    if (char.IsLetter(next))
                        return true;
                    i++;
                    continue;
                }
                if (c == '{' || c == '}')
                    return true;
                i++;
            }
            return false;
        }

        /// <summary>MText 字面转义 → UI 显示：<c>\P</c>→换行、<c>\~</c>→不间断空格、<c>\\</c>→反斜杠、<c>\{</c>/<c>\}</c>→括号。</summary>
        private static string UnescapeMText(string s)
        {
            const char placeholder = '\u0001';
            return s
                .Replace("\\\\", placeholder.ToString())
                .Replace("\\P", "\n")
                .Replace("\\~", "\u00A0")
                .Replace("\\{", "{")
                .Replace("\\}", "}")
                .Replace(placeholder.ToString(), "\\");
        }

        /// <summary>UI 文本 → MText 字面：反向操作。注意顺序：先转 <c>\</c>，再转 <c>{</c> <c>}</c>。</summary>
        private static string EscapeMText(string s)
        {
            return s
                .Replace("\\", "\\\\")
                .Replace("{", "\\{")
                .Replace("}", "\\}")
                .Replace("\u00A0", "\\~")
                .Replace("\n", "\\P");
        }
    }

    /// <summary>MText Decode 的"剥下来的信息"——后续 Encode 拼回时使用。</summary>
    public sealed class MTextWrap
    {
        /// <summary>前缀控制码（含末尾分号），例如 <c>\T1.137;\W0.8;</c>。空字符串=无前缀。</summary>
        public string Prefix { get; set; } = string.Empty;

        /// <summary>每层 group brace 的"{头部"，外→内顺序入栈。例如 <c>{\L</c> 表示下划线层。</summary>
        public List<string> BraceOpens { get; } = new List<string>();

        /// <summary>true=剥离失败/复杂结构，<see cref="MTextCodec.Encode"/> 时原样使用 <see cref="MTextWrap.Original"/>。</summary>
        public bool RawFallback { get; set; }

        /// <summary>原始 Contents 串备份，仅 <see cref="RawFallback"/>=true 时有用。</summary>
        public string Original { get; set; } = string.Empty;
    }
}
