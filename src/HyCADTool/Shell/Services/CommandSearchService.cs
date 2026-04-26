using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using HyCADTool.Presentation.ViewModels;

namespace HyCADTool.Shell.Services
{
    /// <summary>
    /// Hy 面板命令搜索：维护跨 Tab 扁平索引，支持当前 Tab 内过滤与全局搜索（关键词 + 拼音首字母）。
    /// </summary>
    public sealed class CommandSearchService
    {
        private const string PreferencesTabKey = "__preferences__";
        private const string FilterTabKey = "__filter__";

        private readonly List<CommandItemVm> _flatAll = new List<CommandItemVm>();

        public void Rebuild(ObservableCollection<CategoryTabVm> tabs)
        {
            _flatAll.Clear();
            if (tabs == null) return;
            foreach (var t in tabs)
            {
                if (t == null) continue;
                if (string.Equals(t.Key, PreferencesTabKey, StringComparison.Ordinal)
                    || string.Equals(t.Key, FilterTabKey, StringComparison.Ordinal))
                    continue;
                foreach (var it in t.Items)
                    _flatAll.Add(it);
            }
        }

        public IEnumerable<CommandItemVm> SearchInTab(CategoryTabVm tab, string searchText)
        {
            if (tab == null || string.IsNullOrWhiteSpace(searchText)) return Array.Empty<CommandItemVm>();
            var q = searchText.Trim();
            return tab.Items.Where(it => Matches(it, q));
        }

        public IEnumerable<CommandItemVm> SearchAll(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText)) return Array.Empty<CommandItemVm>();
            var q = searchText.Trim();
            return _flatAll.Where(it => Matches(it, q));
        }

        private static bool Matches(CommandItemVm it, string queryLower)
        {
            if (it == null || string.IsNullOrEmpty(queryLower)) return false;
            var q = queryLower.ToLowerInvariant();
            if (it.MatchText.Contains(q)) return true;
            var fl = PinyinHelper.GetFirstLetters(it.DisplayName ?? string.Empty);
            return fl.IndexOf(q, StringComparison.Ordinal) >= 0;
        }
    }
}
