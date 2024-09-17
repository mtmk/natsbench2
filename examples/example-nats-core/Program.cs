using System.Diagnostics;
using System.Text;
using example_alib;
using NATS.Client.Core;
using NATS.Net;

Console.OutputEncoding = Encoding.UTF8;
{
    await using var nats = new NatsConnection(new NatsOpts { Url = Constants.Url });

    nats.MessageDropped += async (_, e) =>
    {
        await File.AppendAllTextAsync("c:/tmp/log.txt", $"[DROP] {e.Subject}: {e.Data}\n");
    };

    const int max = 1000;
    var sync = 0;

    var sub = Task.Run(async () =>
    {
        var count = 0;
        await foreach (var msg in nats.SubscribeAsync<string>("data.*"))
        {
            if (msg.Subject == "data.sync")
            {
                Interlocked.Increment(ref sync);
                continue;
            }

            if (msg.Data == null)
            {
                break;
            }

            count++;

            Console.WriteLine($"[SUB] Received: {msg.Subject}: {msg.Data}");
        }

        if (count != max)
        {
            throw new Exception("subscriber max");
        }

        Console.WriteLine($"[SUB] done (count={count})");
    });

    while (Volatile.Read(ref sync) == 0)
    {
        await nats.PublishAsync("data.sync");
    }

    for (var i = 0; i < max; i++)
    {
        await nats.PublishAsync($"data.{i}", $"[{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fff}] {i:D5}");
    }

    Console.WriteLine("Publishing done. Signaling for subscriber to finish...");

    await nats.PublishAsync("data.done");

    await sub;

    Console.WriteLine("👋 BYE");
    Console.WriteLine(FileVersionInfo.GetVersionInfo(typeof(NatsConnection).Assembly.Location).ProductVersion);
}

// Client
{
    await using var nats = new NatsClient(Constants.Url);

    const int max = 1000;
    var sync = 0;

    var sub = Task.Run(async () =>
    {
        var count = 0;
        await foreach (var msg in nats.SubscribeAsync<string>("data.*"))
        {
            if (msg.Subject == "data.sync")
            {
                Interlocked.Increment(ref sync);
                continue;
            }

            if (msg.Data == null)
            {
                break;
            }

            count++;

            Console.WriteLine($"[SUB] Received: {msg.Subject}: {msg.Data}");
        }

        if (count != max)
        {
            throw new Exception("subscriber max");
        }

        Console.WriteLine($"[SUB] done (count={count})");
    });

    while (Volatile.Read(ref sync) == 0)
    {
        await nats.PublishAsync("data.sync");
    }

    for (var i = 0; i < max; i++)
    {
        await nats.PublishAsync($"data.{i}", $"[{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fff}] {i:D5}");
    }

    Console.WriteLine("Publishing done. Signaling for subscriber to finish...");

    await nats.PublishAsync("data.done");

    await sub;

    Console.WriteLine("👋 BYE");
    Console.WriteLine(FileVersionInfo.GetVersionInfo(typeof(NatsConnection).Assembly.Location).ProductVersion);
}
