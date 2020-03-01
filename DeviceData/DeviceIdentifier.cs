using Hspi.Utils;
using NullGuard;
using Scheduler.Classes;
using System;
using static System.FormattableString;

namespace Hspi.DeviceData
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class DeviceIdentifier
    {
        public DeviceIdentifier(string cameraId, DeviceType deviceType, string deviceSubTypeId)
        {
            CameraId = cameraId;
            DeviceType = deviceType;
            DeviceSubTypeId = deviceSubTypeId;
        }

        public string Address
        {
            get
            {
                string deviceTypeString = EnumHelper.GetDescription(DeviceType);
                return Invariant($"{CreateDeviceIdSpecficAddress(CameraId)}{AddressSeparator}{deviceTypeString}{AddressSeparator}{DeviceSubTypeId}");
            }
        }

        public string CameraId { get; }

        public DeviceType DeviceType { get; }

        public string DeviceSubTypeId { get; }

        public static string CreateDeviceIdSpecficAddress(string cameraId)
        {
            return Invariant($"{PluginData.PlugInName}{AddressSeparator}{cameraId}");
        }
        public static DeviceIdentifier Identify(DeviceClass hsDevice)
        {
            var childAddress = hsDevice.get_Address(null);

            var parts = childAddress.Split(AddressSeparator);

            if (parts.Length != 4)
            {
                return null;
            }

            DeviceType? deviceType = ParseDeviceType(parts[2]);

            if (deviceType == null)
            {
                return null;
            }

            string deviceTypeData = parts[3];
            if (deviceTypeData == null)
            {
                return null;
            }

            return new DeviceIdentifier(parts[1], deviceType.Value, deviceTypeData);
        }

        private static DeviceType? ParseDeviceType(string part)
        {
            DeviceType? deviceType = null;
            foreach (var value in Enum.GetValues(typeof(DeviceType)))
            {
                if (EnumHelper.GetDescription((DeviceType)value) == part)
                {
                    deviceType = (DeviceType)value;
                    break;
                }
            }

            return deviceType;
        }

        private const char AddressSeparator = '.';
    }
}