using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hspi.Camera
{
    internal abstract class SnapshotsHelper
    {
        public SnapshotsHelper(CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;
        }

        public async Task DownloadContinuousSnapshots(TimeSpan totalTimeSpan, TimeSpan interval)
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

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        public abstract Task<string> DownloadSnapshot();

        private async Task DownloadSnapshotWithDelay(TimeSpan delay)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            await DownloadSnapshot().ConfigureAwait(false);
        }

        protected readonly CancellationToken cancellationToken;
    }
}