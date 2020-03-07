using Hspi.DeviceData;
using NullGuard;

namespace Hspi.Camera.Onvif
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class EventsListeningInfo : OnOffCameraContruct
    {
        public EventsListeningInfo(bool active) :
            base("Events Listening", DeviceType.OnvifEventListening, active)
        {
        }
    };
}