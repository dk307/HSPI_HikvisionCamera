using Hspi.Camera.Onvif.Behaviour;
using Hspi.Camera.Onvif.Security;
using Hspi.Onvif.Contracts.DeviceManagement;
using Hspi.Onvif.Contracts.Event;
using Hspi.Onvif.Contracts.Media;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading;
using System.Threading.Tasks;
using DateTime = System.DateTime;

namespace Hspi.Camera.Onvif
{
    public sealed class OnvifClient : IDisposable
    {
        public OnvifClient(ConnectionParameters connectionParameters, TimeSpan subscriptionTerminationTime)
        {
            if (connectionParameters == null)
                throw new ArgumentNullException(nameof(connectionParameters));

            ConnectionParameters = connectionParameters;
            this.subscriptionTerminationTime = subscriptionTerminationTime;

            deviceServicePath = connectionParameters.ConnectionUri.AbsolutePath;

            if (deviceServicePath == "/")
                deviceServicePath = DefaultDeviceServicePath;
        }

        public ConnectionParameters ConnectionParameters { get; }

        public AsyncProducerConsumerQueue<NotificationMessageHolderType> DeviceEvents { get; }
                                                    = new AsyncProducerConsumerQueue<NotificationMessageHolderType>();
        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            DateTime deviceTime = await GetDeviceTimeAsync().ConfigureAwait(false);

            if (!string.IsNullOrEmpty(ConnectionParameters.Credentials.UserName))
            {
                byte[] nonceBytes = new byte[20];
                var random = new Random();
                random.NextBytes(nonceBytes);

                var token = new SecurityToken(deviceTime, nonceBytes);

                onvifClientFactory.SetSecurityToken(token);
            }

            cancellationToken.ThrowIfCancellationRequested();

            _deviceCapabilities = await GetDeviceCapabilitiesAsync().ConfigureAwait(false);

