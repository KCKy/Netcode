using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Kcky.Useful;

namespace Kcky.GameNewt.Transport.Default.Tests;

/// <summary>
/// Tests for <see cref="IpClientTransport"/> and <see cref="IpServerTransport"/>.
/// </summary>
/// <remarks>
/// These tests test whether the two corresponding classes work together.
///
/// These tests are not unit test as their outcome is undeterministic and dependent on external factors. Nonetheless, it is useful to assure functionality of the transport layer.
/// They are prone to fail when run back-to-back or currently.
/// </remarks>
public class IpTransportTests
{
    static IpClientTransport[] ConstructClients(IPEndPoint endPoint, int count)
    {
        return (from endpoint in Enumerable.Repeat(endPoint, count) select new IpClientTransport(endpoint)).ToArray();
    }

    static Task[] RunClients(IEnumerable<IpClientTransport> clients)
    {
        return (from client in clients select client.RunAsync()).ToArray();
    }

    static void TerminateClients(IEnumerable<IpClientTransport> clients)
    {
        foreach (var client in clients)
            client.Terminate();
    }

    ITestOutputHelper output_;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="output">Output for logging.</param>
    public IpTransportTests(ITestOutputHelper output)
    {
        output_ = output;
    }
    
    /// <summary>
    /// Test whether clients connecting work.
    /// </summary>
    /// <param name="clientCount">The number of clients to join.</param>
    /// <returns>Test run task.</returns>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
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
        var clients = ConstructClients(target, clientCount);
        var clientTasks = RunClients(clients);
        
        await Task.Delay(150);

        output_.WriteLine("[Test] Terminating clients.");

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

        await Task.Delay(20);

        Assert.Equal(clientCount, properlyEnded);

        output_.WriteLine("[Test] Terminating server.");

        server.Kick();

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
        
