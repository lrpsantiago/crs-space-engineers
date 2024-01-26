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
            public Weather CurrentWeather { get; set; }
            public int RiskOfRain { get; set; }
            public int WeatherChangeCountdown { get; set; }
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
                    CurrentWeather = (Weather)Convert.ToInt32(values[7]);
                    RiskOfRain = Convert.ToInt32(values[8]);
                    WeatherChangeCountdown = Convert.ToInt32(values[9]);
                    RankTable = values[10];
                    StatusS1 = (LapSectorStatus)Convert.ToInt32(values[11]);
                    StatusS2 = (LapSectorStatus)Convert.ToInt32(values[12]);
                    StatusS3 = (LapSectorStatus)Convert.ToInt32(values[13]);
                    PrevLapTime = values[14];
                }
                catch (Exception) { }
            }
        }
    }
}
