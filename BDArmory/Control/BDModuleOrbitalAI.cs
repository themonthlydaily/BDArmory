using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

using BDArmory.Extensions;
using BDArmory.Targeting;
using BDArmory.Settings;
using BDArmory.UI;
using BDArmory.Utils;
using BDArmory.Weapons;
using BDArmory.Guidances;

namespace BDArmory.Control
{
    public class BDModuleOrbitalAI : BDGenericAIBase, IBDAIControl
    {
        // Code contained within this file is adapted from Hatbat, Spartwo and MiffedStarfish's Kerbal Combat Systems Mod https://github.com/Halbann/StockCombatAI/tree/dev/Source/KerbalCombatSystems.
        // Code is distributed under CC-BY-SA 4.0: https://creativecommons.org/licenses/by-sa/4.0/

        #region Declarations

        // Orbiter AI variables.
        public float updateInterval;
        public float emergencyUpdateInterval = 0.5f;
        public float combatUpdateInterval = 2.5f;
        private bool allowWithdrawal = true;
        public float firingAngularVelocityLimit = 1; // degrees per second

        private BDOrbitalControl fc;

        public IBDWeapon currentWeapon;

        private float trackedDeltaV;
        private Vector3 attitudeCommand;
        private PilotCommands lastUpdateCommand = PilotCommands.Free;
        private float maneuverTime;
        private float minManeuverTime;
        private bool maneuverStateChanged = false;
        private bool belowSafeAlt = false;
        private bool wasDescendingUnsafe = false;
        private bool hasPropulsion;
        private bool hasWeapons;
        private float maxAcceleration;
        private float maxThrust;
        private Vector3 maxAngularAcceleration;
        private Vector3 availableTorque;
        private double minSafeAltitude;
        private CelestialBody safeAltBody = null;

        // Evading
        bool evadingGunfire = false;
        float evasiveTimer;
        Vector3 threatRelativePosition;
        Vector3 evasionNonLinearityDirection;
        string evasionString = " & Evading Gunfire";

