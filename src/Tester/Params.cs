using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog.Core;
using Serilog;
using System.CommandLine.Invocation;
using System.CommandLine;
using System.Net;

namespace Tester;

public readonly record struct CommonParams(IPEndPoint Target, ILoggerFactory ComLogger, ILoggerFactory GameLogger, ILoggerFactory TestLogger, bool Trace, bool Checksum);
public readonly record struct ServerParams(CommonParams Common);
public readonly record struct ClientParams(CommonParams Common, float TargetDelta, int SampleWindow) {}

static class ParamsException
{
    public static T UnwrapOrThrow<T>(this T? self) where T : struct 
    {
        if (self is not {  } valid)
            throw new ArgumentException("Invalid parameters.");
        return valid;
    }
}

static class InvocationContextExtensions
{
    public static void FlagTestFail(this InvocationContext context) => context.ExitCode = -1;

    public static T? GetOption<T>(this InvocationContext context, Option<T> option) => context.ParseResult.GetValueForOption(option);

    static void HandleTickrate(InvocationContext ctx)
    {
        Program.TickRate = ctx.GetOption(Program.Tickrate);
    }

    static ILoggerFactory GetLoggerFactory(InvocationContext ctx, Option<FileInfo> option)
    {
        if (ctx.GetOption(option) is not { } path)
            return NullLoggerFactory.Instance;

        Logger logger;

        try
        { 
            logger = new LoggerConfiguration().WriteTo.File(path.FullName).MinimumLevel.Verbose().CreateLogger();
        }
        catch (Exception)
        {
            Console.Error.WriteLine("Invalid path for log file.");
            return NullLoggerFactory.Instance;
        }
        
        return LoggerFactory.Create(c => c.AddSerilog(logger));
    }

   
    static CommonParams? TryGetCommonParams(InvocationContext ctx)
    {
        string? targetCandidate = ctx.GetOption(Program.Target) ?? "";
        if (!IPEndPoint.TryParse(targetCandidate, out IPEndPoint? target))
        {
            Console.Error.WriteLine("Invalid target specified.");
            return null;
        }
        
        ILoggerFactory comLogger = GetLoggerFactory(ctx, Program.ComLogger);
        ILoggerFactory gameLogger = GetLoggerFactory(ctx, Program.GameLogger);
        ILoggerFactory testLogger = GetLoggerFactory(ctx, Program.TestLogger);
        bool trace = ctx.GetOption(Program.TraceState);
        bool checksum = ctx.GetOption(Program.Checksum);

        return new(target, comLogger, gameLogger, testLogger, trace, checksum);
    }

    public static bool IsServer(this InvocationContext ctx) => ctx.GetOption(Program.ServerParam);

    public static ServerParams? TryGetServerParams(this InvocationContext ctx)
    {
        HandleTickrate(ctx);

        if (TryGetCommonParams(ctx) is { } common)
            return new(common);

        return null;
    }

    public static ClientParams? TryGetClientParams(this InvocationContext ctx)
    {
        HandleTickrate(ctx);

        if (TryGetCommonParams(ctx) is not { } commonParams)
            return null;

        float delta = ctx.GetOption(Program.DesiredDelta);
        int sampleWindow = ctx.GetOption(Program.SamplingWindow);

        return new(commonParams, delta, sampleWindow);
    }
}
