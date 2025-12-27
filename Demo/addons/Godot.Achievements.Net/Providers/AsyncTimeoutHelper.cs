using System;
using System.Threading.Tasks;

namespace Godot.Achievements.Providers;

/// <summary>
/// Helper class for awaiting tasks with timeout support.
/// </summary>
public static class AsyncTimeoutHelper
{
    /// <summary>
    /// Awaits a task with a timeout. Returns the task result or the timeout value if timed out.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="task">The task to await.</param>
    /// <param name="timeoutSeconds">Timeout in seconds.</param>
    /// <param name="timeoutValue">Value to return if the operation times out.</param>
    /// <returns>The task result or timeout value.</returns>
    public static async Task<T> AwaitWithTimeout<T>(Task<T> task, double timeoutSeconds, T timeoutValue)
    {
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
        var completedTask = await Task.WhenAny(task, timeoutTask);

        if (completedTask == timeoutTask)
            return timeoutValue;

        return await task;
    }

    /// <summary>
    /// Awaits a TaskCompletionSource with a timeout.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="tcs">The TaskCompletionSource to await.</param>
    /// <param name="timeoutSeconds">Timeout in seconds.</param>
    /// <param name="timeoutValue">Value to return if the operation times out.</param>
    /// <returns>The task result or timeout value.</returns>
    public static Task<T> AwaitWithTimeout<T>(TaskCompletionSource<T> tcs, double timeoutSeconds, T timeoutValue)
    {
        return AwaitWithTimeout(tcs.Task, timeoutSeconds, timeoutValue);
    }
}
