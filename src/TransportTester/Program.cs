using System.Net;
using DefaultTransport.TcpTransport;
using Serilog;

namespace TransportTester;

static class Program
{
    static char? TryGetCommand()
    {
        string? input = Console.ReadLine();
        if (input is { Length: 1 })
            return input.ToLowerInvariant()[0];
        return null;
    }

    static char GetCommand(Action info)
    {
        while (true)
        {
            info();

            if (TryGetCommand() is {} command)
                return command;
        }
    }

    static IPEndPoint GetEndPoint(Action info)
    {
        while (true)
        {
            info();

            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                return IPEndPoint.Parse("127.0.0.1:12345");

            if (IPEndPoint.TryParse(input, out IPEndPoint? point))
                return point;
        }
    }

    static long GetLong(Action info)
    {
        while (true)
        {
            info();

            string? input = Console.ReadLine();

            if (input is null)
                continue;

            if (long.TryParse(input, out long value))
                return value;
        }
    }

    static void Main()
    {
        Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

        Console.WriteLine("Transport Tester");

        while (true)
        {
            switch (GetCommand(() => Console.Write("Set mode ([c]lient, [s]erver): ")))
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
        IPEndPoint server = GetEndPoint(() => Console.Write("Enter server IP address and port: "));

        TcpClientTransport<string, string> client = new()
        {
            Target = server
        };

        client.OnMessage += MessageHandler;
        client.OnFinish += FinishHandler;

        while (true)
        {
            Console.WriteLine($"Enter client command ([b]egin, [s]end, [e]nd, [q]uit).");
            char command = GetCommand(() => Console.Write("> "));

            switch (command)
            {
                case 'b':
                    client.Start().Wait();
                    continue;
                case 's':
                    client.SendReliable(GetMessage());
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
        void MessageHandler(string message) => Console.WriteLine($"Received message [[{message}]]");
        void FinishHandler() => Console.WriteLine($"Client stopped because of {client.FinishReason}.");
    }

    static string GetMessage()
    {
        Console.WriteLine("Enter message to send.");
        Console.Write("> ");
        return Console.ReadLine() ?? "";
    }

    static void RunServer()
    {
        IPEndPoint local = GetEndPoint(() => Console.Write("Enter local IP address and port: "));

        TcpServerTransport<string, string> server = new(local);

        server.OnClientJoin += ClientJoinHandler;
        server.OnClientFinish += ClientFinishHandler;
        server.OnClientFinishReason += ClientFinishReasonHandler;
        server.OnFinish += FinishHandler;
        server.OnMessage += MessageHandler;
        
        while (true)
        {
            Console.WriteLine($"Enter client command ([b]egin, [s]end, send-[a]ll, [e]nd, [k]ick, [q]uit).");
            char command = GetCommand(() => Console.Write("> "));
            long id;

            switch (command)
            {
                case 'b':
                    server.Start().Wait();
                    continue;
                case 's':
                    id = GetLong(() => Console.WriteLine("Enter addressee id: "));
                    server.SendReliable(GetMessage(), id);
                    continue;
                case 'a':
                    server.SendReliable(GetMessage());
                    continue;
                case 'e':
                    Console.WriteLine("Stopping the client.");
                    server.Terminate();
                    continue;
                case 'k':
                    id = GetLong(() => Console.WriteLine("Enter id to kick: "));
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
        void ClientFinishReasonHandler(long id, ClientFinishReason reason) => Console.WriteLine($"Client with id {id} left because of {reason}.");
        void FinishHandler() => Console.WriteLine($"Server stopped because of {server.FinishReason}.");
        void MessageHandler(long id, string message) => Console.WriteLine($"Received message from client {id} [[{message}]]");
    }
}
