using BlockChainP34.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BlockChainP34.Services
{
    public class HashingService
    {
        public string ComputeHash(Block block)
        {
            string rawData = $"{block.Index}{block.Author}{block.Timestamp}{block.MerkleRoot}{block.PreviousHash}{block.Nonce}{block.DifficultyAtMining}";
            return ComputeHash(rawData);
        }
        public string ComputeHash(string rawData)
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(rawData);
            byte[] hashBytes = SHA256.HashData(inputBytes);

            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
        
        public string BuildMerkleRoot(List<Transaction> transactions)
        {
            if(transactions == null || transactions.Count == 0) return string.Empty;
            
            var hashAllTransactions = transactions.Select(t => ComputeHash(t.ToRawString())).ToList();

            while(hashAllTransactions.Count > 1)
            {
                var tempList = new List<string>();

                for (int i = 0; i < hashAllTransactions.Count; i += 2)
                {
                    if(i+1 < hashAllTransactions.Count)
                    {
                        string combinedHash = hashAllTransactions[i] + hashAllTransactions[i + 1];
                        tempList.Add(ComputeHash(combinedHash));
                    }
                    else
                    {
                        tempList.Add(hashAllTransactions[i]);
                    }
                }
                hashAllTransactions = tempList;
            }
            return hashAllTransactions[0];
        }

        public List<string> GetMerkleProof(List<Transaction> transactions, string txId)
        {
            var hashes = transactions.Select(t => ComputeHash(t.ToRawString())).ToList();
            var proof = new List<string>();

            int index = transactions.FindIndex(t => t.Id == txId);
            if (index == -1) return proof;

            while (hashes.Count > 1)
            {
                if(index % 2 == 0)
                {
                    if(index+1 < hashes.Count) proof.Add("R:" + hashes[index+1]);
                } else
                {
                    proof.Add("L:" + hashes[index-1]);
                }
                var nextLevelHashes = new List<string>();
                for(int i = 0; i < hashes.Count; i += 2)
                {
                    nextLevelHashes.Add(i + 1 < hashes.Count ? ComputeHash(hashes[i] + hashes[i + 1]) : hashes[i]);
                }

                index /= 2;
                hashes = nextLevelHashes;
            }

            return proof;
        }

        public bool VerifyMerkleProof(string txHash, List<string> proof, string expectedMerkleRoot)
        {
            var currentHash = txHash;
            foreach(var entry in proof)
            {
                if (entry.StartsWith("R:"))
                {
                    var sibling = entry[2..];
                    currentHash = ComputeHash(currentHash + sibling);
                }
                else if (entry.StartsWith("L:"))
                {
                    var sibling = entry[2..];
                    currentHash = ComputeHash(sibling + currentHash);
                }
            }
            return currentHash == expectedMerkleRoot;
        }
    }
}
