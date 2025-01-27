using System.Buffers.Text;
using System.Net;
using System.Net.Sockets;
using System.Reflection.Metadata.Ecma335;
using System.Text;

namespace msgserver;

public class MessageServer
{
    private readonly TcpListener _listener = new(IPAddress.Any, 4222);
    private readonly int _level = 8;

    public Task StartAsync()
    {
        // Start the listener and accept incoming connections
        _listener.Start();
        Log(1, "Server started on port 4222");
        
        Task.Run(() => AcceptConnectionsAsync(_listener));
        return Task.CompletedTask;
    }

    private Task AcceptConnectionsAsync(TcpListener listener)
    {
        // Accept incoming connections
        var clientId = 1;
        while (true)
        {
            var client = listener.AcceptTcpClient();
            Log(3, $"Client {clientId} connected");
            
            // Handle the client connection
            Task.Run(() => HandleClientAsync(client, clientId++));
        }        
    }

    private async Task HandleClientAsync(TcpClient client, int clientId)
    {
        var buffer = new char[1024];
        try
        {
            // Handle the client connection
            var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII);
            await using var writer = new StreamWriter(stream, Encoding.ASCII);
            writer.AutoFlush = true;
            writer.NewLine = "\r\n";

            Log(4, $"Sending to {clientId}: INFO");
            await writer.WriteLineAsync("""INFO {"max_payload":1048576}""");
            
            // Read messages from the client
            while (true)
            {
                ReadOnlySpan<char> message = await reader.ReadLineAsync();

                if (_level >= 9)
                {
                    Log(9, $"Received from {clientId}: {message}");
                }
                
                // PUB message processing
                ReadOnlySpan<char> protoPub = "PUB";
                if (message.StartsWith(protoPub))
                {
                    var i = message.LastIndexOf(' ');
                    
                    //var subject = parts;
                    var size = int.Parse(message.Slice(i + 1));
                    var total = 0;
                    
                    // Read into the buffer in a loop
                    while (true)
                    {
                        var read = await reader.ReadAsync(buffer, total, size + 2 - total);
                        total += read;
                        if (total >= size + 2)
                        {
                            break;
                        }
                    }

                    if (_level >= 9)
                    {
                        //Log(9, $"Received message from {clientId}: {subject} {size} bytes: {new string(buffer)}");
                    }
                    continue;
                }
                
                if (message == null)
                {
                    // Client disconnected
                    Log(3, $"Socket closed for {clientId}");
                    break;
                }

                // CONNECT message processing
                if (message.StartsWith("CONNECT"))
                {
                    continue;
                }
                
                // PING message processing
                if (message.StartsWith("PING"))
                {
                    Log(8, "Received PING");
                    if (_level >= 9)
                    {
                        Log(9, $"Sending to {clientId}: PONG");
                    }
                    await writer.WriteLineAsync("PONG");
                    continue;
                }

                // Unrecognized message
                Log(3, $"Unrecognized message from {clientId}: {message}");
                break;
            }
        }
        catch (Exception ex)
        {
            Log(3, $"Client {clientId} disconnected with error: {ex.Message}");
        }

        // Close the client connection
        client.Close();
        Log(3, $"Client {clientId} disconnected");
    }
    
    private void Log(int level, string message)
    {
        if (level > _level)
        {
            return;
        }
        Console.WriteLine($"[{level}] {message}");
    }

    public Task StopAsync()
    {
        Log(1, "Stopping server...");
        Log(1, "Bye.");
        return Task.CompletedTask;
    }
}