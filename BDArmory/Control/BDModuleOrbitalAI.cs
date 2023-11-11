using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

using BDArmory.Extensions;
using BDArmory.Guidances;
using BDArmory.Targeting;
using BDArmory.Settings;
using BDArmory.UI;
using BDArmory.Utils;
using BDArmory.Weapons;
using BDArmory.Weapons.Missiles;
using static UnityEngine.GraphicsBuffer;

namespace BDArmory.Control
{
    public class BDModuleOrbitalAI : BDGenericAIBase, IBDAIControl
    {
        // All code contained within this file is adapted from Hatbat, Spartwo and MiffedStarfish's Kerbal Combat Systems Mod https://github.com/Halbann/StockCombatAI/tree/dev/Source/KerbalCombatSystems.
        // Code is distributed under CC-BY-SA 4.0: https://creativecommons.org/licenses/by-sa/4.0/

        #region KCS code
        const string shipControllerGroupName = "Ship AI";

        // User parameters changed via UI.

        public bool controllerRunning;
        public float updateInterval = 2.5f;
        public float emergencyUpdateInterval = 0.5f;
        public float combatUpdateInterval = 2.5f;
        private bool allowWithdrawal = true;
        public float firingAngularVelocityLimit = 1; // degrees per second
        public float controlTimeout = 10;

        // Ship AI variables.

        private BDOrbitalControl fc;
        private Coroutine pilotLogic;

        public IBDWeapon currentWeapon;

        private float lastUpdate;
        public float initialMass;
        private bool hasPropulsion;
        private bool hasWeapons;
        private bool hasControl;
        private float maxAcceleration;
        private float shipLength;
        private Vector3 maxAngularAcceleration;
        private double minSafeAltitude;
        public float heatSignature;
        public float averagedSize;
        private float lastInControl;
        private Part originalReferenceTransform;

