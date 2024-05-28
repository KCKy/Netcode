using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Kcky.Useful;

/// <summary>
/// Utility extension methods for tasks.
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    /// Non-blocking method, which assures the given task completes successfully, otherwise an error is signaled via the provided logger. 
    /// </summary>
    /// <param name="task">Task to assure.</param>
    /// <param name="logger">A logger to use for signalling the potential error.</param>
    /// <example>
    /// <code>
    /// var task = FooAsync(bar);
    /// task.AssureSuccess(); // This does not block, we do not care when the task finishes
    /// </code>
    /// In cases where it is not desirable to await a Task, calling <see cref="AssureSuccess"/> makes sure potential errors or cancellations are logged.
    /// FooAsync therefore does some side effect but returns no direct result.
    /// </example>
    public static void AssureSuccess(this Task task, ILogger logger) => AssureSuccessInternal(task, logger);

    /// <summary>
    /// Non-blocking method, which assures the given task completes successfully or is canceled, otherwise an error is signaled via the provided logger. 
    /// </summary>
    /// <param name="task">Task to assure.</param>
    /// <param name="logger">A logger to use for signalling the potential error.</param>
    /// <example>
    /// <code>
    /// var task = FooAsync(bar);
    /// task.AssureNoFault(); // This does not block, we do not care when the task finishes
    /// </code>
    /// In cases where it is not desirable to await a Task, calling <see cref="AssureNoFault"/> makes sure potential exceptions are logged.
    /// FooAsync therefore does some side effect but returns no direct result.
    /// </example>
    public static void AssureNoFault(this Task task, ILogger logger) => AssureNoFaultInternal(task, logger);

    static async void AssureSuccessInternal(this Task task, ILogger logger)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            logger.LogError(Cancelled);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, Exception);
        }
    }

    static async void AssureNoFaultInternal(Task task, ILogger logger)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
           logger.LogError(ex, Exception);
        }
    }

    const string Exception = "Assured task faulted with exception.";
    const string Cancelled = "Assured task was cancelled.";
}
