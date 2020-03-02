using System.ComponentModel;

namespace Hspi.DeviceData
{
    internal enum DeviceType
    {
        [Description("Root")]
        HikvisionISAPIRoot,

        [Description("CameraProperty")]
        HikvisionISAPICameraProperty,

        [Description("Alarm")]
        HikvisionISAPIAlarm,

        [Description("AlarmStreamConnected")]
        HikvisionISAPIAlarmStreamConnected,

        [Description("OnvifRoot")]
        OnvifRoot,

        [Description("OnvifEvent")]
        OnvifEvent,
    }
}