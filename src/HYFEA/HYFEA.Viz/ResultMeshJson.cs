using System;
using System.Globalization;
using System.Text;

namespace HYFEA.Viz;

/// <summary>将 <see cref="ResultMesh"/> 序列化为 vtk.js 视口契约 JSON（无 UI 依赖）。</summary>
public static class ResultMeshJson
{
    public static string ToJson(ResultMesh mesh, string activeScalar = "U_mag")
    {
        if (mesh == null) throw new ArgumentNullException(nameof(mesh));
        if (string.IsNullOrWhiteSpace(activeScalar))
            activeScalar = "U_mag";

        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder(Math.Max(256, mesh.Points.Length * 12));
        sb.Append('{');
        sb.Append("\"points\":[");
        AppendDoubles(sb, mesh.Points, inv);
        sb.Append("],\"lines\":[");
        AppendInts(sb, mesh.LineConnectivity, inv);
        sb.Append("],\"scalars\":{");
        bool first = true;
        foreach (var kv in mesh.PointData)
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append('"').Append(Escape(kv.Key)).Append("\":[");
            AppendDoubles(sb, kv.Value ?? Array.Empty<double>(), inv);
            sb.Append(']');
        }

        sb.Append("},\"activeScalar\":\"").Append(Escape(activeScalar)).Append("\"}");
        return sb.ToString();
    }

    public static byte[] ToUtf8(ResultMesh mesh, string activeScalar = "U_mag") =>
        Encoding.UTF8.GetBytes(ToJson(mesh, activeScalar));

    private static void AppendDoubles(StringBuilder sb, double[] values, CultureInfo inv)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(values[i].ToString("G17", inv));
        }
    }

    private static void AppendInts(StringBuilder sb, int[] values, CultureInfo inv)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(values[i].ToString(inv));
        }
    }

    private static string Escape(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
