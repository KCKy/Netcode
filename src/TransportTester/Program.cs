using System.Net;
using Core.Extensions;
using Core.Utility;
using DefaultTransport.IpTransport;
using MemoryPack;
using Serilog;
using SimpleCommandLine;

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

    static void RunClient()
    {
        IPEndPoint server = Command.GetEndPoint("Enter server IP address and port: ", IPAddress.Loopback);

        IpClientTransport client = new(server);

        client.OnReliableMessage += ClientMessageHandler;
        client.OnUnreliableMessage += ClientMessageHandler;

        client.RunAsync().AssureNoFault();

        while (true)
        {
            Console.WriteLine("Enter client command ([s]end, [e]nd, [q]uit).");
            char command = Command.GetCommand("> ");

            switch (command)
            {
                case 's':
                    client.SendUnreliable(GetMessage());
                    continue;
                case 'e':
                    Console.WriteLine("Stopping the client.");
                    client.Terminate();
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

    static Memory<byte> GetMessage()
    {
        Console.WriteLine("Enter message to send.");
        Console.Write("> ");

        MemoryPackSerializer.Serialize(messageWriter_, Console.ReadLine() ?? "");

        return messageWriter_.ExtractAndReplace();
    }

    static void RunServer()
    {
        IPEndPoint local = Command.GetEndPoint("Enter local IP address and port: ", IPAddress.Loopback);

        IpServerTransport server = new(local);

        server.OnClientJoin += ClientJoinHandler;
        server.OnClientFinish += ClientFinishHandler;
        server.OnReliableMessage += MessageHandler;
        server.OnUnreliableMessage += MessageHandler;

        server.RunAsync().AssureNoFault();
        
        while (true)
        {
            Console.WriteLine("Enter server command ([s]end, send-[a]ll, [k]ick, [q]uit).");
            char command = Command.GetCommand("> ");
            long id;

            switch (command)
            {
                case 's':
                    id = Command.GetLong("Enter addressee id: ");
                    server.SendUnreliable(GetMessage(), id);
                    continue;
                case 'a':
                    server.SendUnreliable(GetMessage());
                    continue;
                case 'k':
                    id = Command.GetLong("Enter id to kick: ");
                    server.Terminate(id);
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
