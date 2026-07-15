using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace HYFEA.Viz;

/// <summary>写出 VTK XML UnstructuredGrid（ASCII .vtu），ParaView 可直接打开。</summary>
public static class VtuWriter
{
    /// <summary>VTK_LINE cell type.</summary>
    public const byte VtkLine = 3;

    public static void Write(ResultMesh mesh, string path)
    {
        if (mesh == null) throw new ArgumentNullException(nameof(mesh));
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("路径无效。", nameof(path));

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        // 原子写：避免边车读到半成品 XML 导致进程异常
        var tmp = path + ".tmp";
        using (var fs = File.Create(tmp))
            Write(mesh, fs);

        try
        {
            File.Copy(tmp, path, overwrite: true);
        }
        finally
        {
            try { File.Delete(tmp); } catch { /* ignore */ }
        }
    }

    public static void Write(ResultMesh mesh, Stream stream)
    {
        if (mesh == null) throw new ArgumentNullException(nameof(mesh));
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        int nPts = mesh.NumberOfPoints;
        int nCells = mesh.NumberOfLines;
        if (nPts < 2 || nCells < 1)
            throw new ArgumentException("网格为空。", nameof(mesh));

        var inv = CultureInfo.InvariantCulture;
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 1024, leaveOpen: true);

        writer.WriteLine(@"<?xml version=""1.0""?>");
        writer.WriteLine(@"<VTKFile type=""UnstructuredGrid"" version=""0.1"" byte_order=""LittleEndian"">");
        writer.WriteLine("  <UnstructuredGrid>");
        writer.WriteLine($"    <Piece NumberOfPoints=\"{nPts}\" NumberOfCells=\"{nCells}\">");

        writer.WriteLine("      <Points>");
        writer.WriteLine(@"        <DataArray type=""Float64"" NumberOfComponents=""3"" format=""ascii"">");
        for (int i = 0; i < nPts; i++)
        {
            writer.Write("          ");
            writer.Write(mesh.Points[i * 3].ToString("G17", inv));
            writer.Write(' ');
            writer.Write(mesh.Points[i * 3 + 1].ToString("G17", inv));
            writer.Write(' ');
            writer.WriteLine(mesh.Points[i * 3 + 2].ToString("G17", inv));
        }

        writer.WriteLine("        </DataArray>");
        writer.WriteLine("      </Points>");

        writer.WriteLine("      <Cells>");
        writer.WriteLine(@"        <DataArray type=""Int32"" Name=""connectivity"" format=""ascii"">");
        writer.Write("          ");
        for (int i = 0; i < mesh.LineConnectivity.Length; i++)
        {
            if (i > 0) writer.Write(' ');
            writer.Write(mesh.LineConnectivity[i].ToString(inv));
        }

        writer.WriteLine();
        writer.WriteLine("        </DataArray>");
        writer.WriteLine(@"        <DataArray type=""Int32"" Name=""offsets"" format=""ascii"">");
        writer.Write("          ");
        for (int c = 0; c < nCells; c++)
        {
            if (c > 0) writer.Write(' ');
            writer.Write(((c + 1) * 2).ToString(inv));
        }

        writer.WriteLine();
        writer.WriteLine("        </DataArray>");
        writer.WriteLine(@"        <DataArray type=""UInt8"" Name=""types"" format=""ascii"">");
        writer.Write("          ");
        for (int c = 0; c < nCells; c++)
        {
            if (c > 0) writer.Write(' ');
            writer.Write(VtkLine.ToString(inv));
        }

        writer.WriteLine();
        writer.WriteLine("        </DataArray>");
        writer.WriteLine("      </Cells>");

        var vectorsAttr = mesh.PointVectors.Count > 0 && mesh.PointVectors.ContainsKey("U")
            ? @" Vectors=""U"""
            : "";
        writer.WriteLine($@"      <PointData Scalars=""U_mag""{vectorsAttr}>");
        foreach (var kv in mesh.PointData)
        {
            if (kv.Value == null || kv.Value.Length != nPts)
                throw new InvalidOperationException($"点场 {kv.Key} 长度与节点数不一致。");

            writer.WriteLine($"        <DataArray type=\"Float64\" Name=\"{EscapeXml(kv.Key)}\" format=\"ascii\">");
            writer.Write("          ");
            for (int i = 0; i < nPts; i++)
            {
                if (i > 0) writer.Write(' ');
                writer.Write(kv.Value[i].ToString("G17", inv));
            }

            writer.WriteLine();
            writer.WriteLine("        </DataArray>");
        }

        foreach (var kv in mesh.PointVectors)
        {
            if (kv.Value == null || kv.Value.Length != nPts * 3)
                throw new InvalidOperationException($"点向量场 {kv.Key} 长度应为 3×节点数。");

            writer.WriteLine(
                $"        <DataArray type=\"Float64\" Name=\"{EscapeXml(kv.Key)}\" NumberOfComponents=\"3\" format=\"ascii\">");
            writer.Write("          ");
            for (int i = 0; i < kv.Value.Length; i++)
            {
                if (i > 0) writer.Write(' ');
                writer.Write(kv.Value[i].ToString("G17", inv));
            }

            writer.WriteLine();
            writer.WriteLine("        </DataArray>");
        }

        writer.WriteLine("      </PointData>");

        writer.WriteLine("    </Piece>");
        writer.WriteLine("  </UnstructuredGrid>");
        writer.WriteLine("</VTKFile>");
        writer.Flush();
    }

    private static string EscapeXml(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
