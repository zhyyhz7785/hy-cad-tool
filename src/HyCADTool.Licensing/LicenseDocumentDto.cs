using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HyCADTool.Licensing
{
    /// <summary>license.lic 反序列化 DTO。签名对「除 signature 外所有字段的规范化 JSON（UTF-8）」作 RSA+SHA256（PKCS#1 v1.5）。</summary>
    public sealed class LicenseDocumentDto
    {
        [JsonProperty("v", Required = Required.Always)]
        public int V { get; set; } = 1;

        [JsonProperty("customer")]
        public string Customer { get; set; }

        [JsonProperty("machine_code", Required = Required.Always)]
        public string MachineCode { get; set; }

        /// <summary>standard | professional | enterprise</summary>
        [JsonProperty("edition", Required = Required.Always)]
        public string Edition { get; set; }

        [JsonProperty("features")]
        public List<string> Features { get; set; } = new List<string>();

        [JsonProperty("issued_at", Required = Required.Always)]
        public DateTime IssuedAt { get; set; }

        [JsonProperty("expires_at", Required = Required.Always)]
        public DateTime ExpiresAt { get; set; }

        [JsonProperty("perpetual", Required = Required.Always)]
        public bool Perpetual { get; set; }

        [JsonProperty("max_devices")]
        public int MaxDevices { get; set; } = 1;

        [JsonProperty("migration_token")]
        public string MigrationToken { get; set; }

        [JsonProperty("signature", Required = Required.Always)]
        public string Signature { get; set; }
    }
}
