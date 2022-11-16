// COPYRIGHT 2014 by the Open Rails project.
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

using System;
using System.Diagnostics;
using System.IO;
using Orts.Common;
using Orts.Simulation;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems;
using Orts.Simulation.RollingStocks.SubSystems.Brakes.MSTS;
using Orts.Simulation.RollingStocks.SubSystems.PowerSupplies;
using ORTS.Common;
using ORTS.Scripting.Api.ETCS;

namespace ORTS.Scripting.Api
{
    public abstract class TrainControlSystem : AbstractTrainScriptClass
    {
        internal ScriptedTrainControlSystem Host;
        internal MSTSLocomotive Locomotive => Host.Locomotive;
        internal Simulator Simulator => Host.Simulator;

        internal void AttachToHost(ScriptedTrainControlSystem host)
        {
            Host = host;
        }

        public bool Activated { get; set; }

        public readonly ETCSStatus ETCSStatus = new ETCSStatus();

        /// <summary>
        /// True if train control is switched on (the locomotive is the lead locomotive and the train is not autopiloted).
        /// </summary>
        protected bool IsTrainControlEnabled() => Locomotive == Locomotive.Train.LeadLocomotive && Locomotive.Train.TrainType != Train.TRAINTYPE.AI_PLAYERHOSTING;

        /// <summary>
        /// True if train is autopiloted
        /// </summary>
        protected bool IsAutopiloted() => Locomotive == Simulator.PlayerLocomotive && Locomotive.Train.TrainType == Train.TRAINTYPE.AI_PLAYERHOSTING;

        /// <summary>
        /// True if vigilance monitor was switched on in game options.
        /// </summary>
        protected bool IsAlerterEnabled() => Simulator.Settings.Alerter && !(Simulator.Settings.AlerterDisableExternal && !Simulator.PlayerIsInCab);

        /// <summary>
        /// True if speed control was switched on in game options.
        /// </summary>
        protected bool IsSpeedControlEnabled() => Simulator.Settings.SpeedControl;

        /// <summary>
        /// True if low voltage power supply is switched on.
        /// </summary>
        protected bool IsLowVoltagePowerSupplyOn() => Locomotive.LocomotivePowerSupply.CabPowerSupplyOn;

        /// <summary>
        /// True if cab power supply is switched on.
        /// </summary>
        protected bool IsCabPowerSupplyOn() => Locomotive.LocomotivePowerSupply.CabPowerSupplyOn;

        /// <summary>
        /// True if alerter sound rings, otherwise false
        /// </summary>
        protected bool AlerterSound() => Locomotive.AlerterSnd;

        /// <summary>
        /// Max allowed speed for the train in that moment.
        /// </summary>
        protected float TrainSpeedLimitMpS() => Math.Min(Locomotive.Train.AllowedMaxSpeedMpS, Locomotive.Train.TrainMaxSpeedMpS);

        /// <summary>
        /// Max allowed speed for the train basing on consist and route max speed.
        /// </summary>
        protected float TrainMaxSpeedMpS() => Locomotive.Train.TrainMaxSpeedMpS;

        /// <summary>
        /// Max allowed speed determined by current signal.
        /// </summary>
        protected float CurrentSignalSpeedLimitMpS() => Locomotive.Train.allowedMaxSpeedSignalMpS;

        /// <summary>
        /// Max allowed speed determined by next signal.
        /// </summary>
        protected float NextSignalSpeedLimitMpS(int itemSequenceIndex) => Host.NextGenericSignalItem(itemSequenceIndex, ref Host.ItemSpeedLimit, float.MaxValue, Train.TrainObjectItem.TRAINOBJECTTYPE.SIGNAL, "NORMAL");

        /// <summary>
        /// Aspect of the next signal.
        /// </summary>
        protected Aspect NextSignalAspect(int itemSequenceIndex) => Host.NextGenericSignalItem(itemSequenceIndex, ref Host.ItemAspect, float.MaxValue, Train.TrainObjectItem.TRAINOBJECTTYPE.SIGNAL, "NORMAL");

        /// <summary>
        /// Distance to next signal.
        /// </summary>
        protected float NextSignalDistanceM(int itemSequenceIndex) => Host.NextGenericSignalItem(itemSequenceIndex, ref Host.ItemDistance, float.MaxValue, Train.TrainObjectItem.TRAINOBJECTTYPE.SIGNAL, "NORMAL");

        /// <summary>
        /// Aspect of the DISTANCE heads of next NORMAL signal.
        /// </summary>
        protected Aspect NextNormalSignalDistanceHeadsAspect() => Host.NextNormalSignalDistanceHeadsAspect();

        /// <summary>
        /// Next normal signal has only two aspects (STOP and CLEAR_2).
        /// </summary>
        protected bool DoesNextNormalSignalHaveTwoAspects() => Host.DoesNextNormalSignalHaveTwoAspects();

        /// <summary>
        /// Aspect of the next DISTANCE signal.
        /// </summary>
        protected Aspect NextDistanceSignalAspect()
            => Host.NextGenericSignalItem(0, ref Host.ItemAspect, ScriptedTrainControlSystem.GenericItemDistance, Train.TrainObjectItem.TRAINOBJECTTYPE.SIGNAL, "DISTANCE");

        /// <summary>
        /// Distance to next DISTANCE signal.
        /// </summary>
        protected float NextDistanceSignalDistanceM() =>
            Host.NextGenericSignalItem(0, ref Host.ItemDistance, ScriptedTrainControlSystem.GenericItemDistance, Train.TrainObjectItem.TRAINOBJECTTYPE.SIGNAL, "DISTANCE");

        /// <summary>
        /// Signal type of main head of hext generic signal. Not for NORMAL signals
        /// </summary>
        protected string NextGenericSignalMainHeadSignalType(string type) =>
            Host.NextGenericSignalItem(0, ref Host.MainHeadSignalTypeName, ScriptedTrainControlSystem.GenericItemDistance, Train.TrainObjectItem.TRAINOBJECTTYPE.SIGNAL, type);

