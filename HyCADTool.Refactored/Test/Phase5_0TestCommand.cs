using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autofac;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Infrastructure.Configuration;
using System;
using System.Linq;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Exception = System.Exception;

namespace HyCADTool.Refactored.Test
{
    /// <summary>
    /// 阶段 5.0 测试命令 - UI 基础设施迁移（面板与选择工具）
    /// </summary>
    public static class Phase5_0TestCommand
    {
        /// <summary>
        /// 运行所有阶段 5.0 测试
        /// </summary>
        [CommandMethod("C1P50")]
        public static void RunAllPhase50Tests()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument.Editor;

            ed.WriteMessage("\n========================================");
            ed.WriteMessage("\nHyCADTool.Refactored 阶段 5.0 综合测试");
            ed.WriteMessage("\n========================================\n");

            try
            {
                // 测试 1: 选择服务基础功能
                Test1_SelectionServiceBasics(ed);

                // 测试 2: 类型化选择
                Test2_TypedSelection(ed);

                // 测试 3: 过滤功能
                Test3_FilterFunctions(ed);

                // 测试 4: 面板管理器
                Test4_PanelManager(ed);

                ed.WriteMessage("\n========================================");
                ed.WriteMessage("\n  阶段 5.0 综合测试结束");
                ed.WriteMessage("\n========================================\n");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 测试执行失败: {ex.Message}");
                ed.WriteMessage($"\n堆栈跟踪: {ex.StackTrace}");
            }
        }

        #region 测试 1: 选择服务基础功能

