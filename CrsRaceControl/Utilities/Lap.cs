using System;
using System.Linq;

namespace IngameScript
{
    partial class Program
    {
        private class Lap
        {
            private long _startTimeStamp;
            private long? _endTimeStamp;
            private long[] _checkpointTimeStamp;

            public TimeSpan LapTime
            {
                get
                {
                    if (_endTimeStamp != null)
                    {
                        var start = (_startTimeStamp / 10000) * 10000;
                        var end = (_endTimeStamp.Value / 10000) * 10000;

                        return new TimeSpan(end - start);
                    }

                    return new TimeSpan(DateTime.Now.Ticks - _startTimeStamp);
                }
            }

            public bool HasCrossedAllCheckpoints
            {
                get { return _checkpointTimeStamp.All(c => c > 0); }
            }

            public bool IsFinished
            {
                get { return HasCrossedAllCheckpoints && _endTimeStamp != null; }
            }

            public TimeSpan TimeS1
            {
                get { return GetSector(1); }
            }

            public TimeSpan TimeS2
            {
                get { return GetSector(2); }
            }

            public TimeSpan TimeS3
            {
                get { return GetSector(3); }
            }

            public bool IsFinishedS1
            {
                get { return IsSectorFinished(1); }
            }

            public bool IsFinishedS2
            {
                get { return IsSectorFinished(2); }
            }

            public bool IsFinishedS3
            {
                get { return IsSectorFinished(3); }
            }

            public bool IsOutLap { get; set; }

            public Lap(long startTimeStamp, bool isOutLap = false)
            {
                _startTimeStamp = startTimeStamp;
                _endTimeStamp = null;
                _checkpointTimeStamp = new long[CHECKPOINT_COUNT];

                for (int i = 0; i < _checkpointTimeStamp.Length; i++)
                {
                    _checkpointTimeStamp[i] = -1;
                }

                IsOutLap = isOutLap;
            }

            public void SetCheckpoint(int i)
            {
                if (_checkpointTimeStamp[i] <= 0)
                {
                    _checkpointTimeStamp[i] = DateTime.Now.Ticks;
                }
            }

            public void Finish()
            {
                _endTimeStamp = DateTime.Now.Ticks;
            }

            public TimeSpan GetSector(int sectorNumber)
            {
                int sectorIndex = sectorNumber - 1;

                if (sectorIndex < 0 || sectorIndex > CHECKPOINT_COUNT)
                {
                    return TimeSpan.Zero;
                }

                long sectorStartTimeStamp = _startTimeStamp;
                long? sectorEndTimeStamp;

                if (sectorIndex == 0)
                {
                    sectorEndTimeStamp = _checkpointTimeStamp[sectorIndex];
                }
                else if (sectorIndex == CHECKPOINT_COUNT)
                {
                    sectorStartTimeStamp = _checkpointTimeStamp[sectorIndex - 1];
                    sectorEndTimeStamp = _endTimeStamp;
                }
                else
                {
                    sectorStartTimeStamp = _checkpointTimeStamp[sectorIndex - 1];
                    sectorEndTimeStamp = _checkpointTimeStamp[sectorIndex];
                }

                if (sectorStartTimeStamp < 0)
                {
                    return TimeSpan.Zero;
                }

                if (sectorEndTimeStamp == null || sectorEndTimeStamp <= 0)
                {
                    return new TimeSpan(DateTime.Now.Ticks - sectorStartTimeStamp);
                }

                return new TimeSpan(sectorEndTimeStamp.Value - sectorStartTimeStamp);
            }

            public bool IsSectorFinished(int sectorNumber)
            {
                int sectorIndex = sectorNumber - 1;
                long? sectorEndTimeStamp;

                if (sectorIndex == 0)
                {
                    sectorEndTimeStamp = _checkpointTimeStamp[sectorIndex];
                }
                else if (sectorIndex == CHECKPOINT_COUNT)
                {
                    sectorEndTimeStamp = _endTimeStamp;
                }
                else
                {
                    sectorEndTimeStamp = _checkpointTimeStamp[sectorIndex];
                }

                return sectorEndTimeStamp.HasValue && sectorEndTimeStamp > 0;
            }

            public override string ToString()
            {
                var time = LapTime;
                return $"{time.Minutes:00}:{time.Seconds:00}.{time.Milliseconds:000}";
            }
        }
    }
}
