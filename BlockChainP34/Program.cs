using System.Diagnostics;
using System.Runtime.InteropServices;
using BlockChainP34.Models;
using BlockChainP34.Services;
using BlockChainP34.Services.P2P;
using Microsoft.Extensions.DependencyInjection;

var cryptoService = new CryptoService();
var myWallet = File.Exists("wallet.json") ? Wallet.Load() : new Wallet (cryptoService);
var alice = new Wallet(cryptoService);
/*blockChainService.MineBlock(alice.PublicKey);
blockChainService.MineBlock(alice.PublicKey);*/
var bob = new Wallet(cryptoService);
/*blockChainService.MineBlock(bob.PublicKey);
blockChainService.MineBlock(bob.PublicKey);*/

Console.WriteLine($"My wallet address: {myWallet.PublicKey}");
Console.WriteLine("Enter your node's port:");
int port = int.Parse(Console.ReadLine());
int mode;
do
{
    Console.WriteLine("Working mode:");
    Console.WriteLine("1. Full Node");
    Console.WriteLine("2. Light wallet (SPV Client)");
} while (!int.TryParse(Console.ReadLine(), out mode) || (mode != 1 && mode != 2));

var isFullNode = mode == 1;

if (isFullNode)
{
    var service = new ServiceCollection();
    service.AddSingleton<BlockChainService, BlockChainService>();
    service.AddSingleton<P2PClient, P2PClient>();
    service.AddSingleton<P2PServer, P2PServer>();
    service.AddSingleton<DisplaySerivce, DisplaySerivce>();
    service.AddSingleton<StorageService, StorageService>();
    service.AddSingleton<MiningService, MiningService>();
    service.AddSingleton<HashingService, HashingService>();
    service.AddSingleton<BlockchainExplorer, BlockchainExplorer>();

    var provider = service.BuildServiceProvider();

    var blockChainService = provider.GetRequiredService<BlockChainService>();
    var newBlockChainService = provider.GetService<BlockChainService>();
    var testingBlockChainService = provider.GetService<BlockChainService>();
    var p2pClient = provider.GetService<P2PClient>();
    var p2pServer = provider.GetService<P2PServer>();
    var displayService = provider.GetService<DisplaySerivce>();
    var storageService = provider.GetService<StorageService>();
    var miningService = provider.GetService<MiningService>();
    var hashingService = provider.GetService<HashingService>();
    var explorer = provider.GetService<BlockchainExplorer>();
    p2pClient?.InitializeAsync().Wait();

    p2pServer.Start(port);

    while (true)
    {
        Console.WriteLine("Main Menu");
        Console.WriteLine("1. Connect to another node");
        Console.WriteLine("2. Create new transaction");
        Console.WriteLine("3. Show MeMpool");
        Console.WriteLine("4. Mine block");
        Console.WriteLine("5. Check my balance");
        Console.WriteLine("6. Show blockchain");
        Console.WriteLine("7. Delete blockchain");
        Console.WriteLine("8. SPV transaction check");
        Console.WriteLine("9. Find transaction via ID");
        Console.WriteLine("0. Exit");

        Console.WriteLine("Your option: ");

        switch (Console.ReadLine())
        {
            case "1":
                Console.WriteLine("Enter address of the node you want to connect to (Example: 127.0.0.1:5000):");
                string address = Console.ReadLine();
                p2pClient.ConnectToPeer(address);
                p2pClient.RequestChainAsync(address.Split(':')[0], int.Parse(address.Split(':')[1])).Wait();
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
                foreach (var tx in blockChainService.PendingTransactions)
                {
                    Console.WriteLine(tx);
                }
                break;
            case "4":
                blockChainService.MineBlock(myWallet.PublicKey);
                var lastBlock = blockChainService.Chain.LastOrDefault();
                p2pClient.BroadcastNewBlockAsync(lastBlock).Wait();
                break;
            case "5":
                Console.WriteLine($"Your balance: {blockChainService.GetBalanceNew(myWallet.PublicKey)}");
                break;
            case "6":
                displayService.DisplayBlockChain(blockChainService.Chain);
                break;
            case "7":
                Console.WriteLine("Are you sure you want to delete the blockchain? This action cannot be undone. (y/n)");
                var confirmation = Console.ReadLine();
                if (confirmation != null && confirmation.ToLower() == "y")
                {
                    storageService.DeleteBlockChain();
                    blockChainService.Chain.Clear();
                    Console.WriteLine("Blockchain deleted.");
                }
                else
                {
                    Console.WriteLine("Action cancelled.");
                }
                break;
            case "8":
                var spvBlock = blockChainService.Chain.FirstOrDefault(b => b.Transactions.Count >= 2 && !string.IsNullOrEmpty(b.MerkleRoot));
                if(spvBlock == null)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("[SPV] No suitable block found. Mine a few blocks with pending transactions first.");
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                }
                
                var spvTx = spvBlock.Transactions[new Random().Next(0, spvBlock.Transactions.Count)];
                var spvTxHash = hashingService.ComputeHash(spvTx.ToRawString());
                var proof = hashingService.GetMerkleProof(spvBlock.Transactions, spvTx.Id);
                var verified = hashingService.VerifyMerkleProof(spvTxHash, proof, spvBlock.MerkleRoot);

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[SPV] Block #{spvBlock.Index} — {spvBlock.Transactions.Count} transactions");
                Console.WriteLine($"[SPV] Target Transaction ID:  {spvTx.Id}");
                Console.WriteLine($"[SPV] Transaction Hash:       {spvTxHash}");
                Console.WriteLine($"[SPV] expectedMerkleRoot:     {spvBlock.MerkleRoot}");
                Console.WriteLine($"[SPV] Merkle Proof Hash Path ({proof.Count} hashes):");
                for (int i = 0; i < proof.Count; i++)
                    Console.WriteLine($"       [{i}] {proof[i]}");
                Console.ForegroundColor = ConsoleColor.White;

                Console.WriteLine();
                if (verified)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("[SPV Verification Passed: TRUE]");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[SPV Verification Failed: FALSE]");
                }
                Console.ForegroundColor = ConsoleColor.White;
                break;
            case "9":
                Console.WriteLine("Enter transaction ID:");
                var txId = Console.ReadLine();
                var foundTx = explorer.FindTransactionLocation(txId);
                if (foundTx.tx != null)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    if (foundTx.block != null)
                    {
                        Console.WriteLine($"Transaction found in block: {foundTx.block.Index}" +
                            $"\n==Transaction details==" +
                            $"\nFrom: {foundTx.tx.From}" +
                            $"\nTo: {foundTx.tx.To}" +
                            $"\nAmount: {foundTx.tx.Amount}" +
                            $"\nTime: {foundTx.tx.TimeStamp}");
                    }
                    else
                    {
                        Console.WriteLine($"Transaction found in mempol." +
                            $"\n==Transaction details==" +
                            $"\nFrom: {foundTx.tx.From}" +
                            $"\nTo: {foundTx.tx.To}" +
                            $"\nAmount: {foundTx.tx.Amount}" +
                            $"\nTime: {foundTx.tx.TimeStamp}");
                    }
                    Console.ForegroundColor = ConsoleColor.White;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Transaction not found.");
                    Console.ForegroundColor = ConsoleColor.White;
                }
                break;
            case "0":
                Console.WriteLine("Exiting...");
                return;
            default:
                Console.WriteLine("Unknown choice.");
                break;
        }
    }
}
else
{
    var service = new ServiceCollection();
    service.AddSingleton<CryptoService, CryptoService>();
    service.AddSingleton<P2PClient, P2PClient>();

    var provider = service.BuildServiceProvider();
    
    var p2pClient = provider.GetService<P2PClient>();
    p2pClient?.InitializeAsync().Wait();

    var localWallet = new Wallet(cryptoService);
    var localMemPool = new List<Transaction>();
    while (true)
    {
        Console.WriteLine("1. Connect to another node");
        Console.WriteLine("2. Create new transaction");
        Console.WriteLine("3. Ask for SPV proof from network");
        Console.WriteLine("0. Exit");

        switch (Console.ReadLine())
        {
            case "1":
                Console.WriteLine("Enter address of the node you want to connect to (Example: 127.0.0.1:5000):");
                string address = Console.ReadLine();
                p2pClient.ConnectToPeer(address);
                p2pClient.RequestChainAsync(address.Split(':')[0], int.Parse(address.Split(':')[1])).Wait();
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
                    localMemPool.Add(tx);
                    p2pClient.BroadcastTransactionAsync(tx).Wait();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
                break;
            case "3":
                /*Console.WriteLine("Enter transaction ID: ");
                var txId = Console.ReadLine();

                await p2pClient.RequestSpvProofAsync("127.0.0.1", port, txId);*/
                break;
            case "0":
                Console.WriteLine("Exiting...");
                return;
            default:
                Console.WriteLine("Unknown choice.");
                break;
        }
    }
}