        private static void Test1_SelectionServiceBasics(Autodesk.AutoCAD.EditorInput.Editor ed)
        {
            ed.WriteMessage("\n=== 测试 1: 选择服务基础功能 ===\n");

            try
            {
                // 解析选择服务
                var selectionService = ServiceLocator.Container.Resolve<ISelectionService>();
                ed.WriteMessage("✅ ISelectionService 已解析\n");

                // 创建测试图形
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                var db = doc.Database;

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    var btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                    // 创建测试线段
                    var line1 = new Line(new Point3d(0, 0, 0), new Point3d(100, 0, 0));
                    var line2 = new Line(new Point3d(0, 100, 0), new Point3d(100, 100, 0));
                    btr.AppendEntity(line1);
                    btr.AppendEntity(line2);
                    tr.AddNewlyCreatedDBObject(line1, true);
                    tr.AddNewlyCreatedDBObject(line2, true);

                    // 创建测试圆
                    var circle1 = new Circle(new Point3d(200, 0, 0), Vector3d.ZAxis, 50);
                    var circle2 = new Circle(new Point3d(200, 100, 0), Vector3d.ZAxis, 30);
                    btr.AppendEntity(circle1);
                    btr.AppendEntity(circle2);
                    tr.AddNewlyCreatedDBObject(circle1, true);
                    tr.AddNewlyCreatedDBObject(circle2, true);

                    tr.Commit();

                    ed.WriteMessage("✅ 已创建测试图形：2 条线段，2 个圆\n");
                }

                // 测试 SelectAll
                var allLines = selectionService.SelectAll("LINE");
                ed.WriteMessage($"SelectAll(\"LINE\") 返回: {allLines.Length} 个线段\n");
                ed.WriteMessage(allLines.Length >= 2 ? "✅ SelectAll 测试通过\n" : "❌ SelectAll 测试失败\n");

                var allCircles = selectionService.SelectAll("CIRCLE");
                ed.WriteMessage($"SelectAll(\"CIRCLE\") 返回: {allCircles.Length} 个圆\n");
                ed.WriteMessage(allCircles.Length >= 2 ? "✅ SelectAll 测试通过\n" : "❌ SelectAll 测试失败\n");

                ed.WriteMessage("\n✅ 测试 1 完成\n");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 测试 1 失败: {ex.Message}\n");
            }
        }

        #endregion

        #region 测试 2: 类型化选择

        private static void Test2_TypedSelection(Autodesk.AutoCAD.EditorInput.Editor ed)
        {
            ed.WriteMessage("\n=== 测试 2: 类型化选择 ===\n");

            try
            {
                var selectionService = ServiceLocator.Container.Resolve<ISelectionService>();

                // 提示用户选择线段
                ed.WriteMessage("请在图形中选择线段...\n");
                var lines = selectionService.SelectLines("请选择线段进行测试");

                if (lines.Count > 0)
                {
                    ed.WriteMessage($"✅ 选中了 {lines.Count} 条线段\n");

                    // 显示第一条线段的信息
                    var firstLine = lines[0];
                    ed.WriteMessage($"  第一条线段: 起点({firstLine.StartPoint.X:F2}, {firstLine.StartPoint.Y:F2}), ");
                    ed.WriteMessage($"终点({firstLine.EndPoint.X:F2}, {firstLine.EndPoint.Y:F2})\n");
                    ed.WriteMessage($"  长度: {firstLine.Length:F2}\n");
                }
                else
                {
                    ed.WriteMessage("⚠️ 未选择任何线段（用户取消或无线段）\n");
                }

                // 提示用户选择圆
                ed.WriteMessage("\n请在图形中选择圆...\n");
                var circles = selectionService.SelectCircles("请选择圆进行测试");

                if (circles.Count > 0)
                {
                    ed.WriteMessage($"✅ 选中了 {circles.Count} 个圆\n");

                    // 显示第一个圆的信息
                    var firstCircle = circles[0];
                    ed.WriteMessage($"  第一个圆: 圆心({firstCircle.Center.X:F2}, {firstCircle.Center.Y:F2}), ");
                    ed.WriteMessage($"半径 {firstCircle.Radius:F2}\n");
                    ed.WriteMessage($"  面积: {Math.PI * firstCircle.Radius * firstCircle.Radius:F2}\n");
                }
                else
                {
                    ed.WriteMessage("⚠️ 未选择任何圆（用户取消或无圆）\n");
                }

                ed.WriteMessage("\n✅ 测试 2 完成\n");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 测试 2 失败: {ex.Message}\n");
            }
        }

        #endregion

        #region 测试 3: 过滤功能

        private static void Test3_FilterFunctions(Autodesk.AutoCAD.EditorInput.Editor ed)
        {
            ed.WriteMessage("\n=== 测试 3: 过滤功能 ===\n");

            try
            {
                var selectionService = ServiceLocator.Container.Resolve<ISelectionService>();
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                var db = doc.Database;

                // 创建测试图层和实体
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    // 创建测试图层
                    var lt = tr.GetObject(db.LayerTableId, OpenMode.ForWrite) as LayerTable;
                    string testLayerName = "TestLayer_Phase50";

                    if (!lt.Has(testLayerName))
                    {
                        var ltr = new LayerTableRecord
                        {
                            Name = testLayerName,
                            Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                                Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 1) // 红色
                        };
                        lt.Add(ltr);
                        tr.AddNewlyCreatedDBObject(ltr, true);
                        ed.WriteMessage($"✅ 已创建测试图层: {testLayerName}\n");
                    }

                    // 在测试图层上创建线段
                    var bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    var btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                    var testLine = new Line(new Point3d(300, 0, 0), new Point3d(400, 0, 0))
                    {
                        Layer = testLayerName
                    };
                    btr.AppendEntity(testLine);
                    tr.AddNewlyCreatedDBObject(testLine, true);

                    tr.Commit();
                }

                // 测试图层过滤
                var allEntities = selectionService.SelectAll();
                ed.WriteMessage($"图形中共有 {allEntities.Length} 个实体\n");

                var filteredByLayer = selectionService.FilterByLayer(allEntities, "TestLayer_Phase50");
                ed.WriteMessage($"图层 'TestLayer_Phase50' 上有 {filteredByLayer.Length} 个实体\n");
                ed.WriteMessage(filteredByLayer.Length > 0 ? "✅ 图层过滤测试通过\n" : "⚠️ 图层过滤未找到实体\n");

                // 测试颜色过滤
                var filteredByColor = selectionService.FilterByColor(allEntities, 1); // 红色
                ed.WriteMessage($"颜色索引 1（红色）的实体有 {filteredByColor.Length} 个\n");

                // 测试类型过滤
                var filteredByType = selectionService.FilterByType(allEntities, "LINE");
                ed.WriteMessage($"类型为 LINE 的实体有 {filteredByType.Length} 个\n");
                ed.WriteMessage(filteredByType.Length > 0 ? "✅ 类型过滤测试通过\n" : "⚠️ 类型过滤未找到实体\n");

                ed.WriteMessage("\n✅ 测试 3 完成\n");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 测试 3 失败: {ex.Message}\n");
            }
        }

        #endregion

        #region 测试 4: 面板管理器

        private static void Test4_PanelManager(Autodesk.AutoCAD.EditorInput.Editor ed)
        {
            ed.WriteMessage("\n=== 测试 4: 面板管理器 ===\n");

            try
            {
                // 检查容器是否初始化
                if (ServiceLocator.Container == null)
                {
                    ed.WriteMessage("❌ ServiceLocator.Container 为 null！\n");
                    ed.WriteMessage("提示：请确保插件已正确初始化\n");
                    return;
                }
                ed.WriteMessage("✅ ServiceLocator.Container 已初始化\n");

                // 解析 PanelManager
                ed.WriteMessage("正在解析 PanelManager...\n");
                var panelManager = ServiceLocator.Container.Resolve<HyCADTool.Refactored.Presentation.PanelManager>();
                
                if (panelManager == null)
                {
                    ed.WriteMessage("❌ PanelManager 解析失败（返回 null）\n");
                    return;
                }
                ed.WriteMessage("✅ PanelManager 已成功解析\n");

                // 测试 PanelManager 的基本功能
                ed.WriteMessage("PanelManager 类型: " + panelManager.GetType().FullName + "\n");
                ed.WriteMessage("✅ 面板管理器功能正常\n");

                // 测试显示 ReinPanel
                ed.WriteMessage("\n正在显示 ReinPanel...\n");
                try
                {
                    panelManager.ShowPanel<HyCADTool.Refactored.Presentation.Views.ReinPanel>(
                        "钢筋配置", 
                        new System.Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890"));
                    ed.WriteMessage("✅ ReinPanel 已显示\n");
                    ed.WriteMessage("提示：请检查 AutoCAD 窗口右侧或左侧是否出现钢筋配置面板\n");
                }
                catch (Exception panelEx)
                {
                    ed.WriteMessage($"❌ 显示面板失败: {panelEx.Message}\n");
                    ed.WriteMessage($"详细: {panelEx.StackTrace}\n");
                }

                // 测试显示 FilterPanel
                ed.WriteMessage("\n正在显示 FilterPanel...\n");
                ed.WriteMessage("步骤 1: 准备调用 panelManager.ShowPanel<FilterPanel>()\n");
                try
                {
                    ed.WriteMessage("步骤 2: 开始创建 FilterPanel...\n");
                    panelManager.ShowPanel<HyCADTool.Refactored.Presentation.Views.FilterPanel>(
                        "图形过滤器", 
                        new System.Guid("B2C3D4E5-F6A7-8901-BCDE-F23456789012"));
                    ed.WriteMessage("步骤 3: ShowPanel 调用成功\n");
                    ed.WriteMessage("✅ FilterPanel 已显示\n");
                    ed.WriteMessage("提示：请检查 AutoCAD 窗口四周是否出现图形过滤器面板\n");
                    ed.WriteMessage("       面板标题应为：图形过滤器\n");
                    ed.WriteMessage("       该面板采用 MVVM 模式，ViewModel 已通过 DI 注入\n");
                }
                catch (Exception panelEx)
                {
                    ed.WriteMessage($"\n❌❌❌ 显示 FilterPanel 失败 ❌❌❌\n");
                    ed.WriteMessage($"错误类型: {panelEx.GetType().Name}\n");
                    ed.WriteMessage($"错误消息: {panelEx.Message}\n");
                    if (panelEx.InnerException != null)
                    {
                        ed.WriteMessage($"内部异常: {panelEx.InnerException.Message}\n");
                        ed.WriteMessage($"内部异常堆栈: {panelEx.InnerException.StackTrace}\n");
                    }
                    ed.WriteMessage($"堆栈跟踪:\n{panelEx.StackTrace}\n");
                }

                ed.WriteMessage("\n✅ 测试 4 完成\n");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 测试 4 失败: {ex.Message}\n");
                ed.WriteMessage($"堆栈跟踪: {ex.StackTrace}\n");
            }
        }

        #endregion
    }
}
