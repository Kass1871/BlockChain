using System.Text.Json;
using BlockChainP34.Models;
using System.Security.Cryptography;
using System.Text;

namespace BlockChainP34.Services
{
    public class StorageService
    {
        public void SaveStateSnapshot(List<Block> blockchain)
        {
            var jsonBalnaces = JsonSerializer.Serialize(blockchain);
            File.WriteAllText("state.json", jsonBalnaces);
        }
        public List<Block> LoadStateSnapshot()
        {
            if (File.Exists("state.json"))
            {
                var json = File.ReadAllText("state.json");
                return JsonSerializer.Deserialize<List<Block>>(json) ?? new List<Block>();
            }
            else
            {
                return new List<Block>();
            }
        }
        public void DeleteBlockChain()
        {
            if (File.Exists("state.json"))
            {
                File.Delete("state.json");
            }
            else
            {
                Console.WriteLine("No state file to delete.");
            }
        }
    }
}
