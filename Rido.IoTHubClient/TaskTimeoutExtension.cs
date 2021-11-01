using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Rido.IoTHubClient
{
    public static class TaskTimeoutExtension
    {
        public static async Task<TResult> TimeoutAfter<TResult>(this Task<TResult> task, TimeSpan timeout)
        {
            using var timeoutCancellationTokenSource = new CancellationTokenSource();
            var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token));
            if (completedTask == task)
            {
                timeoutCancellationTokenSource.Cancel();
                return await task;
            }
            else
            {
                throw new TimeoutException($"{nameof(TimeoutAfter)}: The operation has timed out after {timeout:mm\\:ss}");
            }

        }
    }
}