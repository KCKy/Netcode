using System;
using System.Threading.Tasks;
using MemoryPack;

namespace FrameworkTest.Extensions;

/// <summary>
/// Utility extension methods for tasks.
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    /// Test whether an ended task succeeds. Writes an error if it fails.
    /// </summary>
    /// <param name="task">A completed <see cref="Task"/>.</param>
    /// <exception cref="ArgumentException">Given task did not end yet.</exception>
    static void TestTaskSuccess(Task task)
    {
        switch (task.Status)
        {
            case TaskStatus.RanToCompletion:
                return;
            case TaskStatus.Faulted:
                Console.WriteLine($"Task {task} assure failed: the task faulted with exception: {task.Exception}");
                return;
            case TaskStatus.Canceled:
                Console.WriteLine($"Task {task} assure failed: the task was canceled.");
                return;
            case TaskStatus.Created:
            case TaskStatus.WaitingForActivation:
            case TaskStatus.WaitingToRun:
            case TaskStatus.Running:
            case TaskStatus.WaitingForChildrenToComplete:
            default:
                throw new ArgumentException(); // This should not happen as the this is a continuation of the task
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
                Console.WriteLine($"Task {task} assure failed: the task faulted with exception: {task.Exception}");
                return;
            case TaskStatus.Created:
            case TaskStatus.WaitingForActivation:
            case TaskStatus.WaitingToRun:
            case TaskStatus.Running:
            case TaskStatus.WaitingForChildrenToComplete:
            default:
                throw new ArgumentException(); // This should not happen as the this is a continuation of the task
        }
    }

    /// <summary>
    /// Non-blocking method, which assures the given task completes successfully, otherwise an error is written. 
    /// </summary>
    /// <param name="task">Task to await</param>
    public static void AssureSuccess(this Task task) => task.ContinueWith(TestTaskSuccess);

    public static void AssureNoFault(this Task task) => task.ContinueWith(TestTaskNoFault);
}

public static class ObjectExtensions
{
    public static T MemoryPackCopy<T>(this T value) where T : notnull
    {
        var data = MemoryPackSerializer.Serialize(value);
        return MemoryPackSerializer.Deserialize<T>(data) ?? throw new Exception();
    }
}
