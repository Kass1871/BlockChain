using BlockChainP34.Models;
using System.ComponentModel.DataAnnotations;

namespace BlockChainP34.Services
{
    public static class TransactionService
    {
        private static readonly CryptoService cryptoService;
        static TransactionService()
        {
            cryptoService = new CryptoService();
        }

        public static Transaction CreateTransaction(string from, string to, decimal amount, decimal fee, string privateKey, int? lockTime = null, string token = "MAIN")
        {
            var tx = new Transaction(from, to, amount, fee, lockTime);
            tx.TokenSymbol = token ?? "MAIN";
            SignTransaction(tx, privateKey);
            var validTransaction = ValidateTransaction(tx);
            if (!validTransaction.IsValid)
            {
                throw new ValidationException(validTransaction.error);
            }
            return tx;
        }

        public static Transaction CreateMintTransaction(string to, decimal amount, string token)
        {
            return new Transaction("MINT", to, amount, 0m, null, token);
        }

        public static (bool IsValid, string error) ValidateTransaction(Transaction transaction)
        {
            if (transaction == null) return (false, "Transaction is null");
            if (string.IsNullOrEmpty(transaction.From)) return (false, "Sender address is required");
            if (string.IsNullOrEmpty(transaction.To)) return (false, "Recepient address is required");
            if (transaction.Amount <= 0) return (false, "Amount must be greater than zero");
            if (transaction.From != "COINBASE" && transaction.From != "MINT")
            {
                if (transaction.Signature == null || transaction.Signature.Length == 0) return (false, "Transaction must be signed.");
                if (!cryptoService.VerifySignature(transaction.ToRawString(), transaction.Signature, transaction.From)) return (false, "Invalid transaction signature");
            }
            if (transaction.From != "COINBASE" && transaction.From != "MINT" && transaction.Fee < 0) return (false, "Transaction can not be negative.");
            if (string.IsNullOrEmpty(transaction.TokenSymbol)) return (false, "Transaction must have a token.");

            return (true, string.Empty);
        }

        public static void SignTransaction(Transaction transaction, string privateKey)
        {
            var signature = cryptoService.SignData(transaction.ToRawString(), privateKey);
            transaction.Signature = signature;
        }
    }
}
