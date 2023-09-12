using Core.DataStructures;
using Core.Providers;
using Core.Utility;
using MemoryPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Server;

public sealed class Server<TPlayerInput, TServerInput, TGameState, TUpdateInfo>
    where TGameState : class, IGameState<TPlayerInput, TServerInput>, new()
    where TPlayerInput : class, new()
    where TServerInput : class, new()
{
    public IServerInputProvider<TServerInput, TUpdateInfo> inputProvider_ { get; set; } = new DefaultServerInputProvider<TServerInput, TUpdateInfo>();
    public IDisplayer<TGameState> displayer_ { get; set; } = new DefaultDisplayer<TGameState>();

    readonly ClientInputQueue<TPlayerInput> inputQueue_ = new();

    static readonly TimeSpan FrameInterval = TimeSpan.FromSeconds(1d / TGameState.DesiredTickRate);

    TGameState state_ = ObjectPool<TGameState>.Create();

    readonly object stateMutex_ = new();

    void ClientConnect(long id)
    {
        long frameIndex;
        byte[] frameBinary;

        lock (stateMutex_)
        {
            frameIndex = inputQueue_.Frame;
            inputQueue_.AddPlayer(id);
            frameBinary = MemoryPackSerializer.Serialize(inputQueue_.Frame);
        }

        // TODO: send init packet
    }

    void ClientDisconnect(long id)
    {
        lock (stateMutex_)
        {
            inputQueue_.RemovePlayer(id);
        }

        // TODO: more
    }

    public async Task RunAsync()
    {
        // TODO: move off thread pool

        while (true)
        {
            Task delay = Task.Delay(FrameInterval);

            UpdateOutput updateOutput;
            TServerInput serverInput = new(); // TODO: do

            lock (stateMutex_)
            {
                var clientInput = inputQueue_.ConstructAuthoritativeFrame();

                UpdateInput<TPlayerInput, TServerInput> input = new(clientInput, serverInput);

                //MemoryPackSerializer.Serialize(input, );
            }
        }
    }


}
