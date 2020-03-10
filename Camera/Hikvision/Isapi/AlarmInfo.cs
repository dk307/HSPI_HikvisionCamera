using Hspi.DeviceData;
using NullGuard;
using System.Globalization;
using System.Linq;
using static System.FormattableString;

namespace Hspi.Camera.Hikvision.Isapi
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class AlarmInfo : OnOffCameraContruct
    {
        public AlarmInfo(string alarmType, int channelID, bool active) :
            base(Invariant($"Channel {channelID} - {AlarmReadableName(alarmType)}"), DeviceType.HikvisionISAPIAlarm, active)
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
                    return FirstCharToUpper(alarmType, CultureInfo.InvariantCulture);
            }
        }

        private static string FirstCharToUpper(string input, CultureInfo culture)
        {
            switch (input)
            {
                case null: return null;
                case "": return string.Empty;
                default: return input.First().ToString(culture).ToUpper(culture) + input.Substring(1);
            }
        }
    };
}