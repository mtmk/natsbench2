using msgserver;

Console.WriteLine("Message Server v0.1");

var server = new MessageServer();
await server.StartAsync();

while (true)
{
    var line = Console.ReadLine();
    if (line == "exit")
    {
        break;
    }
}

await server.StopAsync();
