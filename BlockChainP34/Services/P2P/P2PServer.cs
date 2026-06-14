using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using BlockChainP34.Models;

namespace BlockChainP34.Services.P2P
{
    public class P2PServer
    {
        private readonly BlockChainService blockChainService;
        private readonly P2PClient p2PClient;
        private readonly HashingService hashingService;
        public P2PServer(BlockChainService blockChainService, P2PClient p2PClient, HashingService hashingService)
        {
            this.blockChainService = blockChainService;
            this.p2PClient = p2PClient;
            this.hashingService = hashingService;
        }

        public void Start(int port)
        {
            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            Console.WriteLine($"P2P Server started on port: {port}");

            Task.Run(async () =>
            {
                while (true)
                {
                    var client = await listener.AcceptTcpClientAsync();
                    _ = HandleClientAsync(client);
                }
            });
        }
        private async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                await using var stream = client.GetStream();
                using var reader = new StreamReader(stream);

                var jsonLine = await reader.ReadLineAsync();
                if (!string.IsNullOrEmpty(jsonLine))
                {
                    var message = JsonSerializer.Deserialize<NetworkMessage>(jsonLine);
                    if (message != null)
                    {
                        if (message.type == "NEW_TRANSACTION")
                        {
                            var tx = JsonSerializer.Deserialize<Transaction>(message.data);

                            if (tx != null && !blockChainService.PendingTransactions.Any(t => t.Signature == tx.Signature))
                            {
                                try
                                {
                                    blockChainService.AddTransaction(tx);
                                    await p2PClient.BroadcastTransactionAsync(tx);
                                    Console.WriteLine($"[Gossip] Broadcasted transaction to other nodes... {tx.Id}");
                                }
                                catch (Exception ex)
                                {
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine($"Rejected incoming transaction: {tx.Id}: {ex.Message}");
                                    Console.ForegroundColor = ConsoleColor.White;
                                }
                            }
                        }
                        if (message.type == "REQUEST_CHAIN")
                        {
                            await p2PClient.BroadcastChainAsync(blockChainService.Chain);
                        }
                        if (message.type == "NEW_CHAIN")
                        {
                            var newChain = JsonSerializer.Deserialize<List<Block>>(message.data);
                            if (newChain != null && newChain.Count > blockChainService.Chain.Count)
                            {
                                blockChainService.ReplaceChain(newChain);
                            }
                        }
                        if (message.type == "NEW_BLOCK")
                        {
                            var newBlock = JsonSerializer.Deserialize<Block>(message.data);
                            if (newBlock != null)
                            {
                                var lastBlock = blockChainService.Chain.LastOrDefault();
                                if (lastBlock.Hash == newBlock.PreviousHash && hashingService.ComputeHash(newBlock) == newBlock.Hash)
                                {
                                    blockChainService.Chain.Add(newBlock);
                                    blockChainService.UpdateBalances(newBlock);

                                    var includeTxIds = newBlock.Transactions.Select(t => t.Id).ToHashSet();
                                    blockChainService.PendingTransactions.RemoveAll(t => includeTxIds.Contains(t.Id));
                                }
                            }
                        }
                        if (message.type == "REQUEST_MEMPOOL")
                        {
                            var jsonMempool = JsonSerializer.Serialize(blockChainService.PendingTransactions);
                            var responseMessage = JsonSerializer.Serialize(new NetworkMessage(type: "SYNC_MEMPOOL", data: jsonMempool));
                            await using var writer = new StreamWriter(stream, Encoding.UTF8, 1024, leaveOpen: true) { AutoFlush = true };
                            await writer.WriteLineAsync(responseMessage);

                            Console.WriteLine($"[Gossip] Sent mempool with {blockChainService.PendingTransactions.Count} transactions...");

                        }
                        if (message.type == "SYNC_MEMPOOL")
                        {
                            var sentTx = JsonSerializer.Deserialize<List<Transaction>>(message.data);
                            if (sentTx == null) return;

                            var existingTxIds = blockChainService.PendingTransactions.Select(t => t.Id).ToHashSet();

                            var txToInclude = new List<Transaction>();
                            foreach (var tx in sentTx)
                            {
                                if (existingTxIds.Contains(tx.Id)) continue;

                                var isValid = TransactionService.ValidateTransaction(tx).IsValid;
                                if (isValid)
                                {
                                    txToInclude.Add(tx);
                                    existingTxIds.Add(tx.Id);
                                    continue;
                                }
                                else
                                {
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine($"[Warning] Invalid transaction received in mempool sync: {tx.Id}");
                                    Console.ForegroundColor = ConsoleColor.White;
                                }
                            }
                            blockChainService.PendingTransactions.AddRange(txToInclude);
                        }
                        if(message.type == "REQUEST_SPV_PROOF")
                        {
                            var txId = message.data;
                            Block? targetBlock = null;
                            Transaction? targetTx = null;

                            foreach(var block in blockChainService.Chain)
                            {
                                var match = block.Transactions.FirstOrDefault(t => t.Id == txId);
                                if(match != null)
                                {
                                    targetBlock = block;
                                    targetTx = match;
                                    break;
                                }
                            }

                            string responseJson;
                            if(targetBlock != null && targetTx != null)
                            {
                                var spvProof = new SpvProof { TxId = txId, TxHash = hashingService.ComputeHash(targetTx.ToRawString()),
                                    ProofPath = hashingService.GetMerkleProof(targetBlock.Transactions, txId), MerkleRoot = targetBlock.MerkleRoot, BlockIndex = targetBlock.Index};
                                responseJson = JsonSerializer.Serialize(new NetworkMessage("SPV_RESULT", JsonSerializer.Serialize(spvProof)));
                            }
                            else
                            {
                                responseJson = JsonSerializer.Serialize(new NetworkMessage("SPV_NOT_FOUND", txId));
                            }

                            await using var spvWriter = new StreamWriter(stream, Encoding.UTF8, 1024, leaveOpen: true) { AutoFlush = true };
                            await spvWriter.WriteLineAsync(responseJson);
                        }
                        if(message.type == "REQUEST_MERKLE_ROOT")
                        {
                            var exists = blockChainService.Chain.Any(b => b.MerkleRoot == message.data);
                            var responseJson = JsonSerializer.Serialize(new NetworkMessage("MERKLE_ROOT_RESULT", exists.ToString()));

                            await using var writer = new StreamWriter(stream, Encoding.UTF8, 1024, leaveOpen: true) { AutoFlush = true };
                            await writer.WriteLineAsync(responseJson);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occured while handling client: {ex.Message}");
            }
            finally
            {
                client.Close();
            }
        }
    }
}
