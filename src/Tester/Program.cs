using System.CommandLine;
using System.Reflection;

namespace Tester;

interface ITestGame
{
    static abstract void Register(RootCommand command);
}

class Program
{
    public static float TickRate { get; set; } = 5;

    public static readonly Option<float> Tickrate = new("--tickrate",  () => 5, "Tick-rate of the simulation.");
    public static readonly Option<string> Target = new("--target", () => "127.0.0.1:42000", "The server end point.");
    public static readonly Option<FileInfo> ComLogger = new("--comlog", "Log file for the communication layers.");
    public static readonly Option<FileInfo> GameLogger = new("--gamelog", "Log file for the game layer.");
    public static readonly Option<FileInfo> TestLogger = new("--log", "Log file for the game test.");
    public static readonly Option<bool> ServerParam = new("--server", "Run the GameNewt server instead of the client.");
    public static readonly Option<bool> TraceState = new("--trace",  () => false, "Whether to trace state.");
    public static readonly Option<bool> Checksum = new("--checksum",  () => false, "Whether to checksum.");
    public static readonly Option<int> SamplingWindow = new("--sample-window", () => 20, "The size of sampling window for jitter prevention.");
    public static readonly Option<float> DesiredDelta = new("--delta",  () => 0.05f, "The desired delay of client inputs to server.");

    static IEnumerable<Action<RootCommand>> GetCurrentAssemblyRegisterCommands()
    {
        Type testInterface = typeof(ITestGame);
        return from type in Assembly.GetExecutingAssembly().GetTypes()
                where testInterface.IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface
                let method = type.GetMethod("Register", BindingFlags.Static | BindingFlags.Public)
                where method is not null
                let del = Delegate.CreateDelegate(typeof(Action<RootCommand>), method, false) as Action<RootCommand>
                where del is not null
                select del; 
    }

    static async Task<int> Main(string[] args)
    {
        RootCommand rootCommand = new("Tester consisting of a suite of testing games for GameNewt.");
        rootCommand.AddGlobalOptions(Tickrate, Target, ComLogger, GameLogger, TestLogger, ServerParam, TraceState, Checksum, SamplingWindow, DesiredDelta);

        foreach (Action<RootCommand> register in GetCurrentAssemblyRegisterCommands())
            register(rootCommand);

        return await rootCommand.InvokeAsync(args);
    }
}