        /// <summary>
        /// Aspect of the next generic signal. Not for NORMAL signals
        /// </summary>
        protected Aspect NextGenericSignalAspect(string type) =>
            Host.NextGenericSignalItem(0, ref Host.ItemAspect, ScriptedTrainControlSystem.GenericItemDistance, Train.TrainObjectItem.TRAINOBJECTTYPE.SIGNAL, type);

        /// <summary>
        /// Distance to next generic signal. Not for NORMAL signals
        /// </summary>
        protected float NextGenericSignalDistanceM(string type) =>
            Host.NextGenericSignalItem(0, ref Host.ItemDistance, ScriptedTrainControlSystem.GenericItemDistance, Train.TrainObjectItem.TRAINOBJECTTYPE.SIGNAL, type);

        /// <summary>
        /// Features of next generic signal. 
        /// string: signal type (DISTANCE etc.)
        /// int: position of signal in the signal sequence along the train route, starting from train front; 0 for first signal;
        /// float: max testing distance
        /// </summary>
        protected SignalFeatures NextGenericSignalFeatures(string signalTypeName, int itemSequenceIndex, float maxDistanceM) =>
            Host.NextGenericSignalFeatures(signalTypeName, itemSequenceIndex, maxDistanceM, Train.TrainObjectItem.TRAINOBJECTTYPE.SIGNAL);

        /// <summary>
        /// Features of next speed post
        /// int: position of speed post in the speed post sequence along the train route, starting from train front; 0 for first speed post;
        /// float: max testing distance
        /// </summary>
        protected SpeedPostFeatures NextSpeedPostFeatures(int itemSequenceIndex, float maxDistanceM) => Host.NextSpeedPostFeatures(itemSequenceIndex, maxDistanceM);

        /// <summary>
        /// Next normal signal has a repeater head
        /// </summary>
        protected bool DoesNextNormalSignalHaveRepeaterHead() => Host.DoesNextNormalSignalHaveRepeaterHead();

        /// <summary>
        /// Max allowed speed determined by current speedpost.
        /// </summary>
        protected float CurrentPostSpeedLimitMpS() => Locomotive.Train.allowedMaxSpeedLimitMpS;

        /// <summary>
        /// Max allowed speed determined by next speedpost.
        /// </summary>
        protected float NextPostSpeedLimitMpS(int itemSequenceIndex) => 
            Host.NextGenericSignalItem(itemSequenceIndex, ref Host.ItemSpeedLimit, float.MaxValue, Train.TrainObjectItem.TRAINOBJECTTYPE.SPEEDPOST);

        /// <summary>
        /// Distance to next speedpost.
        /// </summary>
        protected float NextPostDistanceM(int itemSequenceIndex) =>
            Host.NextGenericSignalItem(itemSequenceIndex, ref Host.ItemDistance, float.MaxValue, Train.TrainObjectItem.TRAINOBJECTTYPE.SPEEDPOST);

        /// <summary>
        /// Distance and length of next tunnels
        /// int: position of tunnel along the train route, starting from train front; 0 for first tunnel;
        /// If train is in tunnel, index 0 will contain the remaining length of the tunnel
        /// </summary>
        protected TunnelInfo NextTunnel(int itemSequenceIndex)
        {
            var list = Locomotive.Train.PlayerTrainTunnels[Locomotive.Train.MUDirection == Direction.Reverse ? 1 : 0];
            if (list == null || itemSequenceIndex >= list.Count) return new TunnelInfo(float.MaxValue, -1);
            return new TunnelInfo(list[itemSequenceIndex].DistanceToTrainM, list[itemSequenceIndex].StationPlatformLength);
        }

        /// <summary>
        /// Distance and value of next mileposts
        /// int: return nth milepost ahead; 0 for first milepost
        /// </summary>
        protected MilepostInfo NextMilepost(int itemSequenceIndex)
        {
            var list = Locomotive.Train.PlayerTrainMileposts[Locomotive.Train.MUDirection == Direction.Reverse ? 1 : 0];
            if (list == null || itemSequenceIndex >= list.Count) return new MilepostInfo(float.MaxValue, -1);
            return new MilepostInfo(list[itemSequenceIndex].DistanceToTrainM, float.Parse(list[itemSequenceIndex].ThisMile));
        }

        /// <summary>
        /// Distance to end of authority.
        /// int: direction; 0: forwards; 1: backwards
        /// </summary>
        protected float EOADistanceM(int direction) => Locomotive.Train.DistanceToEndNodeAuthorityM[direction];

        /// <summary>
        /// Train's length
        /// </summary>
        protected float TrainLengthM() => Locomotive.Train != null ? Locomotive.Train.Length : 0f;

        /// <summary>
        /// Locomotive direction.
        /// </summary>
        protected Direction CurrentDirection() => Locomotive.Direction;

        /// <summary>
        /// True if locomotive direction is forward.
        /// </summary>
        protected bool IsDirectionForward() => Locomotive.Direction == Direction.Forward;

        /// <summary>
        /// True if locomotive direction is neutral.
        /// </summary>
        protected bool IsDirectionNeutral() => Locomotive.Direction == Direction.N;

        /// <summary>
        /// True if locomotive direction is reverse.
        /// </summary>
        protected bool IsDirectionReverse() => Locomotive.Direction == Direction.Reverse;

        /// <summary>
        /// Train direction.
        /// </summary>
        protected Direction CurrentTrainMUDirection() => Locomotive.Train.MUDirection;

        /// <summary>
        /// True if locomotive is flipped.
        /// </summary>
        protected bool IsFlipped() => Locomotive.Flipped;

        /// <summary>
        /// True if player is in rear cab.
        /// </summary>
        protected bool IsRearCab() => Locomotive.UsingRearCab;

        /// <summary>
        /// True if train brake controller is in emergency position, otherwise false.
        /// </summary>
        protected bool IsBrakeEmergency() => Locomotive.TrainBrakeController.EmergencyBraking;

        /// <summary>
        /// True if train brake controller is in full service position, otherwise false.
        /// </summary>
        protected bool IsBrakeFullService() => Locomotive.TrainBrakeController.TCSFullServiceBraking;

