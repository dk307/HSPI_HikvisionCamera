using HomeSeerAPI;
using Hspi.Camera;
using Hspi.DeviceData;
using Hspi.Utils;
using Nito.AsyncEx;
using Nito.AsyncEx.Synchronous;
using NullGuard;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Hspi
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class CameraManager : IDisposable
    {
        public CameraManager(IHSApplication HS,
                            CameraSettings cameraSettings,
                            CancellationToken shutdownDownToken)
        {
            this.HS = HS;
            this.CameraSettings = cameraSettings;
            this.cancelTokenSource = new CombinedCancelToken(shutdownDownToken);
            rootDeviceData = new DeviceRootDeviceManager(cameraSettings, this.HS, cancelTokenSource.Token);
            camera = new HikvisionCamera(CameraSettings, cancelTokenSource.Token);

            processUpdatesTask = Task.Factory.StartNew(ProcessUpdates,
                                cancelTokenSource.Token,
                                TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                                TaskScheduler.Default).WaitAndUnwrapException(cancelTokenSource.Token);
        }

        private async Task ProcessUpdates()
        {
            while (!cancelTokenSource.Token.IsCancellationRequested)
            {
                var update = await camera.Updates.DequeueAsync(cancelTokenSource.Token).ConfigureAwait(false);
                rootDeviceData.ProcessUpdate(update);
            }
        }

        public async Task HandleCommand(DeviceIdentifier deviceIdentifier, string stringValue, double value, ePairControlUse control)
        {
            if (deviceIdentifier.DeviceId != CameraSettings.Id)
            {
                throw new ArgumentException("Invalid Device Identifier");
            }

            using (var deviceLock = await rootDeviceDataLock.LockAsync(cancelTokenSource.Token).ConfigureAwait(false))
            {
                await rootDeviceData.HandleCommand(deviceIdentifier, camera, stringValue, value, control).ConfigureAwait(false);
            }
        }

        private void DisposeConnector()
        {
            if (camera != null)
            {
                camera.Dispose();
            }
        }

        #region IDisposable Support

        public CameraSettings CameraSettings { get; }

        public void Dispose()
        {
            if (!disposedValue)
            {
                Trace.WriteLine(Invariant($"[{CameraSettings.Name}]Disposing Camera Manager"));
                cancelTokenSource.Cancel();
                processUpdatesTask.WaitWithoutException();
                DisposeConnector();
                cancelTokenSource.Dispose();

                disposedValue = true;
            }
        }

        private bool disposedValue = false; // To detect redundant calls

        #endregion IDisposable Support

        private readonly HikvisionCamera camera;
        private readonly Task processUpdatesTask;
        private readonly CombinedCancelToken cancelTokenSource;
        private readonly IHSApplication HS;
        private readonly DeviceRootDeviceManager rootDeviceData;
        private readonly AsyncLock rootDeviceDataLock = new AsyncLock();
    }
}