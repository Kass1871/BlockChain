using BlockChainP34.Models;

namespace BlockChainP34.Services
{
    internal class MiningService
    {
        private readonly HashingService hashing;

        public MiningService(HashingService hashingService)
        {
            hashing = hashingService;
        }

        public long MineBlock(Block block, string Prefix) {
            var target = Prefix.ToLower();

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
                    return block.Nonce;
                }
                block.Nonce++;
            }
        }
    }
}
