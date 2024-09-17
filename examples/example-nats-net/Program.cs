using System.Diagnostics;
using System.Text;
using example_alib;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.KeyValueStore;
using NATS.Client.ObjectStore;
using NATS.Client.Serializers.Json;

var stopwatch = Stopwatch.StartNew();

Console.OutputEncoding = Encoding.UTF8;
{
    await using var nats = new NatsConnection(NatsOpts.Default with
    {
        SerializerRegistry = NatsJsonSerializerRegistry.Default,
        Url = Constants.Url
    });

    var js = new NatsJSContext(nats);
    var kv = new NatsKVContext(js);

    try
    {
        await kv.DeleteStoreAsync("shop_orders");
    }
    catch (NatsJSApiException e)
    {
        if (e.Error.Code != 404)
            throw;
    }

    var store = await kv.CreateStoreAsync("shop_orders");

    await store.CreateAsync("order-1", new ShopOrder(Id: 0));

    var entry = await store.GetEntryAsync<ShopOrder>("order-1");

    Console.WriteLine($"[GET] {entry.Value}");

    var tcs = new TaskCompletionSource();

    const int max = 1000;

    var watcher = Task.Run(async () =>
    {
        var count = 0;
        await foreach (var entry1 in store.WatchAsync<ShopOrder>())
        {
            Console.WriteLine($"[RCV] {entry1}");

            if (entry1.Value?.Id == 0)
                tcs.SetResult();

            if (entry1.Value?.Id == -1)
                break;

            count++;
        }

        if (count != max)
            throw new Exception("watcher max");

        Console.WriteLine("[RCV] done");
    });

    await tcs.Task;

    for (var i = 1; i < max; i++)
    {
        await store.PutAsync("order-1", new ShopOrder(Id: i));
    }

    await store.PutAsync("order-1", new ShopOrder(Id: -1));

    await watcher;

    
}

{
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

        var file1 = @"c:/users/mtmk/bin/pfSense-CE-memstick-2.7.2-RELEASE-amd64.img.gz";
        var file2 = @"c:/users/mtmk/Downloads/pfSense-CE-memstick-2.7.2-RELEASE-amd64.img.gz";
        File.Delete(file2);

        await store.PutAsync("k1", File.OpenRead(file1));
        await store.GetAsync("k1", File.OpenWrite(file2));
        
        {
            await using var fs1 = File.OpenRead(file1);
            await using var fs2 = File.OpenRead(file2);
            var buf1 = new byte[1024 * 1024];
            var buf2 = new byte[1024 * 1024];
            while (true)
            {
                var len1 = await fs1.ReadAsync(buf1);
                var len2 = await fs2.ReadAsync(buf2);
                if (len1 != len2)
                    throw new Exception("compare: len");
                if (len1 == 0)
                    break;
                for (int j = 0; j < len1; j++)
                {
                    if (buf1[j] != buf2[j])
                        throw new Exception("compare: buf");
                }
            }
            Console.WriteLine("COMPARE OK");
        }
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
}

Console.WriteLine("👋 BYE");
Console.WriteLine(FileVersionInfo.GetVersionInfo(typeof(NatsConnection).Assembly.Location).ProductVersion);
Console.WriteLine($"took: {stopwatch.ElapsedMilliseconds}ms");
public record ShopOrder(int Id);