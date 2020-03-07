using NullGuard;

namespace Hspi.DeviceData.Onvif
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class EventsListeningDeviceData : OnOffDeviceData
    {
        public EventsListeningDeviceData() : base(DeviceType.OnvifEventListening, "Events Listening")
        {
        }

        public override bool IsRootDevice => false;
    }
}