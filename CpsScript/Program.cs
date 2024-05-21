using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        #region mdk preserve

        private readonly float MAX_HEIGHT = 0.05f;
        private readonly float DEFAULT_HEIGHT = 0.2f;
        private readonly bool INVERT_SUSPENSIONS = false;
        private readonly float LO_SPEED_THRESHOLD = 75;
        private readonly float HI_SPEED_THRESHOLD = 89;

        private readonly float ANG_LO_SPEED_F = 30;
        private readonly float ANG_LO_SPEED_R = 30;
        private readonly float ANG_MD_SPEED_F = 25;
        private readonly float ANG_MD_SPEED_R = 25;
        private readonly float ANG_HI_SPEED_F = 20;
        private readonly float ANG_HI_SPEED_R = 20;

        private readonly float SMART_ERS_ACTIVATION = 70;

        private readonly float OUTTER_FACTOR = 1f;
        private readonly string DEBUG_LCD = "Debug LCD";

        #endregion

        enum SuspensionPosition
        {
            FrontRight,
            FrontLeft,
            RearRight,
            RearLeft
        }

        private IMyShipController _mainController;
        private IMyTextSurface _debugLcd;
        private IMyMotorSuspension[] _suspensions;
        private IMyProgrammableBlock _fsessProgBlock;
        private IMySensorBlock _draftingSensor;
        private IMySensorBlock _caRightSensor;
        private IMySensorBlock _caLeftSensor;
        private IMyCameraBlock _noseCamera;
        private bool _enableSers;
        private bool _hasError;

        public Program()
        {
            try
            {
                SetupDebugLcd();
                SetupFsessProgBlock();
                SetupController();
                SetupSuspensions();
                //SetupCamera();
                SetupCollisionAvoidanceSensors();
                SetupDraftingSensor();
            }
            catch (Exception ex)
            {
                Echo("ERROR: " + ex.Message);
                _hasError = true;
            }

            Runtime.UpdateFrequency = UpdateFrequency.Update1;
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

            Echo("Running CPS");

            var delta = (float)Runtime.TimeSinceLastRun.TotalMilliseconds / 1000;

            HandleArgument(argument);
            UpdateSteering(delta);
            UpdateCollisionAvoidance(delta);
            UpdateSmartErs();
            UpdateActiveSuspensions(delta);
        }

        #region Updates

        private void UpdateCollisionAvoidance(float delta)
        {
            if (_caLeftSensor == null
                || _caLeftSensor.Closed
                || _caRightSensor == null
                || _caRightSensor.Closed)
            {
                return;
            }

            var moveX = _mainController.MoveIndicator.X;
            var speed = _mainController.GetShipSpeed();
            var fr = GetSuspension(SuspensionPosition.FrontRight);
            var fl = GetSuspension(SuspensionPosition.FrontLeft);
            var rr = GetSuspension(SuspensionPosition.RearRight);
            var rl = GetSuspension(SuspensionPosition.RearLeft);
            var steeringOverride = 0.4f;

            if (_caLeftSensor.IsActive && !_caRightSensor.IsActive)
            {
                fr.SteeringOverride = steeringOverride;
                fl.SteeringOverride = steeringOverride;
                rr.SteeringOverride = -steeringOverride;
                rl.SteeringOverride = -steeringOverride;
            }
            else if (_caRightSensor.IsActive && !_caLeftSensor.IsActive)
            {
                fr.SteeringOverride = -steeringOverride;
                fl.SteeringOverride = -steeringOverride;
                rr.SteeringOverride = steeringOverride;
                rl.SteeringOverride = steeringOverride;
            }
            else
            {
                fr.SteeringOverride = 0;
                fl.SteeringOverride = 0;
                rr.SteeringOverride = 0;
                rl.SteeringOverride = 0;
            }
        }

        private void UpdateSmartErs()
        {
            if (!_enableSers || _fsessProgBlock == null || _fsessProgBlock.Closed)
            {
                return;
            }

            var speed = _mainController.GetShipSpeed();
            var isDrafting = _draftingSensor.IsActive && speed > 50;
            var isTurning = _mainController.MoveIndicator.X != 0;

            if (!isDrafting
                && !isTurning
                && ((speed >= 95 && speed < 98)
                    || speed < SMART_ERS_ACTIVATION))
            {
                _fsessProgBlock.TryRun("ERS_ON");
            }
            else
            {
                _fsessProgBlock.TryRun("ERS_OFF");
            }
        }

        private void UpdateActiveSuspensions(float delta)
        {
            var moveX = _mainController.MoveIndicator.X;
            var moveY = _mainController.MoveIndicator.Y;
            var speed = _mainController.GetShipSpeed();
            var fr = GetSuspension(SuspensionPosition.FrontRight);
            var fl = GetSuspension(SuspensionPosition.FrontLeft);
            var rr = GetSuspension(SuspensionPosition.RearRight);
            var rl = GetSuspension(SuspensionPosition.RearLeft);
            var elevationSpeed = 2 * Math.Abs(MAX_HEIGHT - DEFAULT_HEIGHT) * delta;

            var flh = fl.Height;
            var frh = fr.Height;
            var rlh = rl.Height;
            var rrh = rr.Height;

            if (moveX < 0) //left
            {
                flh += !INVERT_SUSPENSIONS ? elevationSpeed : -elevationSpeed;
                rlh += !INVERT_SUSPENSIONS ? elevationSpeed : -elevationSpeed;
                frh += !INVERT_SUSPENSIONS ? -elevationSpeed : elevationSpeed;
                rrh += !INVERT_SUSPENSIONS ? -elevationSpeed : elevationSpeed;
            }
            else if (moveX > 0) //right
            {
                flh += !INVERT_SUSPENSIONS ? -elevationSpeed : elevationSpeed;
                rlh += !INVERT_SUSPENSIONS ? -elevationSpeed : elevationSpeed;
                frh += !INVERT_SUSPENSIONS ? elevationSpeed : -elevationSpeed;
                rrh += !INVERT_SUSPENSIONS ? elevationSpeed : -elevationSpeed;
            }
            else
            {
                frh = DEFAULT_HEIGHT;
                flh = DEFAULT_HEIGHT;
                rrh = DEFAULT_HEIGHT;
                rlh = DEFAULT_HEIGHT;
            }

            fr.Height = MathHelper.Clamp(frh, MAX_HEIGHT, DEFAULT_HEIGHT);
            fl.Height = MathHelper.Clamp(flh, MAX_HEIGHT, DEFAULT_HEIGHT);
            rr.Height = MathHelper.Clamp(rrh, MAX_HEIGHT, DEFAULT_HEIGHT);
            rl.Height = MathHelper.Clamp(rlh, MAX_HEIGHT, DEFAULT_HEIGHT);
        }

        private void UpdateSteering(float delta)
        {
            var speed = _mainController.GetShipSpeed();
            var moveX = _mainController.MoveIndicator.X;
            var fr = GetSuspension(SuspensionPosition.FrontRight);
            var fl = GetSuspension(SuspensionPosition.FrontLeft);
            var rr = GetSuspension(SuspensionPosition.RearRight);
            var rl = GetSuspension(SuspensionPosition.RearLeft);

            var isLowSpeed = speed < LO_SPEED_THRESHOLD;
            var isMidSpeed = speed >= LO_SPEED_THRESHOLD && speed < HI_SPEED_THRESHOLD;
            var angleF = isLowSpeed ? ANG_LO_SPEED_F : isMidSpeed ? ANG_MD_SPEED_F : ANG_HI_SPEED_F;
            var angleR = isLowSpeed ? ANG_LO_SPEED_R : isMidSpeed ? ANG_MD_SPEED_R : ANG_HI_SPEED_R;
            var outterAngleF = angleF * OUTTER_FACTOR;
            var outterAngleR = angleR * OUTTER_FACTOR;

            var targetAngleFr = moveX >= 0 ? MathHelper.ToRadians(angleF) : MathHelper.ToRadians(outterAngleF);
            var targetAngleFl = moveX <= 0 ? MathHelper.ToRadians(angleF) : MathHelper.ToRadians(outterAngleF);
            var targetAngleRr = moveX >= 0 ? MathHelper.ToRadians(angleR) : MathHelper.ToRadians(outterAngleR);
            var targetAngleRl = moveX <= 0 ? MathHelper.ToRadians(angleR) : MathHelper.ToRadians(outterAngleR);
            var transitionSpeed = 0.1f * delta;

            fr.MaxSteerAngle = MoveTowards(fr.MaxSteerAngle, targetAngleFr, transitionSpeed);
            fl.MaxSteerAngle = MoveTowards(fl.MaxSteerAngle, targetAngleFl, transitionSpeed);
            rr.MaxSteerAngle = MoveTowards(rr.MaxSteerAngle, targetAngleRr, transitionSpeed);
            rl.MaxSteerAngle = MoveTowards(rl.MaxSteerAngle, targetAngleRl, transitionSpeed);
        }

        #endregion

        #region Setups

        private void HandleArgument(string argument)
        {
            if (argument.Equals("SERS", StringComparison.InvariantCultureIgnoreCase))
            {
                _enableSers = !_enableSers;
            }
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

        private void SetupSuspensions()
        {
            var suspensions = new List<IMyMotorSuspension>();
            GridTerminalSystem.GetBlocksOfType(suspensions, s => s.CubeGrid == Me.CubeGrid);

            if (suspensions == null || suspensions.Count != 4)
            {
                throw new Exception("Need 4 suspensions!");
            }

            var susFl = suspensions.FirstOrDefault(s => s.CustomName.Contains("FL"));
            if (susFl == null) throw new Exception("FL suspension not found.");

            var susFr = suspensions.FirstOrDefault(s => s.CustomName.Contains("FR"));
            if (susFr == null) throw new Exception("FR suspension not found.");

            var susRl = suspensions.FirstOrDefault(s => s.CustomName.Contains("RL"));
            if (susRl == null) throw new Exception("RL suspension not found.");

            var susRr = suspensions.FirstOrDefault(s => s.CustomName.Contains("RR"));
            if (susRr == null) throw new Exception("RR suspension not found.");

            _suspensions = new IMyMotorSuspension[4];

            SetSuspension(SuspensionPosition.FrontLeft, susFl);
            SetSuspension(SuspensionPosition.FrontRight, susFr);
            SetSuspension(SuspensionPosition.RearLeft, susRl);
            SetSuspension(SuspensionPosition.RearRight, susRr);
        }

        private void SetupFsessProgBlock()
        {
            var progBlocks = new List<IMyProgrammableBlock>();
            GridTerminalSystem.GetBlocksOfType(progBlocks, p => p != Me);

            if (progBlocks.Count <= 0)
            {
                throw new Exception("No other programmable block found.");
            }

            _fsessProgBlock = progBlocks.FirstOrDefault();
        }

        private void SetupCollisionAvoidanceSensors()
        {
            _caRightSensor = (IMySensorBlock)GridTerminalSystem.GetBlockWithName("CA Sensor Right");

            if (_caRightSensor == null)
            {
                throw new Exception("\"CA Sensor Right\" not found.");
            }

            _caRightSensor.FrontExtend = 3f;
            _caRightSensor.BackExtend = 0.275f;
            _caRightSensor.TopExtend = 2f;
            _caRightSensor.BottomExtend = 0.5f;
            _caRightSensor.RightExtend = 5f;
            _caRightSensor.LeftExtend = 25f;
            _caRightSensor.DetectLargeShips = true;
            _caRightSensor.DetectStations = true;
            _caRightSensor.DetectPlayers = false;
            _caRightSensor.DetectSmallShips = false;

            _caLeftSensor = (IMySensorBlock)GridTerminalSystem.GetBlockWithName("CA Sensor Left");

            if (_caLeftSensor == null)
            {
                throw new Exception("\"CA Sensor Left\" not found.");
            }

            _caLeftSensor.FrontExtend = 3f;
            _caLeftSensor.BackExtend = 0.275f;
            _caLeftSensor.TopExtend = 2f;
            _caLeftSensor.BottomExtend = 0.5f;
            _caLeftSensor.RightExtend = 25f;
            _caLeftSensor.LeftExtend = 5f;
            _caLeftSensor.DetectLargeShips = true;
            _caLeftSensor.DetectStations = true;
            _caLeftSensor.DetectPlayers = false;
            _caLeftSensor.DetectSmallShips = false;
        }

        private void SetupDebugLcd()
        {
            _debugLcd = (IMyTextSurface)GridTerminalSystem.GetBlockWithName(DEBUG_LCD);
        }

        private void SetupDraftingSensor()
        {
            var sensor = (IMySensorBlock)GridTerminalSystem.GetBlockWithName("Drafting Sensor");

            if (sensor == null)
            {
                throw new Exception("\"Drafting Sensor\" not found.");
            }

            _draftingSensor = sensor;
        }

        #endregion

        #region Helpers

        private IMyMotorSuspension GetSuspension(SuspensionPosition pos)
        {
            return _suspensions[(int)pos];
        }

        private void SetSuspension(SuspensionPosition pos, IMyMotorSuspension suspension)
        {
            if (suspension == null)
            {
                return;
            }

            _suspensions[(int)pos] = suspension;
        }

        private void ConsolePrint(string text, bool append = true)
        {
            Me.GetSurface(0).WriteText(text, append);
            _debugLcd?.WriteText(text, append);
            Echo(text);
        }

        private void ConsoleClear()
        {
            Me.GetSurface(0).WriteText(string.Empty);
            _debugLcd?.WriteText(string.Empty);
            Echo(string.Empty);
        }

        private float MoveTowards(float current, float target, float maxDelta)
        {
            if (current == target)
            {
                return target;
            }

            var fixedDelta = Math.Min(maxDelta, Math.Abs(target - current));

            if (current > target)
            {
                current -= fixedDelta;
                current = MathHelper.Clamp(current, target, current);
            }
            else if (current < target)
            {
                current += fixedDelta;
                current = MathHelper.Clamp(current, current, target);
            }

            return current;
        }

        private void PrintVector(Vector3D vector)
        {
            ConsolePrint($"X:{Math.Round(vector.X, 3)} Y:{Math.Round(vector.Y, 3)} Z:{Math.Round(vector.Z, 3)}\n");
        }

        private void SetPropulsionOverride(float value)
        {
            var susFl = GetSuspension(SuspensionPosition.FrontLeft);
            var susFr = GetSuspension(SuspensionPosition.FrontRight);
            var susRl = GetSuspension(SuspensionPosition.RearLeft);
            var susRr = GetSuspension(SuspensionPosition.RearRight);

            susFl.PropulsionOverride = value;
            susFr.PropulsionOverride = -value;
            susRl.PropulsionOverride = value;
            susRr.PropulsionOverride = -value;
        }

        #endregion
    }
}
