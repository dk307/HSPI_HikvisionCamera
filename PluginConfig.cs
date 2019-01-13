using HomeSeerAPI;
using Hspi.Camera;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using static System.FormattableString;
using NullGuard;
using System.Collections.Immutable;
using Hspi.Utils;

namespace Hspi
{
    /// <summary>
    /// Class to store PlugIn Configuration
    /// </summary>
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class PluginConfig
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PluginConfig"/> class.
        /// </summary>
        /// <param name="HS">The homeseer application.</param>
        public PluginConfig(IHSApplication HS)
        {
            this.HS = HS;

            ffmpegPath = GetValue(nameof(ffmpegPath), string.Empty);
            debugLogging = GetValue(nameof(debugLogging), false);

            LoadCameraProperties();
            LoadCameras();
        }

        public event EventHandler<EventArgs> ConfigChanged;

        public ImmutableDictionary<string, CameraProperty> CameraProperties
        {
            get
            {
                using (var scopedLock = configLock.ReaderLock())
                {
                    return cameraProperties.ToImmutableDictionary();
                }
            }
        }

        public ImmutableDictionary<string, CameraSettings> Cameras
        {
            get
            {
                using (var scopedLock = configLock.ReaderLock())
                {
                    return devices.ToImmutableDictionary();
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether debug logging is enabled.
        /// </summary>
        /// <value>
        ///   <c>true</c> if [debug logging]; otherwise, <c>false</c>.
        /// </value>
        public bool DebugLogging
        {
            get
            {
                using (var scopedLock = configLock.ReaderLock())
                {
                    return debugLogging;
                }
            }

            set
            {
                using (var scopedLock = configLock.WriterLock())
                {
                    SetValue(DebugLoggingKey, value);
                    debugLogging = value;
                }
            }
        }

        public string FfmpegPath
        {
            get
            {
                using (var scopedLock = configLock.ReaderLock())
                {
                    return ffmpegPath;
                }
            }
            set
            {
                using (var scopedLock = configLock.WriterLock())
                {
                    SetValue(nameof(ffmpegPath), value);
                    ffmpegPath = value;
                }
            }
        }

        public void AddCamera(CameraSettings device)
        {
            using (var scopedLock = configLock.WriterLock())
            {
                devices[device.Id] = device;

                SetValue(nameof(device.Name), device.Name, device.Id);
                SetValue(nameof(device.CameraHost), device.CameraHost, device.Id);
                SetValue(nameof(device.Login), device.Login, device.Id);
                SetValue(nameof(device.Password), HS.EncryptString(device.Password, nameof(device.Password)), device.Id);
                SetValue(nameof(device.AlarmCancelInterval), (long)device.AlarmCancelInterval.TotalSeconds, device.Id);
                SetValue(nameof(device.CameraPropertiesRefreshInterval), (long)device.CameraPropertiesRefreshInterval.TotalSeconds, device.Id);
                SetValue(nameof(device.SnapshotDownloadDirectory), device.SnapshotDownloadDirectory, device.Id);
                SetValue(nameof(device.VideoDownloadDirectory), device.VideoDownloadDirectory, device.Id);

                var ffMpegRecordSettings = device.FfMpegRecordSettings;
                var ffMpegRecordSettingsId = device.Id + nameof(device.FfMpegRecordSettings);
                SetValue(nameof(ffMpegRecordSettings.FileEncodeOptions), ffMpegRecordSettings.FileEncodeOptions, ffMpegRecordSettingsId);
                SetValue(nameof(ffMpegRecordSettings.FileNameExtension), ffMpegRecordSettings.FileNameExtension, ffMpegRecordSettingsId);
                SetValue(nameof(ffMpegRecordSettings.FileNamePrefix), ffMpegRecordSettings.FileNamePrefix, ffMpegRecordSettingsId);
                SetValue(nameof(ffMpegRecordSettings.RecordingSaveDirectory), ffMpegRecordSettings.RecordingSaveDirectory, ffMpegRecordSettingsId);
                SetValue(nameof(ffMpegRecordSettings.StreamArguments), ffMpegRecordSettings.StreamArguments, ffMpegRecordSettingsId);

                SetValue(CameraIds, devices.Keys.Aggregate((x, y) => x + cameraIdsSeparator + y));
            }
        }

        public void AddCameraProperty(CameraProperty cameraProperty)
        {
            using (var scopedLock = configLock.WriterLock())
            {
                cameraProperties[cameraProperty.Id] = cameraProperty;

                SetValue(nameof(cameraProperty.Name), cameraProperty.Name, cameraProperty.Id);
                SetValue(nameof(cameraProperty.StringValues), ObjectSerialize.SerializeToString(cameraProperty.StringValues.ToList()), cameraProperty.Id);
                SetValue(nameof(cameraProperty.UrlPath), cameraProperty.UrlPath, cameraProperty.Id);
                SetValue(nameof(cameraProperty.XPathForGet.Path.Expression), cameraProperty.XPathForGet.Path.Expression, cameraProperty.Id);

                SetValue(CameraPropertyIds, cameraProperties.Keys.Aggregate((x, y) => x + CameraPropertyIdsSeparator + y));

                //  recreate cameras
                RecreateCameras();
            }
        }

        /// <summary>
        /// Fires event that configuration changed.
        /// </summary>
        public void FireConfigChanged()
        {
            if (ConfigChanged != null)
            {
                var ConfigChangedCopy = ConfigChanged;
                ConfigChangedCopy(this, EventArgs.Empty);
            }
        }

        public void RemoveCamera(string cameraId)
        {
            using (var scopedLock = configLock.WriterLock())
            {
                devices.Remove(cameraId);
                if (devices.Count > 0)
                {
                    SetValue(CameraIds, devices.Keys.Aggregate((x, y) => x + cameraIdsSeparator + y));
                }
                else
                {
                    SetValue(CameraIds, string.Empty);
                }
                HS.ClearINISection(cameraId, FileName);
            }
        }

        public void RemoveCameraProperty(string cameraPropertyId)
        {
            using (var scopedLock = configLock.WriterLock())
            {
                cameraProperties.Remove(cameraPropertyId);
                if (devices.Count > 0)
                {
                    SetValue(CameraPropertyIds, cameraProperties.Keys.Aggregate((x, y) => x + CameraPropertyIdsSeparator + y));
                }
                else
                {
                    SetValue(CameraPropertyIds, string.Empty);
                }
                HS.ClearINISection(cameraPropertyId, FileName);

                //  recreate cameras
                RecreateCameras();
            }
        }

        private TimeSpan GetTimeSpanValue(string key, TimeSpan defaultValue, string section)
        {
            long seconds = GetValue<long>(key, (long)defaultValue.TotalSeconds, section);
            return TimeSpan.FromSeconds(seconds);
        }

        private T GetValue<T>(string key, T defaultValue)
        {
            return GetValue(key, defaultValue, DefaultSection);
        }

        private T GetValue<T>(string key, T defaultValue, string section)
        {
            string stringValue = HS.GetINISetting(section, key, null, FileName);

            if (stringValue != null)
            {
                try
                {
                    T result = (T)System.Convert.ChangeType(stringValue, typeof(T), CultureInfo.InvariantCulture);
                    return result;
                }
                catch (Exception)
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        private void LoadCameraProperties()
        {
            string cameraPropertiesIdsConcatString = GetValue(CameraPropertyIds, string.Empty);
            var cameraPropertiesIds = cameraPropertiesIdsConcatString.Split(cameraIdsSeparator);

            foreach (var cameraPropertyId in cameraPropertiesIds)
            {
                if (string.IsNullOrWhiteSpace(cameraPropertyId))
                {
                    continue;
                }

                try
                {
                    cameraProperties.Add(cameraPropertyId, new CameraProperty(cameraPropertyId,
                             GetValue(nameof(CameraProperty.Name), string.Empty, cameraPropertyId),
                             GetValue(nameof(CameraProperty.UrlPath), string.Empty, cameraPropertyId),
                             GetValue(nameof(CameraProperty.XPathForGet.Path.Expression), string.Empty, cameraPropertyId),
                             ObjectSerialize.DeSerializeToObject<List<string>>(GetValue(nameof(CameraProperty.StringValues), string.Empty, cameraPropertyId))?.ToImmutableSortedSet()));
                }
                catch (Exception ex)
                {
                    Trace.TraceError(Invariant($"Failed to read config for {cameraPropertyId} with {ExceptionHelper.GetFullMessage(ex)}"));
                }
            }
        }

        private void LoadCameras()
        {
            string cameraIdsConcatString = GetValue(CameraIds, string.Empty);
            var cameraIds = cameraIdsConcatString.Split(cameraIdsSeparator);

            var periodicFetchedProperties = cameraProperties.ToImmutableDictionary();
            foreach (var cameraId in cameraIds)
            {
                if (string.IsNullOrWhiteSpace(cameraId))
                {
                    continue;
                }

                try
                {
                    var ffMpegRecordSettingsId = cameraId + nameof(CameraSettings.FfMpegRecordSettings);

                    FFMpegRecordSettings ffMpegRecordSettings = null;

                    ffMpegRecordSettings = new FFMpegRecordSettings(
                                GetValue(nameof(FFMpegRecordSettings.StreamArguments), string.Empty, ffMpegRecordSettingsId),
                                GetValue(nameof(FFMpegRecordSettings.RecordingSaveDirectory), string.Empty, ffMpegRecordSettingsId),
                                GetValue(nameof(FFMpegRecordSettings.FileNamePrefix), string.Empty, ffMpegRecordSettingsId),
                                GetValue(nameof(FFMpegRecordSettings.FileNameExtension), string.Empty, ffMpegRecordSettingsId),
                                GetValue(nameof(FFMpegRecordSettings.FileEncodeOptions), string.Empty, ffMpegRecordSettingsId));

                    devices.Add(cameraId, new CameraSettings(cameraId,
                                GetValue(nameof(CameraSettings.Name), string.Empty, cameraId),
                                GetValue(nameof(CameraSettings.CameraHost), string.Empty, cameraId),
                                GetValue(nameof(CameraSettings.Login), string.Empty, cameraId),
                                HS.DecryptString(GetValue(nameof(CameraSettings.Password), string.Empty, cameraId), nameof(CameraSettings.Password)),
                                GetTimeSpanValue(nameof(CameraSettings.AlarmCancelInterval), TimeSpan.Zero, cameraId),
                                periodicFetchedProperties,
                                GetTimeSpanValue(nameof(CameraSettings.CameraPropertiesRefreshInterval), TimeSpan.Zero, cameraId),
                                GetValue(nameof(CameraSettings.SnapshotDownloadDirectory), string.Empty, cameraId),
                                GetValue(nameof(CameraSettings.VideoDownloadDirectory), string.Empty, cameraId),
                                ffMpegRecordSettings));
                }
                catch (Exception ex)
                {
                    Trace.TraceError(Invariant($"Failed to read config for {cameraId} with {ExceptionHelper.GetFullMessage(ex)}"));
                }
            }
        }

        private void RecreateCameras()
        {
            var copyCameras = devices.ToImmutableList();
            devices.Clear();

            var periodicFetchedProperties = cameraProperties.ToImmutableDictionary();
            foreach (var camera in copyCameras)
            {
                devices.Add(camera.Key, new CameraSettings(camera.Value.Id,
                                                           camera.Value.Name,
                                                           camera.Value.CameraHost,
                                                           camera.Value.Login,
                                                           camera.Value.Password,
                                                           camera.Value.AlarmCancelInterval,
                                                           periodicFetchedProperties,
                                                           camera.Value.CameraPropertiesRefreshInterval,
                                                           camera.Value.SnapshotDownloadDirectory,
                                                           camera.Value.VideoDownloadDirectory,
                                                           camera.Value.FfMpegRecordSettings));
            }
        }

        private void SetValue<T>(string key, T value)
        {
            SetValue<T>(key, value, DefaultSection);
        }

        private void SetValue<T>(string key, T value, string section)
        {
            string stringValue = System.Convert.ToString(value, CultureInfo.InvariantCulture);
            HS.SaveINISetting(section, key, stringValue, FileName);
        }

        private const string CameraIds = "CameraIds";
        private const char cameraIdsSeparator = '|';
        private const string CameraPropertyIds = "CameraPropertyIds";
        private const char CameraPropertyIdsSeparator = '|';
        private const string DebugLoggingKey = "DebugLogging";
        private const string DefaultSection = "Settings";
        private readonly static string FileName = Invariant($"{Path.GetFileName(System.Reflection.Assembly.GetEntryAssembly().Location)}.ini");
        private readonly Dictionary<string, CameraProperty> cameraProperties = new Dictionary<string, CameraProperty>();
        private readonly AsyncReaderWriterLock configLock = new AsyncReaderWriterLock();
        private readonly Dictionary<string, CameraSettings> devices = new Dictionary<string, CameraSettings>();
        private readonly IHSApplication HS;
        private bool debugLogging;
        private string ffmpegPath;
    };
}