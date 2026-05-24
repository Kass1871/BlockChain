using BlockChainP34.Models;
using BlockChainP34.Services;
using BlockChainP34.Services.P2P;
using Microsoft.Extensions.DependencyInjection;

var service = new ServiceCollection();
service.AddTransient<BlockChainService>(provider => new BlockChainService(Difficulty: 1));
service.AddSingleton<CryptoService, CryptoService>();
service.AddSingleton<P2PClient, P2PClient>();
service.AddSingleton<P2PServer, P2PServer>();
service.AddSingleton<DisplaySerivce, DisplaySerivce>();

var provider = service.BuildServiceProvider();

var blockChainService = provider.GetService<BlockChainService>();
var newBlockChainService = provider.GetService<BlockChainService>();
var p2pClient = provider.GetService<P2PClient>();
var p2pServer = provider.GetService<P2PServer>();
var cryptoService = provider.GetService<CryptoService>();
var displayService = provider.GetService<DisplaySerivce>();

var myWallet = new Wallet(cryptoService);
Console.WriteLine($"My wallet address: {myWallet.PublicKey}");
Console.WriteLine("Enter your node's port:");
int port = int.Parse(Console.ReadLine());

p2pServer.Start(port);


//MENU

while (true)
{
    Console.WriteLine("Main Menu");
    Console.WriteLine("1. Connect to another node");
    Console.WriteLine("2. Create new transaction");
    Console.WriteLine("3. Show MeMpool");
    Console.WriteLine("4. Mine block");
    Console.WriteLine("5. Check my balance");
    Console.WriteLine("6. Show blockchain");
    Console.WriteLine("9. Test stuff");
    Console.WriteLine("0. Exit");

    Console.WriteLine("Your option: ");

    switch (Console.ReadLine())
    {
        case "1":
            Console.WriteLine("Enter address of the node you want to connect to (Example: 127.0.0.1:5000):");
            string address = Console.ReadLine();
            p2pClient.ConnectToPeer(address);
            break;
        case "2":
            Console.WriteLine("Enter reciever address:");
            var receiver = Console.ReadLine();
            Console.WriteLine("Enter amount: ");
            var amount = decimal.Parse(Console.ReadLine());
            Console.WriteLine("Enter fee:");
            var fee = decimal.Parse(Console.ReadLine() ?? "1");

            try
            {
                var tx = TransactionService.CreateTransaction(myWallet.PublicKey, receiver, amount, fee: fee, myWallet.PrivateKey);
                blockChainService.AddTransaction(tx);
                p2pClient.BroadcastTransactionAsync(tx).Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            break;
        case "3":
            Console.WriteLine("Mempool:");
            foreach(var tx in blockChainService.PendingTransactions)
            {
                Console.WriteLine($" - {tx.Id}: {tx.Amount} from {tx.From} to {tx.To}");
            }
            break;
        case "4":
            blockChainService.MineBlock(myWallet.PublicKey);
            break;
        case "5":
            Console.WriteLine($"Your balance: {blockChainService.GetBalance(myWallet.PublicKey)}");
            break;
        case "6":
            displayService.DisplayBlockChain(blockChainService.Chain);
            break;
        case "0":
            Console.WriteLine("Exiting...");
            return;
        case "9":
            for (int i = 0; i < 5; i++)
            {
                newBlockChainService.MineBlock("HackerWallet");
            }
            blockChainService.ReplaceChain(newBlockChainService.Chain);
            break;
        default:
            Console.WriteLine("Unknown choice.");
            break;
    }
}