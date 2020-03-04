using HomeSeerAPI;
using Hspi.Camera;
using Hspi.Camera.Onvif;
using NullGuard;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Hspi.DeviceData.Onvif
{

    /// <summary>
    ///  Base class for Root Devices
    /// </summary>
    /// <seealso cref="Hspi.DeviceDataBase" />
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class RootDeviceData : RootDeviceDataBase
    {
        public RootDeviceData() : base(DeviceType.OnvifRoot, "Root")
        {
        }

        private enum Commands
        {
            Reboot,
        };

        public override string HSDeviceTypeString => Invariant($"{PluginData.PlugInName} Onvif Root Device");

        public override IList<VSVGPairs.VSPair> StatusPairs
        {
            get
            {
                var pairs = new List<VSVGPairs.VSPair>();
                pairs.Add(new VSVGPairs.VSPair(HomeSeerAPI.ePairStatusControl.Control)
                {
                    PairType = VSVGPairs.VSVGPairType.SingleValue,
                    Value = (double)Commands.Reboot,
                    Status = "Reboot",
                    Render = Enums.CAPIControlType.Button,
                });
                return pairs;
            }
        }

        public override Task HandleCommand(IHSApplication HS,
                                           CameraBase baseCamera,
                                           string stringValue,
                                           double value,
                                           ePairControlUse control,
                                           CancellationToken token)
        {
            var camera = (OnvifCamera)baseCamera;
            switch ((Commands)value)
            {
                case Commands.Reboot:
                    return camera.Reboot();
            }

            return Task.CompletedTask;
        }
    }
}