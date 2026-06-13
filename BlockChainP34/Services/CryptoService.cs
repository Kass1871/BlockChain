using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace BlockChainP34.Services
{
    public class CryptoService
    {
        public (string publicKey, string privateKey) GenerateKeyPair()
        {
            using (var rsa = RSA.Create())
            {
                var publicKey = Convert.ToBase64String(rsa.ExportRSAPublicKey());
                var privateKey = Convert.ToBase64String(rsa.ExportRSAPrivateKey());



                return (publicKey, privateKey);
            }
        }
        public byte[] SignData(string data, string privateKey)
        {
            using (var rsa = RSA.Create())
            {
                rsa.ImportRSAPrivateKey(Convert.FromBase64String(privateKey), out _);
                var dataBytes = Encoding.UTF8.GetBytes(data);
                return rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
        }

        public bool VerifySignature(string data, byte[] signature, string publicKey)
        {
            using (var rsa = RSA.Create())
            {
                try
                {
                    rsa.ImportRSAPublicKey(Convert.FromBase64String(publicKey), out _);
                    var dataBytes = Encoding.UTF8.GetBytes(data);
                    return rsa.VerifyData(dataBytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error caught: {ex.Message}");
                }
            }
        }
    }
}
