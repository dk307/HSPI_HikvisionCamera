using Hspi.DeviceData;
using NullGuard;

namespace Hspi.Camera.Onvif
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class OnvifEventInfo : OnOffCameraContruct
    {
        public OnvifEventInfo(string id, bool active) :
            base(id, DeviceType.OnvifEvent, active)
        {
        }
    };
}