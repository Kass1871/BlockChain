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

        public static Transaction CreateTransaction(string from, string to, decimal amount, decimal fee, string privateKey)
        {
            var tx = new Transaction(from, to, amount, fee);
            SignTransaction(tx, privateKey);
            var validTransaction = ValidateTransaction(tx);
            if (!validTransaction.IsValid)
            {
                throw new ValidationException(validTransaction.error);
            }
            return tx;
        }

        public static (bool IsValid, string error) ValidateTransaction(Transaction transaction)
        {
            if (transaction == null) return (false, "Transaction is null");
            if (string.IsNullOrEmpty(transaction.From)) return (false, "Sender address is required");
            if (string.IsNullOrEmpty(transaction.To)) return (false, "Recepient address is required");
            if (transaction.Amount <= 0) return (false, "Amount must be greater than zero");
            if (transaction.Signature == null || transaction.Signature.Length == 0) return (false, "Transaction must be signed.");
            if (!cryptoService.VerifySignature(transaction.ToRawString(), transaction.Signature, transaction.From)) return (false, "Invalid transaction signature");
            if (transaction.Fee < 0) return (false, "Transaction can not be negative.");

            return (true, string.Empty);
        }

        public static void SignTransaction(Transaction transaction, string privateKey)
        {
            var signature = cryptoService.SignData(transaction.ToRawString(), privateKey);
            transaction.Signature = signature;
        }
    }
}
