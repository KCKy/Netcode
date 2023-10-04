using System.Diagnostics;
using System.Net;
using Core.Extensions;
using DefaultTransport;
using DefaultTransport.Client;
using DefaultTransport.Server;
using DefaultTransport.TcpTransport;
using Serilog;
using Serilog.Events;
using SimpleCommandLine;

namespace TestGame;

static class Program
{
    const string ServerAddress = "127.0.0.1:15965";
    const int ServerSidePort = 15965;

    static readonly IPEndPoint ServerTargetPoint = IPEndPoint.Parse(ServerAddress);
    static readonly IPEndPoint ServerSidePoint = new(IPAddress.Loopback, ServerSidePort);

    const string ServerLogFile = "server_{0}.log";
    const string ClientLogFile = "client_{0}.log";

    const string ServerOutputTemplate = "[{Timestamp:HH:mm:ss.fff}] <S:{Level:u3}> {Message:j}{NewLine}{Exception}";
    const string ClientOutputTemplate = "[{Timestamp:HH:mm:ss.fff}] <C:{Level:u3}> {Message:j}{NewLine}{Exception}";

    static async Task Main()
    {
        while (true)
        {
            switch (Command.GetCommand(() => Console.Write("Set mode ([c]lient, [s]erver): ")))
            {
                case 'c':
                    RunClient();
                    goto end;
                case 's':
                    RunServer();
                    goto end;
                default:
                    Console.WriteLine("Unknown mode.");
                    continue;
            }
        }

        end:
        await Log.CloseAndFlushAsync();
    }

    static string TimestampFile(string filename)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HHmmssfff");
        return string.Format(filename, timestamp);
    }

    static void RunClient()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Debug, outputTemplate: ClientOutputTemplate)
            .WriteTo.File(TimestampFile(ClientLogFile), outputTemplate: ClientOutputTemplate)
            .CreateLogger();

        TcpClientTransport<IMessageToClient, IMessageToServer> transport = new()
        {
            Target = ServerTargetPoint
        };

        Displayer displayer = new("Client");
        ClientInputProvider input = new(displayer.Window);

        var client = DefaultClientConstructor.Construct<ClientInput, ServerInput, GameState>(transport, input, displayer: displayer);
        client.UseChecksum = true;
        client.TraceFrameTime = true;
        client.TraceState = true;

        client.RunAsync().AssureSuccess();
        transport.Start().AssureSuccess();
        
        while (true)
            displayer.Update();
    }

    static void RunServer()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Debug, outputTemplate: ServerOutputTemplate)
            .WriteTo.File(TimestampFile(ServerLogFile), outputTemplate: ServerOutputTemplate)
            .CreateLogger();

        TcpServerTransport<IMessageToServer, IMessageToClient> transport = new(ServerSidePoint);
        
        Displayer displayer = new("Server");

        var server = DefaultServerConstructor.Construct<ClientInput, ServerInput, GameState>(transport, new ServerInputProvider(), displayer: displayer);
        server.SendChecksum = true;
        server.TraceFrameTime = true;
        server.TraceState = true;

        server.RunAsync().AssureSuccess();
        transport.Start().AssureSuccess();

        while (true)
            displayer.Update();
    }
}
