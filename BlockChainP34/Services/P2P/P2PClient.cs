using BlockChainP34.Models;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;

namespace BlockChainP34.Services.P2P;

public class P2PClient
{
    private readonly BlockChainService blockChainService;
    public P2PClient(BlockChainService blockChainService)
    {
        this.blockChainService = blockChainService;
    }

    private readonly List<string> _peers = new List<string>(); // peerAddress -> lastSeen
    public async Task ConnectToPeer(string peerAddress)
    {
        if (!_peers.Contains(peerAddress))
        {
            _peers.Add(peerAddress);
            Console.WriteLine($"Connected to peer: {peerAddress}");

            var parts = peerAddress.Split(':');
            await RequestMempool(parts[0], int.Parse(parts[1]));
        }
    }

    public async Task BroadcastTransactionAsync(Transaction transaction)
    {
        var jsonTransaction = JsonSerializer.Serialize(transaction);
        var message = JsonSerializer.Serialize(new NetworkMessage(type: "NEW_TRANSACTION", data: jsonTransaction));

        SendMessageAsync(message).Wait();
    }
    public async Task BroadcastNewBlockAsync(Block block)
    {
        var jsonBlock = JsonSerializer.Serialize(block);
        var message = JsonSerializer.Serialize(new NetworkMessage(type: "NEW_BLOCK", data: jsonBlock));

        SendMessageAsync(message).Wait();
    }
    public async Task BroadcastChainAsync(List<Block> chain)
    {
        var jsonChain = JsonSerializer.Serialize(chain);
        var message = JsonSerializer.Serialize(new NetworkMessage(type: "NEW_CHAIN", data: jsonChain));

        SendMessageAsync(message).Wait();
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