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
        public List<Transaction> PendingTransactions;
        public readonly int MaxTransactionsPerBlock = 10;
        public decimal NetworkBaseFee { get; set; } = 1.0m;

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
            var currentBalance = GetBalance(tx.From);
            var totalPendingAmount = PendingTransactions.Where(t => t.From == tx.From).Sum(t => t.Amount + t.Fee);
            var isValid = TransactionService.ValidateTransaction(tx);
            if(!isValid.IsValid)
            {
                throw new Exception($"Invalid transaction: {isValid.error}");
            }
            if(PendingTransactions.Any(t => t.Signature == tx.Signature))
            {
                throw new Exception("Transaction with the same Id already exists. Rejected.");
            }
            /*if (currentBalance < tx.Amount+tx.Fee)
            {
                throw new Exception("Insufficient balance to perform transaction.");
            }*/
            /*if (currentBalance - totalPendingAmount < tx.Amount + tx.Fee)
            {
                throw new Exception("Insufficient balance to perform transaction considering pending transactions.");
            }*/
            if(PendingTransactions.Count(t => t.From == tx.From) >= 3)
            {
                throw new Exception("Spam detected! Too many pending transactions from this address");
            }
            if(tx.Fee < NetworkBaseFee)
            {
                throw new Exception($"Transaction fee must be at least {NetworkBaseFee}.");
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
            var ptxToInclude = PendingTransactions.OrderByDescending(t => t.Fee).Take(MaxTransactionsPerBlock).ToList();
            var ptxToRemove = PendingTransactions.Where(t => t.TimeStamp < DateTime.UtcNow.AddSeconds(-5)).ToList();
            foreach(var ptx in ptxToRemove)
            {
                ptxToInclude.Remove(ptx);
            }

            var lastBlock = Chain.Last();
            var nextIndex = lastBlock.Index + 1;
            var halvings = nextIndex / halveRewardInterval;

            var reward = initialMiningReward / (decimal)Math.Pow(2, halvings);
            var totalTips = ptxToInclude.Sum(t => t.Fee - NetworkBaseFee);
            var totalReward = reward + totalTips;

            var rewardTransaction = new Transaction("COINBASE", minerAddress, totalReward, 0);
            ptxToInclude.Insert(0, rewardTransaction);

            var transactions = new List<Transaction>(ptxToInclude);
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

        public bool IsValid(List<Block> newChain)
        {
            if (newChain.Count == 0) return false;

            var genesis = newChain[0];
            if (genesis.Index != 0) return false;
            if (genesis.PreviousHash != "0") return false;
            if (genesis.Hash != _hashingService.ComputeHash(genesis)) return false;

            for (int i = 1; i < newChain.Count; i++)
            {
                var currentBlock = newChain[i];
                var previousBlock = newChain[i - 1];
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
                    BalancesCash[tx.From] -= tx.Amount + tx.Fee;
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
        public decimal GetTotalBurnedFees()
        {
            var totalBurnedFees = 0m;
            foreach(var block in Chain)
            {
                totalBurnedFees += block.Transactions.Where(x=>x.Fee>0).Sum(t => t.Fee - NetworkBaseFee);
            }
            return totalBurnedFees;
        }
        public decimal GetActualTotalSupply()
        {
            return GetTotalSupply() - GetTotalBurnedFees();
        }
        public void ReplaceChain(List<Block> newChain)
        {
            var chainCopy = new List<Block>(Chain);
            if (newChain.Count <= Chain.Count)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("New chain is not longer than the current chain.");
                Console.ForegroundColor = ConsoleColor.White;
                return;
            }
            if (!IsValid(newChain))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("New chain is invalid");
                Console.ForegroundColor = ConsoleColor.White;
                return;
            }
            if(newChain.Sum(x=>x.DifficultyAtMining) > Chain.Sum(x => x.DifficultyAtMining)) {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine($"[Audit] Our node was living in the past! We're off by {newChain.Count - Chain.Count} blocks.");
                Console.ForegroundColor = ConsoleColor.White;
                Chain = newChain;
            } else
            {
                 Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[Audit] New chain has more blocks but less total difficulty. This might be a sign of a potential attack or a network issue. Proceeding with caution.");
                Console.ForegroundColor = ConsoleColor.White;
                return;
            }
            var BalanceesCashCopy = BalancesCash.ToDictionary(entry => entry.Key, entry => entry.Value);

            BalancesCash.Clear();
            foreach(var block in Chain)
            {
                UpdateBalances(block);
            }
            foreach (var balance in BalanceesCashCopy)
            {
                var balanceOld = balance.Value;
                var balanceNew = BalancesCash.ContainsKey(balance.Key) ? BalancesCash[balance.Key] : 0;
                if (balanceOld > balanceNew)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[Balance Audit] Warning! User {balance.Key} suddenly lost {balanceOld - balanceNew} coins due to the network backup!");
                    Console.ForegroundColor = ConsoleColor.White;
                }
            }
            foreach(var block in chainCopy)
            {
                foreach(var tx in block.Transactions)
                {
                    if(!Chain.Any(b => b.Transactions.Any(t => t.Id == tx.Id)) && tx.From != "System")
                    {
                        Console.ForegroundColor= ConsoleColor.Red;
                        Console.WriteLine($"[Alarm] Transaction {tx.Id} was erased from history! Transaction canceled.");
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                }
            }

            var minedTxId = Chain.SelectMany(tr => tr.Transactions).Select(x => x.Id).ToHashSet();
            PendingTransactions.RemoveAll(tx => minedTxId.Contains(tx.Id));
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Chain replaced with a new longer and more difficult chain.");
            Console.ForegroundColor = ConsoleColor.White;
        }
    }
}