        /// <summary>
        /// True if circuit breaker or power contactor closing authorization is true.
        /// </summary>
        protected bool PowerAuthorization() => Host.PowerAuthorization;

        /// <summary>
        /// True if circuit breaker or power contactor closing order is true.
        /// </summary>
        protected bool CircuitBreakerClosingOrder() => Host.CircuitBreakerClosingOrder;

        /// <summary>
        /// True if circuit breaker or power contactor opening order is true.
        /// </summary>
        protected bool CircuitBreakerOpeningOrder() => Host.CircuitBreakerOpeningOrder;

        /// <summary>
        /// Returns the number of pantographs on the locomotive.
        /// </summary>
        protected int PantographCount() => Locomotive.Pantographs.Count;

        /// <summary>
        /// Checks the state of any pantograph
        /// int: pantograph ID (1 for first pantograph)
        /// </summary>
        protected PantographState GetPantographState(int pantoID)
        {
            if (pantoID >= Pantographs.MinPantoID && pantoID <= Pantographs.MaxPantoID)
            {
                return Locomotive.Pantographs[pantoID].State;
            }
            else
            {
                Trace.TraceError($"TCS script used bad pantograph ID {pantoID}");
                return PantographState.Down;
            }
        }

        /// <summary>
        /// True if all pantographs are down.
        /// </summary>
        protected bool ArePantographsDown() => Locomotive.Pantographs.State == PantographState.Down;

        /// <summary>
        /// Get doors state
        /// </summary>
        public Func<DoorSide, DoorState> CurrentDoorState;
        /// <summary>
        /// Returns throttle percent
        /// </summary>
        protected float ThrottlePercent() => Locomotive.ThrottleController.CurrentValue * 100;

        /// <summary>
        /// Returns maximum throttle percent
        /// </summary>
        protected float MaxThrottlePercent() => Host.MaxThrottlePercent;

        /// <summary>
        /// Returns dynamic brake percent
        /// </summary>
        protected float DynamicBrakePercent() => Locomotive.DynamicBrakeController?.CurrentValue * 100 ?? 0;

        /// <summary>
        /// True if traction is authorized.
        /// </summary>
        protected bool TractionAuthorization() => Host.TractionAuthorization;

        /// <summary>
        /// Train brake pipe pressure. Returns float.MaxValue if no data is available.
        /// For vacuum brake system, the pressure is considered as absolute.
        /// </summary>
        protected float BrakePipePressureBar() => Locomotive.BrakeSystem != null ? Bar.FromPSI(Locomotive.BrakeSystem.BrakeLine1PressurePSI) : float.MaxValue;

        /// <summary>
        /// Locomotive brake cylinder pressure. Returns float.MaxValue if no data is available.
        /// </summary>
        protected float LocomotiveBrakeCylinderPressureBar() => Locomotive.BrakeSystem != null ? Bar.FromPSI(Locomotive.BrakeSystem.GetCylPressurePSI()) : float.MaxValue;

        /// <summary>
        /// True if power must be cut if the pneumatic brake is applied.
        /// </summary>
        protected bool DoesBrakeCutPower() => Host.DoesBrakeCutPower;

        /// <summary>
        /// True if power must be cut if the vacuum brake is applied.
        /// </summary>
        protected bool DoesVacuumBrakeCutPower() => Host.DoesVacuumBrakeCutPower;

        /// <summary>
        /// Brake cylinder pressure value which triggers the power cut-off.
        /// </summary>
        protected float BrakeCutsPowerAtBrakeCylinderPressureBar() => Bar.FromPSI(Host.BrakeCutsPowerAtBrakeCylinderPressurePSI);

        /// <summary>
        /// Brake pipe pressure value which triggers the power cut-off.
        /// For vacuum brake system, the pressure is considered as absolute.
        /// </summary>
        protected float BrakeCutsPowerAtBrakePipePressureBar() =>
            Locomotive.BrakeSystem is VacuumSinglePipe
                ? Bar.FromPSI(VacuumSinglePipe.OneAtmospherePSI - Host.BrakeCutsPowerAtBrakePipePressurePSI)
                : Bar.FromPSI(Host.BrakeCutsPowerAtBrakePipePressurePSI);

        /// <summary>
        /// Brake pipe pressure value which cancels the power cut-off.
        /// For vacuum brake system, the pressure is considered as absolute.
        /// </summary>
        protected float BrakeRestoresPowerAtBrakePipePressureBar() =>
            Locomotive.BrakeSystem is VacuumSinglePipe
                ? Bar.FromPSI(VacuumSinglePipe.OneAtmospherePSI - Host.BrakeRestoresPowerAtBrakePipePressurePSI)
                : Bar.FromPSI(Host.BrakeRestoresPowerAtBrakePipePressurePSI);

        /// <summary>
        /// Train speed above which the power cut-off may be enabled.
        /// </summary>
        protected float BrakeCutsPowerForMinimumSpeedMpS() => Host.BrakeCutsPowerForMinimumSpeedMpS;

        /// <summary>
        /// True if traction cut-off cancellation needs for the throttle to be at zero or in dynamic brake position.
        /// </summary>
        protected bool BrakeCutsPowerUntilTractionCommandCancelled() => Host.BrakeCutsPowerUntilTractionCommandCancelled;

        /// <summary>
        /// Type of behaviour for traction cut-off when brakes are applied.
        /// </summary>
        protected BrakeTractionCutOffModeType BrakeTractionCutOffMode() => Host.BrakeTractionCutOffMode;

        /// <summary>
        /// State of the train brake controller.
        /// </summary>
        protected ControllerState TrainBrakeControllerState() => Locomotive.TrainBrakeController.TrainBrakeControllerState;

        /// <summary>
        /// Locomotive acceleration.
        /// </summary>
        protected float AccelerationMpSS() => Locomotive.AccelerationMpSS;

        /// <summary>
        /// Locomotive altitude.
        /// </summary>
        protected float AltitudeM() => Locomotive.WorldPosition.Location.Y;

        /// <summary>
        /// Track gradient percent at the locomotive's location (positive = uphill).
        /// </summary>
        protected float CurrentGradientPercent() => -Locomotive.CurrentElevationPercent;

