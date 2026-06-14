using BlockChainP34.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace BlockChainP34.Models
{
    public class Wallet
    {
        public string PublicKey { get; set; }
        public string PrivateKey { get; set; }

        private Wallet() { }
        public Wallet(CryptoService cryptoService)
        {
            var keyPair = cryptoService.GenerateKeyPair();
            PublicKey = keyPair.publicKey;
            PrivateKey = keyPair.privateKey;

            Console.WriteLine("Set a password to protect your private key: ");
            var password = Console.ReadLine();
            Save(password);
        }

        private void Save(string password)
        {
            var encrypted = WalletEncryptionService.Encrypt(PrivateKey, password);
            var json = JsonSerializer.Serialize(new { PublicKey, PrivateKey = $"ENC:{encrypted}" });

            File.WriteAllText("wallet.json", json);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[Keystore] Wallet saved. Private key is encrypted.");
            Console.ForegroundColor = ConsoleColor.White;
        }

        public static Wallet Load(CryptoService cryptoService)
        {
            var json = File.ReadAllText("wallet.json");
            var stored = JsonSerializer.Deserialize<JsonElement>(json);

            var publicKey = stored.GetProperty("PublicKey").GetString()!;
            var encryptedPrivateKey = stored.GetProperty("PrivateKey").GetString()!;

            const int maxAtt = 3;
            
            for(int i = 1; i <= maxAtt; i++)
            {
                Console.WriteLine("Enter your wallet password: ");
                var password = Console.ReadLine();

                try
                {
                    string privateKey;
                    if (encryptedPrivateKey.StartsWith("ENC:"))
                    {
                        var encryptedPart = encryptedPrivateKey.Substring(4);
                        privateKey = WalletEncryptionService.Decrypt(encryptedPart, password);
                    }
                    else
                    {
                        privateKey = encryptedPrivateKey;
                    }
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("[Keystore] Wallet unlocked.");
                    Console.ForegroundColor = ConsoleColor.White;

                    return new Wallet { PublicKey = publicKey, PrivateKey = privateKey };
                }
                catch
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[Keystore] Wrong password or corrupted wallet.");
                    Console.ForegroundColor = ConsoleColor.White;
                }
            }
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[Keystore] Too many failed attempts. Create a new wallet? Old wallet file will be overwritten. (y/n)");
            Console.ForegroundColor = ConsoleColor.White;

            if (Console.ReadLine()?.Trim().ToLower() == "y")
            {
                return new Wallet(cryptoService);
            }

            return null!;
        }
    }
}
