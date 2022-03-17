using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BDArmory.Control;
using BDArmory.Core;
using BDArmory.Core.Extension;
using BDArmory.Core.Utils;
using BDArmory.Misc;
using BDArmory.UI;
using UnityEngine;

namespace BDArmory.Modules
{
    public class BDModuleVTOLAI : BDGenericAIBase, IBDAIControl
    {
        #region Declarations

        Vessel extendingTarget = null;
        Vessel bypassTarget = null;
        Vector3 bypassTargetPos;

        Vector3 targetDirection;
        float targetVelocity; // the velocity the craft should target, not the velocity of its target
        float targetAltitude; // the altitude the craft should hold, not the altitude of its target
        Vector3 rollTarget;
        bool aimingMode = false;

        int collisionDetectionTicker = 0;
        Vector3? dodgeVector;
        float weaveAdjustment = 0;
        float weaveDirection = 1;
        const float weaveLimit = 15;
        const float weaveFactor = 6.5f;

        Vector3 upDir;

        AIUtils.TraversabilityMatrix pathingMatrix;
        List<Vector3> waypoints = new List<Vector3>();
        bool leftPath = false;

        protected override Vector3d assignedPositionGeo
        {
            get { return intermediatePositionGeo; }
            set
            {
                finalPositionGeo = value;
                leftPath = true;
            }
        }

        Vector3 upDirection = Vector3.up;
        Vector3d finalPositionGeo;
        Vector3d intermediatePositionGeo;
        public override Vector3d commandGPS => finalPositionGeo;

        private BDVTOLSpeedControl airspeedControl;

        //wing command
        bool useRollHint;

        // Terrain avoidance and below minimum altitude globals.
        int terrainAlertTicker = 0; // A ticker to reduce the frequency of terrain alert checks.
        bool belowMinAltitude; // True when below minAltitude or avoiding terrain.
        bool gainAltInhibited = false; // Inhibit gain altitude to minimum altitude when chasing or evading someone as long as we're pointing upwards.
        bool avoidingTerrain = false; // True when avoiding terrain.
        bool initialTakeOff = true; // False after the initial take-off.
        float terrainAlertDetectionRadius = 30.0f; // Sphere radius that the vessel occupies. Should cover most vessels. FIXME This could be based on the vessel's maximum width/height.
        float terrainAlertThreatRange; // The distance to the terrain to consider (based on turn radius).
        float terrainAlertThreshold; // The current threshold for triggering terrain avoidance based on various factors.
        float terrainAlertDistance; // Distance to the terrain (in the direction of the terrain normal).
        Vector3 terrainAlertNormal; // Approximate surface normal at the terrain intercept.
        Vector3 terrainAlertDirection; // Terrain slope in the direction of the velocity at the terrain intercept.
        Vector3 terrainAlertCorrectionDirection; // The direction to go to avoid the terrain.
        float terrainAlertCoolDown = 0; // Cool down period before allowing other special modes to take effect (currently just "orbitting").
        Vector3 relativeVelocityRightDirection; // Right relative to current velocity and upDirection.
        Vector3 relativeVelocityDownDirection; // Down relative to current velocity and upDirection.
        Vector3 terrainAlertDebugPos, terrainAlertDebugDir, terrainAlertDebugPos2, terrainAlertDebugDir2; // Debug vector3's for drawing lines.
        bool terrainAlertDebugDraw2 = false;

        float turnRadius;
        float bodyGravity = (float)PhysicsGlobals.GravitationalAcceleration;

        float dynDynPresGRecorded = 1f; // Start at reasonable non-zero value.
        float dynVelocityMagSqr = 1f; // Start at reasonable non-zero value.
        float dynDecayRate = 1f; // Decay rate for dynamic measurements. Set to a half-life of 60s in Start.
        float dynVelSmoothingCoef = 1f; // Decay rate for smoothing the dynVelocityMagSqr

        public float TurnRadius
        {
            get { return turnRadius; }
            private set { turnRadius = value; }
        }

        float maxLiftAcceleration;

        public float MaxLiftAcceleration
        {
            get { return maxLiftAcceleration; }
            private set { maxLiftAcceleration = value; }
        }

        //settings


        // Max pitch
        // - Don't pitch beyond this when gaining speed
        // Max speed
        // Default alt
        // - Default behavior outside of combat
        // Climb rate
        // Climb speed
        // - How AI transitions between altitudes
        // Combat Alt
        // Combat Speed
        // - Maintain these in combat

        // Use throttle/pitch to control:
        // - Speed
        // - Altitude
        // - Climb rate


        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_VehicleType"),//Vehicle type
            UI_ChooseOption(options = new string[4] { "Stationary", "Land", "Water", "Amphibious" })]
        public string SurfaceTypeName = "Land";

