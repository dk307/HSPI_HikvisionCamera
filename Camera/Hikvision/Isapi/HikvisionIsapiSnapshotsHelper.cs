using System.Threading;
using System.Threading.Tasks;

namespace Hspi.Camera.Hikvision.Isapi
{
    internal sealed class HikvisionIsapiSnapshotsHelper : SnapshotsHelper
    {
        public HikvisionIsapiSnapshotsHelper(HikvisionIsapiCamera hikvisionIdapiCamera,
                                                               CancellationToken cancellationToken) :
            base(cancellationToken)
        {
            this.hikvisionIdapiCamera = hikvisionIdapiCamera;
        }

        public override Task<string> DownloadSnapshot()
        {
            return hikvisionIdapiCamera.DownloadSnapshot(HikvisionIsapiCamera.Track1);
        }

        private readonly HikvisionIsapiCamera hikvisionIdapiCamera;
    }
}