        /// <summary>
        /// Line speed taken from .trk file.
        /// </summary>
        protected float LineSpeedMpS() => (float)Simulator.TRK.Tr_RouteFile.SpeedLimit;

        /// <summary>
        /// Running total of distance travelled - negative or positive depending on train direction
        /// </summary>
        protected float SignedDistanceM() => Locomotive.Train.DistanceTravelledM;

        /// <summary>
        /// True if starting from terminal station (no track behind the train).
        /// </summary>
        protected bool DoesStartFromTerminalStation() => Host.DoesStartFromTerminalStation();

        /// <summary>
        /// True if game just started and train speed = 0.
        /// </summary>
        protected bool IsColdStart() => Locomotive.Train.ColdStart;

        /// <summary>
        /// Get front traveller track node offset.
        /// </summary>
        protected float GetTrackNodeOffset() => Locomotive.Train.FrontTDBTraveller.TrackNodeLength - Locomotive.Train.FrontTDBTraveller.TrackNodeOffset;

        /// <summary>
        /// Search next diverging switch distance
        /// </summary>
        protected float NextDivergingSwitchDistanceM(float maxDistanceM)
        {
            var list = Locomotive.Train.PlayerTrainDivergingSwitches[Locomotive.Train.MUDirection == Direction.Reverse ? 1 : 0, 0];
            if (list == null || list.Count == 0 || list[0].DistanceToTrainM > maxDistanceM) return float.MaxValue;
            return list[0].DistanceToTrainM;
        }

        /// <summary>
        /// Search next trailing diverging switch distance
        /// </summary>
        protected float NextTrailingDivergingSwitchDistanceM(float maxDistanceM)
        {
            var list = Locomotive.Train.PlayerTrainDivergingSwitches[Locomotive.Train.MUDirection == Direction.Reverse ? 1 : 0, 1];
            if (list == null || list.Count == 0 || list[0].DistanceToTrainM > maxDistanceM) return float.MaxValue;
            return list[0].DistanceToTrainM;
        }

        /// <summary>
        /// Get Control Mode of player train
        /// </summary>
        protected TRAIN_CONTROL GetControlMode() => (TRAIN_CONTROL)(int)Locomotive.Train.ControlMode;

        /// <summary>
        /// Get name of next station if any, else empty string
        /// </summary>
        protected string NextStationName() =>
            Locomotive.Train.StationStops != null && Locomotive.Train.StationStops.Count > 0
                ? Locomotive.Train.StationStops[0].PlatformItem.Name
                : "";

        /// <summary>
        /// Get distance of next station if any, else max float value
        /// </summary>
        protected float NextStationDistanceM() =>
            Locomotive.Train.StationStops != null && Locomotive.Train.StationStops.Count > 0
                ? Locomotive.Train.StationStops[0].DistanceToTrainM
                : float.MaxValue;

        /// <summary>
        /// (float targetDistanceM, float targetSpeedMpS, float slope, float delayS, float decelerationMpS2)
        /// Returns a speed curve based speed limit, unit is m/s
        /// </summary>
        protected float SpeedCurve(float targetDistanceM, float targetSpeedMpS, float slope, float delayS, float decelerationMpS2) =>
            ScriptedTrainControlSystem.SpeedCurve(targetDistanceM, targetSpeedMpS, slope, delayS, decelerationMpS2);

        /// <summary>
        /// (float currentSpeedMpS, float targetSpeedMpS, float slope, float delayS, float decelerationMpS2)
        /// Returns a distance curve based safe braking distance, unit is m
        /// </summary>
        protected float DistanceCurve(float currentSpeedMpS, float targetSpeedMpS, float slope, float delayS, float decelerationMpS2) =>
            ScriptedTrainControlSystem.DistanceCurve(currentSpeedMpS, targetSpeedMpS, slope, delayS, decelerationMpS2);

        /// <summary>
        /// (float currentSpeedMpS, float targetSpeedMpS, float distanceM)
        /// Returns the deceleration needed to decrease the speed to the target speed at the target distance
        /// </summary>
        protected float Deceleration(float currentSpeedMpS, float targetSpeedMpS, float distanceM) =>
            ScriptedTrainControlSystem.Deceleration(currentSpeedMpS, targetSpeedMpS, distanceM);

        /// <summary>
        /// Set train brake controller to full service position.
        /// </summary>
        protected void SetFullBrake(bool value)
        {
            if (Locomotive.TrainBrakeController.TCSFullServiceBraking != value)
            {
                Locomotive.TrainBrakeController.TCSFullServiceBraking = value;

                //Debrief Eval
                if (value && Locomotive.IsPlayerTrain && !Host.ldbfevalfullbrakeabove16kmh && Math.Abs(Locomotive.SpeedMpS) > 4.44444)
                {
                    var train = Simulator.PlayerLocomotive.Train;//Debrief Eval
                    ScriptedTrainControlSystem.DbfevalFullBrakeAbove16kmh++;
                    Host.ldbfevalfullbrakeabove16kmh = true;
                    train.DbfEvalValueChanged = true;//Debrief eval
                }
                if (!value)
                    Host.ldbfevalfullbrakeabove16kmh = false;
            }
        }

        /// <summary>
        /// Set emergency braking on or off.
        /// </summary>
        protected void SetEmergencyBrake(bool value)
        {
            if (Locomotive.TrainBrakeController.TCSEmergencyBraking != value)
                Locomotive.TrainBrakeController.TCSEmergencyBraking = value;
        }

        /// Set full dynamic braking on or off.
        /// </summary>
        protected void SetFullDynamicBrake(bool value) => Host.FullDynamicBrakingOrder = value;

        /// <summary>
        /// Set throttle controller to position in range [0-1].
        /// </summary>
        protected void SetThrottleController(float value) => Locomotive.ThrottleController.SetValue(value);

        /// <summary>
        /// Set dynamic brake controller to position in range [0-1].
        /// </summary>
        protected void SetDynamicBrakeController(float value)
        {
            if (Locomotive.DynamicBrakeController == null)
                return;

            Locomotive.DynamicBrakeChangeActiveState(value > 0);
            Locomotive.DynamicBrakeController.SetValue(value);
        }

