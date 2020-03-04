using Hspi.DeviceData;
using Hspi.DeviceData.Onvif;
using NullGuard;
using System;
using System.Threading;

namespace Hspi.Camera.Onvif
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
                              string snapshotDownloadDirectory)
        {
            Id = id;
            Name = name;
            CameraHost = cameraHost;
            Login = login;
            Password = password;
            AlarmCancelInterval = alarmCancelInterval;
            SnapshotDownloadDirectory = snapshotDownloadDirectory;
        }

        public string CameraHost { get; }
        public string Id { get; }
        public string Name { get; }
        public string SnapshotDownloadDirectory { get; }

        public CameraBase CreateCamera(CancellationToken shutdownDownToken)
        {
            return new OnvifCamera(this, shutdownDownToken);
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
                   other.SnapshotDownloadDirectory == SnapshotDownloadDirectory;

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
                case DeviceType.OnvifRoot:
                    return new RootDeviceData();

                case DeviceType.OnvifEvent:
                    return new OnvifEventData(deviceIdentifier.DeviceSubTypeId);

                default:
                    throw new NotImplementedException();
            }
        }

        public override int GetHashCode()
        {
            return SnapshotDownloadDirectory.GetHashCode() ^
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
            return new DeviceIdentifier(Id, DeviceType.OnvifRoot, "Root");
        }

        public readonly TimeSpan AlarmCancelInterval;
        public readonly string Login;
        public readonly string Password;
    }
}