using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace BlockChainP34.Models
{
    public class SpvProof
    {
        public string TxId { get; set; }
        public string TxHash { get; set; }
        public List<string> ProofPath { get; set; }
        public string MerkleRoot { get; set; }
        public int BlockIndex { get; set; }
    }
}