        /// <summary>
        /// Cut power by pull all pantographs down.
        /// </summary>
        protected void SetPantographsDown()
        {
            if (Locomotive.Pantographs.State == PantographState.Up)
            {
                Locomotive.Train.SignalEvent(PowerSupplyEvent.LowerPantograph);
            }
        }

        /// <summary>
        /// Raise specified pantograph
        /// int: pantographID, from 1 to 4
        /// </summary>
        protected void SetPantographUp(int pantoID)
        {
            if (pantoID < Pantographs.MinPantoID || pantoID > Pantographs.MaxPantoID)
            {
                Trace.TraceError($"TCS script used bad pantograph ID {pantoID}");
                return;
            }
            Locomotive.Train.SignalEvent(PowerSupplyEvent.RaisePantograph, pantoID);
        }

        /// <summary>
        /// Lower specified pantograph
        /// int: pantographID, from 1 to 4
        /// </summary>
        protected void SetPantographDown(int pantoID)
        {
            if (pantoID<Pantographs.MinPantoID || pantoID> Pantographs.MaxPantoID)
            {
                Trace.TraceError($"TCS script used bad pantograph ID {pantoID}");
                return;
            }
            Locomotive.Train.SignalEvent(PowerSupplyEvent.LowerPantograph, pantoID);
        }

        /// <summary>
        /// Set the circuit breaker or power contactor closing authorization.
        /// </summary>
        protected void SetPowerAuthorization(bool value) => Host.PowerAuthorization = value;

        /// <summary>
        /// Set the circuit breaker or power contactor closing order.
        /// </summary>
        protected void SetCircuitBreakerClosingOrder(bool value) => Host.CircuitBreakerClosingOrder = value;

        /// <summary>
        /// Set the circuit breaker or power contactor opening order.
        /// </summary>
        protected void SetCircuitBreakerOpeningOrder(bool value) => Host.CircuitBreakerOpeningOrder = value;

        /// <summary>
        /// Set the traction authorization.
        /// </summary>
        protected void SetTractionAuthorization(bool value) => Host.TractionAuthorization = value;

        /// <summary>
        /// Set the maximum throttle percent
        /// Range: 0 to 100
        /// </summary>
        protected void SetMaxThrottlePercent(float value)
        {
            if (value >= 0 && value <= 100f)
            {
                Host.MaxThrottlePercent = value;
            }
        }

        /// <summary>
        /// Switch vigilance alarm sound on (true) or off (false).
        /// </summary>
        protected void SetVigilanceAlarm(bool value) => Locomotive.SignalEvent(value ? Event.VigilanceAlarmOn : Event.VigilanceAlarmOff);

        /// <summary>
        /// Set horn on (true) or off (false).
        /// </summary>
        protected void SetHorn(bool value) => Locomotive.TCSHorn = value;

        /// <summary>
        /// Open or close doors
        /// DoorSide: side for which doors will be opened or closed
        /// bool: true for closing order, false for opening order
        /// </summary>
        public Action<DoorSide, bool> SetDoors;
        /// <summary>
        /// Lock doors so they cannot be opened
        /// </summary>
        public Action<DoorSide, bool> LockDoors;
        /// <summary>
        /// Trigger Alert1 sound event
        /// </summary>
        protected void TriggerSoundAlert1() => Host.SignalEvent(Event.TrainControlSystemAlert1, this);

        /// <summary>
        /// Trigger Alert2 sound event
        /// </summary>
        protected void TriggerSoundAlert2() => Host.SignalEvent(Event.TrainControlSystemAlert2, this);

        /// <summary>
        /// Trigger Info1 sound event
        /// </summary>
        protected void TriggerSoundInfo1() => Host.SignalEvent(Event.TrainControlSystemInfo1, this);

        /// <summary>
        /// Trigger Info2 sound event
        /// </summary>
        protected void TriggerSoundInfo2() => Host.SignalEvent(Event.TrainControlSystemInfo2, this);

        /// <summary>
        /// Trigger Penalty1 sound event
        /// </summary>
        protected void TriggerSoundPenalty1() => Host.SignalEvent(Event.TrainControlSystemPenalty1, this);

        /// <summary>
        /// Trigger Penalty2 sound event
        /// </summary>
        protected void TriggerSoundPenalty2() => Host.SignalEvent(Event.TrainControlSystemPenalty2, this);

        /// <summary>
        /// Trigger Warning1 sound event
        /// </summary>
        protected void TriggerSoundWarning1() => Host.SignalEvent(Event.TrainControlSystemWarning1, this);

        /// <summary>
        /// Trigger Warning2 sound event
        /// </summary>
        protected void TriggerSoundWarning2() => Host.SignalEvent(Event.TrainControlSystemWarning2, this);

        /// <summary>
        /// Trigger Activate sound event
        /// </summary>
        protected void TriggerSoundSystemActivate() => Host.SignalEvent(Event.TrainControlSystemActivate, this);

        /// <summary>
        /// Trigger Deactivate sound event
        /// </summary>
        protected void TriggerSoundSystemDeactivate() => Host.SignalEvent(Event.TrainControlSystemDeactivate, this);

        /// <summary>
        /// Trigger generic sound event
        /// </summary>
        protected void TriggerGenericSound(Event value) => Host.SignalEvent(value, this);

        /// <summary>
        /// Set ALERTER_DISPLAY cabcontrol display's alarm state on or off.
        /// </summary>
        protected void SetVigilanceAlarmDisplay(bool value) => Host.VigilanceAlarm = value;

        /// <summary>
        /// Set ALERTER_DISPLAY cabcontrol display's emergency state on or off.
        /// </summary>
        protected void SetVigilanceEmergencyDisplay(bool value) => Host.VigilanceEmergency = value;

        /// <summary>
        /// Set OVERSPEED cabcontrol display on or off.
        /// </summary>
        protected void SetOverspeedWarningDisplay(bool value) => Host.OverspeedWarning = value;

        /// <summary>
        /// Set PENALTY_APP cabcontrol display on or off.
        /// </summary>
        protected void SetPenaltyApplicationDisplay(bool value) => Host.PenaltyApplication = value;

