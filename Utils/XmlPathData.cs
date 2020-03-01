using NullGuard;
using System;
using System.Xml.XPath;

namespace Hspi.Utils
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    public sealed class XmlPathData
    {
        public XmlPathData(string xpath)
        {
            path = new Lazy<XPathExpression>(() => { return XPathExpression.Compile(xpath); }, true);
        }

        public XPathExpression Path => path.Value;
        private readonly Lazy<XPathExpression> path;
    }
}