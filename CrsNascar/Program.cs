using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    class Program : MyGridProgram
    {
        #region mdk preserve

        private readonly string DRIVER_NAME = "Guest";
        private readonly int DRIVER_NUMBER = 99;
        private const float DEFAULT_SUSPENSION_STRENGTH = 10f;
        private const string DISPLAY_NAME = "Driver LCD";
        private const string BRAKELIGHT_GROUP_NAME = "Brakelight";
        private const string DRAFTING_SENSOR_NAME = "Drafting Sensor";
        private readonly int? COCKPIT_DISPLAY_INDEX = null;                     //If you wanna use a cockpit display to show dashboard info (0, 1, 2, 3 or null);
        private readonly Color DEFAULT_FONT_COLOR = new Color(127, 127, 127);   //Font Color (R, G, B)

        #endregion

        //************ DO NOT MODIFY BELLOW HERE ************

        enum TyreCompound
        {
            Prime  //100% ~20 minutes
        }

        enum Flag
        {
            Green,
            Yellow,
            Red,
            Blue
        }

        enum FuelMode
        {
            Eco,
            Standard,
            Max
        }

        private class RaceData
        {
            public int Position { get; set; }
            public int TotalRacers { get; set; }
            public int Laps { get; set; }
            public int TotalLaps { get; set; }
            public string CurrentLapTime { get; set; } = "--:--.---";
            public string BestLapTime { get; set; } = "--:--.---";
            public Flag CurrentFlag { get; set; } = Flag.Green;

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
                }
                catch (Exception) { }
            }
        }

        private readonly string CODE_VERSION = "2.0.0";
        private const int CONNECTION_TIMEOUT = 3000;
        private const int TIRE_SAVING_COOLDOWN = 1000;
        private const int DRAFTING_COOLDOWN = 1000;
        private const float DEFAULT_SUSPENSION_POWER = 80f;
        private const float DEFAULT_SUSPENSION_SPEED_LIMIT = 96f;
        private const char ARROW_UP_CHAR = '\u2191';
        private const char ARROW_DOWN_CHAR = '\u2193';
        private const char BLOCK_FILLED_CHAR = '\u2588';
        private const char BLOCK_HALF_CHAR = '\u2592';
        private const char BLOCK_EMPTY_CHAR = '\u2591';
        private readonly Color BRAKE_COLOR_OFF = new Color(32, 0, 0);
        private List<IMyMotorSuspension> _suspensions;
        private IMyCockpit _mainController;
        private List<IMyTextSurface> _displays;
        private IMyRadioAntenna _antenna;
        private IMySensorBlock _draftingSensor;
        private bool _isPitLimiterActive;
        private StringBuilder _stringBuilder;
        private RaceData _data;
        private List<IMyLightingBlock> _brakelights;
        private TyreCompound _currentTyreCompound;
        private float _minTyreFriction = 0;
        private float _maxTyreFriction = 100;
        private float _tyreWearFactor = 1;
        private float _friction = 100;
        private long _address = -1;
        private IMyBroadcastListener _broadcastListener;
        private int _connectionTimeout;
        private int _tyreSavingCooldown;
        private DateTime _lastTimeStamp;
        private float _delta;
        private FuelMode _fuelMode = FuelMode.Standard;
        private float _fuelAmount = 1f;
        private bool _isRefueling = false;
        private bool _isDrafting = false;
        private int _draftingCooldown;

        public Program()
        {
            _data = new RaceData();

            SetupGridName();
            SetupController();
            SetupSuspensions();
            SetupDisplay();
            SetupBrakelights();
            SetupAntenna();
            SetupDraftingSensor();
            LoadState();
            SetupBroadcastListener();

            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            _lastTimeStamp = DateTime.Now;
        }

        private void SetupDraftingSensor()
        {
            var sensor = (IMySensorBlock)GridTerminalSystem.GetBlockWithName(DRAFTING_SENSOR_NAME);

            if (sensor == null)
            {
                return;
            }

            _draftingSensor = sensor;
            _draftingSensor.TopExtend = 50;
            _draftingSensor.BottomExtend = 0;
            _draftingSensor.RightExtend = 2.5f;
            _draftingSensor.LeftExtend = 2.5f;
            _draftingSensor.FrontExtend = 0;
            _draftingSensor.BackExtend = 1;
            _draftingSensor.DetectSmallShips = true;
            _draftingSensor.DetectLargeShips = false;
            _draftingSensor.DetectStations = false;
            _draftingSensor.DetectSubgrids = false;
            _draftingSensor.DetectAsteroids = false;
            _draftingSensor.DetectPlayers = false;
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
            var currentTimeStamp = DateTime.Now;
            _delta = (float)(currentTimeStamp - _lastTimeStamp).TotalMilliseconds / 1000;

            Echo($"Running FSESS-Nascar {CODE_VERSION}");

            HandleArgument(argument);
            UpdateFuel();
            UpdateDraftingSensor();
            UpdatePitLimiter();
            UpdateTyreDegradation();
            UpdateBrakelights();
            UpdateCommunication();
            UpdateDisplays();
            UpdateAntenna();

            _lastTimeStamp = currentTimeStamp;
        }

        private void UpdateBrakelights()
        {
            var isBreaking = _mainController.MoveIndicator.Z > 0
                || _mainController.MoveIndicator.Y > 0
                || _mainController.HandBrake;

            foreach (var l in _brakelights)
            {
                l.Color = isBreaking ? Color.Red : BRAKE_COLOR_OFF;
            }
        }

        private void UpdateDraftingSensor()
        {
            if (_draftingSensor == null)
            {
                return;
            }

            var isBehindCar = !_draftingSensor.Closed
                && _draftingSensor.IsActive
                && _mainController.GetShipSpeed() >= 60;

            if (isBehindCar)
            {
                _draftingCooldown = DRAFTING_COOLDOWN;
            }

            if (_isPitLimiterActive)
            {
                _draftingCooldown = 0;
            }

            _isDrafting = _draftingCooldown > 0;

            foreach (var s in _suspensions)
            {
                var speedLimit = GetWheeSpeedLimit();
                s.SetValueFloat("Speed Limit", speedLimit * 3.6f);
                s.Power = GetWheelPower();
                s.Strength += _isDrafting
                    ? (100f / 2) * _delta
                    : -(100f / 2) * _delta;

                s.Strength = MathHelper.Clamp(s.Strength, DEFAULT_SUSPENSION_STRENGTH, 100);
            }

            _draftingCooldown -= (int)(_delta * 1000);
        }

        private void UpdateCommunication()
        {
            var unisource = IGC.UnicastListener;

            if (!unisource.HasPendingMessage)
            {
                _connectionTimeout -= (int)(_delta * 1000);

                if (_broadcastListener.HasPendingMessage && _connectionTimeout <= 0)
                {
                    var message = _broadcastListener.AcceptMessage();

                    if (message.Tag == "Address")
                    {
                        _address = Convert.ToInt64(message.Data.ToString());
                        IGC.SendUnicastMessage(_address, "Register", $"{Me.CubeGrid.CustomName};{IGC.Me}");
                    }
                }

                return;
            }

            while (unisource.HasPendingMessage)
            {
                var messageUni = unisource.AcceptMessage();

                if (messageUni.Tag == "RaceData")
                {
                    _data.Map(messageUni.Data.ToString());
                }

                if (messageUni.Tag == "Argument")
                {
                    HandleArgument(messageUni.Data.ToString());
                }
            }

            _connectionTimeout = CONNECTION_TIMEOUT;
        }

        private void UpdateDisplays()
        {
            _stringBuilder.Clear();

            var speed = _mainController.GetShipSpeed();
            var tyreWear = ((_friction - _minTyreFriction) / (_maxTyreFriction - _minTyreFriction)) * 100f;
            var tyreCompoundIndicator = GetCurrentTyreChar();
            var strTyreWear = ((int)Math.Floor(tyreWear)).ToString();
            var strSpeed = $"{speed:F0}m/s";
            var strFuelMode = $"MODE: {GetFuelModeName()}".PadLeft(20 - strSpeed.Length, ' ');
            var ersBar = BuildBar();
            var fuelAmount = $"{(int)Math.Floor(_fuelAmount * 100),3}%";
            var message = _isPitLimiterActive ? "PIT LIMITER" : _isDrafting ? "DRAFTING" : "";

            _stringBuilder.AppendLine($"{strSpeed}{strFuelMode}");
            _stringBuilder.AppendLine(message);
            _stringBuilder.AppendLine($"FUEL {ersBar} {fuelAmount}");
            _stringBuilder.AppendLine($"P:{_data.Position:00}/{_data.TotalRacers:00}      L:{(_data.Laps):00}/{_data.TotalLaps:00}");
            _stringBuilder.AppendLine($"TYRE .........: {strTyreWear,3}%");
            _stringBuilder.AppendLine($"TIME.....: {_data.CurrentLapTime}");
            _stringBuilder.AppendLine($"BEST.....: {_data.BestLapTime}");

            if (_connectionTimeout <= 0)
            {
                _stringBuilder.AppendLine($"NO CONNECTION");
            }

            foreach (var d in _displays)
            {
                d.WriteText(_stringBuilder);

                var bgColor = Color.Black;
                var fontColor = DEFAULT_FONT_COLOR;

                switch (_data.CurrentFlag)
                {
                    case Flag.Yellow:
                        bgColor = Color.Yellow;
                        fontColor = Color.Black;
                        break;

                    case Flag.Red:
                        bgColor = Color.Red;
                        fontColor = Color.White;
                        break;

                    case Flag.Blue:
                        bgColor = Color.Blue;
                        fontColor = Color.White;
                        break;
                }

                d.BackgroundColor = bgColor;
                d.ScriptBackgroundColor = bgColor;
                d.FontColor = fontColor;
            }
        }

        private void UpdatePitLimiter()
        {
            if (!_isPitLimiterActive)
            {
                return;
            }

            foreach (var s in _suspensions)
            {
                s.Power = GetWheelPower();
                s.SetValueFloat("Speed Limit", 26f * 3.6f);
            }

            var speed = _mainController.GetShipSpeed();
            _mainController.HandBrake = speed > 24;
        }

        private void UpdateFuel()
        {
            var throttle = _mainController.MoveIndicator.Z < 0;
            var speed = _mainController.GetShipSpeed();
            var consumption = GetFuelModeConsumption();

            if (speed > 1)
            {
                _isRefueling = false;

                if (throttle)
                {
                    _fuelAmount -= consumption * (1f / (60 * 15)) * _delta;
                }
            }
            else if (_isPitLimiterActive && _isRefueling)
            {
                _fuelAmount += (1f / 20) * _delta;
            }

            _fuelAmount = MathHelper.Clamp(_fuelAmount, 0, 1);

            foreach (var s in _suspensions)
            {
                s.Propulsion = _fuelAmount > 0;
            }
        }

        private void UpdateTyreDegradation()
        {
            var speed = _mainController.GetShipSpeed();

            if (speed < 1)
            {
                return;
            }

            var speedFactor = (float)MathHelper.Clamp(speed, 0, 90) / 90;
            var wearRate = _tyreWearFactor * speedFactor * _delta;

            _friction -= wearRate;
            _friction = MathHelper.Clamp(_friction, _minTyreFriction, _maxTyreFriction);

            foreach (var s in _suspensions)
            {
                s.Friction = _friction;
            }

            if (_friction <= _minTyreFriction)
            {
                if (_suspensions.All(s => s.IsAttached))
                {
                    var rand = new Random().Next(4);
                    _suspensions[rand].Detach();
                }
            }

            SaveState();
        }

        private void UpdateAntenna()
        {
            if (_antenna == null)
            {
                return;
            }

            _antenna.HudText = $"P{_data.Position}";
        }

        private void SetupGridName()
        {
            if (DRIVER_NUMBER <= 0 && DRIVER_NUMBER > 99)
            {
                throw new Exception("DRIVER_NUMBER should be between 1 and 99");
            }

            Me.CubeGrid.CustomName = $"{DRIVER_NUMBER:00}-{DRIVER_NAME.Trim()}";
        }

        private void SetupController()
        {
            var controllerList = new List<IMyCockpit>();
            GridTerminalSystem.GetBlocksOfType(controllerList);
            _mainController = controllerList.FirstOrDefault();

            if (_mainController == null)
            {
                throw new Exception("No cockpit!");
            }
        }

        private void SetupSuspensions()
        {
            _suspensions = new List<IMyMotorSuspension>();
            GridTerminalSystem.GetBlocksOfType(_suspensions, s => s.CubeGrid == Me.CubeGrid);

            if (_suspensions == null || _suspensions.Count != 4)
            {
                throw new Exception("Need 4 suspensions!");
            }
        }

        private void SetupDisplay()
        {
            _stringBuilder = new StringBuilder();
            _displays = new List<IMyTextSurface> { Me.GetSurface(0) };

            var display = (IMyTextSurface)GridTerminalSystem.GetBlockWithName(DISPLAY_NAME);

            if (display != null)
            {
                _displays.Add(display);
            }

            if (COCKPIT_DISPLAY_INDEX.HasValue)
            {
                var d = _mainController.GetSurface(COCKPIT_DISPLAY_INDEX.GetValueOrDefault());

                if (d != null)
                {
                    _displays.Add(d);
                }
            }

            foreach (var d in _displays)
            {
                d.ContentType = ContentType.TEXT_AND_IMAGE;
                d.Alignment = TextAlignment.CENTER;
                d.Font = "Monospace";
            }
        }

        private void SetupBrakelights()
        {
            var lights = new List<IMyTerminalBlock>();

            GridTerminalSystem.GetBlockGroupWithName(BRAKELIGHT_GROUP_NAME)
                .GetBlocks(lights, b => b.CubeGrid == Me.CubeGrid);

            _brakelights = new List<IMyLightingBlock>();

            foreach (var l in lights)
            {
                var light = (IMyLightingBlock)l;
                light.Enabled = true;
                light.Intensity = 10f;
                light.BlinkLength = 0f;
                light.BlinkIntervalSeconds = 0f;
                light.Color = Color.Black;

                _brakelights.Add(light);
            }
        }

        private void LoadState()
        {
            if (string.IsNullOrWhiteSpace(Me.CustomData))
            {
                SetTyres(TyreCompound.Prime);
                return;
            }

            var values = Me.CustomData.Split(';');

            if (values.Length < 3)
            {
                SetTyres(TyreCompound.Prime);
                return;
            }

            var compoundChar = Convert.ToChar(values[0]);
            var friction = (float)Convert.ToDouble(values[1]);
            var charge = (float)Convert.ToDouble(values[2]);

            switch (compoundChar)
            {
                case 'P': SetTyres(TyreCompound.Prime); break;
                default: SetTyres(TyreCompound.Prime); break;
            }

            _friction = friction;
            _fuelAmount = charge;
        }

        private void SetupAntenna()
        {
            var antennas = new List<IMyRadioAntenna>();
            GridTerminalSystem.GetBlocksOfType(antennas);
            var antenna = antennas.FirstOrDefault();

            if (antenna == null)
            {
                return;
            }

            antenna.Enabled = true;
            antenna.Radius = 5000;
            antenna.EnableBroadcasting = true;
            antenna.HudText = $"(P{_data.Position}) {DRIVER_NAME}-{DRIVER_NUMBER}";
            _antenna = antenna;
        }

        private void SetupBroadcastListener()
        {
            IGC.RegisterBroadcastListener("Address");
            var listeners = new List<IMyBroadcastListener>();
            IGC.GetBroadcastListeners(listeners);
            _broadcastListener = listeners.FirstOrDefault();
        }

        private void HandleArgument(string argument)
        {
            if (argument.Equals("LMT", StringComparison.InvariantCultureIgnoreCase))
            {
                _isPitLimiterActive = !_isPitLimiterActive;
                return;
            }

            if (argument.Equals("LMT_ON", StringComparison.InvariantCultureIgnoreCase))
            {
                _isPitLimiterActive = true;
                return;
            }

            if (argument.Equals("LMT_OFF", StringComparison.InvariantCultureIgnoreCase))
            {
                _isPitLimiterActive = false;
                return;
            }

            if (argument.Equals("PIT", StringComparison.InvariantCultureIgnoreCase))
            {
                ChangeTyres(TyreCompound.Prime);
                return;
            }

            if (argument.Equals("ECO", StringComparison.InvariantCultureIgnoreCase))
            {
                _fuelMode = FuelMode.Eco;
                return;
            }

            if (argument.Equals("STD", StringComparison.InvariantCultureIgnoreCase))
            {
                _fuelMode = FuelMode.Standard;
                return;
            }

            if (argument.Equals("MAX", StringComparison.InvariantCultureIgnoreCase))
            {
                _fuelMode = FuelMode.Max;
                return;
            }
        }

        private void ChangeTyres(TyreCompound compound)
        {
            if (!_isPitLimiterActive || _mainController.GetShipSpeed() > 1)
            {
                return;
            }

            _isRefueling = true;
            SetTyres(compound);
            SaveState(true);
        }

        private void SetTyres(TyreCompound compound)
        {
            switch (compound)
            {
                case TyreCompound.Prime:
                    _maxTyreFriction = 75;
                    _minTyreFriction = 43.75f;
                    _tyreWearFactor = (_maxTyreFriction - _minTyreFriction) / (60 * 18.75f);
                    break;

                default:
                    break;
            }

            _friction = _maxTyreFriction;
            _currentTyreCompound = compound;

            foreach (var s in _suspensions)
            {
                s.ApplyAction("Add Top Part");
            }
        }

        private void SetBrakelightColor(Color color)
        {
            foreach (var l in _brakelights)
            {
                var light = (IMyLightingBlock)l;
                light.Color = color;
            }
        }

        private void SetSpeedLimit(float metersPerSecond)
        {
            foreach (var s in _suspensions)
            {
                s.SetValueFloat("Speed Limit", metersPerSecond * 3.6f);
            }
        }

        private void SaveState(bool force = false)
        {
            _tyreSavingCooldown -= (int)(_delta * 1000);

            if (!force && _tyreSavingCooldown > 0)
            {
                return;
            }

            var tyreChar = GetCurrentTyreChar();

            Me.CustomData = $"{tyreChar};{_friction};{_fuelAmount}";
            _tyreSavingCooldown = TIRE_SAVING_COOLDOWN;
        }

        private char GetCurrentTyreChar()
        {
            var tyreChar = 'P';

            switch (_currentTyreCompound)
            {
                case TyreCompound.Prime: tyreChar = 'P'; break;
            }

            return tyreChar;
        }

        private string GetCurrentFlagName()
        {
            var flagName = string.Empty;

            switch (_data.CurrentFlag)
            {
                case Flag.Blue: flagName = "Blue"; break;
                case Flag.Green: flagName = "Green"; break;
                case Flag.Red: flagName = "Red"; break;
                case Flag.Yellow: flagName = "Yellow"; break;
            }

            return flagName;
        }

        private Color GetCurrentFlagColor()
        {
            var color = Color.Black;

            switch (_data.CurrentFlag)
            {
                case Flag.Blue: color = Color.Blue; break;
                case Flag.Green: color = Color.Green; break;
                case Flag.Red: color = Color.Red; break;
                case Flag.Yellow: color = Color.Yellow; break;
            }

            return color;
        }

        private string GetFuelModeName()
        {
            switch (_fuelMode)
            {
                case FuelMode.Eco:
                    return "ECO";
                case FuelMode.Standard:
                    return "STD";
                case FuelMode.Max:
                    return "MAX";
                default:
                    return "STD";
            }
        }

        private float GetFuelModeConsumption()
        {
            switch (_fuelMode)
            {
                case FuelMode.Eco:
                    return 0.8f;
                case FuelMode.Standard:
                    return 1f;
                case FuelMode.Max:
                    return 2f;
                default:
                    return 1f;
            }
        }

        private string BuildBar()
        {
            var strBar = string.Empty;
            const int barLength = 10;

            for (int i = 0; i < barLength; i++)
            {
                var factor = 1f / barLength;

                if (_fuelAmount > factor * i)
                {
                    if (_fuelAmount < factor * (i + 1))
                    {
                        strBar += BLOCK_HALF_CHAR;
                        continue;
                    }

                    strBar += BLOCK_FILLED_CHAR;
                }
                else
                {
                    strBar += BLOCK_EMPTY_CHAR;
                }
            }

            return strBar;
        }

        private float GetWheelPower()
        {
            if (_isPitLimiterActive)
            {
                return 20f;
            }

            if (_isDrafting)
            {
                return 100f;
            }

            switch (_fuelMode)
            {
                case FuelMode.Eco:
                    return 60f;
                case FuelMode.Standard:
                    return 80f;
                case FuelMode.Max:
                    return 100f;
                default:
                    return DEFAULT_SUSPENSION_POWER;
            }
        }

        private float GetWheeSpeedLimit()
        {
            if (_isPitLimiterActive)
            {
                return 26;
            }

            if (_data.CurrentFlag == Flag.Yellow)
            {
                return 45;
            }

            if (_isDrafting)
            {
                return 999;
            }

            switch (_fuelMode)
            {
                case FuelMode.Eco: return 95f;
                case FuelMode.Max: return 98f;
                case FuelMode.Standard:
                default: return DEFAULT_SUSPENSION_SPEED_LIMIT;
            }
        }
    }
}
