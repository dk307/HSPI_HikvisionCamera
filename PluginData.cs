using System.IO;

namespace Hspi
{
    /// <summary>
    /// Class to store static data for PlugIn
    /// </summary>
    internal static class PluginData
    {
        /// <summary>
        /// The plugin name
        /// </summary>
        public const string PlugInName = @"Hikvision Camera";

        /// <summary>
        /// The images path root for devices
        /// </summary>
        public static readonly string HSImagesPathRoot = Path.Combine(Path.DirectorySeparatorChar.ToString(), "images", "HomeSeer", "status");
    }
}