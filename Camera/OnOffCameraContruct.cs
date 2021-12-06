using Hspi.DeviceData;
using NullGuard;

namespace Hspi.Camera
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]

    internal abstract class OnOffCameraContruct : ICameraContruct
    {
        protected OnOffCameraContruct(string id, DeviceType deviceType, bool active)
        {
            Id = id;
            DeviceType = deviceType;
            Active = active;
        }

        public string Value => !Active ? OnOffDeviceData.OffValueString : OnOffDeviceData.OnValueString;

        public bool Active { get; }

        public DeviceType DeviceType { get; }

        public string Id { get; }
    };
}