using System;
using System.Buffers;
using DefaultTransport.Dispatcher;
using MemoryPack;
using Useful;

namespace DefaultTransportTests;

/// <summary>
/// Tests for <see cref="DefaultServerDispatcher"/> and <see cref="DefaultClientDispatcher"/>.
/// </summary>
/// <remarks>
/// These tests test whether the two corresponding classes work together.
/// </remarks>
public sealed class DefaultDispatcherTests
{
    /// <summary>
    /// Test sending from client to server.
    /// </summary>
    /// <param name="id">The id of the client.</param>
    /// <param name="startFrame">The first frame to send.</param>
    /// <param name="frameCount">The last frame to send.</param>
    /// <param name="respond">Whether the server should ping back.</param>
    [Theory]
    [InlineData(1, 1, 10, false)]
    [InlineData(1, 1, 10, true)]
    public void ClientSendTest(long id, long startFrame, long frameCount, bool respond)
    {
        MockClientTransport clientTransport = new(id);

        MockServerTransport serverTransport = new()
        {
            clientTransport
        };

        DefaultServerDispatcher server = new(serverTransport);
        DefaultClientDispatcher client = new(clientTransport);

        long nextFrame = startFrame;

        object mutex = new();
        int add = 0;
        int remove = 0;
        
        server.OnAddClient += cid =>
        {
            Assert.Equal(id, cid);
            lock (mutex) add++;
        };

        server.OnRemoveClient += cid =>
        {
            Assert.Equal(id, cid);
            lock (mutex) remove++;
        };

        server.OnAddInput += (cid, frame, input) =>
        {
            lock (mutex)
            {
                Assert.Equal(id, cid);
                Assert.InRange(frame, startFrame, startFrame + frameCount - 1);
                var data = MemoryPackSerializer.Deserialize<long>(input);
                Assert.Equal(frame, data);

                if (frame < nextFrame)
                    return;
                
                if (frame > nextFrame)
                    Assert.Fail("Missed input.");

                nextFrame++;

                if (respond)
                    server.SendAuthoritativeInput(frame, null, 42);
            }
        };

        clientTransport.Start();

        for (long i = 0; i < frameCount; i++)
        {
            long cur = startFrame + i;
            client.SendInput(cur, cur);
        }

        client.Disconnect();

        Assert.Equal(1, remove);
        Assert.Equal(1, add);
        Assert.Equal(startFrame + frameCount, nextFrame);
    }

    /// <summary>
    /// Test messages from server to client.
    /// </summary>
    /// <param name="id">The id of the client.</param>
    /// <param name="startFrame">The first frame to send.</param>
    /// <param name="frameCount">The last frame to send.</param>
    [Theory]
    [InlineData(1, 1, 10)]
    public void ClientReceiveTest(long id, long startFrame, int frameCount)
    {
        MockClientTransport clientTransport = new(id);

        MockServerTransport serverTransport = new()
        {
            clientTransport
        };

        DefaultServerDispatcher server = new(serverTransport);
        DefaultClientDispatcher client = new(clientTransport);

        object mutex = new();
        int add = 0;
        int remove = 0;

        server.OnAddClient += cid =>
        {
            Assert.Equal(id, cid);
            lock (mutex) add++;
        };

        server.OnRemoveClient += cid =>
        {
            Assert.Equal(id, cid);
            lock (mutex) remove++;
        };

        int started = 0;
        int initialized = 0;
        int delay = 0;

        client.OnStart += _ =>
        {
            lock (mutex)
                started++;
        };

        client.OnInitialize += (frame, raw) =>
        {
            var data = MemoryPackSerializer.Deserialize<long>(raw.Span);
            ArrayPool<byte>.Shared.Return(raw);

            Assert.Equal(frame, data);
            Assert.Equal(frame, startFrame);
            
            lock (mutex) initialized++;
        };

        long currentFrame = startFrame + 1;

        client.OnAddAuthInput += (frame, input, checksum) =>
        {
            lock (mutex)
            {
                if (frame < currentFrame)
                    return;

                Assert.Equal(currentFrame, frame);
                Assert.True(checksum.HasValue);
                Assert.Equal(frame, checksum.Value);

                var data = MemoryPackSerializer.Deserialize<long>(input.Span);
                ArrayPool<byte>.Shared.Return(input);

                Assert.Equal(frame, data);

                currentFrame++;
            }
        };

        client.OnSetDelay += (_, _) =>
        {
            lock (mutex)
                delay++;
        };

        clientTransport.Start();

        server.Initialize(id, startFrame, startFrame);

        for (int i = 1; i < frameCount; i++)
        {
            long cur = startFrame + i;
            server.SendAuthoritativeInput(cur, cur, cur);
            server.InputAuthored(id, cur, TimeSpan.Zero);
        }

        server.Kick(id);

        Assert.Equal(1, remove);
        Assert.Equal(1, add);
        Assert.Equal(delay, frameCount - 1);
        Assert.Equal(1, initialized);
        Assert.Equal(startFrame + frameCount, currentFrame);
    }

    /// <summary>
    /// Test whether receiving empty messages does not break the client.
    /// </summary>
    [Fact]
    public void TestClientEmptyMessages()
    {
        SingleClientMockTransport transport = new();
        DefaultClientDispatcher _ = new(transport);

        var a = ArrayPool<byte>.Shared.RentMemory(0);
        transport.InvokeReliable(a);

        var b = ArrayPool<byte>.Shared.RentMemory(0);
        transport.InvokeUnreliable(b);
    }

    /// <summary>
    /// Test whether receiving empty messages does not break the server.
    /// </summary>
    [Fact]
    public void TestServerEmptyMessages()
    {
        SingleServerMockTransport transport = new();
        DefaultServerDispatcher _ = new(transport);
        
        var a = ArrayPool<byte>.Shared.RentMemory(0);
        transport.InvokeReliable(42, a);

        var b = ArrayPool<byte>.Shared.RentMemory(0);
        transport.InvokeUnreliable(42, b);
    }

    /// <summary>
    /// Test whether invalid client input does not break the server.
    /// </summary>
    /// <param name="givenLength">The fake length in the packet.</param>
    /// <param name="actual">The actual length of the packet.</param>
    [Theory]
    [InlineData(-456, 0)]
    [InlineData(0, 0)]
    [InlineData(0, 456)]
    [InlineData(96841539, 0)]
    [InlineData(8974312, 321)]
    public void TestInvalidClientInput(int givenLength, int actual)
    {
        SingleServerMockTransport transport = new();
        DefaultServerDispatcher _ = new(transport);

        var message = ArrayPool<byte>.Shared.RentMemory(actual + sizeof(int) + sizeof(long) + 1);
        var span = message.Span;
        span[0] = (byte)MessageType.ClientInput;
        span = span[1..];
        Bits.Write(42L, span);
        span = span[sizeof(long)..];
        Bits.Write(givenLength, span);

        transport.InvokeUnreliable(42, message);
    }

}