        // User parameters changed via UI.

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_AI_MinEngagementRange"),//Min engagement range
            UI_FloatSemiLogRange(minValue = 10f, maxValue = 10000f, scene = UI_Scene.All)]
        public float MinEngagementRange = 500;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_AI_ManeuverRCS"),//RCS active
            UI_Toggle(enabledText = "#LOC_BDArmory_AI_ManeuverRCS_enabledText", disabledText = "#LOC_BDArmory_AI_ManeuverRCS_disabledText", scene = UI_Scene.All),]//Maneuvers--Combat
        public bool ManeuverRCS = false;

        public float vesselStandoffDistance = 200f; // try to avoid getting closer than 200m

        [KSPField(isPersistant = true,
            guiActive = true,
            guiActiveEditor = true,
            guiName = "#LOC_BDArmory_AI_ManeuverSpeed",
            guiUnits = " m/s"),
            UI_FloatSemiLogRange(
                minValue = 10f,
                maxValue = 10000f,
                stepIncrement = 10f,
                scene = UI_Scene.All
            )]
        public float ManeuverSpeed = 100f;

        [KSPField(isPersistant = true,
            guiActive = true,
            guiActiveEditor = true,
            guiName = "#LOC_BDArmory_AI_StrafingSpeed",
            guiUnits = " m/s"),
            UI_FloatSemiLogRange(
                minValue = 2f,
                maxValue = 1000f,
                scene = UI_Scene.All
            )]
        public float firingSpeed = 20f;

        #region Evade
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_AI_MinEvasionTime", advancedTweakable = true, // Min Evasion Time
            groupName = "pilotAI_EvadeExtend", groupDisplayName = "#LOC_BDArmory_AI_EvadeExtend", groupStartCollapsed = true),
            UI_FloatRange(minValue = 0f, maxValue = 1f, stepIncrement = .05f, scene = UI_Scene.All)]
        public float minEvasionTime = 0.2f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_AI_EvasionThreshold", advancedTweakable = true, //Evasion Distance Threshold
            groupName = "pilotAI_EvadeExtend", groupDisplayName = "#LOC_BDArmory_AI_EvadeExtend", groupStartCollapsed = true),
            UI_FloatRange(minValue = 0f, maxValue = 100f, stepIncrement = 1f, scene = UI_Scene.All)]
        public float evasionThreshold = 25f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_AI_EvasionTimeThreshold", advancedTweakable = true, // Evasion Time Threshold
            groupName = "pilotAI_EvadeExtend", groupDisplayName = "#LOC_BDArmory_AI_EvadeExtend", groupStartCollapsed = true),
            UI_FloatRange(minValue = 0f, maxValue = 5f, stepIncrement = 0.1f, scene = UI_Scene.All)]
        public float evasionTimeThreshold = 0.1f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_AI_EvasionErraticness", advancedTweakable = true, // Evasion Erraticness
            groupName = "pilotAI_EvadeExtend", groupDisplayName = "#LOC_BDArmory_AI_EvadeExtend", groupStartCollapsed = true),
            UI_FloatRange(minValue = 0f, maxValue = 1f, stepIncrement = 0.01f, scene = UI_Scene.All)]
        public float evasionErraticness = 0.1f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_AI_EvasionMinRangeThreshold", advancedTweakable = true, // Evasion Min Range Threshold
            groupName = "pilotAI_EvadeExtend", groupDisplayName = "#LOC_BDArmory_AI_EvadeExtend", groupStartCollapsed = true),
            UI_FloatSemiLogRange(minValue = 10f, maxValue = 10000f, sigFig = 1)]
        public float evasionMinRangeThreshold = 10f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_AI_EvasionIgnoreMyTargetTargetingMe", advancedTweakable = true,//Ignore my target targeting me
            groupName = "pilotAI_EvadeExtend", groupDisplayName = "#LOC_BDArmory_AI_EvadeExtend", groupStartCollapsed = true),
            UI_Toggle(enabledText = "#LOC_BDArmory_Enabled", disabledText = "#LOC_BDArmory_Disabled", scene = UI_Scene.All),]
        public bool evasionIgnoreMyTargetTargetingMe = false;
        #endregion


        // Debugging
        internal float distToCPA;
        internal float timeToCPA;
        internal float stoppingDist;
        internal Vector3 debugPosition;

        /// <summary>
        /// //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// </summary>

        Vector3 upDir;

        #endregion

        #region Status Mode
        public enum StatusMode { Idle, Evading, CorrectingOrbit, Withdrawing, Firing, Maneuvering, Stranded, Commanded, Custom }
        public StatusMode currentStatusMode = StatusMode.Idle;
        StatusMode lastStatusMode = StatusMode.Idle;
        protected override void SetStatus(string status)
        {
            if (evadingGunfire)
                status += evasionString;

            base.SetStatus(status);
            if (status.StartsWith("Idle")) currentStatusMode = StatusMode.Idle;
            else if (status.StartsWith("Correcting Orbit")) currentStatusMode = StatusMode.CorrectingOrbit;
            else if (status.StartsWith("Evading")) currentStatusMode = StatusMode.Evading;
            else if (status.StartsWith("Withdrawing")) currentStatusMode = StatusMode.Withdrawing;
            else if (status.StartsWith("Firing")) currentStatusMode = StatusMode.Firing;
            else if (status.StartsWith("Maneuvering")) currentStatusMode = StatusMode.Maneuvering;
            else if (status.StartsWith("Stranded")) currentStatusMode = StatusMode.Stranded;
            else if (status.StartsWith("Commanded")) currentStatusMode = StatusMode.Commanded;
            else currentStatusMode = StatusMode.Custom;
        }
        #endregion

        #region RMB info in editor

        // <color={XKCDColors.HexFormat.Lime}>Yes</color>
        public override string GetInfo()
        {
            // known bug - the game caches the RMB info, changing the variable after checking the info
            // does not update the info. :( No idea how to force an update.
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<b>Available settings</b>:");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Min Engagement Range</color> - AI will try to move away from oponents if closer than this range");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- RCS Active</color> - Use RCS during any maneuvers, or only in combat");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Maneuver Speed</color> - Max speed relative to target during intercept maneuvers");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Strafing Speed</color> - Max speed relative to target during gun firing");
            if (GameSettings.ADVANCED_TWEAKABLES)
            {
                sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Min Evasion Time</color> - Minimum seconds AI will evade for");
                sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Evasion Distance Threshold</color> - How close incoming gunfire needs to come to trigger evasion");
                sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Evasion Time Threshold</color> - How many seconds the AI needs to be under fire to begin evading");
                sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Evasion Min Range Threshold</color> - Attacker needs to be beyond this range to trigger evasion");
                sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Don't Evade My Target</color> - Whether gunfire from the current target is ignored for evasion");
            }
            return sb.ToString();
        }

        #endregion RMB info in editor

        #region events

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (HighLogic.LoadedSceneIsFlight)
                GameEvents.onVesselPartCountChanged.Add(CalculateAvailableTorque);
            CalculateAvailableTorque(vessel);
        }

        protected override void OnDestroy()
        {
            GameEvents.onVesselPartCountChanged.Remove(CalculateAvailableTorque);
            base.OnDestroy();
        }

        public override void ActivatePilot()
        {
            base.ActivatePilot();
            //originalMaxSpeed = ManeuverSpeed;
            if (!fc)
            {
                fc = gameObject.AddComponent<BDOrbitalControl>();
                fc.vessel = vessel;

                fc.alignmentToleranceforBurn = 7.5f;
                fc.throttleLerpRate = 3;
            }
            fc.Activate();
        }

        public override void DeactivatePilot()
        {
            base.DeactivatePilot();

            if (fc)
            {
                fc.Deactivate();
                fc = null;
            }

            evadingGunfire = false;
            SetStatus("");
        }

        protected override void OnGUI()
        {
            base.OnGUI();

            if (!pilotEnabled || !vessel.isActiveVessel) return;

            if (!BDArmorySettings.DEBUG_LINES) return;
            GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, debugPosition, 5, Color.red); // Target intercept position
            GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, fc.attitude * 100, 5, Color.green); // Attitude command
            GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, fc.RCSVector * 100, 5, Color.cyan); // RCS command
            GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, fc.RCSVectorLerped * 100, 5, Color.blue); // RCS lerped command

            GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, vesselTransform.position + vesselTransform.up * 1000, 3, Color.white);
        }

        #endregion events

        #region Actual AI Pilot
        protected override void AutoPilot(FlightCtrlState s)
        {
            // Update vars
            InitialFrameUpdates();

            UpdateStatus(); // Combat decisions, evasion, maneuverStateChanged = true and set new statusMode, etc.

            maneuverTime += Time.fixedDeltaTime;
            if (maneuverStateChanged || maneuverTime > minManeuverTime)
            {
                maneuverTime = 0;
                evasionNonLinearityDirection = UnityEngine.Random.onUnitSphere;
                fc.lerpAttitude = true;
                minManeuverTime = combatUpdateInterval;
                switch (currentStatusMode)
                {
                    case StatusMode.Evading:
                        minManeuverTime = emergencyUpdateInterval;
                        break;
                    case StatusMode.CorrectingOrbit:
                        break;
                    case StatusMode.Withdrawing:
                        {
                            // Determine the direction.
                            Vector3 averagePos = Vector3.zero;
                            using (List<TargetInfo>.Enumerator target = BDATargetManager.TargetList(weaponManager.Team).GetEnumerator())
                                while (target.MoveNext())
                                {
                                    if (target.Current == null) continue;
                                    if (target.Current && target.Current.Vessel && weaponManager.CanSeeTarget(target.Current))
                                    {
                                        averagePos += FromTo(vessel, target.Current.Vessel).normalized;
                                    }
                                }

                            Vector3 direction = -averagePos.normalized;
                            Vector3 orbitNormal = vessel.orbit.Normal(Planetarium.GetUniversalTime());
                            bool facingNorth = Vector3.Dot(direction, orbitNormal) > 0;
                            trackedDeltaV = 200;
                            attitudeCommand = (orbitNormal * (facingNorth ? 1 : -1)).normalized;
                        }
                        break;
                    case StatusMode.Commanded:
                        {
                            lastUpdateCommand = currentCommand;
                            if (maneuverStateChanged)
                            {
                                if (currentCommand == PilotCommands.Follow)
                                    attitudeCommand = commandLeader.transform.up;
                                else
                                    attitudeCommand = (assignedPositionWorld - vessel.transform.position).normalized;
                            }
                            minManeuverTime = 30f;
                            trackedDeltaV = 200;
                        }
                        break;
                    case StatusMode.Firing:
                        fc.lerpAttitude = false;
                        break;
                    case StatusMode.Maneuvering:
                        break;
                    case StatusMode.Stranded:
                        break;
                    default: // Idle
                        break;
                }
            }
            Maneuver(); // Set attitude, alignment tolerance, throttle, update RCS if needed

            AddDebugMessages();
        }

        void InitialFrameUpdates()
        {
            upDir = vessel.up;
            CalculateAngularAcceleration();
            maxAcceleration = GetMaxAcceleration(vessel);
            debugPosition = Vector3.zero;
            fc.alignmentToleranceforBurn = 5;
            fc.throttle = 0;
            vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, ManeuverRCS);
            maneuverStateChanged = false;
        }

        void Maneuver()
        {
            Vector3 rcsVector = Vector3.zero;
            switch (currentStatusMode)
            {
                case StatusMode.Evading:
                    {
                        SetStatus("Evading Missile");
                        vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, true);

                        Vector3 incomingVector = FromTo(vessel, weaponManager.incomingMissileVessel);
                        Vector3 dodgeVector = Vector3.ProjectOnPlane(vessel.ReferenceTransform.up, incomingVector.normalized);

                        fc.attitude = dodgeVector;
                        fc.alignmentToleranceforBurn = 45;
                        fc.throttle = 1;
                        rcsVector = dodgeVector;
                    }
                    break;
                case StatusMode.CorrectingOrbit:
                    {
                        Orbit o = vessel.orbit;
                        double UT = Planetarium.GetUniversalTime();
                        if (!belowSafeAlt && (o.ApA < 0 && o.timeToPe < -60))
                        {
                            // Vessel is on an escape orbit and has passed the periapsis by over 60s, burn retrograde
                            SetStatus("Correcting Orbit (On escape trajectory)");
                            fc.attitude = -o.Prograde(UT);
                            fc.throttle = 1;
                        }
                        else if (!belowSafeAlt && (o.ApA >= minSafeAltitude) && (o.altitude >= minSafeAltitude))
                        {
                            // We are outside the atmosphere but our periapsis is inside the atmosphere.
                            // Execute a burn to circularize our orbit at the current altitude.
                            SetStatus("Correcting Orbit (Circularizing)");

                            Vector3d fvel = Math.Sqrt(o.referenceBody.gravParameter / o.GetRadiusAtUT(UT)) * o.Horizontal(UT);
                            Vector3d deltaV = fvel - vessel.GetObtVelocity();

                            fc.attitude = deltaV.normalized;
                            fc.throttle = Mathf.Lerp(0, 1, (float)(deltaV.sqrMagnitude / 100));
                        }
                        else
                        {
                            belowSafeAlt = true;
                            var descending = o.timeToPe > 0 && o.timeToPe < o.timeToAp;
                            if (o.ApA < minSafeAltitude * 1.1)
                            {
                                // Entirety of orbit is inside atmosphere, perform gravity turn burn until apoapsis is outside atmosphere by a 10% margin.

                                SetStatus("Correcting Orbit (Apoapsis too low)");

                                double gravTurnAlt = 0.1;
                                float turn;

                                if (o.altitude < gravTurnAlt * minSafeAltitude || descending || wasDescendingUnsafe) // At low alts or when descending, burn straight up
                                {
                                    turn = 1f;
                                    fc.alignmentToleranceforBurn = 45f; // Use a wide tolerance as aero forces could make it difficult to align otherwise.
                                    wasDescendingUnsafe = descending || o.timeToAp < 10; // Hysteresis for upwards vs gravity turn burns.
                                }
                                else // At higher alts, gravity turn towards horizontal orbit vector
                                {
                                    turn = Mathf.Clamp((float)((1.1 * minSafeAltitude - o.ApA) / (minSafeAltitude * (1.1 - gravTurnAlt))), 0.1f, 1f);
                                    turn = Mathf.Clamp(Mathf.Log10(turn) + 1f, 0.33f, 1f);
                                    fc.alignmentToleranceforBurn = Mathf.Clamp(15f * turn, 5f, 15f);
                                    wasDescendingUnsafe = false;
                                }

                                fc.attitude = Vector3.Lerp(o.Horizontal(UT), upDir, turn);
                                fc.throttle = 1;
                            }
                            else if (o.altitude < minSafeAltitude * 1.1 && descending)
                            {
                                // Our apoapsis is outside the atmosphere but we are inside the atmosphere and descending.
                                // Burn up until we are ascending and our apoapsis is outside the atmosphere by a 10% margin.

                                SetStatus("Correcting Orbit (Falling inside atmo)");

                                fc.attitude = o.Radial(UT);
                                fc.alignmentToleranceforBurn = 45f; // Use a wide tolerance as aero forces could make it difficult to align otherwise.
                                fc.throttle = 1;
                            }
                            else
                            {
                                SetStatus("Correcting Orbit (Drifting)");
                                belowSafeAlt = false;
                            }
                        }
                    }
                    break;
                case StatusMode.Commanded:
                    {
                        // We have been given a command from the WingCommander to fly/follow/attack in a general direction
                        // Burn for 200 m/s then coast remainder of 30s period
                        switch (currentCommand)
                        {
                            case PilotCommands.Follow:
                                SetStatus("Commanded to Follow Leader");
                                break;
                            case PilotCommands.Attack:
                                SetStatus("Commanded to Attack");
                                break;
                            default: // Fly To
                                SetStatus("Commanded to Position");
                                break;
                        }
                        trackedDeltaV -= Vector3.Project(vessel.acceleration, attitudeCommand).magnitude * TimeWarp.fixedDeltaTime;
                        fc.attitude = attitudeCommand;
                        fc.throttle = (trackedDeltaV > 10) ? 1 : 0;
                    }
                    break;
                case StatusMode.Withdrawing:
                    {
                        SetStatus("Withdrawing");

                        // Withdraw sequence. Locks behaviour while burning 200 m/s of delta-v either north or south.
                        trackedDeltaV -= Vector3.Project(vessel.acceleration, attitudeCommand).magnitude * TimeWarp.fixedDeltaTime;
                        fc.attitude = attitudeCommand;
                        fc.throttle = (trackedDeltaV > 10) ? 1 : 0;
                    }
                    break;
                case StatusMode.Firing:
                    {
                        // Aim at appropriate point to launch missiles that aren't able to launch now
                        SetStatus("Firing Missiles");
                        vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, true);

                        fc.lerpAttitude = false;
                        Vector3 firingSolution = FromTo(vessel, targetVessel).normalized;

                        if (weaponManager.currentGun && GunReady(weaponManager.currentGun))
                        {
                            SetStatus("Firing Guns");
                            firingSolution = weaponManager.currentGun.FiringSolutionVector ?? Vector3.zero;
                        }
                        else if (weaponManager.CurrentMissile && !weaponManager.GetLaunchAuthorization(targetVessel, weaponManager, weaponManager.CurrentMissile))
                        {
                            SetStatus("Firing Missiles");
                            firingSolution = MissileGuidance.GetAirToAirFireSolution(weaponManager.CurrentMissile, targetVessel);
                        }
                        else
                            SetStatus("Firing");


                        fc.attitude = firingSolution;
                        fc.throttle = 0;
                        rcsVector = -Vector3.ProjectOnPlane(RelVel(vessel, targetVessel), FromTo(vessel, targetVessel));
                    }
                    break;
                case StatusMode.Maneuvering:
                    {
                        Vector3 interceptRanges = InterceptionRanges();

                        float minRange = interceptRanges.x;
                        float maxRange = interceptRanges.y;

                        vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, true);

                        float currentRange = VesselDistance(vessel, targetVessel);
                        Vector3 relVel = RelVel(vessel, targetVessel);

                        float speedTarget = Mathf.Max(Mathf.Min(firingSpeed, maxAcceleration * 0.15f), firingSpeed / 5f);
                        bool killVelOngoing = currentStatus.Contains("Kill Velocity") && (relVel.sqrMagnitude > speedTarget * speedTarget);

                        if (currentRange < minRange && AwayCheck(minRange))
                        {
                            SetStatus("Maneuvering (Away)");
                            fc.throttle = 1;
                            fc.alignmentToleranceforBurn = 135;
                            fc.attitude = FromTo(targetVessel, vessel).normalized;
                            fc.throttle = Vector3.Dot(RelVel(vessel, targetVessel), fc.attitude) < ManeuverSpeed ? 1 : 0;
                        }
                        else if (hasPropulsion && (ApproachingIntercept(currentStatus.Contains("Kill Velocity") ? 1.5f : 0f) || killVelOngoing))
                            KillVelocity(true);
                        else if (currentRange > maxRange && CanInterceptShip(targetVessel) && !OnIntercept(currentStatus.Contains("Intercept Target") ? 0.05f : 0.25f))
                            InterceptTarget();
                        else
                        {
                            bool killAngOngoing = currentStatus.Contains("Kill Angular Velocity") && (AngularVelocity(vessel, targetVessel, 5f) < firingAngularVelocityLimit / 2);
                            if (hasPropulsion && (relVel.sqrMagnitude > firingSpeed * firingSpeed))
                            {
                                KillVelocity();
                            }
                            else if (hasPropulsion && targetVessel != null && (AngularVelocity(vessel, targetVessel, 5f) > firingAngularVelocityLimit || killAngOngoing))
                            {
                                SetStatus("Maneuvering (Kill Angular Velocity)");
                                fc.attitude = -Vector3.ProjectOnPlane(RelVel(vessel, targetVessel), vessel.PredictPosition(vessel.TimeToCPA(targetVessel))).normalized;
                                fc.throttle = 1;
                            }
                            else // Drifting
                            {
                                fc.throttle = 0;
                                fc.attitude = FromTo(vessel, targetVessel).normalized;
                                if (weaponManager.previousGun != null) // If we had a gun recently selected, use it to continue pointing toward target
                                    fc.attitude = weaponManager.previousGun.FiringSolutionVector ?? fc.attitude;
                                if (currentRange < minRange)
                                    SetStatus("Maneuvering (Drift Away)");
                                else
                                    SetStatus("Maneuvering (Drift)");
                            }
                        }
                    }
                    break;
                case StatusMode.Stranded:
                    {
                        SetStatus("Stranded");

                        fc.attitude = FromTo(vessel, targetVessel).normalized;
                        fc.throttle = 0;
                    }
                    break;
                default: // Idle
                    {
                        if (hasWeapons)
                            SetStatus("Idle");
                        else
                            SetStatus("Idle (Unarmed)");

                        fc.attitude = Vector3.zero;
                        fc.throttle = 0;
                    }
                    break;
            }
            UpdateRCSVector(rcsVector);
        }

        void KillVelocity(bool onIntercept = false)
        {
            if (onIntercept)
                SetStatus($"Maneuvering (Kill Velocity), {timeToCPA:G2}s, , {distToCPA:N0}m");
            else
                SetStatus("Maneuvering (Kill Velocity)");
            Vector3 relVel = targetVessel.GetObtVelocity() - vessel.GetObtVelocity();
            fc.attitude = (relVel + targetVessel.perturbation).normalized;
            fc.throttle = 1;
        }

        void InterceptTarget()
        {
            SetStatus($"Maneuvering (Intercept Target), {timeToCPA:G2}s, {distToCPA:N0}m");
            Vector3 relPos = targetVessel.CoM - vessel.CoM;
            Vector3 relVel = targetVessel.GetObtVelocity() - vessel.GetObtVelocity();

            // Burn the difference between the target and current velocities.
            Vector3 toIntercept = Intercept(relPos, relVel);
            Vector3 burn = toIntercept.normalized * ManeuverSpeed + relVel;
            fc.attitude = burn.normalized;
            fc.throttle = 1f;
        }

        void AddDebugMessages()
        {
            if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_AI)
            {
                debugString.AppendLine($"Current Status: {currentStatus}");
                debugString.AppendLine($"Has Propulsion: {hasPropulsion}");
                debugString.AppendLine($"Has Weapons: {hasWeapons}");
                if (targetVessel)
                {
                    debugString.AppendLine($"Target Vessel: {targetVessel.GetDisplayName()}");
                    debugString.AppendLine($"Can Intercept: {CanInterceptShip(targetVessel)}");
                    debugString.AppendLine($"Target Range: {VesselDistance(vessel, targetVessel):G3}");
                    debugString.AppendLine($"Time to CPA: {timeToCPA:G3}");
                    debugString.AppendLine($"Distance to CPA: {distToCPA:G3}");
                    debugString.AppendLine($"Stopping Distance: {stoppingDist:G3}");
                }
                debugString.AppendLine($"Evasive {evasiveTimer}s");
                if (weaponManager) debugString.AppendLine($"Threat Sqr Distance: {weaponManager.incomingThreatDistanceSqr}");
            }
        }

        void UpdateStatus()
        {
            // Update propulsion and weapon status
            bool hasRCSFore = VesselModuleRegistry.GetModules<ModuleRCS>(vessel).Any(e => e.rcsEnabled && !e.flameout && e.useThrottle);
            hasPropulsion = hasRCSFore || VesselModuleRegistry.GetModuleEngines(vessel).Any(e => (e.EngineIgnited && e.isOperational));
            hasWeapons = (weaponManager != null) && weaponManager.HasWeaponsAndAmmo();

            // Check on command status
            UpdateCommand();

            // FIXME Josue There seems to be a fair bit of oscillation between circularising, intercept velocity and kill velocity in my tests, with the craft repeatedly rotating 180° to perform burns in opposite directions.
            // In particular, this happens a lot when the craft's periapsis is at the min safe altitude, which occurs frequently if the spawn distance is large enough to give significant inclinations.
            // I think there needs to be some manoeuvre logic to better handle this condition, such as modifying burns that would bring the periapsis below the min safe altitude, which might help with inclination shifts.
            // Also, maybe some logic to ignore targets that will fall below the min safe altitude before they can be reached could be useful.

            // Update status mode
            if (weaponManager && weaponManager.missileIsIncoming && weaponManager.incomingMissileVessel && weaponManager.incomingMissileTime <= weaponManager.evadeThreshold) // Needs to start evading an incoming missile.
                currentStatusMode = StatusMode.Evading;
            else if (CheckOrbitDangerous() || belowSafeAlt)
                currentStatusMode = StatusMode.CorrectingOrbit;
            else if (currentCommand == PilotCommands.FlyTo || currentCommand == PilotCommands.Follow || currentCommand == PilotCommands.Attack)
            {
                currentStatusMode = StatusMode.Commanded;
                if (currentCommand != lastUpdateCommand)
                    maneuverStateChanged = true;
            }
            else if (weaponManager)
            {
                if (allowWithdrawal && hasPropulsion && !hasWeapons && CheckWithdraw())
                    currentStatusMode = StatusMode.Withdrawing;
                else if (targetVessel != null && weaponManager.currentGun && GunReady(weaponManager.currentGun))
                    currentStatusMode = StatusMode.Firing; // Guns
                else if (targetVessel != null && weaponManager.CurrentMissile && !weaponManager.GetLaunchAuthorization(targetVessel, weaponManager, weaponManager.CurrentMissile))
                    currentStatusMode = StatusMode.Firing; // Missiles
                else if (targetVessel != null && weaponManager.CurrentMissile && weaponManager.guardFiringMissile && currentStatusMode == StatusMode.Firing)
                    currentStatusMode = StatusMode.Firing; // Post-launch authorization missile firing underway, don't change status from Firing
                else if (targetVessel != null && hasWeapons)
                {
                    if (hasPropulsion)
                        currentStatusMode = StatusMode.Maneuvering;
                    else
                        currentStatusMode = StatusMode.Stranded;
                }
                else if (CheckOrbitUnsafe() || belowSafeAlt)
                    currentStatusMode = StatusMode.CorrectingOrbit;
                else
                    currentStatusMode = StatusMode.Idle;
            }
            else if (CheckOrbitUnsafe() || belowSafeAlt)
                currentStatusMode = StatusMode.CorrectingOrbit;
            else
                currentStatusMode = StatusMode.Idle;

            // Flag changed status if necessary
            if (lastStatusMode != currentStatusMode || maneuverStateChanged)
            {
                maneuverStateChanged = true;
                lastStatusMode = currentStatusMode;
                if (BDArmorySettings.DEBUG_AI)
                    Debug.Log("[BDArmory.BDModuleOrbitalAI]: Status of " + vessel.vesselName + " changed from " + lastStatusMode + " to " + currentStatus);
            }

            // Temporarily inhibit maneuvers if not evading a missile and waiting for a launched missile to fly to a safe distance
            if (currentStatusMode != StatusMode.Evading && weaponManager && weaponManager.PreviousMissile)
            {
                if ((vessel.CoM - weaponManager.PreviousMissile.vessel.transform.position).sqrMagnitude < vessel.vesselSize.sqrMagnitude)
                    fc.Stability(true);
                else
                    fc.Stability(false);
            }
            else
                fc.Stability(false);

            // Check for incoming gunfire
            EvasionStatus();

            // Set target as UI target
            if (vessel.isActiveVessel && targetVessel && !targetVessel.IsMissile() && (vessel.targetObject == null || vessel.targetObject.GetVessel() != targetVessel))
            {
                FlightGlobals.fetch.SetVesselTarget(targetVessel, true);
            }
        }

        void UpdateCommand()
        {
            if (command == PilotCommands.Follow && commandLeader is null)
            {
                ReleaseCommand();
                return;
            }
            else if (command == PilotCommands.Attack)
            {
                if (targetVessel != null)
                {
                    ReleaseCommand(false);
                    return;
                }
                else if (weaponManager.underAttack || weaponManager.underFire)
                {
                    ReleaseCommand(false);
                    return;
                }
            }
        }

        void EvasionStatus()
        {
            evadingGunfire = false;

            // Return if evading missile
            if (currentStatusMode == StatusMode.Evading)
            {
                evasiveTimer = 0;
                return;
            }

            // Check if we should be evading gunfire, missile evasion is handled separately
            float threatRating = evasionThreshold + 1f; // Don't evade by default
            if (weaponManager != null && weaponManager.underFire)
            {
                if (weaponManager.incomingMissTime >= evasionTimeThreshold && weaponManager.incomingThreatDistanceSqr >= evasionMinRangeThreshold * evasionMinRangeThreshold) // If we haven't been under fire long enough or they're too close, ignore gunfire
                    threatRating = weaponManager.incomingMissDistance;
            }
            // If we're currently evading or a threat is significant
            if ((evasiveTimer < minEvasionTime && evasiveTimer != 0) || threatRating < evasionThreshold)
            {
                if (evasiveTimer < minEvasionTime)
                {
                    threatRelativePosition = vessel.Velocity().normalized + vesselTransform.right;
                    if (weaponManager)
                    {
                        if (weaponManager.underFire)
                            threatRelativePosition = weaponManager.incomingThreatPosition - vesselTransform.position;
                    }
                }
                evadingGunfire = true;
                evasionNonLinearityDirection = (evasionNonLinearityDirection + evasionErraticness * UnityEngine.Random.onUnitSphere).normalized;
                evasiveTimer += Time.fixedDeltaTime;

                if (evasiveTimer >= minEvasionTime)
                    evasiveTimer = 0;
            }
        }
        #endregion Actual AI Pilot

        #region Utility Functions

        private bool CheckWithdraw()
        {
            var nearest = BDATargetManager.GetClosestTarget(weaponManager);
            if (nearest == null) return false;

            return RelVel(vessel, nearest.Vessel).sqrMagnitude < 200 * 200;
        }

        private bool CheckOrbitDangerous()
        {
            Orbit o = vessel.orbit;
            if (o.referenceBody != safeAltBody) // Body has been updated, update min safe alt
            {
                minSafeAltitude = o.referenceBody.MinSafeAltitude();
                safeAltBody = o.referenceBody;
            }
            if (o.altitude < minSafeAltitude)
                return true;
            else
                return false;
        }

        private bool CheckOrbitUnsafe()
        {
            Orbit o = vessel.orbit;
            if (o.referenceBody != safeAltBody) // Body has been updated, update min safe alt
            {
                minSafeAltitude = o.referenceBody.MinSafeAltitude();
                safeAltBody = o.referenceBody;
            }

            return (o.PeA < minSafeAltitude && o.timeToPe < o.timeToAp) || (o.ApA < minSafeAltitude && (o.ApA >= 0 || o.timeToPe < -60)); // Match conditions in PilotLogic
        }

        private bool CanInterceptShip(Vessel target)
        {
            bool canIntercept = false;

            // Is it worth us chasing a withdrawing ship?
            BDModuleOrbitalAI targetAI = VesselModuleRegistry.GetModule<BDModuleOrbitalAI>(target);

            if (targetAI)
            {
                Vector3 toTarget = target.CoM - vessel.CoM;
                bool escaping = targetAI.currentStatusMode == StatusMode.Withdrawing;

                canIntercept = !escaping || // It is not trying to escape.
                    toTarget.sqrMagnitude < weaponManager.gunRange * weaponManager.gunRange || // It is already in range.
                    maxAcceleration * maxAcceleration > targetAI.vessel.acceleration_immediate.sqrMagnitude || //  We are faster (currently).
                    Vector3.Dot(target.GetObtVelocity() - vessel.GetObtVelocity(), toTarget) < 0; // It is getting closer.
            }
            return canIntercept;
        }

        public float BurnTime(float deltaV, float totalConsumption)
        {
            float isp = GetMaxThrust(vessel) / totalConsumption;
            return ((float)vessel.totalMass * (1.0f - 1.0f / Mathf.Exp(deltaV / isp)) / totalConsumption);
        }

        public float StoppingDistance(float speed)
        {
            float consumptionRate = GetConsumptionRate(vessel);
            float time = BurnTime(speed, consumptionRate);
            float jerk = (float)(maxThrust / (vessel.totalMass - consumptionRate)) - maxAcceleration;
            return speed * time + 0.5f * -maxAcceleration * time * time + 1 / 6 * -jerk * time * time * time;
        }

        private bool ApproachingIntercept(float margin = 0.0f)
        {
            Vector3 relPos = targetVessel.CoM - vessel.CoM;
            Vector3 relVel = targetVessel.GetObtVelocity() - vessel.GetObtVelocity();
            if (Vector3.Dot(relVel, (-relPos).normalized) < 10.0)
                return false;
            float angleToRotate = Vector3.Angle(vessel.ReferenceTransform.up, relVel.normalized) * ((float)Math.PI / 180.0f) * 0.75f;
            Vector3 angularAcceleration = maxAngularAcceleration;
            float angAccelMag = angularAcceleration.magnitude;
            float timeToRotate = BDAMath.SolveTime(angleToRotate, angAccelMag) / 0.75f;
            float interceptStoppingDistance = StoppingDistance(relVel.magnitude) + relVel.magnitude * (margin + timeToRotate * 3f);
            Vector3 toIntercept = Intercept(relPos, relVel);
            float distanceToIntercept = toIntercept.magnitude;
            distToCPA = distanceToIntercept;
            timeToCPA = vessel.TimeToCPA(targetVessel);
            stoppingDist = interceptStoppingDistance;

            return distanceToIntercept < interceptStoppingDistance;
        }

        private bool OnIntercept(float tolerance)
        {
            if (targetVessel is null)
                return false;
            Vector3 relPos = targetVessel.CoM - vessel.CoM;
            Vector3 relVel = targetVessel.GetObtVelocity() - vessel.GetObtVelocity();
            if (Vector3.Dot(relPos, relVel) >= 0.0)
                return false;
            timeToCPA = vessel.TimeToCPA(targetVessel);
            Vector3 cpa = vessel.PredictPosition(timeToCPA);
            float interceptRange = InterceptionRange();
            float interceptRangeTolSqr = (interceptRange * (tolerance + 1f)) * (interceptRange * (tolerance + 1f));
            return cpa.sqrMagnitude < interceptRangeTolSqr && Mathf.Abs(relVel.magnitude - ManeuverSpeed) < ManeuverSpeed * tolerance;
        }

        private Vector3 Intercept(Vector3 relPos, Vector3 relVel)
        {
            Vector3 lateralVel = Vector3.ProjectOnPlane(-relVel, relPos);
            Vector3 lateralOffset = lateralVel.normalized * InterceptionRange();
            return relPos + lateralOffset;
        }

        private Vector3 InterceptionRanges()
        {
            Vector3 interceptRanges = Vector3.zero;
            float minRange = Mathf.Max(MinEngagementRange, targetVessel.GetRadius());
            float maxRange = Mathf.Max(weaponManager.gunRange, minRange * 1.2f);
            bool usingProjectile = true;
            if (weaponManager != null && weaponManager.selectedWeapon != null)
            {
                currentWeapon = weaponManager.selectedWeapon;
                EngageableWeapon engageableWeapon = currentWeapon as EngageableWeapon;
                minRange = Mathf.Max(engageableWeapon.GetEngagementRangeMin(), minRange);
                maxRange = Mathf.Min(engageableWeapon.GetEngagementRangeMax(), maxRange);
                usingProjectile = weaponManager.selectedWeapon.GetWeaponClass() != WeaponClasses.Missile;
            }
            float interceptRange = minRange + (maxRange - minRange) * (usingProjectile ? 0.25f : 0.75f);
            interceptRanges.x = minRange;
            interceptRanges.y = maxRange;
            interceptRanges.z = interceptRange;
            return interceptRanges;
        }

        private float InterceptionRange()
        {
            Vector3 interceptRanges = InterceptionRanges();
            return interceptRanges.z;
        }

        private bool GunReady(ModuleWeapon gun)
        {
            if (gun == null) return false;

            // Check gun/laser can fire soon, we are within guard and weapon engagement ranges, and we are under the firing speed
            float targetSqrDist = FromTo(vessel, targetVessel).sqrMagnitude;
            return RelVel(vessel, targetVessel).sqrMagnitude < firingSpeed * firingSpeed &&
                gun.CanFireSoon() &&
                (targetSqrDist <= gun.GetEngagementRangeMax() * gun.GetEngagementRangeMax()) &&
                (targetSqrDist <= weaponManager.gunRange * weaponManager.gunRange);
        }

        private bool AwayCheck(float minRange)
        {
            // Check if we need to manually burn away from an enemy that's too close or
            // if it would be better to drift away.

            Vector3 toTarget = FromTo(vessel, targetVessel);
            Vector3 toEscape = -toTarget.normalized;
            Vector3 relVel = targetVessel.GetObtVelocity() - vessel.GetObtVelocity();

            float rotDistance = Vector3.Angle(vessel.ReferenceTransform.up, toEscape) * Mathf.Deg2Rad;
            float timeToRotate = BDAMath.SolveTime(rotDistance / 2, maxAngularAcceleration.magnitude) * 2;
            float timeToDisplace = BDAMath.SolveTime(minRange - toTarget.magnitude, maxAcceleration, Vector3.Dot(-relVel, toEscape));
            float timeToEscape = timeToRotate * 2 + timeToDisplace;

            Vector3 drift = AIUtils.PredictPosition(toTarget, relVel, Vector3.zero, timeToEscape);
            bool manualEscape = drift.sqrMagnitude < minRange * minRange;

            return manualEscape;
        }

        private void UpdateRCSVector(Vector3 inputVec = default(Vector3))
        {
            if (evadingGunfire) // Quickly move RCS vector
            {
                inputVec = Vector3.ProjectOnPlane(evasionNonLinearityDirection, threatRelativePosition);
                fc.rcsLerpRate = 15f;
                fc.rcsRotate = true;
            }
            else // Slowly lerp RCS vector
            {
                fc.rcsLerpRate = 5f;
                fc.rcsRotate = false;
            }

            fc.RCSVector = inputVec;
        }
        #endregion

        #region Utils
        public static Vector3 FromTo(Vessel v1, Vessel v2)
        {
            return v2.transform.position - v1.transform.position;
        }

        public static Vector3 RelVel(Vessel v1, Vessel v2)
        {
            return v1.GetObtVelocity() - v2.GetObtVelocity();
        }

        public static Vector3 AngularAcceleration(Vector3 torque, Vector3 MoI)
        {
            return new Vector3(MoI.x.Equals(0) ? float.MaxValue : torque.x / MoI.x,
                MoI.y.Equals(0) ? float.MaxValue : torque.y / MoI.y,
                MoI.z.Equals(0) ? float.MaxValue : torque.z / MoI.z);
        }

        public static float AngularVelocity(Vessel v, Vessel t, float window)
        {
            Vector3 tv1 = FromTo(v, t);
            Vector3 tv2 = tv1 + window * RelVel(v, t);
            return Vector3.Angle(tv1.normalized, tv2.normalized) / window;
        }

        public static float VesselDistance(Vessel v1, Vessel v2)
        {
            return (v1.transform.position - v2.transform.position).magnitude;
        }

        public static Vector3 Displacement(Vector3 velocity, Vector3 acceleration, float time)
        {
            return velocity * time + 0.5f * acceleration * time * time;
        }

        private void CalculateAngularAcceleration()
        {
            maxAngularAcceleration = AngularAcceleration(availableTorque, vessel.MOI);
        }

        private void CalculateAvailableTorque(Vessel v)
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            if (v != vessel) return;

            availableTorque = Vector3.zero;
            var reactionWheels = VesselModuleRegistry.GetModules<ModuleReactionWheel>(v);
            foreach (var wheel in reactionWheels)
            {
                wheel.GetPotentialTorque(out Vector3 pos, out pos);
                availableTorque += pos;
            }
        }

        public float GetMaxAcceleration(Vessel v)
        {
            maxThrust = GetMaxThrust(v);
            return maxThrust / v.GetTotalMass();
        }

        public static float GetMaxThrust(Vessel v)
        {
            float thrust = VesselModuleRegistry.GetModuleEngines(v).Where(e => e != null && e.EngineIgnited && e.isOperational).Sum(e => e.MaxThrustOutputVac(true));
            thrust += VesselModuleRegistry.GetModules<ModuleRCS>(v).Where(rcs => rcs != null && rcs.useThrottle).Sum(rcs => rcs.thrusterPower);
            return thrust;
        }

        private static float GetConsumptionRate(Vessel v)
        {
            float consumptionRate = 0.0f;
            foreach (var engine in VesselModuleRegistry.GetModuleEngines(v))
                consumptionRate += Mathf.Lerp(engine.minFuelFlow, engine.maxFuelFlow, 0.01f * engine.thrustPercentage) * engine.flowMultiplier;
            return consumptionRate;
        }
        #endregion

        #region Autopilot helper functions

        public override bool CanEngage()
        {
            return !vessel.LandedOrSplashed && vessel.InOrbit();
        }

        public override bool IsValidFixedWeaponTarget(Vessel target)
        {
            if (!vessel) return false;

            return true;
        }

        #endregion Autopilot helper functions

        #region WingCommander

        Vector3 GetFormationPosition()
        {
            return commandLeader.vessel.CoM + Quaternion.LookRotation(commandLeader.vessel.up, upDir) * this.GetLocalFormationPosition(commandFollowIndex);
        }

        #endregion WingCommander
    }
}