using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Drawing;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road;
using HyCADTool.Refactored.Infrastructure.Configuration;
using HyCADTool.Refactored.Presentation.ViewModels;
using HyCADTool.Refactored.Presentation.ViewModels.Road;
using HyCADTool.Refactored.Presentation.Views.Road;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands.Road
{
    /// <summary>
    /// M3 v2：横断面绘制命令（<c>hyRoadCs</c>）。
    ///
    /// <para>
    /// 取代 <see cref="RoadTemplateCommand"/>（hyRoadT）。本命令继续承担同样的三入口职责，
    /// 但 UI 走 BlenderUI workbench 风格的 <see cref="CrossSectionDrawWindow"/>，
    /// 并以 <see cref="CrossSectionDrawViewModel"/> 暴露 v2 新增的路牙 / 坡型 / 路拱 / 路面结构 / 桩号字段。
    /// </para>
    ///
    /// 主入口 <c>hyRoadCs</c>：一点击即打开横断面 WPF（默认主干路预设），不再先询问 N/L/C。
    /// 其余入口：
    /// <list type="bullet">
    ///   <item><c>hyRoadCsLoad</c>（<see cref="ExecuteLoadFromDwg"/>）：从当前 DWG 已保存模板加载 → 绘制窗口。</item>
    ///   <item><c>hyRoadCsQuick</c>（<see cref="ExecuteQuickPreset"/>）：纯命令行预设直出（不开 WPF），便于脚本 / 回归。</item>
    /// </list>
    ///
    /// 交付链路：
    /// <c>CrossSectionLayout → Template → JSON（Registry + Export） → CrossSectionFigure → ModelSpace 绘制</c>。
    ///
    /// <para>
    /// 与 v1 (<see cref="RoadTemplateCommand"/>) 的差异：
    /// <list type="bullet">
    ///   <item>UI：BlenderUI workbench (Outliner / Toolbar / Canvas / PropertyEditor / StatusBar) → 旧 UI 是 3 列 DataGrid；</item>
    ///   <item>VM：<see cref="CrossSectionDrawViewModel"/>（继承 <see cref="CrossSectionDesignerViewModel"/>），多了侧栏显隐 / 复制粘贴 / 状态栏；</item>
    ///   <item>所有 AutoCAD 交互 / 持久化 / 出图链路保持一致，便于 v1↔v2 平滑切换。</item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class RoadCrossSectionDrawCommand
    {
        /// <summary>菜单 / 面板 / 键盘入口：直接打开横断面 WPF（新建 + 默认预设）。</summary>
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            ExecuteNew(doc);
        }

        /// <summary>从当前 DWG 已保存的横断面模板加载并打开 WPF（原「L」分支）。</summary>
        public void ExecuteLoadFromDwg()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            ExecuteLoad(doc);
        }

        /// <summary>命令行预设直出，不开 WPF（原「C」分支）。</summary>
        public void ExecuteQuickPreset()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            ExecuteCommandLine(doc);
        }

        // =========================================================================
        //  新建
        // =========================================================================

        private static void ExecuteNew(Document doc)
        {
            var vm = new CrossSectionDrawViewModel(
                initialLayout: CrossSectionPresets.CreateCjj37UrbanArterial());

            if (!TryRunDesigner(doc, vm, out var result)) return;

            var origin = PromptInsertionPoint(doc.Editor);
            if (origin == null) return;

            var mode = vm.UseSingleLineMode
                ? CrossSectionDrawMode.SingleLine
                : CrossSectionDrawMode.WithStructureThickness;
            DrawAndSave(doc, result, origin.Value, drawMode: mode);
        }

        // =========================================================================
        //  加载已有
        // =========================================================================

        private static void ExecuteLoad(Document doc)
        {
            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
            if (!registry.TryGet(doc.Name, out var design) || design.Templates.Count == 0)
            {
                doc.Editor.WriteMessage("\n[道路] 当前 DWG 还没有保存过横断面模板。请先使用【新建】分支。");
                return;
            }

            var pick = PromptTemplate(doc.Editor, design.Templates);
            if (pick == null) return;

            CrossSectionLayout existing;
            try
            {
                existing = CrossSectionLayoutBuilder.FromTemplate(pick);
            }
            catch (Exception ex)
            {
                doc.Editor.WriteMessage("\n[道路] 模板解析失败：" + ex.Message + "\n[道路] 已改为加载默认预设。");
                existing = CrossSectionPresets.CreateCjj37UrbanArterial();
            }

            var vm = new CrossSectionDrawViewModel(existing, existingTemplateId: pick.Id);
            if (!TryRunDesigner(doc, vm, out var result)) return;

            var origin = PromptInsertionPoint(doc.Editor);
            if (origin == null) return;

            var mode = vm.UseSingleLineMode
                ? CrossSectionDrawMode.SingleLine
                : CrossSectionDrawMode.WithStructureThickness;
            DrawAndSave(doc, result, origin.Value, replaceTemplateId: pick.Id, drawMode: mode);
        }

        // =========================================================================
        //  命令行快速分支（预设直出）
        // =========================================================================

        private static void ExecuteCommandLine(Document doc)
        {
            var preset = PromptPreset(doc.Editor);
            if (preset == null) return;

            var layout = preset.Create();

            var origin = PromptInsertionPoint(doc.Editor);
            if (origin == null) return;

            var result = new CrossSectionDesignerResult(
                template: CrossSectionLayoutBuilder.ToTemplate(layout, null, layout.Title),
                figure: CrossSectionLayoutBuilder.ToFigure(layout),
                layout: layout);

            DrawAndSave(doc, result, origin.Value);
        }

        // =========================================================================
        //  交互与持久化
        // =========================================================================

        private static bool TryRunDesigner(Document doc, CrossSectionDrawViewModel vm, out CrossSectionDesignerResult result)
        {
            result = null;
            CrossSectionDesignerResult captured = null;
            vm.Confirmed += (_, r) => captured = r;

            var window = new CrossSectionDrawWindow(vm);
            // 把 AutoCAD 主窗口设为 owner，避免"浮窗被主窗口压到最下面"
            var ownerHandle = AcApp.MainWindow?.Handle ?? IntPtr.Zero;
            if (ownerHandle != IntPtr.Zero)
            {
                new System.Windows.Interop.WindowInteropHelper(window).Owner = ownerHandle;
            }

            bool? dlg;
            try
            {
                dlg = AcApp.ShowModalWindow(window);
            }
            catch (InvalidOperationException)
            {
                // 极端场景（非 CAD 宿主 / 单元测试）兜底到原生 ShowDialog
                dlg = window.ShowDialog();
            }

            if (captured == null || dlg != true)
            {
                doc.Editor.WriteMessage("\n[道路] 已取消。");
                return false;
            }
            result = captured;
            return true;
        }

        private static Point2d? PromptInsertionPoint(Editor ed)
        {
            var opt = new PromptPointOptions("\n[道路] 指定横断面图插入点：")
            {
                AllowNone = false,
            };
            var res = ed.GetPoint(opt);
            if (res.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                return null;
            }
            return new Point2d(res.Value.X, res.Value.Y);
        }

        private static Template PromptTemplate(Editor ed, IReadOnlyList<Template> templates)
        {
            ed.WriteMessage("\n[道路] 当前 DWG 的横断面模板：");
            for (int i = 0; i < templates.Count; i++)
            {
                var t = templates[i];
                ed.WriteMessage(string.Format("\n  {0,2}: {1}（Id={2:N}）", i + 1, t.Name ?? "未命名", t.Id));
            }
            var opt = new PromptIntegerOptions("\n[道路] 输入序号（回车取消）：")
            {
                AllowNone = true,
                AllowNegative = false,
                AllowZero = false,
                LowerLimit = 1,
                UpperLimit = templates.Count,
            };
            var res = ed.GetInteger(opt);
            if (res.Status == PromptStatus.None || res.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                return null;
            }
            return templates[res.Value - 1];
        }

        private static PresetDescriptor PromptPreset(Editor ed)
        {
            var all = CrossSectionPresets.All;
            ed.WriteMessage("\n[道路] 预设：");
            for (int i = 0; i < all.Count; i++)
            {
                ed.WriteMessage(string.Format("\n  {0}: {1}（Key={2}）", i + 1, all[i].DisplayName, all[i].Key));
            }
            var opt = new PromptIntegerOptions("\n[道路] 输入序号 <1>：")
            {
                AllowNone = true,
                AllowNegative = false,
                AllowZero = false,
                LowerLimit = 1,
                UpperLimit = all.Count,
                DefaultValue = 1,
                UseDefaultValue = true,
            };
            var res = ed.GetInteger(opt);
            if (res.Status == PromptStatus.None) return all[0];
            if (res.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                return null;
            }
            return all[res.Value - 1];
        }

        private static void DrawAndSave(
            Document doc,
            CrossSectionDesignerResult result,
            Point2d origin,
            Guid? replaceTemplateId = null,
            CrossSectionDrawMode drawMode = CrossSectionDrawMode.WithStructureThickness)
        {
            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
            var exporter = ServiceLocator.Resolve<RoadJsonExportService>();
            var drawService = ServiceLocator.Resolve<RoadStandardSectionDrawService>();
            DrawingSheetTitleSpec titleSpec = SettingsPanelViewModel.Current != null
                ? SettingsPanelViewModel.Current.CreateDrawingSheetTitleSpec()
                : DrawingSheetTitleSpec.RoadCrossSectionDefault;

            var design = registry.GetOrCreate(doc.Name);
            var template = result.Template;

            // 1) 替换或追加到 Domain
            if (replaceTemplateId.HasValue)
            {
                int idx = design.Templates.FindIndex(t => t.Id == replaceTemplateId.Value);
                if (idx >= 0) design.Templates[idx] = template;
                else design.Templates.Add(template);
            }
            else
            {
                // 若 Id 已存在（理论上不应），覆盖；否则追加
                var existing = design.Templates.FirstOrDefault(t => t.Id == template.Id);
                if (existing != null)
                {
                    design.Templates.Remove(existing);
                }
                design.Templates.Add(template);
            }
            design.LastModifiedUtc = DateTime.UtcNow;

            // 2) 清旧 + 出图
            int erased = 0;
            int created = 0;
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                erased = drawService.Clear(tr, doc.Database, template.Id);
                double planOff = SettingsPanelViewModel.Current?.RoadCrossSectionPlanStripVerticalOffsetM ?? 5.0;
                created = drawService.Draw(tr, doc.Database, result.Figure, template, origin,
                    modelUnitPerMeter: 1.0, mode: drawMode, layout: result.Layout, annotationStyle: null,
                    sheetTitleSpec: titleSpec, planStripVerticalOffsetMeters: planOff);
                tr.Commit();
            }

            // 3) JSON 落盘
            string savedTo = exporter.SaveForDocument(design, doc.Name);

            doc.Editor.WriteMessage(
                $"\n[道路] 已 {(replaceTemplateId.HasValue ? "更新" : "新增")} 模板 {template.Name}（Id={template.Id:N}）。");
            doc.Editor.WriteMessage(
                $"\n[道路] 路幅 {result.Layout.TotalWidth:F2} m，"
                + $"左半 {result.Layout.LeftHalfWidth:F2} m / 右半 {result.Layout.RightHalfWidth:F2} m，"
                + $"设计速度 V={result.Layout.DesignSpeed} km/h，比例 1:{result.Layout.ScaleDenominator}。");
            doc.Editor.WriteMessage($"\n[道路] 实体变更：擦除 {erased} 个 / 生成 {created} 个。");

            if (!string.IsNullOrEmpty(savedTo))
                doc.Editor.WriteMessage($"\n[道路] JSON 已同步落盘：{savedTo}");
            else
                doc.Editor.WriteMessage(
                    "\n[道路] 未落盘（DWG 尚未保存）。先 QSAVE / SAVEAS，再跑 hyRoadSave 即可。");
        }
    }
}
