using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Serilog;
using System.CommandLine.Invocation;
using System.CommandLine;
using System.Net;
using Serilog.Core;

namespace Tester;

static class RootCommandExtensions
{
    public static void AddGlobalOptions(this RootCommand command, params Option[] options)
    {
        foreach (Option option in options)
            command.AddGlobalOption(option);
    }
}

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

    static void HandleTickrate(InvocationContext ctx)
    {
        Program.TickRate = ctx.ParseResult.GetValueForOption(Program.Tickrate);
    }

    static ILoggerFactory GetLoggerFactory(InvocationContext ctx, Option<FileInfo> option)
    {
        if (ctx.ParseResult.GetValueForOption(option) is not { } path)
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
        string? targetCandidate = ctx.ParseResult.GetValueForOption(Program.Target) ?? "";
        if (!IPEndPoint.TryParse(targetCandidate, out IPEndPoint? target))
        {
            Console.Error.WriteLine("Invalid target specified.");
            return null;
        }
        
        ILoggerFactory comLogger = GetLoggerFactory(ctx, Program.ComLogger);
        ILoggerFactory gameLogger = GetLoggerFactory(ctx, Program.GameLogger);
        ILoggerFactory testLogger = GetLoggerFactory(ctx, Program.TestLogger);
        bool trace = ctx.ParseResult.GetValueForOption(Program.TraceState);
        bool checksum = ctx.ParseResult.GetValueForOption(Program.Checksum);

        return new(target, comLogger, gameLogger, testLogger, trace, checksum);
    }

    public static bool IsServer(this InvocationContext ctx) => ctx.ParseResult.GetValueForOption(Program.ServerParam);

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

        float delta = ctx.ParseResult.GetValueForOption(Program.DesiredDelta);
        int sampleWindow = ctx.ParseResult.GetValueForOption(Program.SamplingWindow);

        return new(commonParams, delta, sampleWindow);
    }
}
