using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using example_alib;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

Console.OutputEncoding = Encoding.UTF8;

await using var nats = new NatsConnection(new NatsOpts{Url = Constants.Url});

var js = new NatsJSContext(nats);

try
{
    await js.DeleteStreamAsync("test-stream-1");
}
catch (NatsJSApiException e) when(e.Error.Code == 404)
{
}

var stream = await js.CreateStreamAsync(new StreamConfig("test-stream-1", new[]{"test-stream-1.*"})
{
    
});

const int max = 1000;

var publisher = Task.Run(async () =>
{
    for (int i = 0; i < max; i++)
    {
        var ack = await js.PublishAsync("test-stream-1.1", $"[{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fff}] {i:D5}");
        ack.EnsureSuccess();
    }

    {
        var ack = await js.PublishAsync("test-stream-1.1", $"done");
        ack.EnsureSuccess();
    }
    
    Console.WriteLine("[PUBLISHER] done");
});

var consumer1 = Task.Run(async () =>
{
    var consumer = await stream.CreateOrUpdateConsumerAsync(new ConsumerConfig("c1")
    {
        MaxAckPending = 4000
    });
    var count = 0;
    await foreach (var msg in consumer.ConsumeAsync<string>())
    {
        Console.WriteLine($"[CONSUMER] Received: {msg.Data}");
        await msg.AckAsync();
        if (msg.Data == "done")
            break;
        count++;
    }
    
    if (count != max)
        throw new Exception("consumer max");
});

var consumer2 = Task.Run(async () =>
{
    var orderedConsumer = await stream.CreateOrderedConsumerAsync();
    var count = 0;
    await foreach (var msg in orderedConsumer.ConsumeAsync<string>(opts: new NatsJSConsumeOpts
                   {
                       MaxMsgs = 100
                   }))
    {
        Console.WriteLine($"[ORDERED-CONSUMER] Received: {msg.Data}");
        if (msg.Data == "done")
            break;
        var n = int.Parse(Regex.Match(msg.Data!, @"(\d+)$").Groups[1].Value);
        if (count++ != n)
            throw new Exception("out of order");
    }
    
    if (count != max)
        throw new Exception("ordered consumer max");
});

await Task.WhenAll(publisher, consumer1, consumer2);

Console.WriteLine("👋 BYE");
Console.WriteLine(FileVersionInfo.GetVersionInfo(typeof(NatsConnection).Assembly.Location).ProductVersion);
