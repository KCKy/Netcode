using Framework;
namespace Game;

public class Game : IGameState
{
    public int value_;

    public static void Print(Game game)
    {
        Console.WriteLine(game.value_);
    }
}
