using System.Diagnostics;
using System.Net;
using Core;
using Core.Extensions;
using Core.Providers;
using DefaultTransport;
using DefaultTransport.Client;
using DefaultTransport.Server;
using DefaultTransport.TcpTransport;
using MemoryPack;
using Serilog;
using Serilog.Events;
using SimpleCommandLine;
using TestGame;

namespace ConsoleGame;

static class Program
{
    const int Port = 15965;

    static IPEndPoint serverAddress = IPEndPoint.Parse("127.0.0.1:15965");

    const string ServerLogFile = "server.log";
    const string ClientLogFile = "client.log";

    const string OutputTemplate = "{Timestamp: HH:mm:ss.fff zzz} [{Level}] {Message}{NewLine}{Exception}";

    static readonly IPEndPoint ServerPoint = new(IPAddress.Loopback, Port);

    static void SetupLogger(string file)
    {
        if (Debugger.IsAttached)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Debug, outputTemplate: OutputTemplate)
                .WriteTo.File(file, rollingInterval: RollingInterval.Infinite, outputTemplate: OutputTemplate)
                .CreateLogger();
            return;
        }

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Debug, outputTemplate: OutputTemplate)
            .WriteTo.File(file, rollingInterval: RollingInterval.Infinite, outputTemplate: OutputTemplate)
            .CreateLogger();
    }
    
    static async Task Main()
    {
        while (true)
        {
            switch (Command.GetCommand(() => Console.Write("Set mode ([c]lient, [s]erver): ")))
            {
                case 'c':
                    SetupLogger(ClientLogFile);
                    RunClient();
                    goto end;
                case 's':
                    SetupLogger(ServerLogFile);
                    await RunServer();
                    goto end;
                default:
                    Console.WriteLine("Unknown mode.");
                    continue;
            }
        }

        end:
        await Log.CloseAndFlushAsync();
    }

    static void RunClient()
    {
        TcpClientTransport<IMessageToClient, IMessageToServer> transport = new()
        {
            Target = serverAddress
        };

        Displayer displayer = new("Client");

        var client = DefaultClientConstructor.Construct<ClientInput, ServerInput, GameState>(transport, null, displayer: displayer);
        client.UseChecksum = true;
        client.TraceFrameTime = true;
        client.TraceState = true;

        client.RunAsync().AssureSuccess();
        transport.Start().AssureSuccess();
        
        while (true)
            displayer.Display();
    }

    static async Task RunServer()
    {
        TcpServerTransport<IMessageToServer, IMessageToClient> transport = new(ServerPoint);
        
        var server = DefaultServerConstructor.Construct<ClientInput, ServerInput, GameState>(transport, new ServerInputProvider());
        
        server.SendChecksum = true;
        server.TraceFrameTime = true;
        server.TraceState = true;

        server.RunAsync().AssureSuccess();
        transport.Start().AssureSuccess();

        await Task.Delay(Timeout.Infinite);
    }
}

[MemoryPackable]
partial class ClientInput
{

}

struct FoodSpawnEvent
{
    public int X;
    public int Y;
    public FoodType Type;

    public FoodSpawnEvent()
    {
        X = 0;
        Y = 0;
        Type = default;
    }
}

[MemoryPackable]
partial class ServerInput
{
    public FoodSpawnEvent? FoodSpawnEvent = null;
}

[MemoryPackable]
[MemoryPackUnion(0, typeof(Food))]
partial interface ILevelObject
{

}

enum FoodType : byte
{
    Apple,
    Carrot
}

[MemoryPackable]
sealed partial class Food : ILevelObject
{
    [MemoryPackInclude]
    public FoodType FoodType { get; set; } = FoodType.Apple;
}

[MemoryPackable]
partial struct Level
{
    [MemoryPackConstructor]
    public Level()
    {
        objects_ = Array.Empty<ILevelObject>();
        Width = 0;
        Height = 0;
    }

    [MemoryPackInclude] ILevelObject?[] objects_;
    public int Width { get; private set; }
    public int Height { get; private set; }
    
    public Level(int width, int height)
    {
        Width = width;
        Height = height;
        int length = width * height;
        objects_ = new ILevelObject[length];
    }

    readonly void CheckValid(int x, int y)
    {
        if (x  < 0 || x >= Width)
            throw new ArgumentOutOfRangeException(nameof(x), x, "Invalid x coordinate.");
        if (y  < 0 || y >= Height)
            throw new ArgumentOutOfRangeException(nameof(y), y, "Invalid x coordinate.");
    }

    public ref ILevelObject? this[int x, int y]
    {
        get
        {
            CheckValid(x, y);
            return ref objects_[x + y * Width];
        }
    }
}

[MemoryPackable]
partial class GameState : IGameState<ClientInput, ServerInput>
{
    public long Frame = -1;

    public const int LevelWidth = 30;
    public const int LevelHeight = 30;

    public static double DesiredTickRate => 20;

    //[MemoryPackInclude] Level level_ = new(LevelWidth, LevelHeight);

    /*void HandleFoodEvent(in FoodSpawnEvent foodEvent)
    {
        ref ILevelObject? place = ref level_[foodEvent.X, foodEvent.Y];

        if (place is not null)
            return;
        
        Food food = new()
        {
            FoodType = foodEvent.Type
        };

        place = food;
    }*/

    public UpdateOutput Update(UpdateInput<ClientInput, ServerInput> updateInputs)
    {
        Frame++;

        /*if (updateInputs.ServerInput.FoodSpawnEvent is { } foodEvent)
            HandleFoodEvent(foodEvent);*/

        return UpdateOutput.Empty;
    }
}

sealed class ServerInputProvider : IServerInputProvider<ServerInput, GameState>
{
    readonly Random random_ = new();

    public ServerInput GetInput(GameState info)
    {
        ServerInput input = new();

        if (random_.NextDouble() >= 0.05)
            return input;

        int x = random_.Next(0, GameState.LevelWidth);
        int y = random_.Next(0, GameState.LevelHeight);

        input.FoodSpawnEvent = new FoodSpawnEvent()
        {
            Type = FoodType.Carrot,
            X = x,
            Y = y
        };

        return input;
    }
}
