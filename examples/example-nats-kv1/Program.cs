using System.Diagnostics;
using System.Text;
using example_alib;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.KeyValueStore;
using NATS.Client.Serializers.Json;

Console.OutputEncoding = Encoding.UTF8;

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
        throw new Exception($"watcher max max={max} != count={count}");
    
    Console.WriteLine("[RCV] done");
});

await tcs.Task;

for (var i = 1; i < max; i++)
{
    await store.PutAsync("order-1", new ShopOrder(Id: i));
}

await store.PutAsync("order-1", new ShopOrder(Id: -1));

await watcher;

Console.WriteLine("👋 BYE");
Console.WriteLine(FileVersionInfo.GetVersionInfo(typeof(NatsConnection).Assembly.Location).ProductVersion);

public record ShopOrder(int Id);
