using NullGuard;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace Hspi.Camera
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class CameraSettings : IEquatable<CameraSettings>
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

        public ImmutableDictionary<string, CameraProperty> PeriodicFetchedCameraProperties { get; }
        public TimeSpan CameraPropertiesRefreshInterval { get; }
        public string SnapshotDownloadDirectory { get; }
        public string VideoDownloadDirectory { get; }
        public string Name { get; }
        public readonly TimeSpan AlarmCancelInterval;
        public readonly string CameraHost;
        public readonly string Id;
        public readonly string Login;
        public readonly string Password;

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

        public override bool Equals(object obj)
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
    }
}