using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        #region mdk preserve

        private const int CHECKPOINT_COUNT = 2;
        private const int RACE_LAPS = 99;
        private const int START_LIGHTS_COUNT = 5;
        private const int TIME_PER_LIGHT = 1000;
        private const int START_LIGHTS_STARTUP_TIME = 10000;
        private const int START_LIGHTS_OUT_TIME_MIN = 1000;
        private const int START_LIGHTS_OUT_TIME_MAX = 1001;
        private readonly string MAIN_LCD_NAME = "Race Main LCD";
        private readonly string LAPS_LCD_NAME = "Race Laps LCD";
        private readonly string SPEEDTRAP_LCD_NAME = "Race Speedtrap LCD";
        private readonly string FASTEST_LAPS_LCD_GROUP_NAME = "Race Fastest Laps LCDs";
        private readonly string START_FINISH_SENSOR_NAME = "Start/Finish Sensor";
        private readonly string CHECKPOINT_SENSOR_PREFIX = "Checkpoint Sensor ";
        private readonly string START_LIGHTS_PREFIX = "Start Lights ";
        private readonly string START_LIGHTS_GO = "Start Lights Go";
        private readonly string LAP_COUNTERS_GROUP_NAME = "Lap Counter LCDs";
        private readonly string PIT_ENTRY_SENSOR_NAME = "Pit Entry Sensor";
        private readonly string PIT_EXIT_SENSOR_NAME = "Pit Exit Sensor";
        private const int DISPLAY_WIDTH = 38;
        private const int BROADCAST_COOLDOWN = 1000;
        private const bool ENABLE_WEATHER = true;
        private const int INITIAL_RISK_OF_RAIN = 50;
        private const int WEATHER_CHANGE_TIME = 30000;
        private const int RAIN_TIME_MIN = 60000 * 5;
        private const int RAIN_TIME_MAX = 60000 * 25;

        #endregion

        private string CRS_VERSION_COMPATIBILITY = "12.5.0";
        private IMyTextPanel _lcdMain;
        private IMyTextPanel _lcdLaps;
        private IMyTextPanel _lcdSpeedtrap;
        private List<IMyTextPanel> _lcdGroupFastestLaps;
        private List<IMyTextPanel> _lcdGroupLapCounters;
        private IMySensorBlock _startFinishSensor;
        private IList<IMySensorBlock> _checkpointSensors = new List<IMySensorBlock>();
        private IMySensorBlock _pitEntrySensor;
        private IMySensorBlock _pitExitSensor;
        private IList<IEnumerable<IMyLightingBlock>> _startLightGroups = new List<IEnumerable<IMyLightingBlock>>();
        private IEnumerable<IMyLightingBlock> _startLightGroupGo;
        private Dictionary<string, TrackedRacer> _racers = new Dictionary<string, TrackedRacer>();
        private List<MyDetectedEntityInfo> _detectedEntities = new List<MyDetectedEntityInfo>();
        private StringBuilder _stringBuilder;
        private Dictionary<string, long> _pendingAddressTracking;
        private List<TrackedRacer> _racePositions = new List<TrackedRacer>();
        private Flag _currentFlag = Flag.Green;
        private Weather _currentWeather = Weather.Clear;
        private int _riskOfRain = INITIAL_RISK_OF_RAIN;
        private string _rankTable;
        private int _weatherChangeCountdown = -1;

        private RaceMode _raceMode;
        private bool _startLightsProtocol;
        private long _raceStartTimeStamp;
        private int _originalStartTime;
        private int _startTimeCounter;
        private int _broadcastCooldown;
        private Lap _bestLap;

        public Program()
        {
            _pendingAddressTracking = new Dictionary<string, long>();
            _stringBuilder = new StringBuilder();
            Runtime.UpdateFrequency = UpdateFrequency.Update1;

            try
            {
                SetupConsole();
                MyEcho("Initializing Race Script...\n");

                MyEcho("Detecting LCDs.................");
                SetupLcds();
                MyEcho("OK!\n");

                MyEcho("Detecting Sensors..............");
                SetupSensors();
                MyEcho("OK!\n");

                MyEcho("Detecting Start Lights.........");
                SetupLights();
                MyEcho("OK!\n");

                HandleArgument("FREE");
                MyEcho("Waiting for players...\n");
            }
            catch (Exception e)
            {
                MyEcho("\nError: " + e.Message);
            }

            UpdateFastestLapsLcd();
        }

        public void Save()
        {
            // Called when the program needs to save its state. Use
            // this method to save your state to the Storage field
            // or some other means. 
            // 
            // This method is optional and can be removed if not
            // needed.
        }

        public void Main(string argument, UpdateType updateSource)
        {
            Echo($"Running Race Control {CRS_VERSION_COMPATIBILITY}+");

            HandleArgument(argument);

            BroadcastAddress();
            ListenSignals();

            UpdateStartLights();
            UpdateStartFinishSensor();
            UpdateCheckpointsSensors();
            UpdatePitEntrySensor();
            UpdatePitExitSensor();
            UpdateMainLcd();
            UpdateLapsLcd();
            UpdateWeather();

            SendData();
        }

        private void HandleArgument(string argument)
        {
            switch (argument)
            {
                case "RACE":
                    Reset();
                    _startLightsProtocol = true;
                    _raceMode = RaceMode.Race;
                    ConsolePrint("Race Mode!\n");
                    break;

                case "QUALI":
                    Reset();
                    _raceMode = RaceMode.Qualifying;
                    ConsolePrint("Quilifying Mode!\n");
                    break;

                case "FREE":
                    Reset();
                    _raceMode = RaceMode.FreePractice;
                    ConsolePrint("Free Practice Mode!\n");
                    break;

                case "FLAG_G":
                    _currentFlag = Flag.Green;
                    break;

                case "FLAG_Y":
                    _currentFlag = Flag.Yellow;
                    break;

                case "FLAG_R":
                    _currentFlag = Flag.Red;
                    break;

                default:
                    break;
            }
        }

        private void UpdateStartLights()
        {
            if (!_startLightsProtocol || _raceStartTimeStamp != 0)
            {
                return;
            }

            if (_startTimeCounter <= 0)
            {
                var random = new Random();
                var randomTime = random.Next(START_LIGHTS_OUT_TIME_MIN, START_LIGHTS_OUT_TIME_MAX + 1);
                var lightTime = START_LIGHTS_COUNT * TIME_PER_LIGHT;

                _startTimeCounter = START_LIGHTS_STARTUP_TIME + lightTime + randomTime;
                _originalStartTime = _startTimeCounter;

                for (int i = 0; i < _startLightGroups.Count; i++)
                {
                    SetLights(i, false);
                }

                foreach (var l in _startLightGroupGo)
                {
                    l.Enabled = true;
                    l.Color = Color.Black;
                }

                MyEcho("Starting Countdown...\n");

                return;
            }

            _startTimeCounter -= (int)(Runtime.TimeSinceLastRun.TotalMilliseconds);

            if (_startTimeCounter > _originalStartTime - START_LIGHTS_STARTUP_TIME)
            {
                return;
            }

            for (int i = 0; i < START_LIGHTS_COUNT; i++)
            {
                SetLights(i, _startTimeCounter <= _originalStartTime - START_LIGHTS_STARTUP_TIME - ((i + 1) * TIME_PER_LIGHT));
            }

            if (_startTimeCounter <= 0)
            {
                _startLightsProtocol = false;

                for (int i = 0; i < _startLightGroups.Count; i++)
                {
                    SetLights(i, false);
                }

                foreach (var l in _startLightGroupGo)
                {
                    l.Enabled = true;
                    l.Color = Color.Lime;
                }

                _raceStartTimeStamp = DateTime.Now.Ticks;

                MyEcho($"Race started after {_originalStartTime} milliseconds!\n");
            }
        }

        private void UpdateStartFinishSensor()
        {
            _detectedEntities.Clear();
            _startFinishSensor.DetectedEntities(_detectedEntities);
            var nowTimeStamp = DateTime.Now.Ticks;
            var rand = new Random();

            foreach (var entity in _detectedEntities)
            {
                if (entity.IsEmpty())
                {
                    continue;
                }

                if (entity.Name.Contains("Grid"))
                {
                    continue;
                }

                if (_racers.ContainsKey(entity.Name))
                {
                    var racer = _racers[entity.Name];

                    if (_raceMode == RaceMode.Race && racer.Laps >= RACE_LAPS)
                    {
                        continue;
                    }

                    if (!racer.CurrentLap.HasCrossedAllCheckpoints)
                    {
                        continue;
                    }

                    racer.NewLap(nowTimeStamp);

                    if (_bestLap == null || racer.BestLap?.LapTime < _bestLap?.LapTime)
                    {
                        _bestLap = racer.BestLap;
                    }

                    UpdateFastestLapsLcd();
                }
                else
                {
                    long addressTracking;
                    var addressExists = _pendingAddressTracking.TryGetValue(entity.Name, out addressTracking);
                    var newRacer = new TrackedRacer
                    {
                        Name = entity.Name,
                        IgcAddress = addressExists ? addressTracking : (long?)null,
                    };

                    var initialStamp = _raceMode == RaceMode.Race
                        ? _raceStartTimeStamp
                        : nowTimeStamp;

                    newRacer.NewLap(initialStamp);

                    _racers[entity.Name] = newRacer;

                    if (addressExists)
                    {
                        _pendingAddressTracking.Remove(entity.Name);
                    }

                    MyEcho($"{entity.Name} registered!\n");
                }

                UpdatePositions();
                UpdateLapCountersLcd();
                UpdateSpeedtrapLcd(entity);

                if (_raceMode == RaceMode.Race && ENABLE_WEATHER)
                {
                    var first = _racePositions.FirstOrDefault();

                    if (first != null
                        && first == _racers[entity.Name]
                        && _riskOfRain < 100)
                    {
                        _riskOfRain += rand.Next(-4, 9);
                        _riskOfRain = MathHelper.Clamp(_riskOfRain, 0, 100);
                        ConsolePrint($"RoR: {_riskOfRain}\n");

                        if (_riskOfRain == 100 && _weatherChangeCountdown <= 0)
                        {
                            _weatherChangeCountdown = WEATHER_CHANGE_TIME;
                        }
                    }
                }
            }
        }

        private void UpdateCheckpointsSensors()
        {
            for (int i = 0; i < _checkpointSensors.Count; i++)
            {
                _detectedEntities.Clear();

                var sensor = _checkpointSensors[i];
                sensor.DetectedEntities(_detectedEntities);

                foreach (var entity in _detectedEntities)
                {
                    if (entity.IsEmpty())
                    {
                        continue;
                    }

                    if (entity.Name.Contains("Grid"))
                    {
                        continue;
                    }

                    if (_racers.ContainsKey(entity.Name))
                    {
                        var racer = _racers[entity.Name];
                        racer.CurrentLap.SetCheckpoint(i);
                    }
                }
            }

            UpdatePositions();
        }

        private void UpdatePitEntrySensor()
        {
            if (_pitEntrySensor == null)
            {
                return;
            }

            _detectedEntities.Clear();
            _pitEntrySensor.DetectedEntities(_detectedEntities);

            foreach (var entity in _detectedEntities)
            {
                if (entity.IsEmpty())
                {
                    continue;
                }

                if (entity.Name.Contains("Grid"))
                {
                    continue;
                }

                if (_racers.ContainsKey(entity.Name))
                {
                    var racer = _racers[entity.Name];

                    if (racer.IgcAddress.HasValue)
                    {
                        IGC.SendUnicastMessage(racer.IgcAddress.Value, "Argument", "LMT_ON");
                    }
                }
            }
        }

        private void UpdatePitExitSensor()
        {
            if (_pitExitSensor == null)
            {
                return;
            }

            _detectedEntities.Clear();
            _pitExitSensor.DetectedEntities(_detectedEntities);
            var nowTimeStamp = DateTime.Now.Ticks;

            foreach (var entity in _detectedEntities)
            {
                if (entity.IsEmpty())
                {
                    continue;
                }

                if (entity.Name.Contains("Grid"))
                {
                    continue;
                }

                if (!_racers.ContainsKey(entity.Name))
                {
                    continue;
                }

                var racer = _racers[entity.Name];

                if (racer.IgcAddress.HasValue)
                {
                    IGC.SendUnicastMessage(racer.IgcAddress.Value, "Argument", "LMT_OFF");
                }

                if (_raceMode == RaceMode.Race && racer.Laps >= RACE_LAPS)
                {
                    continue;
                }

                if (!racer.CurrentLap.HasCrossedAllCheckpoints)
                {
                    continue;
                }

                racer.NewLap(nowTimeStamp, true);

                UpdateFastestLapsLcd();
            }
        }

        private void UpdateMainLcd()
        {
            _stringBuilder.Clear();
            _stringBuilder.AppendLine($"- Position -");
            _stringBuilder.AppendLine();

            for (int p = 0; p < _racePositions.Count; p++)
            {
                var position = p + 1;
                var racer = _racePositions[p];
                racer.Position = position;

                var racerName = TruncateString(racer.Name.Trim(), 15).Trim();
                var raceTime = racer.TotalRaceTime;
                var strTime = $"{raceTime.Minutes:00}:{raceTime.Seconds:00}.{raceTime.Milliseconds:000}";

                var line = BuildLine($"#{position:00}> {racerName}", $"L{racer.Laps:00} ({strTime})");
                _stringBuilder.AppendLine(line);
            }

            _rankTable = _stringBuilder.Replace(';', ' ').ToString();
            _lcdMain.WriteText(_rankTable);
        }

        private void UpdateLapsLcd()
        {
            if (_lcdLaps == null)
            {
                return;
            }

            var displayList = _racers.Values
                .OrderBy(r => r.Name)
                .ToList();

            _stringBuilder.Clear();
            _stringBuilder.AppendLine("- Laps Logs -");
            _stringBuilder.AppendLine();

            for (int i = 0; i < displayList.Count; i++)
            {
                var racer = displayList[i];

                for (int j = 0; j < racer.LapTimes.Count; j++)
                {
                    var lap = j + 1;
                    var racerName = TruncateString(racer.Name.Trim(), 20).Trim();
                    var lapObj = racer.LapTimes[j];
                    var lapTime = lapObj.LapTime;
                    var strTime = $"{lapTime.Minutes:00}:{lapTime.Seconds:00}.{lapTime.Milliseconds:000}";
                    var timeS1 = lapObj.TimeS1;
                    var timeS2 = lapObj.TimeS2;
                    var timeS3 = lapObj.TimeS3;

                    var strTimeS1 = $"{timeS1.Minutes:00}:{timeS1.Seconds:00}.{timeS1.Milliseconds:000}";
                    var strTimeS2 = $"{timeS2.Minutes:00}:{timeS2.Seconds:00}.{timeS2.Milliseconds:000}";
                    var strTimeS3 = $"{timeS3.Minutes:00}:{timeS3.Seconds:00}.{timeS3.Milliseconds:000}";

                    var line1 = BuildLine($"{racerName}", $"L{lap:00} ({strTime})");
                    var line2 = $"└► {strTimeS1} | {strTimeS2} | {strTimeS3}\n";

                    _stringBuilder.AppendLine(line1);
                    _stringBuilder.AppendLine(line2);
                }
            }

            _lcdLaps.WriteText(_stringBuilder);
        }

        private void UpdatePositions()
        {
            _racePositions = _racers.Values
                .OrderByDescending(r => r.Laps)
                .ThenBy(r => r.TotalRaceTime)
                .ToList();
        }

        private void SendData()
        {
            foreach (var key in _racers.Keys)
            {
                var racer = _racers[key];

                if (!racer.IgcAddress.HasValue)
                {
                    continue;
                }

                var tag = "RaceData";

                var currentLapTime = racer.CurrentLapTime;
                var strCurrentLapTime = $"{currentLapTime.Minutes:00}:{currentLapTime.Seconds:00}.{currentLapTime.Milliseconds:000}";
                var bestLapTime = racer.BestLapTime.GetValueOrDefault();
                var strBestLapTime = $"{bestLapTime.Minutes:00}:{bestLapTime.Seconds:00}.{bestLapTime.Milliseconds:000}";
                var strTotalRacers = $"{_racers.Count}";
                var strTotalLaps = $"{RACE_LAPS}";
                var strCurrentFlag = $"{(int)_currentFlag}";
                var strCurrentWeather = $"{(int)_currentWeather}";
                var strRiskOfRain = $"{_riskOfRain}";
                var strWeatherChangeCountdown = $"{Math.Ceiling((float)_weatherChangeCountdown / 1000)}";
                var strRankTable = _rankTable;
                var strS1 = $"{(int)GetSectorStatus(1, racer)}";
                var strS2 = $"{(int)GetSectorStatus(2, racer)}";
                var strS3 = $"{(int)GetSectorStatus(3, racer)}";
                var prevLapTime = racer.PreviousLapTime.GetValueOrDefault();
                var strPrevLapTime = $"{prevLapTime.Minutes:00}:{prevLapTime.Seconds:00}.{prevLapTime.Milliseconds:000}";

                var data = $"{racer.Laps};{racer.Position};{strCurrentLapTime};{strBestLapTime};{strTotalRacers};{strTotalLaps};{strCurrentFlag};{strCurrentWeather};{strRiskOfRain};{strWeatherChangeCountdown};{strRankTable};{strS1};{strS2};{strS3};{strPrevLapTime}";

                IGC.SendUnicastMessage(racer.IgcAddress.Value, tag, data);
            }
        }

        private void UpdateSpeedtrapLcd(MyDetectedEntityInfo entity)
        {
            if (_lcdSpeedtrap == null)
            {
                return;
            }

            var v = entity.Velocity;
            var speed = Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);

            _lcdSpeedtrap.WriteText("Speed:\n" + speed.ToString("F2") + "\nm/s");
        }

        private void UpdateFastestLapsLcd()
        {
            if (_lcdGroupFastestLaps == null || _lcdGroupFastestLaps.Count <= 0)
            {
                return;
            }

            _stringBuilder.Clear();
            _stringBuilder.AppendLine("- Fastest Laps -");
            _stringBuilder.AppendLine();

            var racers = _racers.Values
                .OrderBy(r => r.BestLapTime)
                .ToList();

            for (int i = 0; i < racers.Count; i++)
            {
                var position = i + 1;
                var racerName = TruncateString(racers[i].Name.Trim(), 20).Trim();
                var bestLap = racers[i].BestLapTime.GetValueOrDefault();
                var strBestLap = racers[i].BestLapTime.HasValue
                    ? $"{bestLap.Minutes:00}:{bestLap.Seconds:00}.{bestLap.Milliseconds:000}"
                    : "00:00.000";

                var line = BuildLine($"#{position:00}> {racerName}", strBestLap);
                _stringBuilder.AppendLine(line);
            }

            var text = _stringBuilder.ToString();

            foreach (var l in _lcdGroupFastestLaps)
            {
                l.WriteText(text);
            }
        }

        private void UpdateLapCountersLcd()
        {
            if (_lcdGroupLapCounters.Count <= 0)
            {
                return;
            }

            var lapCount = _racers.Values
                .OrderByDescending(r => r.Laps)
                .Select(r => r.Laps)
                .FirstOrDefault();

            _stringBuilder.Clear();
            _stringBuilder.AppendLine(lapCount.ToString());

            foreach (var lcd in _lcdGroupLapCounters)
            {
                lcd.WriteText(_stringBuilder);
            }
        }

        private void UpdateWeather()
        {
            if (!ENABLE_WEATHER || _weatherChangeCountdown <= 0 || _raceMode != RaceMode.Race)
            {
                return;
            }

            _weatherChangeCountdown -= (int)Runtime.TimeSinceLastRun.TotalMilliseconds;

            if (_weatherChangeCountdown <= 0)
            {
                _currentWeather = Weather.Rain;
            }
        }

        private void SetupLcds()
        {
            _lcdMain = (IMyTextPanel)GridTerminalSystem.GetBlockWithName(MAIN_LCD_NAME);

            if (_lcdMain == null)
            {
                throw new Exception($"\'{MAIN_LCD_NAME}\' not set!");
            }
            else
            {
                _lcdMain.ContentType = ContentType.TEXT_AND_IMAGE;
                _lcdMain.Alignment = TextAlignment.CENTER;
                _lcdMain.Font = "Monospace";
                _lcdMain.FontSize = 0.67f;
            }

            _lcdLaps = (IMyTextPanel)GridTerminalSystem.GetBlockWithName(LAPS_LCD_NAME);

            if (_lcdLaps != null)
            {
                _lcdLaps.ContentType = ContentType.TEXT_AND_IMAGE;
                _lcdLaps.Alignment = TextAlignment.CENTER;
                _lcdLaps.Font = "Monospace";
                _lcdLaps.FontSize = 0.67f;
            }

            _lcdSpeedtrap = (IMyTextPanel)GridTerminalSystem.GetBlockWithName(SPEEDTRAP_LCD_NAME);
            if (_lcdSpeedtrap != null) _lcdSpeedtrap.ContentType = ContentType.TEXT_AND_IMAGE;

            _lcdGroupFastestLaps = new List<IMyTextPanel>();
            var group = GridTerminalSystem.GetBlockGroupWithName(FASTEST_LAPS_LCD_GROUP_NAME);

            if (group != null)
            {
                group.GetBlocksOfType(_lcdGroupFastestLaps);

                foreach (var l in _lcdGroupFastestLaps)
                {
                    l.ContentType = ContentType.TEXT_AND_IMAGE;
                    l.Alignment = TextAlignment.CENTER;
                    l.Font = "Monospace";
                    l.FontSize = 0.67f;
                }
            }

            _lcdGroupLapCounters = new List<IMyTextPanel>();
            var lcdGroup = GridTerminalSystem.GetBlockGroupWithName(LAP_COUNTERS_GROUP_NAME);

            if (lcdGroup != null)
            {
                lcdGroup.GetBlocksOfType(_lcdGroupLapCounters);

                foreach (var lcd in _lcdGroupLapCounters)
                {
                    lcd.ContentType = ContentType.TEXT_AND_IMAGE;
                    lcd.Alignment = TextAlignment.CENTER;
                    lcd.FontSize = 10f;
                    lcd.TextPadding = 17f;
                }
            }
        }

        private void SetupSensors()
        {
            _startFinishSensor = (IMySensorBlock)GridTerminalSystem.GetBlockWithName(START_FINISH_SENSOR_NAME);

            if (_startFinishSensor == null)
            {
                throw new Exception($"\'{START_FINISH_SENSOR_NAME}\' not set!");
            }

            if (CHECKPOINT_COUNT < 1)
            {
                throw new Exception($"The grid must have at least one checkpoint sensor.");
            }

            for (int i = 1; i <= CHECKPOINT_COUNT; i++)
            {
                var sensorName = CHECKPOINT_SENSOR_PREFIX + i;
                var checkpointSensor = (IMySensorBlock)GridTerminalSystem.GetBlockWithName(sensorName);

                if (checkpointSensor == null)
                {
                    throw new Exception($"\'{sensorName}\' not set!");
                }

                _checkpointSensors.Add(checkpointSensor);
            }

            _pitEntrySensor = (IMySensorBlock)GridTerminalSystem.GetBlockWithName(PIT_ENTRY_SENSOR_NAME);
            _pitExitSensor = (IMySensorBlock)GridTerminalSystem.GetBlockWithName(PIT_EXIT_SENSOR_NAME);
        }

        private void SetupLights()
        {
            if (START_LIGHTS_COUNT < 3)
            {
                throw new Exception($"The grid must have at least 3 start lights.");
            }

            for (int i = 1; i <= START_LIGHTS_COUNT; i++)
            {
                var lightName = START_LIGHTS_PREFIX + i;
                var lightGroup = GridTerminalSystem.GetBlockGroupWithName(lightName);

                if (lightGroup == null)
                {
                    throw new Exception($"\'{lightName}\' not set!");
                }

                var lights = new List<IMyLightingBlock>();
                lightGroup.GetBlocksOfType(lights);

                _startLightGroups.Add(lights);
            }

            var lightList = new List<IMyLightingBlock>();
            var group = GridTerminalSystem.GetBlockGroupWithName(START_LIGHTS_GO);

            if (group != null)
            {
                group.GetBlocksOfType(lightList);
            }

            _startLightGroupGo = lightList;
        }

        private void SetupConsole()
        {
            var screen = Me.GetSurface(0);

            screen.ContentType = ContentType.TEXT_AND_IMAGE;
            screen.ClearImagesFromSelection();
            screen.Alignment = TextAlignment.LEFT;
            screen.Font = "Monospace";
            screen.FontColor = Color.Lime;
            screen.BackgroundColor = Color.Black;
            screen.FontSize = 0.75f;
            screen.TextPadding = 2;

            screen.WriteText("", false);
        }

        private void Reset()
        {
            _racers.Clear();
            _startLightsProtocol = false;
            _raceStartTimeStamp = 0;
            _originalStartTime = 0;
            _startTimeCounter = 0;
            _racePositions.Clear();
            _currentFlag = Flag.Green;
            _currentWeather = Weather.Clear;
            _riskOfRain = INITIAL_RISK_OF_RAIN;
            _weatherChangeCountdown = -1;
            _bestLap = null;

            for (int i = 0; i < _startLightGroups.Count; i++)
            {
                SetLights(i, false);
            }

            _lcdMain.WriteText("", false);

            if (_lcdLaps != null)
            {
                _lcdLaps.WriteText("", false);
            }

            if (_lcdSpeedtrap != null)
            {
                _lcdSpeedtrap.WriteText("", false);
            }

            if (_lcdGroupFastestLaps != null && _lcdGroupLapCounters.Count > 0)
            {
                foreach (var lcd in _lcdGroupLapCounters)
                {
                    lcd.WriteText("", false);
                }
            }

            if (_lcdGroupLapCounters != null && _lcdGroupLapCounters.Count > 0)
            {
                foreach (var lcd in _lcdGroupLapCounters)
                {
                    lcd.WriteText("0", false);
                }
            }

            Me.GetSurface(0).WriteText("", false);
        }

        private void MyEcho(string text)
        {
            Echo(text);
            ConsolePrint(text);
        }

        private void ConsolePrint(string text)
        {
            Me.GetSurface(0).WriteText(text, true);
        }

        private string BuildLine(string key, object value)
        {
            return BuildLine(key, value.ToString());
        }

        private string BuildLine(string key, string value, int maxIdent = 999)
        {
            var ident = MathHelper.Clamp(DISPLAY_WIDTH - value.Length - 2, 0, maxIdent);

            return key.PadRight(ident, '.') + ": " + value;
        }

        private string TruncateString(string input, int length)
        {
            if (length < input.Length)
            {
                return input.Substring(0, length);
            }

            return input;
        }

        private void BroadcastAddress()
        {
            _broadcastCooldown -= (int)Runtime.TimeSinceLastRun.TotalMilliseconds;

            if (_broadcastCooldown <= 0)
            {
                IGC.SendBroadcastMessage("Address", IGC.Me.ToString());
                _broadcastCooldown = BROADCAST_COOLDOWN;
            }
        }

        private void ListenSignals()
        {
            var unisource = IGC.UnicastListener;

            while (unisource.HasPendingMessage)
            {
                var messageUni = unisource.AcceptMessage();

                switch (messageUni.Tag)
                {
                    case "Register":
                        HandleRegisterSignal(messageUni);
                        break;

                    case "Flag":
                        HandleFlagSignal(messageUni);
                        break;

                    default:
                        break;
                }
            }
        }

        private void HandleRegisterSignal(MyIGCMessage messageUni)
        {
            var values = messageUni.Data
                .ToString()
                .Split(';');

            if (values.Length < 2)
            {
                return;
            }

            var name = values[0];
            var address = Convert.ToInt64(values[1]);

            if (_racers.ContainsKey(name))
            {
                _racers[name].IgcAddress = address;
                return;
            }

            if (_pendingAddressTracking.ContainsKey(name))
            {
                _pendingAddressTracking[name] = address;
                return;
            }

            _pendingAddressTracking.Add(name, address);
        }

        private void HandleFlagSignal(MyIGCMessage messageUni)
        {
            var flag = (Flag)Convert.ToInt32(messageUni.Data);
            _currentFlag = flag;
        }

        private void SetLights(int index, bool value)
        {
            var lights = _startLightGroups[index];

            foreach (var l in lights)
            {
                l.Enabled = true;
                l.Color = value ? Color.Red : Color.Black;
            }
        }

        private LapSectorStatus GetSectorStatus(int sectorNumber, TrackedRacer racer)
        {
            var globalBestLap = _bestLap;
            var currLap = racer.CurrentLap;
            var bestLap = racer.BestLap;
            var prevLap = racer.PreviousLap;
            var status = LapSectorStatus.NotSet;

            if (currLap == null)
            {
                return status;
            }

            if (sectorNumber == 1)
            {
                if (currLap.IsFinishedS1)
                {
                    if (currLap.TimeS1 <= globalBestLap?.TimeS1 || _bestLap == null)
                    {
                        return LapSectorStatus.Best;
                    }

                    if (currLap.TimeS1 <= bestLap?.TimeS1)
                    {
                        return LapSectorStatus.Better;
                    }

                    return LapSectorStatus.Worse;
                }
            }
            else if (sectorNumber == 2)
            {
                if (currLap.IsFinishedS2)
                {
                    if (currLap.TimeS2 <= globalBestLap?.TimeS2 || _bestLap == null)
                    {
                        return LapSectorStatus.Best;
                    }

                    if (currLap.TimeS2 <= bestLap?.TimeS2)
                    {
                        return LapSectorStatus.Better;
                    }

                    return LapSectorStatus.Worse;
                }
                else if (prevLap != null && !currLap.IsFinishedS1)
                {
                    if (prevLap.TimeS2 <= globalBestLap?.TimeS2)
                    {
                        return LapSectorStatus.Best;
                    }

                    if (prevLap.TimeS2 <= bestLap?.TimeS2)
                    {
                        return LapSectorStatus.Better;
                    }

                    return LapSectorStatus.Worse;
                }
            }
            else if (sectorNumber == 3)
            {
                if (prevLap != null && prevLap.IsFinishedS3 && (!currLap.IsFinishedS1 || !currLap.IsFinishedS2))
                {
                    if (prevLap.TimeS3 <= globalBestLap?.TimeS3)
                    {
                        return LapSectorStatus.Best;
                    }

                    if (prevLap.TimeS3 <= bestLap?.TimeS3)
                    {
                        return LapSectorStatus.Better;
                    }

                    return LapSectorStatus.Worse;
                }
            }

            return status;
        }
    }
}