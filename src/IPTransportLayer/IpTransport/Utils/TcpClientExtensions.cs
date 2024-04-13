using System;
using System.Net.Sockets;

namespace Kcky.GameNewt.Transport.Default;

static class TcpClientExtensions
{
    public static NetworkStream? TryGetStream(this TcpClient client)
    {
        try
        {
            return client.GetStream();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