        [KSPField(isPersistant = true)]
        public bool alive = true;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_MinEngagementRange"),//Min engagement range
            UI_FloatRange(minValue = 0f, maxValue = 10000f, stepIncrement = 100f, scene = UI_Scene.All)]
        public float MinEngagementRange = 500;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_MaxEngagementRange"),//Max engagement range
            UI_FloatRange(minValue = 500f, maxValue = 100000f, stepIncrement = 100f, scene = UI_Scene.All)]
        public float MaxEngagementRange = 5000;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_ManeuverRCS"),//RCS active
            UI_Toggle(enabledText = "#LOC_BDArmory_ManeuverRCS_enabledText", disabledText = "#LOC_BDArmory_ManeuverRCS_disabledText", scene = UI_Scene.All),]//Maneuvers--Combat
        public bool ManeuverRCS = false;

        public float vesselStandoffDistance = 200f; // try to avoid getting closer than 200m

        [KSPField(isPersistant = true,
            guiActive = true,
            guiActiveEditor = true,
            guiName = "Maneuvering Speed",
            guiUnits = " m/s",
            groupName = shipControllerGroupName,
            groupDisplayName = shipControllerGroupName),
            UI_FloatRange(
                minValue = 10f,
                maxValue = 500f,
                stepIncrement = 10f,
                scene = UI_Scene.All
            )]
        public float ManeuveringSpeed = 100f;

        [KSPField(isPersistant = true,
            guiActive = true,
            guiActiveEditor = true,
            guiName = "Strafing Speed Limit",
            guiUnits = " m/s",
            groupName = shipControllerGroupName,
            groupDisplayName = shipControllerGroupName),
            UI_FloatRange(
                minValue = 2f,
                maxValue = 100f,
                stepIncrement = 1f,
                scene = UI_Scene.All
            )]
        public float firingSpeed = 20f;

        [KSPField(isPersistant = true,
            guiActive = true,
            guiActiveEditor = true,
            guiName = "Max. Salvo Size",
            groupName = shipControllerGroupName,
            groupDisplayName = shipControllerGroupName),
            UI_FloatRange(
                minValue = 1,
                maxValue = 20,
                stepIncrement = 1,
                scene = UI_Scene.All
            )]
        public float maxSalvoSize = 5;

        [KSPField(isPersistant = true,
            guiActive = true,
            guiActiveEditor = true,
            guiName = "Salvo Interval",
            guiUnits = " s",
            groupName = shipControllerGroupName,
            groupDisplayName = shipControllerGroupName),
            UI_FloatRange(
                minValue = 1,
                maxValue = 30,
                stepIncrement = 0.1f,
                scene = UI_Scene.All
            )]
        public float firingInterval = 7.5f;

        [KSPField(isPersistant = true,
            guiActive = true,
            guiActiveEditor = true,
            guiUnits = "%",
            guiName = "Forwards Launch Throttle Limit", // could do with a better name
            groupName = shipControllerGroupName,
            groupDisplayName = shipControllerGroupName),
            UI_FloatRange(
                minValue = 0,
                maxValue = 100f,
                stepIncrement = 5f,
                scene = UI_Scene.All
            )]
        public float forwardLaunchThrottle = 0f;

        private const float priorityTargetMin = 1f;
        private const float priorityTargetMax = 250f;

        [KSPField(isPersistant = true,
            guiActive = true,
            guiActiveEditor = true,
            guiName = "Priority Target Mass",
            guiUnits = " t",
            groupName = shipControllerGroupName,
            groupDisplayName = shipControllerGroupName),
            UI_MinMaxRange(
                minValueX = priorityTargetMin,
                maxValueX = priorityTargetMax,
                minValueY = priorityTargetMin,
                maxValueY = priorityTargetMax,
                stepIncrement = 1f,
                scene = UI_Scene.All
            )]
        public Vector2 priorityTargetRange = new Vector2(priorityTargetMin, priorityTargetMax);

        [KSPField(
            isPersistant = true,
            guiActive = true,
            guiActiveEditor = true,
            guiName = "Withdrawing Enemies",
            groupName = shipControllerGroupName,
            groupDisplayName = shipControllerGroupName),
            UI_ChooseOption(controlEnabled = true, affectSymCounterparts = UI_Scene.None,
            options = new string[] { "Default", "Chase", "Ignore" })]
        public string withdrawingPriority = "Default";

        [KSPField(isPersistant = true,
            guiActive = true,
            guiActiveEditor = true,
            guiName = "Use Evasion",
            groupName = shipControllerGroupName,
            groupDisplayName = shipControllerGroupName),
            UI_Toggle(
                enabledText = "Enabled",
                disabledText = "Disabled",
                scene = UI_Scene.All
            )]
        public bool useEvasion = true;

        // Debugging
        internal float nearInterceptBurnTime;
        internal float nearInterceptApproachTime;
        internal float lateralVelocity;



        #endregion
        /// <summary>
        /// //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// </summary>
        #region Declarations


        Vector3 targetDirection;

        Vector3 upDir;


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
        }

        public override void ActivatePilot()
        {
            base.ActivatePilot();

            if (!fc)
            {
                fc = gameObject.AddComponent<BDOrbitalControl>();
                fc.vessel = vessel;

                fc.alignmentToleranceforBurn = 7.5f;
                fc.throttleLerpRate = 3;
            }
            fc.Activate();
            updateInterval = combatUpdateInterval;
            Vector3 size = vessel.vesselSize;
            shipLength = (new[] { size.x, size.y, size.z }).ToList().Max();
            averagedSize = (size.x + size.y + size.z) / 3;
            initialMass = vessel.GetTotalMass();
            StartCoroutine(CalculateMaxAcceleration());
            StartCoroutine(ShipController());

        }

        public override void DeactivatePilot()
        {
            base.DeactivatePilot();

            if (fc)
                fc.Deactivate();

            StopCoroutine(CalculateMaxAcceleration());
            StopCoroutine(ShipController());
        }

        private IEnumerator ShipController()
        {
            CheckStatus();
            //if (!alive)
            //{
            //    StopAI();
            //    yield break;
            //}

            while (true)
            {
                lastUpdate = Time.time;

                pilotLogic = StartCoroutine(PilotLogic());
                yield return pilotLogic;
            }
        }

        protected override void OnGUI()
        {
            base.OnGUI();

            if (!pilotEnabled || !vessel.isActiveVessel) return;

            if (!BDArmorySettings.DEBUG_LINES) return;
            if (command == PilotCommands.Follow)
            {
                GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, assignedPositionWorld, 2, Color.red);
            }

            //GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, vesselTransform.position + targetDirection * 10f, 2, Color.blue);
            //GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position + (0.05f * vesselTransform.right), vesselTransform.position + (0.05f * vesselTransform.right), 2, Color.green);

            // Vel vectors
            GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, vesselTransform.position + Vector3.Project(vessel.Velocity(), Vector3.ProjectOnPlane(vesselTransform.up, upDir)).normalized * 10f, 2, Color.cyan); //forward/rev
            GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, vesselTransform.position + Vector3.Project(vessel.Velocity(), Vector3.ProjectOnPlane(vesselTransform.right, upDir)).normalized * 10f, 3, Color.yellow); //lateral


            GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, vesselTransform.position + targetDirection * 10f, 5, Color.red);
            GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, vesselTransform.position + vesselTransform.up * 1000, 3, Color.white);
            GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, vesselTransform.position + -vesselTransform.forward * 100, 3, Color.yellow);
            GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, vesselTransform.position + vessel.Velocity().normalized * 100, 3, Color.magenta);
        }

        #endregion events

        #region Actual AI Pilot

        protected override void AutoPilot(FlightCtrlState s)
        {

            upDir = VectorUtils.GetUpDirection(vesselTransform.position);
            if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_AI) DebugLine("");

        }

        public bool CheckStatus()
        {
            bool hasRCSFore = vessel.FindPartModulesImplementing<ModuleRCSFX>().FindIndex(e => e.rcsEnabled && !e.flameout && e.useThrottle) > -1;
            hasPropulsion = hasRCSFore || vessel.FindPartModulesImplementing<ModuleEngines>().FindIndex(e => e.EngineIgnited && e.isOperational) > -1;
            hasWeapons = weaponManager.HasWeaponsAndAmmo();

            //bool spunOut = false;
            //if (vessel.angularVelocity.magnitude > 50)
            //{
            //    if (Time.time - lastInControl > controlTimeout)
            //        spunOut = true;
            //}
            //else
                lastInControl = Time.time;

            if (weaponManager.incomingMissileVessel != null && updateInterval != emergencyUpdateInterval)
                updateInterval = emergencyUpdateInterval;
            else if (weaponManager.incomingMissileVessel == null && updateInterval == emergencyUpdateInterval)
                updateInterval = combatUpdateInterval;

            hasControl = true;//vessel.isCommandable
                //&& !spunOut
                //&& vessel.CurrentControlLevel != Vessel.ControlLevel.NONE;

            bool dead = (!hasPropulsion && !hasWeapons) || !hasControl;
            alive = !dead;

            return alive;
        }

            private IEnumerator PilotLogic()
        {
            maxAcceleration = GetMaxAcceleration(vessel);
            fc.RCSVector = Vector3.zero;
            fc.alignmentToleranceforBurn = 5;
            fc.throttle = 0;
            ModuleWeapon currentProjectile = weaponManager.currentGun;
            vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, ManeuverRCS);

            // Movement.
            if (allowWithdrawal && hasPropulsion && !hasWeapons && CheckWithdraw())
            {
                currentStatus = "Withdrawing";

                
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
                bool facingNorth = Vector3.Angle(direction, orbitNormal) < 90;

                // Withdraw sequence. Locks behaviour while burning 200 m/s of delta-v either north or south.

                Vector3 deltav = orbitNormal * (facingNorth ? 1 : -1) * 200;
                fc.throttle = 1;

                while (deltav.magnitude > 10)
                {
                    if (!hasPropulsion) break;

                    deltav -= Vector3.Project(vessel.acceleration, deltav) * TimeWarp.fixedDeltaTime;
                    fc.attitude = deltav.normalized;

                    yield return new WaitForFixedUpdate();
                }

                fc.throttle = 0;
            }
            else if (useEvasion && weaponManager.incomingMissileVessel) // Needs to start evading an incoming missile.
            {

                currentStatus = "Dodging";
                vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, true);

                float previousTolerance = fc.alignmentToleranceforBurn;
                fc.alignmentToleranceforBurn = 45;
                fc.throttle = 1;

                Vessel incoming = weaponManager.incomingMissileVessel;
                Vector3 incomingVector = FromTo(vessel, incoming);
                Vector3 dodgeVector;

                bool complete = false;

                while (UnderTimeLimit() && incoming != null && !complete)
                {
                    incomingVector = FromTo(vessel, incoming);
                    dodgeVector = Vector3.ProjectOnPlane(vessel.ReferenceTransform.up, incomingVector.normalized);
                    fc.attitude = dodgeVector;
                    fc.RCSVector = dodgeVector * 2;

                    yield return new WaitForFixedUpdate();
                    complete = Vector3.Dot(RelVel(vessel, incoming), incomingVector) < 0;
                }

                fc.throttle = 0;
                fc.alignmentToleranceforBurn = previousTolerance;
            }
            //else if (target != null && HasLock() && CanFireProjectile(target) && AngularVelocity(vessel, target) < firingAngularVelocityLimit)
            else if (targetVessel != null && currentProjectile)
            {
                // Aim at target using current projectile weapon.
                // The weapon handles firing.

                currentStatus = "Firing Projectile";
                vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, true);
                fc.throttle = 0;
                fc.lerpAttitude = false;

                //Vessel v = targetVessel;
                //Vector3 target = AIUtils.PredictPosition(v, TimeWarp.fixedDeltaTime);//v.CoM;
                //Vector3 vectorToTarget = v.transform.position - vesselTransform.position;
                //float distanceToTarget = vectorToTarget.magnitude;

                //Vector3 leadOffset = currentProjectile.finalAimTarget;

                //float targetAngVel = Vector3.Angle(v.transform.position - vessel.transform.position, v.transform.position + (vessel.Velocity()) - vessel.transform.position);
                //float magnifier = Mathf.Clamp(targetAngVel, 1f, 2f);
                //magnifier += ((magnifier - 1f) * Mathf.Sin(Time.time * 0.75f));
                //if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_AI) debugString.AppendLine($"targetAngVel: {targetAngVel:F4}, magnifier: {magnifier:F2}");
                //target -= magnifier * leadOffset; // The effect of this is to exagerate the lead if the angular velocity is > 1

                //if (distanceToTarget > weaponManager.gunRange * 1.5f || Vector3.Dot(target - vesselTransform.position, vesselTransform.up) < 0) // Target is airborne a long way away or behind us.
                //{
                //    target = v.CoM; // Don't bother with the off-by-one physics frame correction as this doesn't need to be so accurate here.
                //}


                while (UnderTimeLimit() && targetVessel != null && currentProjectile.CanFireSoon())
                {
                    fc.attitude = currentProjectile.finalAimTarget;
                    fc.RCSVector = Vector3.ProjectOnPlane(RelVel(vessel, targetVessel), FromTo(vessel, targetVessel)) * -1;

                    // todo: correct for relative and angular velocity while firing if firing at an accelerating target

                    yield return new WaitForFixedUpdate();
                }

                RestoreReferenceTransform();
                fc.lerpAttitude = true;
            }
            else if (CheckOrbitUnsafe())
            {
                Orbit o = vessel.orbit;
                double UT;

                if (o.ApA < minSafeAltitude)
                {
                    // Entirety of orbit is inside atmosphere, burn up until apoapsis is outside atmosphere by a 10% margin.

                    currentStatus = "Correcting Orbit (Apoapsis too low)";
                    fc.throttle = 1;

                    while (UnderTimeLimit() && o.ApA < minSafeAltitude * 1.1)
                    {
                        UT = Planetarium.GetUniversalTime();
                        fc.attitude = o.Radial(UT);
                        yield return new WaitForFixedUpdate();
                    }
                }
                else if (o.altitude < minSafeAltitude)
                {
                    // Our apoapsis is outside the atmosphere but we are inside the atmosphere and descending.
                    // Burn up until we are ascending and our apoapsis is outside the atmosphere by a 10% margin.

                    currentStatus = "Correcting Orbit (Falling inside atmo)";
                    fc.throttle = 1;

                    while (UnderTimeLimit() && (o.ApA < minSafeAltitude * 1.1 || o.timeToPe < o.timeToAp))
                    {
                        UT = Planetarium.GetUniversalTime();
                        fc.attitude = o.Radial(UT);
                        yield return new WaitForFixedUpdate();
                    }
                }
                else
                {
                    // We are outside the atmosphere but our periapsis is inside the atmosphere.
                    // Execute a burn to circularize our orbit at the current altitude.

                    currentStatus = "Correcting Orbit (Circularizing)";

                    Vector3d fvel, deltaV = Vector3d.up * 100;
                    fc.throttle = 1;

                    while (UnderTimeLimit() && deltaV.magnitude > 2)
                    {
                        yield return new WaitForFixedUpdate();

                        UT = Planetarium.GetUniversalTime();
                        fvel = Math.Sqrt(o.referenceBody.gravParameter / o.GetRadiusAtUT(UT)) * o.Horizontal(UT);
                        deltaV = fvel - vessel.GetObtVelocity();

                        fc.attitude = deltaV.normalized;
                        fc.throttle = Mathf.Lerp(0, 1, (float)(deltaV.magnitude / 10));
                    }
                }
            }
            else if (targetVessel != null && hasWeapons)
            {

                // todo: implement for longer range movement.
                // https://github.com/MuMech/MechJeb2/blob/dev/MechJeb2/MechJebModuleRendezvousAutopilot.cs
                // https://github.com/MuMech/MechJeb2/blob/dev/MechJeb2/OrbitalManeuverCalculator.cs
                // https://github.com/MuMech/MechJeb2/blob/dev/MechJeb2/MechJebLib/Maths/Gooding.cs

                float minRange = Mathf.Max(MinEngagementRange, targetVessel.GetRadius() + vesselStandoffDistance);
                float maxRange = MaxEngagementRange;
                float minRangeProjectile = minRange;
                bool complete = false;
                bool usingProjectile = true;

                vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, true);

                if (weaponManager != null && weaponManager.selectedWeapon != null)
                {
                    currentWeapon = weaponManager.selectedWeapon;
                    minRange = Mathf.Max((currentWeapon as EngageableWeapon).engageRangeMin, minRange);
                    maxRange = Mathf.Min((currentWeapon as EngageableWeapon).engageRangeMax, maxRange);
                    usingProjectile = weaponManager.selectedWeapon.GetWeaponClass() != WeaponClasses.Missile;
                }
                
                float currentRange = VesselDistance(vessel, targetVessel);
                bool nearInt = false;
                Vector3 relVel = RelVel(vessel, targetVessel);
                if (currentRange < (!usingProjectile ? minRange : minRangeProjectile) && AwayCheck(minRange))
                {
                    currentStatus = "Maneuvering (Away)";
                    fc.throttle = 1;
                    fc.alignmentToleranceforBurn = 135;

                    while (UnderTimeLimit() && targetVessel != null && !complete)
                    {
                        fc.attitude = FromTo(vessel, targetVessel).normalized * -1;
                        fc.throttle = Vector3.Dot(RelVel(vessel, targetVessel), fc.attitude) < ManeuveringSpeed ? 1 : 0;
                        complete = FromTo(vessel, targetVessel).magnitude > minRange || !AwayCheck(minRange);

                        yield return new WaitForFixedUpdate();
                    }
                }
                // Reduce near intercept time by accounting for target acceleration
                // It should be such that "near intercept" is so close that you would go past them after you stop burning despite their acceleration
                // Also a chase timeout after which both parties should just use their weapons regardless of range.
                else if (hasPropulsion
                    && currentRange > maxRange
                    && !(nearInt = NearIntercept(relVel, minRange, maxRange))
                    && CanInterceptShip(targetVessel))
                {
                    currentStatus = "Maneuvering (Intercept Target)";
                    complete = FromTo(vessel, targetVessel).magnitude < maxRange || NearIntercept(relVel, minRange, maxRange);
                    while (UnderTimeLimit() && targetVessel != null && !complete)
                    {
                        Vector3 toTarget = FromTo(vessel, targetVessel);
                        relVel = targetVessel.GetObtVelocity() - vessel.GetObtVelocity();

                        toTarget = ToClosestApproach(toTarget, relVel * -1, minRange * 1.2f);

                        // Burn the difference between the target and current velocities.
                        Vector3 desiredVel = toTarget.normalized * 100;
                        Vector3 burn = desiredVel - (relVel * -1);

                        // Bias towards eliminating lateral velocity early on.
                        Vector3 lateral = Vector3.ProjectOnPlane(burn, toTarget.normalized);
                        burn = Vector3.Slerp(burn.normalized, lateral.normalized,
                            Mathf.Clamp01(lateral.magnitude / (maxAcceleration * 10))) * burn.magnitude;

                        lateralVelocity = lateral.magnitude;

                        float throttle = Vector3.Dot(RelVel(vessel, targetVessel), toTarget.normalized) < ManeuveringSpeed ? 1 : 0;
                        if (burn.magnitude / maxAcceleration < 1 && fc.throttle == 0)
                            throttle = 0;

                        fc.throttle = throttle * Mathf.Clamp(burn.magnitude / maxAcceleration, 0.2f, 1);

                        if (fc.throttle > 0)
                            fc.attitude = burn.normalized;
                        else
                            fc.attitude = toTarget.normalized;

                        complete = FromTo(vessel, targetVessel).magnitude < maxRange || NearIntercept(relVel, minRange, maxRange);

                        yield return new WaitForFixedUpdate();
                    }
                }
                else
                {
                    if (hasPropulsion && (relVel.magnitude > firingSpeed || nearInt))
                    {
                        currentStatus = "Maneuvering (Kill Velocity)";
                        while (UnderTimeLimit() && targetVessel != null && !complete)
                        {
                            relVel = targetVessel.GetObtVelocity() - vessel.GetObtVelocity();
                            fc.attitude = (relVel + targetVessel.acceleration).normalized;
                            complete = relVel.magnitude < firingSpeed / 3;
                            fc.throttle = !complete ? 1 : 0;

                            yield return new WaitForFixedUpdate();
                        }
                    }
                    else if (hasPropulsion && targetVessel != null && AngularVelocity(vessel, targetVessel) > firingAngularVelocityLimit)// && currentProjectile != null
                    {
                        currentStatus = "Maneuvering (Kill Angular Velocity)";

                        while (UnderTimeLimit() && targetVessel != null && !complete)
                        {
                            complete = AngularVelocity(vessel, targetVessel) < firingAngularVelocityLimit / 2;
                            fc.attitude = Vector3.ProjectOnPlane(RelVel(vessel, targetVessel), FromTo(vessel, targetVessel)).normalized * -1;
                            fc.throttle = !complete ? 1 : 0;

                            yield return new WaitForFixedUpdate();
                        }
                    }
                    else
                    {
                        if (hasPropulsion)
                        {
                            if (currentRange < minRange)
                            {
                                currentStatus = "Maneuvering (Drift Away)";

                                Vector3 toTarget;
                                fc.throttle = 0;

                                while (UnderTimeLimit() && targetVessel != null && !complete)
                                {
                                    toTarget = FromTo(vessel, targetVessel);
                                    complete = toTarget.magnitude > minRange;
                                    fc.attitude = toTarget.normalized;

                                    yield return new WaitForFixedUpdate();
                                }
                            }
                            else
                            {
                                currentStatus = "Maneuvering (Drift)";
                                fc.throttle = 0;
                                fc.attitude = Vector3.zero;
                            }
                        }
                        else
                        {
                            currentStatus = "Stranded";
                            fc.throttle = 0;
                            fc.attitude = Vector3.zero;
                        }

                        yield return new WaitForSeconds(updateInterval);
                    }
                }
            }
            else
            {
                // Idle

                if (hasWeapons)
                    currentStatus = "Idle";
                else
                    currentStatus = "Idle (Unarmed)";

                fc.throttle = 0;
                fc.attitude = Vector3.zero;

                yield return new WaitForSeconds(updateInterval);
            }
        }

        #endregion Actual AI Pilot

        #region Utility Functions

        private bool CheckWithdraw()
        {
            var nearest = BDATargetManager.GetClosestTarget(weaponManager);
            if (nearest == null) return false;

            return Mathf.Abs(RelVel(vessel, nearest.Vessel).magnitude) < 200;
        }

        private bool CheckOrbitUnsafe()
        {
            Orbit o = vessel.orbit;
            CelestialBody body = o.referenceBody;
            PQS pqs = body.pqsController;
            double maxTerrainHeight = pqs.radiusMax - pqs.radius;
            minSafeAltitude = Math.Max(maxTerrainHeight, body.atmosphereDepth);

            return (o.PeA < minSafeAltitude && o.timeToPe < o.timeToAp) || o.ApA < minSafeAltitude;
        }

        private bool UnderTimeLimit(float timeLimit = 0)
        {
            if (timeLimit == 0)
                timeLimit = updateInterval;

            return Time.time - lastUpdate < timeLimit;
        }

        private bool NearIntercept(Vector3 relVel, float minRange, float maxRange)
        {
            float timeToKillVelocity = relVel.magnitude / maxAcceleration;

            float rotDistance = Vector3.Angle(vessel.ReferenceTransform.up, relVel.normalized * -1) * Mathf.Deg2Rad;
            float timeToRotate = SolveTime(rotDistance * 0.75f, maxAngularAcceleration.magnitude) / 0.75f;

            Vector3 toClosestApproach = ToClosestApproach(relVel, minRange);
            Vector3 toTarget = FromTo(vessel, targetVessel);

            // Return false if we aren't headed towards the target.
            float velToClosestApproach = Vector3.Dot(relVel, toTarget.normalized);
            if (velToClosestApproach < 10)
                return false;

            float timeToClosestApproach = AIUtils.TimeToCPA(toClosestApproach, relVel * -1, Vector3.zero, 9999);
            if (timeToClosestApproach == 0)
                return false;

            nearInterceptBurnTime = timeToKillVelocity + timeToRotate;
            nearInterceptApproachTime = timeToClosestApproach;

            return timeToClosestApproach < (timeToKillVelocity + timeToRotate);
        }

        private bool CanInterceptShip(Vessel target)
        {
            bool canIntercept = false;

            // Is it worth us chasing a withdrawing ship?
            BDModuleOrbitalAI targetAI = target.FindPartModuleImplementing<BDModuleOrbitalAI>();

            if (targetAI)
            {
                Vector3 toTarget = target.CoM - vessel.CoM;
                bool escaping = targetAI.currentStatus.Contains("Withdraw"); // || targetAI.currentStatus.Contains("Idle (Unarmed)");

                canIntercept = !escaping || // It is not trying to escape.
                    toTarget.magnitude < weaponManager.gunRange || // It is already in range.
                    maxAcceleration > targetAI.maxAcceleration || // We are faster.
                    Vector3.Dot(target.GetObtVelocity() - vessel.GetObtVelocity(), toTarget) < 0; // It is getting closer.
            }
            return canIntercept;
        }

        private bool AwayCheck(float minRange)
        {
            // Check if we need to manually burn away from an enemy that's too close or
            // if it would be better to drift away.

            Vector3 toTarget = FromTo(vessel, targetVessel);
            Vector3 toEscape = toTarget.normalized * -1;
            Vector3 relVel = targetVessel.GetObtVelocity() - vessel.GetObtVelocity();

            float rotDistance = Vector3.Angle(vessel.ReferenceTransform.up, toEscape) * Mathf.Deg2Rad;
            float timeToRotate = SolveTime(rotDistance / 2, maxAngularAcceleration.magnitude) * 2;
            float timeToDisplace = SolveTime(minRange - toTarget.magnitude, maxAcceleration, Vector3.Dot(relVel * -1, toEscape));
            float timeToEscape = timeToRotate * 2 + timeToDisplace;

            Vector3 drift = AIUtils.PredictPosition(toTarget, relVel, Vector3.zero, timeToEscape);
            bool manualEscape = drift.magnitude < minRange;

            return manualEscape;
        }

        private Vector3 ToClosestApproach(Vector3 toTarget, Vector3 relVel, float minRange)
        {
            Vector3 relVelInverse = targetVessel.GetObtVelocity() - vessel.GetObtVelocity();
            float timeToIntercept = AIUtils.TimeToCPA(toTarget, relVelInverse, Vector3.zero, 9999);

            // Minimising the target closest approach to the current closest approach prevents
            // ships that are targeting each other from fighting over the closest approach based on their min ranges.
            // todo: allow for trajectory fighting if fuel is high.
            Vector3 actualClosestApproach = toTarget + Displacement(relVelInverse, Vector3.zero, timeToIntercept);
            float actualClosestApproachDistance = actualClosestApproach.magnitude;

            // Get a position that is laterally offset from the target by our desired closest approach distance.
            Vector3 rotatedVector = Vector3.ProjectOnPlane(relVel, toTarget.normalized).normalized;

            // Lead if the target is accelerating away from us.
            if (Vector3.Dot(targetVessel.acceleration.normalized, toTarget.normalized) > 0)
                toTarget += Displacement(Vector3.zero, toTarget.normalized * Vector3.Dot(targetVessel.acceleration, toTarget.normalized), Mathf.Min(timeToIntercept, 999));

            Vector3 toClosestApproach = toTarget + (rotatedVector * Mathf.Clamp(actualClosestApproachDistance, minRange, toTarget.magnitude));

            // Need a maximum angle so that we don't end up going further away at close range.
            toClosestApproach = Vector3.RotateTowards(toTarget, toClosestApproach, 22.5f, float.MaxValue);

            return toClosestApproach;
        }

        private Vector3 ToClosestApproach(Vector3 relVel, float minRange)
        {
            Vector3 toTarget = FromTo(vessel, targetVessel);
            return ToClosestApproach(toTarget, relVel, minRange);
        }

        internal void RestoreReferenceTransform()
        {
            vessel.SetReferenceTransform(originalReferenceTransform);
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

        public static bool OnTarget(Vector3 targetAim, Vector3 currentAim, Vector3 relativePosition, float targetSize, float tolerance)
        {
            // Scale the accuracy requirement (in degrees) based on the distance and size of the target.
            Vector3 targetRadius = Vector3.ProjectOnPlane(Vector3.up, relativePosition.normalized).normalized * (targetSize / 2) * tolerance;
            float aimTolerance = Vector3.Angle(relativePosition, relativePosition + targetRadius);

            return Vector3.Angle(targetAim.normalized, currentAim) < aimTolerance;
        }

        public static float AngularVelocity(Vessel v, Vessel t)
        {
            Vector3 tv1 = FromTo(v, t);
            Vector3 tv2 = tv1 + RelVel(v, t);
            return Vector3.Angle(tv1.normalized, tv2.normalized);
        }

        public static float Integrate(float d, float a, float i = 0.1f, float v = 0)
        {
            float t = 0;

            while (d > 0)
            {
                v = v + a * i;
                d = d - v * i;
                t = t + i;
            }

            return t;
        }

        public static float SolveTime(float distance, float acceleration, float vel = 0)
        {
            float a = 0.5f * acceleration;
            float b = vel;
            float c = Mathf.Abs(distance) * -1;

            float x = (-b + Mathf.Sqrt(b * b - 4 * a * c)) / (2 * a);

            return x;
        }

        public static float SolveDistance(float time, float acceleration, float vel = 0)
        {
            return (vel * time) + 0.5f * acceleration * Mathf.Pow(time, 2);
        }

        public static Vector3 TargetLead(Vessel target, Part firer, float travelVelocity)
        {
            Vector3 relPos = target.CoM - firer.transform.position;
            Vector3 relVel = target.GetObtVelocity() - firer.vessel.GetObtVelocity();
            Vector3 relAcc = target.acceleration - firer.vessel.acceleration;

            float timeToHit = AIUtils.TimeToCPA(relPos, relVel + (relPos.normalized * travelVelocity * -1), relAcc, 60);
            Vector3 leadPosition = AIUtils.PredictPosition(relPos, relVel, relAcc, timeToHit);

            return leadPosition;
        }

        public static float VesselDistance(Vessel v1, Vessel v2)
        {
            return (v1.transform.position - v2.transform.position).magnitude;
        }

        public static bool RayIntersectsVessel(Vessel v, Ray r, Color color = default)
        {
            RaycastHit hitInfo;

            foreach (Part p in v.parts)
            {
                foreach (Collider c in p.GetPartColliders())
                {
                    if (c.Raycast(r, out hitInfo, 50f))
                        return true;
                }
            }

            return false;
        }

        public static bool CylinderIntersectsVessel(Vessel v, Ray r, float radius, int sides = 4)
        {
            Ray edgeRay = new Ray(r.origin, r.direction);
            Vector3 cylinderEdge = Vector3.ProjectOnPlane(Vector3.up, r.direction).normalized * radius;
            RaycastHit hitInfo;

            for (int i = 0; i < sides; i++)
            {
                edgeRay.origin = r.origin + (Quaternion.AngleAxis(360f * (i / (float)sides), r.direction) * cylinderEdge);

                foreach (Part p in v.parts)
                {
                    foreach (Collider c in p.GetPartColliders())
                    {
                        if (c.Raycast(edgeRay, out hitInfo, 50f))
                            return true;
                    }
                }
            }

            return false;
        }

        public static Vector3 Displacement(Vector3 velocity, Vector3 acceleration, float time)
        {
            return velocity * time + 0.5f * acceleration * Mathf.Pow(time, 2);
        }

        private IEnumerator CalculateMaxAcceleration()
        {
            while (vessel.MOI == Vector3.zero)
            {
                yield return new WaitForSeconds(1);
            }

            Vector3 availableTorque = Vector3.zero;
            var reactionWheels = vessel.FindPartModulesImplementing<ModuleReactionWheel>();
            foreach (var wheel in reactionWheels)
            {
                wheel.GetPotentialTorque(out Vector3 pos, out pos);
                availableTorque += pos;
            }

            maxAngularAcceleration = AngularAcceleration(availableTorque, vessel.MOI);
        }

        public static float GetMaxAcceleration(Vessel v)
        {
            return GetMaxThrust(v) / v.GetTotalMass();
        }

        public static float GetMaxThrust(Vessel v)
        {
            List<ModuleEngines> engines = v.FindPartModulesImplementing<ModuleEngines>();
            engines.RemoveAll(e => !e.EngineIgnited || !e.isOperational);
            float thrust = engines.Sum(e => e.MaxThrustOutputVac(true));

            List<ModuleRCSFX> RCS = v.FindPartModulesImplementing<ModuleRCSFX>();
            foreach (ModuleRCS thruster in RCS)
            {
                if (thruster.useThrottle)
                    thrust += thruster.thrusterPower;
            }

            return engines.Sum(e => e.MaxThrustOutputVac(true));
        }
        #endregion

        #region Autopilot helper functions

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

        public override bool IsValidFixedWeaponTarget(Vessel target)
        {
            if (!vessel) return false;

            return true;
        }

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

        #endregion Autopilot helper functions

        #region WingCommander

        Vector3 GetFormationPosition()
        {
            return commandLeader.vessel.CoM + Quaternion.LookRotation(commandLeader.vessel.up, upDir) * this.GetLocalFormationPosition(commandFollowIndex);
        }

        #endregion WingCommander
    }
}

