using System.Text.Json;
using BlockChainP34.Models;
using BlockChainP34.Services;
using BlockChainP34.Services.P2P;
using Microsoft.Extensions.DependencyInjection;

var cryptoService = new CryptoService();
var myWallet = File.Exists("wallet.json") ? Wallet.Load(cryptoService) : new Wallet (cryptoService);

Console.ForegroundColor = ConsoleColor.White;
Console.WriteLine($"=====!!!My wallet address!!!=====\n{myWallet.PublicKey}\n================================");
Console.WriteLine(" Enter your node's port:");
int port = int.Parse(Console.ReadLine());
int mode;
do
{
    Console.WriteLine("[Working mode]:");
    Console.WriteLine("(1) Full Node");
    Console.WriteLine("(2) Light wallet (SPV Client)");
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
        Console.WriteLine("===[Main Menu]===");
        Console.WriteLine(" (1) Connect to another node");
        Console.WriteLine(" (2) Create new transaction");
        Console.WriteLine(" (3) Show MeMpool");
        Console.WriteLine(" (4) Mine block");
        Console.WriteLine(" (5) Check my balance");
        Console.WriteLine(" (6) Show blockchain");
        Console.WriteLine(" (7) Delete blockchain");
        Console.WriteLine(" (8) SPV transaction check");
        Console.WriteLine(" (9) Find transaction via ID");
        Console.WriteLine(" (10) Generate offline transaction");
        Console.WriteLine(" (11) Broadcast transaction from file");
        Console.WriteLine(" (12) Check wallet history");
        Console.WriteLine(" (13) Mint a token");
        Console.WriteLine(" (14) Show all balances");
        Console.WriteLine(" (0) Exit");

        Console.WriteLine("Your option: ");

        switch (Console.ReadLine())
        {
            case "1":
                Console.WriteLine("Enter address of the node you want to connect to (Example: 127.0.0.1:5000):");
                string address = Console.ReadLine();
                p2pClient.ConnectToPeer(address).Wait();
                p2pClient.RequestChainAsync(address.Split(':')[0], int.Parse(address.Split(':')[1])).Wait();
                break;
            case "2":
                Console.WriteLine("Enter reciever address:");
                var receiver = Console.ReadLine();
                Console.WriteLine("Enter amount: ");
                var amount = decimal.Parse(Console.ReadLine());
                Console.WriteLine("Enter fee:");
                var fee = decimal.Parse(Console.ReadLine() ?? "1");
                Console.WriteLine();
                try
                {
                    Console.WriteLine("Enter token symbol (leave blank for MAIN):");
                    var tokenInput = Console.ReadLine()?.Trim().ToUpper();
                    var token = string.IsNullOrWhiteSpace(tokenInput) ? "MAIN" : tokenInput;
                    var tx = TransactionService.CreateTransaction(myWallet.PublicKey, receiver, amount, fee: fee, myWallet.PrivateKey, token: token);
                    blockChainService.AddTransaction(tx);
                    p2pClient.BroadcastTransactionAsync(tx).Wait();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
                break;
            case "3":
                Console.ForegroundColor = ConsoleColor.DarkBlue;
                Console.WriteLine("===[Mempool]===:");
                Console.ForegroundColor = ConsoleColor.White;
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
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[Your balance]: {blockChainService.GetBalanceNew(myWallet.PublicKey)}");
                Console.ForegroundColor = ConsoleColor.White;
                break;
            case "6":
                displayService.DisplayBlockChain(blockChainService.Chain);
                break;
            case "7":
                Console.ForegroundColor = ConsoleColor.Red;
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
                Console.ForegroundColor = ConsoleColor.White;
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
                var foundTx = explorer.FindTransactionById(txId);
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
            case "10":
                Console.WriteLine("Enter sender address (your public key): ");
                var coldFrom = Console.ReadLine();
                Console.WriteLine("Enter receiver address: ");
                var coldTo = Console.ReadLine();
                Console.WriteLine("Enter amount: ");
                var coldAmount = decimal.Parse(Console.ReadLine());
                Console.WriteLine("Enter fee: ");
                var coldFee = decimal.Parse(Console.ReadLine());
                Console.WriteLine("Enter your wallet password: ");
                var coldPkeyPass = Console.ReadLine();
                var coldPKey = string.Empty;
                try
                {
                    if (File.Exists("wallet.json"))
                    {
                        var json = File.ReadAllText("wallet.json");
                        var stored = JsonSerializer.Deserialize<JsonElement>(json);
                        var encryptedPrivateKey = stored.GetProperty("PrivateKey").GetString() ?? string.Empty;

                        if (encryptedPrivateKey.StartsWith("ENC:"))
                        {
                            var encryptedPart = encryptedPrivateKey.Substring(4);
                            coldPKey = WalletEncryptionService.Decrypt(encryptedPart, coldPkeyPass);
                        }
                        else
                        {
                            coldPKey = encryptedPrivateKey;
                        }
                    }
                    else
                    {
                        coldPKey = myWallet.PrivateKey;
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error: {ex.Message}");
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                }
                Console.WriteLine("Enter output file path (e.g. offline_tx.json): ");
                var coldFilePath = Console.ReadLine();
                try
                {
                    var coldWallet = new ColdWalletService(cryptoService);
                    coldWallet.GenerateOfflineTransaction(myWallet.PublicKey, coldTo, coldAmount, coldFee, coldPKey, coldFilePath);
                } catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[Cold Wallet] Failed to generate transaction: {ex.Message}");
                    Console.ForegroundColor = ConsoleColor.White;
                }
                break;
            case "11":
                Console.WriteLine("Enter path to offline transaction file: ");
                var broadcastPath = Console.ReadLine();

                try
                {
                    if (!File.Exists(broadcastPath))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("[Cold Wallet] File not found.");
                        Console.ForegroundColor = ConsoleColor.White;
                        break;
                    }

                    var json = File.ReadAllText(broadcastPath);
                    var tx = JsonSerializer.Deserialize<Transaction>(json);
                    if(tx == null)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("[Cold Wallet] Failed to parse transaction file.");
                        Console.ForegroundColor = ConsoleColor.White; 
                        break;
                    }

                    var validation = TransactionService.ValidateTransaction(tx);
                    if (!validation.IsValid)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[Cold Wallet] REJECTED: RSA signature invalid, file may have been tempered: {validation.error}");
                        Console.ForegroundColor = ConsoleColor.White;
                        break;
                    }

                    var tokenBalance = blockChainService.GetBalance(tx.From, tx.TokenSymbol);
                    var mainBalance = blockChainService.GetBalance(tx.From, "MAIN");
                    bool insufficientFunds = tx.TokenSymbol == "MAIN" ? mainBalance < tx.Amount + tx.Fee : tokenBalance < tx.Amount || mainBalance < tx.Fee;

                    if (insufficientFunds)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[Cold Wallet] REJECTED: insufficient balance.");
                        Console.ForegroundColor = ConsoleColor.White;
                        break;
                    }

                    blockChainService.AddTransaction(tx);
                    p2pClient.BroadcastTransactionAsync(tx).Wait();

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[Cold Wallet] Transaction {tx.Id} added to mempool from file and broadcasted to other nodes.");
                    Console.ForegroundColor = ConsoleColor.White;
                } catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[Cold Wallet] An error occured: {ex.Message}");
                    Console.ForegroundColor = ConsoleColor.White;
                }
                break;
            case "12":
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine("===Wallet history===");
                var history = explorer.GetTransactionHistory(myWallet.PublicKey);
                if (history.Count == 0) Console.WriteLine("No transactions found.");
                else
                {
                    foreach(var tx in history)
                    {
                        Console.WriteLine(tx);
                    }
                }
                Console.WriteLine("====================");
                Console.ForegroundColor = ConsoleColor.White;
                break;
            case "13":
                Console.WriteLine("Enter token symbol (e.g. ACADEMY_COIN): ");
                var mintSymbol = Console.ReadLine()?.Trim().ToUpper();
                if (string.IsNullOrWhiteSpace(mintSymbol))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[MINT] Token symbol cannot be empty.");
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                }
                Console.WriteLine("Enter amount to mint: ");
                if(!decimal.TryParse(Console.ReadLine(), out var mintAmount) || mintAmount <= 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[MINT] Invalid amount.");
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                }

                try
                {
                    var mintTx = TransactionService.CreateMintTransaction(myWallet.PublicKey, mintAmount, mintSymbol);
                    blockChainService.AddTransaction(mintTx);
                    p2pClient.BroadcastTransactionAsync(mintTx).Wait();
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"[Mint] Issued {mintAmount} {mintSymbol} - pending until next block is mined.");
                    Console.ForegroundColor = ConsoleColor.White;
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[Mint] Error: {ex.Message}");
                    Console.ForegroundColor = ConsoleColor.White;
                }
                break;
            case "14":
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("===[ Confirmed Token Balances ]===");
                var allBalances = blockChainService.GetAllTokenBalances(myWallet.PublicKey);
                if (allBalances.Count == 0) Console.WriteLine("No confirmed balances yet. Mine a block first.");
                else
                {
                    foreach(var (token, balance) in allBalances)
                    {
                        Console.WriteLine($"    {token}: {balance}");
                    }
                }
                Console.WriteLine("==================================");
                Console.ForegroundColor = ConsoleColor.White;
                break;
            case "0":
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Exiting...");
                Console.ForegroundColor = ConsoleColor.White;
                return;
            default:
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Unknown choice.");
                Console.ForegroundColor = ConsoleColor.White;
                break;
        }
    }
}
else
{
    var hashingService = new HashingService();
    var p2pClient = new P2PClient(null, hashingService);
    await p2pClient.InitializeAsync();

    var localMemPool = new List<Transaction>();
    while (true)
    {
        Console.WriteLine("===[SPV menu]===");
        Console.WriteLine("(1) Connect to another node");
        Console.WriteLine("(2) Create new transaction");
        Console.WriteLine("(3) Ask for SPV proof from network");
        Console.WriteLine("(0) Exit");

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
                Console.WriteLine("Enter transaction ID to verify: ");
                var txId = Console.ReadLine();
                Console.WriteLine("Enter full node address to request proof from (e.g. 127.0.0.1:5000): ");
                var spvAddress = Console.ReadLine()!.Split(":");
                try
                {
                    await p2pClient.RequestSpvProofAsync(spvAddress[0], int.Parse(spvAddress[1]), txId);
                } catch(Exception ex)
                {
                    Console.WriteLine($"An error occured: {ex.Message}");
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