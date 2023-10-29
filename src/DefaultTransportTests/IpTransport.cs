using System.Net;
using DefaultTransport.IpTransport;
using Serilog;
using Xunit.Abstractions;

namespace DefaultTransportTests;

public class IpTransport
{
    static IEnumerable<IpClientTransport> ConstructClients(IPEndPoint endPoint, int count) =>
        from endpoint in Enumerable.Repeat(endPoint, count) select new IpClientTransport(endpoint);

    static IEnumerable<Task> RunClients(IEnumerable<IpClientTransport> clients) =>
        from client in clients select client.RunAsync();

    static void TerminateClients(IEnumerable<IpClientTransport> clients)
    {
        foreach (var client in clients)
            client.Terminate();
    }

    public IpTransport(ITestOutputHelper output)
    {
        Log.Logger = new LoggerConfiguration().WriteTo.TestOutput(output).MinimumLevel.Verbose().CreateLogger();
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(100)]
    public async Task TestConnection(int clientCount)
    {
        // Construct a server, connect N clients and then terminate.

        IPEndPoint endPoint = new(IPAddress.Loopback, 0);

        IpServerTransport server = new(endPoint);

        Task serverTask = server.RunAsync();

        await Task.Delay(100);

        IPEndPoint target = new(IPAddress.Loopback, server.Port);

        int properlyEnded = 0;

        var clients = ConstructClients(target, clientCount).ToArray();

        var clientTasks = RunClients(clients).ToArray();
        
        await Task.Delay(1000);

        TerminateClients(clients);

        foreach (var task in clientTasks)
        {
            try
            {
                await task;

            }
            catch (OperationCanceledException)
            {
                properlyEnded++;
            }
        }

        await Task.Delay(1000);

        server.Terminate();

        try
        {
            await serverTask;
        }
        catch (OperationCanceledException)
        {
            properlyEnded++;
        }

        Assert.Equal(1 + clientCount, properlyEnded);
        
        await Task.Delay(100);
    }
}
