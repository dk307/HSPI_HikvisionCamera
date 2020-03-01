using NullGuard;

namespace Hspi.DeviceData.Hikvision.Isapi
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]

    internal sealed class AlarmConnectedDeviceData : OnOffDeviceData
    {
        public AlarmConnectedDeviceData() : base(DeviceType.HikvisionISAPIAlarmStreamConnected, "Alarm Stream Connected")
        {
        }

        public override bool IsRootDevice => false;
    }
}