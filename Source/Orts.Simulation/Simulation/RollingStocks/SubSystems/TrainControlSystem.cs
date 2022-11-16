// COPYRIGHT 2021 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

// Define this to log the wheel configurations on cars as they are loaded.
//#define DEBUG_WHEELS

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Orts.Common;
using Orts.Formats.Msts;
using Orts.Parsers.Msts;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks.SubSystems.Brakes;
using Orts.Simulation.RollingStocks.SubSystems.Brakes.MSTS;
using ORTS.Common;
using ORTS.Scripting.Api;
using ORTS.Scripting.Api.ETCS;
using Event = Orts.Common.Event;

namespace Orts.Simulation.RollingStocks.SubSystems
{
    public class ScriptedTrainControlSystem : ISubSystem<ScriptedTrainControlSystem>
    {
        public class MonitoringDevice
        {
            public float MonitorTimeS = 66; // Time from alerter reset to applying emergency brake
            public float AlarmTimeS = 60; // Time from alerter reset to audible and visible alarm
            public float PenaltyTimeS;
            public float CriticalLevelMpS;
            public float ResetLevelMpS;
            public bool AppliesFullBrake = true;
            public bool AppliesEmergencyBrake;
            public bool EmergencyCutsPower;
            public bool EmergencyShutsDownEngine;
            public float AlarmTimeBeforeOverspeedS = 5;         // OverspeedMonitor only
            public float TriggerOnOverspeedMpS;                 // OverspeedMonitor only
            public bool TriggerOnTrackOverspeed;                // OverspeedMonitor only
            public float TriggerOnTrackOverspeedMarginMpS = 4;  // OverspeedMonitor only
            public bool ResetOnDirectionNeutral = false;
            public bool ResetOnZeroSpeed = true;
            public bool ResetOnResetButton;                     // OverspeedMonitor only

            public MonitoringDevice(STFReader stf)
            {
                stf.MustMatch("(");
                stf.ParseBlock(new STFReader.TokenProcessor[] {
                    new STFReader.TokenProcessor("monitoringdevicemonitortimelimit", () => { MonitorTimeS = stf.ReadFloatBlock(STFReader.UNITS.Time, MonitorTimeS); }),
                    new STFReader.TokenProcessor("monitoringdevicealarmtimelimit", () => { AlarmTimeS = stf.ReadFloatBlock(STFReader.UNITS.Time, AlarmTimeS); }),
                    new STFReader.TokenProcessor("monitoringdevicepenaltytimelimit", () => { PenaltyTimeS = stf.ReadFloatBlock(STFReader.UNITS.Time, PenaltyTimeS); }),
                    new STFReader.TokenProcessor("monitoringdevicecriticallevel", () => { CriticalLevelMpS = stf.ReadFloatBlock(STFReader.UNITS.Speed, CriticalLevelMpS); }),
                    new STFReader.TokenProcessor("monitoringdeviceresetlevel", () => { ResetLevelMpS = stf.ReadFloatBlock(STFReader.UNITS.Speed, ResetLevelMpS); }),
                    new STFReader.TokenProcessor("monitoringdeviceappliesfullbrake", () => { AppliesFullBrake = stf.ReadBoolBlock(AppliesFullBrake); }),
                    new STFReader.TokenProcessor("monitoringdeviceappliesemergencybrake", () => { AppliesEmergencyBrake = stf.ReadBoolBlock(AppliesEmergencyBrake); }),
                    new STFReader.TokenProcessor("monitoringdeviceappliescutspower", () => { EmergencyCutsPower = stf.ReadBoolBlock(EmergencyCutsPower); }),
                    new STFReader.TokenProcessor("monitoringdeviceappliesshutsdownengine", () => { EmergencyShutsDownEngine = stf.ReadBoolBlock(EmergencyShutsDownEngine); }),
                    new STFReader.TokenProcessor("monitoringdevicealarmtimebeforeoverspeed", () => { AlarmTimeBeforeOverspeedS = stf.ReadFloatBlock(STFReader.UNITS.Time, AlarmTimeBeforeOverspeedS); }),
                    new STFReader.TokenProcessor("monitoringdevicetriggeronoverspeed", () => { TriggerOnOverspeedMpS = stf.ReadFloatBlock(STFReader.UNITS.Speed, TriggerOnOverspeedMpS); }),
                    new STFReader.TokenProcessor("monitoringdevicetriggerontrackoverspeed", () => { TriggerOnTrackOverspeed = stf.ReadBoolBlock(TriggerOnTrackOverspeed); }),
                    new STFReader.TokenProcessor("monitoringdevicetriggerontrackoverspeedmargin", () => { TriggerOnTrackOverspeedMarginMpS = stf.ReadFloatBlock(STFReader.UNITS.Speed, TriggerOnTrackOverspeedMarginMpS); }),
                    new STFReader.TokenProcessor("monitoringdeviceresetondirectionneutral", () => { ResetOnDirectionNeutral = stf.ReadBoolBlock(ResetOnDirectionNeutral); }),
                    new STFReader.TokenProcessor("monitoringdeviceresetonresetbutton", () => { ResetOnResetButton = stf.ReadBoolBlock(ResetOnResetButton); }),
                    new STFReader.TokenProcessor("monitoringdeviceresetonzerospeed", () => { ResetOnZeroSpeed = stf.ReadBoolBlock(ResetOnZeroSpeed); }),
                });
            }

            public MonitoringDevice() { }

            public MonitoringDevice(MonitoringDevice other)
            {
                MonitorTimeS = other.MonitorTimeS;
                AlarmTimeS = other.AlarmTimeS;
                PenaltyTimeS = other.PenaltyTimeS;
                CriticalLevelMpS = other.CriticalLevelMpS;
                ResetLevelMpS = other.ResetLevelMpS;
                AppliesFullBrake = other.AppliesFullBrake;
                AppliesEmergencyBrake = other.AppliesEmergencyBrake;
                EmergencyCutsPower = other.EmergencyCutsPower;
                EmergencyShutsDownEngine = other.EmergencyShutsDownEngine;
                AlarmTimeBeforeOverspeedS = other.AlarmTimeBeforeOverspeedS;
                TriggerOnOverspeedMpS = other.TriggerOnOverspeedMpS;
                TriggerOnTrackOverspeed = other.TriggerOnTrackOverspeed;
                TriggerOnTrackOverspeedMarginMpS = other.TriggerOnTrackOverspeedMarginMpS;
                ResetOnDirectionNeutral = other.ResetOnDirectionNeutral;
                ResetOnZeroSpeed = other.ResetOnZeroSpeed;
                ResetOnResetButton = other.ResetOnResetButton;
            }
        }

