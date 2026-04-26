namespace HyCADTool.Licensing
{
    /// <summary>嵌入公钥，与 LicenseSigner 使用的私钥配对。轮换密钥时同时更新签发端 PrivateKey 与本处 XML。</summary>
    public static class EmbeddedPublicKey
    {
        /// <summary>须与仓库 <c>build/keys/rsa-pub.xml</c> 内容一致（运行 <c>build/scripts/_gen-rsa-keys.ps1</c> 后请用新公钥覆盖此处并重新编译 HyCADTool）。</summary>
        public const string RsaKeyXml = @"<RSAKeyValue><Modulus>wg4YdBLK6WTS3cjdngr3hVL1AW7Lk68uRRaXJHUqLVJZo+lGEBqV83nKw4s2aBGezdnxM0/Oim7hSL67h/QoiqhsfoWQdtjJze28CI0uOJ1zbDLFRJYJ31FPzTYqfvqGcVmjpc9fndo3kqer/BlDhqC7J9Gf5mZKtsAi4kSmx0Yb+kkITKQFHJb9DijYgHqmk/cQJlfhcU/PaRxB/ce1Zxma810EOeMNJfalgumNbQvuSsa+6nqXkNa1gD3BzQUVip9BiueLyTtL+DbrJhwLUR+mnE0tfp3LfB7ca3k8CAd3Mot3y1vfwyfSGVGZCIIP94nJpOeuLP6hYg/uDciHZQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";
    }
}
