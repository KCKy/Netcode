using System.Diagnostics;
using DefaultTransport.IpTransport;
using MemoryPack;
using Serilog;
using System.Net;
using Core;
using Core.Client;
using Core.Providers;
using Core.Server;
using DefaultTransport.Dispatcher;
using Useful;

namespace MemoryTestGame;

static class Program
{
    static Task Main()
    {
        Log.Logger = new LoggerConfiguration().WriteTo.Console().MinimumLevel.Verbose().CreateLogger();

        Console.WriteLine("Transport Tester");

        while (true)
        {
            switch (Command.GetCommand("Set mode ([c]lient, [s]erver): "))
            {
                case 'c':
                    return RunClientAsync();
                case 's':
                    return RunServerAsync();
                default:
                    Console.WriteLine("Unknown mode.");
                    continue;
            }
        }
    }

    const int DefaultPort = 13675;

    static async Task RunClientAsync()
    {
        IPEndPoint server = Command.GetEndPoint("Enter server IP address and port: ", new(IPAddress.Loopback, DefaultPort));
        IpClientTransport transport = new(server);

        DefaultClientDispatcher dispatcher = new(transport);

        Client<ClientInput, ServerInput, MockState> client = new(dispatcher, dispatcher, null, new InputProvider())
        {
            UseChecksum = true,
            PredictDelayMargin = 0.3
        };

        client.RunAsync().AssureSuccess();

        try
        {
            await transport.RunAsync();
        }
        finally
        {
            client.Terminate();
        }
    }

    static async Task RunServerAsync()
    {
        IPEndPoint local = Command.GetEndPoint("Enter local IP address and port: ", new(IPAddress.Any, DefaultPort));
        IpServerTransport transport = new(local);
        DefaultServerDispatcher dispatcher = new(transport);

        Server<ClientInput, ServerInput, MockState> server = new(dispatcher, dispatcher, serverProvider: new InputProvider())
        {
            SendChecksum = true
        };

        server.RunAsync().AssureSuccess();

        try
        {
            await transport.RunAsync();
        }
        finally
        {
            server.Terminate();
        }
    }
}

[MemoryPackable]
sealed partial class ClientInput
{
    public long ActionCounter;
}

[MemoryPackable]
sealed partial class ServerInput
{
    public long ActionCounter;
}

sealed class InputProvider : IClientInputProvider<ClientInput>, IServerInputProvider<ServerInput, MockState>
{
    const long ActionFrequency = 20;
    long counter_ = 0;

    public ClientInput GetInput()
    {
        return new()
        {
            ActionCounter = counter_++ / ActionFrequency
        };
    }

    public ServerInput GetInput(MockState info)
    {
        return new()
        {
            ActionCounter = counter_++ / ActionFrequency
        };
    }
}

[MemoryPackable]
sealed partial class MockState : IGameState<ClientInput, ServerInput>
{
    public long Index = 0;

    const int UpdateTime = 0;
    const int ExtraDataSize = 0;

    public byte[] ExtraData = new byte[ExtraDataSize];

    public UpdateOutput Update(UpdateInput<ClientInput, ServerInput> updateInputs)
    {
        Thread.SpinWait(UpdateTime);
        return UpdateOutput.Empty;
    }
    
    public static double DesiredTickRate => 20;
}
