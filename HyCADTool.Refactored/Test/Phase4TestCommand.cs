using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Refactored.Application.DTOs.OverKill;
using HyCADTool.Refactored.Application.UseCases.OverKill;
using HyCADTool.Refactored.Domain.Services;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Infrastructure.Configuration;
using System;
using System.Collections.Generic;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Exception = System.Exception;

namespace HyCADTool.Refactored.Test
{
    /// <summary>
    /// 阶段 4 测试命令
    /// 测试 OverKill 功能的各个组件
    /// </summary>
    public static class Phase4TestCommand
    {
        public static void RunAllPhase4Tests()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            
            ed.WriteMessage("\n========================================");
            ed.WriteMessage("\nHyCADTool.Refactored 阶段 4 综合测试");
            ed.WriteMessage("\n========================================");
            
            try
            {
                TestLineOverKillService();
                TestOverKillUseCase();
                
                ed.WriteMessage("\n");
                ed.WriteMessage("\n========================================");
                ed.WriteMessage("\n阶段 4 综合测试结束");
                ed.WriteMessage("\n========================================");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n\n❌ 测试执行失败: {ex.Message}\n");
            }
        }
        
        private static void TestLineOverKillService()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            
            ed.WriteMessage("\n\n=== 测试 1: LineOverKillService ===");
            
            try
            {
                var service = new LineOverKillService();
                
                // 测试 1.1: 合并重叠线段
                var line1 = new Line2D(new Point2D(0, 0), new Point2D(10, 0));
                var line2 = new Line2D(new Point2D(5, 0), new Point2D(15, 0));
                
                var merged = service.MergeOverlappingLines(
                    new[] { line1, line2 }, 
                    1e-6);
                
                ed.WriteMessage($"\n原始线段数: 2");
                ed.WriteMessage($"\n合并后线段数: {merged.Count}");
                ed.WriteMessage($"\n预期: 1");
                
                if (merged.Count == 1)
                {
                    ed.WriteMessage("\n✅ 重叠合并测试通过");
                }
                else
                {
                    ed.WriteMessage("\n❌ 重叠合并测试失败");
                }
                
                // 测试 1.2: 查找独立端点
                var lines = new List<Line2D>
                {
                    new Line2D(new Point2D(0, 0), new Point2D(10, 0)),
                    new Line2D(new Point2D(10, 0), new Point2D(10, 10)),
                    new Line2D(new Point2D(20, 0), new Point2D(30, 0))
                };
                
                var endpoints = service.FindIndependentEndpoints(lines, 1e-6);
                
                ed.WriteMessage($"\n\n独立端点数: {endpoints.Count}");
                ed.WriteMessage($"\n预期: 4 (line1起点, line2终点, line3两端)");
                
                if (endpoints.Count == 4)
                {
                    ed.WriteMessage("\n✅ 独立端点查找测试通过");
                }
                else
                {
                    ed.WriteMessage($"\n❌ 独立端点查找测试失败: 期望 4，实际 {endpoints.Count}");
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 测试失败: {ex.Message}");
            }
        }
        
        private static void TestOverKillUseCase()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            
            ed.WriteMessage("\n\n=== 测试 2: OverKillUseCase ===");
            
            try
            {
                var service = ServiceLocator.Resolve<LineOverKillService>();
                var useCase = new OverKillUseCase(service);
                
                // 创建测试数据：两条重叠线段
                var lines = new List<Line2D>
                {
                    new Line2D(new Point2D(0, 0), new Point2D(10, 0)),
                    new Line2D(new Point2D(5, 0), new Point2D(15, 0))
                };
                
                var request = new OverKillRequest
                {
                    Lines = lines,
                    Tolerance = 1e-6,
                    ExtensionDistance = 10.0
                };
                
                var result = useCase.Execute(request);
                
                ed.WriteMessage($"\n处理结果: {result.Message}");
                ed.WriteMessage($"\n合并数量: {result.MergedCount}");
                ed.WriteMessage($"\n最终线段数: {result.ProcessedLines.Count}");
                
                if (result.Success && result.MergedCount == 1)
                {
                    ed.WriteMessage("\n✅ 用例执行测试通过");
                }
                else
                {
                    ed.WriteMessage("\n❌ 用例执行测试失败");
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 测试失败: {ex.Message}");
            }
        }
    }
}