        // Traction cut-off parameters
        public bool DoesBrakeCutPower { get; private set; }
        public bool DoesVacuumBrakeCutPower { get; private set; }
        public float BrakeCutsPowerAtBrakeCylinderPressurePSI { get; private set; } = 4.0f;
        public float BrakeCutsPowerAtBrakePipePressurePSI { get; private set; }
        public float BrakeRestoresPowerAtBrakePipePressurePSI { get; private set; }
        public float BrakeCutsPowerForMinimumSpeedMpS { get; private set; }
        public bool BrakeCutsPowerUntilTractionCommandCancelled { get; private set; }
        public BrakeTractionCutOffModeType BrakeTractionCutOffMode
        {
            get
            {
                if (DoesBrakeCutPower && !DoesVacuumBrakeCutPower) // Air brake system
                {
                    if (BrakeCutsPowerAtBrakePipePressurePSI > 0f)
                    {
                        if (BrakeRestoresPowerAtBrakePipePressurePSI > 0f)
                        {
                            return BrakeTractionCutOffModeType.AirBrakePipeHysteresis;
                        }
                        else
                        {
                            return BrakeTractionCutOffModeType.AirBrakePipeSinglePressure;
                        }
                    }
                    else // Default to brake cylinder
                    {
                        return BrakeTractionCutOffModeType.AirBrakeCylinderSinglePressure;
                    }
                }
                else if (!DoesBrakeCutPower && DoesVacuumBrakeCutPower) // Vacuum brake system
                {
                    return BrakeTractionCutOffModeType.VacuumBrakePipeHysteresis;
                }
                else
                {
                    return BrakeTractionCutOffModeType.None;
                }
            }
        }

        // Properties
        public bool VigilanceAlarm { get; set; }
        public bool VigilanceEmergency { get; set; }
        public bool OverspeedWarning { get; set; }
        public bool PenaltyApplication { get; set; }
        public float CurrentSpeedLimitMpS { get; set; }
        public float NextSpeedLimitMpS { get; set; }
        public TrackMonitorSignalAspect CabSignalAspect { get; set; }

        public bool Activated = false;
        public bool CustomTCSScript = false;

        public readonly MSTSLocomotive Locomotive;
        public readonly Simulator Simulator;

        public float ItemSpeedLimit;
        public Aspect ItemAspect;
        public float ItemDistance;
        public string MainHeadSignalTypeName;

        MonitoringDevice VigilanceMonitor;
        MonitoringDevice OverspeedMonitor;
        MonitoringDevice EmergencyStopMonitor;
        MonitoringDevice AWSMonitor;

        private bool simulatorEmergencyBraking = false;
        public bool SimulatorEmergencyBraking {
            get
            {
                return simulatorEmergencyBraking;
            }
            protected set
            {
                simulatorEmergencyBraking = value;

                if (Script != null)
#pragma warning disable CS0618 // SetEmergency is obsolete
                    Script.SetEmergency(value);
#pragma warning restore CS0618 // SetEmergency is obsolete
                else
                    Locomotive.TrainBrakeController.TCSEmergencyBraking = value;
            }
        }
        public bool AlerterButtonPressed { get; private set; }
        public bool PowerAuthorization { get; set; }
        public bool CircuitBreakerClosingOrder { get; set; }
        public bool CircuitBreakerOpeningOrder { get; set; }
        public bool TractionAuthorization { get; set; }
        public float MaxThrottlePercent { get; set; } = 100f;
        public bool FullDynamicBrakingOrder { get; set; }

        public Dictionary<int, float> CabDisplayControls = new Dictionary<int, float>();

        // generic TCS commands
        public Dictionary<int, bool> TCSCommandButtonDown = new Dictionary<int, bool>();
        public Dictionary<int, bool> TCSCommandSwitchOn = new Dictionary<int, bool>();
        // List of customized control strings;
        public Dictionary<int, string> CustomizedCabviewControlNames = new Dictionary<int, string>();
        // TODO : Delete this when SetCustomizedTCSControlString is deleted
        public int NextCabviewControlNameToEdit = 0;

        public string ScriptName;
        public string SoundFileName;
        public string ParametersFileName;
        public string TrainParametersFileName;
        TrainControlSystem Script;

        public ETCSStatus ETCSStatus { get { return Script?.ETCSStatus; } }

        public Dictionary<TrainControlSystem, string> Sounds = new Dictionary<TrainControlSystem, string>();

        public const float GravityMpS2 = 9.80665f;
        public const float GenericItemDistance = 400.0f;

        public ScriptedTrainControlSystem() { }

        public ScriptedTrainControlSystem(MSTSLocomotive locomotive)
        {
            Locomotive = locomotive;
            Simulator = Locomotive.Simulator;

            PowerAuthorization = true;
            CircuitBreakerClosingOrder = false;
            CircuitBreakerOpeningOrder = false;
            TractionAuthorization = true;
            FullDynamicBrakingOrder = false;
        }

        public void Parse(string lowercasetoken, STFReader stf)
        {
            switch (lowercasetoken)
            {
                case "engine(vigilancemonitor": VigilanceMonitor = new MonitoringDevice(stf); break;
                case "engine(overspeedmonitor": OverspeedMonitor = new MonitoringDevice(stf); break;
                case "engine(emergencystopmonitor": EmergencyStopMonitor = new MonitoringDevice(stf); break;
                case "engine(awsmonitor": AWSMonitor = new MonitoringDevice(stf); break;
                case "engine(ortstraincontrolsystem": ScriptName = stf.ReadStringBlock(null); break;
                case "engine(ortstraincontrolsystemsound": SoundFileName = stf.ReadStringBlock(null); break;
                case "engine(ortstraincontrolsystemparameters": ParametersFileName = stf.ReadStringBlock(null); break;
                case "engine(ortsdoesvacuumbrakecutpower":
                    DoesVacuumBrakeCutPower = stf.ReadBoolBlock(false);
                    if (DoesBrakeCutPower)
                    {
                        STFException.TraceWarning(stf, "DoesBrakeCutPower (for pneumatic brake systems) and ORTSDoesVacuumCutPower (for vacuum brake systems) are both set");
                    }
                    break;
                case "engine(doesbrakecutpower":
                    DoesBrakeCutPower = stf.ReadBoolBlock(false);
                    if (DoesVacuumBrakeCutPower)
                    {
                        STFException.TraceWarning(stf, "DoesBrakeCutPower (for pneumatic brake systems) and ORTSDoesVacuumCutPower (for vacuum brake systems) are both set");
                    }
                    break;
                case "engine(brakecutspoweratbrakecylinderpressure": BrakeCutsPowerAtBrakeCylinderPressurePSI = stf.ReadFloatBlock(STFReader.UNITS.PressureDefaultPSI, null); break;
                case "engine(ortsbrakecutspoweratbrakepipepressure": BrakeCutsPowerAtBrakePipePressurePSI = stf.ReadFloatBlock(STFReader.UNITS.PressureDefaultPSI, null); break;
                case "engine(ortsbrakerestorespoweratbrakepipepressure": BrakeRestoresPowerAtBrakePipePressurePSI = stf.ReadFloatBlock(STFReader.UNITS.PressureDefaultPSI, null); break;
                case "engine(ortsbrakecutspowerforminimumspeed": BrakeCutsPowerForMinimumSpeedMpS = stf.ReadFloatBlock(STFReader.UNITS.Speed, null); break;
                case "engine(ortsbrakecutspoweruntiltractioncommandcancelled": BrakeCutsPowerUntilTractionCommandCancelled = stf.ReadBoolBlock(false); break;
            }
        }

