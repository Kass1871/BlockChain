using System.Text.Json;
using System.IO;
using BlockChainP34.Models;

namespace BlockChainP34.Services
{
    public class BlockChainService
    {
        public List<Block> Chain { get; set; }
        private HashingService _hashingService;
        private MiningService _miningService;
        private readonly StorageService _storageService;
        private decimal initialMiningReward = 50;
        private int halveRewardInterval = 5;
        public List<Transaction> PendingTransactions;
        public readonly int MaxTransactionsPerBlock = 10;
        public decimal NetworkBaseFee { get; set; } = 1.0m;
        public TimeSpan TransactionTtl = TimeSpan.FromSeconds(5);

        public Dictionary<string, decimal> BalancesCash = new Dictionary<string, decimal>();

        private readonly double _targetBlockTimeSeconds = 1;
        private readonly int _difficultyAdjustmentInterval = 3;

        public int Difficulty = 1;

        public BlockChainService(StorageService storageService, HashingService hashingService, MiningService miningService)
        {
            _hashingService = hashingService;
            _miningService = miningService;
            Chain = new List<Block>();
            PendingTransactions = new List<Transaction>();
            AddGenesisBlock();
            _storageService = storageService;

            var loadedChain = _storageService.LoadStateSnapshot();
            if(loadedChain != null && loadedChain.Count > 0)
            {
                if (IsValid(loadedChain))
                {
                    Chain = loadedChain;
                    RebuildState();
                    Console.WriteLine("Loaded blockchain state from snapshot.");
                }
                else
                {
                    Console.WriteLine("Loaded blockchain state is invalid. Starting with a new chain.");
                }
            }
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
            if(PendingTransactions.Count(t => t.From == tx.From) >= 3)
            {
                throw new InvalidOperationException($"Spam detected! Too many pending transactions from this address: {tx.From}");
            }
            if(tx.Fee < NetworkBaseFee)
            {
                throw new Exception($"Transaction fee must be at least {NetworkBaseFee}.");
            }
            if(tx.TimeStamp > DateTime.UtcNow.AddMinutes(5)){
                throw new Exception("Transaction timestamp is too far in the future.");
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
            EvictStaleTransactions();
            var ptxToInclude = PendingTransactions.Where(t => !t.LockTime.HasValue || t.LockTime.Value <= Chain.Count).OrderByDescending(t => t.Amount).ThenByDescending(t => t.Fee).Take(MaxTransactionsPerBlock).ToList();
            var includedIds = ptxToInclude.Select(t => t.Id).ToHashSet();

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
            newBlock.MerkleRoot = _hashingService.BuildMerkleRoot(transactions);

            _miningService.MineBlock(newBlock, Difficulty);

            Chain.Add(newBlock);
            UpdateBalances(newBlock);
            PendingTransactions.RemoveAll(t => includedIds.Contains(t.Id));

            if (newBlock.Index % _difficultyAdjustmentInterval == 0)
            {
                AdjustDifficulty();
            }

            _storageService.SaveStateSnapshot(Chain);
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
            var tempBalances = new Dictionary<string, decimal>();
            if (newChain.Count == 0) return false;

            var genesis = newChain[0];
            if (genesis.Index != 0) return false;
            if (genesis.PreviousHash != "0") return false;
            if (genesis.Hash != _hashingService.ComputeHash(genesis)) return false;

            for (int i = 1; i < newChain.Count; i++)
            {
                // POW - validate hash, previous hash link and difficulty requirement
                var currentBlock = newChain[i];
                var previousBlock = newChain[i - 1];
                if (currentBlock.Hash != _hashingService.ComputeHash(currentBlock)) return false;
                if (currentBlock.PreviousHash != previousBlock.Hash) return false;
                if (!currentBlock.Hash.StartsWith(new String('0', currentBlock.DifficultyAtMining))) return false;

                // Validate transactions
                foreach(var transaction in currentBlock.Transactions)
                {
                    var validateResult = TransactionService.ValidateTransaction(transaction);
                    if (!validateResult.IsValid)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[CRITICAL] tampered transaction detected in block #{currentBlock.Index}: {validateResult.error}");
                        Console.ForegroundColor = ConsoleColor.White;

                        File.AppendAllText("security_alerts.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\nATTACK DETECTED!\nTampered transaction at ID: {transaction.Id}\nAttacker tried to change amount to {transaction.Amount}" + Environment.NewLine);
                        return false;
                    }
                    if(transaction.From != "COINBASE")
                    {
                        decimal senderBalance = tempBalances.ContainsKey(transaction.From) ? tempBalances[transaction.From] : 0;
                        if(senderBalance < transaction.Amount + transaction.Fee)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"[CRITICAL] double spending or balance tampering detected in block #{currentBlock.Index} for address {transaction.From}");
                            Console.ForegroundColor = ConsoleColor.White;
                            File.AppendAllText("security_alerts.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\nATTACK DETECTED!\nDouble spending or balance tampering for address: {transaction.From}\nAttempted amount: {transaction.Amount + transaction.Fee}" + Environment.NewLine);
                            return false;
                        }
                        tempBalances[transaction.From] = senderBalance - transaction.Amount - transaction.Fee;
                    }
                    if (!tempBalances.ContainsKey(transaction.From))
                    {
                        tempBalances[transaction.From] = 0;
                    }
                    if (!tempBalances.ContainsKey(transaction.To))
                    {
                        tempBalances[transaction.To] = 0;
                    }
                    tempBalances[transaction.To] += transaction.Fee;
                }
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
                    balance -= ptx.Amount + ptx.Fee;
                }
                if (ptx.To == publicKey)
                {
                    balance += ptx.Amount;
                }
            }
            return balance;
        }

        public void UpdateBalances(Block block)
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

                var orphanTransactions = new List<Transaction>();
                var forkPoint = chainCopy.Count();

                for(int i = 0; i < chainCopy.Count; i++)
                {
                    if (chainCopy[i].Hash == newChain[i].Hash) continue;
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Blue;
                        Console.WriteLine($"[Audit] Hash mismatch at index #{i} - fork point found.");
                        Console.ForegroundColor = ConsoleColor.White;
                        forkPoint = i;
                        break;
                    }
                }
                if(forkPoint == chainCopy.Count())
                {
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine("[Audit] No fork point found, new chain extends the old one.");
                    Console.ForegroundColor = ConsoleColor.White;
                }
                for( int i = forkPoint; i < chainCopy.Count; i++)
                {
                    var txToInc = chainCopy[i].Transactions.Where(t => t.From != "COINBASE" && t.From != "System").ToList();
                    orphanTransactions.AddRange(txToInc);
                }
                var newChainTxIds = newChain.SelectMany(tr => tr.Transactions).Select(x => x.Id).ToHashSet();
                var existingPendingIds = PendingTransactions.Select(t => t.Id).ToHashSet();

                int rescuedTx = 0;
                foreach (var orphanTx in orphanTransactions)
                {
                    if(!newChainTxIds.Contains(orphanTx.Id) && !existingPendingIds.Contains(orphanTx.Id))
                    {
                        PendingTransactions.Add(orphanTx);
                        existingPendingIds.Add(orphanTx.Id);
                        rescuedTx++;
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"[Phoenix] Rescued orphan transaction {orphanTx.Id} from {orphanTx.From}.");
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                }

                if(rescuedTx > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"[Phoenix] Rescued {rescuedTx} transactions in total.");
                    Console.ForegroundColor = ConsoleColor.White;
                }
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
        public decimal GetBalanceOld(string publicKey)
        {
            decimal balance = 0m;
            foreach(var block in Chain)
            {
                foreach(var tx in block.Transactions)
                {
                    if(tx.From == publicKey)
                    {
                        balance -= tx.Amount + tx.Fee;
                    }
                    if(tx.To == publicKey)
                    {
                        balance += tx.Amount;
                    }
                }
            }
            return balance;
        }
        public decimal GetBalanceNew(string publicKey)
        {
            return BalancesCash.TryGetValue(publicKey, out decimal balance) ? balance : 0m;
        }
        public class AuditReport
        {
            public bool IsChainValid { get; set; }
            public List<int> CompromisedBlockIndexes { get; set; } = new();
            public Dictionary<int, List<string>> ViolationDetails { get; set; } = new();
            // ViolationDetails: ключ = індекс блоку, значення = список рядків з описом порушень
        }
        public AuditReport RunFullAudit(List<Block> chain)
        {
            if(chain == null || chain.Count == 0)
            {
                return new AuditReport { IsChainValid = false };
            }

            var report = new AuditReport();
            foreach(var block in chain)
            {
                if(!report.ViolationDetails.ContainsKey(block.Index))report.ViolationDetails[block.Index] = new List<string>();

                if (block.Index > 0)
                {
                    var previousBlock = chain.FirstOrDefault(b => b.Hash == block.PreviousHash);
                    if (block.Hash == null || block.PreviousHash != previousBlock.Hash)
                    {
                        report.IsChainValid = false;
                        if (!report.CompromisedBlockIndexes.Contains(block.Index)) report.CompromisedBlockIndexes.Add(block.Index);
                        report.ViolationDetails[block.Index].Add("Previous hash mismatch - chain link broken");
                    }
                    var allTransactions = block.Transactions;
                    if (block.MerkleRoot != _hashingService.BuildMerkleRoot(allTransactions))
                    {
                        report.IsChainValid = false;
                        if (!report.CompromisedBlockIndexes.Contains(block.Index)) report.CompromisedBlockIndexes.Add(block.Index);
                        report.ViolationDetails[block.Index].Add("Merkle root mismatch - transactions tampered");
                    }
                    if (!block.Hash.StartsWith(new String('0', block.DifficultyAtMining)))
                    {
                        report.IsChainValid = false;
                        if (!report.CompromisedBlockIndexes.Contains(block.Index)) report.CompromisedBlockIndexes.Add(block.Index);
                        report.ViolationDetails[block.Index].Add("Hash does not meet difficulty requirement - possible tampering with nonce or mining duration");
                    }
                }
            }
            report.IsChainValid = report.CompromisedBlockIndexes.Count == 0;
            return report;
        }

        public Block FindAttackOrigin(AuditReport report, List<Block> chain)
        {
            foreach(var block in chain.OrderBy(b => b.Index))
            {
                if(!report.ViolationDetails.TryGetValue(block.Index, out List<string> violations)) continue;

                bool hasNonHashViolation = violations.Any(v => v.Contains("Merkle root mismatch") || v.Contains("Hash does not meet difficulty requirement"));
                if (hasNonHashViolation) return block;
            }
            return null;
        }

        public string GenerateForensicReport(AuditReport report, Block attackOrigin)
        {
            var forensicReport = new System.Text.StringBuilder();

            forensicReport.AppendLine("=== FORENSIC REPORT ===");
            forensicReport.AppendLine($"Chain status: {(report.IsChainValid ? "VALID" : "COMPROMISED")}");
            forensicReport.AppendLine($"Attack origin: Block #{attackOrigin.Index} (timestamp: {attackOrigin.Timestamp})");
            forensicReport.AppendLine($"Total affected blocks: {report.CompromisedBlockIndexes.Count}");
            forensicReport.AppendLine();
            forensicReport.AppendLine("VIOLATION LOG:");
            foreach(var block in report.CompromisedBlockIndexes)
            {
                forensicReport.AppendLine($"[Block #{block}]");
                if (report.ViolationDetails.TryGetValue(block, out List<string> violations))
                {
                    foreach (var violation in violations)
                    {
                        forensicReport.Append($" - {violation}");
                    }
                }
            }
            return forensicReport.ToString();
        }

        public bool ValidateAndRebuildState()
        {
            BalancesCash.Clear();
            foreach (var block in Chain)
            {
                UpdateBalances(block);
            }
            if(BalancesCash.Any(kv => kv.Key != "System" && kv.Value < 0))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("State validation failed! Negative balance detected for address: " + BalancesCash.First(kv => kv.Key != "System" && kv.Value < 0).Key);
                Console.ForegroundColor = ConsoleColor.White;
                BalancesCash.Clear();
                return false;
            }
            return true;
        }

        public int EvictStaleTransactions()
        {
            var now = DateTime.UtcNow;
            var removedCount = PendingTransactions.RemoveAll(tx => now - tx.TimeStamp > TransactionTtl);
            if (removedCount > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[TTL] Evicted {removedCount} stale transactions from the mempool.");
                Console.ForegroundColor = ConsoleColor.White;
            }
            return removedCount;
            
        }
    }
}
