// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using System.Text;
using example_alib;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.ObjectStore;

Console.OutputEncoding = Encoding.UTF8;

var meg = Math.Pow(2, 20);

var memoryMb = Process.GetCurrentProcess().PrivateMemorySize64 / meg;
var allocatedMb = GC.GetTotalAllocatedBytes() / meg;
Console.WriteLine($"memoryMb: {memoryMb:n2} MB");
Console.WriteLine($"allocatedMb: {allocatedMb:n2} MB");

await using var nats = new NatsConnection(new NatsOpts { Url = Constants.Url});

var js = new NatsJSContext(nats);
var obj = new NatsObjContext(js);

var store = await obj.CreateObjectStoreAsync("test-obj-1");

for (int i = 0; i < 3; i++)
{
    Console.WriteLine("________________________________");
    // var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    // var file1 = Path.Combine(home, "Downloads", "big.bin");
    // var file2 = Path.Combine(home, "Downloads", "big-copy.bin");
    var file1 = Path.Combine("d:", "tmp", "big.bin");
    var file2 = Path.Combine("d:", "tmp", "big-copy.bin");
    File.Delete(file2);

    await store.PutAsync("k1", File.OpenRead(file1));
    await store.GetAsync("k1", File.OpenWrite(file2));
    File.Delete(file2);

    var objectMetadata = await store.GetInfoAsync("k1");
    Console.WriteLine(objectMetadata);

    await store.DeleteAsync("k1");

    Console.WriteLine(i);
    memoryMb = Process.GetCurrentProcess().PrivateMemorySize64 / meg;
    allocatedMb = GC.GetTotalAllocatedBytes() / meg;
    Console.WriteLine($"memoryMb: {memoryMb:n2} MB");
    Console.WriteLine($"allocatedMb: {allocatedMb:n2} MB");
    GC.Collect();
}

Console.WriteLine("👋 BYE");
Console.WriteLine(FileVersionInfo.GetVersionInfo(typeof(NatsConnection).Assembly.Location).ProductVersion);
