using System.Threading;
using System.Threading.Tasks;

namespace Hspi.Camera.Hikvision.Isapi
{
    internal sealed class HikvisionIdapiSnapshotsHelper : SnapshotsHelper
    {
        public HikvisionIdapiSnapshotsHelper(HikvisionIdapiCamera hikvisionIdapiCamera,
                                                               CancellationToken cancellationToken) :
            base(cancellationToken)
        {
            this.hikvisionIdapiCamera = hikvisionIdapiCamera;
        }

        public override Task<string> DownloadSnapshot()
        {
            return hikvisionIdapiCamera.DownloadSnapshot(HikvisionIdapiCamera.Track1);
        }

        private readonly HikvisionIdapiCamera hikvisionIdapiCamera;
    }
}