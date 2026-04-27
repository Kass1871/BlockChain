using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlockChainP34.Services
{
    public class DisplaySerivce
    {
        public void DisplayBlockChain(List<Models.Block> chain)
        {
            foreach (var block in chain)
            {
                Console.WriteLine($"Index: {block.Index}");
                Console.WriteLine($"Author: {block.Author}");
                Console.WriteLine($"Timestamp: {block.Timestamp}");
                Console.WriteLine($"Hash: {block.Hash}");
                Console.WriteLine($"Previous Hash: {block.PreviousHash}");
                Console.WriteLine($"Nonce: {block.Nonce}");
                Console.WriteLine(new string('=', 50));

                var transactions = block.Transactions;
                foreach (var transaction in transactions) {
                    Console.WriteLine($"    Transaction Id: {transaction.Id.ToString()}");
                    Console.WriteLine($"    From: {transaction.From}");
                    Console.WriteLine($"    To: {transaction.To}");
                    Console.WriteLine($"    Amount: {transaction.Amount}");
                    Console.WriteLine($"    Timestamp: {transaction.TimeStamp}");
                    Console.WriteLine(new string('-', 50));
                }

            }
        }
    }
}
