using HomeSeerAPI;
using Hspi.Camera;
using NullGuard;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hspi.DeviceData
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class CameraPropertyDeviceData : DeviceDataBase
    {
        public CameraPropertyDeviceData(CameraProperty property)
            : base(DeviceType.CameraProperty, property.Id)
        {
            Property = property;
        }

        public override bool StatusDevice => false;

        public override IList<VSVGPairs.VSPair> StatusPairs
        {
            get
            {
                var pairs = new List<VSVGPairs.VSPair>();

                int i = 0;
                foreach (var value in Property.StringValues)
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        pairs.Add(new VSVGPairs.VSPair(HomeSeerAPI.ePairStatusControl.Both)
                        {
                            PairType = VSVGPairs.VSVGPairType.SingleValue,
                            Value = i++,
                            Status = value,
                            Render = Enums.CAPIControlType.Button
                        });
                    }
                }

                return pairs;
            }
        }

        internal CameraProperty Property { get; }

        public override string DefaultName => Property.Name;

        public override void Update(IHSApplication HS, [AllowNull]string deviceValue)
        {
            if (Property.StringValues.IsEmpty)
            {
                UpdateDeviceData(HS, deviceValue);
            }
            else
            {
                var pairs = HS.DeviceVSP_GetAllStatus(RefId);

                double? doubleValue = null;
                foreach (var value in pairs)
                {
                    if (value.ControlStatus == ePairStatusControl.Control)
                    {
                        continue;
                    }

                    if (string.Compare(value.GetPairString(0, string.Empty, null),
                                        deviceValue, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        if (value.PairType == VSVGPairs.VSVGPairType.SingleValue)
                        {
                            doubleValue = value.Value;
                        }
                        else
                        {
                            doubleValue = value.RangeStart;
                        }
                        break;
                    }
                }

                UpdateDeviceData(HS, deviceValue, doubleValue);
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
    }
}