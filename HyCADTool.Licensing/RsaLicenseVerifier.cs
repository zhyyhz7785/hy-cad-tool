using System;
using System.Security.Cryptography;
namespace HyCADTool.Licensing
{
    public static class RsaLicenseVerifier
    {
        public static bool VerifyDataUtf8(string rsaPublicKeyXml, byte[] utf8Payload, string signatureBase64)
        {
            if (string.IsNullOrEmpty(signatureBase64) || utf8Payload == null || utf8Payload.Length == 0)
                return false;
            byte[] sig;
            try
            {
                sig = Convert.FromBase64String(signatureBase64);
            }
            catch
            {
                return false;
            }
            try
            {
                byte[] hash;
                using (var sha = SHA256.Create())
                    hash = sha.ComputeHash(utf8Payload);
                using (var rsa = new RSACryptoServiceProvider())
                {
                    rsa.FromXmlString(rsaPublicKeyXml);
                    return rsa.VerifyHash(hash, "SHA256", sig);
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