        /// <summary>
        /// Monitoring status determines the colors speeds displayed with. (E.g. circular speed gauge).
        /// </summary>
        protected void SetMonitoringStatus(MonitoringStatus value)
        {
            switch (value)
            {
                case MonitoringStatus.Normal:
                case MonitoringStatus.Indication:
                    ETCSStatus.CurrentMonitor = Monitor.CeilingSpeed;
                    ETCSStatus.CurrentSupervisionStatus = SupervisionStatus.Normal;
                    break;
                case MonitoringStatus.Overspeed:
                    ETCSStatus.CurrentMonitor = Monitor.TargetSpeed;
                    ETCSStatus.CurrentSupervisionStatus = SupervisionStatus.Indication;
                    break;
                case MonitoringStatus.Warning:
                    ETCSStatus.CurrentSupervisionStatus = SupervisionStatus.Overspeed;
                    break;
                case MonitoringStatus.Intervention:
                    ETCSStatus.CurrentSupervisionStatus = SupervisionStatus.Intervention;
                    break;
            }
        }

        /// <summary>
        /// Set current speed limit of the train, as to be shown on SPEEDLIMIT cabcontrol.
        /// </summary>
        protected void SetCurrentSpeedLimitMpS(float value)
        {
            Host.CurrentSpeedLimitMpS = value;
            ETCSStatus.AllowedSpeedMpS = value;
        }

        /// <summary>
        /// Set speed limit of the next signal, as to be shown on SPEEDLIM_DISPLAY cabcontrol.
        /// </summary>
        protected void SetNextSpeedLimitMpS(float value)
        {
            Host.NextSpeedLimitMpS = value;
            ETCSStatus.TargetSpeedMpS = value;
        }

        /// <summary>
        /// The speed at the train control system applies brake automatically.
        /// Determines needle color (orange/red) on circular speed gauge, when the locomotive
        /// already runs above the permitted speed limit. Otherwise is unused.
        /// </summary>
        protected void SetInterventionSpeedLimitMpS(float value) => ETCSStatus.InterventionSpeedMpS = value;

        /// <summary>
        /// Will be whown on ASPECT_DISPLAY cabcontrol.
        /// </summary>
        protected void SetNextSignalAspect(Aspect value) => Host.CabSignalAspect = (TrackMonitorSignalAspect)value;

        /// <summary>
        /// Sets the value for a cabview control.
        /// </summary>
        protected void SetCabDisplayControl(int id, float value) => Host.CabDisplayControls[id] = value;

        /// <summary>
        /// Sets the name which is to be shown which putting the cursor above a cabview control.
        /// DEPRECATED
        /// </summary>
        protected void SetCustomizedTCSControlString(string value)
        {
            if (Host.NextCabviewControlNameToEdit == 0)
            {
                Trace.TraceWarning("SetCustomizedTCSControlString is deprecated. Please use SetCustomizedCabviewControlName.");
            }

            Host.CustomizedCabviewControlNames[Host.NextCabviewControlNameToEdit] = value;

            Host.NextCabviewControlNameToEdit++;
        }

        /// <summary>
        /// Sets the name which is to be shown which putting the cursor above a cabview control.
        /// </summary>
        protected void SetCustomizedCabviewControlName(int id, string name)
        {
            if (id >= 0)
            {
                Host.CustomizedCabviewControlNames[id] = name;
            }
        }

        /// <summary>
        /// Requests toggle to and from Manual Mode.
        /// </summary>
        protected void RequestToggleManualMode()
        {
            if (Locomotive.Train.ControlMode == Train.TRAIN_CONTROL.OUT_OF_CONTROL && Locomotive.Train.ControlModeBeforeOutOfControl == Train.TRAIN_CONTROL.EXPLORER)
            {
                Trace.TraceWarning("RequestToggleManualMode() is deprecated for explorer mode. Please use ResetOutOfControlMode() instead");
                Locomotive.Train.ManualResetOutOfControlMode();
            }
            else Locomotive.Train.RequestToggleManualMode();
        }

        /// <summary>
        /// Requests reset of Out of Control Mode.
        /// </summary>
        protected void ResetOutOfControlMode() => Locomotive.Train.ManualResetOutOfControlMode();

        /// <summary>
        /// Get bool parameter in the INI file.
        /// </summary>
        protected bool GetBoolParameter(string sectionName, string keyName, bool defaultValue) => LoadParameter(sectionName, keyName, defaultValue);

        /// <summary>
        /// Get int parameter in the INI file.
        /// </summary>
        protected int GetIntParameter(string sectionName, string keyName, int defaultValue) => LoadParameter(sectionName, keyName, defaultValue);

        /// <summary>
        /// Get int parameter in the INI file.
        /// </summary>
        protected float GetFloatParameter(string sectionName, string keyName, float defaultValue) => LoadParameter(sectionName, keyName, defaultValue);

        /// <summary>
        /// Get string parameter in the INI file.
        /// </summary>
        protected string GetStringParameter(string sectionName, string keyName, string defaultValue) => LoadParameter(sectionName, keyName, defaultValue);

