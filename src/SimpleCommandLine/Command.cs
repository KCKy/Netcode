using System.Net;

namespace SimpleCommandLine;

public static class Command
{
    public static char? TryGetCommand()
    {
        string? input = Console.ReadLine();
        if (input is { Length: 1 })
            return input.ToLowerInvariant()[0];
        return null;
    }

    public static char GetCommand(Action info)
    {
        while (true)
        {
            info();

            if (TryGetCommand() is {} command)
                return command;
        }
    }

    public static IPEndPoint GetEndPoint(Action info)
    {
        while (true)
        {
            info();

            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                return IPEndPoint.Parse("127.0.0.1:12345");

            if (IPEndPoint.TryParse(input, out IPEndPoint? point))
                return point;
        }
    }

    public static long GetLong(Action info)
    {
        while (true)
        {
            info();

            string? input = Console.ReadLine();

            if (input is null)
                continue;

            if (long.TryParse(input, out long value))
                return value;
        }
    }
}
