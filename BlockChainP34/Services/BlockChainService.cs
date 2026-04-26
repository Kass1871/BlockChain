using BlockChainP34.Models;

namespace BlockChainP34.Services
{
    public class BlockChainService
    {
        public List<Block> Chain { get; set; }
        private HashingService _hashingService;
        private MiningService _miningService;

        private readonly double _targetBlockTimeSeconds = 1;
        private readonly int _difficultyAdjustmentInterval = 3;

        public int Difficulty = 1;

        public BlockChainService(int Difficulty)
        {
            _hashingService = new HashingService();
            _miningService = new MiningService(_hashingService);
            Chain = new List<Block>();
            this.Difficulty = Difficulty;
            AddGenesisBlock();
        }

        private void AddGenesisBlock()
        {
            var block = new Block(0, "System", "Genesis Block", "0", DateTime.Parse("2026-06-01T00:00:00Z"), Difficulty);
            block.Hash = _hashingService.ComputeHash(block);
            Chain.Add(block);
        }

        public void AddBlock(string data, string author)
        {
            var lastBlock = Chain.Last();
            var newBlock = new Block(lastBlock.Index + 1, author, data, lastBlock.Hash, DateTime.UtcNow, Difficulty);
            newBlock.Hash = _hashingService.ComputeHash(newBlock);
            _miningService.MineBlock(newBlock, Difficulty);
            Chain.Add(newBlock);

            if(newBlock.Index % _difficultyAdjustmentInterval == 0)
            {
                AdjustDifficulty();
            }
        }

        private void AdjustDifficulty()
        {
            var recentBlocks = Chain.Skip(Chain.Count - _difficultyAdjustmentInterval).Take(_difficultyAdjustmentInterval).ToList();

            var totalMiningTime = recentBlocks.Sum(b => b.MiningDurationSeconds);
            var averageMiningTime = totalMiningTime / _difficultyAdjustmentInterval;

            if(averageMiningTime < _targetBlockTimeSeconds / 5)
            {
                Difficulty += 2;
                Console.WriteLine($"Significantly increasing difficulty to {Difficulty}. Average mining time: {averageMiningTime:F2} seconds.");
            }
            if(averageMiningTime < _targetBlockTimeSeconds)
            {
                Difficulty++;
                Console.WriteLine($"Increasing difficulty to {Difficulty}. Average mining time: {averageMiningTime:F2} seconds.");
            }
            else if(averageMiningTime > _targetBlockTimeSeconds * 5)
            {
                Difficulty -= 2;
                Console.WriteLine($"Significantly decreasing difficulty to {Difficulty}. Average mining time: {averageMiningTime:F2} seconds.");
            }
            else if(averageMiningTime > _targetBlockTimeSeconds)
            {
                Difficulty = Math.Max(1, Difficulty - 1);
                Console.WriteLine($"Decreasing difficulty to {Difficulty}. Average mining time: {averageMiningTime:F2} seconds.");
            }
            else
            {
                Console.WriteLine($"Difficulty remains at {Difficulty}. Average mining time: {averageMiningTime:F2} seconds.");
            }
        }

        public string PrintDifficultyHistoy()
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
                if (!currentBlock.Hash.StartsWith(new String('0', Difficulty)))
                    return false;
            }
            return true;
        }

        public string AnalyzeChain()
        {
            if (Chain.Count == 0) return "Genesis block!";

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
                if(currentBlock.Hash.StartsWith(new String('0', Difficulty)) != true)
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
    }
}
