using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/*
class ClientSession<TGameState>
{
    public Task<(long frame, TGameState state)> ConnectAsync()
    {
        throw new NotImplementedException();
    }
}

class PlayerInputQueue<TGameState>
{
    readonly ClientSession<TGameState> session_;

    public PlayerInputQueue(ClientSession<TGameState> session)
    {
        session_ = session;
    }
}

public class Client<TPlayerInput, TServerInput, TServerOutput, TGameState>
    where TGameState : IGameState<TPlayerInput, TServerInput, TServerOutput>, new()
    where TServerOutput : IServerOutput
{
    readonly IPlayerInputProvider<TPlayerInput> provider_;

    readonly ClientSession<TGameState> session_ = new();

    TGameState authoritativeState_ = new();
    TGameState currentState_ = new();
    
    public Client(IPlayerInputProvider<TPlayerInput> provider)
    {
        provider_ = provider;
    }

    public async Task StartAsync()
    {
        (long frame, authoritativeState_) = await session_.ConnectAsync();
        currentState_ = authoritativeState_;

        while (true)
        {

        }
    }
}
*/
