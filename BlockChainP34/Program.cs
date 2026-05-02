using BlockChainP34.Models;
using BlockChainP34.Services;
using System.Xml;

var displayService = new DisplaySerivce();
HashingService hashingService = new HashingService();

var Difficulty = 0;
var blockChainService = new BlockChainService(Difficulty);

//============MENU============
var list = new List<Transaction>();
while (true)
{

    Console.WriteLine("Select an option:");
    Console.WriteLine("[1] Add transactions");
    Console.WriteLine("[2] Mine block");
    Console.WriteLine("[3] Show blockchain");
    Console.WriteLine("[4] Check validity");
    Console.WriteLine("[0] Exit");
    var input = Console.ReadLine();

    switch (input)
    {
        case "1":
            string? sender = null;
            do {
                Console.WriteLine("Enter sender:");
                sender = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(sender)) {
                    Console.WriteLine("Sender cannot be empty. Please enter a valid sender.");
                }
            } while (string.IsNullOrWhiteSpace(sender));

            string? receiver = null;
            do
            {
                Console.WriteLine("Enter receiver:");
                receiver = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(receiver) || sender == receiver){
                    Console.WriteLine("Receiver cannot be empty or be the same as sender. Please enter a valid receiver.");
                }
            } while(string.IsNullOrWhiteSpace(receiver) || sender == receiver);

            decimal amt;
            do
            {
                Console.WriteLine("Enter amount:");
                if(!decimal.TryParse(Console.ReadLine(), out amt) || amt <= 0)
                {
                    Console.WriteLine("Invalid amount. Please enter a positive number.");
                }
            } while (!decimal.TryParse(Console.ReadLine(), out amt));

            list.Add(TransactionService.CreateTransaction(sender, receiver, amt));
            break;
        case "2":
            if(list.Count == 0)
            {
                Console.WriteLine("No transactions to mine. Please add transactions first.");
                break;
            }
            Console.WriteLine("Mining block...");
            blockChainService.AddBlock(list, "System");
            list = new List<Transaction>();
            break;
        case "3":
            Console.WriteLine("Current Blockchain:");
            displayService.DisplayBlockChain(blockChainService.Chain);
            break;
        case "4":
            Console.WriteLine("Checking chain for validity...");
            Console.WriteLine(blockChainService.AnalyzeChain());
            break;
        case "0":
            Console.WriteLine("Exiting...");
            return;
        default:
            Console.WriteLine("Invalid option. Please select a valid option.");
            break;
    }
}



//============TESTING============
/*do
{
    Console.WriteLine("Enter mining Difficulty (e.g. '2'):");
    var input = Console.ReadLine();
    if (!int.TryParse(input, out Difficulty))
    {
        Console.WriteLine("Invalid Difficulty. Must be a positive integer.");
    }

} while (Difficulty <= 0);

var blockChainService = new BlockChainService(Difficulty);

blockChainService.AddBlock(new List<Transaction>(), "System");
blockChainService.AddBlock(new List<Transaction> { TransactionService.CreateTransaction("Alice", "Bob", 10)}, "Alice");
blockChainService.AddBlock(new List<Transaction> { TransactionService.CreateTransaction("Alice", "Bob", 100) }, "Alice");
blockChainService.AddBlock(new List<Transaction>(), "System");
blockChainService.AddBlock(new List<Transaction> { TransactionService.CreateTransaction("Alice", "Bob", 10), TransactionService.CreateTransaction("Alice", "Bob", 10)}, "Alice");

displayService.DisplayBlockChain(blockChainService.Chain);

BlockchainExplorer blockchainExplorer = new BlockchainExplorer(blockChainService.Chain);
Console.WriteLine($" Total volume of transactions: {blockchainExplorer.GetTotalVolume()}");
Console.WriteLine($" Largest transaction: {blockchainExplorer.GetLargestTransaction()}");
Console.WriteLine($" Transactions from or to Alice:");
foreach (var tr in blockchainExplorer.GetAddressHistory("Alice"))
{
    Console.WriteLine(tr);
}
Console.WriteLine($"Transaction location for txId {blockChainService.Chain[3].Transactions[0].Id}: {blockchainExplorer.FindTransactionLocation(blockChainService.Chain[3].Transactions[0].Id).ToString()}");
Console.WriteLine(new string('-', 50));*/
/*blockChainService.AddBlock("Alice pays Bob 1054 ETH", "Alice");
blockChainService.AddBlock("Bob pays Charlie 500 ETH", "Bob");
blockChainService.AddBlock("Charlie pays Dave 200 ETH", "Charlie");
blockChainService.AddBlock("Dave pays Eve 100 ETH", "Dave");

//displayService.DisplayBlockChain(blockChainService.Chain);

if(blockChainService.IsValid())
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("Blockchain is valid.");
    Console.ForegroundColor= ConsoleColor.White;
}
else
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("Blockchain is invalid.");
    Console.ForegroundColor = ConsoleColor.White;
}

blockChainService.Chain[3].Data = "Charlie pays Dave 1000 ETH";
//displayService.DisplayBlockChain(blockChainService.Chain);

if (blockChainService.IsValid())
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("Blockchain is valid.");
    Console.ForegroundColor = ConsoleColor.White;
}
else
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("Blockchain is invalid.");
    Console.ForegroundColor = ConsoleColor.White;
}

foreach(var d in Enumerable.Range(0, Difficulty))
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var blockChain = new BlockChainService(Difficulty);
    blockChain.AddBlock("Alice pays Bob 10 BTC", "System");
    sw.Stop();
    Console.WriteLine($"Time taken to mine a block with pref {Difficulty}: {sw.ElapsedMilliseconds} ms");
}

var blockChainService2 = new BlockChainService(Difficulty);

blockChainService2.AddBlock("Daniel pays Gus 102 ETH", "Daniel");
blockChainService2.AddBlock("Gus pays Bane 520 ETH", "Gus");
blockChainService2.AddBlock("Diana pays Hugh 10 ETH", "Diana");
blockChainService2.AddBlock("Hugh pays Diana 110 ETH", "Hugh");
blockChainService2.AddBlock("Jake pays Paul 13000 ETH", "Jake");

Console.WriteLine(blockChainService2.AnalyzeChain());

blockChainService2.Chain[2].Data = "Gus pays Bane 5000 ETH";
Console.WriteLine(blockChainService2.AnalyzeChain());
blockChainService2.Chain[3].Hash = "0000invalidHashAAA";
Console.WriteLine(blockChainService2.AnalyzeChain());
blockChainService2.Chain[4].PreviousHash = "ChangedHashBUH";
Console.WriteLine(blockChainService2.AnalyzeChain());


Console.ForegroundColor = ConsoleColor.White;
var blockChainService3 = new BlockChainService(Difficulty);

for (int i = 0; i < 2; i++)
{
    for(int j = 0; j < 2; j++)
    {
        blockChainService3.AddBlock("Alice pays Bob 1054 ETH", "Alice");
        blockChainService3.AddBlock("Bob pays Charlie 500 ETH", "Bob");
        blockChainService3.AddBlock("Charlie pays Dave 200 ETH", "Charlie");
        displayService.DisplayBlockChain(blockChainService3.Chain);
        Console.WriteLine($"Difficulty: {blockChainService3.Difficulty}");
    }
}
Console.WriteLine();
Console.WriteLine();

blockChainService3.PrintDifficultyHistory();*/