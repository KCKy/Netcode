using System.Globalization;
using System.Net;

namespace Useful;

public static class Command
{
    static Action Info(string info) => () => Console.Write(info);

    public static char GetCommand(string info) => GetCommand(Info(info));
    public static char GetCommand(Action info)
    {
        while (true)
        {
            info();

            if (Console.ReadLine() is { Length: > 0 } command )
                return command.ToLowerInvariant()[0];
        }
    }

    public static IPEndPoint GetEndPoint(string info, IPEndPoint defaultPoint) => GetEndPoint(Info(info), defaultPoint);
    public static IPEndPoint GetEndPoint(Action info, IPEndPoint defaultPoint)
    {
        while (true)
        {
            info();

            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                return defaultPoint;
            
            if (IPEndPoint.TryParse(input, out IPEndPoint? point))
                return point;
        }
    }

    public static int GetPort(string info, int defaultPort) => GetPort(Info(info), defaultPort);

    public static int GetPort(Action info, int defaultPort)
    {
        const int minPort = ushort.MinValue;
        const int maxPort = ushort.MaxValue;

        while (true)
        {
            info();

            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                return defaultPort;

            if (int.TryParse(input, CultureInfo.InvariantCulture, out int port) && port is >= minPort and <= maxPort)
                return port;
        }
    }

    public static float GetFloat(string info, float defaultValue) => GetFloat(Info(info), defaultValue);
    public static float GetFloat(Action info, float defaultValue)
    {
        while (true)
        {
            info();

            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                return defaultValue;

            if (float.TryParse(input, CultureInfo.InvariantCulture, out float value))
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

            if (long.TryParse(input, CultureInfo.InvariantCulture, out long value))
                return value;
        }
    }
}
