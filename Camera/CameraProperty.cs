using System;
using System.Collections.Immutable;
using NullGuard;

namespace Hspi.Camera
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class CameraProperty : IEquatable<CameraProperty>
    {
        public CameraProperty(string id,
                             string name,
                             string urlPath,
                             string xpath,
                             [AllowNull]ImmutableSortedSet<string> stringValues)
        {
            Id = id;
            Name = name;
            XPathForGet = new XmlPathData(xpath);
            UrlPath = urlPath;
            StringValues = stringValues ?? ImmutableSortedSet<string>.Empty;
        }

        public string Id { get; }
        public string Name { get; }
        public string UrlPath { get; }
        public XmlPathData XPathForGet { get; }
        public ImmutableSortedSet<string> StringValues { get; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "0")]
        public bool Equals([AllowNull] CameraProperty other)
        {
            if (other == null)
            {
                return false;
            }

            if (this == other)
            {
                return true;
            }

            return Id == other.Id &&
                   Name == other.Name &&
                   UrlPath == other.UrlPath &&
                   XPathForGet.Path.Expression == other.XPathForGet.Path.Expression &&
                   StringValues.SetEquals(other.StringValues);
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            CameraProperty cameraPropertyObj = obj as CameraProperty;
            if (cameraPropertyObj == null)
            {
                return false;
            }
            else
            {
                return Equals(cameraPropertyObj);
            }
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode() ^
                   UrlPath.GetHashCode() ^
                   XPathForGet.Path.Expression.GetHashCode() ^
                   StringValues.GetHashCode();
        }
    };
}