using GameCommon;
using Useful;

namespace SnakeGame;

static class Program
{
    static Task Main(string[] args)
    {
        Displayer? displayer = null;

        Task game = IpGameLoader.Load<GameState, ClientInput, ServerInput>(args,
            () => (null, new ServerInputProvider(), null),
            () =>
            {
                displayer = new("Top Down Shooter Demo");
                ClientInputProvider input = new(displayer.Window);
                return (displayer, input, null, new ServerInputPredictor());
            },
            c => displayer!.Client = c,
            s => { },
            c => c.Terminate(),
            s => s.Terminate());

        if (displayer is null)
            return game;
        
        game.AssureSuccess();
        while (displayer.Run()) { }

        return Task.CompletedTask;
    }
}
