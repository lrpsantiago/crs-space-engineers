using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        private class Tyre
        {
            private readonly float _wearFactor;
            private readonly float _maxPerformance;
            private readonly float _minPerformance;

            public char Symbol { get; private set; }

            public float CurrentFriction { get { return GetFrictionFromPerformanceScore(CurrentPerformance); } }

            private float CurrentPerformance { get { return CalculateCurrentPerformance(); } }

            public float MaxFriction { get; private set; }

            public float MinFriction { get; private set; }

            public float WearPercentage { get; private set; }

            public int Lifespan { get; private set; }

            public bool IsSlick { get; private set; }

            public Color Color { get; private set; }

            private Tyre(int lifespan, float maxFriction, float minFriction, char symbol, Color color, bool isSlick = true)
            {
                Lifespan = lifespan;
                MaxFriction = maxFriction;
                MinFriction = minFriction;
                Symbol = symbol;
                Color = color;
                IsSlick = isSlick;
                WearPercentage = 1f;

                _wearFactor = 1f / (60 * Lifespan);
                _maxPerformance = GetPerformanceScoreFromFriction(MaxFriction);
                _minPerformance = GetPerformanceScoreFromFriction(MinFriction);
            }

            public void Update(IMyShipController mainController, IMyMotorSuspension[] suspensions,
                List<IMyLightingBlock> brakelights, List<IMyLightingBlock> tyreLights, RaceData data, float delta)
            {
                var speed = mainController.GetShipSpeed();

                if (speed < 1)
                {
                    return;
                }

                var speedFactor = (float)MathHelper.Clamp(speed, 0, 90) / 90;
                var wearRate = _wearFactor * speedFactor * delta;

                WearPercentage -= wearRate * GetTyreWeariness(data.CurrentWeather);
                WearPercentage = MathHelper.Clamp(WearPercentage, 0, 1f);

                var adjustedPerformance = CurrentPerformance * GetTyreEfficiency(data.CurrentWeather);
                var adjustedFriction = GetFrictionFromPerformanceScore(adjustedPerformance);

                foreach (var s in suspensions)
                {
                    s.Friction = adjustedFriction;
                }

                if (WearPercentage <= 0.25f)
                {
                    if (brakelights.Any(l => l.BlinkIntervalSeconds <= 0))
                    {
                        foreach (var l in brakelights)
                        {
                            l.BlinkIntervalSeconds = 0.25f;
                        }
                    }

                    if (tyreLights.Any(l => l.BlinkIntervalSeconds <= 0))
                    {
                        foreach (var l in tyreLights)
                        {
                            l.BlinkIntervalSeconds = 0.25f;
                        }
                    }
                }
                else
                {
                    if (brakelights.Any(l => l.BlinkIntervalSeconds > 0))
                    {
                        foreach (var l in brakelights)
                        {
                            l.BlinkIntervalSeconds = 0f;
                        }
                    }

                    if (tyreLights.Any(l => l.BlinkIntervalSeconds > 0))
                    {
                        foreach (var l in tyreLights)
                        {
                            l.BlinkIntervalSeconds = 0f;
                        }
                    }
                }

                if (CurrentPerformance <= _minPerformance)
                {
                    if (suspensions.All(s => s.IsAttached))
                    {
                        var rand = new Random().Next(4);
                        suspensions[rand].Detach();
                    }
                }
            }

            public static Tyre Load(char compoundSymbol, float wearPercentage)
            {
                Tyre result;

                switch (compoundSymbol)
                {
                    case 'U': result = NewUltras(); break;
                    case 'S': result = NewSofts(); break;
                    case 'M': result = NewMediums(); break;
                    case 'H': result = NewHards(); break;
                    case 'X': result = NewExtras(); break;
                    case 'I': result = NewIntermediates(); break;
                    case 'W': result = NewWets(); break;
                    default: result = NewSofts(); break;
                }

                result.WearPercentage = wearPercentage;

                return result;
            }

            public static Tyre NewUltras()
            {
                return new Tyre(8, 90, 38, 'U', new Color(192, 0, 255));
            }

            public static Tyre NewSofts()
            {
                return new Tyre(10, 80, 40, 'S', Color.Red);
            }

            public static Tyre NewMediums()
            {
                return new Tyre(15, 65, 40, 'M', Color.Yellow);
            }

            public static Tyre NewHards()
            {
                return new Tyre(20, 55, 40, 'H', Color.White);
            }

            public static Tyre NewExtras()
            {
                return new Tyre(30, 50, 40, 'X', new Color(255, 32, 0));
            }

            public static Tyre NewIntermediates()
            {
                return new Tyre(15, 60, 38, 'I', Color.Green, false);
            }

            public static Tyre NewWets()
            {
                return new Tyre(15, 52, 38, 'W', new Color(0, 16, 255), false);
            }

            private float GetTyreEfficiency(WeatherLevel weatherLevel)
            {
                switch (weatherLevel)
                {
                    case WeatherLevel.Clear:
                    case WeatherLevel.LightClouds:
                    case WeatherLevel.Cloudy:
                    case WeatherLevel.Overcast:
                        return 1;
                    case WeatherLevel.Drizzle:
                        return IsSlick ? 0.75f : 1;
                    case WeatherLevel.Rain:
                        return IsSlick ? 0.5f : 1;
                    case WeatherLevel.HeavyRain:
                        return IsSlick ? 0.25f : (Symbol == 'I' ? 0.8f : 1);
                    default:
                        return 1;
                }
            }

            private float GetTyreWeariness(WeatherLevel weatherLevel)
            {
                switch (weatherLevel)
                {
                    case WeatherLevel.Clear:
                        return IsSlick ? 1 : (Symbol == 'W' ? 2f : 1.25f);
                    case WeatherLevel.LightClouds:
                        return Symbol == 'W' ? 1.8f : 1;
                    case WeatherLevel.Cloudy:
                        return Symbol == 'W' ? 1.6f : 1;
                    case WeatherLevel.Overcast:
                        return Symbol == 'W' ? 1.4f : 1;
                    case WeatherLevel.Drizzle:
                        return Symbol == 'W' ? 1.2f : 1;
                    case WeatherLevel.Rain:
                    case WeatherLevel.HeavyRain:
                    default:
                        return 1;
                }
            }

            private float CalculateCurrentPerformance()
            {
                var used = 1f - WearPercentage;
                var drop = 1f - (float)Math.Cos(MathHelper.ToRadians(used * 90));

                return _maxPerformance - ((_maxPerformance - _minPerformance) * drop);
            }

            private float GetPerformanceScoreFromFriction(float friction)
            {
                friction = MathHelper.Clamp(friction, 0f, 100f);

                var performance = 1f - (float)Math.Exp(-Math.Pow(friction / 46.9f, 2.97f));

                return MathHelper.Clamp(performance, 0f, 1f);
            }

            private float GetFrictionFromPerformanceScore(float performance)
            {
                performance = MathHelper.Clamp(performance, 0.001f, 0.999999f);

                return 46.9f * (float)Math.Pow(-Math.Log(1f - performance), 1f / 2.97f);
            }
        }
    }
}
