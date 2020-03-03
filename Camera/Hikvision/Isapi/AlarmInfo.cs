using Hspi.DeviceData;
using NullGuard;

using static System.FormattableString;

namespace Hspi.Camera.Hikvision.Isapi
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class AlarmInfo : OnOffCameraContruct
    {
        public AlarmInfo(string alarmType, int channelID, bool active) :
            base(Invariant($"{AlarmReadableName(alarmType)}_{channelID}"), DeviceType.HikvisionISAPIAlarm, active)
        {
            if (string.IsNullOrWhiteSpace(alarmType))
            {
                throw new System.ArgumentException("Invalid Alarm Type", nameof(alarmType));
            }

            AlarmType = alarmType;
            ChannelID = channelID;
        }

        public string AlarmType { get; }
        public int ChannelID { get; }

        private static string AlarmReadableName(string alarmType)
        {
            switch (alarmType)
            {
                case "VMD":
                    return "Motion Detection";

                default:
                    return alarmType;
            }
        }
    };
}