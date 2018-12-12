using Hspi.DeviceData;

namespace Hspi.Camera
{
    internal interface ICameraContruct
    {
        DeviceType DeviceType { get; }
        string Id { get; }
        string Value { get; }
    };
}