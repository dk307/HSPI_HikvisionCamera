using HeyRed.Mime;
using Nito.AsyncEx;
using Nito.AsyncEx.Synchronous;
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

namespace Hspi.Camera
{
    // Based on
    // https://down.dipol.com.pl/Cctv/-Hikvision-/isapi/HIKVISION%20ISAPI_2.6-IPMD%20Service.pdf

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling")]
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class HikvisionCamera : IDisposable
    {
        public HikvisionCamera(CameraSettings cameraSettings,
                               CancellationToken shutdown)
        {
            CameraSettings = cameraSettings;
            sourceToken = CancellationTokenSource.CreateLinkedTokenSource(shutdown);
            propertiesGroups = CreatePropertyGroup(cameraSettings.PeriodicFetchedCameraProperties);

            defaultHttpClient = CreateHttpClient();

            alarmStreamTask = Task.Factory.StartNew(StartAlarmStream, Token,
                                                   TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                                                   TaskScheduler.Default).WaitAndUnwrapException(Token);
            alarmsBackgroundProcessingTask = Task.Factory.StartNew(ResetBackAlarmsLoop, Token,
                                                    TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                                                    TaskScheduler.Default).WaitAndUnwrapException(Token);
            fetchPropertiesTask = Task.Factory.StartNew(FetchProperties, Token,
                                                    TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                                                    TaskScheduler.Default).WaitAndUnwrapException(Token);
            videoDownloadTask = Task.Factory.StartNew(DownloadVideos, Token,
                                                    TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                                                    TaskScheduler.Default).WaitAndUnwrapException(Token);
        }

        public CameraSettings CameraSettings { get; }
        private CancellationToken Token => sourceToken.Token;
        public AsyncProducerConsumerQueue<ICameraContruct> Updates { get; } = new AsyncProducerConsumerQueue<ICameraContruct>();

        public async Task DownloadRecordedVideo(RecordedVideo video, string path)
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

            await DownloadToFile(path, uri, "mp4", HttpMethod.Get, stringBuilder.ToString()).ConfigureAwait(false);
            Trace.WriteLine(Invariant($"[{CameraSettings.Name}]Finished downloading {video.Name}"));
        }

        public async Task<string> DownloadSnapshot(int channel)
        {
            string path = Path.Combine(CameraSettings.SnapshotDownloadDirectory, DateTimeOffset.Now.ToString("yyyy-MM-dd--HH-mm-ss-ff"));
            Uri uri = CreateUri(Invariant($"/ISAPI/Streaming/channels/{channel}/picture"));
            Trace.WriteLine(Invariant($"[{CameraSettings.Name}]Taking snapshot at {path}"));

            return await DownloadToFile(path, uri, null, HttpMethod.Get, null).ConfigureAwait(false);
        }

        public async Task<IList<RecordedVideo>> GetRecording(int maxResults, int searchResultPostion, DateTimeOffset? filterStartTime, DateTimeOffset? filterEndTime)
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
            using (var response = await Send(HttpMethod.Post, uri, stringBuilder.ToString()).ConfigureAwait(false))
            {
                var xmlDocument = await GetXMLDocumentFromResponse(response).ConfigureAwait(false);

                XPathNavigator rootNavigator = xmlDocument.DocumentElement.CreateNavigator();
                XPathNodeIterator childNodeIter = rootNavigator.Select(xPathForSelectingVideos.Path);

                if (childNodeIter != null)
                {
                    while (childNodeIter.MoveNext())
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
            return videos;
        }

        public async Task Put(CameraProperty cameraProperty, string value)
        {
            Uri uri = CreateUri(cameraProperty.UrlPath);

            using (var response = await SendToClient(HttpMethod.Get, uri).ConfigureAwait(false))
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

                await SendToClient(HttpMethod.Put, uri, xmlDocument.OuterXml).ConfigureAwait(false);
                await FetchPropertiesForCommonUri(uri, new CameraProperty[] { cameraProperty }).ConfigureAwait(false);
            }
        }

        public async Task Reboot()
        {
            Trace.TraceInformation(Invariant($"[{CameraSettings.Name}]Rebooting ..."));

            Uri uri = CreateUri(@"/ISAPI/System/reboot");
            await Send(HttpMethod.Put, uri).ConfigureAwait(false);
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
            await Send(HttpMethod.Put, uri).ConfigureAwait(false);
        }

        public async Task StartRecording(int track)
        {
            Trace.TraceInformation(Invariant($"[{CameraSettings.Name}]Start Recording for track {track}"));
            Uri uri = CreateUri(Invariant($"/ISAPI/ContentMgmt/record/control/manual/start/tracks/{track}"));
            await Send(HttpMethod.Put, uri).ConfigureAwait(false);
        }

