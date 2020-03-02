using Nito.AsyncEx;
using NullGuard;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hspi.Camera
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal abstract class CameraBase : IDisposable
    {
        public CameraBase(CancellationToken shutdown)
        {
            Updates = new AsyncProducerConsumerQueue<ICameraContruct>();
            sourceToken = CancellationTokenSource.CreateLinkedTokenSource(shutdown);
        }

        protected CancellationToken Token => sourceToken.Token;
        public AsyncProducerConsumerQueue<ICameraContruct> Updates { get; }

        public Task DownloadContinuousSnapshots(TimeSpan totalTimeSpan, TimeSpan interval)
        {
            var tasks = new List<Task>
            {
                DownloadSnapshot()
            };

            TimeSpan delay = interval;
            while (delay < totalTimeSpan)
            {
                tasks.Add(DownloadSnapshotWithDelay(delay));
                delay = delay.Add(interval);
            }

            return Task.WhenAll(tasks);
        }

        public abstract Task<string> DownloadSnapshot();

        private async Task DownloadSnapshotWithDelay(TimeSpan delay)
        {
            await Task.Delay(delay).ConfigureAwait(false);
            await DownloadSnapshot().ConfigureAwait(false);
        }
        protected readonly CancellationTokenSource sourceToken;
        #region IDisposable Support

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    sourceToken.Cancel();
                    sourceToken.Dispose();
                }

                disposedValue = true;
            }
        }

        private bool disposedValue = false; // To detect redundant calls
        #endregion IDisposable Support
    }
}