using System;
using System.Collections.Specialized;
using System.IO;
using Autodesk.AutoCAD.Customization;
using HyCADTool.ReCall;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.UI
{
    /// <summary>
    /// 向 AutoCAD 经典菜单栏注入一个 <c>HyCAD</c> 菜单组。
    ///
    /// 流程：
    /// 1) 目标写盘位置：<c>%APPDATA%\HyCAD\HyCAD.cuix</c>（避免 Program Files 权限问题）。
    /// 2) 以 commands.json 的 mtime 为版本号：.cuix 不存在或比 commands.json 旧，重建。
    /// 3) 构建成功后调用 <c>Application.LoadPartialMenu</c> 挂载；重复加载先 <c>UnloadPartialMenu</c>。
    /// 4) Terminate 时调用 <see cref="Unload"/>，避免 C2 热重载残留。
    ///
    /// 说明：CUIX 菜单栏是经典工作区入口，与 Ribbon 并存；两者数据源同一份 commands.json。
    ///
    /// 关键 API（经反射校正）：
    /// - <c>new CustomizationSection(path, menuGroupName)</c> 创建新 .cuix 并落盘
    /// - <c>new MacroGroup(name, menuGroup)</c> 自动挂到 <c>MenuGroup.MacroGroups</c>
    /// - <c>new MenuMacro(macroGroup, displayName, macro, elementID)</c> 自动挂到 <c>MacroGroup.MenuMacros</c>
    /// - <c>new PopMenu(name, aliases, tag, menuGroup)</c> 自动挂到 <c>MenuGroup.PopMenus</c>
    /// - <c>new PopMenuItem(menuMacro, displayName, parentPopMenu, index)</c> 自动挂到 <c>PopMenu.PopMenuItems</c>
    /// - <c>new PopMenuRef(targetPopMenu, parentPopMenu, index)</c> 把子菜单挂到父菜单，index=-1 表示追加
    /// - <c>cs.Save()</c> 保存到构造时指定的路径；没有 SaveAs。
    /// </summary>
    public static class CuiMenuBuilder
    {
        private const string MenuGroupName = "HYCAD";
        private const string RootPopElementId = "HYCAD_MENU_ROOT";

        private static string _lastLoadedCuixPath;

        /// <summary>目标 CUIX 绝对路径（始终返回，未必存在）。</summary>
        public static string TargetCuixPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HyCAD",
            "HyCAD.cuix");

        /// <summary>
        /// 确保 CUIX 已加载到 AutoCAD：必要时重建文件，再 LoadPartialMenu。
        /// </summary>
        public static void EnsureLoaded()
        {
            string target = TargetCuixPath;
            EnsureDirectoryExists(Path.GetDirectoryName(target));

            if (NeedsRebuild(target))
            {
                try
                {
                    BuildCuix(target);
                }
                catch (UnauthorizedAccessException)
                {
                    return;
                }
            }

            LoadPartialMenu(target);
        }

        /// <summary>卸载 HyCAD 菜单组（幂等）。</summary>
        public static void Unload()
        {
            if (string.IsNullOrEmpty(_lastLoadedCuixPath)) return;
            try
            {
                AcApp.UnloadPartialMenu(_lastLoadedCuixPath);
            }
            catch
            {
                // 静默：菜单可能已被用户手动卸载
            }
            finally
            {
                _lastLoadedCuixPath = null;
            }
        }

        /// <summary>
        /// 是否需要重建 CUIX：目标不存在 或 commands.json 更新时间更新。
        /// </summary>
        private static bool NeedsRebuild(string target)
        {
            if (!File.Exists(target)) return true;

            var jsonMtime = CommandTable.GetFileMtimeUtc();
            if (!jsonMtime.HasValue) return false;

            var cuixMtime = File.GetLastWriteTimeUtc(target);
            return jsonMtime.Value > cuixMtime;
        }

        /// <summary>
        /// 构建 <c>HyCAD.cuix</c>：
        /// - MenuGroup "HYCAD"
        /// - 顶层 PopMenu "HyCAD"
        /// - 每个 Category 一个子 PopMenu，通过 PopMenuRef 挂到顶层；子 PopMenu 内每个命令一个 PopMenuItem。
        /// </summary>
        private static void BuildCuix(string target)
        {
            if (File.Exists(target))
            {
                try { File.Delete(target); } catch { }
            }

            var groups = CommandTable.GroupByCategory();

            var cs = new CustomizationSection(target, MenuGroupName);
            var mg = cs.MenuGroup;
            var macroGroup = new MacroGroup("HyCAD", mg);

            var rootAliases = new StringCollection { "POP_HYCAD" };
            var root = new PopMenu("HyCAD", rootAliases, RootPopElementId, mg)
            {
                Description = "HyCAD 工具集"
            };

            int macroIndex = 0;
            int subIndex = 0;
            foreach (var g in groups)
            {
                string subElementId = "HYCAD_SUB_" + (subIndex++).ToString("D2");
                var subAliases = new StringCollection { "POP_" + subElementId };
                var sub = new PopMenu(g.Category, subAliases, subElementId, mg)
                {
                    Description = g.Category
                };

                new PopMenuRef(sub, root, -1);

                foreach (var cmd in g.Items)
                {
                    string elementId = "HYCAD_CMD_" + (macroIndex++).ToString("D4");
                    string macroText = "^C^C" + cmd.Key + " ";
                    var macro = new MenuMacro(macroGroup, cmd.DisplayName, macroText, elementId);

                    new PopMenuItem(macro, cmd.DisplayName, sub, -1);
                }
            }

            cs.Save();
        }

        private static void LoadPartialMenu(string cuixPath)
        {
            if (string.IsNullOrEmpty(cuixPath) || !File.Exists(cuixPath)) return;

            try { AcApp.UnloadPartialMenu(cuixPath); } catch { }

            AcApp.LoadPartialMenu(cuixPath);
            _lastLoadedCuixPath = cuixPath;
        }

        private static void EnsureDirectoryExists(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        }
    }
}
