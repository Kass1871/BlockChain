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
            public byte[] Signature { get; set; }
            public decimal Amount { get; set; }
            public decimal Fee { get; set; }
            public DateTime TimeStamp { get; set; } = DateTime.UtcNow;
            public int? LockTime { get; set; }
            public string TokenSymbol { get; set; } = "MAIN";
            public Transaction() { }
            public Transaction(string from, string to, decimal amount, decimal fee, int? locktime = null, string token = "MAIN")
            {
                Id = Guid.NewGuid().ToString();
                From = from;
                To = to;
                Amount = amount;
                Fee = fee;
                LockTime = locktime;
                TokenSymbol = token;
            }
            public string ToRawString()
            {
                return $"{From} pays {To} {Amount} {TokenSymbol} with {Fee} MAIN fee at {TimeStamp:O}";
            }
            public override string ToString()
            {
                return $"Transaction ID: {Id}\nFrom: {From}\nTo: {To}\nAmount: {Amount} ETH, Fee: {Fee}\nTimeStamp: {TimeStamp:O}\nLocktime: {LockTime ?? 0}";
            }
        }
    }
