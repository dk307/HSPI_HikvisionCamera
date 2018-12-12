using System;
using System.Threading;

namespace Hspi.Utils
{
    internal sealed class CombinedCancelToken : IDisposable
    {
        public CombinedCancelToken(CancellationToken shutdownToken)
        {
            this.combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken);
        }

        public CancellationToken Token => combinedTokenSource.Token;

        public void Cancel()
        {
            combinedTokenSource.Cancel();
        }

        public void Dispose()
        {
            combinedTokenSource?.Dispose();
        }

        private readonly CancellationTokenSource combinedTokenSource;
    }
}