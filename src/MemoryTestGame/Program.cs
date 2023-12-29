using DefaultTransport.IpTransport;
using MemoryPack;
using Serilog;
using System.Net;
using Core;
using Core.Client;
using Core.Providers;
using Core.Server;
using DefaultTransport.Dispatcher;
using GameCommon;
using Useful;

namespace MemoryTestGame;

static class Program
{
    static Task Main(string[] args)
    {
        return IpGameLoader.Load<MockState, ClientInput, ServerInput>(args, 
            () => (null, new InputProvider(), null),
            () => (null, new InputProvider(), null, null),
            c => { },
            s => { },
            c => c.Terminate(),
            s => s.Terminate());
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
    const bool DoAction = false;
    const long ActionFrequency = 20;
    long counter_ = 0;

    public ClientInput GetInput()
    {
        return new()
        {
            ActionCounter = counter_++
        };
    }

    public ServerInput GetInput(MockState info)
    {
        return new()
        {
            ActionCounter = DoAction ? counter_++ / ActionFrequency : 0
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
    
    public static double DesiredTickRate => 2;
}
