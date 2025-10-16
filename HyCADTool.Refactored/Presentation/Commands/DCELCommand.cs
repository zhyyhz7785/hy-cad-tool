using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Refactored.Domain.Services.GeometryAlgorithms;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Interfaces;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;

[assembly: CommandClass(typeof(HyCADTool.Refactored.Presentation.Commands.DCELCommand))]

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// DCEL 命令（Doubly Connected Edge List Command）
    /// 从选定的曲线构建 DCEL 图并绘制结果
    /// 命令名：HYDCEL（统一大写，保持命名一致性）
    /// </summary>
    public class DCELCommand
    {
        private readonly ICurveSegmentExtractor _curveExtractor;
        private readonly IDCELBuilderService _dcelBuilder;
        private readonly IDCELRenderer _dcelRenderer;

        /// <summary>
        /// 构造函数（用于依赖注入）
        /// </summary>
        public DCELCommand(
            ICurveSegmentExtractor curveExtractor,
            IDCELBuilderService dcelBuilder,
            IDCELRenderer dcelRenderer)
        {
            _curveExtractor = curveExtractor ?? throw new ArgumentNullException(nameof(curveExtractor));
            _dcelBuilder = dcelBuilder ?? throw new ArgumentNullException(nameof(dcelBuilder));
            _dcelRenderer = dcelRenderer ?? throw new ArgumentNullException(nameof(dcelRenderer));
        }

        /// <summary>
        /// 默认构造函数（用于AutoCAD命令注册）
        /// </summary>
        public DCELCommand()
        {
            // 创建服务实例（简单工厂模式）
            _curveExtractor = new CurveSegmentExtractor();
            _dcelBuilder = new DCELBuilderService();
            _dcelRenderer = new DCELRenderer();
        }

        /// <summary>
        /// AutoCAD 命令入口
        /// 命令名：HYDCEL（统一大写，保持命名一致性）
        /// </summary>
        [CommandMethod("HYDCEL")]
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            var ed = doc.Editor;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // 1. 提示用户选择曲线
                var selectionOptions = new PromptSelectionOptions
                {
                    MessageForAdding = "\n请选择用于生成 DCEL 的曲线："
                };

                var filter = new SelectionFilter(new[]
                {
                    new TypedValue((int)DxfCode.Start, "LINE,ARC,LWPOLYLINE,POLYLINE,SPLINE")
                });

                var selectionResult = ed.GetSelection(selectionOptions, filter);

                if (selectionResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n未选择任何曲线。");
                    return;
                }

                var selectionSet = selectionResult.Value;
                ed.WriteMessage($"\n选择了 {selectionSet.Count} 个对象");

                // 2. 提取曲线 ID
                var curveIds = new List<ObjectId>();
                foreach (SelectedObject selObj in selectionSet)
                {
                    if (selObj != null)
                    {
                        curveIds.Add(selObj.ObjectId);
                    }
                }

                // 3. 从曲线提取线段
                var tolerance = 0.01; // 容差值，与原代码保持一致
                var segments = _curveExtractor.ExtractSegments(curveIds, tolerance);

                if (segments.Count == 0)
                {
                    ed.WriteMessage("\n没有提取到有效的线段。");
                    return;
                }

                ed.WriteMessage($"\n提取了 {segments.Count} 条线段");

                // 4. 构建 DCEL 图
                var graph = _dcelBuilder.BuildFromSegments(segments, new Tolerance(tolerance));

                if (graph == null || graph.Faces.Count == 0)
                {
                    ed.WriteMessage("\n未能构建 DCEL 图或未生成任何面。");
                    return;
                }

                // 5. 输出统计信息
                var stats = graph.GetStatistics();
                ed.WriteMessage($"\nDCEL 构建完成：{stats.VertexCount} 顶点, {stats.EdgeCount} 边, {stats.FaceCount} 面");
                
                // 6. 输出面的分类信息
                var outerFaces = new List<Domain.DataStructures.DCEL.Face>();
                var innerFaces = new List<Domain.DataStructures.DCEL.Face>();
                foreach (var face in graph.Faces)
                {
                    if (face.IsOuter)
                        outerFaces.Add(face);
                    else
                        innerFaces.Add(face);
                }
                ed.WriteMessage($"\n外轮廓面: {outerFaces.Count}, 内部面: {innerFaces.Count}");

                // 7. 验证拓扑一致性
                if (!graph.Validate(out var errors))
                {
                    ed.WriteMessage("\n警告：DCEL 拓扑验证失败：");
                    foreach (var error in errors)
                    {
                        ed.WriteMessage($"\n  - {error}");
                    }
                }

                // 8. 渲染到 AutoCAD
                _dcelRenderer.Render(graph, "dcelOuter", "dcelInner");

                stopwatch.Stop();
                ed.WriteMessage($"\nINFO: DCEL处理 耗时 {stopwatch.ElapsedMilliseconds} 毫秒");
            }
            catch (System.Exception ex)
            {
                stopwatch.Stop();
                ed.WriteMessage($"\n错误：{ex.Message}");
                ed.WriteMessage($"\n{ex.StackTrace}");
            }
        }
    }
}


