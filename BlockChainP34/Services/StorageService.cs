using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BlockChainP34.Models;
using BlockChainP34.Services;

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
