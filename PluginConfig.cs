using HomeSeerAPI;
using Hspi.Camera;
using Hspi.Camera.Hikvision.Isapi;
using Hspi.Utils;
using Nito.AsyncEx;
using NullGuard;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using static System.FormattableString;

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

            debugLogging = GetValue(nameof(debugLogging), false);

            LoadHikvisionIsapiCameraProperties();
            LoadHikvisionIsapiCameras();
        }

        public event EventHandler<EventArgs> ConfigChanged;

        public ImmutableDictionary<string, ICameraSettings> AllCameras
        {
            get
            {
                using (var scopedLock = configLock.ReaderLock())
                {
                    var allCameras = new Dictionary<string, ICameraSettings>();


                    foreach (var hikvisionIsapiCamera in hikvisionIsapiCameras)
                    {
                        allCameras.Add(hikvisionIsapiCamera.Key, hikvisionIsapiCamera.Value);
                    }

                    return allCameras.ToImmutableDictionary();
                }
            }
        }
        public ImmutableDictionary<string, CameraSettings> HikvisionIsapiCameras
        {
            get
            {
                using (var scopedLock = configLock.ReaderLock())
                {
                    return hikvisionIsapiCameras.ToImmutableDictionary();
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

        public ImmutableDictionary<string, CameraProperty> HikvisionIsapiCameraProperties
        {
            get
            {
                using (var scopedLock = configLock.ReaderLock())
                {
                    return hikvisionIsapiCameraProperties.ToImmutableDictionary();
                }
            }
        }

        public void AddHikvisionIsapiCamera(CameraSettings device)
        {
            using (var scopedLock = configLock.WriterLock())
            {
                hikvisionIsapiCameras[device.Id] = device;

                SetValue(nameof(device.Name), device.Name, device.Id);
                SetValue(nameof(device.CameraHost), device.CameraHost, device.Id);
                SetValue(nameof(device.Login), device.Login, device.Id);
                SetValue(nameof(device.Password), HS.EncryptString(device.Password, nameof(device.Password)), device.Id);
                SetValue(nameof(device.AlarmCancelInterval), (long)device.AlarmCancelInterval.TotalSeconds, device.Id);
                SetValue(nameof(device.CameraPropertiesRefreshInterval), (long)device.CameraPropertiesRefreshInterval.TotalSeconds, device.Id);
                SetValue(nameof(device.SnapshotDownloadDirectory), device.SnapshotDownloadDirectory, device.Id);
                SetValue(nameof(device.VideoDownloadDirectory), device.VideoDownloadDirectory, device.Id);

                SetValue(HikvisionIsapiCameraIds, hikvisionIsapiCameras.Keys.Aggregate((x, y) => x + idsSeparator + y));
            }
        }

        public void AddHikvisionIsapiCameraProperty(CameraProperty cameraProperty)
        {
            using (var scopedLock = configLock.WriterLock())
            {
                hikvisionIsapiCameraProperties[cameraProperty.Id] = cameraProperty;

                SetValue(nameof(cameraProperty.Name), cameraProperty.Name, cameraProperty.Id);
                SetValue(nameof(cameraProperty.StringValues), ObjectSerialize.SerializeToString(cameraProperty.StringValues.ToList()), cameraProperty.Id);
                SetValue(nameof(cameraProperty.UrlPath), cameraProperty.UrlPath, cameraProperty.Id);
                SetValue(nameof(cameraProperty.XPathForGet.Path.Expression), cameraProperty.XPathForGet.Path.Expression, cameraProperty.Id);

                SetValue(HikvisionIsapiCameraPropertyIds, hikvisionIsapiCameraProperties.Keys.Aggregate((x, y) => x + HikvisionIsapiCameraPropertyIdsSeparator + y));

                //  recreate cameras
                RecreateHikvisionIsapiCameras();
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

        public void RemoveHikvisionIsapiCamera(string cameraId)
        {
            using (var scopedLock = configLock.WriterLock())
            {
                hikvisionIsapiCameras.Remove(cameraId);
                if (hikvisionIsapiCameras.Count > 0)
                {
                    SetValue(HikvisionIsapiCameraIds, hikvisionIsapiCameras.Keys.Aggregate((x, y) => x + idsSeparator + y));
                }
                else
                {
                    SetValue(HikvisionIsapiCameraIds, string.Empty);
                }
                HS.ClearINISection(cameraId, FileName);
            }
        }

        public void RemoveHikvisionIsapiCameraProperty(string cameraPropertyId)
        {
            using (var scopedLock = configLock.WriterLock())
            {
                hikvisionIsapiCameraProperties.Remove(cameraPropertyId);
                if (hikvisionIsapiCameras.Count > 0)
                {
                    SetValue(HikvisionIsapiCameraPropertyIds, hikvisionIsapiCameraProperties.Keys.Aggregate((x, y) => x + HikvisionIsapiCameraPropertyIdsSeparator + y));
                }
                else
                {
                    SetValue(HikvisionIsapiCameraPropertyIds, string.Empty);
                }
                HS.ClearINISection(cameraPropertyId, FileName);

                //  recreate cameras
                RecreateHikvisionIsapiCameras();
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

        private void LoadHikvisionIsapiCameraProperties()
        {
            string cameraPropertiesIdsConcatString = GetValue(HikvisionIsapiCameraPropertyIds, string.Empty);
            var cameraPropertiesIds = cameraPropertiesIdsConcatString.Split(idsSeparator);

            foreach (var cameraPropertyId in cameraPropertiesIds)
            {
                if (string.IsNullOrWhiteSpace(cameraPropertyId))
                {
                    continue;
                }

                try
                {
                    hikvisionIsapiCameraProperties.Add(cameraPropertyId, new CameraProperty(cameraPropertyId,
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

        private void LoadHikvisionIsapiCameras()
        {
            string cameraIdsConcatString = GetValue(HikvisionIsapiCameraIds, string.Empty);
            var cameraIds = cameraIdsConcatString.Split(idsSeparator);

            var periodicFetchedProperties = hikvisionIsapiCameraProperties.ToImmutableDictionary();
            foreach (var cameraId in cameraIds)
            {
                if (string.IsNullOrWhiteSpace(cameraId))
                {
                    continue;
                }

                try
                {
                    hikvisionIsapiCameras.Add(cameraId, new CameraSettings(cameraId,
                                GetValue(nameof(CameraSettings.Name), string.Empty, cameraId),
                                GetValue(nameof(CameraSettings.CameraHost), string.Empty, cameraId),
                                GetValue(nameof(CameraSettings.Login), string.Empty, cameraId),
                                HS.DecryptString(GetValue(nameof(CameraSettings.Password), string.Empty, cameraId), nameof(CameraSettings.Password)),
                                GetTimeSpanValue(nameof(CameraSettings.AlarmCancelInterval), TimeSpan.Zero, cameraId),
                                periodicFetchedProperties,
                                GetTimeSpanValue(nameof(CameraSettings.CameraPropertiesRefreshInterval), TimeSpan.Zero, cameraId),
                                GetValue(nameof(CameraSettings.SnapshotDownloadDirectory), string.Empty, cameraId),
                                GetValue(nameof(CameraSettings.VideoDownloadDirectory), string.Empty, cameraId)));
                }
                catch (Exception ex)
                {
                    Trace.TraceError(Invariant($"Failed to read config for {cameraId} with {ExceptionHelper.GetFullMessage(ex)}"));
                }
            }
        }
        private void RecreateHikvisionIsapiCameras()
        {
            var copyCameras = hikvisionIsapiCameras.ToImmutableList();
            hikvisionIsapiCameras.Clear();

            var periodicFetchedProperties = hikvisionIsapiCameraProperties.ToImmutableDictionary();
            foreach (var camera in copyCameras)
            {
                hikvisionIsapiCameras.Add(camera.Key, new CameraSettings(camera.Value.Id,
                                                           camera.Value.Name,
                                                           camera.Value.CameraHost,
                                                           camera.Value.Login,
                                                           camera.Value.Password,
                                                           camera.Value.AlarmCancelInterval,
                                                           periodicFetchedProperties,
                                                           camera.Value.CameraPropertiesRefreshInterval,
                                                           camera.Value.SnapshotDownloadDirectory,
                                                           camera.Value.VideoDownloadDirectory));
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

        private const string DebugLoggingKey = "DebugLogging";
        private const string DefaultSection = "Settings";
        private const string HikvisionIsapiCameraIds = "CameraIds";
        private const string HikvisionIsapiCameraPropertyIds = "CameraPropertyIds";
        private const char HikvisionIsapiCameraPropertyIdsSeparator = '|';
        private const char idsSeparator = '|';
        private readonly static string FileName = Invariant($"{Path.GetFileName(System.Reflection.Assembly.GetEntryAssembly().Location)}.ini");
        private readonly AsyncReaderWriterLock configLock = new AsyncReaderWriterLock();
        private readonly Dictionary<string, CameraProperty> hikvisionIsapiCameraProperties = new Dictionary<string, CameraProperty>();
        private readonly Dictionary<string, CameraSettings> hikvisionIsapiCameras = new Dictionary<string, CameraSettings>();
        private readonly IHSApplication HS;
        private bool debugLogging;
    };
}