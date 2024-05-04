using System;

namespace IngameScript
{
    partial class Program
    {
        private class RaceData
        {
            public int Position { get; set; }
            public int TotalRacers { get; set; }
            public int Laps { get; set; }
            public int TotalLaps { get; set; }
            public string CurrentLapTime { get; set; } = "--:--.---";
            public string BestLapTime { get; set; } = "--:--.---";
            public Flag CurrentFlag { get; set; }
            public WeatherLevel CurrentWeather { get; set; }
            public string RankTable { get; set; }
            public LapSectorStatus StatusS1 { get; set; }
            public LapSectorStatus StatusS2 { get; set; }
            public LapSectorStatus StatusS3 { get; set; }
            public string PrevLapTime { get; set; } = "--:--.---";

            public void Map(string data)
            {
                try
                {
                    var values = data.Split(';');

                    Laps = Convert.ToInt32(values[0]);
                    Position = Convert.ToInt32(values[1]);
                    CurrentLapTime = values[2];
                    BestLapTime = values[3];
                    TotalRacers = Convert.ToInt32(values[4]);
                    TotalLaps = Convert.ToInt32(values[5]);
                    CurrentFlag = (Flag)Convert.ToInt32(values[6]);
                    CurrentWeather = (WeatherLevel)Convert.ToInt32(values[7]);
                    RankTable = values[8];
                    StatusS1 = (LapSectorStatus)Convert.ToInt32(values[9]);
                    StatusS2 = (LapSectorStatus)Convert.ToInt32(values[10]);
                    StatusS3 = (LapSectorStatus)Convert.ToInt32(values[11]);
                    PrevLapTime = values[12];
                }
                catch (Exception) { }
            }
        }
    }
}
