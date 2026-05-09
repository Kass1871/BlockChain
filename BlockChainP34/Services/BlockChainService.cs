using BlockChainP34.Models;

namespace BlockChainP34.Services
{
    public class BlockChainService
    {
        public List<Block> Chain { get; set; }
        private HashingService _hashingService;
        private MiningService _miningService;
        private decimal initialMiningReward = 50;
        private int halveRewardInterval = 5;
        private List<Transaction> PendingTransactions;

        public readonly Dictionary<string, decimal> BalancesCash = new Dictionary<string, decimal>();

        private readonly double _targetBlockTimeSeconds = 1;
        private readonly int _difficultyAdjustmentInterval = 3;

        public int Difficulty = 1;

        public BlockChainService(int Difficulty)
        {
            _hashingService = new HashingService();
            _miningService = new MiningService(_hashingService);
            Chain = new List<Block>();
            PendingTransactions = new List<Transaction>();
            this.Difficulty = Difficulty;
            AddGenesisBlock();
        }

        public void AddTransaction(Transaction tx)
        {
            var isValid = TransactionService.ValidateTransaction(tx);
            if(!isValid.IsValid)
            {
                throw new Exception($"Invalid transaction: {isValid.error}");
            }
            if(PendingTransactions.Any(t => t.Signature == tx.Signature))
            {
                throw new Exception("Transaction with the same Id already exists. Rejected.");
            }
            if (GetBalance(tx.From) < tx.Amount)
            {
                throw new Exception("Insufficient balance to perform transaction.");
            }
            PendingTransactions.Add(tx);
        }

        private void AddGenesisBlock()
        {
            var block = new Block(0, "System", new List<Transaction>(), "0", DateTime.Parse("2026-06-01T00:00:00Z"), Difficulty);
            _miningService.MineBlock(block, Difficulty);
            Chain.Add(block);
        }

        public void MineBlock(string minerAddress)
        {
            foreach (var tx in PendingTransactions)
            {
                var isValid = TransactionService.ValidateTransaction(tx);
                if (!isValid.IsValid)
                {
                    throw new Exception("Invalid transaction in pending transactions. Mining aborted. Error: " + isValid.error);
                }
            }

            var lastBlock = Chain.Last();
            var nextIndex = lastBlock.Index + 1;
            var halvings = nextIndex / halveRewardInterval;

            var reward = initialMiningReward / (decimal)Math.Pow(2, halvings);
            var rewardTransaction = new Transaction("COINBASE", minerAddress, reward);

            var transactions = new List<Transaction>(PendingTransactions) { rewardTransaction };
            var newBlock = new Block(nextIndex, minerAddress, transactions, lastBlock.Hash, DateTime.UtcNow, Difficulty);

            _miningService.MineBlock(newBlock, Difficulty);

            Chain.Add(newBlock);
            UpdateBalances(newBlock);
            PendingTransactions.Clear();

            if (newBlock.Index % _difficultyAdjustmentInterval == 0)
            {
                AdjustDifficulty();
            }
        }

        private void AdjustDifficulty()
        {
            var recentBlocks = Chain.Skip(Chain.Count - _difficultyAdjustmentInterval).Take(_difficultyAdjustmentInterval).ToList();

            var totalMiningTime = recentBlocks.Sum(b => b.MiningDurationSeconds);
            var averageMiningTime = totalMiningTime / _difficultyAdjustmentInterval;

            int change = 0;
            if(averageMiningTime < _targetBlockTimeSeconds)
            {
                if(averageMiningTime < _targetBlockTimeSeconds / 5)
                {
                    change += 2;
                }
                else
                {
                    change++;
                }
            }
            else if(averageMiningTime > _targetBlockTimeSeconds)
            {
                if (averageMiningTime > _targetBlockTimeSeconds * 5)
                {
                    change -= 2;
                }
                else
                {
                    change--;
                }
            }
            else
            {
                Console.WriteLine($"Difficulty remains at {Difficulty}. Average mining time: {averageMiningTime:F2} seconds.");
            }
            int oldDifficulty = Difficulty;
            Difficulty = Math.Clamp(Difficulty + change, 1, 6);

            var changeCheck = Difficulty - oldDifficulty;
            if (changeCheck == 2)
            {
                Console.WriteLine($"Significantly increasing difficulty to {Difficulty}. Average mining time: {averageMiningTime:F2} seconds.");
            }
            else if (changeCheck == 1)
            {
                Console.WriteLine($"Increasing difficulty to {Difficulty}. Average mining time: {averageMiningTime:F2} seconds.");
            }
            else if (changeCheck == -1)
            {
                Console.WriteLine($"Decreasing difficulty to {Difficulty}. Average mining time: {averageMiningTime:F2} seconds.");
            }
            else if (changeCheck == -2)
            {
                Console.WriteLine($"Significantly decreasing difficulty to {Difficulty}. Average mining time: {averageMiningTime:F2} seconds.");
            }
            else
            {
                Console.WriteLine($"Difficulty hit a boundary and remains at {Difficulty}. Average mining time: {averageMiningTime:F2} seconds.");
            }
        }

