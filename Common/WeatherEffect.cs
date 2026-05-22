using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using VRage;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        private class WeatherEffect
        {
            private const float DEFAULT_RADIUS = 5000f;
            private const float DEFAULT_FADE_SPEED = 0.001f;

            private readonly IMyProgrammableBlock _programmableBlock;
            private float _intensity;
            private string _subtype = "FogLight";

            public bool Enabled { get; set; }

            public WeatherEffect(IMyProgrammableBlock programmableBlock)
            {
                _programmableBlock = programmableBlock;
            }

            public void Apply(WeatherLevel level)
            {
                if (!Enabled)
                {
                    level = WeatherLevel.Clear;
                }

                var settings = GetSettings(level);

                _intensity = MoveTowards(_intensity, settings.Intensity, DEFAULT_FADE_SPEED);

                if (!string.IsNullOrEmpty(settings.Subtype))
                {
                    _subtype = settings.Subtype;
                }

                _programmableBlock.SetValue(
                    "ScriptWeather",
                    new MyTuple<string, float, Vector3D, Vector3, int, float>(
                        _subtype,
                        DEFAULT_RADIUS,
                        _programmableBlock.WorldMatrix.Translation,
                        settings.Velocity,
                        0,
                        _intensity));
            }

            private static WeatherEffectSettings GetSettings(WeatherLevel level)
            {
                switch (level)
                {
                    case WeatherLevel.Clear:
                        return new WeatherEffectSettings("FogLight", 0f, Vector3.Zero);

                    case WeatherLevel.LightClouds:
                        return new WeatherEffectSettings("FogLight", 0.25f, Vector3.Zero);

                    case WeatherLevel.Cloudy:
                        return new WeatherEffectSettings("FogLight", 0.5f, Vector3.Zero);

                    case WeatherLevel.Overcast:
                        return new WeatherEffectSettings("FogLight", 0.75f, Vector3.Zero);

                    case WeatherLevel.Drizzle:
                        return new WeatherEffectSettings("RainLight", 0.8f, new Vector3(0, 1, 0));

                    case WeatherLevel.Rain:
                        return new WeatherEffectSettings("RainHeavy", 0.9f, new Vector3(0, 1, 0));

                    case WeatherLevel.HeavyRain:
                        return new WeatherEffectSettings("ThunderstormHeavy", 0.99f, new Vector3(0, 1, 0));

                    default:
                        return new WeatherEffectSettings("FogLight", 0f, Vector3.Zero);
                }
            }

            private static float MoveTowards(float current, float target, float maxDelta)
            {
                if (current < target)
                {
                    return MathHelper.Min(current + maxDelta, target);
                }

                if (current > target)
                {
                    return MathHelper.Max(current - maxDelta, target);
                }

                return target;
            }

            private struct WeatherEffectSettings
            {
                public readonly string Subtype;
                public readonly float Intensity;
                public readonly Vector3 Velocity;

                public WeatherEffectSettings(string subtype, float intensity, Vector3 velocity)
                {
                    Subtype = subtype;
                    Intensity = intensity;
                    Velocity = velocity;
                }
            }
        }
    }
}