        public void Copy(ScriptedTrainControlSystem other)
        {
            ScriptName = other.ScriptName;
            SoundFileName = other.SoundFileName;
            ParametersFileName = other.ParametersFileName;
            TrainParametersFileName = other.TrainParametersFileName;
            if (other.VigilanceMonitor != null) VigilanceMonitor = new MonitoringDevice(other.VigilanceMonitor);
            if (other.OverspeedMonitor != null) OverspeedMonitor = new MonitoringDevice(other.OverspeedMonitor);
            if (other.EmergencyStopMonitor != null) EmergencyStopMonitor = new MonitoringDevice(other.EmergencyStopMonitor);
            if (other.AWSMonitor != null) AWSMonitor = new MonitoringDevice(other.AWSMonitor);

            DoesBrakeCutPower = other.DoesBrakeCutPower;
            DoesVacuumBrakeCutPower = other.DoesVacuumBrakeCutPower;
            BrakeCutsPowerAtBrakeCylinderPressurePSI = other.BrakeCutsPowerAtBrakeCylinderPressurePSI;
            BrakeCutsPowerAtBrakePipePressurePSI = other.BrakeCutsPowerAtBrakePipePressurePSI;
            BrakeRestoresPowerAtBrakePipePressurePSI = other.BrakeRestoresPowerAtBrakePipePressurePSI;
            BrakeCutsPowerForMinimumSpeedMpS = other.BrakeCutsPowerForMinimumSpeedMpS;
            BrakeCutsPowerUntilTractionCommandCancelled = other.BrakeCutsPowerUntilTractionCommandCancelled;
        }

        //Debrief Eval
        public static int DbfevalFullBrakeAbove16kmh = 0;
        public bool ldbfevalfullbrakeabove16kmh = false;

