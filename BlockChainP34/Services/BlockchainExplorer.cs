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

        public decimal GetTotalVolumeWithoutFees()
        {
            decimal total = _blockChainService.Chain.SelectMany(b => b.Transactions).Sum(t => t.Amount);
            return total;
        }

        public Transaction? GetLargestTransaction()
        {
            Transaction? largest = _blockChainService.Chain.SelectMany(b => b.Transactions).MaxBy(t => t.Amount);
            return largest;
        }

        public List<Transaction> GetTransactionHistory(string address)
        {
            var history = _blockChainService.Chain.SelectMany(b => b.Transactions).Where(t => t.From == address || t.To == address).OrderByDescending(t => t.TimeStamp).ToList();
            if (history == null) return null;

            return history;
        }
        public (Block? block, Transaction? tx) FindTransactionById(string txId)
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
        public Block? FindBlockByTransactionId(string txId)
        {
            if (string.IsNullOrEmpty(txId))
            {
                return null;
            }

            var match = _blockChainService.Chain.FirstOrDefault(b => b.Transactions.Any(t => t.Id == txId));

            if(match != null)
            {
                return match;
            }

            return null;
        }
        public decimal GetTotalFeesEarned(string minerAddress)
        {
            var total = _blockChainService.Chain.Where(b => b.Author == minerAddress).SelectMany(b => b.Transactions).Where(t => t.From != "COINBASE").Sum(t => t.Fee);
            return total;
        }
    }
}
