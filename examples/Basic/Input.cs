using Kcky.GameNewt.Providers;

namespace Basic;

class InputProvider : IClientInputProvider<ClientInput>
{
    public ClientInput GetInput()
    {
        ClientInput input = new();

        while (Console.KeyAvailable)
        {
            ConsoleKeyInfo read = Console.ReadKey(true);
            switch (read.Key)
            {
                case ConsoleKey.A or ConsoleKey.LeftArrow:
                    input.Direction = Direction.Left;
                    break;
                case ConsoleKey.W or ConsoleKey.RightArrow:
                    input.Direction = Direction.Up;
                    break;
                case ConsoleKey.S or ConsoleKey.DownArrow:
                    input.Direction = Direction.Down;
                    break;
                case ConsoleKey.D or ConsoleKey.RightArrow:
                    input.Direction = Direction.Right;
                    break;
                case ConsoleKey.Spacebar:
                    input.PlaceFlag = true;
                    break;
            }
        }

        return input;
    }
}