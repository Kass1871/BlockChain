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
        public TimeSpan TransactionTtl = TimeSpan.FromSeconds(60);

        Dictionary<string, Dictionary<string, decimal>> BalancesCash = new();

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
            if(tx.From != "MINT" && tx.Fee < NetworkBaseFee)
            {
                throw new Exception($"Transaction fee must be at least {NetworkBaseFee}.");
            }
            if(tx.TimeStamp > DateTime.UtcNow.AddMinutes(5)){
                throw new Exception("Transaction timestamp is too far in the future.");
            }

            if(tx.From != "MINT")
            {
                decimal tokenBalance = GetBalance(tx.From, tx.TokenSymbol);
                decimal mainBalance = GetBalance(tx.From, "MAIN");

                if(tx.TokenSymbol == "MAIN")
                {
                    if (tokenBalance < tx.Amount + tx.Fee) throw new Exception($"Insufficient MAIN balance. Available: {tokenBalance} -- Required: {tx.Amount + tx.Fee}");
                }
                else
                {
                    if (tokenBalance < tx.Amount) throw new Exception($"Insufficient {tx.TokenSymbol} balance. Available: {tokenBalance} -- Required: {tx.Amount}");
                    if (mainBalance < tx.Fee) throw new Exception($"Insufficient MAIN for fee. Available: {mainBalance} -- Required: {tx.Fee}");
                }
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
            if (newChain.Count == 0) return false;

            var genesis = newChain[0];
            if (genesis.Index != 0) return false;
            if (genesis.PreviousHash != "0") return false;
            if (genesis.Hash != _hashingService.ComputeHash(genesis)) return false;

            var tempBalances = new Dictionary<string, Dictionary<string, decimal>>();

            decimal GetTemp(string address, string token)
            {
                if (tempBalances.TryGetValue(address, out var tb) && tb.TryGetValue(token, out decimal b)) return b;
                return 0m;
            }
            void AddTemp(string address, string token, decimal delta)
            {
                if (!tempBalances.ContainsKey(address)) tempBalances[address] = new();
                if (!tempBalances[address].ContainsKey(token)) tempBalances[address][token] = 0m;
                tempBalances[address][token] += delta;
            }


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

                        File.AppendAllText("security_alerts.txt",
                            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\nATTACK DETECTED!\nTampered transaction at ID: {transaction.Id}\nAttacker tried to change amount to {transaction.Amount}" + Environment.NewLine);
                        return false;
                    }
                    if(transaction.From == "MINT")
                    {
                        AddTemp(transaction.To, transaction.TokenSymbol, transaction.Amount);
                        continue;
                    }

                    if(transaction.From == "COINBASE")
                    {
                        AddTemp(transaction.To, "MAIN", transaction.Amount);
                        continue;
                    }

                    string token = transaction.TokenSymbol;
                    decimal tokenBalance = GetTemp(transaction.From, token);
                    decimal mainBalance = GetTemp(transaction.From, "MAIN");
                    
                    if(token == "MAIN")
                    {
                        if(mainBalance < transaction.Amount + transaction.Fee)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"[CRITICAL] double spending detected in block #{currentBlock.Index} for {transaction.From}");
                            Console.ForegroundColor = ConsoleColor.White;
                            File.AppendAllText("security_alerts.txt",
                                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\nATTACK DETECTED!\nDouble spending for: {transaction.From}\n" + Environment.NewLine);
                            return false;
                        }
                    }
                    else
                    {
                        if(tokenBalance < transaction.Amount)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"[CRITICAL] insufficient {token} in block #{currentBlock.Index} for {transaction.From}");
                            Console.ForegroundColor = ConsoleColor.White;
                            File.AppendAllText("security_alerts.txt",
                                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\nATTACK DETECTED!\nInsufficient {token} for: {transaction.From}\n" + Environment.NewLine);
                            return false;
                        }
                        if(mainBalance < transaction.Fee)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"[CRITICAL] insufficient MAIN for fee in block #{currentBlock.Index} for {transaction.From}");
                            Console.ForegroundColor = ConsoleColor.White;
                            File.AppendAllText("security_alerts.txt",
                                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\nATTACK DETECTED!\nInsufficient MAIN fee for: {transaction.From}\n" + Environment.NewLine);
                            return false;
                        }
                    }

                    AddTemp(transaction.From, token, -transaction.Amount);
                    AddTemp(transaction.From, "MAIN", -transaction.Fee);
                    AddTemp(transaction.To, token, transaction.Amount);
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

        public decimal GetBalance(string publicKey, string token)
        {
            decimal balance = 0m;
            if(BalancesCash.TryGetValue(publicKey, out var tokenBalances) && tokenBalances.TryGetValue(token, out decimal confirmedBalance))
            {
                balance = confirmedBalance;
            }

            foreach (var ptx in PendingTransactions)
            {
                if (ptx.From == publicKey)
                {
                    if (ptx.TokenSymbol == token) balance -= ptx.Amount;
                    if (token == "MAIN") balance -= ptx.Fee;
                }
                if (ptx.To == publicKey && ptx.TokenSymbol == token) balance += ptx.Fee;
            }
            return balance;
        }
        public decimal GetBalanceOld(string publicKey)
        {
            decimal balance = 0m;
            foreach (var block in Chain)
            {
                foreach (var tx in block.Transactions)
                {
                    if (tx.From == publicKey)
                    {
                        balance -= tx.Amount + tx.Fee;
                    }
                    if (tx.To == publicKey)
                    {
                        balance += tx.Amount;
                    }
                }
            }
            return balance;
        }
        public decimal GetBalanceNew(string publicKey, string token = "MAIN")
        {
            if (BalancesCash.TryGetValue(publicKey, out var tokenBalances) && tokenBalances.TryGetValue(token, out decimal balance))
                return balance;
            return 0m;
        }

        public void UpdateBalances(Block block)
        {
            foreach (var tx in block.Transactions)
            {
                if(tx.From == "MINT")
                {
                    AddBalance(tx.To, tx.TokenSymbol, tx.Amount);
                    continue;
                }

                if(tx.From == "COINBASE")
                {
                    AddBalance(tx.To, tx.TokenSymbol, tx.Amount);
                    continue;

                }

                AddBalance(tx.From, tx.TokenSymbol, -tx.Amount);
                AddBalance(tx.From, "MAIN", -tx.Fee);
                AddBalance(tx.To, tx.TokenSymbol, tx.Amount);
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
            var balancesCashCopy = BalancesCash.ToDictionary(
                entry => entry.Key,
                entry => new Dictionary<string, decimal>(entry.Value)
                );

            BalancesCash.Clear();
            foreach(var block in Chain)
            {
                UpdateBalances(block);
            }
            foreach(var (address, oldTokenBalances) in balancesCashCopy)
            {
                foreach(var(token, oldBalance) in oldTokenBalances)
                {
                    decimal newBalance = BalancesCash.TryGetValue(address, out var newTb) && newTb.TryGetValue(token, out decimal nb) ? nb : 0m;

                    if(oldBalance > newBalance)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"[Balance Audit] Warning! User {address} lost {oldBalance - newBalance} {token} due to chain replacement!");
                        Console.ForegroundColor = ConsoleColor.White;
                    }
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
            foreach(var (address, tokenBalances) in BalancesCash)
            {
                if (address == "System") continue;
                foreach(var (token, balance) in tokenBalances)
                {
                    if(balance < 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"State validation failed! Negative {token} balance detected for address: {address}");
                        Console.ForegroundColor = ConsoleColor.White;
                        BalancesCash.Clear();
                        return false;
                    }
                }
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
        private void AddBalance(string address, string token, decimal delta)
        {
            if (!BalancesCash.TryGetValue(address, out var tokenBalances))
            {
                tokenBalances = new Dictionary<string, decimal>();
                BalancesCash[address] = tokenBalances;
            }

            if (!tokenBalances.ContainsKey(token))
            {
                tokenBalances[token] = 0m;
            }

            tokenBalances[token] += delta;
        }

        public Dictionary<string, decimal> GetAllTokenBalances(string publicKey)
        {
            var balances = BalancesCash.TryGetValue(publicKey, out var tokenBalances) ? new Dictionary<string, decimal>(tokenBalances) : new Dictionary<string, decimal>();
            return balances;
        }
    }
}
