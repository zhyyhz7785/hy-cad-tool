using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Features.Tables.Infrastructure.AutoCad;

namespace HyCADTool.Features.Tables.Commands
{
    /// <summary>
    /// AC4：插入黄金样表并写入 HyTable 真相源（N33 / C1）。
    /// </summary>
    public sealed class InsertSampleTableCommand
    {
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return;

            var ed = doc.Editor;
            var ppr = ed.GetPoint("\n指定表格左上角插入点: ");
            if (ppr.Status != PromptStatus.OK)
                return;

            InsertPersonnelTable(doc, ppr.Value);
        }

        /// <summary>C1 无交互路径：人员表 @ 原点。</summary>
        public void ExecutePersonnelAtOrigin()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return;

            InsertPersonnelTable(doc, Point3d.Origin);
        }

        /// <summary>C1 无交互路径：家庭成员表 @ 原点（AC6 验收）。</summary>
        public void ExecuteFamilyAtOrigin()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return;

            InsertFamilyTable(doc, Point3d.Origin);
        }

        /// <summary>Dev：斜线微夹具 @ 原点。</summary>
        public void ExecuteDiagonalDemoAtOrigin()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return;

            var grid = HyCAD.Tables.Samples.TableSamples.BuildDiagonalDemoTable();
            var renderer = new AcadTableRenderer(doc.Database);
            var handle = renderer.RenderAndAttach(grid, Point3d.Origin);

            doc.Editor.WriteMessage(
                $"\n[HyTable] 已插入斜线演示表；实体 {handle.EntityCount} 个；" +
                $"Group={handle.GroupId.Handle}; Carrier={handle.CarrierId.Handle}; TableId={handle.TableId:D}");
        }

        private static void InsertPersonnelTable(Document doc, Point3d insertionPoint)
        {
            var grid = HyCAD.Tables.Samples.TableSamples.BuildPersonnelTable();
            var renderer = new AcadTableRenderer(doc.Database);
            var handle = renderer.RenderAndAttach(grid, insertionPoint);

            doc.Editor.WriteMessage(
                $"\n[HyTable] 已插入人员基本情况表；实体 {handle.EntityCount} 个；" +
                $"Group={handle.GroupId.Handle}; Carrier={handle.CarrierId.Handle}; TableId={handle.TableId:D}");
        }

        private static void InsertFamilyTable(Document doc, Point3d insertionPoint)
        {
            var grid = HyCAD.Tables.Samples.TableSamples.BuildFamilyTable();
            var renderer = new AcadTableRenderer(doc.Database);
            var handle = renderer.RenderAndAttach(grid, insertionPoint);

            doc.Editor.WriteMessage(
                $"\n[HyTable] 已插入家庭成员表；实体 {handle.EntityCount} 个；" +
                $"Group={handle.GroupId.Handle}; Carrier={handle.CarrierId.Handle}; TableId={handle.TableId:D}");
        }
    }
}
