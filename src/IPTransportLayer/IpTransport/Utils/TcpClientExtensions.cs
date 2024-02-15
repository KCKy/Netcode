using System;
using System.Net.Sockets;

namespace DefaultTransport.IpTransport;

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
