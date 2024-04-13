using System;
using System.Globalization;
using System.Net;

namespace Kcky.Useful;

/// <summary>
/// Provides utility command line methods for simple CLI.
/// </summary>
public static class Command
{
    /// <summary>
    /// Asks the user via <see cref="Console"/> to provide an <see cref="IPEndPoint"/>. If no input is provided <paramref name="defaultPoint"/> is used.
    /// If the user provides an invalid string, the function asks again. This functions blocks until the input is provided.
    /// </summary>
    /// <param name="info">The asking message to write to console.</param>
    /// <param name="defaultPoint">Default value if no value is provided by the user.</param>
    /// <returns>A user-provided valid ip end point.</returns>
    public static IPEndPoint GetEndPoint(string info, IPEndPoint? defaultPoint = null)
    {
        defaultPoint ??= new(IPAddress.Loopback, 0);

        while (true)
        {
            Console.Write(info);

            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                return defaultPoint;
            
            if (IPEndPoint.TryParse(input, out IPEndPoint? point))
                return point;
        }
    }

    /// <summary>
    /// Asks the user via <see cref="Console"/> to provide a port. If no input is provided <paramref name="defaultPort"/> is used.
    /// If the user provides an invalid string, the function asks again. This functions blocks until the input is provided.
    /// </summary>
    /// <param name="info">The asking message to write to console.</param>
    /// <param name="defaultPort">Default value if no value is provided by the user.</param>
    /// <returns>A user-provided valid port.</returns>
    public static int GetPort(string info, int defaultPort = 0)
    {
        const int minPort = IPEndPoint.MinPort;
        const int maxPort = IPEndPoint.MaxPort;

        while (true)
        {
            Console.Write(info);

            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                return defaultPort;

            if (int.TryParse(input, CultureInfo.InvariantCulture, out int port) && port is >= minPort and <= maxPort)
                return port;
        }
    }

    /// <summary>
    /// Asks the user via <see cref="Console"/> to provide a port. If no input is provided <paramref name="defaultValue"/> is used.
    /// If the user provides an invalid string, the function asks again. This functions blocks until the input is provided.
    /// </summary>
    /// <param name="info">The asking message to write to console.</param>
    /// <param name="defaultValue">Default value if no value is provided by the user.</param>
    /// <returns>A user-provided float.</returns>
    public static float GetFloat(string info, float defaultValue = 0)
    {
        while (true)
        {
            Console.Write(info);

            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                return defaultValue;

            if (float.TryParse(input, CultureInfo.InvariantCulture, out float value))
                return value;
        }
    }
}
