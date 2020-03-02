using System.ServiceModel.Channels;
using System.Xml;

namespace Hspi.Camera.Onvif.Behaviour
{
    class CustomAddressHeader : AddressHeader
    {
        public CustomAddressHeader(XmlElement xmlElement)
        {
            _xmlElement = xmlElement;

            Name = xmlElement.LocalName;
            Namespace = xmlElement.NamespaceURI;
        }

        public override string Name { get; }
        public override string Namespace { get; }
        protected override void OnWriteAddressHeaderContents(XmlDictionaryWriter writer)
        {
            _xmlElement.WriteContentTo(writer);
        }

        private readonly XmlElement _xmlElement;
    }
}
