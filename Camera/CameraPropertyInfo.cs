using Hspi.DeviceData;
using NullGuard;
using System;

namespace Hspi.Camera
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class CameraPropertyInfo : ICameraContruct
    {
        public CameraPropertyInfo(CameraProperty property, [AllowNull]string value)
        {
            Property = property;
            Value = value;
        }

        public DeviceType DeviceType => DeviceType.CameraProperty;
        public string Id => Property.Id;
        public CameraProperty Property { get; }
        public string Value { get; }
    }
}