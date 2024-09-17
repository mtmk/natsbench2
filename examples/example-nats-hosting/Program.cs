using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Extensions.Microsoft.DependencyInjection;

Console.OutputEncoding = Encoding.UTF8;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureLogging((_, logging) =>
    {
        logging.AddConsole();
    })
    .ConfigureServices((_, services) =>
    {
        services.AddNatsClient();
        services.AddHostedService<MyService>();
    });

using (var host = builder.Build())
{
    await host.StartAsync();

    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    var nats = host.Services.GetRequiredService<INatsConnection>();

    logger.LogInformation("Starting...");

    for (var i = 0; i < 10; i++)
    {
        await Task.Delay(250);
        await nats.PublishAsync($"events.tick.{i}", $"Tick {DateTime.Now}");
    }

    logger.LogInformation("Stopping...");

    await host.StopAsync();

    logger.LogInformation("Bye");
    logger.LogInformation("NATS.Net v{Version}", FileVersionInfo.GetVersionInfo(typeof(NatsConnection).Assembly.Location).ProductVersion);
}

Console.WriteLine("👋 BYE");
Console.WriteLine(FileVersionInfo.GetVersionInfo(typeof(NatsConnection).Assembly.Location).ProductVersion);

class MyService(INatsConnection nats, ILogger<MyService> logger) : IHostedService, IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private CancellationTokenSource? _cts2;
    private Task? _task;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting..");
        
        _cts2 = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);

        _task = Task.Run(async () =>
        {
            logger.LogInformation("Subscription starting...");

            await foreach (var msg in nats.SubscribeAsync<string>("events.>", cancellationToken: _cts2.Token))
            {
                logger.LogInformation("Received {Subject}: {Data}", msg.Subject, msg.Data);
            }
            
            logger.LogInformation("Subscription ended");
        }, cancellationToken);
        
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping...");

        await _cts.CancelAsync();
        
        if (_task != null)
            await _task;
        
        logger.LogInformation("Bye");
    }

    public void Dispose()
    {
        logger.LogInformation("Disposing...");
        _cts.Dispose();
        _cts2?.Dispose();
        _task?.Dispose();
        logger.LogInformation("Disposed");
    }
}