//Testing
/*Console.ForegroundColor = ConsoleColor.Blue;
Console.WriteLine("Testing amount priority.");
Console.ForegroundColor = ConsoleColor.White;
var tx10 = TransactionService.CreateTransaction(myWallet.PublicKey, alice.PublicKey, 10, fee: 2, myWallet.PrivateKey);
var tx50 = TransactionService.CreateTransaction(myWallet.PublicKey, bob.PublicKey, 50, fee: 2, myWallet.PrivateKey);
var tx20 = TransactionService.CreateTransaction(myWallet.PublicKey, bob.PublicKey, 20, fee: 2, myWallet.PrivateKey);

blockChainService.AddTransaction(tx10);
blockChainService.AddTransaction(tx50);
blockChainService.AddTransaction(tx20);

Console.WriteLine("Mempool before mining:");
foreach (var tx in blockChainService.PendingTransactions) Console.WriteLine(tx);

blockChainService.MineBlock(myWallet.PublicKey);
Console.WriteLine("After:");
foreach (var tx in blockChainService.PendingTransactions) Console.WriteLine(tx);
Console.WriteLine("Blockchain:");
displayService.DisplayBlockChain(blockChainService.Chain);

Console.ForegroundColor = ConsoleColor.Blue;
Console.WriteLine("Testing TTL.");
Console.ForegroundColor = ConsoleColor.White;
var txTTL = TransactionService.CreateTransaction(bob.PublicKey, alice.PublicKey, 32, fee: 2, bob.PrivateKey);
blockChainService.AddTransaction(txTTL);
Console.WriteLine($"Created transaction with TTL: {txTTL}");
Console.WriteLine("Mempool after adding TTL tx:");
foreach (var tx in blockChainService.PendingTransactions) Console.WriteLine(tx);
Console.ForegroundColor = ConsoleColor.Blue;
Console.WriteLine("Waiting 6 seconds to let TTL expire...");
Console.ForegroundColor = ConsoleColor.White;
Thread.Sleep(6000);
Console.WriteLine("Mining block...");
blockChainService.MineBlock(bob.PublicKey);
displayService.DisplayBlockChain(blockChainService.Chain);

Console.ForegroundColor = ConsoleColor.Blue;
Console.WriteLine("Testing locktime.");
Console.ForegroundColor = ConsoleColor.White;

var txlock = TransactionService.CreateTransaction(bob.PublicKey, alice.PublicKey, 10, fee: 2, bob.PrivateKey, lockTime: 6);
Console.WriteLine($"Created transaction with locktime: {txlock}");
blockChainService.AddTransaction(txlock);
Console.WriteLine("Mempool:");
foreach (var tx in blockChainService.PendingTransactions) Console.WriteLine(tx);
Console.ForegroundColor = ConsoleColor.Blue;
Console.WriteLine("Mining 2 blocks:");
Console.ForegroundColor = ConsoleColor.White;
blockChainService.MineBlock(bob.PublicKey);
blockChainService.MineBlock(bob.PublicKey);
Console.WriteLine($"Total blocks: {blockChainService.Chain.Count}");
Console.WriteLine("Mempool:");
foreach (var tx in blockChainService.PendingTransactions) Console.WriteLine(tx);
Console.ForegroundColor = ConsoleColor.Blue;
Console.WriteLine("Mining 2 more blocks:");
Console.ForegroundColor = ConsoleColor.White;
blockChainService.MineBlock(bob.PublicKey);
blockChainService.MineBlock(bob.PublicKey);
Console.WriteLine($"Total blocks: {blockChainService.Chain.Count}");
Console.WriteLine("Mempool:");
foreach (var tx in blockChainService.PendingTransactions) Console.WriteLine(tx);
Console.ForegroundColor = ConsoleColor.Blue;
Console.WriteLine("Mining another 2 blocks:");
Console.ForegroundColor = ConsoleColor.White;
blockChainService.MineBlock(bob.PublicKey);
blockChainService.MineBlock(bob.PublicKey);
Console.WriteLine($"Total blocks: {blockChainService.Chain.Count}");
Console.WriteLine("Mempool:");
foreach (var tx in blockChainService.PendingTransactions) Console.WriteLine(tx);
Console.ForegroundColor = ConsoleColor.Blue;
Console.WriteLine("Chain:");
Console.ForegroundColor = ConsoleColor.White;
displayService.DisplayBlockChain(blockChainService.Chain);*/

