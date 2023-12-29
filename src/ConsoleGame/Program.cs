﻿using GameCommon;
using Useful;

namespace TestGame;

static class Program
{
    static Task Main(string[] args)
    {
        Displayer? displayer = null;

        Task game = IpGameLoader.Load<GameState, ClientInput, ServerInput>(args,
            () => (null, null, null),
            () =>
            {
                displayer = new("Top Down Shooter Demo");
                ClientInputProvider input = new(displayer.Window);
                return (displayer, input, null, null);
            },
            c => { },
            s => { },
            c => c.Terminate(),
            s => s.Terminate());

        if (displayer is null)
            return game;
        
        game.AssureSuccess();
        while (true)
            displayer.Update();
    }
}
