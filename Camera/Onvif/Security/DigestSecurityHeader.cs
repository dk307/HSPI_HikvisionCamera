using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.ServiceModel.Channels;
using System.Text;
using System.Xml;

namespace Hspi.Camera.Onvif.Security
{
    class DigestSecurityHeader : MessageHeader
    {
        public DigestSecurityHeader(NetworkCredential credential, SecurityToken token)
        {
            if (credential == null)
                throw new ArgumentNullException(nameof(credential));
            if (token == null)
                throw new ArgumentNullException(nameof(token));

            _credential = credential;
            _token = token;
            _createdTimestamp = Stopwatch.GetTimestamp();
        }

        public override string Name => "Security";
        public override string Namespace => "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
        protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
        {
            string createdTime = GetCurrentServerTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

            writer.WriteAttributeString("s", "mustUnderstand", null, "1");
            writer.WriteStartElement("UsernameToken");
            writer.WriteStartElement("Username");
            writer.WriteString(_credential.UserName);
            writer.WriteEndElement();
            writer.WriteStartElement("Password");
            writer.WriteAttributeString("Type", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordDigest");
            writer.WriteString(ComputePasswordDigest(_credential.Password, _token.GetNonceBytes(), createdTime));
            writer.WriteEndElement();
            writer.WriteStartElement("Nonce");
            writer.WriteAttributeString("EncodingType", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary");
            writer.WriteBase64(_token.GetNonceBytes(), 0, _token.GetNonceBytes().Length);
            writer.WriteEndElement();
            writer.WriteStartElement("Created", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd");
            writer.WriteString(createdTime);
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.Flush();
        }

        private static string ComputePasswordDigest(string password, byte[] nonceBytes, string createdTime)
        {
            byte[] createdTimeBytes = Encoding.UTF8.GetBytes(createdTime);
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

            byte[] aggregationBytes = nonceBytes.Concat(createdTimeBytes).Concat(passwordBytes).ToArray();
#pragma warning disable CA5350 // Do Not Use Weak Cryptographic Algorithms
            using (var sha1 = SHA1.Create())
#pragma warning restore CA5350 // Do Not Use Weak Cryptographic Algorithms
            {
                byte[] hashBytes = sha1.ComputeHash(aggregationBytes);
                return Convert.ToBase64String(hashBytes);
            }
        }

        private DateTime GetCurrentServerTime()
        {
            long timestamp = Stopwatch.GetTimestamp();
            long elapsedMilliseconds = timestamp - _createdTimestamp * 1000 / Stopwatch.Frequency;

            if (elapsedMilliseconds < 0)
            {
                _createdTimestamp = timestamp;
                return _token.ServerTime;
            }

            return _token.ServerTime + TimeSpan.FromTicks(elapsedMilliseconds * TimeSpan.TicksPerMillisecond);
        }

        private readonly NetworkCredential _credential;
        private readonly SecurityToken _token;
        private long _createdTimestamp;
    }
}
