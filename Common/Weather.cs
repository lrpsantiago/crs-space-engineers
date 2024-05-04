using System;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        private class Weather
        {
            private static Color ClearColor = new Color(255, 106, 0);
            private static Color LightCloudsColor = new Color(255, 233, 127);
            private static Color CloudyColor = new Color(255, 244, 191);
            private static Color OvercastColor = Color.White;
            private static Color DrizzleColor = new Color(127, 202, 255);
            private static Color RainColor = new Color(0, 148, 255);
            private static Color HeavyRainColor = Color.Blue;

            private const float WEATHER_UPDATE_TIME = 60000;
            private readonly WeatherLevel _initialLevel;
            private readonly int[][] _changeChances;
            private readonly Random _random;
            private float _time;

            public WeatherLevel Level { get; private set; }

            public bool Enabled { get; set; }

            public string Description
            {
                get { return GetWeatherDescription(Level); }
            }

            public Weather(WeatherLevel initialLevel = WeatherLevel.Clear)
            {
                _time = WEATHER_UPDATE_TIME;
                _changeChances = new int[][]
                {
                    new int[] { 0,  60 },
                    new int[] { 50, 30 },
                    new int[] { 50, 20 },
                    new int[] { 40, 40 },
                    new int[] { 20, 50 },
                    new int[] { 30, 50 },
                    new int[] { 50,  0 },
                };

                _random = new Random();
                _initialLevel = initialLevel;
                Level = _initialLevel;
            }

            public void Update(float delta)
            {
                if (!Enabled)
                {
                    Level = WeatherLevel.Clear;
                    return;
                }

                _time -= (delta);

                if (_time <= 0)
                {
                    ChangeLevel();
                    _time += WEATHER_UPDATE_TIME;
                }
            }

            private void ChangeLevel()
            {
                var currentLevel = (int)Level;
                var chanceArray = _changeChances[currentLevel + 3];
                var downChance = chanceArray[0] / 10;
                var upChance = 11 - chanceArray[1] / 10;

                var roll = _random.Next(10) + 1;

                if (roll <= downChance)
                {
                    currentLevel--;
                }
                else if (roll >= upChance)
                {
                    currentLevel++;
                }

                Level = (WeatherLevel)MathHelper.Clamp(currentLevel, -3, 3);
            }

            public static string GetWeatherDescription(WeatherLevel weatherLevel)
            {
                switch (weatherLevel)
                {
                    case WeatherLevel.Clear:
                        return "Clear";

                    case WeatherLevel.LightClouds:
                        return "L. Clouds";

                    case WeatherLevel.Cloudy:
                        return "Cloudy";

                    case WeatherLevel.Overcast:
                        return "Overcast";

                    case WeatherLevel.Drizzle:
                        return "Drizzle";

                    case WeatherLevel.Rain:
                        return "Rain";

                    case WeatherLevel.HeavyRain:
                        return "H. Rain";

                    default:
                        return string.Empty;
                }
            }

            public static Color GetWeatherColor(WeatherLevel weatherLevel)
            {
                switch (weatherLevel)
                {
                    case WeatherLevel.Clear:
                        return ClearColor;

                    case WeatherLevel.LightClouds:
                        return LightCloudsColor;

                    case WeatherLevel.Cloudy:
                        return CloudyColor;

                    case WeatherLevel.Overcast:
                        return OvercastColor;

                    case WeatherLevel.Drizzle:
                        return DrizzleColor;

                    case WeatherLevel.Rain:
                        return RainColor;

                    case WeatherLevel.HeavyRain:
                        return HeavyRainColor;

                    default:
                        return Color.White;
                }
            }
        }

        private enum WeatherLevel
        {
            Clear = -3,
            LightClouds = -2,
            Cloudy = -1,
            Overcast = 0,
            Drizzle = 1,
            Rain = 2,
            HeavyRain = 3
        }
    }
}