        public void Initialize()
        {
            if (!Activated)
            {
                #region Parameters sanity checks
                if ((DoesBrakeCutPower || DoesVacuumBrakeCutPower) && BrakeCutsPowerAtBrakePipePressurePSI > BrakeRestoresPowerAtBrakePipePressurePSI)
                {
                    BrakeCutsPowerAtBrakePipePressurePSI = BrakeRestoresPowerAtBrakePipePressurePSI - 1.0f;

                    if (Simulator.Settings.VerboseConfigurationMessages)
                    {
                        Trace.TraceInformation("BrakeCutsPowerAtBrakePipePressure is greater then BrakeRestoresPowerAtBrakePipePressure, and has been set to value of {0}", FormatStrings.FormatPressure(BrakeCutsPowerAtBrakePipePressurePSI, PressureUnit.PSI, Locomotive.BrakeSystemPressureUnits[BrakeSystemComponent.BrakePipe], true));
                    }
                }

                if (DoesVacuumBrakeCutPower && Locomotive.BrakeSystem is VacuumSinglePipe && (BrakeRestoresPowerAtBrakePipePressurePSI == 0 || BrakeRestoresPowerAtBrakePipePressurePSI > VacuumSinglePipe.OneAtmospherePSI))
                {
                    BrakeRestoresPowerAtBrakePipePressurePSI = Bar.ToPSI(Bar.FromInHg(15.0f)); // Power can be restored once brake pipe rises above 15 InHg

                    if (Simulator.Settings.VerboseConfigurationMessages)
                    {
                        Trace.TraceInformation("BrakeRestoresPowerAtBrakePipePressure appears out of limits, and has been set to value of {0} InHg", Bar.ToInHg(Bar.FromPSI(BrakeRestoresPowerAtBrakePipePressurePSI)));
                    }
                }
                #endregion

                if (!Simulator.Settings.DisableTCSScripts && ScriptName != null && ScriptName != "MSTS" && ScriptName != "")
                {
                    var pathArray = new string[] { Path.Combine(Path.GetDirectoryName(Locomotive.WagFilePath), "Script") };
                    Script = Simulator.ScriptManager.Load(pathArray, ScriptName) as TrainControlSystem;
                    CustomTCSScript = true;
                }

                if (ParametersFileName != null)
                {
                    ParametersFileName = Path.Combine(Path.Combine(Path.GetDirectoryName(Locomotive.WagFilePath), "Script"), ParametersFileName);
                }

                if (Locomotive.Train.TcsParametersFileName != null)
                {
                    TrainParametersFileName = Path.Combine(Simulator.BasePath, @"TRAINS\CONSISTS\SCRIPT\", Locomotive.Train.TcsParametersFileName);
                }

                if (Script == null)
                {
                    Script = new MSTSTrainControlSystem();
                    ((MSTSTrainControlSystem)Script).VigilanceMonitor = VigilanceMonitor;
                    ((MSTSTrainControlSystem)Script).OverspeedMonitor = OverspeedMonitor;
                    ((MSTSTrainControlSystem)Script).EmergencyStopMonitor = EmergencyStopMonitor;
                    ((MSTSTrainControlSystem)Script).AWSMonitor = AWSMonitor;
                    ((MSTSTrainControlSystem)Script).EmergencyCausesThrottleDown = Locomotive.EmergencyCausesThrottleDown;
                    ((MSTSTrainControlSystem)Script).EmergencyEngagesHorn = Locomotive.EmergencyEngagesHorn;
                }

                if (SoundFileName != null)
                {
                    var soundPathArray = new[] {
                    Path.Combine(Path.GetDirectoryName(Locomotive.WagFilePath), "SOUND"),
                    Path.Combine(Simulator.BasePath, "SOUND"),
                };
                    var soundPath = ORTSPaths.GetFileFromFolders(soundPathArray, SoundFileName);
                    if (File.Exists(soundPath))
                        Sounds.Add(Script, soundPath);
                }

                // AbstractScriptClass
                Script.ClockTime = () => (float)Simulator.ClockTime;
                Script.GameTime = () => (float)Simulator.GameTime;
                Script.PreUpdate = () => Simulator.PreUpdate;
                Script.DistanceM = () => Locomotive.DistanceM;
                Script.Confirm = Locomotive.Simulator.Confirmer.Confirm;
                Script.Message = Locomotive.Simulator.Confirmer.Message;
                Script.SignalEvent = Locomotive.SignalEvent;
                Script.SignalEventToTrain = (evt) =>
                {
                    if (Locomotive.Train != null)
                    {
                        Locomotive.Train.SignalEvent(evt);
                    }
                };
                Script.SpeedMpS = () => Math.Abs(Locomotive.SpeedMpS);

                Script.AttachToHost(this);
                Script.Initialize();
                Activated = true;
            }
        }

        public void InitializeMoving()
        {
            Script?.InitializeMoving();
        }

        public Aspect NextNormalSignalDistanceHeadsAspect()
        {
            var signal = Locomotive.Train.NextSignalObject[Locomotive.Train.MUDirection == Direction.Reverse ? 1 : 0];
            Aspect distanceSignalAspect = Aspect.None;
            if (signal != null)
            {
                foreach (var signalHead in signal.SignalHeads)
                {
                    if (signalHead.signalType.Function.MstsFunction == MstsSignalFunction.DISTANCE)
                    {
                        return distanceSignalAspect = (Aspect)Locomotive.Train.signalRef.TranslateToTCSAspect(signal.this_sig_lr(MstsSignalFunction.DISTANCE));
                    }
                }
            }
            return distanceSignalAspect;
        }

        public bool DoesNextNormalSignalHaveTwoAspects()
            // ...and the two aspects of each head are STOP and ( CLEAR_2 or CLEAR_1 or RESTRICTING)
        {
            var signal = Locomotive.Train.NextSignalObject[Locomotive.Train.MUDirection == Direction.Reverse ? 1 : 0];
            if (signal != null)
            {
                if (signal.SignalHeads[0].signalType.Aspects.Count > 2) return false;
                else
                {
                    foreach (var signalHead in signal.SignalHeads)
                    {
                        if (signalHead.signalType.Function.MstsFunction != MstsSignalFunction.DISTANCE &&
                            signalHead.signalType.Aspects.Count == 2 &&
                            (int)(signalHead.signalType.Aspects[0].Aspect) == 0 &&
                                ((int)(signalHead.signalType.Aspects[1].Aspect) == 7 ||
                                (int)(signalHead.signalType.Aspects[1].Aspect) == 6 ||
                                (int)(signalHead.signalType.Aspects[1].Aspect) == 2)) continue;
                        else return false;
                    }
                    return true;
                }
            }
            return true;
        }

        public T NextGenericSignalItem<T>(int itemSequenceIndex, ref T retval, float maxDistanceM, Train.TrainObjectItem.TRAINOBJECTTYPE type, string signalTypeName = "UNKNOWN")
        {
            var item = NextGenericSignalFeatures(signalTypeName, itemSequenceIndex, maxDistanceM, type);
            MainHeadSignalTypeName = item.MainHeadSignalTypeName;
            ItemAspect = item.Aspect;
            ItemDistance = item.DistanceM;
            ItemSpeedLimit = item.SpeedLimitMpS;
            return retval;
        }

        public SignalFeatures NextGenericSignalFeatures(string signalFunctionTypeName, int itemSequenceIndex, float maxDistanceM, Train.TrainObjectItem.TRAINOBJECTTYPE type)
        {
            var mainHeadSignalTypeName = "";
            var signalTypeName = "";
            var aspect = Aspect.None;
            var drawStateName = "";
            var distanceM = float.MaxValue;
            var speedLimitMpS = -1f;
            var altitudeM = float.MinValue;
            var textAspect = "";

            int dir = Locomotive.Train.MUDirection == Direction.Reverse ? 1 : 0;

            if (Locomotive.Train.ValidRoute[dir] == null || dir == 1 && Locomotive.Train.PresentPosition[dir].TCSectionIndex < 0)
                goto Exit;

            int index = dir == 0 ? Locomotive.Train.PresentPosition[dir].RouteListIndex :
                Locomotive.Train.ValidRoute[dir].GetRouteIndex(Locomotive.Train.PresentPosition[dir].TCSectionIndex, 0);

            if (!Locomotive.Train.signalRef.SignalFunctions.ContainsKey(signalFunctionTypeName))
            {
                distanceM = -1;
                goto Exit;
            }
            SignalFunction function = Locomotive.Train.signalRef.SignalFunctions[signalFunctionTypeName];

            if (index < 0)
                goto Exit;

            switch (type)
            {
                case Train.TrainObjectItem.TRAINOBJECTTYPE.SIGNAL:
                case Train.TrainObjectItem.TRAINOBJECTTYPE.SPEED_SIGNAL:
                {
                    var playerTrainSignalList = Locomotive.Train.PlayerTrainSignals[dir][function];
                    if (itemSequenceIndex > playerTrainSignalList.Count - 1)
                        goto Exit; // no n-th signal available
                    var trainSignal = playerTrainSignalList[itemSequenceIndex];
                    if (trainSignal.DistanceToTrainM > maxDistanceM)
                        goto Exit; // the requested signal is too distant

                    // All OK, we can retrieve the data for the required signal;
                    distanceM = trainSignal.DistanceToTrainM;
                    mainHeadSignalTypeName = trainSignal.SignalObject.SignalHeads[0].SignalTypeName;
                    if (signalFunctionTypeName == "NORMAL")
                    {
                        aspect = (Aspect)trainSignal.SignalState;
                        speedLimitMpS = trainSignal.AllowedSpeedMpS;
                        altitudeM = trainSignal.SignalObject.tdbtraveller.Y;
                    }
                    else
                    {
                        aspect = (Aspect)Locomotive.Train.signalRef.TranslateToTCSAspect(trainSignal.SignalObject.this_sig_lr(function));
                    }

                    var functionHead = trainSignal.SignalObject.SignalHeads.Find(head => head.Function == function);
                    signalTypeName = functionHead.SignalTypeName;
                    if (functionHead.signalType.DrawStates.Any(d => d.Value.Index == functionHead.draw_state))
                    {
                        drawStateName = functionHead.signalType.DrawStates.First(d => d.Value.Index == functionHead.draw_state).Value.Name;
                    }
                    textAspect = functionHead?.TextSignalAspect ?? "";
                    break;
                }
                case Train.TrainObjectItem.TRAINOBJECTTYPE.SPEEDPOST:
                {
                    var playerTrainSpeedpostList = Locomotive.Train.PlayerTrainSpeedposts[dir].Where(x => !x.IsWarning).ToList();
                    if (itemSequenceIndex > playerTrainSpeedpostList.Count - 1)
                        goto Exit; // no n-th speedpost available
                    var trainSpeedpost = playerTrainSpeedpostList[itemSequenceIndex];
                    if (trainSpeedpost.DistanceToTrainM > maxDistanceM)
                        goto Exit; // the requested speedpost is too distant

                    // All OK, we can retrieve the data for the required speedpost;
                    distanceM = trainSpeedpost.DistanceToTrainM;
                    speedLimitMpS = trainSpeedpost.AllowedSpeedMpS;
                    break;
                }
            }

        Exit:
            return new SignalFeatures(mainHeadSignalTypeName: mainHeadSignalTypeName, signalTypeName: signalTypeName, aspect: aspect, drawStateName: drawStateName, distanceM: distanceM, speedLimitMpS: speedLimitMpS,
                altitudeM: altitudeM, textAspect: textAspect);
        }

        public SpeedPostFeatures NextSpeedPostFeatures(int itemSequenceIndex, float maxDistanceM)
        {
            var speedPostTypeName = "";
            var isWarning = false;
            var distanceM = float.MaxValue;
            var speedLimitMpS = -1f;
            var altitudeM = float.MinValue;

            int dir = Locomotive.Train.MUDirection == Direction.Reverse ? 1 : 0;

            if (Locomotive.Train.ValidRoute[dir] == null || dir == 1 && Locomotive.Train.PresentPosition[dir].TCSectionIndex < 0)
                goto Exit;

            int index = dir == 0 ? Locomotive.Train.PresentPosition[dir].RouteListIndex :
                Locomotive.Train.ValidRoute[dir].GetRouteIndex(Locomotive.Train.PresentPosition[dir].TCSectionIndex, 0);
            if (index < 0)
                goto Exit;

            var playerTrainSpeedpostList = Locomotive.Train.PlayerTrainSpeedposts[dir];
            if (itemSequenceIndex > playerTrainSpeedpostList.Count - 1)
                goto Exit; // no n-th speedpost available
            var trainSpeedpost = playerTrainSpeedpostList[itemSequenceIndex];
            if (trainSpeedpost.DistanceToTrainM > maxDistanceM)
                goto Exit; // the requested speedpost is too distant

            // All OK, we can retrieve the data for the required speedpost;
            speedPostTypeName = Path.GetFileNameWithoutExtension(trainSpeedpost.SignalObject.SpeedPostWorldObject?.SFileName);
            isWarning = trainSpeedpost.IsWarning;
            distanceM = trainSpeedpost.DistanceToTrainM;
            speedLimitMpS = trainSpeedpost.AllowedSpeedMpS;
            altitudeM = trainSpeedpost.SignalObject.tdbtraveller.Y;

        Exit:
            return new SpeedPostFeatures(speedPostTypeName: speedPostTypeName, isWarning: isWarning, distanceM: distanceM, speedLimitMpS: speedLimitMpS,
                altitudeM: altitudeM);
        }

        public bool DoesNextNormalSignalHaveRepeaterHead()
        {
            var signal = Locomotive.Train.NextSignalObject[Locomotive.Train.MUDirection == Direction.Reverse ? 1 : 0];
            if (signal != null)
            {
                foreach (var signalHead in signal.SignalHeads)
                {
                    if (signalHead.signalType.Function.MstsFunction == MstsSignalFunction.REPEATER) return true;
                }
                return false;
            }
            return false;
        }

        public bool DoesStartFromTerminalStation()
        {
            var tempTraveller = new Traveller(Locomotive.Train.RearTDBTraveller);
            tempTraveller.ReverseDirection();
            return tempTraveller.NextTrackNode() && tempTraveller.IsEnd;
        }

        public void SignalEvent(Event evt, TrainControlSystem script)
        {
            try
            { 
                foreach (var eventHandler in Locomotive.EventHandlers)
                    eventHandler.HandleEvent(evt, script);
            }
            catch (Exception error)
            {
                Trace.TraceInformation("Sound event skipped due to thread safety problem " + error.Message);
            }
        }

        public static float SpeedCurve(float targetDistanceM, float targetSpeedMpS, float slope, float delayS, float decelerationMpS2)
        {
            if (targetSpeedMpS < 0)
                targetSpeedMpS = 0;

            decelerationMpS2 -= GravityMpS2 * slope;

            float squareSpeedComponent = targetSpeedMpS * targetSpeedMpS
                + (delayS * delayS) * decelerationMpS2 * decelerationMpS2
                + 2f * targetDistanceM * decelerationMpS2;

            float speedComponent = delayS * decelerationMpS2;

            return (float)Math.Sqrt(squareSpeedComponent) - speedComponent;
        }

        public static float DistanceCurve(float currentSpeedMpS, float targetSpeedMpS, float slope, float delayS, float decelerationMpS2)
        {
            if (targetSpeedMpS < 0)
                targetSpeedMpS = 0;

            float brakingDistanceM = (currentSpeedMpS * currentSpeedMpS - targetSpeedMpS * targetSpeedMpS)
                / (2 * (decelerationMpS2 - GravityMpS2 * slope));

            float delayDistanceM = delayS * currentSpeedMpS;

            return brakingDistanceM + delayDistanceM;
        }

        public static float Deceleration(float currentSpeedMpS, float targetSpeedMpS, float distanceM)
        {
            return (currentSpeedMpS - targetSpeedMpS) * (currentSpeedMpS + targetSpeedMpS) / (2 * distanceM);
        }

        public void Update(float elapsedClockSeconds)
        {
            switch (Locomotive.Train.TrainType)
            {
                case Train.TRAINTYPE.STATIC:
                case Train.TRAINTYPE.AI:
                case Train.TRAINTYPE.AI_NOTSTARTED:
                case Train.TRAINTYPE.AI_AUTOGENERATE:
                case Train.TRAINTYPE.REMOTE:
                case Train.TRAINTYPE.AI_INCORPORATED:
                    DisableRestrictions();
                    break;

                default:
                    if (Locomotive == Simulator.PlayerLocomotive || Locomotive.Train.PlayerTrainSignals == null)
                        Locomotive.Train.UpdatePlayerTrainData();
                    if (Script == null)
                    {
                        DisableRestrictions();
                    }
                    else
                    {
                        ClearParams();
                        Script.Update();
                    }
                    break;
            }
        }

        public void DisableRestrictions()
        {
            PowerAuthorization = true;
            if (Locomotive.TrainBrakeController != null)
            {
                Locomotive.TrainBrakeController.TCSFullServiceBraking = false;
                Locomotive.TrainBrakeController.TCSEmergencyBraking = false;
            }
        }

        public void ClearParams()
        {

        }

        public void AlerterPressed(bool pressed)
        {
            AlerterButtonPressed = pressed;
            HandleEvent(pressed ? TCSEvent.AlerterPressed : TCSEvent.AlerterReleased);
        }

        public void AlerterReset()
        {
            HandleEvent(TCSEvent.AlerterReset);
        }

        public void HandleEvent(TCSEvent evt)
        {
            HandleEvent(evt, String.Empty);
        }

        public void HandleEvent(TCSEvent evt, string message)
        {
            Script?.HandleEvent(evt, message);

            switch (evt)
            {
                case TCSEvent.EmergencyBrakingRequestedBySimulator:
                    SimulatorEmergencyBraking = true;
                    break;

                case TCSEvent.EmergencyBrakingReleasedBySimulator:
                    SimulatorEmergencyBraking = false;
                    break;
            }
        }

        public void HandleEvent(TCSEvent evt, int eventIndex)
        {
            var message = eventIndex.ToString();
            HandleEvent(evt, message);
        }

        public void HandleEvent(PowerSupplyEvent evt)
        {
            HandleEvent(evt, String.Empty);
        }

        public void HandleEvent(PowerSupplyEvent evt, string message)
        {
            Script?.HandleEvent(evt, message);
        }

        private T LoadParameter<T>(string sectionName, string keyName, T defaultValue)
        {
            string buffer;
            int length;

            if (File.Exists(TrainParametersFileName))
            {
                buffer = new string('\0', 256);
                length = NativeMethods.GetPrivateProfileString(sectionName, keyName, null, buffer, buffer.Length, TrainParametersFileName);

                if (length > 0)
                {
                    buffer.Trim();
                    return (T)Convert.ChangeType(buffer, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            if (File.Exists(ParametersFileName))
            {
                buffer = new string('\0', 256);
                length = NativeMethods.GetPrivateProfileString(sectionName, keyName, null, buffer, buffer.Length, ParametersFileName);

                if (length > 0)
                {
                    buffer.Trim();
                    return (T)Convert.ChangeType(buffer, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            return defaultValue;
        }

        // Converts the generic string (e.g. ORTS_TCS5) shown when browsing with the mouse on a TCS control
        // to a customized string defined in the script
        public string GetDisplayString(int commandIndex)
        {
            if (CustomizedCabviewControlNames.TryGetValue(commandIndex - 1, out string name)) return name;
            return "ORTS_TCS"+commandIndex;
        }

        public void Save(BinaryWriter outf)
        {
            outf.Write(ScriptName ?? "");
            if (ScriptName != "")
                Script.Save(outf);
        }

        public void Restore(BinaryReader inf)
        {
            ScriptName = inf.ReadString();
            if (ScriptName != "")
            {
                Initialize();
                Script.Restore(inf);
            }
        }
    }

    public class MSTSTrainControlSystem : TrainControlSystem
    {
        public enum MonitorState
        {
            Disabled,
            StandBy,
            Alarm,
            Emergency
        };

        public bool ResetButtonPressed { get; private set; }

        public bool VigilanceSystemEnabled
        {
            get
            {
                bool enabled = true;

                enabled &= IsAlerterEnabled();

                if (VigilanceMonitor != null)
                {
                    if (VigilanceMonitor.ResetOnDirectionNeutral)
                    {
                        enabled &= CurrentDirection() != Direction.N;
                    }

                    if (VigilanceMonitor.ResetOnZeroSpeed)
                    {
                        enabled &= SpeedMpS() >= 0.1f;
                    }
                }

                return enabled;
            }
        }

        public bool VigilanceReset
        {
            get
            {
                bool vigilanceReset = true;

                if (VigilanceMonitor != null)
                {
                    if (VigilanceMonitor.ResetOnDirectionNeutral)
                    {
                        vigilanceReset &= CurrentDirection() == Direction.N;
                    }

                    if (VigilanceMonitor.ResetOnZeroSpeed)
                    {
                        vigilanceReset &= SpeedMpS() < 0.1f;
                    }

                    if (VigilanceMonitor.ResetOnResetButton)
                    {
                        vigilanceReset &= ResetButtonPressed;
                    }
                }

                return vigilanceReset;
            }
        }

        public bool SpeedControlSystemEnabled
        {
            get
            {
                bool enabled = true;

                enabled &= IsSpeedControlEnabled();

                return enabled;
            }
        }

        public bool Overspeed
        {
            get
            {
                bool overspeed = false;

                if (OverspeedMonitor != null)
                {
                    if (OverspeedMonitor.TriggerOnOverspeedMpS > 0)
                    {
                        overspeed |= SpeedMpS() > OverspeedMonitor.TriggerOnOverspeedMpS;
                    }

                    if (OverspeedMonitor.CriticalLevelMpS > 0)
                    {
                        overspeed |= SpeedMpS() > OverspeedMonitor.CriticalLevelMpS;
                    }

                    if (OverspeedMonitor.TriggerOnTrackOverspeed)
                    {
                        overspeed |= SpeedMpS() > CurrentSpeedLimitMpS + OverspeedMonitor.TriggerOnTrackOverspeedMarginMpS;
                    }
                }

                return overspeed;
            }
        }

        public bool OverspeedReset
        {
            get
            {
                bool overspeedReset = true;

                if (OverspeedMonitor != null)
                {
                    if (OverspeedMonitor.ResetOnDirectionNeutral)
                    {
                        overspeedReset &= CurrentDirection() == Direction.N;
                    }

                    if (OverspeedMonitor.ResetOnZeroSpeed)
                    {
                        overspeedReset &= SpeedMpS() < 0.1f;
                    }

                    if (OverspeedMonitor.ResetOnResetButton)
                    {
                        overspeedReset &= ResetButtonPressed;
                    }
                }

                return overspeedReset;
            }
        }

        Timer VigilanceAlarmTimer;
        Timer VigilanceEmergencyTimer;
        Timer VigilancePenaltyTimer;
        Timer OverspeedEmergencyTimer;
        Timer OverspeedPenaltyTimer;

        MonitorState VigilanceMonitorState;
        MonitorState OverspeedMonitorState;
        bool ExternalEmergency;

        float VigilanceAlarmTimeoutS;
        float CurrentSpeedLimitMpS;
        float NextSpeedLimitMpS;
       
        MonitoringStatus Status;

        public ScriptedTrainControlSystem.MonitoringDevice VigilanceMonitor;
        public ScriptedTrainControlSystem.MonitoringDevice OverspeedMonitor;
        public ScriptedTrainControlSystem.MonitoringDevice EmergencyStopMonitor;
        public ScriptedTrainControlSystem.MonitoringDevice AWSMonitor;
        public bool EmergencyCausesThrottleDown;
        public bool EmergencyEngagesHorn;

        public MSTSTrainControlSystem() { }

        public override void Initialize()
        {
            VigilanceAlarmTimer = new Timer(this);
            VigilanceEmergencyTimer = new Timer(this);
            VigilancePenaltyTimer = new Timer(this);
            OverspeedEmergencyTimer = new Timer(this);
            OverspeedPenaltyTimer = new Timer(this);

            if (VigilanceMonitor != null)
            {
                if (VigilanceMonitor.MonitorTimeS > VigilanceMonitor.AlarmTimeS)
                    VigilanceAlarmTimeoutS = VigilanceMonitor.MonitorTimeS - VigilanceMonitor.AlarmTimeS;
                VigilanceAlarmTimer.Setup(VigilanceMonitor.AlarmTimeS);
                VigilanceEmergencyTimer.Setup(VigilanceAlarmTimeoutS);
                VigilancePenaltyTimer.Setup(VigilanceMonitor.PenaltyTimeS);
                VigilanceAlarmTimer.Start();
            }
            if (OverspeedMonitor != null)
            {
                OverspeedEmergencyTimer.Setup(Math.Max(OverspeedMonitor.AlarmTimeS, OverspeedMonitor.AlarmTimeBeforeOverspeedS));
                OverspeedPenaltyTimer.Setup(OverspeedMonitor.PenaltyTimeS);
            }

            ETCSStatus.DMIActive = ETCSStatus.PlanningAreaShown = true;

            Activated = true;
        }
        public override void Update()
        {
            UpdateInputs();

            if (IsTrainControlEnabled())
            {
                if (VigilanceMonitor != null)
                    UpdateVigilance();
                if (OverspeedMonitor != null)
                    UpdateSpeedControl();

                bool EmergencyBrake = false;
                bool FullBrake = false;
                bool PowerCut = false;

                if (VigilanceMonitor != null)
                {
                    if (VigilanceMonitor.AppliesEmergencyBrake)
                        EmergencyBrake |= (VigilanceMonitorState == MonitorState.Emergency);
                    else if (VigilanceMonitor.AppliesFullBrake)
                        FullBrake |= (VigilanceMonitorState == MonitorState.Emergency);

                    if (VigilanceMonitor.EmergencyCutsPower)
                        PowerCut |= (VigilanceMonitorState == MonitorState.Emergency);
                }

                if (OverspeedMonitor != null)
                {
                    if (OverspeedMonitor.AppliesEmergencyBrake)
                        EmergencyBrake |= (OverspeedMonitorState == MonitorState.Emergency);
                    else if (OverspeedMonitor.AppliesFullBrake)
                        FullBrake |= (OverspeedMonitorState == MonitorState.Emergency);

                    if (OverspeedMonitor.EmergencyCutsPower)
                        PowerCut |= (OverspeedMonitorState == MonitorState.Emergency);
                }

                if (EmergencyStopMonitor != null)
                {
                    if (EmergencyStopMonitor.AppliesEmergencyBrake)
                        EmergencyBrake |= ExternalEmergency;
                    else if (EmergencyStopMonitor.AppliesFullBrake)
                        FullBrake |= ExternalEmergency;

                    if (EmergencyStopMonitor.EmergencyCutsPower)
                        PowerCut |= ExternalEmergency;
                }

                UpdateTractionCutOff();
                SetTractionAuthorization(!TractionCutOffRequested);

                SetEmergencyBrake(EmergencyBrake);
                SetFullBrake(FullBrake);
                SetPowerAuthorization(!PowerCut);

                if (EmergencyCausesThrottleDown && (IsBrakeEmergency() || IsBrakeFullService()))
                    SetThrottleController(0f);

                if (EmergencyEngagesHorn)
                    SetHorn(IsBrakeEmergency() || IsBrakeFullService());

                SetPenaltyApplicationDisplay(IsBrakeEmergency() || IsBrakeFullService());

                UpdateMonitoringStatus();
                UpdateETCSPlanning();
            }
        }

        public void UpdateInputs()
        {
            SetNextSignalAspect(NextSignalAspect(0));

            CurrentSpeedLimitMpS = CurrentSignalSpeedLimitMpS();
            if (CurrentSpeedLimitMpS < 0 || CurrentSpeedLimitMpS > TrainSpeedLimitMpS())
                CurrentSpeedLimitMpS = TrainSpeedLimitMpS();

            // TODO: NextSignalSpeedLimitMpS(0) should return 0 if the signal is at stop; cause seems to be updateSpeedInfo() within Train.cs
            NextSpeedLimitMpS = NextSignalAspect(0) != Aspect.Stop ? (NextSignalSpeedLimitMpS(0) > 0 && NextSignalSpeedLimitMpS(0) < TrainSpeedLimitMpS() ? NextSignalSpeedLimitMpS(0) : TrainSpeedLimitMpS() ) : 0;

            SetCurrentSpeedLimitMpS(CurrentSpeedLimitMpS);
            SetNextSpeedLimitMpS(NextSpeedLimitMpS);
        }

        private void UpdateMonitoringStatus()
        {
            if (SpeedMpS() > CurrentSpeedLimitMpS)
            {
                if (OverspeedMonitor != null && (OverspeedMonitor.AppliesEmergencyBrake || OverspeedMonitor.AppliesFullBrake))
                    Status = MonitoringStatus.Intervention;
                else
                    Status = MonitoringStatus.Warning;
            }
            else if (NextSpeedLimitMpS < CurrentSpeedLimitMpS && SpeedMpS() > NextSpeedLimitMpS)
            {
                if (Deceleration(SpeedMpS(), NextSpeedLimitMpS, NextSignalDistanceM(0)) > 0.7f)
                    Status = MonitoringStatus.Overspeed;
                else
                    Status = MonitoringStatus.Indication;
            }
            else
                Status = MonitoringStatus.Normal;
            SetMonitoringStatus(Status);
        }

        // Provide basic functionality for ETCS DMI planning area
        protected void UpdateETCSPlanning()
        {
            float maxDistanceAheadM = 0;
            ETCSStatus.SpeedTargets.Clear();
            ETCSStatus.SpeedTargets.Add(new PlanningTarget(0, CurrentSpeedLimitMpS));
            foreach (int i in Enumerable.Range(0, 5))
            {
                maxDistanceAheadM = NextSignalDistanceM(i);
                if (NextSignalAspect(i) == Aspect.Stop || NextSignalAspect(i) == Aspect.None) break;
                float speedLimMpS = NextSignalSpeedLimitMpS(i); 
                if (speedLimMpS >= 0) ETCSStatus.SpeedTargets.Add(new PlanningTarget(maxDistanceAheadM, speedLimMpS));
            }
            float prevDist = 0;
            float prevSpeed = 0;
            foreach (int i in Enumerable.Range(0, 10))
            {
                float distanceM = NextPostDistanceM(i);
                if (distanceM >= maxDistanceAheadM) break;
                float speed = NextPostSpeedLimitMpS(i);
                if (speed == prevSpeed || distanceM - prevDist < 10) continue;
                ETCSStatus.SpeedTargets.Add(new PlanningTarget(distanceM, speed));
                prevDist = distanceM;
                prevSpeed = speed;
            }
            ETCSStatus.SpeedTargets.Sort((x, y) => x.DistanceToTrainM.CompareTo(y.DistanceToTrainM));
            ETCSStatus.SpeedTargets.Add(new PlanningTarget(maxDistanceAheadM, 0)); 
            ETCSStatus.GradientProfile.Clear();
            ETCSStatus.GradientProfile.Add(new GradientProfileElement(0, (int)(CurrentGradientPercent() * 10)));
            ETCSStatus.GradientProfile.Add(new GradientProfileElement(maxDistanceAheadM, 0)); // End of profile
        }

        public override void HandleEvent(TCSEvent evt, string message)
        {
            switch(evt)
            {
                case TCSEvent.AlerterPressed:
                case TCSEvent.AlerterReleased:
                case TCSEvent.AlerterReset:
                    if (Activated)
                    {
                        switch (VigilanceMonitorState)
                        {
                            // case VigilanceState.Disabled: do nothing

                            case MonitorState.StandBy:
                                VigilanceAlarmTimer.Stop();
                                break;

                            case MonitorState.Alarm:
                                VigilanceEmergencyTimer.Stop();
                                VigilanceMonitorState = MonitorState.StandBy;
                                break;

                            // case VigilanceState.Emergency: do nothing
                        }
                    }
                    break;
            }

            switch (evt)
            {
                case TCSEvent.AlerterPressed:
                    ResetButtonPressed = true;
                    break;

                case TCSEvent.AlerterReleased:
                    ResetButtonPressed = false;
                    break;

                case TCSEvent.EmergencyBrakingRequestedBySimulator:
                    ExternalEmergency = true;
                    break;

                case TCSEvent.EmergencyBrakingReleasedBySimulator:
                    ExternalEmergency = false;
                    break;
            }
        }

        void UpdateVigilance()
        {
            switch (VigilanceMonitorState)
            {
                case MonitorState.Disabled:
                    if (VigilanceSystemEnabled)
                    {
                        VigilanceMonitorState = MonitorState.StandBy;
                    }
                    break;

                case MonitorState.StandBy:
                    if (!VigilanceSystemEnabled)
                    {
                        VigilanceAlarmTimer.Stop();
                        VigilanceMonitorState = MonitorState.Disabled;
                    }
                    else
                    {
                        if (!VigilanceAlarmTimer.Started)
                        {
                            VigilanceAlarmTimer.Start();
                        }

                        if (VigilanceAlarmTimer.Triggered)
                        {
                            VigilanceAlarmTimer.Stop();
                            VigilanceMonitorState = MonitorState.Alarm;
                        }
                    }
                    break;

                case MonitorState.Alarm:
                    if (!VigilanceSystemEnabled)
                    {
                        VigilanceEmergencyTimer.Stop();
                        VigilanceMonitorState = MonitorState.Disabled;
                    }
                    else
                    {
                        if (!VigilanceEmergencyTimer.Started)
                        {
                            VigilanceEmergencyTimer.Start();
                        }

                        if (VigilanceEmergencyTimer.Triggered)
                        {
                            VigilanceEmergencyTimer.Stop();
                            VigilanceMonitorState = MonitorState.Emergency;
                        }
                    }
                    break;

                case MonitorState.Emergency:
                    if (!VigilancePenaltyTimer.Started)
                    {
                        VigilancePenaltyTimer.Start();
                    }

                    if (VigilancePenaltyTimer.Triggered && VigilanceReset)
                    {
                        VigilanceEmergencyTimer.Stop();
                        VigilanceMonitorState = (VigilanceSystemEnabled ? MonitorState.StandBy : MonitorState.Disabled);
                    }
                    break;
            }

            if (VigilanceMonitorState >= MonitorState.Alarm)
            {
                if (!AlerterSound())
                {
                    SetVigilanceAlarm(true);
                }
            }
            else
            {
                if (AlerterSound())
                {
                    SetVigilanceAlarm(false);
                }
            }

            SetVigilanceAlarmDisplay(VigilanceMonitorState == MonitorState.Alarm);
            SetVigilanceEmergencyDisplay(VigilanceMonitorState == MonitorState.Emergency);
        }

        void UpdateSpeedControl()
        {
            var interventionSpeedMpS = CurrentSpeedLimitMpS + MpS.FromKpH(5.0f); // Default margin : 5 km/h
            
            if (OverspeedMonitor.TriggerOnTrackOverspeed)
            {
                interventionSpeedMpS = CurrentSpeedLimitMpS + OverspeedMonitor.TriggerOnTrackOverspeedMarginMpS;
            }
            
            SetInterventionSpeedLimitMpS(interventionSpeedMpS);

            switch (OverspeedMonitorState)
            {
                case MonitorState.Disabled:
                    if (SpeedControlSystemEnabled)
                    {
                        OverspeedMonitorState = MonitorState.StandBy;
                    }
                    break;

                case MonitorState.StandBy:
                    if (!SpeedControlSystemEnabled)
                    {
                        OverspeedMonitorState = MonitorState.Disabled;
                    }
                    else
                    {
                        if (Overspeed)
                        {
                            OverspeedMonitorState = MonitorState.Alarm;
                        }
                    }
                    break;

                case MonitorState.Alarm:
                    if (!SpeedControlSystemEnabled)
                    {
                        OverspeedMonitorState = MonitorState.Disabled;
                    }
                    else
                    {
                        if (!OverspeedEmergencyTimer.Started)
                        {
                            OverspeedEmergencyTimer.Start();
                        }

                        if (!Overspeed)
                        {
                            OverspeedEmergencyTimer.Stop();
                            OverspeedMonitorState = MonitorState.StandBy;
                        }
                        else if (OverspeedEmergencyTimer.Triggered)
                        {
                            OverspeedEmergencyTimer.Stop();
                            OverspeedMonitorState = MonitorState.Emergency;
                        }
                    }
                    break;

                case MonitorState.Emergency:
                    if (!OverspeedPenaltyTimer.Started)
                    {
                        OverspeedPenaltyTimer.Start();
                    }

                    if (OverspeedPenaltyTimer.Triggered && OverspeedReset)
                    {
                        OverspeedPenaltyTimer.Stop();
                        OverspeedMonitorState = MonitorState.StandBy;
                    }
                    break;
            }

            SetOverspeedWarningDisplay(OverspeedMonitorState >= MonitorState.Alarm);
        }
    }
}
