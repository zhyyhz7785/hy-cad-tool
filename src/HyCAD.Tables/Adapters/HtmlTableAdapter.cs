using System.Globalization;
using System.Text;
using HyCAD.Tables.Data;
using HyCAD.Tables.Operations;
using HyCAD.Tables.Structure;

namespace HyCAD.Tables.Adapters;

/// <summary>
/// Domain <see cref="TableGrid"/> → HTML table 适配器（阶段 B 预览出口）。
/// </summary>
public sealed class HtmlTableAdapter : ITableAdapter<string>
{
    /// <inheritdoc />
    public AdapterCapability Capability { get; } = AdapterCapability.HtmlDefaults;

    /// <inheritdoc />
    public TableGrid Import(string source) =>
        throw new NotSupportedException("HtmlTableAdapter 仅支持 Export；HTML → Domain 为后续阶段。");

    /// <inheritdoc />
    public string Export(TableGrid model) => Export(model, HtmlTableExportOptions.Default);

    /// <summary>按选项导出 HTML。</summary>
    public string Export(TableGrid grid, HtmlTableExportOptions options)
    {
        if (grid == null)
            throw new ArgumentNullException(nameof(grid));
        if (options == null)
            options = HtmlTableExportOptions.Default;

        var tableHtml = BuildTable(grid, options);
        if (!options.FullDocument)
            return tableHtml;

        var sb = new StringBuilder(4096);
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"zh-CN\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        if (!string.IsNullOrEmpty(options.Title))
            sb.AppendLine("<title>" + Escape(options.Title!) + "</title>");

        if (options.InlineCss)
        {
            sb.AppendLine("<style>");
            sb.AppendLine(GetDefaultCss(options));
            sb.AppendLine("</style>");
        }

        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine(tableHtml);
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static string BuildTable(TableGrid grid, HtmlTableExportOptions options)
    {
        var structure = grid.Structure;
        var topology = structure.Topology;
        var sb = new StringBuilder(2048);

        sb.AppendLine("<table class=\"hy-table\">");

        sb.AppendLine("<colgroup>");
        foreach (var col in topology.Cols)
        {
            sb.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "<col style=\"width:{0}\">",
                FormatLength(col.Size, options)));
        }

        sb.AppendLine("</colgroup>");

