using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using DefaultTransport;
using DefaultTransport.IpTransport;

namespace DefaultTransportTests;

public class IpTransport
{
    [Fact]
    public async Task TestConnection()
    {
        IPEndPoint endPoint = new(IPAddress.Loopback, 4242);

        IpServerTransport server = new(endPoint);
        IpClientTransport client = new(endPoint);

        Task serverTask = server.RunAsync();
        Task clientTask = client.RunAsync();

        await Task.Delay(1000);

        client.Terminate();
        server.Terminate();

        try
        {
            await serverTask;
        }
        catch (OperationCanceledException) { }

        try
        {
            await clientTask;
        }
        catch (OperationCanceledException) { }
    }
}