public class BDOrbitalControl : MonoBehaviour //: PartModule
{

    // /////////////////////////////////////////////////////
    public Vessel vessel;
    public Vector3 attitude = Vector3.zero;
    private Vector3 attitudeLerped;
    private float error;
    private float angleLerp;
    public bool lerpAttitude = true;
    private float lerpRate;
    private bool lockAttitude = false;

    private bool facingDesiredRotation;
    public float throttle;
    public float throttleActual;
    internal float throttleLerped;
    public float throttleLerpRate = 1;
    public float alignmentToleranceforBurn = 5;

    public Vector3 RCSVector;
    public float RCSPower = 3f;
    private Vector3 RCSThrust;
    private Vector3 up, right, forward;
    private float RCSThrottle;
    private Vector3 RCSVectorLerped = Vector3.zero;

    //[KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "ToggleAC")]

    public void Activate()
    {
        vessel.OnFlyByWire -= OrbitalControl;
        vessel.OnFlyByWire += OrbitalControl;
    }

    public void Deactivate()
    {
        vessel.OnFlyByWire -= OrbitalControl;
    }

    void OrbitalControl(FlightCtrlState s)
    {
        error = Vector3.Angle(vessel.ReferenceTransform.up, attitude);

        UpdateSAS(s);
        UpdateThrottle(s);
        UpdateRCS(s);
    }

