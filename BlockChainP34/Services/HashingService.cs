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
            var transactionData = string.Concat(block.Transactions.Select(t => t.ToRawString()).ToArray());
            string rawData = $"{block.Index}{block.Author}{block.Timestamp}{transactionData}{block.PreviousHash}{block.Nonce}";

            return ComputeHash(rawData);
        }

        private string ComputeHash(string rawData)
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(rawData);
            byte[] hashBytes = SHA256.HashData(inputBytes);

            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}
