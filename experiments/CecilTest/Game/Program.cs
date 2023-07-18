using Framework;
namespace Game;

static class Program
{
    static void Main()
    {
        Game initial = new();
        GameRunner<Game> runner = new(Game.Print, initial);
        runner.Run();
    }
}
