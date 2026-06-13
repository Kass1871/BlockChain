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
            var json = JsonSerializer.Serialize(new { PublicKey, PrivateKey = WalletEncryptionService.Encrypt(PrivateKey, password) });

            File.WriteAllText("wallet.json", json);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[Keystore] Wallet saved. Private key is encrypted.");
            Console.ForegroundColor = ConsoleColor.White;
        }

        public static Wallet Load()
        {
            var json = File.ReadAllText("wallet.json");
            var stored = JsonSerializer.Deserialize<JsonElement>(json);

            var publicKey = stored.GetProperty("PublicKey").GetString()!;
            var encryptedPrivateKey = stored.GetProperty("PrivateKey").GetString()!;

            Console.WriteLine("Enter your wallet password: ");
            var password = Console.ReadLine();

            try
            {
                var privateKey = WalletEncryptionService.Decrypt(encryptedPrivateKey, password);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[Keystore] Wallet unlocked.");
                Console.ForegroundColor = ConsoleColor.White;

                return new Wallet { PublicKey = publicKey, PrivateKey = privateKey };
            }
            catch
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[Keystore] Wrong password or corrupted wallet. Exiting.");
                Console.ForegroundColor = ConsoleColor.White;
                Environment.Exit(1);
                return null!;
            }
        }
    }
}
