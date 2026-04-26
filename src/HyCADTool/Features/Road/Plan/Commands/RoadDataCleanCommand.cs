using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.App.Bootstrap;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

using HyCADTool.Features.Road.PlanAlignment.Services;
using HyCADTool.Features.Road.CrossSection.Domain;
using HyCADTool.Features.Road.PlanAlignment.Commands;
using HyCADTool.Features.Road.CrossSection.Commands;
namespace HyCADTool.Features.Road
{
    /// <summary>
    /// <c>hyRoadDataClean</c>（M8.3）：数据整理命令（取整 / 位数 / 放大缩小 / 去小数点）。
    ///
    /// <para>MVP：命令行驱动（无 WPF 窗口，省去 XAML 编码踩坑）。作用对象：
    /// <list type="bullet">
    ///   <item><c>Alignment</c>：StartStation + Centerline 全部顶点 X/Y/Z + Elements 里 Length/Radius。</item>
    ///   <item><c>Template</c>：所有 Points 的 HorizontalOffset/VerticalOffset。</item>
    ///   <item><c>StructureLayer</c>：所有层的 ThicknessCm/LeftWidenCm/RightWidenCm/LeftSlope/RightSlope。</item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class RoadDataCleanCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
            var exporter = ServiceLocator.Resolve<RoadJsonExportService>();

            if (!registry.TryGet(doc.Name, out var design) || design == null)
            {
                ed.WriteMessage("\n[道路] 当前文档无道路数据。");
                return;
            }

            var scope = PromptScope(ed);
            if (scope == null) return;

            var (mode, parameter) = PromptMode(ed);
            if (!mode.HasValue) return;

            int touched = ApplyScope(design, scope.Value, mode.Value, parameter);
            ed.WriteMessage($"\n[道路] 整理完成：{touched} 个字段被更新。");

            design.LastModifiedUtc = DateTime.UtcNow;
            string saved = exporter.SaveForDocument(design, doc.Name);
            if (saved != null)
                ed.WriteMessage($"\n[道路] JSON 已落盘：{saved}");
        }

        private enum Scope
        {
            Alignment = 0,
            Template = 1,
            StructureLayer = 2,
        }

        private static Scope? PromptScope(Editor ed)
        {
            var opts = new PromptKeywordOptions("\n[道路] 作用范围 [Alignment(A)/Template(T)/StructureLayer(S)]: ")
            {
                AllowNone = true,
            };
            opts.Keywords.Add("Alignment");
            opts.Keywords.Add("Template");
            opts.Keywords.Add("StructureLayer");
            opts.Keywords.Default = "Alignment";
            var r = ed.GetKeywords(opts);
            if (r.Status != PromptStatus.OK) return null;
            switch (r.StringResult)
            {
                case "Template": return Scope.Template;
                case "StructureLayer": return Scope.StructureLayer;
                default: return Scope.Alignment;
            }
        }

        private static (DataNormalizationService.Mode? mode, double parameter) PromptMode(Editor ed)
        {
            var opts = new PromptKeywordOptions(
                "\n[道路] 整理模式 [取整(I)/位数(D)/放大(S)/缩小(V)/去小数点(P)]: ")
            {
                AllowNone = true,
            };
            opts.Keywords.Add("Int");
            opts.Keywords.Add("Digits");
            opts.Keywords.Add("Scale");
            opts.Keywords.Add("Vide"); // divide
            opts.Keywords.Add("DropPoint");
            opts.Keywords.Default = "Int";
            var r = ed.GetKeywords(opts);
            if (r.Status != PromptStatus.OK) return (null, 0);

            switch (r.StringResult)
            {
                case "Int":
                    return (DataNormalizationService.Mode.RoundToInt, 0);
                case "Digits":
                    {
                        var np = new PromptIntegerOptions("\n[道路] 保留多少位小数？<2>: ")
                        {
                            AllowNone = true,
                            DefaultValue = 2,
                            UseDefaultValue = true,
                            LowerLimit = 0,
                            UpperLimit = 15,
                        };
                        var nr = ed.GetInteger(np);
                        if (nr.Status != PromptStatus.OK) return (null, 0);
                        return (DataNormalizationService.Mode.RoundToDecimals, nr.Value);
                    }
                case "Scale":
                    {
                        var np = new PromptDoubleOptions("\n[道路] 放大倍数 <10>: ")
                        {
                            AllowNone = true,
                            DefaultValue = 10,
                            UseDefaultValue = true,
                            AllowNegative = false,
                            AllowZero = false,
                        };
                        var nr = ed.GetDouble(np);
                        if (nr.Status != PromptStatus.OK) return (null, 0);
                        return (DataNormalizationService.Mode.Scale, nr.Value);
                    }
                case "Vide":
                    {
                        var np = new PromptDoubleOptions("\n[道路] 缩小倍数 <10>: ")
                        {
                            AllowNone = true,
                            DefaultValue = 10,
                            UseDefaultValue = true,
                            AllowNegative = false,
                            AllowZero = false,
                        };
                        var nr = ed.GetDouble(np);
                        if (nr.Status != PromptStatus.OK) return (null, 0);
                        return (DataNormalizationService.Mode.Divide, nr.Value);
                    }
                case "DropPoint":
                    return (DataNormalizationService.Mode.DropDecimal, 0);
                default:
                    return (null, 0);
            }
        }

        private static int ApplyScope(RoadDesign design, Scope scope, DataNormalizationService.Mode mode, double param)
        {
            int count = 0;
            switch (scope)
            {
                case Scope.Alignment:
                    foreach (var a in design.Alignments)
                    {
                        a.StartStation = DataNormalizationService.Normalize(a.StartStation, mode, param);
                        count++;
                        foreach (var e in a.Elements)
                        {
                            e.Length = DataNormalizationService.Normalize(e.Length, mode, param);
                            e.Radius = DataNormalizationService.Normalize(e.Radius, mode, param);
                            e.SpiralParameterA = DataNormalizationService.Normalize(e.SpiralParameterA, mode, param);
                            count += 3;
                        }
                    }
                    break;
                case Scope.Template:
                    foreach (var t in design.Templates)
                    {
                        foreach (var p in t.Points)
                        {
                            p.HorizontalOffset = DataNormalizationService.Normalize(p.HorizontalOffset, mode, param);
                            p.VerticalOffset = DataNormalizationService.Normalize(p.VerticalOffset, mode, param);
                            count += 2;
                        }
                    }
                    break;
                case Scope.StructureLayer:
                    foreach (var s in design.StructureLayerSchemes)
                    {
                        foreach (var l in s.Layers)
                        {
                            l.ThicknessCm = DataNormalizationService.Normalize(l.ThicknessCm, mode, param);
                            l.LeftWidenCm = DataNormalizationService.Normalize(l.LeftWidenCm, mode, param);
                            l.RightWidenCm = DataNormalizationService.Normalize(l.RightWidenCm, mode, param);
                            l.LeftSlope = DataNormalizationService.Normalize(l.LeftSlope, mode, param);
                            l.RightSlope = DataNormalizationService.Normalize(l.RightSlope, mode, param);
                            count += 5;
                        }
                    }
                    break;
            }
            return count;
        }
    }
}
