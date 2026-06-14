using BlockChainP34.Models;
using System.Text.Json;

namespace BlockChainP34.Services
{
    public class ColdWalletService
    {
        private readonly CryptoService _cryptoService;

        public ColdWalletService(CryptoService cryptoService)
        {
            _cryptoService = cryptoService;
        }
        private static string Shorten(string? s, int max = 30)
        {
            if (string.IsNullOrEmpty(s)) return "<empty>";
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }

        public void GenerateOfflineTransaction(string from, string to, decimal amount, decimal fee, string privateKey, string filePath)
        {
            var tx = TransactionService.CreateTransaction(from, to, amount, fee, privateKey);
            var json = JsonSerializer.Serialize(tx);
            if (string.IsNullOrWhiteSpace(filePath)) filePath = "offline_tx.json";
            try
            {
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(filePath, json);

            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[Cold Wallet] Failed to save transaction file: {ex.Message}");
                Console.ForegroundColor = ConsoleColor.White;
                return;
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[Cold Wallet] Transaction signed and saved to: {filePath}");
            Console.WriteLine($"[Cold Wallet] ID:     {tx.Id}");
            Console.WriteLine($"[Cold Wallet] From:   {Shorten(from)}");
            Console.WriteLine($"[Cold Wallet] To:     {Shorten(to)}");
            Console.WriteLine($"[Cold Wallet] Amount: {amount}  Fee: {fee}");
            Console.ForegroundColor = ConsoleColor.White;
        }
    }
}