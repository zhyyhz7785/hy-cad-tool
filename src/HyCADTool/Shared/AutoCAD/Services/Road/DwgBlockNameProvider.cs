using System;
using System.Collections.ObjectModel;
using System.Linq;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Autodesk.AutoCAD.DatabaseServices;

namespace HyCADTool.Shared.AutoCAD.Services.Road
{
    /// <summary>
    /// 从当前活动 DWG 的 <see cref="BlockTable"/> 收集可用图块名，供 rCs 结构层「插入图块」下拉。
    /// </summary>
    public static class DwgBlockNameProvider
    {
        private static readonly ObservableCollection<string> _names = new ObservableCollection<string>();
        private static readonly ReadOnlyObservableCollection<string> _namesView = new ReadOnlyObservableCollection<string>(_names);

        public static ReadOnlyObservableCollection<string> BlockNames => _namesView;

        public static void Refresh()
        {
            _names.Clear();
            try
            {
                var doc = AcApp.DocumentManager?.MdiActiveDocument;
                if (doc == null) return;
                var db = doc.Database;
                using (var tr = doc.TransactionManager.StartOpenCloseTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    foreach (ObjectId id in bt)
                    {
                        if (!id.IsValid) continue;
                        var btr = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
                        if (btr == null) continue;
                        if (btr.IsLayout || btr.IsFromExternalReference) continue;
                        var name = btr.Name ?? string.Empty;
                        if (name.Length == 0) continue;
                        if (name.StartsWith("*", StringComparison.Ordinal) || name.IndexOf("Paper_Space", StringComparison.OrdinalIgnoreCase) >= 0
                            || name.IndexOf("Model_Space", StringComparison.OrdinalIgnoreCase) >= 0)
                            continue;
                        _names.Add(name);
                    }

                    tr.Commit();
                }
            }
            catch
            {
                // 无图档或非 CAD 环境：保持空或上次结果
            }

            if (_names.Count == 0) return;
            var ordered = _names.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
            _names.Clear();
            foreach (var x in ordered)
                _names.Add(x);
        }
    }
}
