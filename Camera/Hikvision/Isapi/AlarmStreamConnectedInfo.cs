using Hspi.DeviceData;
using NullGuard;

namespace Hspi.Camera.Hikvision.Isapi
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class AlarmStreamConnectedInfo : OnOffCameraContruct
    {
        public AlarmStreamConnectedInfo(bool active) :
            base("Alarm Streem Connected", DeviceType.HikvisionISAPIAlarmStreamConnected, active)
        {
        }
    };
}