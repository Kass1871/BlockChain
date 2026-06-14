using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BlockChainP34.Services
{
    public class WalletEncryptionService
    {
        private const int Iterations = 200000;
        private const int SaltSize = 16;
        private const int KeySize = 32;
        private const int IvSize = 16;

        public static string Encrypt(string plainText, string password)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var key = DeriveKey(password, salt);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = encryptor.TransformFinalBlock(plainTextBytes, 0, plainTextBytes.Length);

            var result = new byte[SaltSize + IvSize + cipherBytes.Length];
            Buffer.BlockCopy(salt, 0, result, 0, SaltSize);
            Buffer.BlockCopy(aes.IV, 0, result, SaltSize, IvSize);
            Buffer.BlockCopy(cipherBytes, 0, result, SaltSize + IvSize, cipherBytes.Length);

            return Convert.ToBase64String(result);
        }

        public static string Decrypt(string base64CipherText, string password)
        {
            var raw = Convert.FromBase64String(base64CipherText);

            var salt = raw[..SaltSize];
            var iv = raw[SaltSize..(SaltSize + IvSize)];
            var cipherBytes = raw[(SaltSize + IvSize)..];

            var key = DeriveKey(password, salt);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }

        public static byte[] DeriveKey(string password, byte[] salt) =>
            Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, KeySize);
    }
}
