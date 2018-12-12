using Scheduler.Classes;
using System;
using static System.FormattableString;
using System.ComponentModel;

namespace Hspi.DeviceData
{
    internal enum DeviceType
    {
        [Description("Camera Property String")]
        Root = 0,

        [Description("Camera Property String")]
        CameraPropertyString = 1,

        [Description("Camera Property Number")]
        CameraPropertyNumber = 2,

        Alarm,
    }

    internal class DeviceIdentifier
    {
        public DeviceIdentifier(string deviceId, DeviceType deviceType, string deviceTypeId)
        {
            DeviceId = deviceId;
            DeviceType = deviceType;
            DeviceTypeId = deviceTypeId;
        }

        public static string CreateDeviceIdSpecficAddress(string deviceId)
        {
            return Invariant($"{PluginData.PlugInName}{AddressSeparator}{deviceId}");
        }

        public string Address => Invariant($"{CreateDeviceIdSpecficAddress(DeviceId)}{AddressSeparator}{DeviceType}{AddressSeparator}{DeviceTypeId}");
        public string DeviceId { get; }
        public DeviceType DeviceType { get; }
        public string DeviceTypeId { get; }

        public static DeviceIdentifier Identify(DeviceClass hsDevice)
        {
            var childAddress = hsDevice.get_Address(null);

            var parts = childAddress.Split(AddressSeparator);

            if (parts.Length != 4)
            {
                return null;
            }

            if (!Enum.TryParse(parts[2], out DeviceType deviceType))
            {
                return null;
            }

            string deviceTypeData = parts[3];
            if (deviceTypeData == null)
            {
                return null;
            }

            return new DeviceIdentifier(parts[1], deviceType, deviceTypeData);
        }

        private const char AddressSeparator = '.';
    }
}