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

        private readonly string TEAM_TAG = "XXX";                               //Your Team Tag (3 chracters), if you are not in a team yet, keep this as it is.
        private readonly string DRIVER_NAME = "Guest";                          //Your name
        private readonly int DRIVER_NUMBER = 99;                                //Your number (0-99)
        private const string DISPLAY_NAME = "Driver LCD";
        private const string MIRROR_SENSOR_RIGHT_NAME = "Mirror Sensor Right";
        private const string MIRROR_SENSOR_LEFT_NAME = "Mirror Sensor Left";
        private readonly int? COCKPIT_DISPLAY_INDEX = null;                     //If you wanna use a cockpit display to show dashboard info (0, 1, 2, 3 or null)
        private readonly Color DEFAULT_FONT_COLOR = new Color(255, 255, 255);   //Font Color (R, G, B)
        private const string TEXT_DISPLAY_NAME = "Text LCD";                    //Optional Text-Based LCD, for HudLcd Plugin
        private const string RANK_DISPLAY_NAME = "Rank LCD";                    //Optional Text-Based LCD, for HudLcd Plugin
        private const string TEXT_DISPLAY_HUDLCD = "hudlcd:-0.7:-0.35:0.9:White:1";
        private const string RANK_DISPLAY_HUDLCD = "hudlcd:0.45:0.9:1:White:1";

        #endregion

        //************ DO NOT MODIFY BELLOW HERE ************

        private readonly string CODE_VERSION = "12.5.0";
        private const int CONNECTION_TIMEOUT = 3000;
        private readonly char ARROW_DOWN_CHAR = '\u25BC';
        private readonly char ARROW_UP_CHAR = '\u25B2';
        private readonly char ARROW_RIGHT = '\u25BA';
        private readonly char ARROW_LEFT = '\u25C4';
        private const char BLOCK_FILLED_CHAR = '\u2588';
        private const char BLOCK_HALF_CHAR = '\u2592';
        private const char BLOCK_EMPTY_CHAR = '\u2591';
        private bool _hasError;
        private IMyShipController _mainController;
        private List<IMyTextSurface> _displays;
        private IMyTextSurface _cockpitDisplay;
        private IMyTextSurface _textDisplay;
        private IMyTextSurface _rankDisplay;
        private IMyRadioAntenna _antenna;
        private IMySensorBlock _mirrorRight;
        private IMySensorBlock _mirrorLeft;
        private List<IMyGyro> _gyros;
        private StringBuilder _stringBuilder;
        private RaceData _data;
        private long _address = -1;
        private IMyBroadcastListener _broadcastListener;
        private int _connectionTimeout;
        private DateTime _lastTimeStamp;
        private float _delta;
        private bool _doFlip;
        private List<MyDetectedEntityInfo> _mirrorAuxList;
        private List<MyDetectedEntityInfo> _draftingAuxList;
        private CharacterAnimation _spinnerAnim;

        public Program()
        {
            _data = new RaceData();
            _spinnerAnim = new CharacterAnimation(new char[] { '-', '\\', '|', '/' }, 150);

            try
            {
                SetupGridName();
                SetupController();
                SetupDisplays();
                SetupAntenna();
                SetupMirrors();
                SetupBroadcastListener();
                SetupGyros();
            }
            catch (Exception ex)
            {
                _hasError = true;
                Echo(ex.Message);
                return;
            }

            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            _lastTimeStamp = DateTime.Now;
        }

        private void SetupMirrors()
        {
            _mirrorAuxList = new List<MyDetectedEntityInfo>();
            _mirrorRight = (IMySensorBlock)GridTerminalSystem.GetBlockWithName(MIRROR_SENSOR_RIGHT_NAME);

            if (_mirrorRight != null)
            {
                _mirrorRight.DetectSmallShips = true;
                _mirrorRight.DetectLargeShips = false;
                _mirrorRight.DetectPlayers = false;
                _mirrorRight.LeftExtend = 12.5f;
                _mirrorRight.RightExtend = 0.5f;
                _mirrorRight.FrontExtend = 50;
                _mirrorRight.BackExtend = 5;
                _mirrorRight.TopExtend = 5;
                _mirrorRight.BottomExtend = 5;
            }

            _mirrorLeft = (IMySensorBlock)GridTerminalSystem.GetBlockWithName(MIRROR_SENSOR_LEFT_NAME);

            if (_mirrorLeft != null)
            {
                _mirrorLeft.DetectSmallShips = true;
                _mirrorLeft.DetectLargeShips = false;
                _mirrorLeft.DetectPlayers = false;
                _mirrorLeft.LeftExtend = 0.5f;
                _mirrorLeft.RightExtend = 12.5f;
                _mirrorLeft.FrontExtend = 50;
                _mirrorLeft.BackExtend = 5;
                _mirrorLeft.TopExtend = 5;
                _mirrorLeft.BottomExtend = 5;
            }
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
            if (_hasError)
            {
                return;
            }

            var currentTimeStamp = DateTime.Now;
            _delta = (float)(currentTimeStamp - _lastTimeStamp).TotalMilliseconds / 1000;

            Echo($"Running FSESS {CODE_VERSION}");

            HandleArgument(argument);
            UpdateCommunication();
            UpdateDisplays();
            UpdateAntenna();
            UpdateGyros();

            _lastTimeStamp = currentTimeStamp;
        }

        private void UpdateGyros()
        {
            if (!_doFlip)
            {
                return;
            }

            // The dot product is only positive when the car is flipped over
            if (Vector3D.Dot(_mainController.GetNaturalGravity(), _mainController.WorldMatrix.Up) > 0)
            {
                for (int i = 0; i < _gyros.Count; i++)
                {
                    _gyros[i].GyroOverride = true;
                }

                return;
            }

            // Simply disables gyro override if it's on
            if (_gyros[0].GyroOverride)
            {
                for (int i = 0; i < _gyros.Count; i++)
                {
                    _gyros[i].GyroOverride = false;
                }

                _doFlip = false;
            }
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

            const int DISPLAY_WIDTH = 21;
            const int DISPLAY_HEIGHT = 8;
            const int INNER_DISPLAY_WIDTH = DISPLAY_WIDTH - 6;
            var charBuffer = new char[DISPLAY_WIDTH, DISPLAY_HEIGHT];
            var speed = _mainController.GetShipSpeed();
            var strSpeed = $"{Math.Floor(speed)}m/s";
            var controlLine = $"                     ";

            var rightProximity = GetMirrorProximityArrows(true);
            var leftProximity = GetMirrorProximityArrows(false);
            var innerLine = leftProximity.PadRight((int)Math.Ceiling((float)INNER_DISPLAY_WIDTH / 2) - (int)Math.Ceiling((float)strSpeed.Length / 2))
                + strSpeed +
                rightProximity.PadLeft((int)Math.Floor((float)INNER_DISPLAY_WIDTH / 2) - (int)Math.Floor((float)strSpeed.Length / 2));

            _spinnerAnim.Update(_delta);

            var strS1 = $"S1{GetSectorStatusChar(_data.StatusS1)}";
            var strS2 = $"S2{GetSectorStatusChar(_data.StatusS2)}";
            var strS3 = $"S3{GetSectorStatusChar(_data.StatusS3)}";

            var lines = new string[]
            {
                $"{innerLine}",
                $"{controlLine}",
                $"P:{_data.Position:00}/{_data.TotalRacers:00} L:{(_data.Laps):00}/{_data.TotalLaps:00}",
                $"TIME: {_data.CurrentLapTime}",
                $"BEST: {_data.BestLapTime}",
                $"PREV: {_data.PrevLapTime}",
                $"               ",
                (_connectionTimeout <= 0)
                    ? $" NO CONNECTION "
                    : $"               "
            };

            for (int i = 0; i < lines.Length; i++)
            {
                _stringBuilder.AppendLine(lines[i]);
            }

            var text = _stringBuilder.ToString();

            foreach (var d in _displays)
            {
                var frame = d.DrawFrame();
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

                var scale = d.SurfaceSize.X / 256;
                var textScale = scale * 0.6f;
                var textSprite = MySprite.CreateText(text, "Monospace", fontColor, textScale);
                textSprite.Position = new Vector2(128 * scale, 18 * scale);
                frame.Add(textSprite);

                //Sectors
                var textS1 = MySprite.CreateText($"{strS1}", "Monospace", GetSectorStatusColor(_data.StatusS1), textScale);
                textS1.Position = new Vector2(128 - 64 + 4 + 1, 128 - 4 - 2);

                var textS2 = MySprite.CreateText($"{strS2}", "Monospace", GetSectorStatusColor(_data.StatusS2), textScale);
                textS2.Position = new Vector2(128, 128 - 4 - 2);

                var textS3 = MySprite.CreateText($"{strS3}", "Monospace", GetSectorStatusColor(_data.StatusS3), textScale);
                textS3.Position = new Vector2(128 + 64 - 4 - 2, 128 - 4 - 2);

                textS1.Position *= scale;
                textS1.Size *= scale;
                frame.Add(textS1);

                textS2.Position *= scale;
                textS2.Size *= scale;
                frame.Add(textS2);

                textS3.Position *= scale;
                textS3.Size *= scale;
                frame.Add(textS3);

                frame.Dispose();
            }

            _stringBuilder.Clear();
            lines[6] = $"    {strS1}  {strS2}  {strS3}    ";

            for (int i = 0; i < lines.Length; i++)
            {
                _stringBuilder.AppendLine($"{lines[i]}");
            }

            text = _stringBuilder.ToString();
            _textDisplay?.WriteText(text);
            _cockpitDisplay?.WriteText(text);

            if (_rankDisplay != null && _data != null && _data.RankTable != null)
            {
                _rankDisplay.WriteText(_data.RankTable);
            }
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

            var teamTag = TEAM_TAG;

            if (TEAM_TAG == string.Empty)
            {
                teamTag = "XXX";
            }

            teamTag = teamTag.Trim()
                .Substring(0, 3)
                .ToUpper();

            Me.CubeGrid.CustomName = $"{teamTag} #{DRIVER_NUMBER:00}-{DRIVER_NAME.Trim()}";
        }

        private void SetupController()
        {
            var controllerList = new List<IMyShipController>();
            GridTerminalSystem.GetBlocksOfType(controllerList);

            var control = controllerList.FirstOrDefault(c => c is IMyRemoteControl)
                ?? controllerList.FirstOrDefault(c => c is IMyCockpit);

            if (control == null)
            {
                throw new Exception("No cockpit or remote control!");
            }

            _mainController = control;
        }

        private void SetupDisplays()
        {
            _stringBuilder = new StringBuilder();
            _displays = new List<IMyTextSurface> { Me.GetSurface(0) };

            var display = (IMyTextSurface)GridTerminalSystem.GetBlockWithName(DISPLAY_NAME);

            if (display != null)
            {
                _displays.Add(display);
            }

            foreach (var d in _displays)
            {
                d.ContentType = ContentType.SCRIPT;
                d.Alignment = TextAlignment.CENTER;
                d.Script = string.Empty;
            }

            if (COCKPIT_DISPLAY_INDEX.HasValue)
            {
                var cockpit = _mainController as IMyCockpit;

                if (cockpit != null)
                {
                    var d = cockpit.GetSurface(COCKPIT_DISPLAY_INDEX.GetValueOrDefault());

                    if (d != null)
                    {
                        d.WriteText(string.Empty);
                        d.ContentType = ContentType.TEXT_AND_IMAGE;
                        d.Alignment = TextAlignment.CENTER;
                        d.Font = "Monospace";

                        _cockpitDisplay = d;
                    }
                }
            }

            var textDisplay = (IMyTextSurface)GridTerminalSystem.GetBlockWithName(TEXT_DISPLAY_NAME);

            if (textDisplay != null)
            {
                textDisplay.WriteText(string.Empty);
                textDisplay.ContentType = ContentType.TEXT_AND_IMAGE;
                textDisplay.Alignment = TextAlignment.CENTER;
                textDisplay.Font = "Monospace";
                ((IMyTerminalBlock)textDisplay).CustomData = TEXT_DISPLAY_HUDLCD;

                _textDisplay = textDisplay;
            }

            var rankDisplay = (IMyTextSurface)GridTerminalSystem.GetBlockWithName(RANK_DISPLAY_NAME);

            if (rankDisplay != null)
            {
                rankDisplay.WriteText(string.Empty);
                rankDisplay.ContentType = ContentType.TEXT_AND_IMAGE;
                rankDisplay.Alignment = TextAlignment.CENTER;
                rankDisplay.Font = "Monospace";
                ((IMyTerminalBlock)rankDisplay).CustomData = RANK_DISPLAY_HUDLCD;

                _rankDisplay = rankDisplay;
            }
        }

        private void SetupGyros()
        {
            var gyros = new List<IMyGyro>();

            GridTerminalSystem.GetBlocksOfType(gyros, x => x.CubeGrid == Me.CubeGrid);

            if (gyros.Count <= 0)
            {
                throw new Exception("No gyroscope found.");
            }

            _gyros = gyros;
        }

        private void SetupAntenna()
        {
            var antennas = new List<IMyRadioAntenna>();
            GridTerminalSystem.GetBlocksOfType(antennas);
            var antenna = antennas.FirstOrDefault();

            if (antenna == null)
            {
                throw new Exception("No antenna!");
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
            if (argument.Equals("FLIP", StringComparison.InvariantCultureIgnoreCase))
            {
                _doFlip = true;
                return;
            }

            if (argument.Equals("FLAG_G", StringComparison.InvariantCultureIgnoreCase))
            {
                RequestFlag(Flag.Green);
                return;
            }

            if (argument.Equals("FLAG_Y", StringComparison.InvariantCultureIgnoreCase))
            {
                RequestFlag(Flag.Yellow);
                return;
            }

            if (argument.Equals("FLAG_R", StringComparison.InvariantCultureIgnoreCase))
            {
                RequestFlag(Flag.Red);
                return;
            }
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

        private void RequestFlag(Flag flag)
        {
            if (_address <= 0)
            {
                return;
            }

            IGC.SendUnicastMessage(_address, "Flag", $"{(int)flag}");
        }

        private float GetMirrorProximity(IMySensorBlock mirrorSensor)
        {
            if (mirrorSensor == null || mirrorSensor.Closed || !mirrorSensor.IsActive)
            {
                return float.MaxValue;
            }

            _mirrorAuxList.Clear();
            mirrorSensor.DetectedEntities(_mirrorAuxList);

            if (_mirrorAuxList.Count <= 0)
            {
                return float.MaxValue;
            }

            var pos = Me.CubeGrid.GetPosition();
            var nearest = _mirrorAuxList.Select(x => Vector3.Distance(pos, x.Position))
                .Min();

            return nearest;
        }

        private string GetMirrorProximityArrows(bool isRightMirror)
        {
            var prox = GetMirrorProximity(isRightMirror ? _mirrorRight : _mirrorLeft);

            if (prox == float.MaxValue)
            {
                return string.Empty;
            }

            var arrow = isRightMirror ? ARROW_RIGHT : ARROW_LEFT;

            if (prox < 15)
            {
                return $"{arrow}{arrow}{arrow}";
            }

            if (prox < 30)
            {
                return $"{arrow}{arrow}";
            }

            return $"{arrow}";
        }

        private char GetSectorStatusChar(LapSectorStatus sectorStatus)
        {
            switch (sectorStatus)
            {
                case LapSectorStatus.NotSet: return _spinnerAnim.CurrentChar;
                case LapSectorStatus.Worse: return '-';
                case LapSectorStatus.Better: return '+';
                case LapSectorStatus.Best: return '*';
                default: return _spinnerAnim.CurrentChar;
            }
        }

        private Color GetSectorStatusColor(LapSectorStatus sectorStatus)
        {
            switch (sectorStatus)
            {
                case LapSectorStatus.NotSet: return Color.White;
                case LapSectorStatus.Worse: return Color.Yellow;
                case LapSectorStatus.Better: return Color.Lime;
                case LapSectorStatus.Best: return Color.Magenta;
                default: return Color.White;
            }
        }
    }
}
