using System;
using System.Collections.Generic;
using System.Windows.Threading;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Shell.ViewModels;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.TextEdit.Services
{
    /// <summary>
    /// 双击时否决 AutoCAD 原生 TEXTEDIT/MTEDIT 等，改由 hyed 走极简 WPF 编辑。
    /// 机制：<see cref="AcApp.BeginDoubleClick"/> + <see cref="DocumentCollection.DocumentLockModeChanged"/> +
    /// <see cref="DocumentCollection.DocumentLockModeChangeVetoed"/>（见 Autodesk 社区范式）。
    /// <para>
    /// 派发：在 <see cref="OnDocumentLockModeChangeVetoed"/> 中通过
    /// <see cref="Dispatcher.BeginInvoke(Delegate, DispatcherPriority, object[])"/> 直接调
    /// <see cref="HyEdLauncher.LaunchEditor"/>，**不**走 <c>SendStringToExecute</c>。
    /// 原因：SendStringToExecute 把命令排队等下次 idle（=下次用户输入）才被消化，
    /// 用户双击后若不动鼠标，编辑框会卡着不出来。
    /// </para>
    /// </summary>
    public static class HyEdDoubleClickInterceptor
    {
        private static bool _installed;
        private static ObjectId _pendingId = ObjectId.Null;
        private static bool _runHyEdAfterVeto;
        private static bool _beginDoubleClickArmed;

        private static readonly HashSet<string> NativeTextEditCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "TEXTEDIT",
            "MTEDIT",
            "DDEDIT",
            "MTEXTEDIT",
            "AI_MLEADER_TEXT_EDIT",
            "MLEADERTEXTEDIT",
        };

        public static void Install()
        {
            if (_installed)
                return;

            AcApp.BeginDoubleClick += OnBeginDoubleClick;
            AcApp.DocumentManager.DocumentLockModeChanged += OnDocumentLockModeChanged;
            AcApp.DocumentManager.DocumentLockModeChangeVetoed += OnDocumentLockModeChangeVetoed;
            _installed = true;
        }

        public static void Uninstall()
        {
            if (!_installed)
                return;

            AcApp.BeginDoubleClick -= OnBeginDoubleClick;
            AcApp.DocumentManager.DocumentLockModeChanged -= OnDocumentLockModeChanged;
            AcApp.DocumentManager.DocumentLockModeChangeVetoed -= OnDocumentLockModeChangeVetoed;
            _installed = false;
            _pendingId = ObjectId.Null;
            _runHyEdAfterVeto = false;
            _beginDoubleClickArmed = false;
        }

        private static void OnBeginDoubleClick(object sender, BeginDoubleClickEventArgs e)
        {
            _pendingId = ObjectId.Null;
            _runHyEdAfterVeto = false;
            _beginDoubleClickArmed = false;

            if (!IsEnabledBySettings())
                return;

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return;

            var ed = doc.Editor;
            PromptSelectionResult res = ed.SelectImplied();
            if (res.Status != PromptStatus.OK || res.Value.Count != 1)
                return;

            ObjectId id = res.Value.GetObjectIds()[0];
            if (!IsSupportedTextEntity(doc.Database, id))
                return;

            _pendingId = id;
            _beginDoubleClickArmed = true;

            int dbl = 1;
            try
            {
                var v = AcApp.GetSystemVariable("DBLCLKEDIT");
                if (v != null)
                    dbl = Convert.ToInt32(v);
            }
            catch
            {
                dbl = 1;
            }

            // DBLCLKEDIT=0 时 CUI 双击不会进入 LockMode 链，需在此直接派发 hyed。
            if (dbl == 0)
                LaunchHyEdInternal(doc, id);
        }

        private static void OnDocumentLockModeChanged(object sender, DocumentLockModeChangedEventArgs e)
        {
            _runHyEdAfterVeto = false;

            if (!_beginDoubleClickArmed || _pendingId.IsNull)
                return;

            if (string.IsNullOrEmpty(e.GlobalCommandName))
                return;

            string cmd = e.GlobalCommandName.Trim();
            if (string.Equals(cmd, "_HYED_INTERNAL", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cmd, "HYED_INTERNAL", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cmd, "hyed", StringComparison.OrdinalIgnoreCase))
                return;

            if (!NativeTextEditCommands.Contains(cmd))
                return;

            _runHyEdAfterVeto = true;
            e.Veto();
        }

        private static void OnDocumentLockModeChangeVetoed(object sender, DocumentLockModeChangeVetoedEventArgs e)
        {
            if (!_runHyEdAfterVeto || _pendingId.IsNull)
                return;

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return;

            ObjectId id = _pendingId;
            _pendingId = ObjectId.Null;
            _beginDoubleClickArmed = false;
            _runHyEdAfterVeto = false;

            LaunchHyEdInternal(doc, id);
        }

        private static void LaunchHyEdInternal(Document doc, ObjectId id)
        {
            _pendingId = ObjectId.Null;
            _beginDoubleClickArmed = false;
            _runHyEdAfterVeto = false;

            // 通过 WPF Dispatcher 派发到下一帧 —— 让 AutoCAD 完成 Veto 收尾后立刻在 UI 线程上调起编辑器，
            // 不需要等 idle。DispatcherPriority.Background 排到 Render 之后，避开 MFC 消息循环重入。
            try
            {
                Dispatcher.CurrentDispatcher.BeginInvoke(
                    new Action(() => HyEdLauncher.LaunchEditor(doc, id)),
                    DispatcherPriority.Background);
            }
            catch
            {
                /* Dispatcher 异常时退回 SendStringToExecute 作为最后保险（命令行入口仍可用） */
                try
                {
                    doc.Editor.SetImpliedSelection(new[] { id });
                    doc.SendStringToExecute("_HYED_INTERNAL ", activate: true, wrapUpInactiveDoc: false, echoCommand: false);
                }
                catch { /* 真极端：静默 */ }
            }
        }

        /// <summary>
        /// 读 <see cref="SettingsPanelViewModel.EnableHyEdDoubleClick"/>；ViewModel 未创建（首次启动尚未打开首选项面板）
        /// 时默认开启（与设置初值一致），失败时也默认开启（不影响原有可达性）。
        /// </summary>
        private static bool IsEnabledBySettings()
        {
            try
            {
                var vm = SettingsPanelViewModel.Current;
                return vm == null || vm.EnableHyEdDoubleClick;
            }
            catch
            {
                return true;
            }
        }

        private static bool IsSupportedTextEntity(Database db, ObjectId id)
        {
            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead, false) is Entity ent))
                    {
                        tr.Commit();
                        return false;
                    }

                    bool ok = HyEdEntityProbe.TryGetKind(ent, out _);
                    tr.Commit();
                    return ok;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