        protected T LoadParameter<T>(string sectionName, string keyName, T defaultValue)
        {
            string buffer;
            int length;

            if (File.Exists(Host.TrainParametersFileName))
            {
                buffer = new string('\0', 256);
                length = NativeMethods.GetPrivateProfileString(sectionName, keyName, null, buffer, buffer.Length, Host.TrainParametersFileName);

                if (length > 0)
                {
                    buffer.Trim();
                    return (T)Convert.ChangeType(buffer, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            if (File.Exists(Host.ParametersFileName))
            {
                buffer = new string('\0', 256);
                length = NativeMethods.GetPrivateProfileString(sectionName, keyName, null, buffer, buffer.Length, Host.ParametersFileName);

                if (length > 0)
                {
                    buffer.Trim();
                    return (T)Convert.ChangeType(buffer, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            return defaultValue;
        }

        /// <summary>
        /// Sends an event and/or a message to the power supply
        /// </summary>
        /// <param name="evt">The event to send</param>
        /// <param name="message">The message to send</param>
        protected void SignalEventToPowerSupply(PowerSupplyEvent evt = PowerSupplyEvent.MessageFromTcs, string message = "")
        {
            Locomotive.LocomotivePowerSupply.HandleEventFromTcs(evt, message);
        }

        /// <summary>
        /// Called once at initialization time.
        /// </summary>
        public abstract void Initialize();

        /// <summary>
        /// Called once at initialization time if the train speed is greater than 0.
        /// Set as virtual to keep compatibility with scripts not providing this method.
        /// </summary>
        public virtual void InitializeMoving() { }

        /// <summary>
        /// Called regularly at every simulator update cycle.
        /// </summary>
        public abstract void Update();

        /// <summary>
        /// Called when a TCS event happens (like the alerter button pressed)
        /// </summary>
        /// <param name="evt">The event happened</param>
        /// <param name="message">The message the event wants to communicate. May be empty.</param>
        public abstract void HandleEvent(TCSEvent evt, string message);

        /// <summary>
        /// Called when a power supply event happens (like the circuit breaker closed)
        /// Set at virtual to keep compatibility with scripts not providing this method.
        /// </summary>
        /// <param name="evt">The event happened</param>
        /// <param name="message">The message the event wants to communicate. May be empty.</param>
        public virtual void HandleEvent(PowerSupplyEvent evt, string message) { }

        /// <summary>
        /// Called by signalling code externally to stop the train in certain circumstances.
        /// </summary>
        [Obsolete("SetEmergency method is deprecated, use HandleEvent(TCSEvent, string) instead")]
        public virtual void SetEmergency(bool emergency) { }

        /// <summary>
        /// Called when player has requested a game save. 
        /// Set at virtual to keep compatibility with scripts not providing this method.
        /// </summary>
        public virtual void Save(BinaryWriter outf) { }

        /// <summary>
        /// Called when player has requested a game restore. 
        /// Set at virtual to keep compatibility with scripts not providing this method.
        /// </summary>
        public virtual void Restore(BinaryReader inf) { }

        /// <summary>
        /// Traction cut-off request due to brake application
        /// True if cut-off is requested
        /// </summary>
        protected bool TractionCutOffRequested = false;

        /// <summary>
        /// Updates the traction cut-off request (due to brake application).
        /// </summary>
        /// <returns>true if traction cut-off is requested</returns>
        public virtual void UpdateTractionCutOff()
        {
            // If BrakeCutsPowerForSpeedAbove is not set (== 0), the brake pressure check is always active.
            if (SpeedMpS() >= BrakeCutsPowerForMinimumSpeedMpS())
            {
                switch (BrakeTractionCutOffMode())
                {
                    case BrakeTractionCutOffModeType.None:
                        TractionCutOffRequested = false;
                        break;

                    case BrakeTractionCutOffModeType.AirBrakeCylinderSinglePressure:
                        if (LocomotiveBrakeCylinderPressureBar() >= BrakeCutsPowerAtBrakeCylinderPressureBar())
                        {
                            TractionCutOffRequested = true;
                        }
                        else if (!BrakeCutsPowerUntilTractionCommandCancelled() || ThrottlePercent() <= 0f)
                        {
                            TractionCutOffRequested = false;
                        }
                        break;

                    case BrakeTractionCutOffModeType.AirBrakePipeSinglePressure:
                        if (BrakePipePressureBar() <= BrakeCutsPowerAtBrakePipePressureBar())
                        {
                            TractionCutOffRequested = true;
                        }
                        else if (!BrakeCutsPowerUntilTractionCommandCancelled() || ThrottlePercent() <= 0f)
                        {
                            TractionCutOffRequested = false;
                        }
                        break;

                    case BrakeTractionCutOffModeType.AirBrakePipeHysteresis:
                        if (BrakePipePressureBar() <= BrakeCutsPowerAtBrakePipePressureBar())
                        {
                            TractionCutOffRequested = true;
                        }
                        else if (BrakePipePressureBar() >= BrakeRestoresPowerAtBrakePipePressureBar()
                            && (!BrakeCutsPowerUntilTractionCommandCancelled() || ThrottlePercent() <= 0f))
                        {
                            TractionCutOffRequested = false;
                        }
                        break;

                    case BrakeTractionCutOffModeType.VacuumBrakePipeHysteresis:
                        if (BrakePipePressureBar() >= BrakeCutsPowerAtBrakePipePressureBar())
                        {
                            TractionCutOffRequested = true;
                        }
                        else if (BrakePipePressureBar() <= BrakeRestoresPowerAtBrakePipePressureBar()
                            && (!BrakeCutsPowerUntilTractionCommandCancelled() || ThrottlePercent() <= 0f))
                        {
                            TractionCutOffRequested = false;
                        }
                        break;
                }
            }
            else
            {
                if (!BrakeCutsPowerUntilTractionCommandCancelled() || ThrottlePercent() <= 0f)
                {
                    TractionCutOffRequested = false;
                }
            }
        }
    }

    // Represents the same enum as TrackMonitorSignalAspect
    /// <summary>
    /// A signal aspect, as shown on track monitor
    /// </summary>
    public enum Aspect
    {
        None,
        Clear_2,
        Clear_1,
        Approach_3,
        Approach_2,
        Approach_1,
        Restricted,
        StopAndProceed,
        Stop,
        Permission,
    }

    // Represents the same enum as TRAIN_CONTROL

    public enum TRAIN_CONTROL
        {
            AUTO_SIGNAL,
            AUTO_NODE,
            MANUAL,
            EXPLORER,
            OUT_OF_CONTROL,
            INACTIVE,
            TURNTABLE,
            UNDEFINED
        }

    public enum TCSEvent
    {
        /// <summary>
        /// Emergency braking requested by simulator (train is out of control).
        /// </summary>
        EmergencyBrakingRequestedBySimulator,
        /// <summary>
        /// Emergency braking released by simulator.
        /// </summary>
        EmergencyBrakingReleasedBySimulator,
        /// <summary>
        /// Manual reset of the train's out of control mode.
        /// </summary>
        ManualResetOutOfControlMode,
        /// <summary>
        /// Reset request by pressing the alerter button.
        /// </summary>
        AlerterPressed,
        /// <summary>
        /// Alerter button was released.
        /// </summary>
        AlerterReleased,
        /// <summary>
        /// Internal reset request by touched systems other than the alerter button.
        /// </summary>
        AlerterReset,
        /// <summary>
        /// Internal reset request by the reverser.
        /// </summary>
        ReverserChanged,
        /// <summary>
        /// Internal reset request by the throttle controller.
        /// </summary>
        ThrottleChanged,
        /// <summary>
        /// Internal reset request by the gear box controller.
        /// </summary>
        GearBoxChanged,
        /// <summary>
        /// Internal reset request by the train brake controller.
        /// </summary>
        TrainBrakeChanged,
        /// <summary>
        /// Internal reset request by the engine brake controller.
        /// </summary>
        EngineBrakeChanged,
         /// <summary>
        /// Internal reset request by the brakeman brake controller.
        /// </summary>
        BrakemanBrakeChanged,
        /// <summary>
        /// Internal reset request by the dynamic brake controller.
        /// </summary>
        DynamicBrakeChanged,
        /// <summary>
        /// Internal reset request by the horn handle.
        /// </summary>
        HornActivated,
        /// <summary>
        /// Generic TCS button pressed.
        /// </summary>
        GenericTCSButtonPressed,
        /// <summary>
        /// Generic TCS button released.
        /// </summary>
        GenericTCSButtonReleased,
        /// <summary>
        /// Generic TCS switch toggled off.
        /// </summary>
        GenericTCSSwitchOff,
        /// <summary>
        /// Generic TCS switch toggled on.
        /// </summary>
        GenericTCSSwitchOn,
        /// <summary>
        /// Circuit breaker has been closed.
        /// </summary>
        CircuitBreakerClosed,
        /// <summary>
        /// Circuit breaker has been opened.
        /// </summary>
        CircuitBreakerOpen,
        /// <summary>
        /// Traction cut-off relay has been closed.
        /// </summary>
        TractionCutOffRelayClosed,
        /// <summary>
        /// Traction cut-off relay has been opened.
        /// </summary>
        TractionCutOffRelayOpen,
        /// <summary>
        /// Left doors have been opened.
        /// </summary>
        LeftDoorsOpen,
        /// <summary>
        /// Left doors have been closed.
        /// </summary>
        LeftDoorsClosed,
        /// <summary>
        /// Right doors have been opened.
        /// </summary>
        RightDoorsOpen,
        /// <summary>
        /// Right doors have been closed.
        /// </summary>
        RightDoorsClosed
    }

    /// <summary>
    /// Controls what color the speed monitoring display uses.
    /// </summary>
    public enum MonitoringStatus
    {
        /// <summary>
        /// Grey color. No speed restriction is ahead.
        /// </summary>
        Normal,
        /// <summary>
        /// White color. Pre-indication, that the next signal is restricted. No manual intervention is needed yet.
        /// </summary>
        Indication,
        /// <summary>
        /// Yellow color. Next signal is restricted, driver should start decreasing speed.
        /// (Please note, it is not for indication of a "real" overspeed. In this state the locomotive still runs under the actual permitted speed.)
        /// </summary>
        Overspeed,
        /// <summary>
        /// Orange color. The locomotive is very close to next speed restriction, driver should start strong braking immediately.
        /// </summary>
        Warning,
        /// <summary>
        /// Red color. Train control system intervention speed. Computer has to apply full service or emergency brake to maintain speed restriction.
        /// </summary>
        Intervention,
    }

    public enum BrakeTractionCutOffModeType
    {
        None,
        AirBrakeCylinderSinglePressure,
        AirBrakePipeSinglePressure,
        AirBrakePipeHysteresis,
        VacuumBrakePipeHysteresis,
    }

    public struct SignalFeatures
    {
        public readonly string MainHeadSignalTypeName;
        public readonly string SignalTypeName;
        public readonly Aspect Aspect;
        public readonly string DrawStateName;
        public readonly float DistanceM;
        public readonly float SpeedLimitMpS;
        public readonly float AltitudeM;
        public readonly string TextAspect;

        public SignalFeatures(string mainHeadSignalTypeName, string signalTypeName, Aspect aspect, string drawStateName, float distanceM, float speedLimitMpS, float altitudeM, string textAspect = "")
        {
            MainHeadSignalTypeName = mainHeadSignalTypeName;
            SignalTypeName = signalTypeName;
            Aspect = aspect;
            DrawStateName = drawStateName;
            DistanceM = distanceM;
            SpeedLimitMpS = speedLimitMpS;
            AltitudeM = altitudeM;
            TextAspect = textAspect;
        }
    }

    public struct SpeedPostFeatures
    {
        public readonly string SpeedPostTypeName;
        public readonly bool IsWarning;
        public readonly float DistanceM;
        public readonly float SpeedLimitMpS;
        public readonly float AltitudeM;

        public SpeedPostFeatures(string speedPostTypeName, bool isWarning, float distanceM, float speedLimitMpS, float altitudeM)
        {
            SpeedPostTypeName = speedPostTypeName;
            IsWarning = isWarning;
            DistanceM = distanceM;
            SpeedLimitMpS = speedLimitMpS;
            AltitudeM = altitudeM;
        }
    }

    public struct TunnelInfo
    {
        /// <summary>
        /// Distance to tunnel (m)
        /// -1 if train is in tunnel
        /// </summary>
        public readonly float DistanceM;
        /// <summary>
        /// Tunnel length (m)
        /// If train is in tunnel, remaining distance to exit
        /// </summary>
        public readonly float LengthM;

        public TunnelInfo(float distanceM, float lengthM)
        {
            DistanceM = distanceM;
            LengthM = lengthM;
        }
    }

    public struct MilepostInfo
    {
        /// <summary>
        /// Distance to milepost (m)
        /// </summary>
        public readonly float DistanceM;
        /// <summary>
        /// Value of the milepost
        /// </summary>
        public readonly float Value;

        public MilepostInfo(float distanceM, float value)
        {
            DistanceM = distanceM;
            Value = value;
        }
    }
}
