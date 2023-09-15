using Serilog;
using System.Diagnostics;

namespace Core.Extensions;

/// <summary>
/// Utility extension methods for tasks.
/// </summary>
public static class TaskExtensions
{
    static void TestTaskSuccess(Task task)
    {
        switch (task.Status)
        {
            case TaskStatus.RanToCompletion:
                return;
            case TaskStatus.Faulted:
                Log.Information("Task {Task} assure failed: the task faulted with exception: {Exception}", task, task.Exception);
                return;
            case TaskStatus.Canceled:
                Log.Information("Task {Task} assure failed: the task was canceled.", task);
                return;
            case TaskStatus.Created:
            case TaskStatus.WaitingForActivation:
            case TaskStatus.WaitingToRun:
            case TaskStatus.Running:
            case TaskStatus.WaitingForChildrenToComplete:
            default:
                Debug.Assert(false); // This should not happen as the this is a continuation of the task
                return;
        }
    }

    static void TestTaskNoFault(Task task)
    {
        switch (task.Status)
        {
            case TaskStatus.RanToCompletion:
            case TaskStatus.Canceled:
                return;
            case TaskStatus.Faulted:
                Log.Information("Task {Task} assure failed: the task faulted with exception: {Exception}", task, task.Exception);
                return;
            case TaskStatus.Created:
            case TaskStatus.WaitingForActivation:
            case TaskStatus.WaitingToRun:
            case TaskStatus.Running:
            case TaskStatus.WaitingForChildrenToComplete:
            default:
                Debug.Assert(false); // This should not happen as the this is a continuation of the task
                return;
        }
    }

    /// <summary>
    /// Non-blocking method, which assures the given task completes successfully, otherwise an error is written into debug. 
    /// </summary>
    /// <param name="task">Task to await</param>
    public static void AssureSuccess(this Task task) => task.ContinueWith(TestTaskSuccess);

    /// <summary>
    /// Non-blocking method, which assures the given task completes successfully or is canceled, otherwise an error is written into debug. 
    /// </summary>
    /// <param name="task">Task to await</param>
    public static void AssureNoFault(this Task task) => task.ContinueWith(TestTaskNoFault);
}
