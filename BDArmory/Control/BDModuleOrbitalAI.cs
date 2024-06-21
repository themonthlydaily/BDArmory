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
using BDArmory.Weapons.Missiles;

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

        private BDOrbitalControl fc;
        private bool PIDActive;
        private int ECID;

        public IBDWeapon currentWeapon;

        private float trackedDeltaV;
        private Vector3 attitudeCommand;
        private PilotCommands lastUpdateCommand = PilotCommands.Free;
        private float maneuverTime;
        private float minManeuverTime;
        private bool maneuverStateChanged = false;
        private bool ongoingOrbitCorrection = false;
        private bool wasDescendingUnsafe = false;
        private bool hasPropulsion;
        private bool hasRCS;
        private bool hasWeapons;
        private bool hasEC;
        private float maxAcceleration;
        private float maxThrust;
        private Vector3 maxAngularAcceleration;
        private float maxAngularAccelerationMag;
        private Vector3 availableTorque;
        private double minSafeAltitude;
        private CelestialBody safeAltBody = null;
        private Vector3 interceptRanges;
        private Vector3 lastFiringSolution;

        // Evading
        bool evadingGunfire = false;
        float evasiveTimer;
        Vector3 threatRelativePosition;
        Vector3 evasionNonLinearityDirection;
        string evasionString = " & Evading Gunfire";

        public enum PIDModeTypes
        {
            Inactive, // Stock autopilot always used
            Firing, // PID used for firing weapons
            Everything // PID used for firing weapons and maneuvers
        }

        public enum RollModeTypes
        {
            Port_Starboard, // Roll to port or starboard, whichever is closer
            Dorsal_Ventral, // Roll to dorsal or ventral, whichever is closer
            Port, // Always roll port to target
            Starboard, // Always roll starboard to target
            Dorsal, // Always roll dorsal to target
            Ventral, // Always roll ventral to target
        }

        // User parameters changed via UI.

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_AI_OrbitalPIDActive"),//PID active mode
            UI_ChooseOption(options = new string[3] { "Inactive", "Firing", "Everything" })]
        public string pidMode = "Firing";
        public readonly string[] pidModes = new string[3] { "Inactive", "Firing", "Everything" };

        public PIDModeTypes PIDMode
            => (PIDModeTypes)Enum.Parse(typeof(PIDModeTypes), pidMode);

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_AI_SteerPower"),//Steer Factor
            UI_FloatRange(minValue = 0.2f, maxValue = 20f, stepIncrement = .1f, scene = UI_Scene.All)]
        public float steerMult = 14;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_AI_SteerKi"), //Steer Ki
            UI_FloatRange(minValue = 0.01f, maxValue = 1f, stepIncrement = 0.01f, scene = UI_Scene.All)]
        public float steerKiAdjust = 0.1f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_AI_SteerDamping"),//Steer Damping
            UI_FloatRange(minValue = 0.1f, maxValue = 10f, stepIncrement = .1f, scene = UI_Scene.All)]
        public float steerDamping = 5;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_AI_MinEngagementRange"),//Min engagement range
            UI_FloatSemiLogRange(minValue = 10f, maxValue = 10000f, scene = UI_Scene.All)]
        public float MinEngagementRange = 500;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_AI_RollMode"),// Preferred roll direction of ship towards target
            UI_ChooseOption(options = new string[6] { "Port_Starboard", "Dorsal_Ventral", "Port", "Starboard", "Dorsal", "Ventral" })]
        public string rollTowards = "Port_Starboard";
        public readonly string[] rollTowardsModes = new string[6] { "Port_Starboard", "Dorsal_Ventral", "Port", "Starboard", "Dorsal", "Ventral" };

        public RollModeTypes rollMode
            => (RollModeTypes)Enum.Parse(typeof(RollModeTypes), rollTowards);

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
        public float firingSpeed = 50f;

        [KSPField(isPersistant = true,
            guiActive = true,
            guiActiveEditor = true,
            guiName = "#LOC_BDArmory_AI_AngularSpeed",
            guiUnits = " m/s"),
            UI_FloatSemiLogRange(
                minValue = 1f,
                maxValue = 1000f,
                scene = UI_Scene.All
            )]
        public float firingAngularVelocityLimit = 10f;

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
        internal Vector3 debugTargetPosition;
        internal Vector3 debugTargetDirection;
        internal Vector3 debugRollTarget;

        // Dynamic measurements
        float dynAngAccel = 1f; // Start at reasonable value.
        float lastAngVel = 1f; // Start at reasonable value.
        float dynDecayRate = 1f; // Decay rate for dynamic measurements. Set to a half-life of 60s in ActivatePilot.

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
            if (!(HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor)) return;
            SetChooseOptions();
            if (HighLogic.LoadedSceneIsFlight)
                GameEvents.onVesselPartCountChanged.Add(CalculateAvailableTorque);
            CalculateAvailableTorque(vessel);
            ECID = PartResourceLibrary.Instance.GetDefinition("ElectricCharge").id; // This should always be found.
        }

        protected override void OnDestroy()
        {
            GameEvents.onVesselPartCountChanged.Remove(CalculateAvailableTorque);
            base.OnDestroy();
        }

        public override void ActivatePilot()
        {
            base.ActivatePilot();
            dynDecayRate = Mathf.Exp(Mathf.Log(0.5f) * Time.fixedDeltaTime / 60f); // Decay rate for a half-life of 60s.
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

        public void SetChooseOptions()
        {
            UI_ChooseOption pidmode = (UI_ChooseOption)(HighLogic.LoadedSceneIsFlight ? Fields["pidMode"].uiControlFlight : Fields["pidMode"].uiControlEditor);
            pidmode.onFieldChanged = ChooseOptionsUpdated;
        }

        public void ChooseOptionsUpdated(BaseField field, object obj)
        {
            this.part.RefreshAssociatedWindows();
            if (BDArmoryAIGUI.Instance != null)
            {
                BDArmoryAIGUI.Instance.SetChooseOptionSliders();
            }
        }

        protected override void OnGUI()
        {
            base.OnGUI();

            if (!pilotEnabled || !vessel.isActiveVessel) return;

            if (!BDArmorySettings.DEBUG_LINES) return;
            GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, PIDActive ? debugTargetPosition : fc.attitude * 1000, 5, Color.red); // The point we're asked to turn to
            if (PIDActive) GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, debugTargetDirection, 5, Color.green); // The direction PID control will actually turn to
            GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, fc.RCSVector * 100, 5, Color.cyan); // RCS command
            GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, fc.RCSVectorLerped * 100, 5, Color.magenta); // RCS lerped command
            GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, vesselTransform.position + debugRollTarget, 2, Color.blue); // Roll target
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
            if (PIDActive)
                AttitudeControl(s);
            AddDebugMessages();
        }

        void InitialFrameUpdates()
        {
            upDir = vessel.up;
            CalculateAngularAcceleration();
            maxAcceleration = GetMaxAcceleration(vessel);
            fc.alignmentToleranceforBurn = 5;
            if (fc.throttle > 0)
                lastFiringSolution = Vector3.zero; // Forget prior firing solution if we recently used engines
            fc.throttle = 0;
            fc.lerpThrottle = true;
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
                        fc.alignmentToleranceforBurn = 70;
                        fc.throttle = 1;
                        fc.lerpThrottle = false;
                        rcsVector = dodgeVector;
                    }
                    break;
                case StatusMode.CorrectingOrbit:
                    {
                        Orbit o = vessel.orbit;
                        double UT = Planetarium.GetUniversalTime();
                        if (!ongoingOrbitCorrection && (o.ApA < 0 && o.timeToPe < -60))
                        {
                            // Vessel is on an escape orbit and has passed the periapsis by over 60s, burn retrograde
                            SetStatus("Correcting Orbit (On escape trajectory)");
                            ongoingOrbitCorrection = o.ApA / o.PeA > 10f;
                            fc.attitude = -o.Prograde(UT);
                            fc.throttle = 1;
                        }
                        else if (!ongoingOrbitCorrection && (o.ApA >= minSafeAltitude) && (o.altitude >= minSafeAltitude))
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
                            ongoingOrbitCorrection = true;
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
                                ongoingOrbitCorrection = false;
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
                        fc.alignmentToleranceforBurn = 70;
                    }
                    break;
                case StatusMode.Firing:
                    {
                        // Aim at appropriate point to launch missiles that aren't able to launch now
                        SetStatus("Firing Missiles");
                        vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, true);

                        fc.lerpAttitude = false;
                        Vector3 firingSolution = FromTo(vessel, targetVessel).normalized;
                        rcsVector = -Vector3.ProjectOnPlane(RelVel(vessel, targetVessel), FromTo(vessel, targetVessel));

                        if (weaponManager.currentGun && GunReady(weaponManager.currentGun))
                        {
                            SetStatus("Firing Guns");
                            firingSolution = weaponManager.currentGun.FiringSolutionVector ?? Vector3.zero;
                        }
                        else if (weaponManager.CurrentMissile && !weaponManager.GetLaunchAuthorization(targetVessel, weaponManager, weaponManager.CurrentMissile))
                        {
                            SetStatus("Firing Missiles");
                            firingSolution = MissileGuidance.GetAirToAirFireSolution(weaponManager.CurrentMissile, targetVessel);
                            firingSolution = (firingSolution - vessel.transform.position).normalized;
                        }
                        else
                            SetStatus("Firing");

                        lastFiringSolution = firingSolution;
                        fc.attitude = firingSolution;
                        fc.throttle = 0;
                    }
                    break;
                case StatusMode.Maneuvering:
                    {
                        Vector3 toTarget = FromTo(vessel, targetVessel).normalized;
                        float minRange = interceptRanges.x;
                        float maxRange = interceptRanges.y;

                        vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, true);

                        float currentRange = VesselDistance(vessel, targetVessel);
                        Vector3 relVel = RelVel(vessel, targetVessel);

                        float speedTarget = KillVelocityTargetSpeed();
                        bool killVelOngoing = currentStatus.Contains("Kill Velocity") && (relVel.sqrMagnitude > speedTarget * speedTarget);
                        bool interceptOngoing = currentStatus.Contains("Intercept Target") && !OnIntercept(0.05f) && !ApproachingIntercept();

                        if (currentRange < minRange && AwayCheck(minRange)) // Too close, maneuever away
                        {
                            SetStatus("Maneuvering (Away)");
                            fc.throttle = 1;
                            fc.alignmentToleranceforBurn = 135;
                            fc.attitude = -toTarget;
                            fc.throttle = Vector3.Dot(RelVel(vessel, targetVessel), fc.attitude) < ManeuverSpeed ? 1 : 0;
                        }
                        else if (hasPropulsion && (ApproachingIntercept(currentStatus.Contains("Kill Velocity") ? 1.5f : 0f) || killVelOngoing)) // Approaching intercept point, kill velocity
                            KillVelocity();
                        else if (hasPropulsion && interceptOngoing || (currentRange > maxRange && CanInterceptShip(targetVessel) && !OnIntercept(currentStatus.Contains("Intercept Target") ? 0.05f : 0.25f))) // Too far away, intercept target
                            InterceptTarget();
                        else if (currentRange > maxRange)
                        {
                            fc.throttle = 0;
                            
                            if (ApproachingIntercept(3f))
                                fc.attitude = -toTarget;
                            else
                                fc.attitude = toTarget;
                            SetStatus("Maneuvering (Drift)");
                        }
                        else // Within weapons range, adjust velocity and attitude for targeting
                        {
                            bool killAngOngoing = currentStatus.Contains("Kill Angular Velocity") && (AngularVelocity(vessel, targetVessel, 5f) < firingAngularVelocityLimit / 2);
                            if (hasPropulsion && (relVel.sqrMagnitude > firingSpeed * firingSpeed || killVelOngoing))
                            {
                                KillVelocity();
                            }
                            else if (hasPropulsion && targetVessel != null && (AngularVelocity(vessel, targetVessel, 5f) > firingAngularVelocityLimit || killAngOngoing))
                            {
                                SetStatus("Maneuvering (Kill Angular Velocity)");
                                fc.attitude = -Vector3.ProjectOnPlane(RelVel(vessel, targetVessel), vessel.PredictPosition(vessel.TimeToCPA(targetVessel))).normalized;
                                fc.throttle = 1;
                                fc.alignmentToleranceforBurn = 45f;
                            }
                            else // Drifting
                            {
                                fc.throttle = 0;
                                fc.attitude = toTarget;
                                if (weaponManager.previousGun != null) // If we had a gun recently selected, use it to continue pointing toward target
                                    fc.attitude = weaponManager.previousGun.FiringSolutionVector ?? fc.attitude;
                                else if (lastFiringSolution != Vector3.zero)
                                    fc.attitude = lastFiringSolution;
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
            UpdateBurnAlignmentTolerance();
        }

        void KillVelocity()
        {
            float timeToCPA = vessel.TimeToCPA(targetVessel);
            Vector3 cpa = vessel.PredictPosition(timeToCPA);
            float cpaDist = (cpa - vessel.CoM).magnitude;
            Vector3 relPos = targetVessel.CoM - vessel.CoM;
            Vector3 relVel = targetVessel.GetObtVelocity() - vessel.GetObtVelocity();
            Vector3 relAccel = targetVessel.perturbation - vessel.perturbation;
            float targetSpeed = KillVelocityTargetSpeed();
            bool maintainThrottle = ((relVel + timeToCPA * relAccel).sqrMagnitude > targetSpeed * targetSpeed) || (cpa.sqrMagnitude == relPos.sqrMagnitude);

            SetStatus($"Maneuvering (Kill Velocity), {timeToCPA:G2}s, {cpaDist:N0}m");

            fc.attitude = (relVel + targetVessel.perturbation).normalized;
            fc.throttle = maintainThrottle ? 1f : 0f;
            fc.alignmentToleranceforBurn = 45f;
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
                    debugString.AppendLine($"Min/Max/Intercept Range: {interceptRanges.x}/{interceptRanges.y}/{interceptRanges.z}");
                    debugString.AppendLine($"Time to CPA: {timeToCPA:G3}");
                    debugString.AppendLine($"Distance to CPA: {distToCPA:G3}");
                    debugString.AppendLine($"Stopping Distance: {stoppingDist:G3}");
                    debugString.AppendLine($"Dynamic Angular Acceleration Estimate: {maxAngularAccelerationMag}/{dynAngAccel}");
                }
                debugString.AppendLine($"Evasive {evasiveTimer}s");
                if (weaponManager) debugString.AppendLine($"Threat Sqr Distance: {weaponManager.incomingThreatDistanceSqr}");
            }
        }

        void UpdateStatus()
        {
            // Update propulsion and weapon status
            hasRCS = VesselModuleRegistry.GetModules<ModuleRCS>(vessel).Any(e => e.rcsEnabled && !e.flameout && e.useThrottle);
            hasPropulsion = hasRCS || VesselModuleRegistry.GetModuleEngines(vessel).Any(e => (e.EngineIgnited && e.isOperational));
            vessel.GetConnectedResourceTotals(ECID, out double EcCurrent, out double ecMax);
            hasEC = EcCurrent > 0 || CheatOptions.InfiniteElectricity;
            hasWeapons = (weaponManager != null) && weaponManager.HasWeaponsAndAmmo();

            // Check on command status
            UpdateCommand();

            // Update intercept ranges
            interceptRanges = InterceptionRanges();

            // Prioritize safe orbits over combat outside of weapon range
            bool fixOrbitNow = hasPropulsion && (CheckOrbitDangerous() || ongoingOrbitCorrection);
            bool fixOrbitLater = false;
            if (hasPropulsion && !fixOrbitNow && CheckOrbitUnsafe())
            {
                fixOrbitLater = true;
                if (weaponManager && targetVessel != null)
                    fixOrbitNow = ((vessel.CoM - targetVessel.CoM).sqrMagnitude > interceptRanges.y * interceptRanges.y) && (vessel.TimeToCPA(targetVessel) > 10f);
            }

            // FIXME Josue There seems to be a fair bit of oscillation between circularising, intercept velocity and kill velocity in my tests, with the craft repeatedly rotating 180° to perform burns in opposite directions.
            // In particular, this happens a lot when the craft's periapsis is at the min safe altitude, which occurs frequently if the spawn distance is large enough to give significant inclinations.
            // I think there needs to be some manoeuvre logic to better handle this condition, such as modifying burns that would bring the periapsis below the min safe altitude, which might help with inclination shifts.
            // Also, maybe some logic to ignore targets that will fall below the min safe altitude before they can be reached could be useful.

            // Update status mode
            if (weaponManager && weaponManager.missileIsIncoming && weaponManager.incomingMissileVessel && weaponManager.incomingMissileTime <= weaponManager.evadeThreshold) // Needs to start evading an incoming missile.
                currentStatusMode = StatusMode.Evading;
            else if (fixOrbitNow)
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
                else if (fixOrbitLater)
                    currentStatusMode = StatusMode.CorrectingOrbit;
                else
                    currentStatusMode = StatusMode.Idle;
            }
            else if (fixOrbitLater)
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

            // Switch on PID Mode
            switch (PIDMode)
            {
                case PIDModeTypes.Inactive:
                    PIDActive = false;
                    break;
                case PIDModeTypes.Firing:
                    PIDActive = currentStatusMode == StatusMode.Firing;
                    break;
                case PIDModeTypes.Everything:
                    PIDActive = true;
                    break;
            }

            // Temporarily inhibit maneuvers if not evading a missile and waiting for a launched missile to fly to a safe distance
            if (currentStatusMode != StatusMode.Evading && weaponManager && weaponManager.PreviousMissile)
            {
                if ((vessel.CoM - weaponManager.PreviousMissile.vessel.transform.position).sqrMagnitude < vessel.vesselSize.sqrMagnitude)
                {
                    fc.Stability(true);
                    PIDActive = false;
                }
                else
                    fc.Stability(false);
            }
            else
                fc.Stability(false);

            // Set PID Mode
            fc.PIDActive = PIDActive;
            if (PIDActive)
                vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, true);

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
            return (o.altitude < minSafeAltitude) || (o.PeA < 0.8f * minSafeAltitude && o.timeToPe < o.timeToAp);
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
            if (Vector3.Dot(relVel, relPos) > -0.01f * relPos.sqrMagnitude)
                return false;
            float angleToRotate = Vector3.Angle(vessel.ReferenceTransform.up, relVel) * Mathf.Deg2Rad * 0.75f;
            float timeToRotate = BDAMath.SolveTime(angleToRotate, maxAngularAccelerationMag) / 0.75f;
            float relSpeed = relVel.magnitude;
            float interceptStoppingDistance = StoppingDistance(relSpeed) + relSpeed * (margin + timeToRotate * 3f);
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
            if (Vector3.Dot(relPos, relVel) >= 0f)
                return false;
            timeToCPA = vessel.TimeToCPA(targetVessel);
            Vector3 cpa = AIUtils.PredictPosition(relPos, relVel, Vector3.zero, timeToCPA);
            float interceptRange = interceptRanges.z;
            float interceptRangeTolSqr = (interceptRange * (tolerance + 1f)) * (interceptRange * (tolerance + 1f));
            return cpa.sqrMagnitude < interceptRangeTolSqr && Mathf.Abs(relVel.magnitude - ManeuverSpeed) < ManeuverSpeed * tolerance;
        }

        private Vector3 Intercept(Vector3 relPos, Vector3 relVel)
        {
            Vector3 lateralVel = Vector3.ProjectOnPlane(-relVel, relPos);
            Vector3 lateralOffset = lateralVel.normalized * interceptRanges.z;
            return relPos + lateralOffset;
        }

        private Vector3 InterceptionRanges()
        {
            Vector3 interceptRanges = Vector3.zero;
            float minRange = MinEngagementRange;
            float maxRange = Mathf.Max(weaponManager.gunRange, minRange * 1.2f);
            bool usingProjectile = true;
            if (weaponManager != null)
            {
                
                if (weaponManager.selectedWeapon != null)
                {
                    currentWeapon = weaponManager.selectedWeapon;
                    EngageableWeapon engageableWeapon = currentWeapon as EngageableWeapon;
                    minRange = Mathf.Max(engageableWeapon.GetEngagementRangeMin(), minRange);
                    maxRange = engageableWeapon.GetEngagementRangeMax();
                    usingProjectile = weaponManager.selectedWeapon.GetWeaponClass() != WeaponClasses.Missile;
                    if (usingProjectile) { maxRange = Mathf.Min(maxRange, weaponManager.gunRange); }
                }
                else
                {
                    for (int i = 0; i < weaponManager.weaponArray.Length; i++)
                    {
                        var weapon = weaponManager.weaponArray[i];
                        if (weapon == null) continue;
                        float maxEngageRange = ((EngageableWeapon)weapon).GetEngagementRangeMax();
                        if (weapon.GetWeaponClass() != WeaponClasses.Missile)
                            maxRange = Mathf.Max(Mathf.Min(maxEngageRange, weaponManager.gunRange), maxRange);
                        else
                        {
                            maxRange = Mathf.Max(maxEngageRange * (weaponManager.UnguidedMissile(weapon as MissileBase, maxEngageRange) ? 0.1f : 1f), maxRange);
                            usingProjectile = false;
                        }
                    }
                }
                if (targetVessel != null)
                    minRange = Mathf.Max(minRange, targetVessel.GetRadius());
            }
            float interceptRange = minRange + (maxRange - minRange) * (usingProjectile ? 0.25f : 0.75f);
            interceptRanges.x = minRange;
            interceptRanges.y = maxRange;
            interceptRanges.z = interceptRange;
            return interceptRanges;
        }

        private bool GunReady(ModuleWeapon gun)
        {
            if (gun == null) return false;

            // Check gun/laser can fire soon, we are within guard and weapon engagement ranges, and we are under the firing speed
            float targetSqrDist = FromTo(vessel, targetVessel).sqrMagnitude;
            return GunFiringSpeedCheck() && gun.CanFireSoon() &&
                (targetSqrDist <= gun.GetEngagementRangeMax() * gun.GetEngagementRangeMax()) &&
                (targetSqrDist <= weaponManager.gunRange * weaponManager.gunRange);
        }

        private bool GunFiringSpeedCheck()
        {
            // See if we are under firing speed for firing, or if killing velocity, under kill velocity speed target
            float relVelSqrMag = RelVel(vessel, targetVessel).sqrMagnitude;
            if (currentStatus.Contains("Kill Velocity"))
            {
                float speedTarget = KillVelocityTargetSpeed();
                return relVelSqrMag < speedTarget * speedTarget;
            }
            else
                return relVelSqrMag < firingSpeed * firingSpeed;
        }

        private float KillVelocityTargetSpeed()
        {
            return Mathf.Clamp(maxAcceleration * 0.15f, firingSpeed / 5f, firingSpeed);
        }

        private bool AwayCheck(float minRange)
        {
            // Check if we need to manually burn away from an enemy that's too close or
            // if it would be better to drift away.

            Vector3 toTarget = FromTo(vessel, targetVessel);
            Vector3 toEscape = -toTarget.normalized;
            Vector3 relVel = targetVessel.GetObtVelocity() - vessel.GetObtVelocity();

            float rotDistance = Vector3.Angle(vessel.ReferenceTransform.up, toEscape) * Mathf.Deg2Rad;
            float timeToRotate = BDAMath.SolveTime(rotDistance / 2, maxAngularAccelerationMag) * 2;
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

        private void UpdateBurnAlignmentTolerance()
        {
            if (!hasEC && !hasRCS)
                fc.alignmentToleranceforBurn = 180f;
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
            return Vector3.Angle(tv1, tv2) / window;
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
            maxAngularAccelerationMag = maxAngularAcceleration.magnitude;

            float angVel = vessel.angularVelocity.magnitude;
            float angAccel = Mathf.Abs(angVel - lastAngVel) / Time.fixedDeltaTime / 3f;
            dynAngAccel *= dynDecayRate; // Decay the highest observed angular acceleration (we want a fairly recent value in case the craft's dynamics have changed).
            dynAngAccel = Mathf.Max(dynAngAccel, angAccel);
            maxAngularAccelerationMag = Mathf.Clamp(((1f - 0.1f)*maxAngularAccelerationMag + 0.1f * dynAngAccel), maxAngularAccelerationMag, dynAngAccel);
            lastAngVel = angVel;
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
        
        //Controller Integral
        Vector3 directionIntegral;
        float pitchIntegral;
        float yawIntegral;
        float rollIntegral;
        Vector3 prevTargetDir;
        void AttitudeControl(FlightCtrlState s)
        {
            Vector3 targetDirection = fc.attitude;
            Vector3 currentRoll = -vesselTransform.forward;
            Vector3 rollTarget = currentRoll;
            if (targetVessel != null) // If we have a target, adjust roll orientation relative to target based on rollMode setting
            {
                Vector3 toTarget = FromTo(vessel, targetVessel).normalized;
                switch (rollMode)
                {
                    case RollModeTypes.Port_Starboard:
                        {
                            if (Vector3.Dot(toTarget, vesselTransform.right) > 0f)
                                rollTarget = Vector3.Cross(vesselTransform.up, toTarget).ProjectOnPlanePreNormalized(vesselTransform.up);
                            else
                                rollTarget = Vector3.Cross(-vesselTransform.up, toTarget).ProjectOnPlanePreNormalized(vesselTransform.up);
                        }
                        break;
                    case RollModeTypes.Dorsal_Ventral:
                        {
                            if (Vector3.Dot(toTarget, vesselTransform.forward) > 0f)
                                rollTarget = toTarget.ProjectOnPlanePreNormalized(vesselTransform.up);
                            else
                                rollTarget = -toTarget.ProjectOnPlanePreNormalized(vesselTransform.up);
                        }
                        break;
                    case RollModeTypes.Port:
                        rollTarget = Vector3.Cross(-vesselTransform.up, toTarget).ProjectOnPlanePreNormalized(vesselTransform.up);
                        break;
                    case RollModeTypes.Starboard:
                        rollTarget = Vector3.Cross(vesselTransform.up, toTarget).ProjectOnPlanePreNormalized(vesselTransform.up);
                        break;
                    case RollModeTypes.Dorsal:
                        rollTarget = toTarget.ProjectOnPlanePreNormalized(vesselTransform.up);
                        break;
                    case RollModeTypes.Ventral:
                        rollTarget = -toTarget.ProjectOnPlanePreNormalized(vesselTransform.up);
                        break;
                }
                debugRollTarget = rollTarget * 100f; ;
            }
            else
                debugRollTarget = Vector3.zero;

            Vector3 localTargetDirection = vesselTransform.InverseTransformDirection(targetDirection).normalized;
            localTargetDirection = Vector3.RotateTowards(Vector3.up, localTargetDirection, 25 * Mathf.Deg2Rad, 0);

            float pitchError = VectorUtils.SignedAngle(Vector3.up, localTargetDirection.ProjectOnPlanePreNormalized(Vector3.right), Vector3.back);
            float yawError = VectorUtils.SignedAngle(Vector3.up, localTargetDirection.ProjectOnPlanePreNormalized(Vector3.forward), Vector3.right);
            float rollError = BDAMath.SignedAngle(currentRoll, rollTarget, vesselTransform.right);

            Vector3 localAngVel = vessel.angularVelocity;
            Vector3 targetAngVel = Vector3.Cross(prevTargetDir, targetDirection) / Time.fixedDeltaTime;
            Vector3 localTargetAngVel = vesselTransform.InverseTransformVector(targetAngVel);
            localAngVel -= localTargetAngVel;
            prevTargetDir = targetDirection;

            #region PID calculations
            float pitchProportional = 0.005f * steerMult * pitchError;
            float yawProportional = 0.005f * steerMult * yawError;
            float rollProportional = 0.005f * steerMult * rollError;

            float pitchDamping = steerDamping * -localAngVel.x;
            float yawDamping = steerDamping * -localAngVel.z;
            float rollDamping = steerDamping * -localAngVel.y;

            // For the integral, we track the vector of the pitch and yaw in the 2D plane of the vessel's forward pointing vector so that the pitch and yaw components translate between the axes when the vessel rolls.
            directionIntegral = (directionIntegral + (pitchError * -vesselTransform.forward + yawError * vesselTransform.right) * Time.deltaTime).ProjectOnPlanePreNormalized(vesselTransform.up);
            if (directionIntegral.sqrMagnitude > 1f) directionIntegral = directionIntegral.normalized;
            pitchIntegral = steerKiAdjust * Vector3.Dot(directionIntegral, -vesselTransform.forward);
            yawIntegral = steerKiAdjust * Vector3.Dot(directionIntegral, vesselTransform.right);
            rollIntegral = steerKiAdjust * Mathf.Clamp(rollIntegral + rollError * Time.deltaTime, -1f, 1f);

            var steerPitch = pitchProportional + pitchIntegral - pitchDamping;
            var steerYaw = yawProportional + yawIntegral - yawDamping;
            var steerRoll = rollProportional + rollIntegral - rollDamping;

            float maxSteer = 1;

            if (BDArmorySettings.DEBUG_LINES)
            {
                debugTargetPosition = vessel.transform.position + targetDirection * 1000; // The asked for target position's direction
                debugTargetDirection = vessel.transform.position + vesselTransform.TransformDirection(localTargetDirection) * 200; // The actual direction to match the "up" direction of the craft with for pitch (used for PID calculations).
            }

            SetFlightControlState(s,
                Mathf.Clamp(steerPitch, -maxSteer, maxSteer), // pitch
                Mathf.Clamp(steerYaw, -maxSteer, maxSteer), // yaw
                Mathf.Clamp(steerRoll, -maxSteer, maxSteer)); // roll

            if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_AI)
            {
                debugString.AppendLine(string.Format("rollError: {0,7:F4}, pitchError: {1,7:F4}, yawError: {2,7:F4}", rollError, pitchError, yawError));
                debugString.AppendLine(string.Format("Pitch: P: {0,7:F4}, I: {1,7:F4}, D: {2,7:F4}", pitchProportional, pitchIntegral, pitchDamping));
                debugString.AppendLine(string.Format("Yaw: P: {0,7:F4}, I: {1,7:F4}, D: {2,7:F4}", yawProportional, yawIntegral, yawDamping));
                debugString.AppendLine(string.Format("Roll: P: {0,7:F4}, I: {1,7:F4}, D: {2,7:F4}", rollProportional, rollIntegral, rollDamping));
            }
            #endregion
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