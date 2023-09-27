using System.Net;
using Core;
using Core.Extensions;
using Core.Server;
using Core.Transport;
using DefaultTransport;
using DefaultTransport.Client;
using DefaultTransport.Server;
using DefaultTransport.TcpTransport;
using MemoryPack;

namespace ConsoleGame;

static class Program
{
    const int Port = 15965;

    static readonly IPEndPoint ServerPoint = new(IPAddress.Loopback, Port);

    static void Main()
    {

    }

    static async Task RunServer()
    {
        TcpServerTransport<IMessageToServer, IMessageToClient> transport = new(ServerPoint);
        var server = DefaultServerConstructor.Construct<ClientInput, ServerInput, GameState>(transport);

        Task serverTask = server.Start();
        
        transport.Start().AssureSuccess();

        await serverTask;
    }
}

[MemoryPackable]
partial class ClientInput
{

}

[MemoryPackable]
partial class ServerInput
{

}

[MemoryPackable]
partial class GameState : IGameState<ClientInput, ServerInput>
{
    public static double DesiredTickRate => 20;

    public UpdateOutput Update(UpdateInput<ClientInput, ServerInput> updateInputs)
    {
        return UpdateOutput.Empty;
    }
}
