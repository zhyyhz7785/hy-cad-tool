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

        public Action ExportJsonSnapshot { get; set; }

        public Action ImportXlsx { get; set; }

        public Action ExportXlsx { get; set; }

        public Func<string> GetSummaryText { get; set; }

        public Func<string> GetStatusMessage { get; set; }

        public event Action GridChanged;

        public void NotifyGridChanged() => GridChanged?.Invoke();

        public Action<string> OnSnapshotExported { get; set; }

        public void InvokeSafe(Action action, Action<string> setStatus)
        {
            if (action == null)
                return;

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
