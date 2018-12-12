using HomeSeerAPI;
using Hspi.Exceptions;
using NullGuard;
using Scheduler.Classes;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hspi.Camera;
using System.Diagnostics;
using static System.FormattableString;

namespace Hspi.DeviceData
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class DeviceRootDeviceManager
    {
        public DeviceRootDeviceManager(CameraSettings cameraSettings,
                                       IHSApplication HS,
                                       CancellationToken cancellationToken)
        {
            this.HS = HS;
            this.cancellationToken = cancellationToken;
            CameraSettings = cameraSettings;
            GetCurrentDevices();
            CreateParentDevice();
        }

        public CameraSettings CameraSettings { get; }

        public void ProcessUpdate(ICameraContruct value)
        {
            var deviceIdentifier = new DeviceIdentifier(CameraSettings.Id, value.DeviceType, value.Id);

            string address = deviceIdentifier.Address;
            if (!devices.ContainsKey(address))
            {
                CreateDevice(deviceIdentifier);
            }

            var childDevice = devices[address];
            childDevice.Update(HS, value.Value);
        }

        internal Task HandleCommand(DeviceIdentifier deviceIdentifier, HikvisionCamera camera, string stringValue, double value, ePairControlUse control)
        {
            if (deviceIdentifier.DeviceId != CameraSettings.Id)
            {
                throw new ArgumentException("Invalid Device Identifier");
            }

            if (!devices.TryGetValue(deviceIdentifier.Address, out var deviceData))
            {
                throw new HspiException(Invariant($"{deviceIdentifier.Address} Not Found."));
            }

            return deviceData.HandleCommand(camera, cancellationToken, stringValue, value, control);
        }

        private void CreateDevice(DeviceIdentifier deviceIdentifier)
        {
            CreateParentDevice();

            string address = deviceIdentifier.Address;
            var childDevice = GetDevice(deviceIdentifier);
            string childDeviceName = Invariant($"{CameraSettings.Name} - {childDevice.DefaultName}");
            var childHSDevice = CreateDevice(parentRefId.Value, childDeviceName, address, childDevice);
            childDevice.RefId = childHSDevice.get_Ref(HS);
            devices[address] = childDevice;
        }

        /// <summary>
        /// Creates the HS device.
        /// </summary>
        /// <param name="optionalParentRefId">The optional parent reference identifier.</param>
        /// <param name="name">The name of device</param>
        /// <param name="deviceAddress">The device address.</param>
        /// <param name="deviceData">The device data.</param>
        /// <returns>
        /// New Device
        /// </returns>
        private DeviceClass CreateDevice(int? optionalParentRefId, string name, string deviceAddress, DeviceDataBase deviceData)
        {
            Trace.TraceInformation(Invariant($"Creating Device with Address:{deviceAddress}"));

            DeviceClass device = null;
            int refId = HS.NewDeviceRef(name);
            if (refId > 0)
            {
                device = (DeviceClass)HS.GetDeviceByRef(refId);
                string address = deviceAddress;
                device.set_Address(HS, address);
                device.set_Device_Type_String(HS, deviceData.HSDeviceTypeString);
                var deviceType = new DeviceTypeInfo_m.DeviceTypeInfo();
                deviceType.Device_API = deviceData.DeviceAPI;
                deviceType.Device_Type = deviceData.HSDeviceType;

                device.set_DeviceType_Set(HS, deviceType);
                device.set_Interface(HS, PluginData.PlugInName);
                device.set_InterfaceInstance(HS, string.Empty);
                device.set_Last_Change(HS, DateTime.Now);
                device.set_Location(HS, PluginData.PlugInName);

                device.MISC_Set(HS, Enums.dvMISC.SHOW_VALUES);
                if (deviceData.StatusDevice)
                {
                    device.MISC_Set(HS, Enums.dvMISC.STATUS_ONLY);
                    device.MISC_Clear(HS, Enums.dvMISC.AUTO_VOICE_COMMAND);
                    device.MISC_Clear(HS, Enums.dvMISC.SET_DOES_NOT_CHANGE_LAST_CHANGE);
                    device.set_Status_Support(HS, true);
                }
                else
                {
                    device.MISC_Set(HS, Enums.dvMISC.SET_DOES_NOT_CHANGE_LAST_CHANGE);
                    device.MISC_Set(HS, Enums.dvMISC.AUTO_VOICE_COMMAND);
                    device.set_Status_Support(HS, false);
                }

                var pairs = deviceData.StatusPairs;
                foreach (var pair in pairs)
                {
                    HS.DeviceVSP_AddPair(refId, pair);
                }

                var gPairs = deviceData.GraphicsPairs;
                foreach (var gpair in gPairs)
                {
                    HS.DeviceVGP_AddPair(refId, gpair);
                }

                DeviceClass parent = null;
                if (optionalParentRefId.HasValue)
                {
                    parent = (DeviceClass)HS.GetDeviceByRef(optionalParentRefId.Value);
                }

                if (parent != null)
                {
                    parent.set_Relationship(HS, Enums.eRelationship.Parent_Root);
                    device.set_Relationship(HS, Enums.eRelationship.Child);
                    device.AssociatedDevice_Add(HS, parent.get_Ref(HS));
                    parent.AssociatedDevice_Add(HS, device.get_Ref(HS));
                }

                deviceData.SetInitialData(HS, refId);
            }

            return device;
        }

        private void CreateParentDevice()
        {
            if (!parentRefId.HasValue)
            {
                string parentAddress = new DeviceIdentifier(CameraSettings.Id, DeviceType.Root, "Root").Address;
                RootDeviceData rootDeviceData = new RootDeviceData();
                string name = Invariant($"{CameraSettings.Name} - {rootDeviceData.DefaultName}");
                var parentHSDevice = CreateDevice(null, name, parentAddress, rootDeviceData);
                parentHSDevice.MISC_Set(HS, Enums.dvMISC.CONTROL_POPUP);
                parentRefId = parentHSDevice.get_Ref(HS);
                rootDeviceData.RefId = parentRefId.Value;
                devices[parentAddress] = rootDeviceData;
            }
        }

        private void GetCurrentDevices()
        {
            var deviceEnumerator = HS.GetDeviceEnumerator() as clsDeviceEnumeration;

            if (deviceEnumerator == null)
            {
                throw new HspiException(Invariant($"{PluginData.PlugInName} failed to get a device enumerator from HomeSeer."));
            }

            string baseAddress = DeviceIdentifier.CreateDeviceIdSpecficAddress(CameraSettings.Id);
            do
            {
                DeviceClass device = deviceEnumerator.GetNext();
                if ((device != null) &&
                    (device.get_Interface(HS) != null) &&
                    (device.get_Interface(HS).Trim() == PluginData.PlugInName))
                {
                    string address = device.get_Address(HS);
                    if (address.StartsWith(baseAddress, StringComparison.Ordinal))
                    {
                        var deviceData = GetDeviceData(device);
                        if (deviceData != null)
                        {
                            devices.Add(address, deviceData);
                        }

                        var rootDevice = (deviceData as RootDeviceData);
                        if (rootDevice != null)
                        {
                            parentRefId = device.get_Ref(HS);
                        }
                    }
                }
            } while (!deviceEnumerator.Finished);
        }

        private DeviceDataBase GetDevice(DeviceIdentifier deviceIdentifier)
        {
            switch (deviceIdentifier.DeviceType)
            {
                case DeviceType.Root:
                    return new RootDeviceData();

                case DeviceType.CameraPropertyString:
                    if (CameraSettings.PeriodicFetchedCameraProperties.TryGetValue(deviceIdentifier.DeviceTypeId, out var cameraProperty))
                    {
                        return new StringCameraDeviceData(cameraProperty);
                    }
                    return null;

                case DeviceType.CameraPropertyNumber:
                    if (CameraSettings.PeriodicFetchedCameraProperties.TryGetValue(deviceIdentifier.DeviceTypeId, out var cameraProperty2))
                    {
                        return new NumberCameraDeviceData(cameraProperty2);
                    }
                    return null;

                case DeviceType.Alarm:
                    return new AlarmDeviceData(deviceIdentifier.DeviceTypeId);

                default:
                    throw new NotImplementedException();
            }
        }

        private DeviceDataBase GetDeviceData(DeviceClass hsDevice)
        {
            var id = DeviceIdentifier.Identify(hsDevice);
            if (id == null)
            {
                return null;
            }

            var device = GetDevice(id);
            if (device != null)
            {
                device.RefId = hsDevice.get_Ref(HS);
            }
            return device;
        }

        private readonly CancellationToken cancellationToken;
        private readonly Dictionary<string, DeviceDataBase> devices = new Dictionary<string, DeviceDataBase>();
        private readonly IHSApplication HS;
        private int? parentRefId = null;
    };
}