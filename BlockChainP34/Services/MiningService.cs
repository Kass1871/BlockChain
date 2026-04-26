using BlockChainP34.Models;
using System.Diagnostics;

namespace BlockChainP34.Services
{
    internal class MiningService
    {
        private readonly HashingService hashing;

        public MiningService(HashingService hashingService)
        {
            hashing = hashingService;
        }

        public long MineBlock(Block block, int Difficulty) {
            var target = new String('0', Difficulty);

            var stopWatch = Stopwatch.StartNew();
            while (true)
            {
                block.Hash = hashing.ComputeHash(block);
                if (block.Nonce % 10000 == 0)
                {
                    Console.Write(".");
                }
                if (block.Hash.StartsWith(target))
                {
                    Console.WriteLine($"Block mined: {block.Hash} with nonce: {block.Nonce}");
                    stopWatch.Stop();
                    block.MiningDurationSeconds = stopWatch.Elapsed.TotalSeconds;
                    return block.Nonce;
                }
                block.Nonce++;
            }
        }
    }
}
