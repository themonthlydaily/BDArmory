using BDArmory.Control;
using BDArmory.Core;
using BDArmory.Core.Extension;
using BDArmory.Misc;
using BDArmory.Modules;
using BDArmory.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BDArmory.FX
{
    public class NukeFX : MonoBehaviour
    {
        public static Dictionary<string, ObjectPool> nukePool = new Dictionary<string, ObjectPool>();

        private bool hasDetonated = false;
        private float startTime;
        float yieldCubeRoot;
        private float lastValidAtmDensity = 0f;

        HashSet<Part> partsHit = new HashSet<Part>();
        public Light LightFx { get; set; }
        public float StartTime { get; set; }
        public AudioClip ExSound { get; set; }
        public AudioSource audioSource { get; set; }
        public float thermalRadius { get; set; } //clamped blast range
        public float fluence { get; set; } //thermal magnitude
        public float detonationTimer { get; set; } //seconds to delay before detonation
        public bool isEMP { get; set; } //do EMP effects?
        private float MaxTime { get; set; }
        public ExplosionSourceType ExplosionSource { get; set; }
        public string SourceVesselName { get; set; }
        public string ReportingName { get; set; }
        public float yield { get; set; } //kilotons
        public Vector3 Position { get; set; }
        public Part ExplosivePart { get; set; }
        public float TimeIndex => Time.time - StartTime;
        public string flashModelPath { get; set; }
        public string shockModelPath { get; set; }
        public string blastModelPath { get; set; }
        public string plumeModelPath { get; set; }
        public string debrisModelPath { get; set; }
        public string blastSoundPath { get; set; }

        public string explModelPath = "BDArmory/Models/explosion/explosion";

        public string explSoundPath = "BDArmory/Sounds/explode1";

        Queue<NukeHitEvent> explosionEvents = new Queue<NukeHitEvent>();
        List<NukeHitEvent> explosionEventsPreProcessing = new List<NukeHitEvent>();
        List<Part> explosionEventsPartsAdded = new List<Part>();
        List<DestructibleBuilding> explosionEventsBuildingAdded = new List<DestructibleBuilding>();
        Dictionary<string, int> explosionEventsVesselsHit = new Dictionary<string, int>();

        private float EMPRadius = 100;

        static RaycastHit[] lineOfSightHits;
        static RaycastHit[] reverseHits;
        static Collider[] overlapSphereColliders;
        public static List<Part> IgnoreParts;
        public static List<DestructibleBuilding> IgnoreBuildings;
        internal static readonly float ExplosionVelocity = 422.75f;

        internal static HashSet<ExplosionSourceType> ignoreCasingFor = new HashSet<ExplosionSourceType> { ExplosionSourceType.Missile, ExplosionSourceType.Rocket };

        void Awake()
        {
            if (lineOfSightHits == null) { lineOfSightHits = new RaycastHit[100]; }
            if (reverseHits == null) { reverseHits = new RaycastHit[100]; }
            if (overlapSphereColliders == null) { overlapSphereColliders = new Collider[100]; }
            if (IgnoreParts == null) { IgnoreParts = new List<Part>(); }
            if (IgnoreBuildings == null) { IgnoreBuildings = new List<DestructibleBuilding>(); }
        }

        private void OnEnable()
        {
            StartTime = Time.time;
            MaxTime = Mathf.Sqrt((thermalRadius / ExplosionVelocity) * 3f) * 2f; // Scale MaxTime to get a reasonable visualisation of the explosion.

            if (BDArmorySettings.DRAW_DEBUG_LABELS)
            {
                Debug.Log("[BDArmory.NukeFX]: Explosion started tntMass: {" + yield + "}  BlastRadius: {" + thermalRadius + "} StartTime: {" + StartTime + "}, Duration: {" + MaxTime + "}");
            }
            if (HighLogic.LoadedSceneIsFlight)
            {
                yieldCubeRoot = Mathf.Pow(yield, 1f / 3f);
                startTime = Time.time;
                if (FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(transform.position),
                                   FlightGlobals.getExternalTemperature(transform.position)) > 0)
                    lastValidAtmDensity = (float)FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(transform.position),
                                       FlightGlobals.getExternalTemperature(transform.position));
                hasDetonated = false;
            }
            //EMP output increases as the sqrt of yield (determined power) and prompt gamma output (~0.5% of yield) 
            //srf detonation is capped to about 16km, < 10km alt electrons qucikly absorbed by atmo.
            //above 10km, emp radius can easily reach 100s of km. But that's no fun, so...
            if (FlightGlobals.getAltitudeAtPos(transform.position) < 10000)
            {
                EMPRadius = Mathf.Sqrt(yield) * 100;
            }
            else
            {
                EMPRadius = Mathf.Sqrt(yield) * 1000;
            }
        }

        void OnDisable()
        {
            ExplosivePart = null; // Clear the Part reference.
            explosionEvents.Clear(); // Make sure we don't have any left over events leaking memory.
            explosionEventsPreProcessing.Clear();
            explosionEventsPartsAdded.Clear();
            explosionEventsBuildingAdded.Clear();
            explosionEventsVesselsHit.Clear();

        }

        private void CalculateBlastEvents()
        {
            using (var enuEvents = ProcessingBlastSphere().OrderBy(e => e.TimeToImpact).GetEnumerator())
            {
                while (enuEvents.MoveNext())
                {
                    if (enuEvents.Current == null) continue;

                    if (BDArmorySettings.DRAW_DEBUG_LABELS)
                    {
                        Debug.Log("[BDArmory.ExplosionFX]: Enqueueing Blast Event");
                    }
                    explosionEvents.Enqueue(enuEvents.Current);
                }
            }
        }

        private List<NukeHitEvent> ProcessingBlastSphere()
        {
            explosionEventsPreProcessing.Clear();
            explosionEventsPartsAdded.Clear();
            explosionEventsBuildingAdded.Clear();
            explosionEventsVesselsHit.Clear();

            using (var hitCollidersEnu = Physics.OverlapSphere(Position, thermalRadius, 9076737).AsEnumerable().GetEnumerator())
            {
                while (hitCollidersEnu.MoveNext())
                {
                    if (hitCollidersEnu.Current == null) continue;
                    try
                    {
                        Part partHit = hitCollidersEnu.Current.GetComponentInParent<Part>();
                        if (partHit != null)
                        {
                            if (ProjectileUtils.IsIgnoredPart(partHit)) continue; // Ignore ignored parts.
                            if (partHit.mass > 0 && !explosionEventsPartsAdded.Contains(partHit))
                            {
                                var damaged = ProcessPartEvent(partHit, SourceVesselName, explosionEventsPreProcessing, explosionEventsPartsAdded);
                                // If the explosion derives from a missile explosion, count the parts damaged for missile hit scores.
                                if (damaged && BDACompetitionMode.Instance)
                                {
                                    bool registered = false;
                                    var damagedVesselName = partHit.vessel != null ? partHit.vessel.GetName() : null;
                                    switch (ExplosionSource)
                                    {
                                        case ExplosionSourceType.Missile:
                                            if (BDACompetitionMode.Instance.Scores.RegisterMissileHit(SourceVesselName, damagedVesselName, 1))
                                                registered = true;
                                            break;
                                    }
                                    if (registered)
                                    {
                                        if (explosionEventsVesselsHit.ContainsKey(damagedVesselName))
                                            ++explosionEventsVesselsHit[damagedVesselName];
                                        else
                                            explosionEventsVesselsHit[damagedVesselName] = 1;
                                    }
                                }
                            }
                        }
                        else
                        {
                            DestructibleBuilding building = hitCollidersEnu.Current.GetComponentInParent<DestructibleBuilding>();
                            if (building != null)
                            {
                                if (!explosionEventsBuildingAdded.Contains(building))
                                {
                                    //ProcessBuildingEvent(building, explosionEventsPreProcessing, explosionEventsBuildingAdded);
                                    Ray ray = new Ray(Position, building.transform.position - Position);
                                    var distance = Vector3.Distance(building.transform.position, Position);
                                    RaycastHit rayHit;
                                    if (Physics.Raycast(ray, out rayHit, thermalRadius, 9076737))
                                    {
                                        DestructibleBuilding destructibleBuilding = rayHit.collider.gameObject.GetComponentUpwards<DestructibleBuilding>();

                                        distance = Vector3.Distance(Position, rayHit.point);
                                        if (building.IsIntact)
                                        {
                                            explosionEventsPreProcessing.Add(new BuildingNukeHitEvent() { Distance = distance, Building = building, TimeToImpact = distance / ExplosionVelocity });
                                            explosionEventsBuildingAdded.Add(building);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }
            if (explosionEventsVesselsHit.Count > 0)
            {
                if (ExplosionSource != ExplosionSourceType.Bullet || ExplosionSource != ExplosionSourceType.Rocket)
                {
                    string message = "";
                    foreach (var vesselName in explosionEventsVesselsHit.Keys)
                        message += (message == "" ? "" : " and ") + vesselName + " had " + explosionEventsVesselsHit[vesselName];
                    if (ExplosionSource == ExplosionSourceType.Missile)
                    {
                        message += " parts damaged due to missile strike";
                    }
                    else //ExplosionType BattleDamage || Other
                    {
                        message += " parts damaged due to explosion";
                    }
                    message += (ReportingName != null ? " (" + ReportingName + ")" : "") + (SourceVesselName != null ? " from " + SourceVesselName : "") + ".";
                    BDACompetitionMode.Instance.competitionStatus.Add(message);
                }
                // Note: damage hasn't actually been applied to the parts yet, just assigned as events, so we can't know if they survived.
                foreach (var vesselName in explosionEventsVesselsHit.Keys) // Note: sourceVesselName is already checked for being in the competition before damagedVesselName is added to explosionEventsVesselsHitByMissiles, so we don't need to check it here.
                {
                    switch (ExplosionSource)
                    {
                        case ExplosionSourceType.Missile:
                            BDACompetitionMode.Instance.Scores.RegisterMissileStrike(SourceVesselName, vesselName);
                            break;
                    }
                }
            }
            return explosionEventsPreProcessing;
        }

        private bool ProcessPartEvent(Part part, string sourceVesselName, List<NukeHitEvent> eventList, List<Part> partsAdded)
        {
            Ray LoSRay = new Ray(transform.position, part.transform.position - transform.position);
            RaycastHit hit;
            var distToG0 = Math.Max((transform.position - part.transform.position).magnitude, 1f);
            if (Physics.Raycast(LoSRay, out hit, distToG0, 9076737)) // only add impulse to parts with line of sight to detonation
            {
                KerbalEVA eva = hit.collider.gameObject.GetComponentUpwards<KerbalEVA>();
                Part p = eva ? eva.part : hit.collider.gameObject.GetComponentInParent<Part>();
                if (p == part)
                {
                    eventList.Add(new PartNukeHitEvent()
                    {
                        Distance = distToG0,
                        Part = part,
                        TimeToImpact = distToG0 / ExplosionVelocity,
                        HitPoint = hit.point,
                        Hit = hit,
                        SourceVesselName = sourceVesselName,
                    });

                    partsAdded.Add(part);
                    return true;
                }
                return false;
            }
            return false;
        }

        public void Update()
        {
            if (!gameObject.activeInHierarchy) return;

            if (HighLogic.LoadedSceneIsFlight)
            {
                if (Time.time - startTime > detonationTimer)
                {
                    if (!hasDetonated)
                    {
                        hasDetonated = true;
                        if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("{BDArmory.NukeFX] Beginning detonation");
                        CalculateBlastEvents();

                        LightFx = gameObject.GetComponent<Light>();
                        LightFx.range = thermalRadius * 3f;
                        float scale = Mathf.Sqrt(400 * (6 * yield)) / 219;
                        if (lastValidAtmDensity < 0.05)
                        {
                            FXEmitter.CreateFX(transform.position, scale, flashModelPath, "", 0.3f, 0.3f);
                        }
                        else
                        {
                            //default model scaled for 20kt; yield = 20 = scale of 1
                            //scaling calc is roughly SqRt( 400 * (6x))
                            FXEmitter.CreateFX(transform.position, scale, flashModelPath, blastSoundPath, 0.3f, -1, default, true);
                            FXEmitter.CreateFX(transform.position, scale * lastValidAtmDensity, shockModelPath, blastSoundPath, 0.3f, -1, default, true);
                            FXEmitter.CreateFX(transform.position, scale, blastModelPath, blastSoundPath, 1.5f, Mathf.Clamp(30 * scale, 30f, 90f), default, true);
                        }
                        if (Misc.Misc.GetRadarAltitudeAtPos(transform.position) < 200 * scale)
                        {
                            double latitudeAtPos = FlightGlobals.currentMainBody.GetLatitude(transform.position);
                            double longitudeAtPos = FlightGlobals.currentMainBody.GetLongitude(transform.position);
                            double altitude = FlightGlobals.currentMainBody.TerrainAltitude(latitudeAtPos, longitudeAtPos);

                            FXEmitter.CreateFX(FlightGlobals.currentMainBody.GetWorldSurfacePosition(latitudeAtPos, longitudeAtPos, altitude), scale, plumeModelPath, blastSoundPath, Mathf.Clamp(30 * scale, 30f, 90f), Mathf.Clamp(30 * scale, 30f, 90f), default, true, true);
                            FXEmitter.CreateFX(FlightGlobals.currentMainBody.GetWorldSurfacePosition(latitudeAtPos, longitudeAtPos, altitude), scale, debrisModelPath, blastSoundPath, 1.5f, Mathf.Clamp(30 * scale, 30f, 90f), default, true) ;
                        }
                    }
                    if (LightFx != null) LightFx.intensity -= 12 * Time.deltaTime;
                }
            }
        }

        public void FixedUpdate()
        {
            if (!gameObject.activeInHierarchy) return;

            //floating origin and velocity offloading corrections
            if (!FloatingOrigin.Offset.IsZero() || !Krakensbane.GetFrameVelocity().IsZero())
            {
                transform.position -= FloatingOrigin.OffsetNonKrakensbane;
            }

            if (hasDetonated)
            {
                while (explosionEvents.Count > 0 && explosionEvents.Peek().TimeToImpact <= TimeIndex)
                {
                    NukeHitEvent eventToExecute = explosionEvents.Dequeue();

                    var partBlastHitEvent = eventToExecute as PartNukeHitEvent;
                    if (partBlastHitEvent != null)
                    {
                        ExecutePartBlastEvent(partBlastHitEvent);
                    }
                    else
                    {
                        ExecuteBuildingBlastEvent((BuildingNukeHitEvent)eventToExecute);
                    }
                }
            }

            if (hasDetonated && explosionEvents.Count == 0 && TimeIndex > MaxTime)
            {
                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                {
                    Debug.Log("[BDArmory.NukeFX]: Explosion Finished");
                }
                gameObject.SetActive(false);
                return;
            }
        }

        private void ExecuteBuildingBlastEvent(BuildingNukeHitEvent eventToExecute)
        {
            DestructibleBuilding building = eventToExecute.Building;
            //Debug.Log("[BDArmory.NukeFX] Beginning building hit");
            if (building && building.IsIntact)
            {
                var distToEpicenter = Mathf.Max((transform.position - building.transform.position).magnitude, 1f);
                var blastImpulse = Mathf.Pow(3.01f * 1100f / distToEpicenter, 1.25f) * 6.894f * lastValidAtmDensity * yieldCubeRoot;
                //Debug.Log("[BDArmory.NukeFX]: Building hit; distToG0: " + distToEpicenter + ", yield: " + yield + ", building: " + building.name);

                if (!double.IsNaN(blastImpulse)) //140kPa, level at which reinforced concrete structures are destroyed
                {
                    //Debug.Log("[BDArmory.NukeFX]: Building Impulse: " + blastImpulse);
                    if (blastImpulse > 140)
                    {
                        building.Demolish();
                    }
                }
            }
        }

        private void ExecutePartBlastEvent(PartNukeHitEvent eventToExecute)
        {
            if (eventToExecute.Part == null || eventToExecute.Part.Rigidbody == null || eventToExecute.Part.vessel == null || eventToExecute.Part.partInfo == null) return;

            Part part = eventToExecute.Part;
            Rigidbody rb = part.Rigidbody;
            var realDistance = eventToExecute.Distance;
            var vesselMass = part.vessel.totalMass;
            if (vesselMass == 0) vesselMass = part.mass; // Sometimes if the root part is the only part of the vessel, then part.vessel.totalMass is 0, despite the part.mass not being 0.
            float radiativeArea = !double.IsNaN(part.radiativeArea) ? (float)part.radiativeArea : part.GetArea();
            if (!eventToExecute.IsNegativePressure)
            {
                if (BDArmorySettings.DRAW_DEBUG_LABELS && double.IsNaN(part.radiativeArea))
                {
                    Debug.Log("[BDArmory.NukeFX]: radiative area of part " + part + " was NaN, using approximate area " + radiativeArea + " instead.");
                }
                //part.skinTemperature += fluence * 3370000000 / (4 * Math.PI * (realDistance * realDistance)) * radiativeArea / 2; // Fluence scales linearly w/ yield, 1 Kt will produce between 33 TJ and 337 kJ at 0-1000m,
                part.skinTemperature += fluence * (337000000 * BDArmorySettings.EXP_DMG_MOD_MISSILE) / (4 * Math.PI * (realDistance * realDistance)); // everything gets heated via atmosphere                                                                                                                                  
                if (isEMP)
                {
                    if (part == part.vessel.rootPart) //don't apply EMP buildup per part
                    {
                        var EMP = part.vessel.rootPart.FindModuleImplementing<ModuleDrainEC>();
                        if (EMP == null)
                        {
                            EMP = (ModuleDrainEC)part.vessel.rootPart.AddModule("ModuleDrainEC");
                        }
						EMP.incomingDamage = ((EMPRadius / realDistance) * 100); //this way craft at edge of blast might only get disabled instead of bricked
                        //work on a better EMP damage value, in case of configs with very large thermalRadius
                        EMP.softEMP = false;
                    }
                }
                double blastImpulse = Mathf.Pow(3.01f * 1100f / realDistance, 1.25f) * 6.894f * lastValidAtmDensity * yieldCubeRoot; // * (radiativeArea / 3f); pascals/m isn't going to increase if a larger surface area, it's still going go be same force
                if (blastImpulse > 0)
                {
                    if (rb != null && rb.mass > 0)
                    {
                        if (double.IsNaN(blastImpulse))
                        {
                            Debug.LogWarning("[BDArmory.NukeFX]: blast impulse is NaN. distToG0: " + realDistance + ", vessel: " + part.vessel + ", atmDensity: " + lastValidAtmDensity + ", yield^(1/3): " + yieldCubeRoot + ", partHit: " + part + ", radiativeArea: " + radiativeArea);
                        }
                        else
                        {
                            if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.NukeTest]: Applying " + blastImpulse.ToString("0.0") + " impulse to " + part + " of mass " + part.mass + " at distance " + realDistance + "m");
                            part.rb.AddForceAtPosition((part.transform.position - transform.position).normalized * ((float)blastImpulse * (radiativeArea / 3f)), part.transform.position, ForceMode.Impulse);
                        }
                    }
                    // Add Reverse Negative Event
                    explosionEvents.Enqueue(new PartNukeHitEvent()
                    {
                        Distance = thermalRadius - realDistance,
                        Part = part,
                        TimeToImpact = 2 * (thermalRadius / ExplosionVelocity) + (thermalRadius - realDistance) / ExplosionVelocity,
                        IsNegativePressure = true,
                        NegativeForce = (float)blastImpulse * 0.25f
                    });
                    float damage = 0;
                    //float blastDamage = ((float)((yield * (45000000 * BDArmorySettings.EXP_DMG_MOD_MISSILE)) / (4f * Mathf.PI * realDistance * realDistance) * (radiativeArea / 2f)));
                    //this shouldn't scale linearly
                    float blastDamage = (float)blastImpulse; //* BDArmorySettings.EXP_DMG_MOD_MISSILE; //DMG_Mod is substantially increasing blast radius above what it should be
                    if (float.IsNaN(blastDamage))
                    {
                        Debug.LogWarning("[BDArmory.NukeFX]: blast damage is NaN. distToG0: " + realDistance + ", yield: " + yield + ", part: " + part + ", radiativeArea: " + radiativeArea);
                    }
                    else
                    {
                        if (!ProjectileUtils.CalculateExplosiveArmorDamage(part, blastImpulse, SourceVesselName, eventToExecute.Hit, ExplosionSource)) //false = armor blowthrough
                        {
                            damage = part.AddExplosiveDamage(blastDamage, 1, ExplosionSource, 1);
                        }
                        if (damage > 0) //else damage from spalling done in CalcExplArmorDamage
                        {
                            if (BDArmorySettings.BATTLEDAMAGE)
                            {
                                Misc.BattleDamageHandler.CheckDamageFX(part, 50, 0.5f, true, false, SourceVesselName, eventToExecute.Hit);
                            }
                            // Update scoring structures
                            var aName = eventToExecute.SourceVesselName; // Attacker
                            var tName = part.vessel.GetName(); // Target
                            switch (ExplosionSource)
                            {
                                case ExplosionSourceType.Missile:
                                    BDACompetitionMode.Instance.Scores.RegisterMissileDamage(aName, tName, damage);
                                    break;
                                case ExplosionSourceType.BattleDamage:
                                    BDACompetitionMode.Instance.Scores.RegisterBattleDamage(aName, part.vessel, damage);
                                    break;
                            }
                        }
                    }
                }
                else if (BDArmorySettings.DRAW_DEBUG_LABELS)
                {
                    Debug.Log("[BDArmory.NukeFX]: Part " + part.name + " at distance " + realDistance + "m took no damage");
                }
            }
            else
            {
                if (rb != null && rb.mass > 0)
                {
                    if (double.IsNaN(eventToExecute.NegativeForce))
                    {
                        Debug.LogWarning("[BDArmory.NukeFX]: blast impulse is NaN. distToG0: " + realDistance + ", vessel: " + part.vessel + ", atmDensity: " + lastValidAtmDensity + ", yield^(1/3): " + yieldCubeRoot + ", partHit: " + part + ", radiativeArea: " + radiativeArea);
                    }
                    else
                    {
                        if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.NukeTest]: Applying " + eventToExecute.NegativeForce.ToString("0.0") + " impulse to " + part + " of mass " + part.mass + " at distance " + realDistance + "m");
                        part.rb.AddForceAtPosition((Position - part.transform.position).normalized * eventToExecute.NegativeForce * BDArmorySettings.EXP_IMP_MOD * 0.25f, part.transform.position, ForceMode.Impulse);
                    }
                }
            }
        }

        // We use an ObjectPool for the ExplosionFx instances as they leak KSPParticleEmitters otherwise.
        static void SetupPool(string ModelPath, string soundPath)
        {
            var key = ModelPath + soundPath;
            if (!nukePool.ContainsKey(key) || nukePool[key] == null)
            {
                var templateFX = GameDatabase.Instance.GetModel(ModelPath);
                if (templateFX == null)
                {
                    Debug.LogError("[BDArmory.NukeFX]: " + ModelPath + " was not found, using the default explosion instead. Please fix your model.");
                    templateFX = GameDatabase.Instance.GetModel(ModuleWeapon.defaultExplModelPath);
                }
                var soundClip = GameDatabase.Instance.GetAudioClip(soundPath);
                if (soundClip == null)
                {
                    Debug.LogError("[BDArmory.ExplosionFX]: " + soundPath + " was not found, using the default sound instead. Please fix your model.");
                    soundClip = GameDatabase.Instance.GetAudioClip(ModuleWeapon.defaultExplSoundPath);
                }
                var eFx = templateFX.AddComponent<NukeFX>();
                eFx.ExSound = soundClip;
                eFx.audioSource = templateFX.AddComponent<AudioSource>();
                eFx.audioSource.minDistance = 200;
                eFx.audioSource.maxDistance = 5500;
                eFx.audioSource.spatialBlend = 1;
                eFx.LightFx = templateFX.AddComponent<Light>();
                eFx.LightFx.color = Misc.Misc.ParseColor255("255,238,184,255");
                eFx.LightFx.intensity = 8;
                eFx.LightFx.shadows = LightShadows.None;
                templateFX.SetActive(false);
                nukePool[key] = ObjectPool.CreateObjectPool(templateFX, 10, true, true, 0f, false);
            }
        }
        public static void CreateExplosion(Vector3 position, ExplosionSourceType explosionSourceType, string sourceVesselName, float delay = 2.5f, float blastRadius = 750, float Yield = 0.05f,
            float thermalShock = 0.05f, bool emp = true, string sourceWeaponName = "Nuke", string ModelPath = "BDArmory/Models/Mutators/NukeCore", string soundPath = "", string blastSound = "BDArmory/Models/explosion/nuke/nukeBoom",
            string flashModel = "BDArmory/Models/explosion/nuke/nukeFlash", string shockModel = "BDArmory/Models/explosion/nuke/nukeShock", string blastModel = "BDArmory/Models/explosion/nuke/nukeBlast", string plumeModel = "BDArmory/Models/explosion/nuke/nukePlume", string debrisModel = "BDArmory/Models/explosion/nuke/nukeScatter")
        {
            SetupPool(ModelPath, soundPath);

            Quaternion rotation;
            rotation = Quaternion.LookRotation(VectorUtils.GetUpDirection(position));
            GameObject newExplosion = nukePool[ModelPath + soundPath].GetPooledObject();
            NukeFX eFx = newExplosion.GetComponent<NukeFX>();
            newExplosion.transform.SetPositionAndRotation(position, rotation);

            eFx.Position = position;
            eFx.ExplosionSource = explosionSourceType;
            eFx.SourceVesselName = sourceVesselName;
            eFx.ReportingName = sourceWeaponName;
            eFx.explModelPath = ModelPath;
            eFx.explSoundPath = soundPath;
            eFx.thermalRadius = blastRadius;

            eFx.flashModelPath = flashModel;
            eFx.shockModelPath = shockModel;
            eFx.blastModelPath = blastModel;
            eFx.plumeModelPath = plumeModel;
            eFx.debrisModelPath = debrisModel;
            eFx.blastSoundPath = blastSound;

            eFx.yield = Yield;
            eFx.fluence = thermalShock;
            eFx.isEMP = emp;
            eFx.detonationTimer = delay;
            newExplosion.SetActive(true);
            eFx.audioSource = newExplosion.GetComponent<AudioSource>();
            newExplosion.SetActive(true);
        }
    }

    public abstract class NukeHitEvent
    {
        public float Distance { get; set; }
        public float TimeToImpact { get; set; }
        public bool IsNegativePressure { get; set; }
    }

    internal class PartNukeHitEvent : NukeHitEvent
    {
        public Part Part { get; set; }
        public Vector3 HitPoint { get; set; }
        public RaycastHit Hit { get; set; }
        public float NegativeForce { get; set; }
        public string SourceVesselName { get; set; }
    }

    internal class BuildingNukeHitEvent : NukeHitEvent
    {
        public DestructibleBuilding Building { get; set; }
    }
}

