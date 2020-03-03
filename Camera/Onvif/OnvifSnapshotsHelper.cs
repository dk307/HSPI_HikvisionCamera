using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hspi.Camera.Onvif
{
    internal sealed class OnvifSnapshotsHelper : SnapshotsHelper
    {
        public OnvifSnapshotsHelper(OnvifCamera camera,
                                    CancellationToken cancellationToken) :
            base(cancellationToken)
        {
            this.camera = camera;
        }

        public override Task<string> DownloadSnapshot()
        {
            return camera.DownloadSnapshot(snapshotUri);
        }

        public async Task Initialize()
        {
            snapshotUri = await camera.GetSnapshotUri().ConfigureAwait(false);
        }
        private readonly OnvifCamera camera;
        private Uri snapshotUri;
    }
}