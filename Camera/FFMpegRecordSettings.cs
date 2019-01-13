using NullGuard;
using System;

namespace Hspi.Camera
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class FFMpegRecordSettings : IEquatable<FFMpegRecordSettings>
    {
        public FFMpegRecordSettings(string streamArguments,
                              string recordingSaveDirectory,
                              string fileNamePrefix,
                              string fileNameExtension,
                              string fileEncodeOptions)
        {
            StreamArguments = streamArguments;
            RecordingSaveDirectory = recordingSaveDirectory;
            FileNamePrefix = fileNamePrefix;
            FileNameExtension = fileNameExtension;
            FileEncodeOptions = fileEncodeOptions;
        }

        public string FileEncodeOptions { get; }
        public string FileNameExtension { get; }
        public string FileNamePrefix { get; }
        public string RecordingSaveDirectory { get; }
        public string StreamArguments { get; }

        public bool Equals(FFMpegRecordSettings other)
        {
            return StreamArguments == other.StreamArguments &&
                    RecordingSaveDirectory == other.RecordingSaveDirectory &&
                    FileNamePrefix == other.FileNamePrefix &&
                    FileNameExtension == other.FileNameExtension &&
                    FileEncodeOptions == other.FileEncodeOptions;
        }

        public override int GetHashCode()
        {
            return FileEncodeOptions.GetHashCode() ^
                   RecordingSaveDirectory.GetHashCode() ^
                   FileNamePrefix.GetHashCode() ^
                   FileNameExtension.GetHashCode() ^
                   FileEncodeOptions.GetHashCode();
        }
    }
}