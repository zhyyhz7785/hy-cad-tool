using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HyCADTool.Refactored.Presentation.Services
{
    /// <summary>
    /// hy 面板命令搜索服务：
    /// - 启动时把全量命令压成扁平索引（Key / DisplayName / 拼音首字母 / 短别名）；
    /// - 运行时按关键词做 O(N) 线性匹配，避免每次 RefreshFilter 都重算 PinyinFirstLetters；
    /// - 返回命中的原始 <see cref="ViewModels.CommandItemVm"/>，保留绑定身份。
    /// 无跨线程需求：所有调用都在 UI 线程。
    /// </summary>
    public sealed class CommandSearchService
    {
        private readonly List<Entry> _entries = new List<Entry>();

        /// <summary>重建扁平索引。每当 <c>Tabs</c> 重新从 CommandTable 加载后调一次即可。</summary>
        public void Rebuild(IEnumerable<ViewModels.CategoryTabVm> tabs)
        {
            _entries.Clear();
            if (tabs == null) return;
            foreach (var tab in tabs)
            {
                if (tab == null) continue;
                // 跳过伪分类（设置/过滤），它们自身没有可搜命令
                if (tab.Key == ViewModels.HyBlenderPanelViewModel.PreferencesTabKey) continue;
                if (tab.Key == ViewModels.HyBlenderPanelViewModel.FilterTabKey) continue;
                foreach (var it in tab.Items)
                {
                    if (it == null) continue;
                    var pinyin = ViewModels.PinyinHelper.GetFirstLetters(it.DisplayName);
                    _entries.Add(new Entry
                    {
                        Item = it,
                        CategoryKey = tab.Key,
                        Haystack = it.MatchText,
                        Pinyin = pinyin,
                    });
                }
            }
        }

        /// <summary>扁平搜索全部命令（忽略 Tab 边界）。空串返回空。</summary>
        public IEnumerable<ViewModels.CommandItemVm> SearchAll(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword)) yield break;
            var kw = keyword.Trim().ToLowerInvariant();
            foreach (var e in _entries)
                if (Match(e, kw)) yield return e.Item;
        }

        /// <summary>在给定 Tab 内搜索命令。kw 为空时返回该 Tab 所有命令。</summary>
        public IEnumerable<ViewModels.CommandItemVm> SearchInTab(ViewModels.CategoryTabVm tab, string keyword)
        {
            if (tab == null) yield break;
            if (string.IsNullOrWhiteSpace(keyword))
            {
                foreach (var it in tab.Items) yield return it;
                yield break;
            }
            var kw = keyword.Trim().ToLowerInvariant();
            var tabKey = tab.Key;
            foreach (var e in _entries)
            {
                if (!string.Equals(e.CategoryKey, tabKey, StringComparison.Ordinal)) continue;
                if (Match(e, kw)) yield return e.Item;
            }
        }

        private static bool Match(Entry e, string kw)
        {
            if (e.Haystack.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return e.Pinyin.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private sealed class Entry
        {
            public ViewModels.CommandItemVm Item;
            public string CategoryKey;
            public string Haystack;
            public string Pinyin;
        }
    }
}