        //Difficulty = Math.Clamp(Difficulty + change, 1, 6);

        public string PrintDifficultyHistory()
        {
            Console.WriteLine("Block Difficulty History:");
            for (int i = 1; i < Chain.Count; i++)
            {
                var currentBlock = Chain[i];

                Console.WriteLine($"Block #{currentBlock.Index} - Difficulty at mining: {currentBlock.DifficultyAtMining} - Mining duration: {currentBlock.MiningDurationSeconds:F2} seconds");
            }
            return new string('=', 50);
        }

        public bool IsValid()
        {
            if (Chain.Count == 0) return false;

            var genesis = Chain[0];
            if (genesis.Index != 0) return false;
            if (genesis.PreviousHash != "0") return false;
            if (genesis.Hash != _hashingService.ComputeHash(genesis)) return false;

            for (int i = 1; i < Chain.Count; i++)
            {
                var currentBlock = Chain[i];
                var previousBlock = Chain[i - 1];
                if (currentBlock.Hash != _hashingService.ComputeHash(currentBlock))
                    return false;
                if (currentBlock.PreviousHash != previousBlock.Hash)
                    return false;
                if (!currentBlock.Hash.StartsWith(new String('0', currentBlock.DifficultyAtMining)))
                    return false;
            }
            return true;
        }

        public string AnalyzeChain()
        {
            if (Chain.Count == 0) return "Chain is empty.";

            var genesis = Chain[0];
            if (genesis.Index != 0) return "Genesis block is invalid.";
            if (genesis.PreviousHash != "0") return "Genesis block is invalid.";
            if (genesis.Hash != _hashingService.ComputeHash(genesis)) return "Genesis block is invalid.";


            var hasError = false;
            Console.ForegroundColor = ConsoleColor.Red;
            for (int i = 1; i < Chain.Count; i++)
            {
                var currentBlock = Chain[i];
                var previousBlock = Chain[i - 1];

                if (_hashingService.ComputeHash(currentBlock) != currentBlock.Hash)
                {
                    var error = $"Error at block #{currentBlock.Index}: Hash doesn't match block's data. (Data/timestamp/Nonce tampered)\n{new string('-', 50)}";
                    Console.WriteLine(error);
                    hasError = true;
                }
                if(currentBlock.Hash.StartsWith(new String('0', currentBlock.DifficultyAtMining)) != true)
                {
                    var error = $"Error at block #{currentBlock.Index}: Hash doesn't meet the required difficulty.\n{new string('-', 50)}";
                    Console.WriteLine(error);
                    hasError = true;
                }
                if(currentBlock.PreviousHash != previousBlock.Hash)
                {
                    var error = $"Error at block #{currentBlock.Index}: Chain link broken. Previous Hash doesn't match the hash of the previous block.\n{new string('-', 50)}";
                    Console.WriteLine(error);
                    hasError = true;
                }
            }
            if (hasError) return $"Chain is invalid. Errors can be seen above.\n";
            Console.ForegroundColor = ConsoleColor.White;
            return $"Chain is valid with {Chain.Count} blocks. Last block hash: {Chain.Last().Hash}";
        }

        public decimal GetBalance(string publicKey)
        {
            var balance = 0m;
            if(BalancesCash.TryGetValue(publicKey, out decimal confirmedBalance))
            {
                balance = confirmedBalance;
            }

            foreach (var ptx in PendingTransactions)
            {
                if (ptx.From == publicKey)
                {
                    balance -= ptx.Amount;
                }
                if (ptx.To == publicKey)
                {
                    balance += ptx.Amount;
                }
            }
            return balance;
        }

        private void UpdateBalances(Block block)
        {
            foreach (var tx in block.Transactions)
            {
                if(tx.From != "COINBASE")
                {
                    if (!BalancesCash.ContainsKey(tx.From))
                    {
                        BalancesCash[tx.From] = 0;
                    }
                    BalancesCash[tx.From] -= tx.Amount;
                }
                if(!BalancesCash.ContainsKey(tx.To))
                {
                    BalancesCash[tx.To] = 0;
                }
                BalancesCash[tx.To] += tx.Amount;
            }
        }

        public void RebuildState()
        {
            BalancesCash.Clear();
            foreach (var block in Chain)
            {
                UpdateBalances(block);
            }
        }

        public decimal GetTotalSupply()
        {
            decimal totalSupply = 0;
            foreach(var block in Chain)
            {
                foreach (var transaction in block.Transactions)
                {
                    if (transaction.From == "COINBASE")
                    {
                        totalSupply += transaction.Amount;
                    }
                }
            }
            return totalSupply;
        }
    }
}