    private void UpdateThrottle(FlightCtrlState s)
    {
        //if (throttle == 0 && throttleLerped == 0) return;
        //if (v == null) return;

        facingDesiredRotation = error < alignmentToleranceforBurn;
        throttleActual = facingDesiredRotation ? throttle : 0;

        // Move actual throttle towards throttle target gradually.
        throttleLerped = Mathf.MoveTowards(throttleLerped, throttleActual, throttleLerpRate * Time.fixedDeltaTime);

        s.mainThrottle = throttleLerped;
        //if (FlightGlobals.ActiveVessel != null && v == FlightGlobals.ActiveVessel)
        //    FlightInputHandler.state.mainThrottle = throttleLerped; //so that the on-screen throttle gauge reflects the autopilot throttle
    }

    void UpdateRCS(FlightCtrlState s)
    {
        if (RCSVector == Vector3.zero) return;

        if (RCSVectorLerped == Vector3.zero)
            RCSVectorLerped = RCSVector;

        // This system works for now but it's convuluted and isn't very stable.
        // todo: redo all of this.
        RCSVectorLerped = Vector3.Lerp(RCSVectorLerped, RCSVector, 5f * Time.fixedDeltaTime * Mathf.Clamp01(RCSVectorLerped.magnitude / RCSPower));
        RCSThrottle = Mathf.Lerp(0, 1.732f, Mathf.InverseLerp(0, RCSPower, RCSVectorLerped.magnitude));
        RCSThrust = RCSVectorLerped.normalized * RCSThrottle;

        up = vessel.ReferenceTransform.forward * -1;
        forward = vessel.ReferenceTransform.up * -1;
        right = Vector3.Cross(up, forward);

        s.X = Mathf.Clamp(Vector3.Dot(RCSThrust, right), -1, 1);
        s.Y = Mathf.Clamp(Vector3.Dot(RCSThrust, up), -1, 1);
        s.Z = Mathf.Clamp(Vector3.Dot(RCSThrust, forward), -1, 1);

        //Vector3 origin = v.ReferenceTransform.position;
        //KCSDebug.PlotLine(new[] { origin, origin + right * 10 * v.ctrlState.X }, rright);
        //KCSDebug.PlotLine(new[] { origin, origin + up * 10 * v.ctrlState.Y }, rup);
        //KCSDebug.PlotLine(new[] { origin, origin + forward * 10 * v.ctrlState.Z }, rforward);
    }

