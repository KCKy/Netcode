using System.Net;
using Core;
using Core.Client;
using Core.Providers;
using Core.Server;
using DefaultTransport.Dispatcher;
using DefaultTransport.IpTransport;
using Serilog;
using Useful;

namespace GameCommon;

public enum GameMode
{
    Client,
    Server
}

public record struct SerilogOption(string Name, string Value)
{
    public SerilogOption() : this("", "") { }
}

public record GameSettings
    (GameMode Mode, string LocalAddress, string TargetAddress, 
        bool DoCheckSum, bool TraceFrameTime, bool TraceState, float LatencyPadding, int ConnectionTimeoutMs,
        List<SerilogOption> SerilogSettings)
{
    public GameSettings() : this(GameMode.Client, "", "", false, false, false, 0, 0, 
        new()) { }

    public static GameSettings Example => new(GameMode.Client, "0.0.0.0:17893", "127.0.0.1:17893", false, false, false, 0.001f, 200,
        new (){
            new("write-to:File.path", "client.log"),
            new("using:File", "Serilog.Sinks.File"),
            new("using:Console", "Serilog.Sinks.Console"),
            new("minimum-level", "Debug"),
            new("write-to:Console", "")
        });
}

public delegate
    (IDisplayer<TGameState>? displayer,
    IClientInputProvider<TClientInput>? input,
    IClientInputPredictor<TClientInput>? clientPredictor,
    IServerInputPredictor<TServerInput, TGameState>? serverPredictor)
    
    ClientConstruction<TGameState, TClientInput, TServerInput>()
    where TClientInput : class, new()
    where TServerInput : class, new()
    where TGameState : class, IGameState<TClientInput, TServerInput>, new();

public delegate
    (IDisplayer<TGameState>? displayer,
    IServerInputProvider<TServerInput, TGameState>? input,
    IClientInputPredictor<TClientInput>? predictor)

    ServerConstruction<TGameState, TClientInput, TServerInput>()
    where TClientInput : class, new()
    where TServerInput : class, new()
    where TGameState : class, IGameState<TClientInput, TServerInput>, new();

public class IpGameLoader
{
    public static async Task Load<TGameState, TClientInput, TServerInput>
        (string[] args,
            ServerConstruction<TGameState, TClientInput, TServerInput> serverConstructor,
            ClientConstruction<TGameState, TClientInput, TServerInput> clientConstructor,
            Action<Client<TClientInput, TServerInput, TGameState>> clientStartCallback,
            Action<Server<TClientInput, TServerInput, TGameState>> serverStartCallback,
            Action<Client<TClientInput, TServerInput, TGameState>> clientTransportEndCallback,
            Action<Server<TClientInput, TServerInput, TGameState>> serverTransportEndCallback)
        where TClientInput : class, new()
        where TServerInput : class, new()
        where TGameState : class, IGameState<TClientInput, TServerInput>, new()
    {
        if (args is not [{ } configPath, ..])
        {
            Console.WriteLine("Config path not specified.");
            return;
        }

        var open = SettingsHelper.HandleSettingsOpen(configPath, w => SettingsHelper<GameSettings>.Serialize(GameSettings.Example, w));
        if (open.Value is not { } reader)
        {
            Console.WriteLine(open.Error);
            return;
        }

        var load = SettingsHelper<GameSettings>.Deserialize(reader);
        reader.Dispose();

        if (load.Value is not { } config)
        {
            Console.WriteLine(open.Error);
            return;
        }

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.KeyValuePairs(from p in config.SerilogSettings select new KeyValuePair<string, string>(p.Name, p.Value))
                .CreateLogger();

        ILogger logger = Log.ForContext<IpGameLoader>();

        Useful.TaskExtensions.OnFault += (task, exc) => logger.Error("Task faulted: {Task} with exception: \n{Exception}", task, exc);
        Useful.TaskExtensions.OnCanceled += task => logger.Error("Task was wrongly cancelled: {Task}", task);

        IPEndPoint local = IPEndPoint.Parse(config.LocalAddress);
        IPEndPoint target = IPEndPoint.Parse(config.TargetAddress);

        switch (config.Mode)
        {
            case GameMode.Client:
            {
                IpClientTransport transport = new(target)
                {
                    ConnectTimeoutMs = config.ConnectionTimeoutMs
                };
                DefaultClientDispatcher dispatcher = new(transport);
                var (displayer, input, clientPredictor, serverPredictor) = clientConstructor();
                Client<TClientInput, TServerInput, TGameState> client =
                    new(dispatcher, dispatcher, displayer, input, serverPredictor, clientPredictor)
                    {
                        UseChecksum = config.DoCheckSum,
                        TraceFrameTime = config.TraceFrameTime,
                        TraceState = config.TraceState,
                        PredictDelayMargin = config.LatencyPadding
                    };
                clientStartCallback(client);
                client.RunAsync().AssureSuccess();

                try
                {
                    await transport.RunAsync();
                }
                catch (Exception ex)
                {
                    logger.Information("Client transport stopped with exception {Exception}.", ex);
                    throw;
                }
                finally
                {
                    clientTransportEndCallback(client);
                }
                break;
            }

            case GameMode.Server:
            {
                IpServerTransport transport = new(local);
                DefaultServerDispatcher dispatcher = new(transport);
                var (displayer, input, predictor) = serverConstructor();
                Server<TClientInput, TServerInput, TGameState> server =
                    new(dispatcher, dispatcher, displayer, input, predictor)
                    {
                        SendChecksum = config.DoCheckSum,
                        TraceFrameTime = config.TraceFrameTime,
                        TraceState = config.TraceState
                    };
                serverStartCallback(server);
                logger.Information("Starting server on {local}", local);
                server.RunAsync().AssureSuccess();

                try
                {
                    await transport.RunAsync();
                }
                catch (Exception ex)
                {
                    logger.Information("Server transport stopped with exception {Exception}.", ex);
                    throw;
                }
                finally
                {
                    serverTransportEndCallback(server);
                }
                break;
            }
            default:
            {
                Console.WriteLine("Invalid mode.");
                break;
            }
        }
    }
}
