// https://github.com/nats-io/nats.net.v2/issues/375

using System.Text;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using System.Diagnostics;

Console.OutputEncoding = Encoding.UTF8;

var msgSize = args.Length > 0 ? int.Parse(args[0]) : 2048;
var msgCount = args.Length > 1 ? int.Parse(args[1]) : 100000;

int cnt = 0;
var data = new byte[msgSize].Select(a => (byte)(++cnt)).ToArray();

await using var nats = new NatsConnection(new NatsOpts
{
    Url = "127.0.0.1:4222",
    Name = "NATS-by-Example",
});

var js = new NatsJSContext(nats);

var stream = await js.CreateStreamAsync(new StreamConfig("test", ["test.subject"]));
await js.PurgeStreamAsync("test", new StreamPurgeRequest());

Stopwatch sw = new Stopwatch();
sw.Start();

const int batch = 100_000;
for (int i = 0; i < msgCount / batch; ++i)
{
    var tasks = new List<Task<PubAckResponse>>();
    
    for (int j = 0; j < batch; j++)
    {
        Task<PubAckResponse> publishAsync = js.PublishAsync<byte[]>(subject: "test.subject", data).AsTask();
        tasks.Add(publishAsync);
    }

    foreach (var task in tasks)
            await task;
}


sw.Stop();

Console.WriteLine($"Produced {msgCount} messages in {(int)sw.ElapsedMilliseconds} ms; {(msgCount / (sw.Elapsed.TotalSeconds) / 1000.0):F0}k msg/s ~ {(msgCount * msgSize) / (1024 * 1024 * sw.Elapsed.TotalSeconds):F0} MB/sec");

Console.WriteLine("👋 BYE");
Console.WriteLine(FileVersionInfo.GetVersionInfo(typeof(NatsConnection).Assembly.Location).ProductVersion);
