using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Kcky.Useful;

/// <summary>
/// Utility extension methods for tasks.
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    /// Method called when a task given to <see cref="AssureSuccess"/> or <see cref="AssureNoFault"/> has been cancelled.
    /// </summary>
    /// <param name="task">The task which faulted.</param>
    /// <param name="exception">The exception causing the fault.</param>
    public delegate void FaultDelegate(Task task, Exception exception);

    /// <summary>
    /// Method called when a task given to <see cref="AssureSuccess"/> has been cancelled.
    /// </summary>
    /// <param name="task">The task which was cancelled.</param>
    public delegate void CanceledDelegate(Task task);
    
    /// <summary>
    /// Non-blocking method, which assures the given task completes successfully, otherwise an error is signaled via <see cref="OnCanceled"/> or <see cref="OnFault"/>.
    /// </summary>
    /// <param name="task">Task to assure.</param>
    /// <example>
    /// <code>
    /// var task = FooAsync(bar);
    /// task.AssureSuccess(); // This does not block, we do not care when the task finishes
    /// </code>
    /// In cases where it is not desirable to await a Task, calling <see cref="AssureSuccess"/> makes sure potential errors or cancellations are logged.
    /// FooAsync therefore does some side effect but returns no direct result.
    /// </example>
    public static void AssureSuccess(this Task task) => task.ContinueWith(TestTaskSuccess);

    /// <summary>
    /// Non-blocking method, which assures the given task completes successfully or is canceled, otherwise an error is signaled via <see cref="OnCanceled"/>. 
    /// </summary>
    /// <param name="task">Task to assure.</param>
    /// <example>
    /// <code>
    /// var task = FooAsync(bar);
    /// task.AssureNoFault(); // This does not block, we do not care when the task finishes
    /// </code>
    /// In cases where it is not desirable to await a Task, calling <see cref="AssureNoFault"/> makes sure potential exceptions are logged.
    /// FooAsync therefore does some side effect but returns no direct result.
    /// </example>
    public static void AssureNoFault(this Task task) => task.ContinueWith(TestTaskNoFault);

    /// <summary>
    /// Called when an assured task fails, although it should not.
    /// </summary>
    public static event FaultDelegate? OnFault;

    /// <summary>
    /// Called when an assured task is canceled, although it should not.
    /// </summary>
    public static event CanceledDelegate? OnCanceled;

    static void TestTaskSuccess(Task task)
    {
        switch (task.Status)
        {
            case TaskStatus.RanToCompletion:
                return;
            case TaskStatus.Faulted:
                OnFault?.Invoke(task, task.Exception!);
                return;
            case TaskStatus.Canceled:
                OnCanceled?.Invoke(task);
                return;
            case TaskStatus.Created:
            case TaskStatus.WaitingForActivation:
            case TaskStatus.WaitingToRun:
            case TaskStatus.Running:
            case TaskStatus.WaitingForChildrenToComplete:
            default:
                Debug.Assert(false); // This should not happen as this is a continuation of the task
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
                OnFault?.Invoke(task, task.Exception!);
                return;
            case TaskStatus.Created:
            case TaskStatus.WaitingForActivation:
            case TaskStatus.WaitingToRun:
            case TaskStatus.Running:
            case TaskStatus.WaitingForChildrenToComplete:
            default:
                Debug.Assert(false); // This should not happen as this is a continuation of the task
                return;
        }
    }
}
