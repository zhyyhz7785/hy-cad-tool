using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Features.Elevation.Services;

namespace HyCADTool.Features.Elevation
{
    /// <summary>
    /// 剖面设计图生成命令（迁移自旧版 hy3C_CreateSection）。
    ///
    /// 工作流：选剖切线（LINE）→ 选 3D 实体（3DSOLID/SURFACE/MESH，通常为 HY3 生成的墙体/筏板）
    /// → 对每条剖切线生成 2D 剖面几何并旋转平移到图纸旁，自动加剖切符号与「N-N 剖面图」标签。
    /// </summary>
    public class CreateSectionCommand
    {
        private readonly double _displacement;
        private readonly double _scale;

        /// <summary>命令行入口：位移交互提示（默认 12000），比例取设置面板 Scale（无则 50）。</summary>
        public CreateSectionCommand()
            : this(displacement: double.NaN, scale: double.NaN)
        {
        }

        /// <summary>
        /// 面板入口：直接传剖面位移与剖切符号比例，跳过位移提示。
        /// </summary>
        /// <param name="displacement">剖面图排布间距（mm）；NaN 或 ≤0 时改为命令行提示。</param>
        /// <param name="scale">剖切符号 / 标签比例；NaN 或 ≤0 时取设置面板 Scale（无则 50）。</param>
        public CreateSectionCommand(double displacement, double scale)
        {
            _displacement = displacement;
            _scale = scale;
        }

        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var db = doc.Database;
            var ed = doc.Editor;

            try
            {
                double displacement = ResolveDisplacement(ed);
                if (double.IsNaN(displacement)) return;

                double scale = ResolveScale();

                // 1. 选剖切线（只取 ObjectId，实体在主事务内打开）
                var lineIds = SectionLine.SelectLineIds(ed);
                if (lineIds.Count == 0) return;

                // 2. 选 3D 实体
                ObjectIdCollection entityIds = SectionGenerationService.Select3DObjects(ed);
                if (entityIds.Count == 0) return;

                var sw = System.Diagnostics.Stopwatch.StartNew();
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    SectionGenerationService.CreateLayers(db, tr);

                    var sectionLines = SectionLine.BuildFrom(tr, lineIds, scale);
                    if (sectionLines.Count == 0)
                    {
                        tr.Commit();
                        return;
                    }
                    SectionLine.SortAndIndexLines(sectionLines);

                    var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                    SectionGenerationService.GenerateSection(db, tr, ed, sectionLines, displacement, entityIds, btr);

                    tr.Commit();
                }

                sw.Stop();
                ed.Regen();
                ed.WriteMessage($"\n剖面生成完成：{lineIds.Count} 条剖切线，位移 {displacement:F0}，比例 {scale:F0}，耗时 {sw.ElapsedMilliseconds} ms。");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误: {ex.Message}");
            }
        }

        /// <summary>位移：构造参数有效则直接用，否则命令行提示（默认 12000）。</summary>
        private double ResolveDisplacement(Editor ed)
        {
            if (!double.IsNaN(_displacement) && _displacement > 0)
                return _displacement;

            var pdo = new PromptDoubleOptions("\n请输入剖面位移距离 (默认 12000): ")
            {
                DefaultValue = 12000,
                AllowNegative = false,
                AllowNone = true,
            };
            var pdr = ed.GetDouble(pdo);
            if (pdr.Status == PromptStatus.OK) return pdr.Value;
            if (pdr.Status == PromptStatus.None) return 12000;
            return double.NaN;
        }

        /// <summary>比例：构造参数有效则直接用，否则取设置面板 Scale，最后回落 50。</summary>
        private double ResolveScale()
        {
            if (!double.IsNaN(_scale) && _scale > 0)
                return _scale;

            var settingsVm = HyCADTool.Shell.ViewModels.SettingsPanelViewModel.Current;
            if (settingsVm != null && settingsVm.Scale > 0)
                return settingsVm.Scale;

            return 50;
        }
    }
}