/*for(int i = 0; i < 6; i++)
{
    blockChainService.MineBlock("Miner1");
}

blockChainService.Chain[2].Transactions.First().Amount = 9999999;
var report = blockChainService.RunFullAudit(blockChainService.Chain);
var attackOrigin = blockChainService.FindAttackOrigin(report, blockChainService.Chain);
Console.WriteLine(blockChainService.GenerateForensicReport(report, attackOrigin));*/

/*var aliceWallet = new Wallet(cryptoService);
var bobWallet = new Wallet(cryptoService);
var spammerWallet = new Wallet(cryptoService);

for (int i = 0; i < 6; i++)
{
    testingBlockChainService.MineBlock("Miner1");
}

//State rebuild test
var tx1 = TransactionService.CreateTransaction(aliceWallet.PublicKey, bobWallet.PublicKey, 10, fee: 1, aliceWallet.PrivateKey);
testingBlockChainService.AddTransaction(tx1);
testingBlockChainService.MineBlock("Miner1");
testingBlockChainService.Chain.Last().Transactions.Where(tx => tx.From != "COINBASE").First();
Console.WriteLine($"Validate and rebuild state result: {testingBlockChainService.ValidateAndRebuildState()}");
Console.WriteLine($"Balances cash cleared? {testingBlockChainService.BalancesCash.Count == 0}");

//TTL test
var newTx = TransactionService.CreateTransaction(bobWallet.PublicKey, aliceWallet.PublicKey, 10, fee: 1, bobWallet.PrivateKey);
var oldTx = TransactionService.CreateTransaction(bobWallet.PublicKey, aliceWallet.PublicKey, 10, fee: 1, bobWallet.PrivateKey);
testingBlockChainService.AddTransaction(oldTx);
oldTx.TimeStamp = oldTx.TimeStamp.AddSeconds(-60);
testingBlockChainService.AddTransaction(newTx);
Console.WriteLine($"Removed transactions: {testingBlockChainService.EvictStaleTransactions(30)}");
Console.WriteLine($"Pending transactions count (should be 1): {testingBlockChainService.PendingTransactions.Count}");

//Spam filter test
for (int i = 0; i < 3; i++)
{
    testingBlockChainService.AddTransaction(TransactionService.CreateTransaction(spammerWallet.PublicKey, aliceWallet.PublicKey, 10, fee: 1, spammerWallet.PrivateKey));
}
try
{
    testingBlockChainService.AddTransaction(TransactionService.CreateTransaction(spammerWallet.PublicKey, aliceWallet.PublicKey, 10, fee: 1, spammerWallet.PrivateKey));

}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"Spam filter triggered: {ex.Message}");
}*/