using System;

namespace Hspi.Camera.Onvif.Security
{
    public class SecurityToken
    {
        public SecurityToken(DateTime serverTime, byte[] nonceBytes)
        {
            ServerTime = serverTime;
            this.nonceBytes = nonceBytes;
        }

        public DateTime ServerTime { get; }

        public byte[] GetNonceBytes()
        {
            return nonceBytes;
        }

        private readonly byte[] nonceBytes;
    }
}