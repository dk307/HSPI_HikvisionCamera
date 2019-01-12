using HomeSeerAPI;
using Hspi.Camera;
using Hspi.DeviceData;
using Hspi.Exceptions;
using Hspi.Pages;
using Hspi.Utils;
using NullGuard;
using Scheduler.Classes;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Hspi
{
    /// <summary>
    /// Plugin class for Ubiquiti mPower
    /// </summary>
    /// <seealso cref="Hspi.HspiBase" />
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class Plugin : HspiBase
    {
        public Plugin()
            : base(PluginData.PlugInName, supportConfigDevice: true)
        {
        }

        public override string InitIO(string port)
        {
            string result = string.Empty;
            try
            {
                pluginConfig = new PluginConfig(HS);
                configPage = new ConfigPage(HS, pluginConfig);
                LogInfo("Starting Plugin");
#if DEBUG
                pluginConfig.DebugLogging = true;
#endif
                pluginConfig.ConfigChanged += PluginConfig_ConfigChanged;

                RegisterConfigPage();

                RestartCameraOperations();

                LogInfo("Plugin Started");
            }
            catch (Exception ex)
            {
                result = Invariant($"Failed to initialize PlugIn With {ExceptionHelper.GetFullMessage(ex)}");
                LogError(result);
            }

            return result;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        private void RestartCameraOperations()
        {
            lock (connectorManagerLock)
            {
                // This returns a new copy every time
                var currentDevices = pluginConfig.Cameras;

                // Update changed or new
                foreach (var device in pluginConfig.Cameras)
                {
                    if (connectorManager.TryGetValue(device.Key, out var oldConnector))
                    {
                        if (!device.Value.Equals(oldConnector.CameraSettings))
                        {
                            oldConnector.Dispose();
                            connectorManager[device.Key] = new CameraManager(HS, device.Value, ShutdownCancellationToken);
                        }
                    }
                    else
                    {
                        connectorManager.Add(device.Key, new CameraManager(HS, device.Value, ShutdownCancellationToken));
                    }
                }

                // Remove deleted
                List<string> removalList = new List<string>();
                foreach (var deviceKeyPair in connectorManager)
                {
                    if (!currentDevices.ContainsKey(deviceKeyPair.Key))
                    {
                        deviceKeyPair.Value.Dispose();
                        removalList.Add(deviceKeyPair.Key);
                    }
                }

                foreach (var key in removalList)
                {
                    connectorManager.Remove(key);
                }
            }
        }

        private void PluginConfig_ConfigChanged(object sender, EventArgs e)
        {
            RestartCameraOperations();
        }

        public override void LogDebug(string message)
        {
            if ((pluginConfig != null) && pluginConfig.DebugLogging)
            {
                base.LogDebug(message);
            }
        }

        public override string GetPagePlugin(string page, [AllowNull]string user, int userRights, [AllowNull]string queryString)
        {
            if (page == ConfigPage.Name)
            {
                return configPage.GetWebPage(queryString);
            }

            return string.Empty;
        }

        public override string PostBackProc(string page, string data, [AllowNull]string user, int userRights)
        {
            if (page == ConfigPage.Name)
            {
                return configPage.PostBackProc(data, user, userRights);
            }

            return string.Empty;
        }

        public override void SetIOMulti(List<CAPI.CAPIControl> colSend)
        {
            foreach (var control in colSend)
            {
                try
                {
                    int refId = control.Ref;
                    DeviceClass deviceClass = (DeviceClass)HS.GetDeviceByRef(refId);

                    var deviceIdentifier = DeviceIdentifier.Identify(deviceClass);

                    lock (connectorManagerLock)
                    {
                        if (connectorManager.TryGetValue(deviceIdentifier.DeviceId, out var connector))
                        {
                            connector.HandleCommand(deviceIdentifier,
                                                    control.Label,
                                                    control.ControlValue,
                                                    control.ControlUse).Wait();
                        }
                        else
                        {
                            throw new HspiException(Invariant($"{refId} device not found for processing."));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceError(Invariant($"Failed With {ExceptionHelper.GetFullMessage(ex)}"));
                }
            }
        }

        public override string ConfigDevice(int deviceId, [AllowNull] string user, int userRights, bool newDevice)
        {
            if (newDevice)
            {
                return string.Empty;
            }

            try
            {
                var deviceClass = (DeviceClass)HS.GetDeviceByRef(deviceId);
                var deviceIdentifier = DeviceIdentifier.Identify(deviceClass);

                if (deviceIdentifier != null)
                {
                    foreach (var camera in pluginConfig.Cameras)
                    {
                        if (camera.Key == deviceIdentifier.DeviceId)
                        {
                            StringBuilder stb = new StringBuilder();

                            stb.Append(@"<table style='width:100%;border-spacing:0px;'");
                            stb.Append("<tr height='5'><td style='width:25%'></td><td style='width:20%'></td><td style='width:55%'></td></tr>");
                            stb.Append($"<tr><td class='tablecell'>Name:</td><td class='tablecell' colspan=2>");
                            stb.Append(PageHelper.HtmlEncode(camera.Value.Name));
                            stb.Append("</td></tr>");
                            stb.Append($"<tr><td class='tablecell'>Uri:</td><td class='tablecell' colspan=2>");
                            stb.Append(Invariant($"<a href=\"{PageHelper.HtmlEncode(camera.Value.CameraHost)}\" target=\"_blank\">{PageHelper.HtmlEncode(camera.Value.CameraHost)}</a>"));
                            stb.Append("</td></tr>");
                            stb.Append($"<tr><td class='tablecell'>Type:</td><td class='tablecell' colspan=2>");
                            stb.Append(PageHelper.HtmlEncode(deviceIdentifier.DeviceType));
                            stb.Append("</td></tr>");
                            stb.Append(Invariant($"</td><td></td></tr>"));
                            stb.Append("<tr height='5'><td colspan=3></td></tr>");
                            stb.Append(@" </table>");

                            return stb.ToString();
                        }
                    }
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                LogError(Invariant($"ConfigDevice for {deviceId} With {ex.Message}"));
                return string.Empty;
            }
        }

        private void RegisterConfigPage()
        {
            string link = ConfigPage.Name;
            HS.RegisterPage(link, Name, string.Empty);

            HomeSeerAPI.WebPageDesc wpd = new HomeSeerAPI.WebPageDesc()
            {
                plugInName = Name,
                link = link,
                linktext = "Configuration",
                page_title = Invariant($"{PluginData.PlugInName} Configuration"),
            };
            Callback.RegisterConfigLink(wpd);
            Callback.RegisterLink(wpd);
        }

        #region "Action Override"

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

        #endregion "Action Override"

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "fileDeleter")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "configPage")]
        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (pluginConfig != null)
                {
                    pluginConfig.ConfigChanged -= PluginConfig_ConfigChanged;
                }

                configPage?.Dispose();

                lock (connectorManagerLock)
                {
                    foreach (var deviceKeyPair in connectorManager)
                    {
                        deviceKeyPair.Value.Dispose();
                    }

                    connectorManager.Clear();
                }

                disposedValue = true;
            }

            base.Dispose(disposing);
        }

        private readonly object connectorManagerLock = new object();
        private readonly Dictionary<string, CameraManager> connectorManager = new Dictionary<string, CameraManager>();
        private ConfigPage configPage;
        private PluginConfig pluginConfig;
        private bool disposedValue = false;
    }
}