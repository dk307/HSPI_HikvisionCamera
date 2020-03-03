using Hspi.Onvif.Contracts.Event;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.XPath;

namespace Hspi.Camera.Onvif
{
    internal sealed class DeviceEvent
    {
        public DeviceEvent(NotificationMessageHolderType notificationMessageHolderType)
        {
            this.Topics = GetTopics(notificationMessageHolderType);
            this.Sources = GetDataFromMessage(notificationMessageHolderType, sourceSelector);
            this.Data = GetDataFromMessage(notificationMessageHolderType, dataSelector);
            this.Id = CalculateId();
        }

        public IReadOnlyDictionary<string, string> Data { get; }

        public string Id { get; }

        public IReadOnlyDictionary<string, string> Sources { get; }

        public IReadOnlyList<string> Topics { get; }

        public string Value
        {
            get
            {
                var dataProcessed = Data.First();
                return dataProcessed.Value;
            }
        }

        public bool? ValueAsBoolean
        {
            get
            {
                var value = Value;
                if (string.Equals(value, "false", System.StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                if (string.Equals(value, "true", System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                return null;
            }
        }

        private static string ConcatString(string value, int length = 32)
        {
            if (value.Length > length)
            {
                int halfLength = length / 2 - 1;
                return value.Substring(0, halfLength) +
                       ".." +
                       value.Substring(value.Length - halfLength, halfLength);
            }
            return value;
        }

        private static string GetSingleOrHash(IReadOnlyList<string> list)
        {
            switch (list.Count)
            {
                case 0:
                    return "(None)";

                case 1:
                    return list[0];

                default:
                    {
                        byte[] data = hashCreator.ComputeHash(Encoding.UTF8.GetBytes(string.Join(",", list)));

                        StringBuilder stringBuilder = new StringBuilder(data.Length * 2);
                        for (int i = 0; i < data.Length; i++)
                        {
                            stringBuilder.Append(data[i].ToString("x2", CultureInfo.InvariantCulture));
                        }

                        return stringBuilder.ToString();
                    }
            }
        }

        private string CalculateId()
        {
            var topicProcessed = Topics.Select(x =>
            {
                int index = x.IndexOf(':', 0);
                if (index == -1)
                {
                    return x;
                }
                return x.Substring(index + 1);
            }).ToList();
            var topic = GetSingleOrHash(topicProcessed);
            var sourceProcessed = Sources.Select(x =>
            {
                return x.Key + ":" + x.Value;
            }).ToList();
            var source = GetSingleOrHash(sourceProcessed);

            var dataProcessed = Data.First();
            var data = dataProcessed.Key;
            return $"{ConcatString(topic)}-{ConcatString(source)}-{ConcatString(data)}";
        }

        private IReadOnlyDictionary<string, string> GetDataFromMessage(NotificationMessageHolderType notificationMessageHolderType,
                                                                       XPathExpression selector)
        {
            var sources = new Dictionary<string, string>();

            System.Xml.XmlElement message = notificationMessageHolderType.Message;
            if (message != null)
            {
                XPathNavigator rootNavigator = message.CreateNavigator();
                XPathNodeIterator childNodeIter = rootNavigator.Select(selector);

                if (childNodeIter != null)
                {
                    while (childNodeIter.MoveNext())
                    {
                        var currentNode = childNodeIter.Current;
                        var name = currentNode.GetAttribute("Name", string.Empty);
                        var value = currentNode.GetAttribute("Value", string.Empty);
                        sources.Add(name, value);
                    }
                }
            }
            return sources;
        }

        private IReadOnlyList<string> GetTopics(NotificationMessageHolderType notificationMessageHolderType)
        {
            var topics = new List<string>();
            foreach (var topic in notificationMessageHolderType.Topic?.Any)
            {
                topics.Add(topic.InnerText);
            }

            return topics;
        }

        private static readonly XPathExpression dataSelector
                            = XPathExpression.Compile("*[local-name()='Data']/*[local-name()='SimpleItem']");

        private static readonly XPathExpression sourceSelector
                            = XPathExpression.Compile("*[local-name()='Source']/*[local-name()='SimpleItem']");

#pragma warning disable CA5351 // Do Not Use Broken Cryptographic Algorithms
        private static HashAlgorithm hashCreator = MD5.Create();
#pragma warning restore CA5351 // Do Not Use Broken Cryptographic Algorithms
    }
}