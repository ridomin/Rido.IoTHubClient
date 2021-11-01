using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Rido.IoTHubClient
{
    public static class TaskTimeoutExtension
    {
        public static async Task<T> TimeoutAfter<T>(this Task<T> source, TimeSpan timeout)
        {
            if (await Task.WhenAny(source,Task.Delay(timeout)) != source)
            {
                throw new TimeoutException();
            }
            return await source;
        }
    }
}