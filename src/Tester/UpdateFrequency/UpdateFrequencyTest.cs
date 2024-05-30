using System.CommandLine;
using Microsoft.Extensions.Logging;
using System.CommandLine.Invocation;
using System.Diagnostics;
using Command = System.CommandLine.Command;

namespace Tester.UpdateFrequency;

class UpdateFrequencyTest : ITestGame
{
    static UpdateFrequencyTest() => TestCommon.CreateServerClientHandler(Command, RunServerAsync, RunClientAsync);
    public static void Register(RootCommand root) => root.AddCommand(Command);

    static readonly Option<int> Warmup = new("--warmup", "The warm-up time in frames.");
    static readonly Option<double> Duration = new("--duration", "The duration of the test in seconds.");
    static readonly Option<double> IdealMean = new("--mean", "The ideal mean delta.");
    static readonly Option<double> MaxVariation = new("--max-dev", "Max passing deviation.");
    static readonly Option<double> MaxMeanError = new("--max-mean-error", "Max passing error in mean.");
    static readonly Command Command = new("freq", "Tests whether client update frequency is correct.")
    {
        Duration,
        Warmup,
        IdealMean,
        MaxVariation,
        MaxMeanError
    };

    static async Task RunServerAsync(InvocationContext ctx)
    {
        double duration = ctx.GetOption(Duration);

        (var server, ILogger logger) = TestCommon.ConstructServer<ClientInput, ServerInput, GameState, UpdateFrequencyTest>(ctx);

        logger.LogInformation("Starting update frequency test server with duration {Duration}.", duration);
        
        Task task = server.RunAsync();

        await Task.Delay(TimeSpan.FromSeconds(duration));

        server.Terminate();

        await TestCommon.AwaitServerCancellation(task, logger, ctx);
    }

    static async Task RunClientAsync(InvocationContext ctx)
    {
        int warmup = ctx.GetOption(Warmup);
        double idealMean = ctx.GetOption(IdealMean);
        double maxMeanError = ctx.GetOption(MaxMeanError);
        double maxDeviationError = ctx.GetOption(MaxVariation);

        int step = 0;
        long prevStamp = long.MinValue;
        int elements = 0; 
        double deltaSum = 0;
        double deltaSquaredSum = 0;
        
        (var client, ILogger logger) = TestCommon.ConstructClient<ClientInput, ServerInput, GameState, UpdateFrequencyTest>(ctx, clientProvider: ProvideInput);

        ClientInput ProvideInput()
        {
            if (step < warmup)
            {
                step++;
                return new();
            }

            if (step == warmup)
            {
                prevStamp = Stopwatch.GetTimestamp();
                step++;
                return new();
            }

            long current = Stopwatch.GetTimestamp();
            double delta = Stopwatch.GetElapsedTime(prevStamp, current).TotalSeconds;
            prevStamp = current;

            elements++;
            deltaSum += delta;
            deltaSquaredSum += delta * delta;

            return new();
        }

        logger.LogInformation("Starting update frequency test client.");

        await TestCommon.RunUntilCompleteAsync(client, logger, ctx);

        double mean = deltaSum / elements;
        double variance = deltaSquaredSum / elements - mean * mean;
        double deviation = Math.Sqrt(variance);

        logger.LogInformation("Test complete. Mean: {M}, deviation: {D}", mean, deviation);

        if (Math.Abs(deviation) > maxDeviationError)
        {
            logger.LogError("Deviation too high {A} > {B}.", deviation, maxDeviationError);
            ctx.FlagTestFail();
        }

        double meanError = Math.Abs(mean - idealMean);

        if (meanError > maxMeanError)
        {
            logger.LogError("Mean error is too high {A} > {B}.", meanError, maxMeanError);
            ctx.FlagTestFail();
        }
    }
}
