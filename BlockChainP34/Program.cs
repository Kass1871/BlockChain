using BlockChainP34.Services;

var displayService = new DisplaySerivce();
HashingService hashingService = new HashingService();

var Prefix = string.Empty;
do
{
    Console.WriteLine("Enter mining Prefix (e.g. 'd3fault'):");
    Prefix = Console.ReadLine();
    if (Prefix == null)
    {
        Console.WriteLine("Invalid prefix. Must not be null.");
    }

} while (string.IsNullOrWhiteSpace(Prefix));

var blockChainService = new BlockChainService(Prefix);

blockChainService.AddBlock("Alice pays Bob 1054 ETH", "Alice");
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

var sw = System.Diagnostics.Stopwatch.StartNew();
var blockChain = new BlockChainService(Prefix);
blockChain.AddBlock("Alice pays Bob 10 BTC", "System");
sw.Stop();
Console.WriteLine($"Time taken to mine a block with pref {Prefix}: {sw.ElapsedMilliseconds} ms");

var blockChainService2 = new BlockChainService(Prefix);

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
