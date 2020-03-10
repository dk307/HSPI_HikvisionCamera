using NullGuard;

namespace Hspi.DeviceData.Hikvision.Isapi
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class AlarmDeviceData : OnOffDeviceData
    {
        public AlarmDeviceData(string alarmType) : base(DeviceType.HikvisionISAPIAlarm, alarmType)
        {
        }

        public override bool IsRootDevice => false;
    }
}