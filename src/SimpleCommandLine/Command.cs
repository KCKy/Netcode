using System.Formats.Tar;
using System.Net;

namespace SimpleCommandLine;

public static class Command
{
    static char? TryGetCommand()
    {
        string? input = Console.ReadLine();
        if (input is { Length: 1 })
            return input.ToLowerInvariant()[0];
        return null;
    }

    static Action Info(string info) => () => Console.Write(info);

    public static char GetCommand(string info) => GetCommand(Info(info));
    public static char GetCommand(Action info)
    {
        while (true)
        {
            info();

            if (TryGetCommand() is {} command)
                return command;
        }
    }

    public static IPEndPoint GetEndPoint(string info, IPAddress defaultAddress) => GetEndPoint(Info(info), defaultAddress);
    public static IPEndPoint GetEndPoint(Action info, IPAddress defaultAddress)
    {
        while (true)
        {
            info();

            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                return new (defaultAddress, DefaultPort);

            if (IPEndPoint.TryParse(input, out IPEndPoint? point))
                return point;
        }
    }

    public static int DefaultPort => 13675;

    public static int GetPort(string info) => GetPort(Info(info));

    public static int GetPort(Action info)
    {
        const int minPort = ushort.MinValue;
        const int maxPort = ushort.MaxValue;

        while (true)
        {
            info();

            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                return DefaultPort;

            if (int.TryParse(input, out int port) && port is >= minPort and <= maxPort)
                return port;
        }
    }

    public static float GetFloat(string info) => GetFloat(Info(info));
    public static float GetFloat(Action info)
    {
        while (true)
        {
            info();

            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                return DefaultPort;

            if (float.TryParse(input, out float value))
                return value;
        }
    }

    public static long GetLong(string info) => GetLong(Info(info));
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