    void UpdateSAS(FlightCtrlState s)
    {
        if (attitude == Vector3.zero || lockAttitude) return;
        //if (v == null) return;

        // SAS must be turned off. Don't know why.
        if (vessel.ActionGroups[KSPActionGroup.SAS])
            vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);

        var ap = vessel.Autopilot;
        if (ap == null) return;

        // The offline SAS must not be on stability assist. Normal seems to work on most probes.
        if (ap.Mode != VesselAutopilot.AutopilotMode.Normal)
            ap.SetMode(VesselAutopilot.AutopilotMode.Normal);

        // Lerp attitude while burning to reduce instability.
        if (lerpAttitude)
        {
            angleLerp = Mathf.InverseLerp(0, 10, error);
            lerpRate = Mathf.Lerp(1, 10, angleLerp);
            attitudeLerped = Vector3.Lerp(attitudeLerped, attitude, lerpRate * Time.deltaTime);
        }

        ap.SAS.SetTargetOrientation(throttleLerped > 0 && lerpAttitude ? attitudeLerped : attitude, false);
    }

    public void Stability(bool enable)
    {
        lockAttitude = enable;

        var ap = vessel.Autopilot;
        if (ap == null) return;

        vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, enable);
        ap.SetMode(enable ? VesselAutopilot.AutopilotMode.StabilityAssist : VesselAutopilot.AutopilotMode.Normal);
    }

}