        await Task.Delay(20);
    }

    /// <summary>
    /// Test whether kicking clients work.
    /// </summary>
    /// <param name="clientCount">Number of clients to join and then kick.</param>
    /// <returns>Test run task.</returns>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public async Task TestKick(int clientCount)
    {
        // Construct a server, connect N clients and then kick them all.

        IPEndPoint endPoint = new(IPAddress.Loopback, 0);
        IpServerTransport server = new(endPoint);

        ConcurrentDictionary<int, byte> joined = new();

        server.OnClientJoin += id => Assert.True(joined.TryAdd(id, 0));
        server.OnClientFinish += id => Assert.True(joined.TryRemove(id, out _));

        Task serverTask = server.RunAsync();

        await Task.Delay(10);

        if (serverTask.IsCompleted)
            await serverTask;

        IPEndPoint target = new(IPAddress.Loopback, server.Port);
        var clients = ConstructClients(target, clientCount);
        var clientTasks = RunClients(clients);
        
        await Task.Delay(150);

        output_.WriteLine("[Test] Kicking clients.");

        Assert.Equal(clientCount, joined.Count);

        foreach (var client in joined)
            server.Kick(client.Key);

        bool serverEnded = false;

        foreach (var task in clientTasks)
            await task;

        await Task.Delay(20);

        output_.WriteLine("[Test] Terminating server.");

        server.Kick();

        try
        {
            await serverTask;
        }
        catch (OperationCanceledException)
        {
            serverEnded = true;
        }

        Assert.True(joined.IsEmpty);
        Assert.True(serverEnded);
        
        await Task.Delay(20);
    }

    /// <summary>
    /// Test whether reliable messages from client to server work.
    /// </summary>
    /// <param name="count">The number of message each client sends.</param>
    /// <param name="clientCount">The number of clients.</param>
    /// <returns>Test run task.</returns>
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

                output_.WriteLine($"Received {read} from {cid}");
                Assert.Equal(expected, read);
                idToExpectedValue[cid] = expected + 1;
            }
        };

        IPEndPoint target = new(IPAddress.Loopback, server.Port);
        var clients = ConstructClients(target, clientCount);
        RunClients(clients);
        
        await Task.Delay(150);
        
        for (int i = 1; i <= count; i++)
        {
            foreach (var client in clients)
            {
                var mem = ArrayPool<byte>.Shared.RentMemory(sizeof(int));
                Bits.Write(i, mem.Span);
                client.SendReliable(mem);
            }
        }

        await Task.Delay(20);

        Assert.Equal(clientCount, idToExpectedValue.Count);
        foreach (int expected in idToExpectedValue.Values)
        {
            Assert.Equal(count, expected - 1);
        }

        output_.WriteLine("[Test] TERMINATING ALL.");
        
        TerminateClients(clients);
        server.Kick();

        await Task.Delay(20);
    }

    /// <summary>
    /// Test whether reliable broadcast from server to client work.
    /// </summary>
    /// <param name="count">The number of messages to send.</param>
    /// <param name="clientCount">The number of clients.</param>
    /// <returns>Test run task.</returns>
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
        Dictionary<int, int> idToExpectedValue = new();

        IPEndPoint target = new(IPAddress.Loopback, server.Port);
        var clients = ConstructClients(target, clientCount);
        RunClients(clients);

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

        await Task.Delay(150);

        Assert.Equal(clientCount, idToExpectedValue.Count);
        foreach (int expected in idToExpectedValue.Values)
        {
            Assert.Equal(count, expected - 1);
        }

        output_.WriteLine("[Test] TERMINATING ALL.");
        
        TerminateClients(clients);
        server.Kick();

        await Task.Delay(20);
    }
    
    /// <summary>
    /// Test whether reliable unicasts from server to client work.
    /// </summary>
    /// <param name="count">The number of messages each client should receive.</param>
    /// <param name="clientCount">The number of clients.</param>
    /// <returns>Test run task.</returns>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 1)]
    [InlineData(10, 1)]
    [InlineData(20, 1)]
    [InlineData(100, 1)]
    [InlineData(1, 10)]
    [InlineData(5, 10)]
    [InlineData(10, 10)]
    [InlineData(20, 10)]
    public async Task TestServerReliableUnicast(int count, int clientCount)
    {
        IPEndPoint endPoint = new(IPAddress.Loopback, 0);
        IpServerTransport server = new(endPoint);

        List<int> clientIds = new();

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
        var clients = ConstructClients(target, clientCount);
        
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
            foreach (var id in clientIds)
            {
                var mem = ArrayPool<byte>.Shared.RentMemory(sizeof(int));
                Bits.Write(i, mem.Span);
                server.SendReliable(mem, id);
            }
        }

        await Task.Delay(150);

        Assert.Equal(clientCount, idToExpectedValue.Count);
        foreach (int expected in idToExpectedValue.Values)
        {
            Assert.Equal(count, expected - 1);
        }

        output_.WriteLine("[Test] TERMINATING ALL.");
        
        TerminateClients(clients);
        server.Kick();

        await Task.Delay(20);
    }

    /// <summary>
    /// Test unreliable messages by an echo server with unicast responses.
    /// </summary>
    /// <param name="pingCount">The number of pings each client makes.</param>
    /// <param name="clientCount">The number of clients.</param>
    /// <returns>Test run task.</returns>
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
        var clients = ConstructClients(target, clientCount);

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

        RunClients(clients);

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

        await Task.Delay(150);

        Assert.Equal(clientCount, idToResponses.Count);

        foreach (int responses in idToResponses.Values)
        {
            Assert.InRange(responses, pingCount * 0.5, float.PositiveInfinity);
            output_.WriteLine($"[Test] Received {responses * 100f / pingCount} responses.");
        }

        output_.WriteLine("[Test] TERMINATING ALL.");

        TerminateClients(clients);
        server.Kick();

        await Task.Delay(20);
    }

    /// <summary>
    /// Test unreliable broadcasts from server by making clients echo back.
    /// </summary>
    /// <param name="pingCount">The number of broadcast pings the server makes.</param>
    /// <param name="clientCount">The number of clients.</param>
    /// <returns>Test run task.</returns>
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
        var clients = ConstructClients(target, clientCount);

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

        RunClients(clients);

        await Task.Delay(10);

        foreach (var client in clients)
        {
            var mem = ArrayPool<byte>.Shared.RentMemory(client.UnreliableMessageHeader);
            client.SendUnreliable(mem);
        }

        await Task.Delay(150);

        Memory<byte> template = new byte[server.UnreliableMessageHeader + sizeof(int)];
        Bits.Write(42, template.Span[server.UnreliableMessageHeader..]);

        for (int i = 1; i <= pingCount; i++)
        {
            var mem = ArrayPool<byte>.Shared.RentMemory(template.Length);
            template.CopyTo(mem);
            server.SendUnreliable(mem);
        }

        await Task.Delay(150);

        Assert.Equal(clientCount, idToMessages.Count);

        foreach (int messages in idToMessages.Values)
        {
            Assert.InRange(messages, pingCount * 0.5, float.PositiveInfinity);
            output_.WriteLine($"[Test] Received {messages * 100f / pingCount} % of responses.") ;
        }

        output_.WriteLine("[Test] TERMINATING ALL.");

        TerminateClients(clients);
        server.Kick();

        await Task.Delay(20);
    }
}
