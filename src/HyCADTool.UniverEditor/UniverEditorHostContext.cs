using System;

namespace HyCADTool.UniverEditor
{
    /// <summary>
    /// HyCAD 主工程通过反射注入的宿主回调（net48 TableEditorViewModel 桥接）。
    /// </summary>
    public sealed class UniverEditorHostContext
    {
        public Func<string> GetLoadSnapshotJson { get; set; }

        public Action<int, int, string> OnCellChanged { get; set; }

        public Action NewEmptyTable { get; set; }

        public Action LoadPersonnelSample { get; set; }

        public Action RequestPick { get; set; }

        public Action RequestPublish { get; set; }

        public Action RequestPublishRangeFull { get; set; }

        public Action RequestPublishRangeContent { get; set; }

        public Action ExportJsonSnapshot { get; set; }

        public Action ImportXlsx { get; set; }

        public Action ExportXlsx { get; set; }

        public Func<string> GetSummaryText { get; set; }

        public Func<string> GetStatusMessage { get; set; }

        /// <summary>读取当前落图比例（口径 B）。</summary>
        public Func<double> GetScale { get; set; }

        /// <summary>网页布局 Tab 修改比例时回传（仅改 VM.Scale，不改 grid）。</summary>
        public Action<double> SetScale { get; set; }

        /// <summary>网页选区变化（startRow, startCol, endRow, endCol）。</summary>
        public Action<int, int, int, int> OnSelectionChanged { get; set; }

        /// <summary>
        /// 布局 Tab 行列/合并/尺寸操作；op 见 UniverSessionLifecycle.HandleHyCadLayout，value 视 op 而定。
        /// </summary>
        public Action<string, double> OnLayoutOp { get; set; }

        /// <summary>读取结构模式开关初值。</summary>
        public Func<bool> GetStructureMode { get; set; }

        /// <summary>读取纸张视口/行列/模板初值 JSON（供网页布局 Tab 回填）。</summary>
        public Func<string> GetViewportJson { get; set; }

        /// <summary>网页自动调整行/列高宽后回写 Domain（isRow, startIndex, sizesMm[]）。</summary>
        public Action<bool, int, double[]> SetTrackSizesMm { get; set; }

        public event Action GridChanged;

        public void NotifyGridChanged() => GridChanged?.Invoke();

        public Action<string, string> OnSnapshotExported { get; set; }

        /// <summary>由 Loader 注入，窗口 Session 就绪后注册 exportSnapshot 委托。</summary>
        public Action<Action> BindExportSnapshot { get; set; }

        /// <summary>由 Loader 注入，Session 就绪后注册 exportSnapshotForPublish 委托（mode: full/content）。</summary>
        public Action<Action<string>> BindExportForPublish { get; set; }

        /// <summary>Univer JS 导出失败时通知桥接取消 pending（避免 exportSnapshot 超时）。</summary>
        public Action<string> OnExportError { get; set; }

        public event Action StatusChanged;

        public void NotifyStatusChanged() => StatusChanged?.Invoke();

        public void InvokeSafe(Action action, Action<string> setStatus)
        {
            if (action == null)
            {
                return;
            }

            try
            {
                action();
            }
            catch (Exception ex)
            {
                setStatus?.Invoke(ex.Message);
            }
        }

        public string TryGetLoadSnapshotJson()
        {
            try
            {
                return GetLoadSnapshotJson?.Invoke();
            }
            catch
            {
                return null;
            }
        }
    }
}
