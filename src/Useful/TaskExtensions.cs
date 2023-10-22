using System.Diagnostics;

namespace Useful;

/// <summary>
/// Utility extension methods for tasks.
/// </summary>
public static class TaskExtensions
{
    public delegate void FaultDelegate(Task task, Exception exception);
    public delegate void CanceledDelegate(Task task);
    
    /// <summary>
    /// Non-blocking method, which assures the given task completes successfully, otherwise an error is signaled via <see cref="OnCanceled"/> or <see cref="OnFault"/>.
    /// </summary>
    /// <param name="task">Task to await</param>
    public static void AssureSuccess(this Task task) => task.ContinueWith(TestTaskSuccess);

    /// <summary>
    /// Non-blocking method, which assures the given task completes successfully or is canceled, otherwise an error is signaled via <see cref="OnCanceled"/>. 
    /// </summary>
    /// <param name="task">Task to await</param>
    public static void AssureNoFault(this Task task) => task.ContinueWith(TestTaskNoFault);

    /// <summary>
    /// Called when an assured task fails although it should not.
    /// </summary>
    public static event FaultDelegate? OnFault;

    /// <summary>
    /// Called when an assured task is canceled although it should not.
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
                OnFault?.Invoke(task, task.Exception!);
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
}
