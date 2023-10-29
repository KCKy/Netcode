using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using DefaultTransport.IpTransport;
using Serilog;
using Xunit.Abstractions;
using Useful;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using Serilog.Core;

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
        Log.Logger = new LoggerConfiguration().WriteTo.TestOutput(output).MinimumLevel.Debug().CreateLogger();
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(50)]
    public async Task TestConnection(int clientCount)
    {
        // Construct a server, connect N clients and then terminate.

        IPEndPoint endPoint = new(IPAddress.Loopback, 0);
        IpServerTransport server = new(endPoint);

        ConcurrentDictionary<long, byte> joined = new();

        server.OnClientJoin += id => Assert.True(joined.TryAdd(id, 0));
        server.OnClientFinish += id => Assert.True(joined.TryRemove(id, out _));

        Task serverTask = server.RunAsync();

        await Task.Delay(10);

        if (serverTask.IsCompleted)
            await serverTask;

        IPEndPoint target = new(IPAddress.Loopback, server.Port);
        var clients = ConstructClients(target, clientCount).ToArray();
        var clientTasks = RunClients(clients).ToArray();
        
        await Task.Delay(10);

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

        await Task.Delay(clientCount);

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
        
        await Task.Delay(10);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(5, 1)]
    [InlineData(10, 1)]
    [InlineData(20, 1)]
    [InlineData(100, 1)]
    [InlineData(1, 10)]
    [InlineData(2, 10)]
    [InlineData(5, 10)]
    [InlineData(10, 10)]
    [InlineData(20, 10)]
    public async Task TestClientReliable(int count, int clientCount)
    {
        IPEndPoint endPoint = new(IPAddress.Loopback, 0);
        IpServerTransport server = new(endPoint);
        Task serverTask = server.RunAsync();

        await Task.Delay(10);

        if (serverTask.IsCompleted)
            await serverTask;
        
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
        
        await Task.Delay(count * clientCount);
        
        for (int i = 1; i <= count; i++)
        {
            foreach (var client in clients)
            {
                var mem = ArrayPool<byte>.Shared.RentMemory(sizeof(int));
                Bits.Write(i, mem.Span);
                client.SendReliable(mem);
            }
        }

        await Task.Delay(clientCount * count);

        Assert.Equal(clientCount, idToExpectedValue.Count);
        foreach (int expected in idToExpectedValue.Values)
        {
            Assert.Equal(count, expected - 1);
        }
        
        TerminateClients(clients);
        server.Terminate();

        await Task.Delay(10);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(5, 1)]
    [InlineData(10, 1)]
    [InlineData(20, 1)]
    [InlineData(100, 1)]
    [InlineData(1, 10)]
    [InlineData(2, 10)]
    [InlineData(5, 10)]
    [InlineData(10, 10)]
    [InlineData(20, 10)]
    public async Task TestServerReliableBroadcast(int count, int clientCount)
    {
        IPEndPoint endPoint = new(IPAddress.Loopback, 0);
        IpServerTransport server = new(endPoint);
        Task serverTask = server.RunAsync();

        await Task.Delay(10);

        if (serverTask.IsCompleted)
            await serverTask;
        
        object readingLock = new();
        Dictionary<long, int> idToExpectedValue = new();

        IPEndPoint target = new(IPAddress.Loopback, server.Port);
        var clients = ConstructClients(target, clientCount).ToArray();
        _ = RunClients(clients).Count();

        foreach ((int index, var client) in clients.WithIndexes())
        {
            int indexCapture = index;
            client.OnReliableMessage += message =>
            {
                lock (readingLock)
                {
                    int read = Bits.ReadInt(message.Span);
                    ArrayPool<byte>.Shared.Return(message);

                    if (!idToExpectedValue.TryGetValue(indexCapture, out int expected))
                    {
                        expected = 1;
                        idToExpectedValue.Add(indexCapture, expected);
                    }

                    Assert.Equal(expected, read);
                    idToExpectedValue[indexCapture] = expected + 1;
                }
            };
        }
        
        await Task.Delay(10);
        
        for (int i = 1; i <= count; i++)
        {
            var mem = ArrayPool<byte>.Shared.RentMemory(sizeof(int));
            Bits.Write(i, mem.Span);
            server.SendReliable(mem);
        }

        await Task.Delay(clientCount * count);

        Assert.Equal(clientCount, idToExpectedValue.Count);
        foreach (int expected in idToExpectedValue.Values)
        {
            Assert.Equal(count, expected - 1);
        }
        
        TerminateClients(clients);
        server.Terminate();

        await Task.Delay(10);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(5, 1)]
    [InlineData(10, 1)]
    [InlineData(20, 1)]
    [InlineData(100, 1)]
    [InlineData(1, 10)]
    [InlineData(2, 10)]
    [InlineData(5, 10)]
    [InlineData(10, 10)]
    [InlineData(20, 10)]
    public async Task TestServerReliableUnicast(int count, int clientCount)
    {
        IPEndPoint endPoint = new(IPAddress.Loopback, 0);
        IpServerTransport server = new(endPoint);

        List<long> clientIds = new();

        server.OnClientJoin += id =>
        {
            lock (clientIds)
                clientIds.Add(id);
        };

        Task serverTask = server.RunAsync();

        await Task.Delay(10);

        if (serverTask.IsCompleted)
            await serverTask;
        
        object readingLock = new();
        Dictionary<long, int> idToExpectedValue = new();

        IPEndPoint target = new(IPAddress.Loopback, server.Port);
        var clients = ConstructClients(target, clientCount).ToArray();
        
        foreach ((int index, var client) in clients.WithIndexes())
        {
            int indexCapture = index;
            client.OnReliableMessage += message =>
            {
                lock (readingLock)
                {
                    int read = Bits.ReadInt(message.Span);
                    ArrayPool<byte>.Shared.Return(message);

                    if (!idToExpectedValue.TryGetValue(indexCapture, out int expected))
                    {
                        expected = 1;
                        idToExpectedValue.Add(indexCapture, expected);
                    }

                    Assert.Equal(expected, read);
                    idToExpectedValue[indexCapture] = expected + 1;
                }
            };
        }

        _ = RunClients(clients).Count();
        
        await Task.Delay(10);

        Assert.Equal(clientCount, clientIds.Count);
        
        for (int i = 1; i <= count; i++)
        {
            var mem = ArrayPool<byte>.Shared.RentMemory(sizeof(int));
            Bits.Write(i, mem.Span);

            foreach (var id in clientIds)
                server.SendReliable(mem, id);

            await Task.Delay(1);
        }

        await Task.Delay(clientCount * count);

        Assert.Equal(clientCount, idToExpectedValue.Count);
        foreach (int expected in idToExpectedValue.Values)
        {
            Assert.Equal(count, expected - 1);
        }
        
        TerminateClients(clients);
        server.Terminate();

        await Task.Delay(10);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(5, 1)]
    [InlineData(10, 1)]
    [InlineData(20, 1)]
    [InlineData(100, 1)]
    [InlineData(1, 10)]
    [InlineData(2, 10)]
    [InlineData(5, 10)]
    [InlineData(10, 10)]
    [InlineData(20, 10)]
    public async Task TestUnreliable(int pingCount, int clientCount)
    {
        IPEndPoint endPoint = new(IPAddress.Loopback, 0);
        IpServerTransport server = new(endPoint);

        server.OnUnreliableMessage += (id, message) =>
        {
            server.SendUnreliable(message, id);
        };

        Task serverTask = server.RunAsync();

        await Task.Delay(10);

        if (serverTask.IsCompleted)
            await serverTask;
        
        object readingLock = new();
        Dictionary<long, int> idToResponses = new();

        IPEndPoint target = new(IPAddress.Loopback, server.Port);
        var clients = ConstructClients(target, clientCount).ToArray();
        
        foreach ((int index, var client) in clients.WithIndexes())
        {
            int indexCapture = index;
            client.OnUnreliableMessage += message =>
            {
                lock (readingLock)
                {
                    Assert.Equal(42, Bits.ReadInt(message.Span));

                    ArrayPool<byte>.Shared.Return(message);

                    if (!idToResponses.TryGetValue(indexCapture, out int responses))
                        idToResponses.Add(indexCapture, responses);

                    idToResponses[indexCapture] = responses + 1;
                }
            };
        }

        _ = RunClients(clients).Count();
        
        await Task.Delay(10);


        for (int i = 1; i <= pingCount; i++)
        {
            foreach (var client in clients)
            {
                var mem = ArrayPool<byte>.Shared.RentMemory(client.UnreliableMessageHeader + sizeof(int));
                Bits.Write(42, mem.Span[client.UnreliableMessageHeader..]);
                client.SendUnreliable(mem);
            }
        }

        await Task.Delay(pingCount * clientCount);

        Assert.Equal(clientCount, idToResponses.Count);

        foreach (int responses in idToResponses.Values)
        {
            Assert.InRange(responses, pingCount * 0.95, float.PositiveInfinity);
        }

        TerminateClients(clients);
        server.Terminate();

        await Task.Delay(10);
    }

    [Theory]

    [InlineData(10, 1)]
    [InlineData(20, 1)]
    [InlineData(100, 1)]
    [InlineData(10, 10)]
    [InlineData(20, 10)]
    public async Task TestUnreliableServerBroadcast(int pingCount, int clientCount)
    {
        IPEndPoint endPoint = new(IPAddress.Loopback, 0);
        IpServerTransport server = new(endPoint);
        
        Task serverTask = server.RunAsync();

        await Task.Delay(10);

        if (serverTask.IsCompleted)
            await serverTask;
        
        object readingLock = new();
        Dictionary<long, int> idToMessages = new();

        IPEndPoint target = new(IPAddress.Loopback, server.Port);
        var clients = ConstructClients(target, clientCount).ToArray();
        
        foreach ((int index, var client) in clients.WithIndexes())
        {
            int indexCapture = index;
            client.OnUnreliableMessage += message =>
            {
                lock (readingLock)
                {
                    Assert.Equal(42, Bits.ReadInt(message.Span));

                    ArrayPool<byte>.Shared.Return(message);

                    if (!idToMessages.TryGetValue(indexCapture, out int responses))
                        idToMessages.Add(indexCapture, responses);

                    idToMessages[indexCapture] = responses + 1;
                }
            };
        }

        _ = RunClients(clients).Count();
        
        await Task.Delay(10);

        foreach (var client in clients)
        {
            var mem = ArrayPool<byte>.Shared.RentMemory(client.UnreliableMessageHeader);
            client.SendUnreliable(mem);
        }

        await Task.Delay(10);

        Memory<byte> template = new byte[server.UnreliableMessageHeader + sizeof(int)];
        Bits.Write(42, template.Span[server.UnreliableMessageHeader..]);

        for (int i = 1; i <= pingCount; i++)
        {
            var mem = ArrayPool<byte>.Shared.RentMemory(template.Length);
            template.CopyTo(mem);
            server.SendUnreliable(mem);
        }

        await Task.Delay(pingCount * clientCount);

        Assert.Equal(clientCount, idToMessages.Count);

        foreach (int messages in idToMessages.Values)
        {
            Assert.InRange(messages, pingCount * 0.95, float.PositiveInfinity);
        }

        TerminateClients(clients);
        server.Terminate();

        await Task.Delay(10);
    }
}
