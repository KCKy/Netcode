using GameCommon;
using TopDownShooter.Game;
using TopDownShooter.Input;
using TopDownShooter.Display;
using Useful;

namespace TopDownShooter;

static class Program
{
    static Task Main(string[] args)
    {
        Displayer? displayer = null;

        Task run = IpGameLoader.Load<GameState, ClientInput, ServerInput>(args, 
            () => (null, null, new ClientInputPredictor()),
            () =>
            {
                displayer = new("Top Down Shooter Demo");
                ClientInputProvider input = new(displayer);
                return (displayer, input, new ClientInputPredictor(), null);
            },
            c => displayer!.Client = c,
            s => { },
            c => c.Terminate(),
            s => s.Terminate());

        if (displayer is null)
            return run;

        run.AssureSuccess();

        while (displayer.Run()) { }

        return Task.CompletedTask;
    }
}
