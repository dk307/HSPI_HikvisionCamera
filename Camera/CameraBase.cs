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

        public abstract Task DownloadContinuousSnapshots(TimeSpan totalTimeSpan, TimeSpan interval);

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