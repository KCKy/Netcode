using System.Net;
using DefaultTransport.IpTransport;
using MemoryPack;
using Serilog;
using Useful;

namespace TransportTester;

static class Program
{
    static void Main()
    {
        Log.Logger = new LoggerConfiguration().WriteTo.Console().MinimumLevel.Verbose().CreateLogger();

        Console.WriteLine("Transport Tester");

        while (true)
        {
            switch (Command.GetCommand("Set mode ([c]lient, [s]erver): "))
            {
                case 'c':
                    RunClient();
                    return;
                case 's':
                    RunServer();
                    return;
                default:
                    Console.WriteLine("Unknown mode.");
                    continue;
            }
        }
    }

    const int DefaultPort = 13675;

    static void RunClient()
    {
        IPEndPoint server = Command.GetEndPoint("Enter server IP address and port: ", new(IPAddress.Loopback, DefaultPort));

        IpClientTransport client = new(server);

        client.OnReliableMessage += ClientMessageHandler;
        client.OnUnreliableMessage += ClientMessageHandler;

        client.RunAsync().AssureNoFault();

        while (true)
        {
            Console.WriteLine("Enter client command ([s]end, [u]nreliable-send, [e]nd, [q]uit).");
            char command = Command.GetCommand("> ");

            switch (command)
            {
                case 's':
                    client.SendReliable(GetMessage(client.ReliableMessageHeader));
                    continue;
                case 'e':
                    Console.WriteLine("Stopping the client.");
                    client.Terminate();
                    continue;
                case 'u':
                    client.SendUnreliable(GetMessage(client.UnreliableMessageHeader));
                    continue;
                case 'q':
                    return;
                default:
                    Console.WriteLine("Unknown command.");
                    continue;
            }
        }
    }

    static void ClientMessageHandler(Memory<byte> message)
    {
        Console.WriteLine($"Received message [[{MemoryPackSerializer.Deserialize<string>(message.Span)}]]");
    }

    static void MessageHandler(long id, Memory<byte> message)
    {
        Console.WriteLine($"Received message from client {id} [[{MemoryPackSerializer.Deserialize<string>(message.Span)}]]");
    }

    static PooledBufferWriter<byte> messageWriter_ = new();

    static Memory<byte> GetMessage(int padding)
    {
        Console.Write("Enter message: ");

        messageWriter_.GetMemory(padding);
        messageWriter_.Advance(padding);

        MemoryPackSerializer.Serialize(messageWriter_, Console.ReadLine() ?? "");

        return messageWriter_.ExtractAndReplace();
    }

    static void RunServer()
    {
        IPEndPoint local = Command.GetEndPoint("Enter local IP address and port: ", new(IPAddress.Any, DefaultPort));

        IpServerTransport server = new(local);

        server.OnClientJoin += ClientJoinHandler;
        server.OnClientFinish += ClientFinishHandler;
        server.OnReliableMessage += MessageHandler;
        server.OnUnreliableMessage += MessageHandler;

        server.RunAsync().AssureNoFault();
        
        while (true)
        {
            Console.WriteLine("Enter server command ([s]end, [u]nreliable-send, send-[a]ll, u[n]reliable-send-all, [k]ick, [e]nd, [q]uit).");
            char command = Command.GetCommand("> ");
            long id;

            switch (command)
            {
                case 's':
                    id = Command.GetLong("Enter addressee id: ");
                    server.SendReliable(GetMessage(server.ReliableMessageHeader), id);
                    continue;
                case 'u':
                    id = Command.GetLong("Enter addressee id: ");
                    server.SendUnreliable(GetMessage(server.UnreliableMessageHeader), id);
                    continue;
                case 'a':
                    server.SendReliable(GetMessage(server.ReliableMessageHeader));
                    continue;
                case 'n':
                    server.SendUnreliable(GetMessage(server.UnreliableMessageHeader));
                    continue;
                case 'k':
                    id = Command.GetLong("Enter id to kick: ");
                    server.Terminate(id);
                    continue;
                case 'e':
                    server.Terminate();
                    continue;
                case 'q':
                    return;
                default:
                    Console.WriteLine("Unknown command.");
                    continue;
            }
        } 

        void ClientJoinHandler(long id) => Console.WriteLine($"A client connected with id {id}.");
        void ClientFinishHandler(long id) => Console.WriteLine($"A client with id {id} left.");
    }
}
