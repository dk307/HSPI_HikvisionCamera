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
            CameraId = RemoveAddressSeperator(cameraId);
            DeviceType = deviceType;
            DeviceSubTypeId = RemoveAddressSeperator(deviceSubTypeId);
        }

        public string Address
        {
            get
            {
                string deviceTypeString = RemoveAddressSeperator(EnumHelper.GetDescription(DeviceType));
                return Invariant($"{CreateDeviceIdSpecficAddress(CameraId)}{AddressSeparator}{deviceTypeString}{AddressSeparator}{DeviceSubTypeId}");
            }
        }

        public string CameraId { get; }

        public string DeviceSubTypeId { get; }

        public DeviceType DeviceType { get; }

        public static string CreateDeviceIdSpecficAddress(string cameraId)
        {
            return Invariant($"{RemoveAddressSeperator(PluginData.PlugInName)}{AddressSeparator}{RemoveAddressSeperator(cameraId)}");
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

        private static string RemoveAddressSeperator(string value)
        {
            return value.Replace(AddressSeparator, '_');
        }

        private const char AddressSeparator = '.';
    }
}