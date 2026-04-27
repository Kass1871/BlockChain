using BlockChainP34.Models;

namespace BlockChainP34.Services
{
    public class BlockchainExplorer
    {
        private List<Block> _chain;
        public BlockchainExplorer(List<Block> chain)
        {
            _chain = chain;
        }

        public decimal GetTotalVolume()
        {
            decimal total = 0m;
            foreach (var block in _chain)
            {
                total += block.Transactions.Sum(t => t.Amount);
            }
            return total;
        }

        public Transaction? GetLargestTransaction()
        {
            Transaction? largest = null;
            foreach (var block in _chain)
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
            foreach (var block in _chain)
            {
                foreach (var transaction in block.Transactions)
                {
                    if (transaction.From == address || transaction.To == address)
                    {
                        history.Add(transaction);
                    }
                }
            }
            return history.ToList();
        }
        public (Block? block, Transaction? tx) FindTransactionLocation(string txId)
        {
            Block? foundBlock = null;
            Transaction? foundTransaction = null;
            foreach (Block block in _chain)
            {
                foreach (Transaction transaction in block.Transactions)
                {
                    if (transaction.Id == txId)
                    {
                        foundTransaction = transaction;
                        foundBlock = block;
                        return (foundBlock, foundTransaction);
                    }
                }
            }
            return (null, null);
        }
    }
}
