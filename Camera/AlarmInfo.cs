using Hspi.DeviceData;
using NullGuard;

namespace Hspi.Camera
{

    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class AlarmInfo : ICameraContruct
    {
        public AlarmInfo(string alarmType, int channelID, bool active)
        {
            if (string.IsNullOrWhiteSpace(alarmType))
            {
                throw new System.ArgumentException("Invalid Alarm Type", nameof(alarmType));
            }

            AlarmType = alarmType;
            ChannelID = channelID;
            Active = active;
        }

        public bool Active { get; }

        public string AlarmType { get; }
        public int ChannelID { get; }

        public DeviceType DeviceType => DeviceType.Alarm;

        public string Id => AlarmReadableName(AlarmType);

        public string Value =>  !Active ? OnOffDeviceData.OffValueString: OnOffDeviceData.OnValueString;

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