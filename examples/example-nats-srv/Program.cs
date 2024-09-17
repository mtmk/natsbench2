using System.Diagnostics;
using System.Text;
using example_alib;
using NATS.Client.Core;
using NATS.Client.Serializers.Json;

Console.OutputEncoding = Encoding.UTF8;

await using var nats = new NatsConnection(NatsOpts.Default with
{
    Url = Constants.Url
});



Console.WriteLine("👋 BYE");
Console.WriteLine(FileVersionInfo.GetVersionInfo(typeof(NatsConnection).Assembly.Location).ProductVersion);
