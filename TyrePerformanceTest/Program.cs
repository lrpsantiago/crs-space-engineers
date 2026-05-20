using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        private const string LCD_NAME = "Tyre Test LCD";

        private const double ACCEL_START_SPEED = 1.0;
        private const double ACCEL_TARGET_SPEED = 90.0;
        private const double BRAKE_START_SPEED = 90.0;
        private const double BRAKE_TARGET_SPEED = 1.0;

        private const int HISTORY_LIMIT = 8;

        private IMyShipController _controller;
        private IMyTextSurface _display;
        private readonly List<IMyMotorSuspension> _wheels = new List<IMyMotorSuspension>();
        private readonly List<string> _history = new List<string>();
        private readonly StringBuilder _sb = new StringBuilder();

        private TestMode _mode = TestMode.Idle;
        private DateTime _startTime;
        private double _startSpeed;
        private double _peakSpeed;
        private double _distance;
        private double _lastSpeed;
        private bool _hasLastSample;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            Setup();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (!string.IsNullOrWhiteSpace(argument))
            {
                HandleArgument(argument.Trim().ToUpperInvariant());
            }

            if (_controller == null || _display == null)
            {
                Setup();
            }

            UpdateTest();
            Draw();
        }

        private void Setup()
        {
            var controllers = new List<IMyShipController>();
            GridTerminalSystem.GetBlocksOfType(controllers, c => c.CubeGrid == Me.CubeGrid);
            _controller = controllers.FirstOrDefault(c => c.IsMainCockpit) ?? controllers.FirstOrDefault();

            _wheels.Clear();
            GridTerminalSystem.GetBlocksOfType(_wheels, w => w.CubeGrid == Me.CubeGrid);

            _display = Me.GetSurface(0);
            _display.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
            _display.Font = "Monospace";
            _display.FontSize = 0.75f;

            var lcd = GridTerminalSystem.GetBlockWithName(LCD_NAME) as IMyTextSurface;
            if (lcd != null)
            {
                _display = lcd;
                _display.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
                _display.Font = "Monospace";
                _display.FontSize = 0.75f;
            }
        }

        private void HandleArgument(string argument)
        {
            switch (argument)
            {
                case "ACCEL":
                case "ARM_ACCEL":
                    ResetRun();
                    _mode = TestMode.ArmedAccel;
                    break;

                case "BRAKE":
                case "ARM_BRAKE":
                    ResetRun();
                    _mode = TestMode.ArmedBrake;
                    break;

                case "CANCEL":
                case "STOP":
                    ResetRun();
                    _mode = TestMode.Idle;
                    break;

                case "CLEAR":
                    _history.Clear();
                    break;
            }
        }

        private void UpdateTest()
        {
            if (_controller == null)
            {
                return;
            }

            var speed = _controller.GetShipSpeed();

            switch (_mode)
            {
                case TestMode.ArmedAccel:
                    if (speed <= ACCEL_START_SPEED && _controller.MoveIndicator.Z < -0.1f)
                    {
                        StartRun(speed, TestMode.RunningAccel);
                    }
                    break;

                case TestMode.ArmedBrake:
                    if (speed >= BRAKE_START_SPEED && IsBraking())
                    {
                        StartRun(speed, TestMode.RunningBrake);
                    }
                    break;

                case TestMode.RunningAccel:
                    SampleDistance(speed);
                    _peakSpeed = Math.Max(_peakSpeed, speed);

                    if (speed >= ACCEL_TARGET_SPEED)
                    {
                        FinishRun("ACCEL", ACCEL_START_SPEED, ACCEL_TARGET_SPEED, speed);
                    }
                    break;

                case TestMode.RunningBrake:
                    SampleDistance(speed);
                    _peakSpeed = Math.Max(_peakSpeed, speed);

                    if (speed <= BRAKE_TARGET_SPEED)
                    {
                        FinishRun("BRAKE", _startSpeed, BRAKE_TARGET_SPEED, speed);
                    }
                    break;
            }
        }

        private void StartRun(double speed, TestMode mode)
        {
            _mode = mode;
            _startTime = DateTime.Now;
            _startSpeed = speed;
            _peakSpeed = speed;
            _distance = 0;
            _lastSpeed = speed;
            _hasLastSample = true;
        }

        private void FinishRun(string label, double fromSpeed, double toSpeed, double endSpeed)
        {
            var elapsed = DateTime.Now - _startTime;
            var friction = GetFrictionSummary();
            var result = $"{label} {fromSpeed:F0}->{toSpeed:F0}m/s  {elapsed.TotalSeconds:F3}s  {_distance:F1}m  F:{friction}";

            _history.Insert(0, result);

            while (_history.Count > HISTORY_LIMIT)
            {
                _history.RemoveAt(_history.Count - 1);
            }

            ResetRun();
            _mode = TestMode.Idle;
        }

        private void SampleDistance(double speed)
        {
            if (!_hasLastSample)
            {
                _lastSpeed = speed;
                _hasLastSample = true;
                return;
            }

            var dt = Runtime.TimeSinceLastRun.TotalSeconds;
            var avgSpeed = (_lastSpeed + speed) * 0.5;
            _distance += avgSpeed * dt;
            _lastSpeed = speed;
        }

        private bool IsBraking()
        {
            return _controller.HandBrake || _controller.MoveIndicator.Z > 0.1f;
        }

        private void ResetRun()
        {
            _startTime = DateTime.MinValue;
            _startSpeed = 0;
            _peakSpeed = 0;
            _distance = 0;
            _lastSpeed = 0;
            _hasLastSample = false;
        }

        private string GetFrictionSummary()
        {
            if (_wheels.Count == 0)
            {
                return "--";
            }

            var min = _wheels.Min(w => w.Friction);
            var max = _wheels.Max(w => w.Friction);
            var avg = _wheels.Average(w => w.Friction);

            if (Math.Abs(max - min) < 0.01f)
            {
                return $"{avg:F1}";
            }

            return $"{avg:F1} ({min:F1}-{max:F1})";
        }

        private void Draw()
        {
            _sb.Clear();
            _sb.AppendLine("CRS Tyre Performance Test");
            _sb.AppendLine();

            if (_controller == null)
            {
                _sb.AppendLine("No ship controller found.");
                _display?.WriteText(_sb);
                return;
            }

            var speed = _controller.GetShipSpeed();
            _sb.AppendLine($"Mode.....: {_mode}");
            _sb.AppendLine($"Speed....: {speed:F1} m/s");
            _sb.AppendLine($"Friction.: {GetFrictionSummary()}");
            _sb.AppendLine($"Wheels...: {_wheels.Count}");
            _sb.AppendLine();

            if (_mode == TestMode.RunningAccel || _mode == TestMode.RunningBrake)
            {
                var elapsed = DateTime.Now - _startTime;
                _sb.AppendLine($"Running..: {elapsed.TotalSeconds:F3}s");
                _sb.AppendLine($"Distance.: {_distance:F1}m");
                _sb.AppendLine($"Peak.....: {_peakSpeed:F1}m/s");
                _sb.AppendLine();
            }

            _sb.AppendLine("Commands:");
            _sb.AppendLine("ACCEL - arm 0->90 test");
            _sb.AppendLine("BRAKE - arm 90->0 test");
            _sb.AppendLine("STOP  - cancel test");
            _sb.AppendLine("CLEAR - clear history");
            _sb.AppendLine();

            _sb.AppendLine("Last results:");
            foreach (var item in _history)
            {
                _sb.AppendLine(item);
            }

            _display.WriteText(_sb);
        }

        private enum TestMode
        {
            Idle,
            ArmedAccel,
            RunningAccel,
            ArmedBrake,
            RunningBrake
        }
    }
}
