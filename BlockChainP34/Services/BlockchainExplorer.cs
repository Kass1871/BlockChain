using BlockChainP34.Models;

namespace BlockChainP34.Services
{
    public class BlockchainExplorer
    {
        private readonly BlockChainService _blockChainService;
        public BlockchainExplorer(BlockChainService blockChainService)
        {
            _blockChainService = blockChainService;
        }

        public decimal GetTotalVolume()
        {
            decimal total = 0m;
            foreach (var block in _blockChainService.Chain)
            {
                total += block.Transactions.Sum(t => t.Amount);
            }
            return total;
        }

        public Transaction? GetLargestTransaction()
        {
            Transaction? largest = null;
            foreach (var block in _blockChainService.Chain)
            {
                foreach (var transaction in block.Transactions)
                {
                    if (largest == null || transaction.Amount > largest.Amount)
                    {
                        largest = transaction;
                    }
                }
            }
            return largest;
        }

        public List<Transaction> GetAddressHistory(string address)
        {
            List<Transaction> history = new List<Transaction>();
            foreach (var block in _blockChainService.Chain)
            {
                foreach (var transaction in block.Transactions)
                {
                    if (transaction.From == address || transaction.To == address)
                    {
                        history.Add(transaction);
                    }
                }
            }
            return history;
        }
        public (Block? block, Transaction? tx) FindTransactionLocation(string txId)
        {
            if (string.IsNullOrEmpty(txId))
            {
                return (null, null);
            }

            var chainMatch = _blockChainService.Chain.SelectMany(block => block.Transactions, (block, transaction) => new { block, transaction }).FirstOrDefault(bt => bt.transaction.Id == txId);

            if (chainMatch != null)
            {
                return (chainMatch.block, chainMatch.transaction);
            }

            var mempoolMatch = _blockChainService.PendingTransactions.FirstOrDefault(tx => tx.Id == txId);

            if (mempoolMatch != null)
            {
                return (null, mempoolMatch);
            }

            return (null, null);
        }
    }
}
