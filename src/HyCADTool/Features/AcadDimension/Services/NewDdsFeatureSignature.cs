using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HyCAD.Geometry;

namespace HyCADTool.Features.AcadDimension.Services
{
    internal static class NewDdsFeatureSignature
    {
        /// <summary>
        /// 对 tessellate 后的轮廓做稳定摘要（顶点坐标量化到 0.001mm + 闭合标记）。
        /// </summary>
        public static string Compute(Polyline2D boundary)
        {
            if (boundary == null || boundary.VertexCount == 0)
                return string.Empty;

            var sb = new StringBuilder();
            sb.Append(boundary.IsClosed ? '1' : '0').Append('|');
            foreach (var v in boundary.Vertices)
            {
                sb.Append(Math.Round(v.X, 3).ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(Math.Round(v.Y, 3).ToString(CultureInfo.InvariantCulture)).Append(';');
            }

            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                var hex = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                    hex.Append(b.ToString("x2"));
                return hex.ToString();
            }
        }
    }
}
