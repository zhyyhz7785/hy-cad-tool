using System;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HyCADTool.Licensing
{
    /// <summary>签名/验签：对 JObject 去掉 signature 后按属性名深度排序，再 <see cref="Formatting.None"/> UTF-8。</summary>
    public static class LicenseCanonicalizer
    {
        public const string SignaturePropertyName = "signature";

        public static void RemoveSignatureProperty(JObject j)
        {
            if (j == null) return;
            var p = j.Property(SignaturePropertyName);
            if (p == null) p = j.Properties().FirstOrDefault(x => x.Name.Equals(SignaturePropertyName, StringComparison.OrdinalIgnoreCase));
            p?.Remove();
        }

        public static byte[] GetUtf8SignPayloadFromJObject(JObject j)
        {
            var c = (JObject)j.DeepClone();
            RemoveSignatureProperty(c);
            return Encoding.UTF8.GetBytes(SortObject(c).ToString(Formatting.None));
        }

        public static JObject SortObject(JObject obj)
        {
            var p = obj.Properties()
                .OrderBy(x => x.Name, StringComparer.Ordinal)
                .Select(x => new JProperty(x.Name, SortToken(x.Value)))
                .ToArray();
            return new JObject(p);
        }

        private static JToken SortToken(JToken t)
        {
            if (t is JObject o) return SortObject(o);
            if (t is JArray a) return new JArray(a.Select(SortToken));
            return t.DeepClone();
        }
    }
}
