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

            public char Symbol { get; private set; }

            public float CurrentFriction { get; set; }

            public float MaxFriction { get; private set; }

            public float MinFriction { get; private set; }

            public float AnchorFriction { get; private set; }

            public float AnchorAtPerc { get; private set; }

            public float WearPercentage
            {
                get { return ((CurrentFriction - MinFriction) / (MaxFriction - MinFriction)) * 100f; }
            }

            public int Lifespan { get; private set; }

            public bool IsSlick { get; private set; }

            public Color Color { get; private set; }

            private Tyre(float maxFriction, int lifespan, float anchorFriction, float anchorAtPerc, char symbol, Color color, bool isSlick = true)
            {
                MaxFriction = maxFriction;
                CurrentFriction = MaxFriction;
                Lifespan = lifespan;
                AnchorFriction = anchorFriction;
                AnchorAtPerc = anchorAtPerc;
                MinFriction = (float)Math.Round(AnchorFriction - ((MaxFriction - AnchorFriction) / (100 - AnchorAtPerc)) * AnchorAtPerc, 2);
                Symbol = symbol;
                Color = color;
                IsSlick = isSlick;

                _wearFactor = (MaxFriction - MinFriction) / (60 * Lifespan);
            }

            public void Update(IMyShipController mainController, IMyMotorSuspension[] suspensions, List<IMyLightingBlock> brakelights, List<IMyLightingBlock> tyreLights, RaceData data, float delta)
            {
                var speed = mainController.GetShipSpeed();

                if (speed < 1)
                {
                    return;
                }

                var speedFactor = (float)MathHelper.Clamp(speed, 0, 90) / 90;
                var wearRate = _wearFactor * speedFactor * delta;

                CurrentFriction -= wearRate;
                CurrentFriction = MathHelper.Clamp(CurrentFriction, MinFriction, MaxFriction);

                foreach (var s in suspensions)
                {
                    s.Friction = !(IsSlick && data.CurrentWeather == Weather.Rain) ? CurrentFriction : CurrentFriction / 2;
                }

                if (WearPercentage <= AnchorAtPerc)
                {
                    if (tyreLights.Any(l => l.BlinkIntervalSeconds <= 0))
                    {
                        foreach (var l in brakelights)
                        {
                            l.BlinkIntervalSeconds = 0.25f;
                        }

                        foreach (var l in tyreLights)
                        {
                            l.BlinkIntervalSeconds = 0.25f;
                        }
                    }
                }
                else
                {
                    if (tyreLights.Any(l => l.BlinkIntervalSeconds > 0))
                    {
                        foreach (var l in brakelights)
                        {
                            l.BlinkIntervalSeconds = 0f;
                        }

                        foreach (var l in tyreLights)
                        {
                            l.BlinkIntervalSeconds = 0f;
                        }
                    }
                }

                if (CurrentFriction <= MinFriction)
                {
                    if (suspensions.All(s => s.IsAttached))
                    {
                        var rand = new Random().Next(4);
                        suspensions[rand].Detach();
                    }
                }
            }

            public static Tyre NewUltras()
            {
                return new Tyre(100, 5, 60, 20, 'U', new Color(192, 0, 255));
            }

            public static Tyre NewSofts()
            {
                return new Tyre(100, 8, 50, 20, 'S', Color.Red);
            }

            public static Tyre NewMediums()
            {
                return new Tyre(75, 13, 50, 20, 'M', Color.Yellow);
            }

            public static Tyre NewHards()
            {
                return new Tyre(60, 21, 50, 20, 'H', Color.White);
            }

            public static Tyre NewExtras()
            {
                return new Tyre(55, 34, 50, 20, 'X', new Color(255, 32, 0));
            }

            public static Tyre NewIntermediates()
            {
                return new Tyre(60, 8, 40, 10, 'I', Color.Green, false);
            }

            public static Tyre NewWets()
            {
                return new Tyre(50, 21, 40, 10, 'W', new Color(0, 16, 255), false);
            }
        }
    }
}
