using HomeSeerAPI;
using Hspi.Camera;
using Hspi.Camera.Hikvision.Isapi;
using NullGuard;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hspi.DeviceData.Hikvision.Isapi
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class CameraPropertyDeviceData : DeviceDataBase
    {
        public CameraPropertyDeviceData(CameraProperty property)
            : base(DeviceType.HikvisionISAPICameraProperty, property.Id)
        {
            Property = property;
        }

        public override string DefaultName => Property.Name;
        public override bool IsRootDevice => false;
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
                            Render = Enums.CAPIControlType.Button,
                            ControlUse = ePairControlUse.Not_Specified,
                        });
                    }
                }

                return pairs;
            }
        }

        internal CameraProperty Property { get; }
        public override Task HandleCommand(IHSApplication HS,
            CameraBase camera,
            [AllowNull]string stringValue,
            double value,
            ePairControlUse control,
            CancellationToken token)
        {
            var hikVisionCamera = (HikvisionIdapiCamera)camera;
            return hikVisionCamera.Put(Property, stringValue ?? string.Empty);
        }

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
    }
}