using BlockChainP34.Models;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;

namespace BlockChainP34.Services.P2P;

public class P2PClient
{
    private readonly BlockChainService? blockChainService;
    private readonly HashingService hashingService;
    public P2PClient(BlockChainService? blockChainService, HashingService hashingService)
    {
        this.blockChainService = blockChainService;
        this.hashingService = hashingService;
    }

    private readonly List<string> _peers = new List<string>(); // peerAddress -> lastSeen
    private readonly object _peersLock = new object();
    private readonly string PeersFilePath = Path.Combine(Directory.GetCurrentDirectory(), "peers.json");

    public async Task InitializeAsync()
    {
        var loaded = await LoadPeersAsync();
        lock (_peersLock)
        {
            foreach(var p in loaded)
            {
                if(!_peers.Contains(p))
                {
                    _peers.Add(p);
                    Console.WriteLine($"Loaded peer from file: {p}");
                }
            }
        }

        foreach(var peer in loaded)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var parts = peer.Split(':');
                    var ip = parts[0];
                    var port = int.Parse(parts[1]);
                    if (await TryConnectAsync(ip, port, TimeSpan.FromSeconds(2)))
                    {
                        await RequestMempool(ip, port);
                        Console.WriteLine($"[P2P] Restored connection to {peer}.");
                    }
                }
                catch
                {
                    Console.WriteLine($"[P2P] Offline peer ignored.");
                }
            });
        }
    }
    public async Task<List<string>> LoadPeersAsync()
    {
        try
        {
            if (!File.Exists(PeersFilePath)) return new List<string>();
            var json = await File.ReadAllTextAsync(PeersFilePath);
            var list = JsonSerializer.Deserialize<List<string>>(json);
            return list ?? new List<string>();
        } catch(Exception ex)
        {
            Console.WriteLine($"[P2P] Error loading peers.json: {ex.Message}");
            return new List<string>();
        }
    }

    public async Task SavePeersAsync()
    {
        try
        {
            List<string> copy;
            lock (_peersLock)
            {
                copy = new List<string>(_peers);
            }
            var json = JsonSerializer.Serialize(copy);
            await File.WriteAllTextAsync(PeersFilePath, json);
        }
        catch (Exception ex)
        {
                Console.WriteLine($"[P2P] Error saving peers: {ex.Message}");
        }
    }
    private async Task<bool> TryConnectAsync(string ip, int port, TimeSpan timeout)
    {
        try
        {
            using var cts = new CancellationTokenSource(timeout);
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(ip, port);
            var completed = await Task.WhenAny(connectTask, Task.Delay(Timeout.Infinite, cts.Token));
            if (completed != connectTask) return false;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task ConnectToPeer(string peerAddress)
    {
       if (string.IsNullOrEmpty(peerAddress)) return;

       lock(_peersLock)
        {
            if (_peers.Contains(peerAddress))
            {
                Console.WriteLine($"[P2P] Already connected to peer: {peerAddress}");
                return;
            }
        }

       var parts = peerAddress.Split(':');
        if (parts.Length != 2)
        {
            Console.WriteLine($"[P2P] Invalid peer address format: {peerAddress}. Expected format is IP:Port");
            return;
        }

        var ip = parts[0];
        if (!int.TryParse(parts[1], out var port))
        {
            Console.WriteLine($"[P2P] Invalid port in address: {peerAddress}");
            return;
        }

        var reachable = await TryConnectAsync(ip, port, TimeSpan.FromSeconds(3));
        if (!reachable)
        {
            Console.WriteLine($"[P2P] Unable to reach {peerAddress}. Not added to registry.");
            return;
        }

        await RequestMempool(ip, port);
        lock(_peersLock)
        {
            _peers.Add(peerAddress);
        }
        await SavePeersAsync();
        Console.WriteLine($"[P2P] Connected and saved peer: {peerAddress}");
    }

    public async Task BroadcastTransactionAsync(Transaction transaction)
    {
        var jsonTransaction = JsonSerializer.Serialize(transaction);
        var message = JsonSerializer.Serialize(new NetworkMessage(type: "NEW_TRANSACTION", data: jsonTransaction));

        await SendMessageAsync(message);
    }
    public async Task BroadcastNewBlockAsync(Block block)
    {
        var jsonBlock = JsonSerializer.Serialize(block);
        var message = JsonSerializer.Serialize(new NetworkMessage(type: "NEW_BLOCK", data: jsonBlock));

        await SendMessageAsync(message);
    }
    public async Task BroadcastChainAsync(List<Block> chain)
    {
        var jsonChain = JsonSerializer.Serialize(chain);
        var message = JsonSerializer.Serialize(new NetworkMessage(type: "NEW_CHAIN", data: jsonChain));

        await SendMessageAsync(message);
    }
    public async Task RequestChainAsync(string ip, int port)
    {
        try
        {
            var message = JsonSerializer.Serialize(new NetworkMessage(type: "REQUEST_CHAIN", data: ""));

            using var client = new TcpClient();
            await client.ConnectAsync(ip, port);

            await using var stream = client.GetStream();
            await using var writer = new StreamWriter(stream) { AutoFlush = true };

            await writer.WriteLineAsync(message);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error requesting chain: {e.Message}");
        }
    }
    public async Task RequestMempool(string ip, int port)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(ip, port);
            await using var stream = client.GetStream();
            await using var writer = new StreamWriter(stream) { AutoFlush = true };

            await writer.WriteLineAsync(JsonSerializer.Serialize(new NetworkMessage(type: "REQUEST_MEMPOOL", data: "")));

            using var reader = new StreamReader(stream);
            var responseLine = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(responseLine)) return;

            var response = JsonSerializer.Deserialize<NetworkMessage>(responseLine);
            if (response?.type != "SYNC_MEMPOOL") return;

            var receivedTxs = JsonSerializer.Deserialize<List<Transaction>>(response.data);
            if (receivedTxs == null) return;

            if (blockChainService == null) return;

            var existingTxs = blockChainService.PendingTransactions.Select(t => t.Id).ToHashSet();
            int added = 0;

            foreach (var tx in receivedTxs)
            {
                if (existingTxs.Contains(tx.Id)) continue;
                if (!TransactionService.ValidateTransaction(tx).IsValid) continue;
                blockChainService.PendingTransactions.Add(tx);
                existingTxs.Add(tx.Id);
                added++;
            }

            Console.WriteLine($"[Gossip] Mempool synced from {ip}:{port} - {added} new transactions added");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Gossip] Error syncing mempool from {ip}:{port} - {ex.Message}");
        }
    }
    public async Task SyncMempool(List<Transaction> transactions)
    {
        var jsonTxs = JsonSerializer.Serialize(transactions);
        var message = new NetworkMessage(type: "SYNC_MEMPOOL", data: jsonTxs);
        await SendMessage(message);
    }
    public async Task RequestSpvProofAsync(string ip, int port, string txId)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(ip, port);
            await using var stream = client.GetStream();
            await using var writer = new StreamWriter(stream) { AutoFlush = true };

            await writer.WriteLineAsync(JsonSerializer.Serialize(new NetworkMessage(type: "REQUEST_SPV_PROOF", data: txId)));

            using var reader = new StreamReader(stream);
            var responseLine = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(responseLine)) return;

            var response = JsonSerializer.Deserialize<NetworkMessage>(responseLine);

            if (response?.type == "SPV_NOT_FOUND")
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[SPV] Transaction not found in the network.");
                Console.ForegroundColor = ConsoleColor.White;
                return;
            }

            if (response?.type == "SPV_RESULT") return;

            var proof = JsonSerializer.Deserialize<SpvProof>(response.data);
            if (proof == null) return;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n[SPV] Proof received from {ip}:{port}");
            Console.WriteLine($"[SPV] Block Index:         #{proof.BlockIndex}");
            Console.WriteLine($"[SPV] Target tx:           {proof.TxId}");
            Console.WriteLine($"[SPV] Tx hash:             {proof.TxHash}");
            Console.WriteLine($"[SPV] expectedMerkleRoot:  {proof.MerkleRoot}");
            Console.ForegroundColor = ConsoleColor.White;

            var requestingPeer = $"{ip}:{port}";
            List<string> otherPeers;
            lock (_peersLock) { otherPeers = _peers.Where(p => p != requestingPeer).ToList(); }

            if (otherPeers.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[SPV] Only one peer available - cannot cross-validate MerkleRoot. Proceeding with caution.");
                Console.ForegroundColor = ConsoleColor.White;
            }
            else
            {
                bool rootConfirmed = false;
                foreach (var peer in otherPeers)
                {
                    var parts = peer.Split(":");
                    if (await VerifyMerkleRootWithPeer(parts[0], int.Parse(parts[1]), proof.MerkleRoot))
                    {
                        rootConfirmed = true;
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"[SPV] MerkleRoot confirmed by independent peer {peer}.");
                        Console.ForegroundColor = ConsoleColor.White;
                        break;
                    }
                }
                if (!rootConfirmed)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[SPV STORM] A full node tried to give a fake merkle root! Proof denied.");
                    Console.ForegroundColor = ConsoleColor.White;
                    return;
                }
            }

            Console.WriteLine($"[SPV] Merkle Proof Hash Path ({proof.ProofPath.Count} hashes):");
            for (int i = 0; i < proof.ProofPath.Count; i++)
                Console.WriteLine($"       [{i}] {proof.ProofPath[i]}");

            var verified = hashingService.VerifyMerkleProof(proof.TxHash, proof.ProofPath, proof.MerkleRoot);

            Console.WriteLine();
            if (verified)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[SPV Verification Passed: TRUE]");
                Console.WriteLine($"Transaction {proof.TxId} is confirmed in block #{proof.BlockIndex}.");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[SPV Verification failed: FALSE]");
            }
            Console.ForegroundColor = ConsoleColor.White;
        } catch(Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[SPV] Error: {ex.Message}");
            Console.ForegroundColor = ConsoleColor.White;
        }
    }

    private async Task<bool> VerifyMerkleRootWithPeer(string ip, int port, string merkleRoot)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(ip, port);
            await using var stream = client.GetStream();
            await using var writer = new StreamWriter(stream) { AutoFlush = true };
            await writer.WriteLineAsync(JsonSerializer.Serialize(new NetworkMessage(type: "REQUEST_MERKLE_ROOT", data: merkleRoot)));

            using var reader = new StreamReader(stream);
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(line)) return false;

            var response = JsonSerializer.Deserialize<NetworkMessage>(line);
            return response?.type == "MERKLE_ROOT_RESULT" && bool.TryParse(response.data, out var exists) && exists;
        }
        catch { return false; }
    }

    private async Task SendMessage(NetworkMessage msg)
    {
        var message = JsonSerializer.Serialize(msg);
        try
        {
            foreach (var peer in _peers)
            {
                var parts = peer.Split(':');
                var ipAddress = parts[0];
                var port = int.Parse(parts[1]);
                var client = new TcpClient();
                await client.ConnectAsync(ipAddress, port);
                await using var stream = client.GetStream();
                await using var writer = new StreamWriter(stream) { AutoFlush = true };
                await writer.WriteLineAsync(message);
            }
        } catch (Exception e)
        {
            Console.WriteLine($"Error sending message: {e.Message}");
        }
    }
    private async Task SendMessageAsync(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Sending to {_peers.Count} peers");
        Console.ForegroundColor = ConsoleColor.White;

        var peersToRemove = new List<string>();
        foreach(var peer in _peers)
        {
            try
            {
                var parts = peer.Split(':');
                var ipAddress = parts[0];
                var port = int.Parse(parts[1]);

                var client = new TcpClient();
                await client.ConnectAsync(ipAddress, port);

                await using var stream = client.GetStream();
                await using var writer = new StreamWriter(stream) { AutoFlush = true };

                await writer.WriteLineAsync(message);
                Console.WriteLine($"Sent to {peer}");
            }
            catch
            {
                var parts = peer.Split(':');
                var ipAddress = parts[0];
                var port = int.Parse(parts[1]);

                Console.WriteLine($"[Network] Peer {ipAddress}:{port} has been turned off. Removing from peers list.");
                peersToRemove.Add(peer);
            }
        }

        foreach(var peer in peersToRemove)
        {
            _peers.Remove(peer);
        }
    }
}