        public AIUtils.VehicleMovementType SurfaceType
            => (AIUtils.VehicleMovementType)Enum.Parse(typeof(AIUtils.VehicleMovementType), SurfaceTypeName);

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_MaxPitchAngle"),//Max pitch angle
            UI_FloatRange(minValue = 1f, maxValue = 90f, stepIncrement = 1f, scene = UI_Scene.All)]
        public float MaxPitchAngle = 30f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_CombatAltitude"), //Combat Alt.
            UI_FloatRange(minValue = 50f, maxValue = 5000f, stepIncrement = 50f, scene = UI_Scene.All)]
        public float CombatAltitude = 100;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_CombatSpeed"),//Combat speed
            UI_FloatRange(minValue = 5f, maxValue = 100f, stepIncrement = 1f, scene = UI_Scene.All)]
        public float CombatSpeed = 40;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_DefaultAltitude"), //Default Alt.
            UI_FloatRange(minValue = 50f, maxValue = 5000f, stepIncrement = 50f, scene = UI_Scene.All)]
        public float defaultAltitude = 1000;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_MinAltitude"), //Min Altitude
            UI_FloatRange(minValue = 10f, maxValue = 1000, stepIncrement = 10f, scene = UI_Scene.All)]
        public float minAltitude = 200f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_ClimbRate"),//Climb Rate
            UI_FloatRange(minValue = 5f, maxValue = 100f, stepIncrement = 1f, scene = UI_Scene.All)]
        public float ClimbRate = 20;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_MaxSpeed"),//Max speed
            UI_FloatRange(minValue = 5f, maxValue = 200f, stepIncrement = 1f, scene = UI_Scene.All)]
        public float MaxSpeed = 30;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_MaxDrift"),//Max drift
            UI_FloatRange(minValue = 1f, maxValue = 180f, stepIncrement = 1f, scene = UI_Scene.All)]
        public float MaxDrift = 10;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_TargetPitch"),//Moving pitch
            UI_FloatRange(minValue = -10f, maxValue = 40f, stepIncrement = .1f, scene = UI_Scene.All)]
        public float TargetPitch = 20;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_MaxBankAngle"),// Max Bank angle
            UI_FloatRange(minValue = 0f, maxValue = 90f, stepIncrement = 1f, scene = UI_Scene.All)]
        public float MaxBankAngle = 0;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_SteerFactor"),//Steer Factor
            UI_FloatRange(minValue = 0.2f, maxValue = 20f, stepIncrement = .1f, scene = UI_Scene.All)]
        public float steerMult = 6;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_SteerKi"), //Steer Ki
            UI_FloatRange(minValue = 0.01f, maxValue = 1f, stepIncrement = 0.01f, scene = UI_Scene.All)]
        public float steerKiAdjust = 0.4f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_SteerDamping"),//Steer Damping
            UI_FloatRange(minValue = 0.1f, maxValue = 10f, stepIncrement = .1f, scene = UI_Scene.All)]
        public float steerDamping = 3;

        //[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Steering"),
        //	UI_Toggle(enabledText = "Powered", disabledText = "Passive")]
        public bool PoweredSteering = true;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_BroadsideAttack"),//Attack vector
            UI_Toggle(enabledText = "#LOC_BDArmory_BroadsideAttack_enabledText", disabledText = "#LOC_BDArmory_BroadsideAttack_disabledText")]//Broadside--Bow
        public bool BroadsideAttack = false;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_MinEngagementRange"),//Min engagement range
            UI_FloatRange(minValue = 0f, maxValue = 6000f, stepIncrement = 100f, scene = UI_Scene.All)]
        public float MinEngagementRange = 500;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_MaxEngagementRange"),//Max engagement range
            UI_FloatRange(minValue = 500f, maxValue = 8000f, stepIncrement = 100f, scene = UI_Scene.All)]
        public float MaxEngagementRange = 4000;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, category = "DoubleSlider", guiName = "#LOC_BDArmory_TurnRadiusTwiddleFactorMin", advancedTweakable = true),//Turn radius twiddle factors (category seems to have no effect)
            UI_FloatRange(minValue = 1f, maxValue = 5f, stepIncrement = 0.1f, scene = UI_Scene.All)]
        public float turnRadiusTwiddleFactorMin = 2.0f; // Minimum and maximum twiddle factors for the turn radius. Depends on roll rate and how the vessel behaves under fire.

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, category = "DoubleSlider", guiName = "#LOC_BDArmory_TurnRadiusTwiddleFactorMax", advancedTweakable = true),//Turn radius twiddle factors (category seems to have no effect)
            UI_FloatRange(minValue = 1f, maxValue = 5f, stepIncrement = 0.1f, scene = UI_Scene.All)]
        public float turnRadiusTwiddleFactorMax = 3.0f; // Minimum and maximum twiddle factors for the turn radius. Depends on roll rate and how the vessel behaves under fire.

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_ManeuverRCS"),//RCS active
            UI_Toggle(enabledText = "#LOC_BDArmory_ManeuverRCS_enabledText", disabledText = "#LOC_BDArmory_ManeuverRCS_disabledText", scene = UI_Scene.All),]//Maneuvers--Combat
        public bool ManeuverRCS = false;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_MinObstacleMass", advancedTweakable = true),//Min obstacle mass
            UI_FloatRange(minValue = 0f, maxValue = 100f, stepIncrement = 1f, scene = UI_Scene.All),]
        public float AvoidMass = 0f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_PreferredBroadsideDirection", advancedTweakable = true),//Preferred broadside direction
            UI_ChooseOption(options = new string[3] { "Port", "Either", "Starboard" }, scene = UI_Scene.All),]
        public string OrbitDirectionName = "Either";
        public readonly string[] orbitDirections = new string[3] { "Port", "Either", "Starboard" };

        [KSPField(isPersistant = true)]
        int sideSlipDirection = 0;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_GoesUp", advancedTweakable = true),//Goes up to 
            UI_Toggle(enabledText = "#LOC_BDArmory_GoesUp_enabledText", disabledText = "#LOC_BDArmory_GoesUp_disabledText", scene = UI_Scene.All),]//eleven--ten
        public bool UpToEleven = false;
        bool toEleven = false;

        const float AttackAngleAtMaxRange = 30f;

        Dictionary<string, float> altMaxValues = new Dictionary<string, float>
        {
            { nameof(MaxPitchAngle), 90f },
            { nameof(CombatSpeed), 300f },
            { nameof(MaxSpeed), 400f },
            { nameof(steerMult), 200f },
            { nameof(steerDamping), 100f },
            { nameof(MinEngagementRange), 20000f },
            { nameof(MaxEngagementRange), 30000f },
            { nameof(AvoidMass), 1000000f },
        };

        #endregion Declarations

        #region RMB info in editor

        // <color={XKCDColors.HexFormat.Lime}>Yes</color>
        public override string GetInfo()
        {
            // known bug - the game caches the RMB info, changing the variable after checking the info
            // does not update the info. :( No idea how to force an update.
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<b>Available settings</b>:");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Vehicle type</color> - can this vessel operate on land/sea/both");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Max slope angle</color> - what is the steepest slope this vessel can negotiate");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Cruise speed</color> - the default speed at which it is safe to maneuver");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Max speed</color> - the maximum combat speed");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Max drift</color> - maximum allowed angle between facing and velocity vector");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Moving pitch</color> - the pitch level to maintain when moving at cruise speed");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Bank angle</color> - the limit on roll when turning, positive rolls into turns");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Steer Factor</color> - higher will make the AI apply more control input for the same desired rotation");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Steer Damping</color> - higher will make the AI apply more control input when it wants to stop rotation");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Attack vector</color> - does the vessel attack from the front or the sides");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Min engagement range</color> - AI will try to move away from oponents if closer than this range");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Max engagement range</color> - AI will prioritize getting closer over attacking when beyond this range");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- RCS active</color> - Use RCS during any maneuvers, or only in combat ");
            if (GameSettings.ADVANCED_TWEAKABLES)
            {
                sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Min obstacle mass</color> - Obstacles of a lower mass than this will be ignored instead of avoided");
                sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Goes up to</color> - Increases variable limits, no direct effect on behaviour");
            }

            return sb.ToString();
        }

        #endregion RMB info in editor

        #region events

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            SetChooseOptions();
        }

        public override void ActivatePilot()
        {
            base.ActivatePilot();

            pathingMatrix = new AIUtils.TraversabilityMatrix();

            if (!airspeedControl)
            {
                airspeedControl = gameObject.AddComponent<BDVTOLSpeedControl>();
                airspeedControl.vessel = vessel;
            }
            airspeedControl.Activate();

            if (BroadsideAttack && sideSlipDirection == 0)
            {
                SetBroadsideDirection(OrbitDirectionName);
            }

            leftPath = true;
            extendingTarget = null;
            bypassTarget = null;
            collisionDetectionTicker = 6;
        }

        public override void DeactivatePilot()
        {
            base.DeactivatePilot();

            if (airspeedControl)
                airspeedControl.Deactivate();
        }

        public void SetChooseOptions()
        {
            UI_ChooseOption broadisdeEditor = (UI_ChooseOption)Fields["OrbitDirectionName"].uiControlEditor;
            UI_ChooseOption broadisdeFlight = (UI_ChooseOption)Fields["OrbitDirectionName"].uiControlFlight;
            UI_ChooseOption SurfaceEditor = (UI_ChooseOption)Fields["SurfaceTypeName"].uiControlEditor;
            UI_ChooseOption SurfaceFlight = (UI_ChooseOption)Fields["SurfaceTypeName"].uiControlFlight;
            broadisdeEditor.onFieldChanged = ChooseOptionsUpdated;
            broadisdeFlight.onFieldChanged = ChooseOptionsUpdated;
            SurfaceEditor.onFieldChanged = ChooseOptionsUpdated;
            SurfaceFlight.onFieldChanged = ChooseOptionsUpdated;
        }

        public void ChooseOptionsUpdated(BaseField field, object obj)
        {
            this.part.RefreshAssociatedWindows();
            if (BDArmoryAIGUI.Instance != null)
            {
                BDArmoryAIGUI.Instance.SetChooseOptionSliders();
            }
        }

        public void SetBroadsideDirection(string direction)
        {
            if (!orbitDirections.Contains(direction)) return;
            OrbitDirectionName = direction;
            sideSlipDirection = orbitDirections.IndexOf(OrbitDirectionName) - 1;
            if (sideSlipDirection == 0)
                sideSlipDirection = UnityEngine.Random.value > 0.5f ? 1 : -1;
        }

        void Update()
        {
            // switch up the alt values if up to eleven is toggled
            if (UpToEleven != toEleven)
            {
                using (var s = altMaxValues.Keys.ToList().GetEnumerator())
                    while (s.MoveNext())
                    {
                        UI_FloatRange euic = (UI_FloatRange)
                            (HighLogic.LoadedSceneIsFlight ? Fields[s.Current].uiControlFlight : Fields[s.Current].uiControlEditor);
                        float tempValue = euic.maxValue;
                        euic.maxValue = altMaxValues[s.Current];
                        altMaxValues[s.Current] = tempValue;
                        // change the value back to what it is now after fixed update, because changing the max value will clamp it down
                        // using reflection here, don't look at me like that, this does not run often
                        StartCoroutine(setVar(s.Current, (float)typeof(BDModuleVTOLAI).GetField(s.Current).GetValue(this)));
                    }
                toEleven = UpToEleven;
            }
        }

        IEnumerator setVar(string name, float value)
        {
            yield return new WaitForFixedUpdate();
            typeof(BDModuleVTOLAI).GetField(name).SetValue(this, value);
        }

        protected override void OnGUI()
        {
            base.OnGUI();

            if (!pilotEnabled || !vessel.isActiveVessel) return;

            if (!BDArmorySettings.DRAW_DEBUG_LINES) return;
            if (command == PilotCommands.Follow)
            {
                BDGUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, assignedPositionWorld, 2, Color.red);
            }

            BDGUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, vesselTransform.position + targetDirection * 10f, 2, Color.blue);
            BDGUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position + (0.05f * vesselTransform.right), vesselTransform.position + (0.05f * vesselTransform.right), 2, Color.green);

            pathingMatrix.DrawDebug(vessel.CoM, waypoints);
        }

        #endregion events

        #region Actual AI Pilot

        protected override void AutoPilot(FlightCtrlState s)
        {
            if (!vessel.Autopilot.Enabled)
                vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, true);

            targetVelocity = 0;
            targetDirection = vesselTransform.up;
            targetAltitude = defaultAltitude;
            aimingMode = false;
            upDir = VectorUtils.GetUpDirection(vesselTransform.position);
            DebugLine("");

            if (initialTakeOff)
            {
                // pilot logic figures out what we're supposed to be doing, and sets the base state
                PilotLogic();
                // situational awareness modifies the base as best as it can (evasive mainly)
                Tactical();
            }
            else
            {
                Takeoff();
            }
            CheckLandingGear();
            AttitudeControl3(s); // move according to our targets
            AdjustThrottle(targetVelocity); // set throttle according to our targets and movement
        }

        void PilotLogic()
        {
            // check for belowMinAlt
            belowMinAltitude = (float)vessel.radarAltitude < minAltitude;
            
            // check for collisions, but not every frame
            if (collisionDetectionTicker == 0)
            {
                collisionDetectionTicker = 20;
                float predictMult = Mathf.Clamp(10 / MaxDrift, 1, 10);

                dodgeVector = null;

                using (var vs = BDATargetManager.LoadedVessels.GetEnumerator())
                    while (vs.MoveNext())
                    {
                        if (vs.Current == null || vs.Current == vessel || vs.Current.GetTotalMass() < AvoidMass) continue;
                        if (!VesselModuleRegistry.ignoredVesselTypes.Contains(vs.Current.vesselType))
                        {
                            var ibdaiControl = VesselModuleRegistry.GetModule<IBDAIControl>(vs.Current);
                            if (!vs.Current.LandedOrSplashed || (ibdaiControl != null && ibdaiControl.commandLeader != null && ibdaiControl.commandLeader.vessel == vessel))
                                continue;
                        }
                        dodgeVector = PredictCollisionWithVessel(vs.Current, 5f * predictMult, 0.5f);
                        if (dodgeVector != null) break;
                    }
            }
            else
                collisionDetectionTicker--;

            // avoid collisions if any are found
            if (dodgeVector != null)
            {
                targetVelocity = PoweredSteering ? MaxSpeed : CombatSpeed;
                targetDirection = (Vector3)dodgeVector;
                SetStatus($"Avoiding Collision");
                leftPath = true;
                return;
            }

            // if bypass target is no longer relevant, remove it
            if (bypassTarget != null && ((bypassTarget != targetVessel && bypassTarget != (commandLeader != null ? commandLeader.vessel : null))
                || (VectorUtils.GetWorldSurfacePostion(bypassTargetPos, vessel.mainBody) - bypassTarget.CoM).sqrMagnitude > 500000))
            {
                bypassTarget = null;
            }

            if (bypassTarget == null)
            {
                // check for enemy targets and engage
                // not checking for guard mode, because if guard mode is off now you can select a target manually and if it is of opposing team, the AI will try to engage while you can man the turrets
                if (weaponManager && targetVessel != null && !BDArmorySettings.PEACE_MODE)
                {
                    leftPath = true;
                    if (collisionDetectionTicker == 5)
                        checkBypass(targetVessel);

                    Vector3 vecToTarget = targetVessel.CoM - vessel.CoM;
                    float distance = vecToTarget.magnitude;
                    // lead the target a bit, where 1km/s is a ballpark estimate of the average bullet velocity
                    float shotSpeed = 1000f;
                    if ((weaponManager != null ? weaponManager.selectedWeapon : null) is ModuleWeapon wep)
                        shotSpeed = wep.bulletVelocity;
                    vecToTarget = targetVessel.PredictPosition(distance / shotSpeed) - vessel.CoM;

                    if (BroadsideAttack)
                    {
                        Vector3 sideVector = Vector3.Cross(vecToTarget, upDir); //find a vector perpendicular to direction to target
                        if (collisionDetectionTicker == 10
                                && !pathingMatrix.TraversableStraightLine(
                                        VectorUtils.WorldPositionToGeoCoords(vessel.CoM, vessel.mainBody),
                                        VectorUtils.WorldPositionToGeoCoords(vessel.PredictPosition(10), vessel.mainBody),
                                        vessel.mainBody, SurfaceType, MaxPitchAngle, AvoidMass))
                            sideSlipDirection = -Math.Sign(Vector3.Dot(vesselTransform.up, sideVector)); // switch sides if we're running ashore
                        sideVector *= sideSlipDirection;

                        float sidestep = distance >= MaxEngagementRange ? Mathf.Clamp01((MaxEngagementRange - distance) / (CombatSpeed * Mathf.Clamp(90 / MaxDrift, 0, 10)) + 1) * AttackAngleAtMaxRange / 90 : // direct to target to attackAngle degrees if over maxrange
                            (distance <= MinEngagementRange ? 1.5f - distance / (MinEngagementRange * 2) : // 90 to 135 degrees if closer than minrange
                            (MaxEngagementRange - distance) / (MaxEngagementRange - MinEngagementRange) * (1 - AttackAngleAtMaxRange / 90) + AttackAngleAtMaxRange / 90); // attackAngle to 90 degrees from maxrange to minrange
                        targetDirection = Vector3.LerpUnclamped(vecToTarget.normalized, sideVector.normalized, sidestep); // interpolate between the side vector and target direction vector based on sidestep
                        targetVelocity = MaxSpeed;
                        targetAltitude = CombatAltitude;
                        DebugLine($"Broadside attack angle {sidestep}");
                    }
                    else // just point at target and go
                    {
                        targetAltitude = CombatAltitude;
                        if ((targetVessel.horizontalSrfSpeed < 10 || Vector3.Dot(Vector3.ProjectOnPlane(targetVessel.srf_vel_direction, upDir), vessel.up) < 0) //if target is stationary or we're facing in opposite directions
                            && (distance < MinEngagementRange || (distance < (MinEngagementRange * 3 + MaxEngagementRange) / 4 //and too close together
                            && extendingTarget != null && targetVessel != null && extendingTarget == targetVessel)))
                        {
                            extendingTarget = targetVessel;
                            // not sure if this part is very smart, potential for improvement
                            targetDirection = -vecToTarget; //extend
                            targetVelocity = MaxSpeed;
                            targetAltitude = CombatAltitude;
                            SetStatus($"Extending");
                            return;
                        }
                        else
                        {
                            extendingTarget = null;
                            targetDirection = Vector3.ProjectOnPlane(vecToTarget, upDir);
                            if (Vector3.Dot(targetDirection, vesselTransform.up) < 0)
                                targetVelocity = PoweredSteering ? MaxSpeed : 0; // if facing away from target
                            else if (distance >= MaxEngagementRange || distance <= MinEngagementRange)
                                targetVelocity = MaxSpeed;
                            else
                            {
                                targetVelocity = CombatSpeed / 10 + (MaxSpeed - CombatSpeed / 10) * (distance - MinEngagementRange) / (MaxEngagementRange - MinEngagementRange); //slow down if inside engagement range to extend shooting opportunities
                                if (weaponManager != null && weaponManager.selectedWeapon != null)
                                {
                                    switch (weaponManager.selectedWeapon.GetWeaponClass())
                                    {
                                        case WeaponClasses.Gun:
                                        case WeaponClasses.Rocket:
                                        case WeaponClasses.DefenseLaser:
                                            var gun = (ModuleWeapon)weaponManager.selectedWeapon;
                                            if ((gun.yawRange == 0 || gun.maxPitch == gun.minPitch) && gun.FiringSolutionVector != null)
                                            {
                                                aimingMode = true;
                                                if (Vector3.Angle((Vector3)gun.FiringSolutionVector, vessel.transform.up) < 20)
                                                    targetDirection = (Vector3)gun.FiringSolutionVector;
                                            }
                                            break;
                                    }
                                }
                            }
                            targetVelocity = Mathf.Clamp(targetVelocity, PoweredSteering ? CombatSpeed / 5 : 0, MaxSpeed); // maintain a bit of speed if using powered steering
                        }
                    }
                    SetStatus($"Engaging target");
                    return;
                }

                // follow
                if (command == PilotCommands.Follow)
                {
                    leftPath = true;
                    if (collisionDetectionTicker == 5)
                        checkBypass(commandLeader.vessel);

                    Vector3 targetPosition = GetFormationPosition();
                    Vector3 targetDistance = targetPosition - vesselTransform.position;
                    if (Vector3.Dot(targetDistance, vesselTransform.up) < 0
                        && Vector3.ProjectOnPlane(targetDistance, upDir).sqrMagnitude < 250f * 250f
                        && Vector3.Angle(vesselTransform.up, commandLeader.vessel.srf_velocity) < 0.8f)
                    {
                        targetDirection = Vector3.RotateTowards(Vector3.ProjectOnPlane(commandLeader.vessel.srf_vel_direction, upDir), targetDistance, 0.2f, 0);
                    }
                    else
                    {
                        targetDirection = Vector3.ProjectOnPlane(targetDistance, upDir);
                    }
                    targetVelocity = (float)(commandLeader.vessel.horizontalSrfSpeed + (vesselTransform.position - targetPosition).magnitude / 15);
                    if (Vector3.Dot(targetDirection, vesselTransform.up) < 0 && !PoweredSteering) targetVelocity = 0;
                    SetStatus($"Following");
                    return;
                }
            }

            // goto
            if (leftPath && bypassTarget == null)
            {
                Pathfind(finalPositionGeo);
                leftPath = false;
            }

            const float targetRadius = 250f;
            targetDirection = Vector3.ProjectOnPlane(assignedPositionWorld - vesselTransform.position, upDir);

            if (targetDirection.sqrMagnitude > targetRadius * targetRadius)
            {
                if (bypassTarget != null)
                    targetVelocity = MaxSpeed;
                else if (waypoints.Count > 1)
                    targetVelocity = command == PilotCommands.Attack ? MaxSpeed : CombatSpeed;
                else
                    targetVelocity = Mathf.Clamp((targetDirection.magnitude - targetRadius / 2) / 5f,
                    0, command == PilotCommands.Attack ? MaxSpeed : CombatSpeed);

                if (Vector3.Dot(targetDirection, vesselTransform.up) < 0 && !PoweredSteering) targetVelocity = 0;
                SetStatus(bypassTarget ? "Repositioning" : "Moving");
                return;
            }

            cycleWaypoint();

            SetStatus($"Not doing anything in particular");
            targetDirection = vesselTransform.up;
        }

        void Tactical()
        {
            // enable RCS if we're in combat
            vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, weaponManager && targetVessel && !BDArmorySettings.PEACE_MODE
                && (weaponManager.selectedWeapon != null || (vessel.CoM - targetVessel.CoM).sqrMagnitude < MaxEngagementRange * MaxEngagementRange)
                || weaponManager.underFire || weaponManager.missileIsIncoming);

            // if weaponManager thinks we're under fire, do the evasive dance
            if (weaponManager.underFire || weaponManager.missileIsIncoming)
            {
                targetVelocity = MaxSpeed;
                if (weaponManager.underFire || weaponManager.incomingMissileDistance < 2500)
                {
                    if (Mathf.Abs(weaveAdjustment) + Time.deltaTime * weaveFactor > weaveLimit) weaveDirection *= -1;
                    weaveAdjustment += weaveFactor * weaveDirection * Time.deltaTime;
                }
                else
                {
                    weaveAdjustment = 0;
                }
            }
            else
            {
                weaveAdjustment = 0;
            }
            DebugLine($"underFire {weaponManager.underFire}, weaveAdjustment {weaveAdjustment}");
        }

        void AdjustThrottle(float targetSpeed)
        {
            targetVelocity = Mathf.Clamp(targetVelocity, 0, MaxSpeed);

            if (float.IsNaN(targetSpeed)) //because yeah, I might have left division by zero in there somewhere
            {
                targetSpeed = CombatSpeed;
                DebugLine("Target velocity NaN, set to CombatSpeed.");
            }
            else
                DebugLine($"Target velocity: {targetVelocity}");
            //DebugLine($"engine thrust: {speedController.debugThrust}, motor zero: {airspeedControl.zeroPoint}");

            speedController.targetSpeed = airspeedControl.targetSpeed = targetSpeed;
            airspeedControl.targetVSpeed = Mathf.Clamp(1 - (targetAltitude - (float)vessel.radarAltitude) / targetAltitude, -1f, 1f) * ClimbRate;
            //speedController.useBrakes = airspeedControl.preventNegativeZeroPoint = speedController.debugThrust > 0;
        }

        void AttitudeControl(FlightCtrlState s)
        {
            const float terrainOffset = 5;

            Vector3 yawTarget = Vector3.ProjectOnPlane(targetDirection, vesselTransform.forward);

            // limit "aoa" if we're moving
            float driftMult = 1;
            if (vessel.horizontalSrfSpeed * 10 > CombatSpeed)
            {
                driftMult = Mathf.Max(Vector3.Angle(vessel.srf_velocity, yawTarget) / MaxDrift, 1);
                yawTarget = Vector3.RotateTowards(vessel.srf_velocity, yawTarget, MaxDrift * Mathf.Deg2Rad, 0);
            }

            float yawError = VectorUtils.SignedAngle(vesselTransform.up, yawTarget, vesselTransform.right) + (aimingMode ? 0 : weaveAdjustment);
            DebugLine($"yaw target: {yawTarget}, yaw error: {yawError}");
            DebugLine($"drift multiplier: {driftMult}");

            Vector3 baseForward = vessel.transform.up * terrainOffset;
            float basePitch = Mathf.Atan2(
                AIUtils.GetTerrainAltitude(vessel.CoM + baseForward, vessel.mainBody, false)
                - AIUtils.GetTerrainAltitude(vessel.CoM - baseForward, vessel.mainBody, false),
                terrainOffset * 2) * Mathf.Rad2Deg;
            float pitchAngle = basePitch + TargetPitch * Mathf.Clamp01((float)vessel.horizontalSrfSpeed / CombatSpeed);
            if (aimingMode)
                pitchAngle = VectorUtils.SignedAngle(vesselTransform.up, Vector3.ProjectOnPlane(targetDirection, vesselTransform.right), -vesselTransform.forward);
            pitchAngle += Mathf.Clamp(1 - (targetVelocity - (float)vessel.srfSpeed) / targetVelocity, -1f, 1f) * -TargetPitch; //Adjust pitchAngle for desired speed
            DebugLine($"terrain fw slope: {basePitch}, target pitch: {pitchAngle}");

            float pitch = 90 - Vector3.Angle(vesselTransform.up, upDir);
           
            float pitchError = pitchAngle - pitch;

            Vector3 baseLateral = vessel.transform.right * terrainOffset;
            float baseRoll = Mathf.Atan2(
                AIUtils.GetTerrainAltitude(vessel.CoM + baseLateral, vessel.mainBody, false)
                - AIUtils.GetTerrainAltitude(vessel.CoM - baseLateral, vessel.mainBody, false),
                terrainOffset * 2) * Mathf.Rad2Deg;
            float drift = VectorUtils.SignedAngle(vesselTransform.up, Vector3.ProjectOnPlane(vessel.GetSrfVelocity(), upDir), vesselTransform.right);
            float bank = VectorUtils.SignedAngle(-vesselTransform.forward, upDir, -vesselTransform.right);
            float targetRoll = baseRoll + MaxBankAngle * Mathf.Clamp01(drift / MaxDrift) * Mathf.Clamp01((float)vessel.srfSpeed / CombatSpeed);
            float rollError = targetRoll - bank;
            DebugLine($"terrain sideways slope: {baseRoll}, target roll: {targetRoll}");

            Vector3 localAngVel = vessel.angularVelocity;
            s.roll = steerMult * 0.006f * rollError - 0.4f * steerDamping * -localAngVel.y;
            s.pitch = ((aimingMode ? 0.02f : 0.015f) * steerMult * pitchError) - (steerDamping * -localAngVel.x);
            s.yaw = (((aimingMode ? 0.007f : 0.005f) * steerMult * yawError) - (steerDamping * 0.2f * -localAngVel.z)) * driftMult;
            s.wheelSteer = -(((aimingMode ? 0.005f : 0.003f) * steerMult * yawError) - (steerDamping * 0.1f * -localAngVel.z));

            if (ManeuverRCS && (Mathf.Abs(s.roll) >= 1 || Mathf.Abs(s.pitch) >= 1 || Mathf.Abs(s.yaw) >= 1))
                vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, true);
        }

        void AttitudeControl3(FlightCtrlState s)
        {
            const float terrainOffset = 5;

            Vector3 yawTarget = Vector3.ProjectOnPlane(targetDirection, vesselTransform.forward);

            // limit "aoa" if we're moving
            float driftMult = 1;
            if (vessel.horizontalSrfSpeed * 10 > CombatSpeed)
            {
                driftMult = Mathf.Max(Vector3.Angle(vessel.srf_velocity, yawTarget) / MaxDrift, 1);
                yawTarget = Vector3.RotateTowards(vessel.srf_velocity, yawTarget, MaxDrift * Mathf.Deg2Rad, 0);
            }

            float yawError = VectorUtils.SignedAngle(vesselTransform.up, yawTarget, vesselTransform.right) + (aimingMode ? 0 : weaveAdjustment);
            DebugLine($"yaw target: {yawTarget}, yaw error: {yawError}");
            DebugLine($"drift multiplier: {driftMult}");



            float pitchAngle = Mathf.Clamp(1 - (targetVelocity - (float)vessel.horizontalSrfSpeed) / targetVelocity, -1f, 1f) * -TargetPitch; //Adjust pitchAngle for desired speed
            
            if (aimingMode)
                pitchAngle = VectorUtils.SignedAngle(vesselTransform.up, Vector3.ProjectOnPlane(targetDirection, vesselTransform.right), -vesselTransform.forward);
            if(belowMinAltitude || targetVelocity == 0f)
                pitchAngle = 0f;
            if (avoidingTerrain)
                pitchAngle = 90 - Vector3.Angle(Vector3.ProjectOnPlane(targetDirection, vesselTransform.right), upDir);
            DebugLine($"target pitch: {pitchAngle}");

            float pitch = 90 - Vector3.Angle(vesselTransform.up, upDir);

            float pitchError = pitchAngle - pitch;


            float drift = VectorUtils.SignedAngle(vesselTransform.up, Vector3.ProjectOnPlane(vessel.GetSrfVelocity(), upDir), vesselTransform.right);
            float bank = VectorUtils.SignedAngle(-vesselTransform.forward, upDir, -vesselTransform.right);
            float targetRoll = MaxBankAngle * Mathf.Clamp01(drift / MaxDrift) * Mathf.Clamp01((float)vessel.horizontalSrfSpeed / CombatSpeed);
            if (belowMinAltitude)
            {
                if (avoidingTerrain)
                    rollTarget = terrainAlertNormal * 100;
                else
                    rollTarget = vessel.upAxis * 100;
                targetRoll = VectorUtils.SignedAngle(-vesselTransform.forward, rollTarget, -vesselTransform.right);
            }

            float rollError = targetRoll - bank;
            DebugLine($"target roll: {targetRoll}");

            Vector3 localAngVel = vessel.angularVelocity;
            //s.roll = steerMult * 0.006f * rollError - 0.4f * steerDamping * -localAngVel.y;
            //s.pitch = ((aimingMode ? 0.02f : 0.015f) * steerMult * pitchError) - (steerDamping * -localAngVel.x);
            //s.yaw = (((aimingMode ? 0.007f : 0.005f) * steerMult * yawError) - (steerDamping * 0.2f * -localAngVel.z)) * driftMult;
            #region PID calculations
            // FIXME Why are there various constants in here that mess with the scaling of the PID in the various axes? Ratios between the axes are 1:0.33:0.1
            float pitchProportional = 0.015f * steerMult * pitchError;
            float yawProportional = 0.005f * steerMult * yawError;
            float rollProportional = 0.0015f * steerMult * rollError;

            float pitchDamping = steerDamping * -localAngVel.x;
            float yawDamping = 0.33f * steerDamping * -localAngVel.z;
            float rollDamping = 0.1f * steerDamping * -localAngVel.y;

            // For the integral, we track the vector of the pitch and yaw in the 2D plane of the vessel's forward pointing vector so that the pitch and yaw components translate between the axes when the vessel rolls.
            directionIntegral = Vector3.ProjectOnPlane(directionIntegral + (pitchError * -vesselTransform.forward + yawError * vesselTransform.right) * Time.deltaTime, vesselTransform.up);
            if (directionIntegral.sqrMagnitude > 1f) directionIntegral = directionIntegral.normalized;
            pitchIntegral = steerKiAdjust * Vector3.Dot(directionIntegral, -vesselTransform.forward);
            yawIntegral = 0.33f * steerKiAdjust * Vector3.Dot(directionIntegral, vesselTransform.right);
            rollIntegral = 0.1f * steerKiAdjust * Mathf.Clamp(rollIntegral + rollError * Time.deltaTime, -1f, 1f);

            s.pitch = pitchProportional + pitchIntegral - pitchDamping;
            s.yaw = yawProportional + yawIntegral - yawDamping;
            s.roll = rollProportional + rollIntegral - rollDamping;
            #endregion
            s.wheelSteer = -(((aimingMode ? 0.005f : 0.003f) * steerMult * yawError) - (steerDamping * 0.1f * -localAngVel.z));

            if (ManeuverRCS && (Mathf.Abs(s.roll) >= 1 || Mathf.Abs(s.pitch) >= 1 || Mathf.Abs(s.yaw) >= 1))
                vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, true);
        }

        Vector3 prevTargetDir;
        Vector3 angVelRollTarget;
        //Controller Integral
        Vector3 directionIntegral;
        float pitchIntegral;
        float yawIntegral;
        float rollIntegral;

        void AttitudeControl2(FlightCtrlState s)
        {
            Vector3 targetPosition = Vector3.ProjectOnPlane(targetDirection*100f, vesselTransform.forward);
            Vector3 targetDirectionYaw;
            float yawError;
            float pitchError;
            //float postYawFactor;
            //float postPitchFactor;

            Vector3d srfVel = vessel.Velocity();
            if (srfVel != Vector3d.zero)
            {
                velocityTransform.rotation = Quaternion.LookRotation(srfVel, -vesselTransform.forward);
            }
            velocityTransform.rotation = Quaternion.AngleAxis(90, velocityTransform.right) * velocityTransform.rotation;

            //ang vel
            Vector3 localAngVel = vessel.angularVelocity;
            Vector3 currTargetDir = (targetPosition - vesselTransform.position).normalized;
            Vector3 targetAngVel = Vector3.Cross(prevTargetDir, currTargetDir) / Time.fixedDeltaTime;
            Vector3 localTargetAngVel = vesselTransform.InverseTransformVector(targetAngVel);



            if (!aimingMode)
            {
                targetDirection = velocityTransform.InverseTransformDirection(targetPosition - velocityTransform.position).normalized;
                targetDirection = Vector3.RotateTowards(Vector3.up, targetDirection, 45 * Mathf.Deg2Rad, 0);

                targetDirectionYaw = vesselTransform.InverseTransformDirection(vessel.Velocity()).normalized;
                targetDirectionYaw = Vector3.RotateTowards(Vector3.up, targetDirectionYaw, 45 * Mathf.Deg2Rad, 0);
            }
            else
            {
                targetDirection = vesselTransform.InverseTransformDirection(targetPosition - vesselTransform.position).normalized;
                targetDirection = Vector3.RotateTowards(Vector3.up, targetDirection, 25 * Mathf.Deg2Rad, 0);
                targetDirectionYaw = targetDirection;
            }

            //// Adjust targetDirection based on ATTITUDE limits
            //var horizonUp = Vector3.ProjectOnPlane(vesselTransform.up, upDirection).normalized;
            //var horizonRight = -Vector3.Cross(horizonUp, upDirection);
            //float attitude = Vector3.SignedAngle(horizonUp, vesselTransform.up, horizonRight);
            //if ((Mathf.Abs(attitude) > maxAttitude) && (maxAttitude != 90f))
            //{
            //    var projectPlane = Vector3.RotateTowards(upDirection, horizonUp, attitude * Mathf.PI / 180f, 0f);
            //    targetDirection = Vector3.ProjectOnPlane(targetDirection, projectPlane);
            //}
            //debugString.AppendLine($"Attitude: " + attitude);

            pitchError = VectorUtils.SignedAngle(Vector3.up, Vector3.ProjectOnPlane(targetDirection, Vector3.right), Vector3.back);
            if (!belowMinAltitude)
                pitchError += Mathf.Clamp(1 - (targetVelocity - (float)vessel.srfSpeed) / targetVelocity, -1f, 1f) * -TargetPitch; //Adjust pitch angle for desired speed
            yawError = VectorUtils.SignedAngle(Vector3.up, Vector3.ProjectOnPlane(targetDirectionYaw, Vector3.forward), Vector3.right);

            //roll
            Vector3 currentRoll = -vesselTransform.forward;
            float rollUp = (aimingMode ? 5f : 10f);
            rollTarget = (targetPosition + (rollUp * upDirection)) - vesselTransform.position;

            //test
            if (aimingMode && belowMinAltitude)
            {
                angVelRollTarget = -140 * vesselTransform.TransformVector(Quaternion.AngleAxis(90f, Vector3.up) * localTargetAngVel);
                rollTarget += angVelRollTarget;
            }

            if (command == PilotCommands.Follow && useRollHint)
            {
                rollTarget = -commandLeader.vessel.ReferenceTransform.forward;
            }

            if (belowMinAltitude)
            {
                    rollTarget = vessel.upAxis * 100;
            }

            float rollError = Utils.SignedAngle(currentRoll, rollTarget, vesselTransform.right);

            #region PID calculations
            // FIXME Why are there various constants in here that mess with the scaling of the PID in the various axes? Ratios between the axes are 1:0.33:0.1
            float pitchProportional = 0.015f * steerMult * pitchError;
            float yawProportional = 0.005f * steerMult * yawError;
            float rollProportional = 0.0015f * steerMult * rollError;

            float pitchDamping = steerDamping * -localAngVel.x;
            float yawDamping = 0.33f * steerDamping * -localAngVel.z;
            float rollDamping = 0.1f * steerDamping * -localAngVel.y;

            // For the integral, we track the vector of the pitch and yaw in the 2D plane of the vessel's forward pointing vector so that the pitch and yaw components translate between the axes when the vessel rolls.
            directionIntegral = Vector3.ProjectOnPlane(directionIntegral + (pitchError * -vesselTransform.forward + yawError * vesselTransform.right) * Time.deltaTime, vesselTransform.up);
            if (directionIntegral.sqrMagnitude > 1f) directionIntegral = directionIntegral.normalized;
            pitchIntegral = steerKiAdjust * Vector3.Dot(directionIntegral, -vesselTransform.forward);
            yawIntegral = 0.33f * steerKiAdjust * Vector3.Dot(directionIntegral, vesselTransform.right);
            rollIntegral = 0.1f * steerKiAdjust * Mathf.Clamp(rollIntegral + rollError * Time.deltaTime, -1f, 1f);

            var steerPitch = pitchProportional + pitchIntegral - pitchDamping;
            var steerYaw = yawProportional + yawIntegral - yawDamping;
            var steerRoll = rollProportional + rollIntegral - rollDamping;
            #endregion

            //v/q
            float dynamicAdjustment = Mathf.Clamp(16 * (float)(vessel.srfSpeed / vessel.dynamicPressurekPa), 0, 1.2f);
            steerPitch *= dynamicAdjustment;
            steerYaw *= dynamicAdjustment;
            steerRoll *= dynamicAdjustment;

            s.pitch = Mathf.Clamp(steerPitch, Mathf.Min(-1f, -0.2f), 1f); // finalMaxSteer for pitch and yaw, but not roll.
            s.yaw = Mathf.Clamp(steerYaw, -1f, 1f);
            s.roll = Mathf.Clamp(steerRoll, -1f, 1);

            if (BDArmorySettings.DRAW_DEBUG_LABELS)
            {
                DebugLine(String.Format("Aiming Mode: {0}, rollError: {1,7:F4}, pitchError: {2,7:F4}, yawError: {3,7:F4}", aimingMode, rollError, pitchError, yawError));
                // debugString.AppendLine($"Bank Angle: " + bankAngle);
                DebugLine(String.Format("Pitch: P: {0,7:F4}, I: {1,7:F4}, D: {2,7:F4}", pitchProportional, pitchIntegral, pitchDamping));
                DebugLine(String.Format("Yaw: P: {0,7:F4}, I: {1,7:F4}, D: {2,7:F4}", yawProportional, yawIntegral, yawDamping));
                DebugLine(String.Format("Roll: P: {0,7:F4}, I: {1,7:F4}, D: {2,7:F4}", rollProportional, rollIntegral, rollDamping));
            }
        }
        #endregion Actual AI Pilot

        #region Autopilot helper functions

        void CalculateAccelerationAndTurningCircle()
        {
            maxLiftAcceleration = dynDynPresGRecorded * (float)vessel.dynamicPressurekPa; //maximum acceleration from lift that the vehicle can provide

            maxLiftAcceleration = Mathf.Clamp(maxLiftAcceleration, bodyGravity, 10f * bodyGravity); //limit it to whichever is smaller, what we can provide or what we can handle. Assume minimum of 1G to avoid extremely high turn radiuses.

            turnRadius = dynVelocityMagSqr / maxLiftAcceleration; //radius that we can turn in assuming constant velocity, assuming simple circular motion (this is a terrible assumption, the AI usually turns on afterboosters!)
        }

        bool FlyAvoidTerrain(FlightCtrlState s) // Check for terrain ahead.
        {
            if (initialTakeOff) return false; // Don't do anything during the initial take-off.
            bool initialCorrection = !avoidingTerrain;
            float controlLagTime = 1.5f; // Time to fully adjust control surfaces. (Typical values seem to be 0.286s -- 1s for neutral to deployed according to wing lift comparison.) FIXME maybe this could also be a slider.

            ++terrainAlertTicker;
            int terrainAlertTickerThreshold = BDArmorySettings.TERRAIN_ALERT_FREQUENCY * (int)(1 + Mathf.Pow((float)vessel.radarAltitude / 500.0f, 2.0f) / Mathf.Max(1.0f, (float)vessel.srfSpeed / 150.0f)); // Scale with altitude^2 / speed.
            if (terrainAlertTicker >= terrainAlertTickerThreshold)
            {
                terrainAlertTicker = 0;

                // Reset/initialise some variables.
                avoidingTerrain = false; // Reset the alert.
                if (vessel.radarAltitude > minAltitude)
                    belowMinAltitude = false; // Also, reset the belowMinAltitude alert if it's active because of avoiding terrain.
                terrainAlertDistance = -1.0f; // Reset the terrain alert distance.
                float turnRadiusTwiddleFactor = turnRadiusTwiddleFactorMax; // A twiddle factor based on the orientation of the vessel, since it often takes considerable time to re-orient before avoiding the terrain. Start with the worst value.
                terrainAlertThreatRange = turnRadiusTwiddleFactor * turnRadius + (float)vessel.srfSpeed * controlLagTime; // The distance to the terrain to consider.

                // First, look 45° down, up, left and right from our velocity direction for immediate danger. (This should cover most immediate dangers.)
                Ray rayForwardUp = new Ray(vessel.transform.position, (vessel.srf_vel_direction - relativeVelocityDownDirection).normalized);
                Ray rayForwardDown = new Ray(vessel.transform.position, (vessel.srf_vel_direction + relativeVelocityDownDirection).normalized);
                Ray rayForwardLeft = new Ray(vessel.transform.position, (vessel.srf_vel_direction - relativeVelocityRightDirection).normalized);
                Ray rayForwardRight = new Ray(vessel.transform.position, (vessel.srf_vel_direction + relativeVelocityRightDirection).normalized);
                RaycastHit rayHit;
                if (Physics.Raycast(rayForwardDown, out rayHit, 1.5f * terrainAlertDetectionRadius, (int)LayerMasks.Scenery)) // sqrt(2) should be sufficient, so 1.5 will cover it.
                {
                    terrainAlertDistance = rayHit.distance * -Vector3.Dot(rayHit.normal, vessel.srf_vel_direction);
                    terrainAlertNormal = rayHit.normal;
                }
                if (Physics.Raycast(rayForwardUp, out rayHit, 1.5f * terrainAlertDetectionRadius, (int)LayerMasks.Scenery) && (terrainAlertDistance < 0.0f || rayHit.distance < terrainAlertDistance))
                {
                    terrainAlertDistance = rayHit.distance * -Vector3.Dot(rayHit.normal, vessel.srf_vel_direction);
                    terrainAlertNormal = rayHit.normal;
                }
                if (Physics.Raycast(rayForwardLeft, out rayHit, 1.5f * terrainAlertDetectionRadius, (int)LayerMasks.Scenery) && (terrainAlertDistance < 0.0f || rayHit.distance < terrainAlertDistance))
                {
                    terrainAlertDistance = rayHit.distance * -Vector3.Dot(rayHit.normal, vessel.srf_vel_direction);
                    terrainAlertNormal = rayHit.normal;
                }
                if (Physics.Raycast(rayForwardRight, out rayHit, 1.5f * terrainAlertDetectionRadius, (int)LayerMasks.Scenery) && (terrainAlertDistance < 0.0f || rayHit.distance < terrainAlertDistance))
                {
                    terrainAlertDistance = rayHit.distance * -Vector3.Dot(rayHit.normal, vessel.srf_vel_direction);
                    terrainAlertNormal = rayHit.normal;
                }
                if (terrainAlertDistance > 0)
                {
                    terrainAlertDirection = Vector3.ProjectOnPlane(vessel.srf_vel_direction, terrainAlertNormal).normalized;
                    avoidingTerrain = true;
                }
                else
                {
                    // Next, cast a sphere forwards to check for upcoming dangers.
                    Ray ray = new Ray(vessel.transform.position, vessel.srf_vel_direction);
                    if (Physics.SphereCast(ray, terrainAlertDetectionRadius, out rayHit, terrainAlertThreatRange, (int)LayerMasks.Scenery)) // Found something. 
                    {
                        // Check if there's anything directly ahead.
                        ray = new Ray(vessel.transform.position, vessel.srf_vel_direction);
                        terrainAlertDistance = rayHit.distance * -Vector3.Dot(rayHit.normal, vessel.srf_vel_direction); // Distance to terrain along direction of terrain normal.
                        terrainAlertNormal = rayHit.normal;
                        if (BDArmorySettings.DRAW_DEBUG_LINES)
                        {
                            terrainAlertDebugPos = rayHit.point;
                            terrainAlertDebugDir = rayHit.normal;
                        }
                        if (!Physics.Raycast(ray, out rayHit, terrainAlertThreatRange, (int)LayerMasks.Scenery)) // Nothing directly ahead, so we're just barely avoiding terrain.
                        {
                            // Change the terrain normal and direction as we want to just fly over it instead of banking away from it.
                            terrainAlertNormal = upDirection;
                            terrainAlertDirection = vessel.srf_vel_direction;
                        }
                        else
                        { terrainAlertDirection = Vector3.ProjectOnPlane(vessel.srf_vel_direction, terrainAlertNormal).normalized; }
                        float sinTheta = Math.Min(0.0f, Vector3.Dot(vessel.srf_vel_direction, terrainAlertNormal)); // sin(theta) (measured relative to the plane of the surface).
                        float oneMinusCosTheta = 1.0f - Mathf.Sqrt(Math.Max(0.0f, 1.0f - sinTheta * sinTheta));
                        turnRadiusTwiddleFactor = (turnRadiusTwiddleFactorMin + turnRadiusTwiddleFactorMax) / 2.0f - (turnRadiusTwiddleFactorMax - turnRadiusTwiddleFactorMin) / 2.0f * Vector3.Dot(terrainAlertNormal, -vessel.transform.forward); // This would depend on roll rate (i.e., how quickly the vessel can reorient itself to perform the terrain avoidance maneuver) and probably other things.
                        float controlLagCompensation = Mathf.Max(0f, -Vector3.Dot(AIUtils.PredictPosition(vessel, controlLagTime * turnRadiusTwiddleFactor) - vessel.transform.position, terrainAlertNormal)); // Include twiddle factor as more re-orienting requires more control surface movement.
                        terrainAlertThreshold = turnRadiusTwiddleFactor * turnRadius * oneMinusCosTheta + controlLagCompensation;
                        if (terrainAlertDistance < terrainAlertThreshold) // Only do something about it if the estimated turn amount is a problem.
                        {
                            avoidingTerrain = true;

                            // Shoot new ray in direction theta/2 (i.e., the point where we should be parallel to the surface) above velocity direction to check if the terrain slope is increasing.
                            float phi = -Mathf.Asin(sinTheta) / 2f;
                            Vector3 upcoming = Vector3.RotateTowards(vessel.srf_vel_direction, terrainAlertNormal, phi, 0f);
                            ray = new Ray(vessel.transform.position, upcoming);
                            if (BDArmorySettings.DRAW_DEBUG_LINES)
                                terrainAlertDebugDraw2 = false;
                            if (Physics.Raycast(ray, out rayHit, terrainAlertThreatRange, (int)LayerMasks.Scenery))
                            {
                                if (rayHit.distance < terrainAlertDistance / Mathf.Sin(phi)) // Hit terrain closer than expected => terrain slope is increasing relative to our velocity direction.
                                {
                                    if (BDArmorySettings.DRAW_DEBUG_LINES)
                                    {
                                        terrainAlertDebugDraw2 = true;
                                        terrainAlertDebugPos2 = rayHit.point;
                                        terrainAlertDebugDir2 = rayHit.normal;
                                    }
                                    terrainAlertNormal = rayHit.normal; // Use the normal of the steeper terrain (relative to our velocity).
                                    terrainAlertDirection = Vector3.ProjectOnPlane(vessel.srf_vel_direction, terrainAlertNormal).normalized;
                                }
                            }
                        }
                    }
                }
                // Finally, check the distance to sea-level as water doesn't act like a collider, so it's getting ignored. Also, for planets without surfaces.
                if (vessel.mainBody.ocean || !vessel.mainBody.hasSolidSurface)
                {
                    float sinTheta = Vector3.Dot(vessel.srf_vel_direction, upDirection); // sin(theta) (measured relative to the ocean surface).
                    if (sinTheta < 0f) // Heading downwards
                    {
                        float oneMinusCosTheta = 1.0f - Mathf.Sqrt(Math.Max(0.0f, 1.0f - sinTheta * sinTheta));
                        turnRadiusTwiddleFactor = (turnRadiusTwiddleFactorMin + turnRadiusTwiddleFactorMax) / 2.0f - (turnRadiusTwiddleFactorMax - turnRadiusTwiddleFactorMin) / 2.0f * Vector3.Dot(upDirection, -vessel.transform.forward); // This would depend on roll rate (i.e., how quickly the vessel can reorient itself to perform the terrain avoidance maneuver) and probably other things.
                        float controlLagCompensation = Mathf.Max(0f, -Vector3.Dot(AIUtils.PredictPosition(vessel, controlLagTime * turnRadiusTwiddleFactor) - vessel.transform.position, upDirection)); // Include twiddle factor as more re-orienting requires more control surface movement.
                        terrainAlertThreshold = turnRadiusTwiddleFactor * turnRadius * oneMinusCosTheta + controlLagCompensation;

                        if ((float)vessel.altitude < terrainAlertThreshold && (terrainAlertDistance < 0 || (float)vessel.altitude < terrainAlertDistance)) // If the ocean surface is closer than the terrain (if any), then override the terrain alert values.
                        {
                            terrainAlertDistance = (float)vessel.altitude;
                            terrainAlertNormal = upDirection;
                            terrainAlertDirection = Vector3.ProjectOnPlane(vessel.srf_vel_direction, upDirection).normalized;
                            avoidingTerrain = true;

                            if (BDArmorySettings.DRAW_DEBUG_LINES)
                            {
                                terrainAlertDebugPos = vessel.transform.position + vessel.srf_vel_direction * (float)vessel.altitude / -sinTheta;
                                terrainAlertDebugDir = upDirection;
                            }
                        }
                    }
                }
            }

            if (avoidingTerrain)
            {
                belowMinAltitude = true; // Inform other parts of the code to behave as if we're below minimum altitude.

                float maxAngle = 70.0f * Mathf.Deg2Rad; // Maximum angle (towards surface normal) to aim.
                float adjustmentFactor = 1f; // Mathf.Clamp(1.0f - Mathf.Pow(terrainAlertDistance / terrainAlertThreatRange, 2.0f), 0.0f, 1.0f); // Don't yank too hard as it kills our speed too much. (This doesn't seem necessary.)
                                             // First, aim up to maxAngle towards the surface normal.

                Vector3 correctionDirection = Vector3.RotateTowards(terrainAlertDirection, terrainAlertNormal, maxAngle * adjustmentFactor, 0.0f);
                // Then, adjust the vertical pitch for our speed (to try to avoid stalling).
                Vector3 horizontalCorrectionDirection = Vector3.ProjectOnPlane(correctionDirection, upDirection).normalized;
                correctionDirection = Vector3.RotateTowards(correctionDirection, horizontalCorrectionDirection, Mathf.Max(0.0f, (1.0f - (float)vessel.srfSpeed / 120.0f) * 0.8f * maxAngle) * adjustmentFactor, 0.0f); // Rotate up to 0.8*maxAngle back towards horizontal depending on speed < 120m/s.
                float alpha = Time.fixedDeltaTime * 2f; // 0.04 seems OK.
                float beta = Mathf.Pow(1.0f - alpha, terrainAlertTickerThreshold);
                terrainAlertCorrectionDirection = initialCorrection ? correctionDirection : (beta * terrainAlertCorrectionDirection + (1.0f - beta) * correctionDirection).normalized; // Update our target direction over several frames (if it's not the initial correction) due to changing terrain. (Expansion of N iterations of A = A*(1-a) + B*a. Not exact due to normalisation in the loop, but good enough.)
                targetDirection = vessel.transform.position + terrainAlertCorrectionDirection * 100f;
                
                // Update status and book keeping.
                SetStatus("Terrain (" + (int)terrainAlertDistance + "m)");
                terrainAlertCoolDown = 0.5f; // 0.5s cool down after avoiding terrain or gaining altitude. (Only used for delaying "orbitting" for now.)
                return true;
            }

            // Hurray, we've avoided the terrain!
            avoidingTerrain = false;
            return false;
        }

        public override bool CanEngage()
        {
            return !vessel.LandedOrSplashed;
        }

        GameObject vobj;

        Transform velocityTransform
        {
            get
            {
                if (!vobj)
                {
                    vobj = new GameObject("velObject");
                    vobj.transform.position = vessel.ReferenceTransform.position;
                    vobj.transform.parent = vessel.ReferenceTransform;
                }

                return vobj.transform;
            }
        }

        void CheckLandingGear()
        {
            if (!vessel.LandedOrSplashed)
            {
                if (!belowMinAltitude)
                    vessel.ActionGroups.SetGroup(KSPActionGroup.Gear, false);
                else
                    vessel.ActionGroups.SetGroup(KSPActionGroup.Gear, true);
            }
        }

        void Takeoff()
        {
            belowMinAltitude = (float)vessel.radarAltitude < minAltitude;
            if (!belowMinAltitude)
                initialTakeOff = true;
        }

        public override bool IsValidFixedWeaponTarget(Vessel target)
            => !BroadsideAttack &&
            (((target != null ? target.Splashed : false) && (SurfaceType & AIUtils.VehicleMovementType.Water) != 0) //boat targeting boat
            || ((target != null ? target.Landed : false) && (SurfaceType & AIUtils.VehicleMovementType.Land) != 0) //vee targeting vee
            || (((target != null && !target.LandedOrSplashed) && (SurfaceType & AIUtils.VehicleMovementType.Amphibious) != 0) && BDArmorySettings.SPACE_HACKS)) //repulsorcraft targeting repulsorcraft
            ; //valid if can traverse the same medium and using bow fire

        /// <returns>null if no collision, dodge vector if one detected</returns>
        Vector3? PredictCollisionWithVessel(Vessel v, float maxTime, float interval)
        {
            //evasive will handle avoiding missiles
            if (v == weaponManager.incomingMissileVessel
                || v.rootPart.FindModuleImplementing<MissileBase>() != null)
                return null;

            float time = Mathf.Min(0.5f, maxTime);
            while (time < maxTime)
            {
                Vector3 tPos = v.PredictPosition(time);
                Vector3 myPos = vessel.PredictPosition(time);
                if (Vector3.SqrMagnitude(tPos - myPos) < 2500f)
                {
                    return Vector3.Dot(tPos - myPos, vesselTransform.right) > 0 ? -vesselTransform.right : vesselTransform.right;
                }

                time = Mathf.MoveTowards(time, maxTime, interval);
            }

            return null;
        }

        void checkBypass(Vessel target)
        {
            if (!pathingMatrix.TraversableStraightLine(
                    VectorUtils.WorldPositionToGeoCoords(vessel.CoM, vessel.mainBody),
                    VectorUtils.WorldPositionToGeoCoords(target.CoM, vessel.mainBody),
                    vessel.mainBody, SurfaceType, MaxPitchAngle, AvoidMass))
            {
                bypassTarget = target;
                bypassTargetPos = VectorUtils.WorldPositionToGeoCoords(target.CoM, vessel.mainBody);
                waypoints = pathingMatrix.Pathfind(
                    VectorUtils.WorldPositionToGeoCoords(vessel.CoM, vessel.mainBody),
                    VectorUtils.WorldPositionToGeoCoords(target.CoM, vessel.mainBody),
                    vessel.mainBody, SurfaceType, MaxPitchAngle, AvoidMass);
                if (VectorUtils.GeoDistance(waypoints[waypoints.Count - 1], bypassTargetPos, vessel.mainBody) < 200)
                    waypoints.RemoveAt(waypoints.Count - 1);
                if (waypoints.Count > 0)
                    intermediatePositionGeo = waypoints[0];
                else
                    bypassTarget = null;
            }
        }

        private void Pathfind(Vector3 destination)
        {
            waypoints = pathingMatrix.Pathfind(
                                    VectorUtils.WorldPositionToGeoCoords(vessel.CoM, vessel.mainBody),
                                    destination, vessel.mainBody, SurfaceType, MaxPitchAngle, AvoidMass);
            intermediatePositionGeo = waypoints[0];
        }

        void cycleWaypoint()
        {
            if (waypoints.Count > 1)
            {
                waypoints.RemoveAt(0);
                intermediatePositionGeo = waypoints[0];
            }
            else if (bypassTarget != null)
            {
                waypoints.Clear();
                bypassTarget = null;
                leftPath = true;
            }
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
