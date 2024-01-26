using System;
using System.Collections.Generic;
using System.Linq;

namespace IngameScript
{
    partial class Program
    {
        private class TrackedRacer
        {
            public string Name { get; set; }

            public int Position { get; set; }

            public long? IgcAddress { get; set; }

            public IList<Lap> LapTimes { get; private set; }

            public int Laps
            {
                get { return LapTimes.Count; }
            }

            public Lap CurrentLap
            {
                get
                {
                    return LapTimes.LastOrDefault();
                }
            }

            public Lap BestLap { get; private set; }

            public Lap PreviousLap
            {
                get
                {
                    if (LapTimes.Count <= 1)
                    {
                        return null;
                    }

                    return LapTimes[LapTimes.Count - 2];
                }
            }

            public TimeSpan CurrentLapTime
            {
                get
                {
                    return CurrentLap != null ? CurrentLap.LapTime : TimeSpan.Zero;
                }
            }

            public TimeSpan? PreviousLapTime
            {
                get
                {
                    return PreviousLap?.LapTime;
                }
            }

            public TimeSpan? BestLapTime
            {
                get
                {
                    return BestLap?.LapTime;
                }
            }

            public TimeSpan TotalRaceTime
            {
                get
                {
                    var totalTicks = LapTimes.Where(l => l.IsFinished)
                        .Sum(t => t.LapTime.Ticks);

                    return new TimeSpan(totalTicks);
                }
            }

            public TrackedRacer()
            {
                LapTimes = new List<Lap>();
            }

            public void NewLap(long startTimeStamp, bool isOutLap = false)
            {
                if (CurrentLap != null)
                {
                    if (!CurrentLap.HasCrossedAllCheckpoints)
                    {
                        return;
                    }

                    CurrentLap.Finish();

                    if (!CurrentLap.IsOutLap && (BestLap == null || CurrentLap?.LapTime < BestLap?.LapTime))
                    {
                        BestLap = CurrentLap;
                    }
                }

                var newLap = new Lap(startTimeStamp, isOutLap);
                LapTimes.Add(newLap);
            }
        }
    }
}
