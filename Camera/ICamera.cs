using Nito.AsyncEx;
using System;
using System.Threading.Tasks;

namespace Hspi.Camera
{
    internal interface ICamera : System.IDisposable
    {
        AsyncProducerConsumerQueue<ICameraContruct> Updates { get; }

        Task DownloadContinuousSnapshots(TimeSpan totalTimeSpan, TimeSpan interval);
    }
}