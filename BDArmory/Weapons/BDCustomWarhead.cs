using KSP.Localization;
using System.Linq;
using UnityEngine;

using BDArmory.Extensions;
using BDArmory.Settings;
using BDArmory.Utils;
using BDArmory.Bullets;
using static BDArmory.Bullets.PooledBullet;
using static BDArmory.Weapons.ModuleWeapon;

namespace BDArmory.Weapons
{
    public class BDCustomWarhead : BDWarheadBase
    {
        [KSPField]
        public string warheadType = "def";
        public string warheadReportingName;
        public BulletInfo _warheadType;

        [KSPField]
        public string explModelPath = "BDArmory/Models/explosion/explosion";

        [KSPField]
        public string explSoundPath = "BDArmory/Sounds/explode1";

        [KSPField]
        public string smokeTexturePath = "";

        [KSPField]
        public string bulletTexturePath = "BDArmory/Textures/bullet";

        [KSPField]
        public float maxDeviation = 1;

        public void ParseWarheadType()
        {
            _warheadType = BulletInfo.bullets[warheadType];
            if (_warheadType.DisplayName != "Default Bullet")
                warheadReportingName = _warheadType.DisplayName;
            else
                warheadReportingName = _warheadType.name;
        }

        private void FireProjectile(float detRange = -1, float detTime = -1)
        {
            if (bulletPool == null)
            {
                GameObject templateBullet = new GameObject("Bullet");
                templateBullet.AddComponent<PooledBullet>();
                templateBullet.SetActive(false);
                bulletPool = ObjectPool.CreateObjectPool(templateBullet, 100, true, true);
            }

            for (int i = 0; i < _warheadType.projectileCount; i++)
            {
                GameObject firedBullet = bulletPool.GetPooledObject();
                PooledBullet pBullet = firedBullet.GetComponent<PooledBullet>();
                if (_warheadType.tntMass > 0)
                {
                    switch (_warheadType.explosive.ToLower())
                    {
                        case "standard":
                            pBullet.HEType = PooledBulletTypes.Explosive;
                            break;
                        //legacy support for older configs that are still explosive = true
                        case "true":
                            pBullet.HEType = PooledBulletTypes.Explosive;
                            break;
                        case "shaped":
                            pBullet.HEType = PooledBulletTypes.Shaped;
                            break;
                        default:
                            pBullet.HEType = PooledBulletTypes.Slug;
                            break;
                    }
                }
                else
                {
                    pBullet.HEType = PooledBulletTypes.Slug;
                }

                pBullet.currentPosition = transform.position;

                pBullet.caliber = _warheadType.caliber;
                pBullet.bulletVelocity = _warheadType.bulletVelocity;
                pBullet.bulletMass = _warheadType.bulletMass;
                pBullet.incendiary = _warheadType.incendiary;
                pBullet.apBulletMod = _warheadType.apBulletMod;
                pBullet.bulletDmgMult = 1f;

                //A = ? x (D / 2)^2
                float bulletDragArea = Mathf.PI * 0.25f * _warheadType.caliber * _warheadType.caliber;

                //Bc = m/Cd * A
                float bulletBallisticCoefficient = _warheadType.bulletMass / ((bulletDragArea / 1000000f) * 0.295f); // mm^2 to m^2

                //Bc = m/d^2 * i where i = 0.484
                //bulletBallisticCoefficient = bulletMass / Mathf.Pow(caliber / 1000, 2f) * 0.484f;

                pBullet.ballisticCoefficient = bulletBallisticCoefficient;

                pBullet.timeElapsedSinceCurrentSpeedWasAdjusted = TimeWarp.fixedDeltaTime;
                // measure bullet lifetime in time rather than in distance, because distances get very relative in orbit
                pBullet.timeToLiveUntil = _warheadType.projectileTTL + (detTime < 0.0f ? 0.0f : detTime) + Time.time;

                Vector3 firedVelocity = VectorUtils.GaussianDirectionDeviation(transform.forward, (maxDeviation / 2)) * _warheadType.bulletVelocity;
                pBullet.currentVelocity = part.rb.velocity + BDKrakensbane.FrameVelocityV3f + firedVelocity; // use the real velocity, w/o offloading

                pBullet.sourceWeapon = part;
                pBullet.sourceVessel = vessel;
                pBullet.team = Team.Name;
                pBullet.bulletTexturePath = "BDArmory/Textures/bullet";
                pBullet.projectileColor = GUIUtils.ParseColor255(_warheadType.projectileColor);
                pBullet.startColor = GUIUtils.ParseColor255(_warheadType.startColor);
                pBullet.fadeColor = _warheadType.fadeColor;
                pBullet.tracerStartWidth = _warheadType.caliber / 300;
                pBullet.tracerEndWidth = _warheadType.caliber / 750;
                pBullet.tracerLength = 0;
                pBullet.tracerLuminance = 1.75f;
                pBullet.tracerDeltaFactor = 2.65f;
                if (!string.IsNullOrEmpty(smokeTexturePath)) pBullet.smokeTexturePath = smokeTexturePath;
                pBullet.bulletDrop = true;

                if (_warheadType.tntMass > 0 || _warheadType.beehive)
                {
                    pBullet.explModelPath = explModelPath;
                    pBullet.explSoundPath = explSoundPath;
                    pBullet.tntMass = _warheadType.tntMass;
                    pBullet.detonationRange = detRange < 0 ? detonationRange : detRange;
                    pBullet.timeToDetonation = detTime < 0 ? detonationRange / Vector3.Magnitude(pBullet.currentVelocity) : detTime;
                    string fuzeTypeS = _warheadType.fuzeType.ToLower();
                    switch (fuzeTypeS)
                    {
                        //Anti-Air fuzes
                        case "timed":
                            pBullet.fuzeType = BulletFuzeTypes.Timed;
                            break;
                        case "proximity":
                            pBullet.fuzeType = BulletFuzeTypes.Proximity;
                            break;
                        case "flak":
                            pBullet.fuzeType = BulletFuzeTypes.Flak;
                            break;
                        //Anti-Armor fuzes
                        case "delay":
                            pBullet.fuzeType = BulletFuzeTypes.Delay;
                            break;
                        case "penetrating":
                            pBullet.fuzeType = BulletFuzeTypes.Penetrating;
                            break;
                        case "impact":
                            pBullet.fuzeType = BulletFuzeTypes.Impact;
                            break;
                        case "none":
                            pBullet.fuzeType = BulletFuzeTypes.Impact;
                            break;
                        default:
                            pBullet.fuzeType = BulletFuzeTypes.None;
                            break;
                    }
                }
                else
                {
                    pBullet.fuzeType = PooledBullet.BulletFuzeTypes.None;
                    pBullet.sabot = ((((_warheadType.bulletMass * 1000) / ((_warheadType.caliber * _warheadType.caliber * Mathf.PI / 400) * 19) + 1) * 10) > _warheadType.caliber * 4);
                }
                pBullet.EMP = _warheadType.EMP;
                pBullet.nuclear = _warheadType.nuclear;
                if (pBullet.nuclear) // Inherit the parent shell's nuke models.
                {
                    pBullet.flashModelPath = BDModuleNuke.defaultflashModelPath;
                    pBullet.shockModelPath = BDModuleNuke.defaultShockModelPath;
                    pBullet.blastModelPath = BDModuleNuke.defaultBlastModelPath;
                    pBullet.plumeModelPath = BDModuleNuke.defaultPlumeModelPath;
                    pBullet.debrisModelPath = BDModuleNuke.defaultDebrisModelPath;
                    pBullet.blastSoundPath = BDModuleNuke.defaultBlastSoundPath;
                }
                pBullet.beehive = _warheadType.beehive;
                if (_warheadType.beehive)
                {
                    pBullet.subMunitionType = _warheadType.subMunitionType;
                }
                //pBullet.homing = BulletInfo.homing;
                switch (_warheadType.bulletDragTypeName.ToLower())
                {
                    case "none":
                        pBullet.dragType = PooledBullet.BulletDragTypes.None;
                        break;

                    case "numericalintegration":
                        pBullet.dragType = PooledBullet.BulletDragTypes.NumericalIntegration;
                        break;

                    case "analyticestimate":
                        pBullet.dragType = PooledBullet.BulletDragTypes.AnalyticEstimate;
                        break;

                    default:
                        pBullet.dragType = PooledBullet.BulletDragTypes.AnalyticEstimate;
                        break;
                }

                pBullet.bullet = BulletInfo.bullets[warheadType];
                pBullet.stealResources = false;
                pBullet.dmgMult = 1f;
                pBullet.targetVessel = null;
                pBullet.guidanceDPS = 0;
                pBullet.isSubProjectile = true;
                pBullet.gameObject.SetActive(true);

                if (!pBullet.CheckBulletCollisions(TimeWarp.fixedDeltaTime)) // Check that the bullet won't immediately hit anything and die.
                {
                    // The following gets bullet tracers to line up properly when at orbital velocities.
                    // It should be consistent with how it's done in Aim().
                    // Technically, there could be a small gap between the collision check and the start position, but this should be insignificant.
                    if (!pBullet.hasRicocheted) // Movement is handled internally for ricochets.
                    {
                        var gravity = (Vector3)FlightGlobals.getGeeForceAtPosition(pBullet.currentPosition);
                        pBullet.currentPosition = AIUtils.PredictPosition(pBullet.currentPosition, firedVelocity, gravity, TimeWarp.fixedDeltaTime);
                        pBullet.currentVelocity += TimeWarp.fixedDeltaTime * gravity; // Adjusting the velocity here mostly eliminates bullet deviation due to iTime.
                        pBullet.DistanceTraveled += TimeWarp.fixedDeltaTime * _warheadType.bulletVelocity; // Adjust the distance traveled to account for iTime.
                    }
                    if (!BDKrakensbane.IsActive) pBullet.currentPosition += TimeWarp.fixedDeltaTime * part.rb.velocity; // If Krakensbane isn't active, bullets get an additional shift by this amount.
                    pBullet.timeAlive = TimeWarp.fixedDeltaTime;
                    pBullet.SetTracerPosition();
                    pBullet.currentPosition += TimeWarp.fixedDeltaTime * (part.rb.velocity + BDKrakensbane.FrameVelocityV3f); // Account for velocity off-loading after visuals are done.
                }
            }
        }

