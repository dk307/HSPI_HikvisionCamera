using Hspi.DeviceData;
using System;
using System.Threading;

namespace Hspi.Camera
{
    internal interface ICameraSettings : IEquatable<ICameraSettings>
    {
        string CameraHost { get; }
        string Id { get; }
        string Name { get; }
        CameraBase CreateCamera(CancellationToken shutdownDownToken);

        DeviceDataBase GetDevice(DeviceIdentifier deviceIdentifier);

        DeviceIdentifier GetRootDeviceIdentifier();

        DeviceDataBase GetRootDevice();
    };
}