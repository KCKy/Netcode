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

/// <summary>
/// Possible modes the game can run in.
/// </summary>
public enum GameMode
{
    /// <summary>
    /// Client mode.
    /// </summary>
    Client,
    /// <summary>
    /// Server mode.
    /// </summary>
    Server
}

/// <summary>
/// A pair of option and its value for Serilog settings.
/// </summary>
/// <param name="Name">The name of the option.</param>
/// <param name="Value">The value of the option.</param>
public record struct SerilogOption(string Name, string Value)
{
    /// <summary>
    /// Empty constructor.
    /// </summary>
    public SerilogOption() : this("", "") { }
}

/// <summary>
/// Struct representing the XML schema for game settings files.
/// </summary>
/// <param name="Mode">Which mode the game should be run in.</param>
/// <param name="LocalAddress">The address the local socket should bind to.</param>
/// <param name="TargetAddress">The address of the target server.</param>
/// <param name="DoCheckSum">Whether to do checksums of game states.</param>
/// <param name="TraceFrameTime">Whether to trace time took to update each frame in the log.</param>
/// <param name="TraceState">Whether to log all states in the log.</param>
/// <param name="LatencyPadding">Amount of time in seconds specifying how much early inputs should be received by the server. Recommended value for standard connections are 5 - 10 ms.</param>
/// <param name="ConnectionTimeoutMs">Time in seconds, the timeout of a new client connection. </param>
/// <param name="SerilogSettings">Settings for the Serilog logging framework.</param>
public record GameSettings
    (GameMode Mode, string LocalAddress, string TargetAddress, 
        bool DoCheckSum, bool TraceFrameTime, bool TraceState, float LatencyPadding, int ConnectionTimeoutMs,
        List<SerilogOption> SerilogSettings)
{
    /// <summary>
    /// Empty constructor.
    /// </summary>
    public GameSettings() : this(GameMode.Client, "", "", false, false, false, 0, 0, 
        new()) { }

    /// <summary>
    /// An example settings file used when a config file does not exist yet.
    /// </summary>
    public static GameSettings Example => new(GameMode.Client, "0.0.0.0:17893", "127.0.0.1:17893", false, false, false, 0.001f, 200,
        new (){
            new("write-to:File.path", "client.log"),
            new("using:File", "Serilog.Sinks.File"),
            new("using:Console", "Serilog.Sinks.Console"),
            new("minimum-level", "Debug"),
            new("write-to:Console", "")
        });
}

/// <summary>
/// Provides the optional dependencies <see cref="Core.Client"/> may be constructed with.
/// </summary>
/// <typeparam name="TGameState">The type of the game state.</typeparam>
/// <typeparam name="TClientInput">The type of the client input.</typeparam>
/// <typeparam name="TServerInput">The type of the server input.</typeparam>
/// <returns>Tuple of optional dependencies to be used for the client construction.</returns>
public delegate
    (IDisplayer<TGameState>? displayer,
    IClientInputProvider<TClientInput>? input,
    IClientInputPredictor<TClientInput>? clientPredictor,
    IServerInputPredictor<TServerInput, TGameState>? serverPredictor)
    
    ClientConstruction<TGameState, TClientInput, TServerInput>()
    where TClientInput : class, new()
    where TServerInput : class, new()
    where TGameState : class, IGameState<TClientInput, TServerInput>, new();

/// <summary>
/// Provides the optional dependencies <see cref="Core.Server"/> may be constructed with.
/// </summary>
/// <typeparam name="TGameState">The type of the game state.</typeparam>
/// <typeparam name="TClientInput">The type of the client input.</typeparam>
/// <typeparam name="TServerInput">The type of the server input.</typeparam>
/// <returns>Tuple of optional dependencies to be used for the server construction.</returns>
public delegate
    (IDisplayer<TGameState>? displayer,
    IServerInputProvider<TServerInput, TGameState>? input,
    IClientInputPredictor<TClientInput>? predictor)

    ServerConstruction<TGameState, TClientInput, TServerInput>()
    where TClientInput : class, new()
    where TServerInput : class, new()
    where TGameState : class, IGameState<TClientInput, TServerInput>, new();

/// <summary>
/// Used to streamline loading games over the <see cref="DefaultTransport"/> layers.
/// </summary>
public static class IpGameLoader
{
    /// <summary>
    /// Construct the game as specified by a config file.
    /// Tries to load a settings file as specified in <see cref="GameSettings"/>. If not, creates a default. Sets up the logging framework.
    /// Constructs client/server (depends on settings). Starts the transport and the client/server.
    /// </summary>
    /// <typeparam name="TGameState">The type of game state used by the game.</typeparam>
    /// <typeparam name="TClientInput">The type of client input used by the game.</typeparam>
    /// <typeparam name="TServerInput">The type of server input used by the game.</typeparam>
    /// <param name="args">Command line arguments. Checks first argument for a config file.</param>
    /// <param name="serverConstructor">Provider of dependencies to server.</param>
    /// <param name="clientConstructor">Provider of dependencies of client.</param>
    /// <param name="clientStartCallback">Callback which is called when the client is started.</param>
    /// <param name="serverStartCallback">Callback which is called when the server is started.</param>
    /// <param name="clientTransportEndCallback">Callback called when the underlying client transport ends. (The client should probably be stopped.)</param>
    /// <param name="serverTransportEndCallback">Callback called when the underlying server transport ends. (The server should probably be stopped.)</param>
    /// <returns>A task representing the execution of the underlying transport layer.</returns>
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

        ILogger logger = Log.ForContext(typeof(IpGameLoader));

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
