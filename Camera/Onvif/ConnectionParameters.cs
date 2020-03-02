using System;
using System.Net;

namespace Hspi.Camera.Onvif
{
    public sealed class ConnectionParameters
    {
        public ConnectionParameters(Uri connectionUri, NetworkCredential credentials, TimeSpan connectionTimeout)
        {
            ConnectionUri = connectionUri;
            Credentials = credentials;
            ConnectionTimeout = connectionTimeout;
        }

        public TimeSpan ConnectionTimeout { get; }
        public Uri ConnectionUri { get; }
        public NetworkCredential Credentials { get; }
    }
}