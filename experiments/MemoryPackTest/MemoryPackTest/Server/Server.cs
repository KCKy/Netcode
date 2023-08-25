using System;
using System.Threading.Tasks;
using FrameworkTest.Extensions;
using MemoryPack;

namespace FrameworkTest;

public sealed class Server<TPlayerInput, TServerInput, TGameState> : IDisposable
    where TGameState : IGameState<TPlayerInput, TServerInput>, new()
    where TPlayerInput : notnull, new()
{
    readonly IServerInputProvider<TServerInput> inputProvider_;
    
    readonly TimeSpan frameInterval_ = TimeSpan.FromSeconds(1d / TGameState.DesiredTickRate);

    readonly IServerSession session_;

    readonly PlayerManager<TPlayerInput> manager_;

    TGameState currentState_ = new();

    public Server(IServerSession session, IServerInputProvider<TServerInput> inputProvider)
    {
        session_ = session;
        inputProvider_ = inputProvider;
        manager_ = new(session);

        session_.OnClientConnect += ClientConnect;
        session_.OnClientDisconnect += manager_.TerminatePlayer;
        session_.OnClientInput += manager_.AddPlayerInput;
    }

    public IServerDisplayer<TPlayerInput, TServerInput, TGameState>? Displayer { get; set; } = null;

    void ClientConnect(long id)
    {
        long frame;
        byte[] bin;

        lock (stateLock_)
        {
            frame = manager_.Frame;
            manager_.AddPlayer(id);
            bin = MemoryPackSerializer.Serialize(currentState_);
        }

        session_.InitiatePlayer(id, frame, bin);
    }

    public void Dispose()
    {
        session_.OnClientDisconnect -= ClientConnect;
        session_.OnClientDisconnect -= manager_.TerminatePlayer;
        session_.OnClientInput -= manager_.AddPlayerInput;
    }

    readonly object stateLock_ = new();

    public async Task RunAsync()
    {
        await session_.StartAsync();
        TServerInput serverInput = inputProvider_.GetInitialInput();

        while (true)
        {
            Task delay = Task.Delay(frameInterval_);

            UpdateOutput updateOutput;

            lock (stateLock_)
            {
                var playerInputs = manager_.ConstructAuthoritativeInputFrame();

                byte[] bin = MemoryPackSerializer.Serialize<Input<TPlayerInput, TServerInput>>(new(serverInput, playerInputs));

                manager_.SendGameInput(bin, manager_.Frame);

                updateOutput = currentState_.Update(new(serverInput, playerInputs));
            }

            Displayer?.AddFrame(currentState_, manager_.Frame);

            serverInput = inputProvider_.GetInput();

            foreach (long client in updateOutput.ClientsToTerminate)
                session_.TerminatePlayer(client);

            if (updateOutput.ShallStop)
                return;

            await delay;
        }
    }
}
