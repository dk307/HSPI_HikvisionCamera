using System;
using NullGuard;

namespace Hspi.Camera
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class CameraProperty : IEquatable<CameraProperty>
    {
        public enum Type
        {
            String,
            Number,
        }

        public CameraProperty(string id, string name, string urlPath, string xpath, Type type)
        {
            Id = id;
            Name = name;
            XPathForGet = new XmlPathData(xpath);
            UrlPath = urlPath;
            CameraPropertyType = type;
        }

        public string Id { get; }
        public string Name { get; }
        public string UrlPath { get; }
        public XmlPathData XPathForGet { get; }
        public Type CameraPropertyType { get; }

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
                   CameraPropertyType == other.CameraPropertyType;
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
                   CameraPropertyType.GetHashCode();
        }
    };
}