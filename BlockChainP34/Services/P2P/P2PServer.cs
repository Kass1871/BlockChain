using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using BlockChainP34.Models;

namespace BlockChainP34.Services.P2P
{
    public class P2PServer
    {
        private readonly BlockChainService blockChainService;
        public P2PServer(BlockChainService blockChainService)
        {
            this.blockChainService = blockChainService;
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
                    var tx = JsonSerializer.Deserialize<Transaction>(jsonLine);

                    if (tx != null && !blockChainService.PendingTransactions.Contains(tx))
                    {
                        blockChainService.AddTransaction(tx);
                    }
                }
            }
            catch(Exception ex)
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
