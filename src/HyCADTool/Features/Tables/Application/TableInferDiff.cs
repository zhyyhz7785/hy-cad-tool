using System;
using System.Collections.Generic;
using System.Linq;
using HyCAD.Tables;
using HyCAD.Tables.Operations;
using HyCAD.Tables.Structure;

namespace HyCADTool.Features.Tables.TableApp
{
    /// <summary>识别结果与 golden TableGrid 对比（AC9 §9.3）。</summary>
    public sealed class TableInferDiff
    {
        public TableInferDiff(
            bool topologyMatch,
            bool mergeMatch,
            double textMatchRate,
            double overallScore)
        {
            TopologyMatch = topologyMatch;
            MergeMatch = mergeMatch;
            TextMatchRate = textMatchRate;
            OverallScore = overallScore;
        }

        public bool TopologyMatch { get; }

        public bool MergeMatch { get; }

        public double TextMatchRate { get; }

        public double OverallScore { get; }

        public static TableInferDiff Compare(
            TableGrid inferred,
            TableGrid golden,
            double endpointSnapMm = 1.0)
        {
            if (inferred == null)
                throw new ArgumentNullException(nameof(inferred));
            if (golden == null)
                throw new ArgumentNullException(nameof(golden));

            var topologyMatch = CompareTopology(inferred, golden, endpointSnapMm);
            var mergeMatch = CompareMerges(inferred, golden);
            var textMatchRate = CompareTexts(inferred, golden);
            var overall = 0.4 * (topologyMatch ? 1.0 : 0.0)
                + 0.3 * (mergeMatch ? 1.0 : 0.0)
                + 0.3 * textMatchRate;

            return new TableInferDiff(topologyMatch, mergeMatch, textMatchRate, overall);
        }

        private static bool CompareTopology(TableGrid inferred, TableGrid golden, double snap)
        {
            var a = inferred.Structure.Topology;
            var b = golden.Structure.Topology;
            if (a.RowCount != b.RowCount || a.ColCount != b.ColCount)
                return false;

            for (var i = 0; i < a.RowCount; i++)
            {
                if (Math.Abs(a.Rows[i].Size - b.Rows[i].Size) > snap)
                    return false;
            }

            for (var i = 0; i < a.ColCount; i++)
            {
                if (Math.Abs(a.Cols[i].Size - b.Cols[i].Size) > snap)
                    return false;
            }

            return true;
        }

        private static bool CompareMerges(TableGrid inferred, TableGrid golden)
        {
            var a = NormalizeMerges(inferred.Structure.Merges);
            var b = NormalizeMerges(golden.Structure.Merges);
            return a.SetEquals(b);
        }

        private static HashSet<(int Row, int Col, int RowSpan, int ColSpan)> NormalizeMerges(
            IReadOnlyList<MergeRegion> merges)
        {
            return merges
                .Select(m => (m.TopLeft.Row, m.TopLeft.Col, m.RowSpan, m.ColSpan))
                .ToHashSet();
        }

        private static double CompareTexts(TableGrid inferred, TableGrid golden)
        {
            var total = 0;
            var matched = 0;
            foreach (var entry in golden.Data.Cells)
            {
                var addr = entry.Key;
                if (golden.Structure.IsHidden(addr))
                    continue;

                var goldenText = NormalizeText(GridEditor.GetValue(golden, addr).Text);
                if (string.IsNullOrEmpty(goldenText))
                    continue;

                total++;
                var anchor = golden.Structure.GetAnchorOf(addr);
                var inferredText = NormalizeText(GridEditor.GetValue(inferred, anchor).Text);
                if (goldenText == inferredText)
                    matched++;
            }

            return total == 0 ? 1.0 : (double)matched / total;
        }

        private static string NormalizeText(string text) =>
            string.Join(" ", (text ?? string.Empty).Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
    }
}
