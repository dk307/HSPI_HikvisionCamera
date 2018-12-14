using HomeSeerAPI;
using Hspi.Camera;
using NullGuard;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Hspi.DeviceData
{
    /// <summary>
    /// This is base class for creating and updating devices in HomeSeer.
    /// </summary>
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal abstract class DeviceDataBase
    {
        protected DeviceDataBase(DeviceType deviceType, string deviceTypeId)
        {
            DeviceType = deviceType;
            DeviceTypeId = deviceTypeId;
        }

        public abstract string DefaultName { get; }
        public virtual DeviceTypeInfo_m.DeviceTypeInfo.eDeviceAPI DeviceAPI => DeviceTypeInfo_m.DeviceTypeInfo.eDeviceAPI.Plug_In;

        public DeviceType DeviceType { get; }

        public string DeviceTypeId { get; }

        public virtual IList<VSVGPairs.VGPair> GraphicsPairs => new List<VSVGPairs.VGPair>();

        public virtual int HSDeviceType => 0;

        public virtual string HSDeviceTypeString => Invariant($"{PluginData.PlugInName} {DeviceType} Device");

        public int RefId { get; set; } = 0;

        public abstract bool StatusDevice { get; }

        /// <summary>
        /// Gets the status pairs for creating device.
        /// </summary>
        /// <param name="config">The plugin configuration.</param>
        /// <returns></returns>
        public abstract IList<VSVGPairs.VSPair> StatusPairs { get; }

        public virtual Task HandleCommand(IHSApplication HS,
                                          HikvisionCamera camera,
                                          CancellationToken token,
                                          string stringValue,
                                          double value,
                                          ePairControlUse control)
        {
            return Task.FromResult(true);
        }

        public virtual void SetInitialData(IHSApplication HS, CameraSettings cameraSettings, int refId)
        {
            HS.SetDeviceValueByRef(refId, 0D, false);
            HS.set_DeviceInvalidValue(refId, true);
        }

        public abstract void Update(IHSApplication HS, string deviceValue);

        protected void UpdateDeviceData(IHSApplication HS, double data)
        {
            HS.set_DeviceInvalidValue(RefId, false);
            HS.SetDeviceValueByRef(RefId, data, true);
        }

        protected void UpdateDeviceData(IHSApplication HS, [AllowNull]string data)
        {
            if (data == null)
            {
                HS.set_DeviceInvalidValue(RefId, true);
            }
            else
            {
                HS.set_DeviceInvalidValue(RefId, false);
                HS.SetDeviceString(RefId, data, true);
            }
        }
    };
}