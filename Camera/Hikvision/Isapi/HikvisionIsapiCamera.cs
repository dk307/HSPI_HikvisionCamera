using Hspi.Utils;
using Nito.AsyncEx;
using NullGuard;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;

using static System.FormattableString;

namespace Hspi.Camera.Hikvision.Isapi
{
    // Based on
    // https://down.dipol.com.pl/Cctv/-Hikvision-/isapi/HIKVISION%20ISAPI_2.6-IPMD%20Service.pdf

    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class HikvisionIsapiCamera : CameraBase
    {
        public HikvisionIsapiCamera(CameraSettings cameraSettings,
                                    CancellationToken shutdown) :
            base(shutdown)
        {
            CameraSettings = cameraSettings;
            propertiesGroups = CreatePropertyGroup(cameraSettings.PeriodicFetchedCameraProperties);

            handler = CreateHttpHandler();
            defaultHttpClient = CreateHttpClient();
            downloadHelper = new DownloadHelper(CameraSettings.Name, defaultHttpClient);
            alarmProcessingHelper = new AlarmProcessingHelper(CameraSettings.Name,
                                                              CameraSettings.AlarmCancelInterval,
                                                              CloneAlarmInfo,
                                                              Enqueue,
                                                              Token);

            Utils.TaskHelper.StartAsyncWithErrorChecking(Invariant($"{cameraSettings.Name} Alarm Steam"), StartAlarmStream, Token);
            Utils.TaskHelper.StartAsyncWithErrorChecking(Invariant($"{cameraSettings.Name} Fetch Properties"), FetchProperties, Token);
            Utils.TaskHelper.StartAsyncWithErrorChecking(Invariant($"{cameraSettings.Name} Download Videos"), DownloadVideos, Token);
        }

        public CameraSettings CameraSettings { get; }

        public override Task DownloadContinuousSnapshots(TimeSpan totalTimeSpan, TimeSpan interval)
        {
            var helper = new HikvisionIsapiSnapshotsHelper(this, Token);
            return helper.DownloadContinuousSnapshots(totalTimeSpan, interval);
        }

        public async Task<string> DownloadSnapshot(int channel)
        {
            if (!Directory.Exists(CameraSettings.SnapshotDownloadDirectory))
            {
                throw new DirectoryNotFoundException("Directory Not Found:" + CameraSettings.SnapshotDownloadDirectory);
            }

            string path = Path.Combine(CameraSettings.SnapshotDownloadDirectory,
                                       DateTimeOffset.Now.ToString("yyyy-MM-dd--HH-mm-ss-ff", CultureInfo.InvariantCulture));
            Uri uri = CreateUri(Invariant($"/ISAPI/Streaming/channels/{channel}/picture"));

            return await downloadHelper.DownloadToFile(Token, path, uri, HttpMethod.Get, null, null).ConfigureAwait(false);
        }

        public async Task<IList<RecordedVideo>> GetRecording(CancellationToken token,
                                                             int maxResults,
                                                             int searchResultPostion,
                                                             DateTimeOffset? filterStartTime,
                                                             DateTimeOffset? filterEndTime)
        {
            StringBuilder stringBuilder = new StringBuilder();
            XmlWriterSettings settings = new XmlWriterSettings()
            {
                OmitXmlDeclaration = true,
            };

            using (XmlWriter writer = XmlWriter.Create(stringBuilder, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("CMSearchDescription");
                writer.WriteElementString("searchID", Guid.NewGuid().ToString());

                writer.WriteStartElement("maxResults");
                writer.WriteValue(maxResults);
                writer.WriteEndElement();

                writer.WriteStartElement("searchResultPostion");
                writer.WriteValue(searchResultPostion);
                writer.WriteEndElement();

                if (filterStartTime.HasValue && filterEndTime.HasValue)
                {
                    writer.WriteStartElement("timeSpanList");
                    writer.WriteStartElement("timeSpan");
                    writer.WriteElementString("startTime", ToISO8601(filterStartTime.Value));
                    writer.WriteElementString("endTime", ToISO8601(filterEndTime.Value));
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }

            List<RecordedVideo> videos = new List<RecordedVideo>();
            Uri uri = CreateUri(@"/ISAPI/ContentMgmt/search/");
            using (var request = new HttpRequestMessage(HttpMethod.Post, uri))
            {
                using (var response = await Send(token, request, stringBuilder.ToString()).ConfigureAwait(false))
                {
                    var xmlDocument = await GetXMLDocumentFromResponse(response).ConfigureAwait(false);

                    XPathNavigator rootNavigator = xmlDocument.DocumentElement.CreateNavigator();
                    XPathNodeIterator childNodeIter = rootNavigator.Select(xPathForSelectingVideos.Path);

                    if (childNodeIter != null)
                    {
                        while (childNodeIter.MoveNext() && !Token.IsCancellationRequested)
                        {
                            var videoNode = childNodeIter.Current;

                            var trackId = videoNode.SelectSingleNode(SelectTrackIdXPath.Path)?.ValueAsInt;
                            var startTime = videoNode.SelectSingleNode(StartTimeXPath.Path)?.Value;
                            var endTime = videoNode.SelectSingleNode(EndTimeXPath.Path)?.Value;
                            var playbackUri = videoNode.SelectSingleNode(PlaybackURIXPath.Path)?.Value;

                            if (trackId.HasValue &&
                                !string.IsNullOrWhiteSpace(startTime) &&
                                !string.IsNullOrWhiteSpace(endTime) &&
                                !string.IsNullOrWhiteSpace(playbackUri))
                            {
                                var uriBuilder = new UriBuilder(playbackUri);

                                videos.Add(new RecordedVideo(trackId.Value,
                                                             uriBuilder.Uri,
                                                             FromISO8601(startTime),
                                                             FromISO8601(endTime)));
                            }
                        }
                    }
                }
            }
            return videos;
        }

        public async Task Put(CameraProperty cameraProperty, string value)
        {
            Uri uri = CreateUri(cameraProperty.UrlPath);

            using (var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri))
            {
                using (var response = await downloadHelper.SendToCamera(Token, httpRequestMessage).ConfigureAwait(false))
                {
                    var xmlDocument = await GetXMLDocumentFromResponse(response).ConfigureAwait(false);

                    XPathNavigator rootNavigator = xmlDocument.DocumentElement.CreateNavigator();

                    XPathNodeIterator childNodeIter = rootNavigator.Select(cameraProperty.XPathForGet.Path);
                    if (childNodeIter != null && childNodeIter.MoveNext())
                    {
                        childNodeIter.Current.SetValue(value);
                    }
                    else
                    {
                        throw new Exception("Element not found in response");
                    }

                    using (HttpRequestMessage httpRequestMessage1 = new HttpRequestMessage(HttpMethod.Put, uri))
                    {
                        await downloadHelper.SendToCamera(Token, httpRequestMessage1, xmlDocument.OuterXml).ConfigureAwait(false);
                    }
                    await FetchPropertiesForCommonUri(uri, new CameraProperty[] { cameraProperty }).ConfigureAwait(false);
                }
            }
        }

        public async Task Reboot()
        {
            Trace.TraceInformation(Invariant($"[{CameraSettings.Name}]Rebooting ..."));

            Uri uri = CreateUri(@"/ISAPI/System/reboot");
            using (var httpRequestMessage = new HttpRequestMessage(HttpMethod.Put, uri))
            {
                await Send(Token, httpRequestMessage).ConfigureAwait(false);
            }
        }

        public async Task RefreshProperties()
        {
            Trace.WriteLine(Invariant($"[{CameraSettings.Name}]Refreshing Properties"));
            await FetchPropertiesImpl().ConfigureAwait(false);
        }

        public async Task RequestKeyFrame(int channel)
        {
            Trace.WriteLine(Invariant($"[{CameraSettings.Name}]Refreshing Key Frame for channel {channel}"));
            Uri uri = CreateUri(Invariant($"/ISAPI/Streaming/channels/{channel}/requestKeyFrame"));
            using (var httpRequestMessage = new HttpRequestMessage(HttpMethod.Put, uri))
            {
                await Send(Token, httpRequestMessage).ConfigureAwait(false);
            }
        }

        public async Task StartRecording(int track)
        {
            Trace.TraceInformation(Invariant($"[{CameraSettings.Name}]Start Recording for track {track}"));
            Uri uri = CreateUri(Invariant($"/ISAPI/ContentMgmt/record/control/manual/start/tracks/{track}"));
            using (var httpRequestMessage = new HttpRequestMessage(HttpMethod.Put, uri))
            {
                await Send(Token, httpRequestMessage).ConfigureAwait(false);
            }
        }

        public void StartVideoDownload()
        {
            Trace.TraceInformation(Invariant($"[{CameraSettings.Name}]Start Video Download"));
            downloadEvent.Set();
        }

        public void CancelVideoDownload()
        {
            Trace.TraceInformation(Invariant($"[{CameraSettings.Name}]Cancel Video Download"));
            downloadTokenSource?.Cancel();
        }

        public async Task StopRecording(int track)
        {
            Trace.TraceInformation(Invariant($"[{CameraSettings.Name}]Stop Recording for track {track}"));
            Uri uri = CreateUri(Invariant($"/ISAPI/ContentMgmt/record/control/manual/stop/tracks/{track}"));
            using (var httpRequestMessage = new HttpRequestMessage(HttpMethod.Put, uri))
            {
                await Send(Token, httpRequestMessage).ConfigureAwait(false);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                defaultHttpClient.Dispose();
                handler.Dispose();
            }
            base.Dispose(disposing);
        }

        private static Dictionary<string, List<CameraProperty>>
                    CreatePropertyGroup(IReadOnlyDictionary<string, CameraProperty> cameraProperties)
        {
            var groups = new Dictionary<string, List<CameraProperty>>(cameraProperties.Count, StringComparer.OrdinalIgnoreCase);

            foreach (var cameraProperty in cameraProperties)
            {
                if (!groups.ContainsKey(cameraProperty.Value.UrlPath))
                {
                    groups.Add(cameraProperty.Value.UrlPath, new List<CameraProperty>());
                }
                groups[cameraProperty.Value.UrlPath].Add(cameraProperty.Value);
            }

            return groups;
        }

        private static DateTimeOffset FromISO8601(string startTime)
        {
            return DateTimeOffset.Parse(startTime, null, DateTimeStyles.RoundtripKind);
        }

        private static async Task<XmlDocument> GetXMLDocumentFromResponse(HttpResponseMessage response)
        {
            HttpContent content = response.Content;
            string responseString = await content.ReadAsStringAsync().ConfigureAwait(false);

            XmlDocument xmlDocument = new XmlDocument()
            {
                XmlResolver = null,
            };

            using (var stringReader = new StringReader(responseString))
            {
                using (XmlReader reader = XmlReader.Create(stringReader, new XmlReaderSettings() { XmlResolver = null }))
                {
                    xmlDocument.Load(reader);
                    return xmlDocument;
                }
            }
        }

        private static string ToISO8601(DateTimeOffset filterStartTime)
        {
            return filterStartTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        }

        private HttpClient CreateHttpClient()
        {
            var httpClient = new HttpClient(handler, false)
            {
                Timeout = TimeSpan.FromSeconds(120)
            };

            return httpClient;
        }

        private HttpMessageHandler CreateHttpHandler()
        {
            var credCache = new CredentialCache();
            var credentials = new NetworkCredential(CameraSettings.Login, CameraSettings.Password);
            credCache.Add(new Uri(CameraSettings.CameraHost), "Digest", credentials);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // This is used because it supports GET with a body, HttpClientHandler doesn't
                var winHttpHandler = new WinHttpHandler
                {
                    ServerCredentials = credCache,
                    MaxConnectionsPerServer = 4,
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                };

                return winHttpHandler;
            }
            else
            {
                var httpClientHandler = new HttpClientHandler
                {
                    Credentials = credCache,
                    MaxConnectionsPerServer = 4,
                };

                if (httpClientHandler.SupportsAutomaticDecompression)
                {
                    httpClientHandler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                }
                return httpClientHandler;
            }
        }

        private Uri CreateUri(string path)
        {
            var uriBuilder = new UriBuilder(CameraSettings.CameraHost)
            {
                Path = path
            };

            var uri = uriBuilder.Uri;
            return uri;
        }

        private async Task DownloadRecordedVideo(CancellationToken token,
                                                 RecordedVideo video,
                                                 string path)
        {
            Trace.WriteLine(Invariant($"[{CameraSettings.Name}]Downloading {video.Name}"));

            Uri uri = CreateUri(@"/ISAPI/ContentMgmt/download");

            StringBuilder stringBuilder = new StringBuilder();
            XmlWriterSettings settings = new XmlWriterSettings()
            {
                OmitXmlDeclaration = true,
            };

            using (XmlWriter writer = XmlWriter.Create(stringBuilder, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("downloadRequest");
                writer.WriteElementString("playbackURI", video.RstpUri.ToString());
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }

            await downloadHelper.DownloadToFile(token, path, uri, HttpMethod.Get, "mp4", stringBuilder.ToString()).ConfigureAwait(false);
            Trace.WriteLine(Invariant($"[{CameraSettings.Name}]Finished downloading {video.Name}"));
        }

        private async Task DownloadVideos()
        {
            while (!Token.IsCancellationRequested)
            {
                await downloadEvent.WaitAsync(Token).ConfigureAwait(false);

                downloadTokenSource = CancellationTokenSource.CreateLinkedTokenSource(Token);

                var downloadToken = downloadTokenSource.Token;
                try
                {
                    //oldest first
                    List<RecordedVideo> list = new List<RecordedVideo>();
                    do
                    {
                        downloadToken.ThrowIfCancellationRequested();
                        IList<RecordedVideo> collection = await GetRecording(downloadToken, 40, list.Count, null, null).ConfigureAwait(false);
                        if (collection.Count == 0)
                        {
                            break;
                        }
                        list.AddRange(collection);
                    } while (!downloadToken.IsCancellationRequested);

                    Trace.WriteLine(Invariant($"[{CameraSettings.Name}]Found {list.Count} recorded files on camera"));

                    var videos = list.OrderBy(x => x.StartTime);

                    foreach (var video in videos)
                    {
                        try
                        {
                            downloadToken.ThrowIfCancellationRequested();

                            var dayDirectory = video.StartTime.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                            string fileDirectory = Path.Combine(CameraSettings.VideoDownloadDirectory, dayDirectory);
                            string fileName = Path.Combine(fileDirectory, video.Name + ".mp4");

                            var fileInfo = new FileInfo(fileName);

                            if (!fileInfo.Exists)
                            {
                                Directory.CreateDirectory(fileDirectory);
                                await DownloadRecordedVideo(downloadToken, video, fileName).ConfigureAwait(false);
                                File.SetCreationTimeUtc(fileName, video.StartTime.UtcDateTime);
                                File.SetLastWriteTimeUtc(fileName, video.EndTime.UtcDateTime);
                            }
                        }
                        catch (Exception ex)
                        {
                            if (ex.IsCancelException() && downloadToken.IsCancellationRequested)
                            {
                                throw;
                            }

                            Trace.TraceError(Invariant($"[{CameraSettings.Name}]Failed to download {video.RstpUri} for {CameraSettings.CameraHost} to {CameraSettings.VideoDownloadDirectory} with {ex.GetFullMessage()}."));
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (ex.IsCancelException())
                    {
                        if (Token.IsCancellationRequested)
                        {
                            throw;
                        }

                        Trace.TraceInformation(Invariant($"[{CameraSettings.Name}]Download videos cancelled"));
                    }
                    else
                    {
                        Trace.TraceError(Invariant($"[{CameraSettings.Name}]Failed to get download videos for {CameraSettings.CameraHost} with {ex.GetFullMessage()}."));
                    }
                }
            }
        }

        private OnOffCameraContruct CloneAlarmInfo(OnOffCameraContruct cameraContruct, bool state)
        {
            var alarmInfo = (AlarmInfo)cameraContruct;
            return new AlarmInfo(alarmInfo.AlarmType, alarmInfo.ChannelID, state);
        }

        private async Task Enqueue(OnOffCameraContruct alarm)
        {
            Trace.WriteLine(Invariant($"[{CameraSettings.Name}]Alarm:{alarm.Id} Active:{alarm.Active}"));
            await Updates.EnqueueAsync(alarm, Token).ConfigureAwait(false);
        }

        private async Task Enqueue(CameraProperty cameraInfo, [AllowNull] string value)
        {
            Trace.WriteLine(Invariant($"[{CameraSettings.Name}]Property:{cameraInfo.Name} Value:{value ?? string.Empty}"));
            await Updates.EnqueueAsync(new CameraPropertyInfo(cameraInfo, value), Token).ConfigureAwait(false);
        }

        private async Task EnqueueAlarmStreamConnectedInfo(bool connected)
        {
            var alarmStreamConnectedInfo = new AlarmStreamConnectedInfo(connected);
            Trace.WriteLine(Invariant($"[{CameraSettings.Name}]Alarm Stream Connected:{alarmStreamConnectedInfo.Active}"));
            await Updates.EnqueueAsync(alarmStreamConnectedInfo, Token).ConfigureAwait(false);
        }

        private async Task FetchProperties()
        {
            while (!Token.IsCancellationRequested)
            {
                try
                {
                    await FetchPropertiesImpl().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (ex.IsCancelException() && Token.IsCancellationRequested)
                    {
                        throw;
                    }

                    Trace.TraceError(Invariant($"[{CameraSettings.Name}]Failed to get properties for {CameraSettings.CameraHost} with {ex}."));
                }

                await Task.Delay(CameraSettings.CameraPropertiesRefreshInterval, Token).ConfigureAwait(false);
            }
        }

        private async Task FetchPropertiesForCommonUri(Uri uri, IList<CameraProperty> cameraInfos)
        {
            using (var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri))
            {
                HttpResponseMessage response = await downloadHelper.SendToCamera(Token, httpRequestMessage).ConfigureAwait(false);
                response = response.EnsureSuccessStatusCode();

                XmlDocument xmlDocument = await GetXMLDocumentFromResponse(response).ConfigureAwait(false);

                XPathNavigator rootNavigator = xmlDocument.DocumentElement.CreateNavigator();

                var cameraInfoCopy = new List<CameraProperty>(cameraInfos);
                foreach (var cameraInfo in cameraInfos)
                {
                    XPathNodeIterator childNodeIter = rootNavigator.Select(cameraInfo.XPathForGet.Path);
                    if (childNodeIter != null && childNodeIter.MoveNext())
                    {
                        string value = childNodeIter.Current.Value.ToString(CultureInfo.InvariantCulture);
                        await Enqueue(cameraInfo, value).ConfigureAwait(false);
                        cameraInfoCopy.Remove(cameraInfo);
                    };
                }

                foreach (var cameraInfo in cameraInfoCopy)
                {
                    await Enqueue(cameraInfo, null).ConfigureAwait(false);
                }
            }
        }

        private async Task FetchPropertiesImpl()
        {
            foreach (var item in propertiesGroups)
            {
                var commonUri = CreateUri(item.Key);
                List<CameraProperty> cameraInfos = item.Value;
                await FetchPropertiesForCommonUri(commonUri, cameraInfos).ConfigureAwait(false);
            }
        }

        private async Task ProcessAlarmEvent(List<string> lines)
        {
            string alarmType = null;
            bool? enabled = null;
            int? channelId = null;

            foreach (var line in lines)
            {
                switch (line)
                {
                    case "<eventState>inactive</eventState>":
                        enabled = false;
                        break;

                    case "<eventState>active</eventState>":
                        enabled = true;
                        break;

                    default:
                        var match = eventTypeRegex.Match(line);
                        if (match.Success)
                        {
                            alarmType = string.Intern(match.Groups[1].Value);
                            break;
                        }
                        match = channelTypeRegex.Match(line);
                        if (match.Success)
                        {
                            if (int.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out int result))
                            {
                                channelId = result;
                            }
                        }
                        break;
                }
            }

            if (!string.IsNullOrWhiteSpace(alarmType) && enabled.HasValue && channelId.HasValue)
            {
                var alarm = new AlarmInfo(alarmType, channelId.Value, enabled.Value);
                await alarmProcessingHelper.ProcessNewAlarm(alarm).ConfigureAwait(false);
            }
        }

        private async Task<HttpResponseMessage> Send(CancellationToken token,
                                                     HttpRequestMessage httpRequestMessage,
                                                     string content = null,
                                                     HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
        {
            return await downloadHelper.SendToCamera(token, httpRequestMessage, content, completionOption).ConfigureAwait(false);
        }

        private async Task StartAlarmStream()
        {
            Uri uri = CreateUri(@"ISAPI/Event/notification/alertStream");
            await EnqueueAlarmStreamConnectedInfo(false).ConfigureAwait(false);
            while (!Token.IsCancellationRequested)
            {
                HttpClient client = null;
                try
                {
                    client = CreateHttpClient();  // create new one
                    {
                        Trace.WriteLine(Invariant($"[{CameraSettings.Name}]Listening to alarm stream"));
                        client.Timeout = Timeout.InfiniteTimeSpan;
                        using (var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri))
                        {
                            using (var response = await downloadHelper.SendToCamera(Token,
                                                                                    httpRequestMessage,
                                                                                    client: client,
                                                                                    completionOption: HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                            {
                                await EnqueueAlarmStreamConnectedInfo(true).ConfigureAwait(false);

                                using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                                {
                                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                                    {
                                        List<string> builder = new List<string>(20);
                                        while (true) //(!reader.EndOfStream)
                                        {
                                            var readTask = reader.ReadLineAsync();
                                            var delayTask = Task.Delay(alarmStreamThreshold, Token);

                                            var completedTask = await Task.WhenAny(delayTask, readTask).ConfigureAwait(false);
                                            if (completedTask == readTask)
                                            {
                                                string line = readTask.Result;

                                                if (line == null)
                                                {
                                                    Trace.TraceWarning(Invariant($"[{CameraSettings.Name}]Alarm Stream for {CameraSettings.CameraHost} disconnected. Restarting it."));
                                                    break;
                                                }

                                                if (line == "--boundary")
                                                {
                                                    await ProcessAlarmEvent(builder).ConfigureAwait(false);
                                                    builder.Clear();
                                                }
                                                else
                                                {
                                                    if (line.StartsWith("<eventType>", StringComparison.InvariantCultureIgnoreCase) ||
                                                        line.StartsWith("<eventState>", StringComparison.InvariantCulture))
                                                    {
                                                        builder.Add(line);
                                                    }
                                                    else if (line.StartsWith("<channelID>", StringComparison.InvariantCulture))
                                                    {
                                                        builder.Add(line);
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                Token.ThrowIfCancellationRequested();
                                                throw new TimeoutException(Invariant($"Did not receive input from Alarm for {alarmStreamThreshold}."));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (ex.IsCancelException() && Token.IsCancellationRequested)
                    {
                        throw;
                    }

                    await EnqueueAlarmStreamConnectedInfo(false).ConfigureAwait(false);

                    Trace.TraceWarning(Invariant($"[{CameraSettings.Name}]Alarm Stream for {CameraSettings.CameraHost} failed with {ex}. Restarting it."));
                    if (!Token.IsCancellationRequested)
                    {
                        await Task.Delay(1000, Token).ConfigureAwait(false);
                    }
                }
                finally
                {
                    client?.Dispose();
                }
            }
        }

        public const int Track1 = 101;

        public const int Track2 = 201;

        private static readonly Regex channelTypeRegex = new Regex(@"<channelID>(.*?)<\/channelID>",
                                                                 RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly XmlPathData EndTimeXPath = new XmlPathData("*[local-name()='timeSpan']/*[local-name()='endTime']");

        private static readonly Regex eventTypeRegex = new Regex(@"<eventType>(.*?)<\/eventType>",
                                                                 RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly XmlPathData PlaybackURIXPath = new XmlPathData("*[local-name()='mediaSegmentDescriptor']/*[local-name()='playbackURI']");

        private static readonly XmlPathData SelectTrackIdXPath = new XmlPathData("*[local-name()='trackID']");

        private static readonly XmlPathData StartTimeXPath = new XmlPathData("*[local-name()='timeSpan']/*[local-name()='startTime']");

        private static readonly XmlPathData xPathForSelectingVideos =
                                                    new XmlPathData(@"*[local-name()='matchList']/*");

#pragma warning disable CA2213 // Disposable fields should be disposed
        private CancellationTokenSource downloadTokenSource;
#pragma warning restore CA2213 // Disposable fields should be disposed

        private readonly AlarmProcessingHelper alarmProcessingHelper;
        private readonly TimeSpan alarmStreamThreshold = TimeSpan.FromSeconds(120);

        private readonly HttpClient defaultHttpClient;
        private readonly AsyncAutoResetEvent downloadEvent = new AsyncAutoResetEvent();
        private readonly DownloadHelper downloadHelper;
        private readonly HttpMessageHandler handler;
        private readonly Dictionary<string, List<CameraProperty>> propertiesGroups;
    }
}