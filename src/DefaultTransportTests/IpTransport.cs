using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using DefaultTransport.IpTransport;
using Serilog;
using Xunit.Abstractions;
using Useful;

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

        ConcurrentDictionary<long, byte> joined = new();

        server.OnClientJoin += id => Assert.True(joined.TryAdd(id, 0));
        server.OnClientFinish += id => Assert.True(joined.TryRemove(id, out _));

        Task serverTask = server.RunAsync();

        await Task.Delay(100);

        IPEndPoint target = new(IPAddress.Loopback, server.Port);
        var clients = ConstructClients(target, clientCount).ToArray();
        var clientTasks = RunClients(clients).ToArray();
        
        await Task.Delay(100);

        Assert.Equal(clientCount, joined.Count);

        TerminateClients(clients);

        int properlyEnded = 0;

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

        Assert.True(joined.IsEmpty);
        Assert.Equal(1 + clientCount, properlyEnded);
        
        await Task.Delay(100);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(5, 1)]
    [InlineData(10, 1)]
    [InlineData(20, 1)]
    [InlineData(100, 1)]
    [InlineData(1000, 1)]
    [InlineData(0, 10)]
    [InlineData(1, 10)]
    [InlineData(2, 10)]
    [InlineData(5, 10)]
    [InlineData(10, 10)]
    [InlineData(20, 10)]
    [InlineData(100, 10)]
    public async Task TestClientReliable(int count, int clientCount)
    {
        IPEndPoint endPoint = new(IPAddress.Loopback, 0);
        IpServerTransport server = new(endPoint);
        _ = server.RunAsync();

        await Task.Delay(100);
        
        object readingLock = new();
        Dictionary<long, int> idToExpectedValue = new();

        server.OnReliableMessage += (cid, message) =>
        {
            lock (readingLock)
            {
                int read = Bits.ReadInt(message.Span);
                ArrayPool<byte>.Shared.Return(message);

                if (!idToExpectedValue.TryGetValue(cid, out int expected))
                {
                    expected = 1;
                    idToExpectedValue.Add(cid, expected);
                }

                Assert.Equal(expected, read);
                idToExpectedValue[cid] = expected + 1;
            }
        };


        IPEndPoint target = new(IPAddress.Loopback, server.Port);
        var clients = ConstructClients(target, clientCount).ToArray();
        _ = RunClients(clients).Count();
        
        await Task.Delay(100);
        
        await Task.Delay(100);
        
        for (int i = 1; i <= count; i++)
        {
            foreach (var client in clients)
            {
                var mem = ArrayPool<byte>.Shared.RentMemory(sizeof(int));
                Bits.Write(i, mem.Span);
                client.SendReliable(mem);
            }
        }

        await Task.Delay(100);

        foreach ((long id, int expected) in idToExpectedValue)
        {
            Assert.Equal(count, expected - 1);
        }
        
        TerminateClients(clients);
        server.Terminate();

        await Task.Delay(100);
    }
}
