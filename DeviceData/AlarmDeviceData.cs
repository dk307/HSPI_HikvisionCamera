using HomeSeerAPI;
using Hspi.Camera;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Hspi.DeviceData
{
    internal class AlarmDeviceData : DeviceDataBase
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
                    Set_Value = OnValue
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
                    Status = "Off",
                    Render = Enums.CAPIControlType.Button
                });

                pairs.Add(new VSVGPairs.VSPair(HomeSeerAPI.ePairStatusControl.Status)
                {
                    PairType = VSVGPairs.VSVGPairType.SingleValue,
                    Value = OnValue,
                    ControlUse = ePairControlUse._On,
                    Status = "On",
                    Render = Enums.CAPIControlType.Button
                });
                return pairs;
            }
        }

        public override Task HandleCommand(HikvisionCamera camera, CancellationToken token, string stringValue, double value, ePairControlUse control)
        {
            throw new System.NotImplementedException();
        }

        public override void Update(IHSApplication HS, string deviceValue)
        {
            if (lastUpdateString == null || lastUpdateString != deviceValue)
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
        }

        private const int OffValue = 0;
        private const int OnValue = 100;
        private string lastUpdateString = null;
    }
}