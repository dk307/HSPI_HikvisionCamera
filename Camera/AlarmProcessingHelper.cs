using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using static System.FormattableString;

namespace Hspi.Camera
{
    internal sealed class AlarmProcessingHelper
    {
        public AlarmProcessingHelper(string cameraName,
                                     TimeSpan alarmCancelInterval,
                                     Func<OnOffCameraContruct, bool, OnOffCameraContruct> cloneWithDifferentState,
                                     Func<OnOffCameraContruct, Task> enqueue,
                                     CancellationToken cancellationToken)
        {
            this.cameraName = cameraName;
            this.alarmCancelInterval = alarmCancelInterval;
            this.deviceStateUpdateInterval = alarmCancelInterval.Add(new TimeSpan(0, 0, 5));
            this.cloneWithDifferentState = cloneWithDifferentState;
            this.enqueue = enqueue;
            this.cancellationToken = cancellationToken;

            Utils.TaskHelper.StartAsyncWithErrorChecking(Invariant($"{cameraName} Alarm Reset Loop"),
                                                         ResetBackAlarmsLoop,
                                                         cancellationToken);
        }

        public async Task ProcessNewAlarm(OnOffCameraContruct alarm)
        {
            using (var sync = await alarmTimersLock.LockAsync(cancellationToken).ConfigureAwait(false))
            {
                bool sendNow = false;
                if (!alarmsData.TryGetValue(alarm.Id, out var alarmData))
                {
                    alarmData = new AlarmData(alarm);
                    alarmsData.Add(alarm.Id, alarmData);

                    //first time alarm, send
                    sendNow = true;
                }

                if (alarm.Active)
                {
                    // becoming active from inactive, send
                    if (!sendNow && !alarmData.state)
                    {
                        sendNow = true;
                    }

                    alarmData.state = true;
                    alarmData.CameraContruct = alarm;
                    alarmData.lastReceived.Restart();
                    Trace.WriteLine($"[{cameraName}]New alarm {alarmData.CameraContruct.Id}");
                }
                else
                {
                    // do not send alarm here as need to honor interval
                }


                if (sendNow)
                {
                    await SendAlarm(alarm, alarmData).ConfigureAwait(false);
                }
            }
        }

        private async Task AlarmsBackgroundProcessing()
        {
            using (var sync = await alarmTimersLock.LockAsync(cancellationToken).ConfigureAwait(false))
            {
                foreach (var pair in alarmsData)
                {
                    var alarmData = pair.Value;
                    if (alarmData.state)
                    {
                        if (alarmData.lastReceived.Elapsed >= alarmCancelInterval)
                        {
                            Trace.WriteLine($"[{cameraName}]Expiring {alarmData.CameraContruct.Id} as {alarmData.lastReceived.Elapsed} passed");
                            // send a alarm over event on expiry
                            alarmData.state = false;
                            alarmData.lastReceived.Reset();

                            await SendAlarm(cloneWithDifferentState(alarmData.CameraContruct, false),
                                            alarmData).ConfigureAwait(false);
                        }
                    }

                    if (alarmData.state)
                    {
                        await SendAlarmWithThrottling(alarmData).ConfigureAwait(false);
                    }
                }
            }
        }

        private async Task ResetBackAlarmsLoop()
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                await AlarmsBackgroundProcessing().ConfigureAwait(false);
            }
        }

        private async Task SendAlarm(OnOffCameraContruct alarm, AlarmData alarmData)
        {
            alarmData.lastUpdated.Restart();
            await enqueue(alarm).ConfigureAwait(false);
        }

        private async Task SendAlarmWithThrottling(AlarmData alarmData)
        {
            if (alarmData.lastUpdated.Elapsed >= deviceStateUpdateInterval)
            {
                Trace.WriteLine($"[{cameraName}]Refreshing alarm {alarmData.CameraContruct.Id} in HS as {alarmData.lastUpdated.Elapsed} passed");
                await SendAlarm(alarmData.CameraContruct, alarmData).ConfigureAwait(false);
            }
        }

        private sealed class AlarmData
        {
            public AlarmData(OnOffCameraContruct cameraContruct)
            {
                CameraContruct = cameraContruct;
            }

            public OnOffCameraContruct CameraContruct { get; set; }
            public Stopwatch lastReceived = new Stopwatch();
            public Stopwatch lastUpdated = new Stopwatch();
            public bool state;
        }

        private readonly string cameraName;
        private readonly TimeSpan alarmCancelInterval;
        private readonly Dictionary<string, AlarmData> alarmsData = new Dictionary<string, AlarmData>();
        private readonly AsyncLock alarmTimersLock = new AsyncLock();
        private readonly CancellationToken cancellationToken;
        private readonly Func<OnOffCameraContruct, bool, OnOffCameraContruct> cloneWithDifferentState;
        private readonly TimeSpan deviceStateUpdateInterval;
        private readonly Func<OnOffCameraContruct, Task> enqueue;
    }
}