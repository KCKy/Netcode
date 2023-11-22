﻿using System.Net;
using Core.Client;
using Core.Providers;
using Core.Server;
using DefaultTransport.Dispatcher;
using DefaultTransport.IpTransport;
using Serilog;
using Serilog.Events;
using Useful;

namespace TestGame;

static class Program
{
    const string ServerLogFile = "server_{0}.log";
    const string ClientLogFile = "client_{0}.log";

    const string ServerOutputTemplate = "[{Timestamp:HH:mm:ss.fff}] <S:{Level:u3}> {Message:j}{NewLine}{Exception}";
    const string ClientOutputTemplate = "[{Timestamp:HH:mm:ss.fff}] <C:{Level:u3}> {Message:j}{NewLine}{Exception}";

    static async Task Main()
    {
        while (true)
        {
            switch (Command.GetCommand("Set mode ([c]lient, [s]erver): "))
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

    const int DefaultPort = 13675;

    static void RunClient()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Debug, outputTemplate: ClientOutputTemplate)
            .WriteTo.File(TimestampFile(ClientLogFile), outputTemplate: ClientOutputTemplate)
            .CreateLogger();

        IPEndPoint target = Command.GetEndPoint("Enter server IP address and port: ", new(IPAddress.Loopback, DefaultPort));

        float delay = Command.GetFloat("Enter latency padding value (s): ", 0.03f);

        Log.Information("Connecting to {EndPoint}...", target);

        IpClientTransport transport = new(target);

        Displayer displayer = new("Client");
        ClientInputProvider input = new(displayer.Window);

        DefaultClientDispatcher dispatcher = new(transport);

        Client<ClientInput, ServerInput, GameState> client = new(dispatcher, dispatcher, displayer, input, new DefaultServerInputPredictor<ServerInput, GameState>(), new DefaultClientInputPredictor<ClientInput>())
        {
            PredictDelayMargin = delay,
            UseChecksum = true,
            TraceFrameTime = true,
            TraceState = true
        };

        client.RunAsync().AssureSuccess();
        transport.RunAsync().ContinueWith(_ => client.Terminate());
        
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

        IPEndPoint local = Command.GetEndPoint("Enter local IP address and port: ", new(IPAddress.Any, DefaultPort));

        Log.Information("Starting server on {local}", local);

        IpServerTransport transport = new(local);
        
        Displayer displayer = new("Server");

        DefaultServerDispatcher dispatcher = new(transport);
        Server<ClientInput, ServerInput, GameState> server = new(dispatcher, dispatcher, displayer, new ServerInputProvider())
        {
            SendChecksum = true,
            TraceFrameTime = true,
            TraceState = true
        };

        server.RunAsync().AssureSuccess();
        transport.RunAsync().AssureSuccess();

        while (true)
            displayer.Update();
    }
}
