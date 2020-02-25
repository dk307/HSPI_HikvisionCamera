﻿using Hspi.DeviceData;
using NullGuard;

namespace Hspi.Camera
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class AlarmStreamConnectedInfo : ICameraContruct
    {
        public AlarmStreamConnectedInfo(bool connected)
        {
            Connected = connected;
        }

        public bool Connected { get; }

        public DeviceType DeviceType => DeviceType.AlarmStreamConnected;

        public string Value => !Connected ? OnOffDeviceData.OffValueString : OnOffDeviceData.OnValueString;
         
        public string Id => "Alarm Streem Connected";

    };
}