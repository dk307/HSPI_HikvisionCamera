using System;

namespace Hspi.Pages
{
    [Serializable]
    internal class TakeSnapshotAction
    {
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(Id) &&
                   TimeSpan > TimeSpan.Zero;
        }

        public string Id;
        public TimeSpan TimeSpan = TimeSpan.Zero;
    }
}