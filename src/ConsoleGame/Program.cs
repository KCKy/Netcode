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
        var server = DefaultServerConstructor.Construct<ClientInput, ServerInput, GameState, UpdateInfo>(transport);

        Task serverTask = server.Start();
        
        transport.Start().AssureSuccess();

        await serverTask;
    }
}

partial class ClientInput
{

}

partial class ServerInput
{

}

struct UpdateInfo { }

[MemoryPackable]
partial class GameState : IGameState<ClientInput, ServerInput, UpdateInfo>
{
    public static double DesiredTickRate => 20;

    public UpdateOutput Update(UpdateInput<ClientInput, ServerInput> updateInputs, ref UpdateInfo info)
    {
        return UpdateOutput.Empty;
    }
}
