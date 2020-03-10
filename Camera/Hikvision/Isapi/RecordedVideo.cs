using System;
using System.Web;

namespace Hspi.Camera.Hikvision.Isapi
{
    internal readonly struct RecordedVideo
    {
        public RecordedVideo(int trackId, Uri rstpUri, DateTimeOffset startTime, DateTimeOffset endTime)
        {
            TrackId = trackId;
            RstpUri = rstpUri;
            StartTime = startTime;
            EndTime = endTime;
        }

        public readonly DateTimeOffset EndTime;
        public readonly Uri RstpUri;
        public readonly DateTimeOffset StartTime;
        public readonly int TrackId;

        public string Name => HttpUtility.ParseQueryString(RstpUri.Query).Get("name");
    };
}