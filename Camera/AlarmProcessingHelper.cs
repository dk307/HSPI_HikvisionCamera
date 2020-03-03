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
            this.alarmCancelInterval = alarmCancelInterval;
            this.cloneWithDifferentState = cloneWithDifferentState;
            this.enqueue = enqueue;
            this.cancellationToken = cancellationToken;

            Utils.TaskHelper.StartAsyncWithErrorChecking(Invariant($"{cameraName} Alarm Reset Loop"),
                                                         ResetBackAlarmsLoop,
                                                         cancellationToken);
        }

        public async Task ProcessNewAlarm(OnOffCameraContruct alarm)
        {
            bool sendNow = false;
            using (var sync = await alarmTimersLock.LockAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!alarmsData.TryGetValue(alarm.Id, out var alarmData))
                {
                    alarmData = new AlarmData(alarm);
                    alarmsData.Add(alarm.Id, alarmData);
                    sendNow = true;
                }

                if (!sendNow)
                {
                    sendNow = !alarmData.state;
                }

                alarmData.state = true;
                alarmData.CameraContruct = alarm;
                alarmData.lastReceived.Restart();

                if (sendNow)
                {
                    alarmData.lastUpdated.Restart();
                    await enqueue(alarm).ConfigureAwait(false);
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
                            alarmData.state = false;
                            alarmData.lastReceived.Reset();
                            alarmData.lastUpdated.Reset();
                            await enqueue(cloneWithDifferentState(alarmData.CameraContruct, false)).ConfigureAwait(false);
                        }
                    }

                    if (alarmData.state)
                    {
                        if (alarmData.lastUpdated.Elapsed >= alarmCancelInterval)
                        {
                            alarmData.lastUpdated.Restart();
                            await enqueue(cloneWithDifferentState(alarmData.CameraContruct, true)).ConfigureAwait(false);
                        }
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

        private class AlarmData
        {
            public AlarmData(OnOffCameraContruct cameraContruct)
            {
                CameraContruct = cameraContruct;
            }

            public OnOffCameraContruct CameraContruct { get; set; }
            public Stopwatch lastReceived = new Stopwatch();
            public Stopwatch lastUpdated = new Stopwatch();
            public bool state = false;
        }

        private readonly TimeSpan alarmCancelInterval;
        private readonly Func<OnOffCameraContruct, bool, OnOffCameraContruct> cloneWithDifferentState;
        private readonly Func<OnOffCameraContruct, Task> enqueue;
        private readonly Dictionary<string, AlarmData> alarmsData = new Dictionary<string, AlarmData>();
        private readonly AsyncLock alarmTimersLock = new AsyncLock();
        private readonly CancellationToken cancellationToken;
    }
}