            if (_deviceCapabilities.Events == null || !_deviceCapabilities.Events.WSPullPointSupport)
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                throw new Exception("Device doesn't support pull point subscription");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
            }
        }

        public void Dispose()
        {
        }

        public async Task<string> Reboot()
        {
            var device = CreateDeviceClient();
            return await device.SystemRebootAsync().ConfigureAwait(false);
        }

        public async Task ReceiveAsync(CancellationToken cancellationToken)
        {
            var eventServiceUri = new Uri(_deviceCapabilities.Events.XAddr);
            EndpointAddress endPointAddress = await GetSubscriptionEndPointAddress(eventServiceUri).ConfigureAwait(false);
            await PullPointAsync(endPointAddress, cancellationToken).ConfigureAwait(false);
        }

        public async Task TakeSnapshot()
        {
            var deviceCapabilitiesUri = new Uri(_deviceCapabilities.Media.XAddr);
            var media = onvifClientFactory.CreateClient<Media>(deviceCapabilitiesUri, ConnectionParameters, MessageVersion.Soap12);

            var profileResponse = await media.GetProfilesAsync(new GetProfilesRequest()).ConfigureAwait(false);

            var profile = profileResponse.Profiles.First(x => x.Name == "mainStream") ?? profileResponse.Profiles.FirstOrDefault();

            if (profile == null)
            {
                throw new Exception("No Onvif profile found");
            }

            var snapshotUriResponse = await media.GetSnapshotUriAsync(profile.token).ConfigureAwait(false);

            Uri snapShotUri = new Uri(snapshotUriResponse.Uri);
        }

 
        private static bool IsTimeOver(int previousTicks, int interval)
        {
            if (Math.Abs(Environment.TickCount - previousTicks) > interval)
                return true;

            return false;
        }

        private Device CreateDeviceClient()
        {
            Uri deviceServiceUri = GetServiceUri(deviceServicePath);

            var deviceClient = onvifClientFactory.CreateClient<Device>(deviceServiceUri,
                ConnectionParameters, MessageVersion.Soap12);

            return deviceClient;
        }

        private async Task<Hspi.Onvif.Contracts.DeviceManagement.Capabilities> GetDeviceCapabilitiesAsync()
        {
            Device deviceClient = CreateDeviceClient();

            GetCapabilitiesResponse capabilitiesResponse =
                await deviceClient.GetCapabilitiesAsync(new GetCapabilitiesRequest(new[] { CapabilityCategory.All })).ConfigureAwait(false);

            return capabilitiesResponse.Capabilities;
        }

        private async Task<DateTime> GetDeviceTimeAsync()
        {
            Device deviceClient = CreateDeviceClient();
            SystemDateTime deviceSystemDateTime = await deviceClient.GetSystemDateAndTimeAsync().ConfigureAwait(false);

            DateTime deviceTime;
            if (deviceSystemDateTime.UTCDateTime == null)
                deviceTime = DateTime.UtcNow;
            else
            {
                deviceTime = new DateTime(deviceSystemDateTime.UTCDateTime.Date.Year,
                    deviceSystemDateTime.UTCDateTime.Date.Month,
                    deviceSystemDateTime.UTCDateTime.Date.Day, deviceSystemDateTime.UTCDateTime.Time.Hour,
                    deviceSystemDateTime.UTCDateTime.Time.Minute, deviceSystemDateTime.UTCDateTime.Time.Second, 0,
                    DateTimeKind.Utc);
            }

            return deviceTime;
        }

        private Uri GetServiceUri(string serviceRelativePath)
        {
            return new Uri(ConnectionParameters.ConnectionUri, serviceRelativePath);
        }

        private async Task<EndpointAddress> GetSubscriptionEndPointAddress(Uri eventServiceUri)
        {
            var portTypeClient = onvifClientFactory.CreateClient<EventPortType>(eventServiceUri,
                ConnectionParameters, MessageVersion.Soap12WSAddressing10);

            string terminationTime = GetTerminationTime();
            var subscriptionRequest = new CreatePullPointSubscriptionRequest(null, terminationTime, null, null);
            CreatePullPointSubscriptionResponse response =
                await portTypeClient.CreatePullPointSubscriptionAsync(subscriptionRequest).ConfigureAwait(false);

            var subscriptionRefUri = new Uri(response.SubscriptionReference.Address.Value);

            var adressHeaders = new List<AddressHeader>();

            if (response.SubscriptionReference.ReferenceParameters?.Any != null)
                foreach (System.Xml.XmlElement element in response.SubscriptionReference.ReferenceParameters.Any)
                    adressHeaders.Add(new CustomAddressHeader(element));

            var seviceUri = GetServiceUri(subscriptionRefUri.PathAndQuery);
            var endPointAddress = new EndpointAddress(seviceUri, adressHeaders.ToArray());
            return endPointAddress;
        }

        private string GetTerminationTime()
        {
            return FormattableString.Invariant($"PT{(int)subscriptionTerminationTime.TotalSeconds}S");
        }
        private async Task PullPointAsync(EndpointAddress endPointAddress, CancellationToken cancellationToken)
        {
            var pullPointSubscriptionClient = onvifClientFactory.CreateClient<PullPointSubscription>(endPointAddress, ConnectionParameters,
                    MessageVersion.Soap12WSAddressing10);
            var subscriptionManagerClient = onvifClientFactory.CreateClient<SubscriptionManager>(endPointAddress, ConnectionParameters,
                MessageVersion.Soap12WSAddressing10);

            var pullRequest = new PullMessagesRequest("PT10S", 1024, null);

            int renewIntervalMs = (int)(subscriptionTerminationTime.TotalMilliseconds / 2);
            int lastTimeRenewMade = Environment.TickCount;

            while (!cancellationToken.IsCancellationRequested)
            {
                PullMessagesResponse response = await pullPointSubscriptionClient.PullMessagesAsync(pullRequest).ConfigureAwait(false);

                foreach (var messageHolder in response.NotificationMessage)
                {
                    await DeviceEvents.EnqueueAsync(messageHolder, cancellationToken).ConfigureAwait(false);
                }

                if (IsTimeOver(lastTimeRenewMade, renewIntervalMs))
                {
                    lastTimeRenewMade = Environment.TickCount;
                    var renew = new Renew { TerminationTime = GetTerminationTime() };
                    await subscriptionManagerClient.RenewAsync(new RenewRequest(renew)).ConfigureAwait(false);
                }
            }

            await subscriptionManagerClient.UnsubscribeAsync(new UnsubscribeRequest(new Unsubscribe())).ConfigureAwait(false);
        }
        private const string DefaultDeviceServicePath = "/onvif/device_service";
        private readonly string deviceServicePath;
        private readonly OnvifClientFactory onvifClientFactory = new OnvifClientFactory();
        private readonly TimeSpan subscriptionTerminationTime;
        private Hspi.Onvif.Contracts.DeviceManagement.Capabilities _deviceCapabilities;
    }
}