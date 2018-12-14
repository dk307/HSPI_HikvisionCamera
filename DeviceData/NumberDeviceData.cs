using HomeSeerAPI;
using Hspi.Camera;
using NullGuard;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Hspi.DeviceData
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class NumberCameraDeviceData : DeviceDataBase
    {
        public NumberCameraDeviceData(CameraProperty cameraProperty)
            : base(DeviceType.CameraPropertyNumber, cameraProperty.Id)
        {
            Property = cameraProperty;
        }

        public override string DefaultName => Property.Name;
        public CameraProperty Property { get; }
        public override bool StatusDevice => false;

        public override IList<VSVGPairs.VSPair> StatusPairs
        {
            get
            {
                var pairs = new List<VSVGPairs.VSPair>();
                pairs.Add(new VSVGPairs.VSPair(ePairStatusControl.Status)
                {
                    PairType = VSVGPairs.VSVGPairType.Range,
                    RangeStart = int.MinValue,
                    RangeEnd = int.MaxValue,
                    IncludeValues = true,
                    RangeStatusDecimals = 3,
                });
                return pairs;
            }
        }

        public override Task HandleCommand(IHSApplication HS,
                                           HikvisionCamera camera,
                                           CancellationToken token,
                                           [AllowNull]string stringValue,
                                           double value,
                                           ePairControlUse control)
        {
            return camera.Put(Property, value.ToString(CultureInfo.InvariantCulture));
        }

        public override void Update(IHSApplication HS, string deviceValue)
        {
            if (deviceValue != null &&
                double.TryParse(deviceValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
                if (!lastValue.HasValue || lastValue.Value != value)
                {
                    UpdateDeviceData(HS, value);
                    lastValue = value;
                }
            }
            else
            {
                UpdateDeviceData(HS, deviceValue);
                HS.set_DeviceInvalidValue(RefId, true);
            }
        }

        private double? lastValue = null;
    }
}