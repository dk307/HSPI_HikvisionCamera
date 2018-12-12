using Hspi.DeviceData;
using NullGuard;

namespace Hspi.Camera
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class AlarmInfo : ICameraContruct
    {
        public AlarmInfo(string alarmType, bool active)
        {
            if (string.IsNullOrWhiteSpace(alarmType))
            {
                throw new System.ArgumentException("Invalid Alarm Type", nameof(alarmType));
            }

            AlarmType = alarmType;
            Active = active;
        }

        public bool Active { get; }

        public string AlarmType { get; }

        public DeviceType DeviceType => DeviceType.Alarm;

        public string Id => AlarmReadableName(AlarmType);

        public const string AlarmOffValue = "0";

        public string Value
        {
            get
            {
                return !Active ? AlarmOffValue : "1";
            }
        }

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