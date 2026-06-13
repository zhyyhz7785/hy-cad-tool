namespace HyCADTool.Licensing
{
    /// <summary>嵌入公钥，与 LicenseSigner 使用的私钥配对。轮换密钥时同时更新签发端 PrivateKey 与本处 XML。</summary>
    public static class EmbeddedPublicKey
    {
        /// <summary>须与仓库 <c>build/keys/rsa-pub.xml</c> 内容一致（运行 <c>build/scripts/_gen-rsa-keys.ps1</c> 后请用新公钥覆盖此处并重新编译 HyCADTool）。</summary>
        public const string RsaKeyXml = @"<RSAKeyValue><Modulus>ruNLSfevmuJLp2FgWAlLTSVmzIzxfAIZ/RZt1CQpLNdZe4q2AfpLiFdwNgBVCKj73xObUPy1ObUUF/Gk8KyWfqWvDIwwoMVaAT7UcEvS3nq9GIpqhVjoLsdPNpbS5xUz4y6NlVPtdkFQdJwKKygQ1woKlOrcgxQz8z3HHkJMbXaGtW+pdk80VB5MfQlrtqHFcmxPjD7ZwYfP0ia123E5EaW6ogPiMhOOJ2IrQhpHldd3c+LimftBFbo1BhJXILklXnIYeDgXjKJ/tziLdaxg6+OcPzFbYNESt9XcbRP3DKi2HLwxPpWRCVWIa26unKGJdskPVyoDl+OmguRob4fTsQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";
    }
}
