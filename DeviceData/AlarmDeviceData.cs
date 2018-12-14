using HomeSeerAPI;
using Hspi.Camera;
using System.Collections.Generic;
using System.IO;

namespace Hspi.DeviceData
{
    internal sealed class AlarmDeviceData : DeviceDataBase
    {
        public AlarmDeviceData(string alarmType) : base(DeviceType.Alarm, alarmType)
        {
        }

        public override string DefaultName => DeviceTypeId;

        public override IList<VSVGPairs.VGPair> GraphicsPairs
        {
            get
            {
                var pairs = new List<VSVGPairs.VGPair>();
                pairs.Add(new VSVGPairs.VGPair()
                {
                    PairType = VSVGPairs.VSVGPairType.SingleValue,
                    Graphic = Path.Combine(PluginData.HSImagesPathRoot, "on.gif"),
                    Set_Value = OnValue,
                });

                pairs.Add(new VSVGPairs.VGPair()
                {
                    PairType = VSVGPairs.VSVGPairType.SingleValue,
                    Graphic = Path.Combine(PluginData.HSImagesPathRoot, "off.gif"),
                    Set_Value = OffValue
                });

                return pairs;
            }
        }

        public override bool StatusDevice => true;

        public override IList<VSVGPairs.VSPair> StatusPairs
        {
            get
            {
                var pairs = new List<VSVGPairs.VSPair>();
                pairs.Add(new VSVGPairs.VSPair(HomeSeerAPI.ePairStatusControl.Status)
                {
                    PairType = VSVGPairs.VSVGPairType.SingleValue,
                    Value = OffValue,
                    ControlUse = ePairControlUse._Off,
                    Status = AlarmInfo.AlarmOffValue,
                    Render = Enums.CAPIControlType.Button
                });

                pairs.Add(new VSVGPairs.VSPair(HomeSeerAPI.ePairStatusControl.Status)
                {
                    PairType = VSVGPairs.VSVGPairType.SingleValue,
                    Value = OnValue,
                    ControlUse = ePairControlUse._On,
                    Status = AlarmInfo.AlarmOnValue,
                    Render = Enums.CAPIControlType.Button
                });
                return pairs;
            }
        }

        public override void Update(IHSApplication HS, string deviceValue)
        {
            if (deviceValue == AlarmInfo.AlarmOffValue)
            {
                UpdateDeviceData(HS, OffValue);
            }
            else
            {
                UpdateDeviceData(HS, OnValue);
            }
        }

        private const int OffValue = 0;
        private const int OnValue = 100;
    }
}