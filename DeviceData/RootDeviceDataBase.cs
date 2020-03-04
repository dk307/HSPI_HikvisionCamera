using HomeSeerAPI;
using Hspi.Camera;
using NullGuard;
using System.Collections.Generic;

namespace Hspi.DeviceData
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal abstract class RootDeviceDataBase : DeviceDataBase
    {
        public RootDeviceDataBase(DeviceType deviceType, string deviceTypeId)
            : base(deviceType, deviceTypeId)
        {
        }

        public override string DefaultName => "Root";
        public override IList<VSVGPairs.VGPair> GraphicsPairs => new List<VSVGPairs.VGPair>();
        public override int HSDeviceType => (int)DeviceTypeInfo_m.DeviceTypeInfo.eDeviceType_Plugin.Root;
        public override bool IsRootDevice => true;
        public override bool StatusDevice => false;

        public override void OnPlugInLoad(IHSApplication HS, ICameraSettings cameraSettings)
        {
        }

        public override void SetOnDeviceCreateData(IHSApplication HS, ICameraSettings cameraSettings, int refID)
        {
            HS.set_DeviceInvalidValue(refID, false);
            HS.SetDeviceString(refID, "Root", false);
        }

        public override void Update(IHSApplication HS, string deviceValue) => throw new System.NotImplementedException();
    }
}