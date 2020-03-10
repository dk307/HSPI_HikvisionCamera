using HomeSeerAPI;
using System.Collections.Generic;
using System.IO;

namespace Hspi.DeviceData
{
    internal abstract class OnOffDeviceData : DeviceDataBase
    {
        public OnOffDeviceData(DeviceType deviceType, string deviceTypeId) : base(deviceType, deviceTypeId)
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
                    Status = OffValueString,
                    Render = Enums.CAPIControlType.Button
                });

                pairs.Add(new VSVGPairs.VSPair(HomeSeerAPI.ePairStatusControl.Status)
                {
                    PairType = VSVGPairs.VSVGPairType.SingleValue,
                    Value = OnValue,
                    ControlUse = ePairControlUse._On,
                    Status = OnValueString,
                    Render = Enums.CAPIControlType.Button
                });
                return pairs;
            }
        }
        public override void Update(IHSApplication HS, string deviceValue)
        {
            if (deviceValue == OffValueString)
            {
                UpdateDeviceData(HS, OffValue);
            }
            else
            {
                UpdateDeviceData(HS, OnValue);
            }
        }

        public const string OffValueString = "Off";
        public const string OnValueString = "On";
        private const int OffValue = 0;
        private const int OnValue = 100;
    }
}