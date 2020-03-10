using HeyRed.Mime;
using NullGuard;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Hspi.Camera
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class DownloadHelper
    {
        public DownloadHelper(string cameraName,
                              HttpClient defaultHttpClient,
                              CancellationToken cancellationToken)
        {
            this.cameraName = cameraName;
            this.defaultHttpClient = defaultHttpClient;
            this.cancellationToken = cancellationToken;
        }

        public async Task<string> DownloadToFile(string path,
                                                 Uri uri,
                                                 HttpMethod httpMethod,
                                                 [AllowNull]string extension,
                                                 [AllowNull]string data)
        {
            string tempPath = Path.ChangeExtension(path, "tmp");
            try
            {
                string mediaType = null;
                using (var fileStream = new FileStream(tempPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
                {
                    mediaType = await DownloadToStream(fileStream, uri, httpMethod, data).ConfigureAwait(false);
                }
                string fileExtension = extension ?? MimeTypesMap.GetExtension(mediaType);
                string destFileName = Path.ChangeExtension(path, fileExtension);
                if (File.Exists(destFileName))
                {
                    string destFileNameOld = Path.ChangeExtension(destFileName, "old");
                    File.Move(destFileName, destFileNameOld);
                    File.Delete(destFileNameOld);
                }

                File.Move(tempPath, destFileName);
                return destFileName;
            }
            finally
            {
                // always delete the tmp file
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        public async Task<HttpResponseMessage> SendToCamera(HttpRequestMessage httpRequestMessage,
                                                            string content = null,
                                                            HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead,
                                                            HttpClient client = null)
        {
            if (content != null)
            {
                httpRequestMessage.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
            }

            var response = await (client ?? defaultHttpClient).SendAsync(httpRequestMessage, completionOption, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string failureContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new HttpRequestException(Invariant($"Request failed with {response.StatusCode}:{response.ReasonPhrase} to {httpRequestMessage.RequestUri} with {failureContent}"));
            }

            return response;
        }

        private async Task<string> DownloadToStream(Stream stream, Uri uri, HttpMethod httpMethod, [AllowNull]string data)
        {
            using (var httpRequestMessage = new HttpRequestMessage(httpMethod, uri))
            {
                using (var response = await SendToCamera(httpRequestMessage, data,
                                                         HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                {
                    string mediaType = response.Content.Headers?.ContentType?.MediaType;

                    if ((mediaType == null))
                    {
                        throw new Exception(Invariant($"[{cameraName}]Invalid Data for {uri} :{mediaType ?? string.Empty}"));
                    }

                    using (var downloadStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    {
                        const int DownloadBufferSize = 1024 * 1024;
                        await downloadStream.CopyToAsync(stream, DownloadBufferSize, cancellationToken).ConfigureAwait(false);
                    }

                    return mediaType;
                }
            }
        }

        private readonly string cameraName;
        private readonly CancellationToken cancellationToken;
        private readonly HttpClient defaultHttpClient;
    }
}