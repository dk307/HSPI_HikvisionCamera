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
    ///  Base class for Root Devices
    /// </summary>
    /// <seealso cref="Hspi.DeviceDataBase" />
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class RootDeviceData : DeviceDataBase
    {
        public RootDeviceData() : base(DeviceType.Root, "Root")
        {
        }

        private enum Commands
        {
            DownloadVideos,
            Reboot,
            RequestKeyFrameTrack1,
            RequestKeyFrameTrack2,
            StartRecordingTrack1,
            StartRecordingTrack2,
            StopRecordingTrack1,
            StopRecordingTrack2,
            TakeSnapshotTrack1,
            TakeSnapshotTrack2,
            Poll,
        };

        public override string DefaultName => "Root";
        public override IList<VSVGPairs.VGPair> GraphicsPairs => new List<VSVGPairs.VGPair>();
        public override int HSDeviceType => (int)DeviceTypeInfo_m.DeviceTypeInfo.eDeviceType_Plugin.Root;
        public override string HSDeviceTypeString => Invariant($"{PluginData.PlugInName} Root Device");
        public override bool StatusDevice => false;

        public override IList<VSVGPairs.VSPair> StatusPairs
        {
            get
            {
                var pairs = new List<VSVGPairs.VSPair>();
                pairs.Add(new VSVGPairs.VSPair(HomeSeerAPI.ePairStatusControl.Control)
                {
                    PairType = VSVGPairs.VSVGPairType.SingleValue,
                    Value = (double)Commands.DownloadVideos,
                    Status = "Download Videos",
                    Render = Enums.CAPIControlType.Button,
                });
                pairs.Add(new VSVGPairs.VSPair(HomeSeerAPI.ePairStatusControl.Control)
                {
                    PairType = VSVGPairs.VSVGPairType.SingleValue,
                    Value = (double)Commands.Reboot,
                    Status = "Reboot",
                    Render = Enums.CAPIControlType.Button,
                });
                pairs.Add(new VSVGPairs.VSPair(HomeSeerAPI.ePairStatusControl.Control)
                {
                    PairType = VSVGPairs.VSVGPairType.SingleValue,
                    Value = (double)Commands.RequestKeyFrameTrack1,
                    Status = "Request Key Frame Track1",
                    Render = Enums.CAPIControlType.Button,
                });
                pairs.Add(new VSVGPairs.VSPair(HomeSeerAPI.ePairStatusControl.Control)
                {
                    PairType = VSVGPairs.VSVGPairType.SingleValue,
                    Value = (double)Commands.RequestKeyFrameTrack2,
                    Status = "Request Key Frame Track2",
                    Render = Enums.CAPIControlType.Button,
                });
                pairs.Add(new VSVGPairs.VSPair(HomeSeerAPI.ePairStatusControl.Control)
                {
                    PairType = VSVGPairs.VSVGPairType.SingleValue,
                    Value = (double)Commands.StartRecordingTrack1,
                    Status = "Start Recording Track1",
                    Render = Enums.CAPIControlType.Button,
                });
                pairs.Add(new VSVGPairs.VSPair(HomeSeerAPI.ePairStatusControl.Control)
                {
                    PairType = VSVGPairs.VSVGPairType.SingleValue,
                    Value = (double)Commands.StartRecordingTrack2,
                    Status = "Start Recording Track2",
                    Render = Enums.CAPIControlType.Button,
                });
                pairs.Add(new VSVGPairs.VSPair(HomeSeerAPI.ePairStatusControl.Control)
                {
                    PairType = VSVGPairs.VSVGPairType.SingleValue,
                    Value = (double)Commands.StopRecordingTrack1,
                    Status = "Stop Recording Track1",
                    Render = Enums.CAPIControlType.Button,
                });
                pairs.Add(new VSVGPairs.VSPair(HomeSeerAPI.ePairStatusControl.Control)
                {
                    PairType = VSVGPairs.VSVGPairType.SingleValue,
                    Value = (double)Commands.StopRecordingTrack2,
                    Status = "Stop Recording Track2",
                    Render = Enums.CAPIControlType.Button,
                });
                pairs.Add(new VSVGPairs.VSPair(HomeSeerAPI.ePairStatusControl.Control)
                {
                    PairType = VSVGPairs.VSVGPairType.SingleValue,
                    Value = (double)Commands.TakeSnapshotTrack1,
                    Status = "Take Snapshot Track1",
                    Render = Enums.CAPIControlType.Button,
                });
                pairs.Add(new VSVGPairs.VSPair(HomeSeerAPI.ePairStatusControl.Control)
                {
                    PairType = VSVGPairs.VSVGPairType.SingleValue,
                    Value = (double)Commands.TakeSnapshotTrack2,
                    Status = "Take Snapshot Track2",
                    Render = Enums.CAPIControlType.Button,
                });
                pairs.Add(new VSVGPairs.VSPair(HomeSeerAPI.ePairStatusControl.Control)
                {
                    PairType = VSVGPairs.VSVGPairType.SingleValue,
                    Value = (double)Commands.Poll,
                    Status = "Poll",
                    Render = Enums.CAPIControlType.Button,
                });
                return pairs;
            }
        }

        public override Task HandleCommand(IHSApplication HS,
                                           HikvisionCamera camera,
                                           CancellationToken token,
                                           string stringValue,
                                           double value,
                                           ePairControlUse control)
        {
            switch ((Commands)value)
            {
                case Commands.DownloadVideos:
                    camera.StartVideoDownload();
                    break;

                case Commands.Reboot:
                    return camera.Reboot();

                case Commands.RequestKeyFrameTrack1:
                    return camera.RequestKeyFrame(HikvisionCamera.Track1);

                case Commands.RequestKeyFrameTrack2:
                    return camera.RequestKeyFrame(HikvisionCamera.Track2);

                case Commands.StartRecordingTrack1:
                    return camera.StartRecording(HikvisionCamera.Track1);

                case Commands.StartRecordingTrack2:
                    return camera.StartRecording(HikvisionCamera.Track2);

                case Commands.StopRecordingTrack1:
                    return camera.StopRecording(HikvisionCamera.Track1);

                case Commands.StopRecordingTrack2:
                    return camera.StopRecording(HikvisionCamera.Track2);

                case Commands.TakeSnapshotTrack1:
                    return TakeSnapshot(HS, camera, HikvisionCamera.Track1);

                case Commands.TakeSnapshotTrack2:
                    return TakeSnapshot(HS, camera, HikvisionCamera.Track2);

                case Commands.Poll:
                    return camera.RefreshProperties();
            }

            return Task.FromResult(true);
        }

        public override void OnPlugInLoad(IHSApplication HS, CameraSettings cameraSettings)
        {
            for (int i = 1; i <= TotalGlobalVars; i++)
            {
                HS.CreateVar(GetGlobalVarName(cameraSettings, i));
            }
        }

        public override void SetOnDeviceCreateData(IHSApplication HS, CameraSettings cameraSettings, int refID)
        {
            HS.set_DeviceInvalidValue(refID, false);
            HS.SetDeviceString(refID, "Root", false);
        }

        public override void Update(IHSApplication HS, string deviceValue) => throw new System.NotImplementedException();

        private static string GetGlobalVarName(CameraSettings camera, int pos)
        {
            return Invariant($"{camera.Name.Replace(' ', '_')}_snapshot{pos}");
        }

        private void SetGlobalVar(IHSApplication HS, HikvisionCamera camera, string path)
        {
            HS.SaveVar(GetGlobalVarName(camera.CameraSettings, lastGlobalVar++), path);

            if (lastGlobalVar > TotalGlobalVars)
            {
                lastGlobalVar = 1;
            }
        }

        private async Task TakeSnapshot(IHSApplication HS, HikvisionCamera camera, int track)
        {
            string path = await camera.DownloadSnapshot(track).ConfigureAwait(false);
            SetGlobalVar(HS, camera, path);
        }

        private const int TotalGlobalVars = 3;
        private int lastGlobalVar = 1;
    }
}