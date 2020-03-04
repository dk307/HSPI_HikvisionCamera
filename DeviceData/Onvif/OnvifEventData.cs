using NullGuard;

namespace Hspi.DeviceData.Onvif
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class OnvifEventData : OnOffDeviceData
    {
        public OnvifEventData(string id) : base(DeviceType.OnvifEvent, id)
        {
        }

        public override bool IsRootDevice => false;
    }
}