        for (var row = 0; row < topology.RowCount; row++)
        {
            var rowHeight = topology.Rows[row].Size;
            sb.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "<tr style=\"height:{0}\">",
                FormatLength(rowHeight, options)));

            for (var col = 0; col < topology.ColCount; col++)
            {
                var addr = new CellAddr(row, col);
                if (structure.IsHidden(addr))
                    continue;

                sb.Append(BuildCell(grid, addr, options));
            }

            sb.AppendLine("</tr>");
        }

        sb.AppendLine("</table>");
        return sb.ToString();
    }

    private static string BuildCell(TableGrid grid, CellAddr addr, HtmlTableExportOptions options)
    {
        var structure = grid.Structure;
        MergeRegion? merge = null;
        structure.TryGetMergeAt(addr, out merge);
        var isAnchor = merge == null || merge.Anchor == addr;

        var style = ResolveStyle(structure, addr, options);

        var classes = new List<string>();
        if (style.Orientation == TextOrientation.VerticalStacked)
            classes.Add("vstack");

        CellRole role;
        if (structure.Roles.TryGetValue(addr, out role))
            classes.Add(RoleClass(role));

        var attrs = new StringBuilder();
        if (classes.Count > 0)
            attrs.Append(" class=\"").Append(string.Join(" ", classes)).Append('"');

        if (merge != null && isAnchor)
        {
            if (merge.RowSpan > 1)
            {
                attrs.Append(" rowspan=\"").Append(merge.RowSpan.ToString(CultureInfo.InvariantCulture)).Append('"');
            }

            if (merge.ColSpan > 1)
            {
                attrs.Append(" colspan=\"").Append(merge.ColSpan.ToString(CultureInfo.InvariantCulture)).Append('"');
            }
        }

        var inlineStyle = BuildCellInlineStyle(structure, addr, style, options);
        if (inlineStyle.Length > 0)
            attrs.Append(" style=\"").Append(inlineStyle).Append('"');

        var sb = new StringBuilder();
        sb.Append("<td");
        sb.Append(attrs);
        sb.Append('>');

        DiagonalSplit diagonal;
        if (structure.Diagonals.TryGetValue(addr, out diagonal))
            sb.Append(BuildDiagonalContent(diagonal));
        else
            sb.Append(Escape(GetDisplayText(GridEditor.GetValue(grid, addr))));

        sb.AppendLine("</td>");
        return sb.ToString();
    }

    private static string BuildDiagonalContent(DiagonalSplit split)
    {
        var directionClass = split.Direction == DiagonalDirection.BackSlashTLBR
            ? "diag-backslash"
            : "diag-slash";

        var sb = new StringBuilder();
        sb.Append("<div class=\"diag-cell ").Append(directionClass).Append("\">");

        for (var i = 0; i < split.Parts.Count; i++)
        {
            var part = split.Parts[i];
            var text = Escape(GetDisplayText(part.Value));
            var anchor = part.TextAnchor;

            var posStyle = string.Format(
                CultureInfo.InvariantCulture,
                "left:{0:0.##}%;top:{1:0.##}%;text-align:{2}",
                anchor.X * 100,
                anchor.Y * 100,
                ToCssTextAlign(part.Align));

            sb.Append("<span class=\"diag-part diag-part-")
                .Append(i.ToString(CultureInfo.InvariantCulture))
                .Append("\" style=\"")
                .Append(posStyle)
                .Append("\">")
                .Append(text)
                .Append("</span>");
        }

        sb.Append("</div>");
        return sb.ToString();
    }

    private static CellStyle ResolveStyle(GridStructure structure, CellAddr addr, HtmlTableExportOptions options)
    {
        CellStyle style;
        if (structure.Styles.TryGetValue(addr, out style))
            return style;

        var defaultBorder = structure.Topology.DefaultBorder;
        if (defaultBorder != null && defaultBorder != BorderSet.None)
            return new CellStyle(Borders: defaultBorder);

        return new CellStyle();
    }

    private static string BuildCellInlineStyle(
        GridStructure structure,
        CellAddr addr,
        CellStyle style,
        HtmlTableExportOptions options)
    {
        var parts = new List<string>();

        parts.Add("font-size:" + FormatLength(style.TextHeight, options));
        parts.Add("font-family:" + options.FontFamily);
        parts.Add("text-align:" + ToCssTextAlign(style.HAlign));
        parts.Add("vertical-align:" + ToCssVerticalAlign(style.VAlign));

        if (!string.IsNullOrEmpty(style.BackColor))
            parts.Add("background-color:" + style.BackColor);

        var borders = style.Borders ?? structure.Topology.DefaultBorder ?? BorderSet.None;
        AppendBorderStyle(parts, borders, options, options.DefaultBorderWidthMm);

        return string.Join(";", parts) + ";";
    }

    private static void AppendBorderStyle(
        List<string> parts,
        BorderSet borders,
        HtmlTableExportOptions options,
        double fallbackWidthMm)
    {
        AppendBorderSide(parts, "top", borders.Top, options, fallbackWidthMm);
        AppendBorderSide(parts, "right", borders.Right, options, fallbackWidthMm);
        AppendBorderSide(parts, "bottom", borders.Bottom, options, fallbackWidthMm);
        AppendBorderSide(parts, "left", borders.Left, options, fallbackWidthMm);
    }

    private static void AppendBorderSide(
        List<string> parts,
        string side,
        double widthMm,
        HtmlTableExportOptions options,
        double fallbackWidthMm)
    {
        var width = widthMm > 0 ? widthMm : fallbackWidthMm;
        parts.Add("border-" + side + "-width:" + FormatLength(width, options));
        parts.Add("border-" + side + "-style:solid");
        parts.Add("border-" + side + "-color:#000");
    }

    private static string GetDisplayText(CellValue value)
    {
        if (value.Runs != null && value.Runs.Count > 0)
        {
            var sb = new StringBuilder();
            foreach (var run in value.Runs)
                sb.Append(run.Text);
            return sb.ToString();
        }

        return value.Text ?? string.Empty;
    }

    private static string RoleClass(CellRole role)
    {
        switch (role)
        {
            case CellRole.Title:
                return "role-title";
            case CellRole.Header:
                return "role-header";
            case CellRole.Label:
                return "role-label";
            case CellRole.Value:
                return "role-value";
            case CellRole.PhotoSlot:
                return "role-photoslot";
            case CellRole.Spacer:
                return "role-spacer";
            default:
                return "role-unknown";
        }
    }

    private static string ToCssTextAlign(TextAlign align)
    {
        switch (align)
        {
            case TextAlign.Start:
                return "left";
            case TextAlign.Center:
                return "center";
            case TextAlign.End:
                return "right";
            default:
                return "left";
        }
    }

    private static string ToCssVerticalAlign(TextAlign align)
    {
        switch (align)
        {
            case TextAlign.Start:
                return "top";
            case TextAlign.Center:
                return "middle";
            case TextAlign.End:
                return "bottom";
            default:
                return "middle";
        }
    }

    private static string FormatLength(double mm, HtmlTableExportOptions options)
    {
        if (options.UseMillimeters)
            return mm.ToString("0.####", CultureInfo.InvariantCulture) + "mm";

        var px = mm * options.MmToPxRatio;
        return px.ToString("0.####", CultureInfo.InvariantCulture) + "px";
    }

    private static string Escape(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }

    private static string GetDefaultCss(HtmlTableExportOptions options)
    {
        var border = FormatLength(options.DefaultBorderWidthMm, options);
        return
            "body { margin: 12px; }\n" +
            ".hy-table {\n" +
            "  border-collapse: collapse;\n" +
            "  table-layout: fixed;\n" +
            "}\n" +
            ".hy-table td {\n" +
            "  border: " + border + " solid #000;\n" +
            "  padding: 1mm;\n" +
            "  overflow: hidden;\n" +
            "  word-wrap: break-word;\n" +
            "}\n" +
            ".hy-table td.vstack {\n" +
            "  writing-mode: vertical-rl;\n" +
            "  text-orientation: mixed;\n" +
            "}\n" +
            ".hy-table td.role-title {\n" +
            "  font-weight: bold;\n" +
            "  text-align: center;\n" +
            "}\n" +
            ".hy-table td.role-header {\n" +
            "  font-weight: bold;\n" +
            "}\n" +
            ".hy-table td.role-photoslot {\n" +
            "  background-color: #f5f5f5;\n" +
            "}\n" +
            ".diag-cell {\n" +
            "  position: relative;\n" +
            "  width: 100%;\n" +
            "  height: 100%;\n" +
            "  min-height: 8mm;\n" +
            "}\n" +
            ".diag-cell.diag-backslash {\n" +
            "  background: linear-gradient(to bottom right,\n" +
            "    transparent calc(50% - 0.5px),\n" +
            "    #000 calc(50% - 0.5px),\n" +
            "    #000 calc(50% + 0.5px),\n" +
            "    transparent calc(50% + 0.5px));\n" +
            "}\n" +
            ".diag-cell.diag-slash {\n" +
            "  background: linear-gradient(to top right,\n" +
            "    transparent calc(50% - 0.5px),\n" +
            "    #000 calc(50% - 0.5px),\n" +
            "    #000 calc(50% + 0.5px),\n" +
            "    transparent calc(50% + 0.5px));\n" +
            "}\n" +
            ".diag-part {\n" +
            "  position: absolute;\n" +
            "  transform: translate(-50%, -50%);\n" +
            "  white-space: nowrap;\n" +
            "}\n";
    }
}
