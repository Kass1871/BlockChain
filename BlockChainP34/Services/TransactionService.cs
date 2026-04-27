using BlockChainP34.Models;
using System.ComponentModel.DataAnnotations;

namespace BlockChainP34.Services
{
    public static class TransactionService
    {
        public static Transaction CreateTransaction(string from, string to, decimal amount)
        {
            var tx = new Transaction(from, to, amount);
            var validTransaction = ValidateTransaction(tx);
            if (!validTransaction.IsValid)
            {
                throw new ValidationException(validTransaction.error);
            }
            return tx;
        }

        public static (bool IsValid, string error) ValidateTransaction(Transaction transaction)
        {
            if(transaction == null) return (false, "Transaction is null");
            if(string.IsNullOrEmpty(transaction.From)) return (false, "Sender address is required");
            if(string.IsNullOrEmpty(transaction.To)) return (false, "Recepient address is required");
            if(transaction.Amount <= 0) return (false, "Amount must be greater than zero");
            return (true, string.Empty);
        }
    }
}
