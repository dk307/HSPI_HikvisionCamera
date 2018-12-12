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

        public DeviceType DeviceType
        {
            get
            {
                switch (Property.CameraPropertyType)
                {
                    case CameraProperty.Type.String:
                        return DeviceType.CameraPropertyString;

                    case CameraProperty.Type.Number:
                        return DeviceType.CameraPropertyNumber;

                    default:
                        throw new NotImplementedException();
                }
            }
        }

        public string Id => Property.Id;
        public CameraProperty Property { get; }
        public string Value { get; }
    }
}