        protected override void WarheadSpecificSetup()
        {
            ParseWarheadType();
        }
        protected override void WarheadSpecificUISetup()
        {

        }

        public override void DetonateIfPossible()
        {
            if (!hasDetonated && Armed)
            {
                hasDetonated = true;

                if (fuseFailureRate > 0f)
                    if (Random.Range(0f, 1f) < fuseFailureRate)
                        fuseFailed = true;

                if (!fuseFailed)
                {
                    direction = part.partTransform.forward; //both the missileReferenceTransform and smallWarhead part's forward direction is Z+, or transform.forward.
                                                            // could also do warheadType == "standard" ? default: part.partTransform.forward, as this simplifies the isAngleAllowed check in ExplosionFX, but at the cost of standard heads always being 360deg blasts (but we don't have limited angle blasts for missiels at present anyway, so not a bit deal RN)
                                                            //var sourceWeapon = part.FindModuleImplementing<EngageableWeapon>();
                    FireProjectile();

                    ////////////////////////////////////////////////////
                    if (BDArmorySettings.DEBUG_MISSILES)
                        Debug.Log($"[BDArmory.BDCustomWarhead]: {part} ({(uint)(part.GetInstanceID())}) from {SourceVesselName} (Team:{Team.Name}) detonating with a {warheadType} warhead");
                    part.explode();
                }
                else
                    if (BDArmorySettings.DEBUG_MISSILES)
                        Debug.Log($"[BDArmory.BDCustomWarhead]: {part} ({(uint)(part.GetInstanceID())}) from {SourceVesselName} explosive fuse failed!");
            }
        }

        protected override void Detonate()
        {
            if (!hasDetonated && Armed)
            {
                hasDetonated = true;

                if (fuseFailureRate > 0f)
                    if (Random.Range(0f, 1f) < fuseFailureRate)
                        fuseFailed = true;

                if (!fuseFailed)
                {
                    direction = part.partTransform.forward;
                    FireProjectile();
                    if (BDArmorySettings.DEBUG_MISSILES)
                        Debug.Log($"[BDArmory.BDCustomWarhead]: {part} ({(uint)(part.GetInstanceID())}) from {SourceVesselName} detonating with a {warheadType} warhead");
                    /////////////////////////

                    part.Destroy();
                    part.explode();
                }
                else
                    if (BDArmorySettings.DEBUG_MISSILES)
                        Debug.Log($"[BDArmory.BDCustomWarhead]: {part} ({(uint)(part.GetInstanceID())}) from {SourceVesselName} explosive fuse failed!");
            }
        }
    }
}