        public void StartVideoDownload()
        {
            Trace.TraceInformation(Invariant($"[{CameraSettings.Name}]Start Video Download"));
            downloadEvent.Set();
        }

        public async Task StopRecording(int track)
        {
            Trace.TraceInformation(Invariant($"[{CameraSettings.Name}]Stop Recording for track {track}"));
            Uri uri = CreateUri(Invariant($"/ISAPI/ContentMgmt/record/control/manual/stop/tracks/{track}"));
            await Send(HttpMethod.Put, uri).ConfigureAwait(false);
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

            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(responseString);
            return xmlDocument;
        }

        private static string ToISO8601(DateTimeOffset filterStartTime)
        {
            return filterStartTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        }

        private async Task AlarmsBackgroundProcessing()
        {
            using (var sync = await alarmTimersLock.LockAsync(Token).ConfigureAwait(false))
            {
                foreach (var pair in alarmsData)
                {
                    var alarmData = pair.Value;
                    if (alarmData.state)
                    {
                        if (alarmData.lastReceived.Elapsed >= CameraSettings.AlarmCancelInterval)
                        {
                            alarmData.state = false;
                            alarmData.lastReceived.Reset();
                            alarmData.lastUpdated.Reset();
                            var alarm = new AlarmInfo(pair.Key, alarmData.state);
                            await Enqueue(alarm).ConfigureAwait(false);
                        }
                    }

                    if (alarmData.state)
                    {
                        if (alarmData.lastUpdated.Elapsed >= CameraSettings.AlarmCancelInterval)
                        {
                            alarmData.lastUpdated.Restart();
                            var alarm = new AlarmInfo(pair.Key, alarmData.state);
                            await Enqueue(alarm).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        private HttpClient CreateHttpClient()
        {
            var credCache = new CredentialCache();
            var credentials = new NetworkCredential(CameraSettings.Login, CameraSettings.Password);
            credCache.Add(new Uri(CameraSettings.CameraHost), "Digest", credentials);

            HttpMessageHandler handler = null;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var winHttpHandler = new WinHttpHandler
                {
                    ServerCredentials = credCache,
                };
                handler = winHttpHandler;
            }
            else
            {
                var httpClientHandler = new HttpClientHandler
                {
                    Credentials = credCache,
                };

                if (httpClientHandler.SupportsAutomaticDecompression)
                {
                    httpClientHandler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                }
                handler = httpClientHandler;
            }

            var httpClient = new HttpClient(handler, true);
            httpClient.Timeout = TimeSpan.FromSeconds(1);
            return httpClient;
        }

        private Uri CreateUri(string path)
        {
            var uriBuilder = new UriBuilder(CameraSettings.CameraHost);
            uriBuilder.Path = path;

            var uri = uriBuilder.Uri;
            return uri;
        }

        private async Task<string> DownloadToFile(string path, Uri uri, [AllowNull]string extension, HttpMethod httpMethod, [AllowNull]string data)
        {
            using (var response = await SendToClient(httpMethod, uri, data,
                                            HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
            {
                string mediaType = response.Content.Headers?.ContentType?.MediaType;

                if ((mediaType == null))
                {
                    throw new Exception(Invariant($"[{CameraSettings.Name}]Invalid Data for {uri} :{mediaType ?? string.Empty}"));
                }

                string fileExtension = extension ?? MimeTypesMap.GetExtension(mediaType);

                string tempPath = path + ".tmp";
                using (var downloadStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                {
                    using (var fileStream = new FileStream(tempPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
                    {
                        await downloadStream.CopyToAsync(fileStream, 81920, Token).ConfigureAwait(false);
                    }
                }

                if (File.Exists(path))
                {
                    File.Move(path, path + ".old");
                    File.Delete(path + ".old");
                }

                string destFileName = Path.ChangeExtension(path, fileExtension);
                File.Move(tempPath, destFileName);
                return destFileName;
            }
        }

        private async Task DownloadVideos()
        {
            while (!Token.IsCancellationRequested)
            {
                await downloadEvent.WaitAsync(Token).ConfigureAwait(false);

                try
                {
                    //oldest first
                    List<RecordedVideo> list = new List<RecordedVideo>();
                    do
                    {
                        Token.ThrowIfCancellationRequested();
                        IList<RecordedVideo> collection = await GetRecording(40, list.Count, null, null).ConfigureAwait(false);
                        if (collection.Count == 0)
                        {
                            break;
                        }
                        list.AddRange(collection);
                    } while (!Token.IsCancellationRequested);

                    Trace.WriteLine(Invariant($"[{CameraSettings.Name}]Found {list.Count} recorded files on camera"));

                    var videos = list.OrderBy(x => x.StartTime);

                    foreach (var video in videos)
                    {
                        try
                        {
                            var dayDirectory = video.StartTime.ToLocalTime().ToString("yyyy-MM-dd");
                            string fileDirectory = Path.Combine(CameraSettings.VideoDownloadDirectory, dayDirectory);
                            string fileName = Path.Combine(fileDirectory, video.Name + ".mp4");
                            Directory.CreateDirectory(fileDirectory);

                            var fileInfo = new FileInfo(fileName);

                            if (!fileInfo.Exists)
                            {
                                await DownloadRecordedVideo(video, fileName).ConfigureAwait(false);
                                File.SetCreationTimeUtc(fileName, video.StartTime.UtcDateTime);
                                File.SetLastWriteTimeUtc(fileName, video.EndTime.UtcDateTime);
                            }
                        }
                        catch (Exception ex)
                        {
                            if (ex.IsCancelException() && Token.IsCancellationRequested)
                            {
                                throw;
                            }

                            Trace.TraceError(Invariant($"[{CameraSettings.Name}]Failed to get download {video.RstpUri} for {CameraSettings.CameraHost} with {ex.GetFullMessage()}."));
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (ex.IsCancelException() && Token.IsCancellationRequested)
                    {
                        throw;
                    }

                    Trace.TraceError(Invariant($"[{CameraSettings.Name}]Failed to get download  videos for {CameraSettings.CameraHost} with {ex.GetFullMessage()}."));
                }
            }
        }

        private async Task Enqueue(AlarmInfo alarm)
        {
            Trace.WriteLine(Invariant($"[{CameraSettings.Name}]Alarm:{alarm.AlarmType} Enabled:{alarm.Active}"));
            await Updates.EnqueueAsync(alarm, Token).ConfigureAwait(false);
        }

        private async Task Enqueue(CameraProperty cameraInfo, [AllowNull]string value)
        {
            Trace.WriteLine(Invariant($"[{CameraSettings.Name}]Property:{cameraInfo.Name} Value:{value ?? string.Empty}"));
            await Updates.EnqueueAsync(new CameraPropertyInfo(cameraInfo, value), Token).ConfigureAwait(false);
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
            HttpResponseMessage response = await SendToClient(HttpMethod.Get, uri).ConfigureAwait(false);
            response = response.EnsureSuccessStatusCode();

            XmlDocument xmlDocument = await GetXMLDocumentFromResponse(response);

            XPathNavigator rootNavigator = xmlDocument.DocumentElement.CreateNavigator();

            var cameraInfoCopy = new List<CameraProperty>(cameraInfos);
            foreach (var cameraInfo in cameraInfos)
            {
                XPathNodeIterator childNodeIter = rootNavigator.Select(cameraInfo.XPathForGet.Path);
                if (childNodeIter != null && childNodeIter.MoveNext())
                {
                    string value = childNodeIter.Current.Value.ToString();
                    await Enqueue(cameraInfo, value).ConfigureAwait(false);
                    cameraInfoCopy.Remove(cameraInfo);
                };
            }

            foreach (var cameraInfo in cameraInfoCopy)
            {
                await Enqueue(cameraInfo, null).ConfigureAwait(false);
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
                        Match match = eventTypeRegex.Match(line);
                        if (match.Success)
                        {
                            alarmType = string.Intern(match.Groups[1].Value);
                        }
                        break;
                }
            }

            if (!string.IsNullOrWhiteSpace(alarmType) && enabled.HasValue)
            {
                var alarm = new AlarmInfo(alarmType, enabled.Value);
                if (enabled.Value)
                {
                    if (await UpdateForAlarm(alarmType).ConfigureAwait(false))
                    {
                        await Enqueue(alarm).ConfigureAwait(false);
                    }
                }
            }
        }

        private async Task ResetBackAlarmsLoop()
        {
            while (!Token.IsCancellationRequested)
            {
                await Task.Delay(1000, Token).ConfigureAwait(false);
                await AlarmsBackgroundProcessing().ConfigureAwait(false);
            }
        }

        private async Task<HttpResponseMessage> Send(HttpMethod method,
                                                            Uri uri,
                                                            string content = null,
                                                            HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
        {
            return await SendToClient(method, uri, content, completionOption).ConfigureAwait(false);
        }

        private async Task<HttpResponseMessage> SendToClient(HttpMethod method,
                                                            Uri uri,
                                                            string content = null,
                                                            HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead,
                                                            HttpClient client = null)
        {
            var httpRequestMessage = new HttpRequestMessage(method, uri);

            if (content != null)
            {
                httpRequestMessage.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
            }

            var response = await (client ?? defaultHttpClient).SendAsync(httpRequestMessage, completionOption, Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string failureContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(Invariant($"Request failed with {response.StatusCode}:{response.ReasonPhrase} to {uri} with {failureContent}"));
            }

            return response;
        }

        private async Task StartAlarmStream()
        {
            while (!Token.IsCancellationRequested)
            {
                try
                {
                    Uri uri = CreateUri(@"ISAPI/Event/notification/alertStream");

                    using (var client = CreateHttpClient())  // create new one
                    {
                        Trace.WriteLine(Invariant($"[{CameraSettings.Name}]Listening to alarm stream"));
                        client.Timeout = Timeout.InfiniteTimeSpan;
                        using (var response = await SendToClient(HttpMethod.Get,
                                                         uri,
                                                         client: client,
                                                         completionOption: HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                        {
                            using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                            {
                                using (var reader = new StreamReader(stream, Encoding.UTF8))
                                {
                                    List<string> builder = new List<string>();
                                    while (true) //(!reader.EndOfStream)
                                    {
                                        var readTask = reader.ReadLineAsync();

                                        var completedTask = await Task.WhenAny(readTask, Task.Delay(alarmStreamThreshold, Token)).ConfigureAwait(false);
                                        if (completedTask == readTask)
                                        {
                                            string line = await readTask.ConfigureAwait(false);

                                            if (line == "--boundary")
                                            {
                                                await ProcessAlarmEvent(builder).ConfigureAwait(false);
                                                builder.Clear();
                                            }
                                            else
                                            {
                                                if (line.StartsWith("<eventType>") ||
                                                    line.StartsWith("<eventState>"))
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
                catch (Exception ex)
                {
                    if (ex.IsCancelException() && Token.IsCancellationRequested)
                    {
                        throw;
                    }

                    Trace.TraceError(Invariant($"[{CameraSettings.Name}]Alarm Stream for {CameraSettings.CameraHost} failed with {ex}. Restarting it."));
                    if (!Token.IsCancellationRequested)
                    {
                        await Task.Delay(1000, Token).ConfigureAwait(false);
                    }
                }
            }
        }

        private async Task<bool> UpdateForAlarm(string alarmType)
        {
            bool sendNow = false;
            using (var sync = await alarmTimersLock.LockAsync(Token).ConfigureAwait(false))
            {
                if (!alarmsData.TryGetValue(alarmType, out var alarmData))
                {
                    alarmData = new AlarmData();
                    alarmsData.Add(alarmType, alarmData);
                    sendNow = true;
                }

                if (!sendNow)
                {
                    sendNow = !alarmData.state;
                }

                alarmData.state = true;
                alarmData.lastReceived.Restart();

                if (sendNow)
                {
                    alarmData.lastUpdated.Restart();
                }
            }

            return sendNow;
        }

        private class AlarmData
        {
            public Stopwatch lastReceived = new Stopwatch();
            public Stopwatch lastUpdated = new Stopwatch();
            public bool state = false;
        }

        public const int Track1 = 101;

        public const int Track2 = 201;

        private const string xmlUtf8Type = "application/xml";

        private static readonly XmlPathData EndTimeXPath = new XmlPathData("*[local-name()='timeSpan']/*[local-name()='endTime']");

        private readonly static Regex eventTypeRegex = new Regex(@"<eventType>(.*?)<\/eventType>",
                                                                 RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly XmlPathData PlaybackURIXPath = new XmlPathData("*[local-name()='mediaSegmentDescriptor']/*[local-name()='playbackURI']");

        private static readonly XmlPathData SelectTrackIdXPath = new XmlPathData("*[local-name()='trackID']");

        private static readonly XmlPathData StartTimeXPath = new XmlPathData("*[local-name()='timeSpan']/*[local-name()='startTime']");

        private readonly static XmlPathData xPathForSelectingVideos =
                                                    new XmlPathData(@"*[local-name()='matchList']/*");

        private readonly Task alarmsBackgroundProcessingTask;
        private readonly Dictionary<string, AlarmData> alarmsData = new Dictionary<string, AlarmData>();
        private readonly Task alarmStreamTask;
        private readonly TimeSpan alarmStreamThreshold = TimeSpan.FromSeconds(15);
        private readonly AsyncLock alarmTimersLock = new AsyncLock();
        private readonly HttpClient defaultHttpClient;
        private readonly AsyncAutoResetEvent downloadEvent = new AsyncAutoResetEvent();
        private readonly Task fetchPropertiesTask;
        private readonly Dictionary<string, List<CameraProperty>> propertiesGroups;
        private readonly CancellationTokenSource sourceToken;
        private readonly Task videoDownloadTask;

        #region IDisposable Support

        public void Dispose()
        {
            if (!disposedValue)
            {
                sourceToken.Cancel();
                alarmStreamTask?.WaitWithoutException();
                alarmsBackgroundProcessingTask?.WaitWithoutException();
                fetchPropertiesTask?.WaitWithoutException();
                videoDownloadTask?.WaitWithoutException();
                defaultHttpClient.Dispose();
                sourceToken.Dispose();
                disposedValue = true;
            }
        }

        private bool disposedValue = false; // To detect redundant calls

        #endregion IDisposable Support
    }
}