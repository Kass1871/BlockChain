using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlockChainP34.Models
{
    public class Block
    {
        // Number of block in the chain
        public int Index {  get; set; }
        public string Author {  get; set; }
        // Time of block creation
        public DateTime Timestamp { get; set; }
        // Data stored in the block
        public string Data { get; set; }
        // Hash of the current block 
        public string Hash { get; set; }
        // Hash of the previous block in the chain
        public string PreviousHash { get; set; }
        public int Nonce { get; set; }
        public double MiningDurationSeconds { get; set; }
        public int DifficultyAtMining { get; set; }

        public Block(int index, string author, string data, string prevHash, DateTime timestamp, int difficultyAtMining )
        {
            Index = index;
            Author = author;
            Data = data;
            Timestamp = timestamp;
            PreviousHash = prevHash;
            Hash = "";
            DifficultyAtMining = difficultyAtMining;
        }
        public Block() { }
    }
}
