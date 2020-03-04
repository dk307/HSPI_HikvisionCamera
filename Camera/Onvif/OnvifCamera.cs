using Hspi.Utils;
using Nito.AsyncEx;
using NullGuard;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using static System.FormattableString;

namespace Hspi.Camera.Onvif
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class OnvifCamera : CameraBase
    {
        public OnvifCamera(CameraSettings cameraSettings,
                           CancellationToken shutdown) :
            base(shutdown)
        {
            CameraSettings = cameraSettings;
            defaultHttpClient = CreateHttpClient();
            downloadHelper = new DownloadHelper(CameraSettings.Name, defaultHttpClient, Token);
            alarmProcessingHelper = new AlarmProcessingHelper(CameraSettings.Name,
                                                  CameraSettings.AlarmCancelInterval,
                                                  CloneWithDifferentState,
                                                  Enqueue,
                                                  Token);

            Utils.TaskHelper.StartAsyncWithErrorChecking(Invariant($"{cameraSettings.Name} Onvif Pull Events"),
                                                         ReceiveEvents,
                                                         Token);
        }

        public CameraSettings CameraSettings { get; }

        public override Task DownloadContinuousSnapshots(TimeSpan totalTimeSpan, TimeSpan interval)
        {
            return DownloadContinuousSnapshotsImpl(totalTimeSpan, interval);
        }

        public async Task<string> DownloadSnapshot(Uri downloadUri)
        {
            if (!Directory.Exists(CameraSettings.SnapshotDownloadDirectory))
            {
                throw new DirectoryNotFoundException("Directory Not Found:" + CameraSettings.SnapshotDownloadDirectory);
            }

            string path = Path.Combine(CameraSettings.SnapshotDownloadDirectory,
                                       DateTimeOffset.Now.ToString("yyyy-MM-dd--HH-mm-ss-ff", CultureInfo.InvariantCulture));

            return await downloadHelper.DownloadToFile(path, downloadUri, HttpMethod.Get, null, null).ConfigureAwait(false);
        }

        public async Task<Uri> GetSnapshotUri()
        {
            try
            {
                var onvifClient = await GetOnvifClient().ConfigureAwait(false);
                return await onvifClient.GetSnapshotUri(Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (!ex.IsCancelException())
                {
                    await ClearOnvifClient().ConfigureAwait(false);
                }
                throw;
            }
        }

        public async Task Reboot()
        {
            Trace.TraceInformation(Invariant($"[{CameraSettings.Name}]Rebooting ..."));

            try
            {
                var onvifClient = await GetOnvifClient().ConfigureAwait(false);
                await onvifClient.Reboot(Token).ConfigureAwait(false);
                await ClearOnvifClient().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (!ex.IsCancelException())
                {
                    await ClearOnvifClient().ConfigureAwait(false);
                }
                throw;
            }
        }

        private async Task ClearOnvifClient()
        {
            using (_ = await onvifClientLock.LockAsync(Token).ConfigureAwait(false))
            {
                onvifClient = null;
            }
        }

        private OnOffCameraContruct CloneWithDifferentState(OnOffCameraContruct cameraContruct, bool state)
        {
            var info = (OnvifEventInfo)cameraContruct;
            return new OnvifEventInfo(info.Id, state);
        }
        private HttpClient CreateHttpClient()
        {
            var credCache = new CredentialCache();
            var credentials = new NetworkCredential(CameraSettings.Login, CameraSettings.Password);
            credCache.Add(new Uri(CameraSettings.CameraHost), "Digest", credentials);

#pragma warning disable CA2000 // Dispose objects before losing scope
            var httpClientHandler = new HttpClientHandler
            {
                Credentials = credCache,
                MaxConnectionsPerServer = 4,
            };
#pragma warning restore CA2000 // Dispose objects before losing scope

            if (httpClientHandler.SupportsAutomaticDecompression)
            {
                httpClientHandler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            }

            var httpClient = new HttpClient(httpClientHandler, true)
            {
                Timeout = TimeSpan.FromSeconds(120)
            };

            return httpClient;
        }

        private async Task DownloadContinuousSnapshotsImpl(TimeSpan totalTimeSpan, TimeSpan interval)
        {
            var helper = new OnvifSnapshotsHelper(this, Token);
            await helper.Initialize().ConfigureAwait(false);
            await helper.DownloadContinuousSnapshots(totalTimeSpan, interval).ConfigureAwait(false);
        }
        private async Task Enqueue(OnOffCameraContruct onvifEvent)
        {
            Trace.WriteLine(Invariant($"[{CameraSettings.Name}]Event:{onvifEvent.Id} Enabled:{onvifEvent.Active}"));
            await Updates.EnqueueAsync(onvifEvent, Token).ConfigureAwait(false);
        }

        private async Task<OnvifClient> GetOnvifClient()
        {
            try
            {
                using (_ = await onvifClientLock.LockAsync(Token).ConfigureAwait(false))
                {
                    if (onvifClient == null)
                    {
                        Uri deviceUri = new Uri(CameraSettings.CameraHost);
                        var credential = new NetworkCredential(CameraSettings.Login, CameraSettings.Password);
                        var timeout = TimeSpan.FromSeconds(60);

                        var connectionParameters = new ConnectionParameters(deviceUri, credential, timeout);
                        var onvifClientTemp = new OnvifClient(connectionParameters, timeout);

                        await onvifClientTemp.ConnectAsync(Token).ConfigureAwait(false);

                        this.onvifClient = onvifClientTemp;
                    }
                    return this.onvifClient;
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(Invariant($"[{CameraSettings.Name}]Failed to Connect to {CameraSettings.CameraHost} with {ex.GetFullMessage()}."));
                throw;
            }
        }
        private async void OnvifClient_EventReceived(object sender, DeviceEvent deviceEvent)
        {
            bool? valueAsBoolean = deviceEvent.ValueAsBoolean;
            if (valueAsBoolean.HasValue)
            {
                var onvifEventInfo = new OnvifEventInfo(deviceEvent.Id, valueAsBoolean.Value);
                await alarmProcessingHelper.ProcessNewAlarm(onvifEventInfo).ConfigureAwait(false);
            }
        }

        private async Task ReceiveEvents()
        {
            OnvifClient onvifClient = null;
            try
            {
                onvifClient = await GetOnvifClient().ConfigureAwait(false);
                onvifClient.EventReceived += OnvifClient_EventReceived;
                await onvifClient.ReceiveAsync(Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (!ex.IsCancelException())
                {
                    await ClearOnvifClient().ConfigureAwait(false);
                }
            }
            finally
            {
                if (onvifClient != null)
                {
                    onvifClient.EventReceived -= OnvifClient_EventReceived;
                }
            }
        }
        private readonly AlarmProcessingHelper alarmProcessingHelper;
        private readonly HttpClient defaultHttpClient;
        private readonly DownloadHelper downloadHelper;
        private OnvifClient onvifClient;
        private AsyncLock onvifClientLock = new AsyncLock();

        #region IDisposable Support

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                defaultHttpClient?.Dispose();
            }
        }

        #endregion IDisposable Support
    }
}