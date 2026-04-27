using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlockChainP34.Models
{
    public class Transaction
    {
        public string Id { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public decimal Amount { get; set; }
        public DateTime TimeStamp { get; set; } = DateTime.UtcNow;
        public Transaction(string from, string to, decimal amouunt)
        {
            Id = Guid.NewGuid().ToString();
            From = from;
            To = to;
            Amount = amouunt;
        }
        public string ToRawString()
        {
            return $"{From} pays {To} {Amount} ETH at {TimeStamp:O}";
        }
        public override string ToString()
        {
            return $"Transaction ID: {Id}, From: {From}, To: {To}, Amount: {Amount} ETH, TimeStamp: {TimeStamp:O}";
        }
    }
}
