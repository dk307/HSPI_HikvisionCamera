using NullGuard;

namespace Hspi.DeviceData.Onvif
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class OnvifEventData : OnOffDeviceData
    {
        public OnvifEventData(string alarmType) : base(DeviceType.OnvifRoot, alarmType)
        {
        }

        public override bool IsRootDevice => false;
    }
}