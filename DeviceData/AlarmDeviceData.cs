using NullGuard;

namespace Hspi.DeviceData
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class AlarmDeviceData : OnOffDeviceData
    {
        public AlarmDeviceData(string alarmType) : base(DeviceType.Alarm, alarmType)
        {
        }
    } 
}