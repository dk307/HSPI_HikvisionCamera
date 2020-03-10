using Hspi.DeviceData;
using NullGuard;

namespace Hspi.Camera.Hikvision.Isapi
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class CameraPropertyInfo : ICameraContruct
    {
        public CameraPropertyInfo(CameraProperty property, [AllowNull]string value)
        {
            Property = property;
            Value = value;
        }

        public DeviceType DeviceType => DeviceType.HikvisionISAPICameraProperty;
        public string Id => Property.Id;
        public CameraProperty Property { get; }
        public string Value { get; }
    }
}