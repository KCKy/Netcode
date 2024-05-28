using System.CommandLine;
using System.Net;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Tester;

interface ITestGame
{
    static abstract void Register(RootCommand command);
}

public readonly record struct CommonParams(IPEndPoint Target, ILoggerFactory ComLogger, ILoggerFactory GameLogger, ILoggerFactory TestLogger, bool Trace, bool Checksum);
public readonly record struct ServerParams(CommonParams Common);
public readonly record struct ClientParams(CommonParams Common, float TargetDelta, int SampleWindow) {}

class Program
{
    public static float TickRate { get; set; } = 5;

    public static readonly Option<float> Tickrate = new("--tickrate", "Override tick-rate.");
    public static readonly Option<string> Target = new("--target", "The server end point.");
    public static readonly Option<FileInfo> ComLogger = new("--comlog", "Log file for the communication layers.");
    public static readonly Option<FileInfo> GameLogger = new("--gamelog", "Log file for the game layer.");
    public static readonly Option<FileInfo> TestLogger = new("--log", "Log file for the game test.");
    public static readonly Option<bool> ServerParam = new("--server", "Run the GameNewt server instead of the client.");
    public static readonly Option<bool> TraceState = new("--trace",  () => false, "Whether to trace state.");
    public static readonly Option<bool> Checksum = new("--checksum",  () => false, "Whether to checksum.");
    public static readonly Option<int> SamplingWindow = new("--sample-window", "The size of sampling window for jitter prevention.");
    public static readonly Option<float> DesiredDelta = new("--delta",  () => 0.05f, "The margin of error for client input sending.");

    static async Task<int> Main(string[] args)
    {
        RootCommand rootCommand = new("Tester consisting of a suite of testing games for GameNewt.");
        rootCommand.AddGlobalOptions(Tickrate, Target, ComLogger, GameLogger, TestLogger, ServerParam, TraceState, Checksum, SamplingWindow, DesiredDelta);

        Type testInterface = typeof(ITestGame);
        IEnumerable<Action<RootCommand>> registerDelegates = from type in Assembly.GetExecutingAssembly().GetTypes()
            where testInterface.IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface
            let method = type.GetMethod("Register", BindingFlags.Static | BindingFlags.Public)
            where method is not null
            let del = Delegate.CreateDelegate(typeof(Action<RootCommand>), method, false) as Action<RootCommand>
            where del is not null
            select del;

        foreach (Action<RootCommand> register in registerDelegates)
            register(rootCommand);

        return await rootCommand.InvokeAsync(args);
    }
}
