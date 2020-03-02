using Hspi.DeviceData;
using Hspi.DeviceData.Hikvision.Isapi;
using NullGuard;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Hspi.Camera.Hikvision.Isapi
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class CameraSettings : ICameraSettings, IEquatable<CameraSettings>
    {
        public CameraSettings(string id,
                              string name,
                              string cameraHost,
                              string login,
                              string password,
                              in TimeSpan alarmCancelInterval,
                              ImmutableDictionary<string, CameraProperty> periodicFetchedProperties,
                              in TimeSpan cameraPropertiesRefreshInterval,
                              string snapshotDownloadDirectory,
                              string videoDownloadDirectory)
        {
            Id = id;
            Name = name;
            CameraHost = cameraHost;
            Login = login;
            Password = password;
            AlarmCancelInterval = alarmCancelInterval;
            PeriodicFetchedCameraProperties = periodicFetchedProperties;
            CameraPropertiesRefreshInterval = cameraPropertiesRefreshInterval;
            SnapshotDownloadDirectory = snapshotDownloadDirectory;
            VideoDownloadDirectory = videoDownloadDirectory;
        }

        public string CameraHost { get; }
        public TimeSpan CameraPropertiesRefreshInterval { get; }
        public string Id { get; }
        public string Name { get; }
        public ImmutableDictionary<string, CameraProperty> PeriodicFetchedCameraProperties { get; }
        public string SnapshotDownloadDirectory { get; }
        public string VideoDownloadDirectory { get; }

        public CameraBase CreateCamera(CancellationToken shutdownDownToken)
        {
            return new HikvisionIdapiCamera(this, shutdownDownToken);
        }

        public bool Equals(CameraSettings other)
        {
            if (other == this)
            {
                return true;
            }
            bool same = other.Id == Id &&
                   other.Name == Name &&
                   other.Password == Password &&
                   other.CameraHost == CameraHost &&
                   other.AlarmCancelInterval == AlarmCancelInterval &&
                   other.CameraPropertiesRefreshInterval == CameraPropertiesRefreshInterval &&
                   other.SnapshotDownloadDirectory == SnapshotDownloadDirectory &&
                   other.VideoDownloadDirectory == VideoDownloadDirectory;

            if (same)
            {
                var firstNotSecond = PeriodicFetchedCameraProperties.Except(other.PeriodicFetchedCameraProperties).ToList();
                var secondNotFirst = other.PeriodicFetchedCameraProperties.Except(PeriodicFetchedCameraProperties).ToList();
                same = firstNotSecond.Count == 0 && secondNotFirst.Count == 0;
            }

            return same;
        }

        public override bool Equals([AllowNull]object obj)
        {
            if (obj == null)
            {
                return false;
            }

            CameraSettings cameraSettingsObj = obj as CameraSettings;
            if (cameraSettingsObj == null)
            {
                return false;
            }
            else
            {
                return Equals(cameraSettingsObj);
            }
        }

        public bool Equals(ICameraSettings other)
        {
            var otherCameraSetting = other as CameraSettings;
            return otherCameraSetting == null ? false : Equals(otherCameraSetting);
        }

        public DeviceDataBase GetDevice(DeviceIdentifier deviceIdentifier)
        {
            switch (deviceIdentifier.DeviceType)
            {
                case DeviceType.HikvisionISAPIRoot:
                    return new RootDeviceData();

                case DeviceType.HikvisionISAPICameraProperty:
                    if (PeriodicFetchedCameraProperties.TryGetValue(deviceIdentifier.DeviceSubTypeId, out var cameraProperty))
                    {
                        return new CameraPropertyDeviceData(cameraProperty);
                    }
                    return null;

                case DeviceType.HikvisionISAPIAlarm:
                    return new AlarmDeviceData(deviceIdentifier.DeviceSubTypeId);

                case DeviceType.HikvisionISAPIAlarmStreamConnected:
                    return new AlarmConnectedDeviceData();

                default:
                    throw new NotImplementedException();
            }
        }

        public override int GetHashCode()
        {
            return PeriodicFetchedCameraProperties.GetHashCode() ^
                   CameraPropertiesRefreshInterval.GetHashCode() ^
                   SnapshotDownloadDirectory.GetHashCode() ^
                   VideoDownloadDirectory.GetHashCode() ^
                   Name.GetHashCode() ^
                   AlarmCancelInterval.GetHashCode() ^
                   CameraHost.GetHashCode() ^
                   Id.GetHashCode() ^
                   Login.GetHashCode() ^
                   Password.GetHashCode();
        }

        public DeviceDataBase GetRootDevice()
        {
            return new RootDeviceData();
        }

        public DeviceIdentifier GetRootDeviceIdentifier()
        {
            return new DeviceIdentifier(Id, DeviceType.HikvisionISAPIRoot, "Root");
        }

        public readonly TimeSpan AlarmCancelInterval;
        public readonly string Login;
        public readonly string Password;
    }
}