using System.Net;
using Kcky.GameNewt.Client;
using Kcky.GameNewt.Server;
using Kcky.GameNewt.Transport.Default;

namespace Basic;

class Program
{
    static async Task RunClientAsync()
    {
        IPEndPoint serverAddress = new(IPAddress.Loopback, 42000);

        IpClientTransport transport = new(serverAddress);
        DefaultClientDispatcher dispatcher = new(transport);

        Client<ClientInput, ServerInput, GameState> client = new(dispatcher)
        {
            Displayer = new Displayer(),
            ClientInputProvider = new InputProvider()
        };

        Task result = await Task.WhenAny(client.RunAsync(), transport.RunAsync());

        transport.Terminate();
        client.Terminate();

        await result;
    }

    static async Task RunServerAsync()
    {
        IPEndPoint serverAddress = new(IPAddress.Any, 42000);
        IpServerTransport transport = new(serverAddress);
        DefaultServerDispatcher dispatcher = new(transport);

        Server<ClientInput, ServerInput, GameState> server = new(dispatcher);

        Task result = await Task.WhenAny(server.RunAsync(), transport.RunAsync());

        server.Terminate();

        await result;
    }

    static async Task Main()
    {
        Console.WriteLine("Run client or server [c/s]? ");
        char? command = Console.ReadLine()?.ToLower()[0];
        
        switch (command)
        {
            case 's':
                await RunServerAsync();
                return;
            default:
                await RunClientAsync();
                return;
        }
    }
}
