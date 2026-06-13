using Autodesk.AutoCAD.EditorInput;

namespace HyCADTool.Features.DCEL.Services
{
    /// <summary>
    /// DCEL 分阶段耗时输出
    /// </summary>
    public static class DCELTimingReporter
    {
        public static void WriteDetailedTiming(Editor ed, DCELRunMetrics m, string title = "详细性能分析")
        {
            double denom = m.TotalMs > 0 ? m.TotalMs : 1;
            ed.WriteMessage($"\n━━━━━━━━━━ {title} ━━━━━━━━━━");
            ed.WriteMessage($"\n  1. ID收集     : {m.CollectMs}ms ({Pct(m.CollectMs, denom)})");
            ed.WriteMessage($"\n  2. 提取+简化  : {m.ExtractMs}ms ({Pct(m.ExtractMs, denom)})");
            ed.WriteMessage($"\n  3. DCEL构建   : {m.BuildMs}ms ({Pct(m.BuildMs, denom)})");
            ed.WriteMessage($"\n  4. 统计信息   : {m.StatsMs}ms ({Pct(m.StatsMs, denom)})");
            ed.WriteMessage($"\n  5. 拓扑验证   : {m.ValidateMs}ms ({Pct(m.ValidateMs, denom)})");
            ed.WriteMessage($"\n  6. 渲染       : {m.RenderMs}ms ({Pct(m.RenderMs, denom)})");
            ed.WriteMessage($"\n  7. 其他开销   : {m.OtherMs}ms ({Pct(m.OtherMs, denom)})");
            ed.WriteMessage($"\n  顶点/边/面    : {m.VertexCount} / {m.EdgeCount} / {m.FaceCount}（{m.OuterFaceCount} 外 + {m.InnerFaceCount} 内）");
            ed.WriteMessage("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        }

        private static string Pct(long ms, double total) => $"{ms * 100.0 / total:F1}%";
    }
}
