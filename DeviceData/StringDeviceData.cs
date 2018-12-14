using HomeSeerAPI;
using Hspi.Camera;
using NullGuard;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Hspi.DeviceData
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class StringCameraDeviceData : DeviceDataBase
    {
        public StringCameraDeviceData(CameraProperty property)
            : base(DeviceType.CameraPropertyString, property.Id)
        {
            Property = property;
        }

        public override bool StatusDevice => false;

        public override IList<VSVGPairs.VSPair> StatusPairs => new List<VSVGPairs.VSPair>();

        internal CameraProperty Property { get; }

        public override string DefaultName => Property.Name;

        public override void Update(IHSApplication HS, [AllowNull]string deviceValue)
        {
            if (lastUpdateString == null || lastUpdateString != deviceValue)
            {
                UpdateDeviceData(HS, deviceValue);
                lastUpdateString = deviceValue;
            }
        }

        public override Task HandleCommand(IHSApplication HS,
                                           HikvisionCamera camera,
                                           CancellationToken token,
                                           [AllowNull]string stringValue,
                                           double value,
                                           ePairControlUse control)
        {
            return camera.Put(Property, stringValue ?? string.Empty);
        }

        private string lastUpdateString = null;
    }
}