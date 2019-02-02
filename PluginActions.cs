using HomeSeerAPI;
using Hspi.Camera;
using Hspi.Pages;
using Hspi.Utils;
using NullGuard;
using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Hspi
{
    internal partial class Plugin : HspiBase
    {
        private const int ActionTakeSnapshotsTANumber = 1;

        public override string ActionBuildUI([AllowNull]string uniqueControlId, IPlugInAPI.strTrigActInfo actionInfo)
        {
            try
            {
                switch (actionInfo.TANumber)
                {
                    case ActionTakeSnapshotsTANumber:
                        using (var actionPage = new ActionPage(HS, pluginConfig))
                        {
                            return actionPage.GetRefreshActionUI(uniqueControlId ?? string.Empty, actionInfo);
                        }

                    default:
                        return base.ActionBuildUI(uniqueControlId, actionInfo);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(Invariant($"Failed to give build Action UI with {ex.GetFullMessage()}"));
                throw;
            }
        }

        public override bool ActionConfigured(IPlugInAPI.strTrigActInfo actionInfo)
        {
            try
            {
                switch (actionInfo.TANumber)
                {
                    case ActionTakeSnapshotsTANumber:
                        if (actionInfo.DataIn != null)
                        {
                            var action = ObjectSerialize.DeSerializeFromBytes(actionInfo.DataIn) as TakeSnapshotAction;
                            return action != null && action.IsValid();
                        }

                        return false;

                    default:
                        return base.ActionConfigured(actionInfo);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(Invariant($"Failed to ActionConfigured with {ex.GetFullMessage()}"));
                return false;
            }
        }

        public override int ActionCount()
        {
            return 1;
        }

        public override string ActionFormatUI(IPlugInAPI.strTrigActInfo actionInfo)
        {
            try
            {
                switch (actionInfo.TANumber)
                {
                    case ActionTakeSnapshotsTANumber:
                        if (actionInfo.DataIn != null)
                        {
                            var action = ObjectSerialize.DeSerializeFromBytes(actionInfo.DataIn) as TakeSnapshotAction;
                            if (action != null)
                            {
                                StringBuilder stringBuilder = new StringBuilder();

                                stringBuilder.Append(@"Take snapshots for ");
                                stringBuilder.Append(action.TimeSpan);
                                stringBuilder.Append(" at interval of ");
                                stringBuilder.Append(action.Interval);
                                stringBuilder.Append(" on ");

                                if ((action != null) && pluginConfig.Cameras.TryGetValue(action.Id, out var device))
                                {
                                    stringBuilder.Append(device.Name);
                                }
                                else
                                {
                                    stringBuilder.Append(@"Unknown");
                                }

                                return stringBuilder.ToString();
                            }
                        }
                        return Invariant($"{PluginData.PlugInName} Unknown action");

                    default:
                        return base.ActionFormatUI(actionInfo);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(Invariant($"Failed to ActionFormatUI with {ex.GetFullMessage()}"));
                throw;
            }
        }

        public override IPlugInAPI.strMultiReturn ActionProcessPostUI([AllowNull] NameValueCollection postData, IPlugInAPI.strTrigActInfo actionInfo)
        {
            try
            {
                switch (actionInfo.TANumber)
                {
                    case ActionTakeSnapshotsTANumber:
                        using (var actionPage = new ActionPage(HS, pluginConfig))
                        {
                            return actionPage.GetRefreshActionPostUI(postData, actionInfo);
                        }

                    default:
                        return base.ActionProcessPostUI(postData, actionInfo);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(Invariant($"Failed to ActionProcessPostUI with {ex.GetFullMessage()}"));
                throw;
            }
        }

        public override bool ActionReferencesDevice(IPlugInAPI.strTrigActInfo actionInfo, int deviceId)
        {
            try
            {
                switch (actionInfo.TANumber)
                {
                    case ActionTakeSnapshotsTANumber:
                        if (actionInfo.DataIn != null)
                        {
                            var action = ObjectSerialize.DeSerializeFromBytes(actionInfo.DataIn) as TakeSnapshotAction;
                            if ((action != null))
                            {
                                CameraManager cameraManager = GetCameraManager(action.Id);
                                if (cameraManager != null)
                                {
                                    return cameraManager.HasDevice(deviceId);
                                }
                            }
                        }
                        return false;

                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(Invariant($"Failed to give Action Name with {ex.GetFullMessage()}"));
                throw;
            }
        }

        public override string get_ActionName(int actionNumber)
        {
            try
            {
                switch (actionNumber)
                {
                    case ActionTakeSnapshotsTANumber:
                        return @"Hikvision Take Snapshots On";

                    default:
                        return base.get_ActionName(actionNumber);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(Invariant($"Failed to give Action Name with {ex.GetFullMessage()}"));
                throw;
            }
        }

        public override bool HandleAction(IPlugInAPI.strTrigActInfo actionInfo)
        {
            try
            {
                switch (actionInfo.TANumber)
                {
                    case ActionTakeSnapshotsTANumber:
                        if (actionInfo.DataIn != null)
                        {
                            var action = ObjectSerialize.DeSerializeFromBytes(actionInfo.DataIn) as TakeSnapshotAction;
                            if ((action != null) && (action.IsValid()))
                            {
                                CameraManager cameraManager = GetCameraManager(action.Id);
                                if (cameraManager != null)
                                {
                                    Task.Run(() => TakeSnapshots(action.TimeSpan, action.Interval, cameraManager));
                                    return true;
                                }
                            }
                        }
                        Trace.TraceWarning(Invariant($"Failed to execute action with invalid action"));
                        return false;

                    default:
                        return base.HandleAction(actionInfo);
                }
            }
            catch (TaskCanceledException ex)
            {
                Trace.TraceWarning(Invariant($"Failed to execute action with: {ex.GetFullMessage()}"));
                return false;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning(Invariant($"Failed to execute action with {ex.GetFullMessage()}"));
                return false;
            }
        }

        private static async Task TakeSnapshots(TimeSpan timeSpan, TimeSpan interval, CameraManager cameraManager)
        {
            await cameraManager.DownloadContinuousSnapshots(timeSpan, interval,
                                                       HikvisionCamera.Track1).ConfigureAwait(false);
        }

        private CameraManager GetCameraManager(string camerId)
        {
            CameraManager cameraManager = null;
            lock (connectorManagerLock)
            {
                connectorManager.TryGetValue(camerId, out cameraManager);
            }

            return cameraManager;
        }
    }
}