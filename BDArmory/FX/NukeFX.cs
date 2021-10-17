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

        public ExplosionSourceType ExplosionSource { get; set; }
        public Vector3 Position { get; set; }
        public Part ExplosivePart { get; set; }
        public string Sourcevessel { get; set; }
        public string reportingName { get; set; }
        public float yield { get; set; } //kilotons
        public float thermalRadius { get; set; } //clamped blast range
        public float fluence { get; set; } //thermal magnitude
        public float detonationTimer { get; set; } //seconds to delay before detonation
        public float tntmass { get; set; }
        public bool isEMP { get; set; } //do EMP effects?
        public string explModelPath { get; set; }
        public string explSoundPath { get; set; } //do EMP effects?

        public static string flashModelPath = "BDArmory/Models/explosion/nuke/nukeFlash";
        public static string shockModelPath = "BDArmory/Models/explosion/nuke/nukeShock";
        public static string blastModelPath = "BDArmory/Models/explosion/nuke/nukeBlast";
        public static string plumeModelPath = "BDArmory/Models/explosion/nuke/nukePlume";
        public static string debrisModelPath = "BDArmory/Models/explosion/nuke/nukeScatter";
        public static string blastSoundPath = "BDArmory/Models/explosion/nuke/nukeBoom";
        void OnEnable()
        {
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
        }
        void OnDisable()
        {
        }
        public void Update()
        {
            if (!gameObject.activeInHierarchy)
            {
                return;
            }
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (Time.time - startTime > detonationTimer)
                {
                    if (!hasDetonated)
                    {
                        Detonate();
                        if (lastValidAtmDensity < 0.05)
                        {
                            FXEmitter.CreateFX(transform.position, yield, flashModelPath, "", 0.3f, 0.3f);
                        }
                        else
                        {
                            FXEmitter.CreateFX(transform.position, yield/2, flashModelPath, ""); 
                            FXEmitter.CreateFX(transform.position, (yield/2) * lastValidAtmDensity, shockModelPath, "");
                            FXEmitter.CreateFX(transform.position, yield/2, blastModelPath, blastSoundPath, 1.5f); 
                        }
                        if (Misc.Misc.GetRadarAltitudeAtPos(transform.position) < (300 + (100 * (yield/2))))
                        {    
                            double latitudeAtPos = FlightGlobals.currentMainBody.GetLatitude(transform.position);
                            double longitudeAtPos = FlightGlobals.currentMainBody.GetLongitude(transform.position);
                            double altitude = FlightGlobals.currentMainBody.TerrainAltitude(latitudeAtPos, longitudeAtPos);
                            
                            FXEmitter.CreateFX(FlightGlobals.currentMainBody.GetWorldSurfacePosition(latitudeAtPos, longitudeAtPos, altitude), yield / 2, plumeModelPath, "", 30f);
                            FXEmitter.CreateFX(FlightGlobals.currentMainBody.GetWorldSurfacePosition(latitudeAtPos, longitudeAtPos, altitude), yield / 2, debrisModelPath, "", 1.5f);
                        }
                    }
                }
            }
        }

        void Detonate() //borrowed from Stockalike Project Orion //Need to add distance/timeToImpact considerations a la ExplosionFX, though I suspect that will involve cloning ~85% of its code
        {
            if (hasDetonated || FlightGlobals.currentMainBody == null)
            {
                return;
            }
            hasDetonated = true;
            if (BDArmorySettings.DRAW_DEBUG_LABELS)
                Debug.Log("[BDArmory.NukeFX]: initiating Nuke explosion");
            //affect any nearby parts/vessels that aren't the source vessel

            Dictionary<string, int> vesselsHitByMissiles = new Dictionary<string, int>();
            double blastImpulse = 1000;
            using (var blastHits = Physics.OverlapSphere(transform.position, thermalRadius, 9076737).AsEnumerable().GetEnumerator())
            {
                partsHit.Clear();
                while (blastHits.MoveNext())
                {
                    if (blastHits.Current == null) continue;
                    if (blastHits.Current.gameObject == FlightGlobals.currentMainBody.gameObject) continue; // Ignore terrain hits.
                    Part partHit = blastHits.Current.GetComponentInParent<Part>();
                    if (partHit == null) continue;
                    if (ProjectileUtils.IsIgnoredPart(partHit)) continue; // Ignore ignored parts.
                    if (partsHit.Contains(partHit)) continue; // Don't hit the same part multiple times.
                    partsHit.Add(partHit);
                    if (partHit != null && partHit.mass > 0)
                    {
                        var distToG0 = Math.Max((transform.position - partHit.transform.position).magnitude, 1f);
                        float radiativeArea = !double.IsNaN(partHit.radiativeArea) ? (float)partHit.radiativeArea : partHit.GetArea();
                        if (BDArmorySettings.DRAW_DEBUG_LABELS && double.IsNaN(partHit.radiativeArea))
                        {
                            Debug.Log("[BDArmory.NukeFX]: radiative area of part " + partHit + " was NaN, using approximate area " + radiativeArea + " instead.");
                        }
                        partHit.skinTemperature += fluence * 3370000000 / (4 * Math.PI * Math.Pow(distToG0, 2.0)) * radiativeArea / 2; // Fluence scales linearly w/ yield, 1 Kt will produce between 33 TJ and 337 kJ at 0-1000m,
                                                                                                                                       // everything gets heated via atmosphere
                        if (isEMP)
                        {
                            if (partHit != partHit.vessel.rootPart) continue; //don't apply EMP buildup per part
                            var EMP = partHit.vessel.rootPart.FindModuleImplementing<ModuleDrainEC>();
                            if (EMP == null)
                            {
                                EMP = (ModuleDrainEC)partHit.vessel.rootPart.AddModule("ModuleDrainEC");
                            }
                            EMP.incomingDamage = (((thermalRadius * 2) - distToG0) * 1); //this way craft at edge of blast might only get disabled instead of bricked
                            EMP.softEMP = true;  
                        }
                        Ray LoSRay = new Ray(transform.position, partHit.transform.position - transform.position);
                        RaycastHit hit;
                        if (Physics.Raycast(LoSRay, out hit, distToG0, 9076737)) // only add impulse to parts with line of sight to detonation
                        {
                            KerbalEVA eva = hit.collider.gameObject.GetComponentUpwards<KerbalEVA>();
                            Part p = eva ? eva.part : hit.collider.gameObject.GetComponentInParent<Part>();
                            float blastDamage = 100;
                            if (p == partHit)
                            {
                                //if (p.vessel != this.vessel)

                                // Forces
                                if (p.rb != null && p.rb.mass > 0) // Don't apply forces to physicsless parts.
                                {
                                    blastImpulse = Mathf.Pow(3.01f * 1100f / distToG0, 1.25f) * 6.894f * lastValidAtmDensity * yieldCubeRoot * radiativeArea / 3f;
                                    // Math.Pow(Math.Pow(Math.Pow(9.54e-3 * 2200.0 / distToG0, 1.95), 4.0) + Math.Pow(Math.Pow(3.01 * 1100.0 / distToG0, 1.25), 4.0), 0.25) * 6.894 * vessel.atmDensity * Math.Pow(yield, 1.0 / 3.0) * partHit.radiativeArea / 3.0; //assuming a 0.05 kT yield
                                    if (double.IsNaN(blastImpulse))
                                    {
                                        Debug.LogWarning("[BDArmory.NukeFX]: blast impulse is NaN. distToG0: " + distToG0 + ", vessel: " + p.vessel + ", atmDensity: " + lastValidAtmDensity + ", yield^(1/3): " + yieldCubeRoot + ", partHit: " + partHit + ", radiativeArea: " + radiativeArea);
                                    }
                                    else
                                    {
                                        if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.NukeTest]: Applying " + blastImpulse.ToString("0.0") + " impulse to " + p + " of mass " + p.mass + " at distance " + distToG0 + "m");
                                        p.rb.AddForceAtPosition((partHit.transform.position - transform.position).normalized * (float)blastImpulse, partHit.transform.position, ForceMode.Impulse);
                                    }
                                }

                                // Damage
                                blastDamage = ((float)((yield * 3370000000) / (4f * Mathf.PI * distToG0 * distToG0) * (radiativeArea / 2f)));
                                if (float.IsNaN(blastDamage))
                                {
                                    Debug.LogWarning("[BDArmory.NukeFX]: blast damage is NaN. distToG0: " + distToG0 + ", yield: " + yield + ", part: " + partHit + ", radiativeArea: " + radiativeArea);
                                    continue;
                                }
                                var damage = p.AddExplosiveDamage(blastDamage, 100, ExplosionSourceType.Missile);

                                // Scoring
                                var aName = Sourcevessel; // Attacker
                                var tName = p.vessel.GetName(); // Target
                                if (BDACompetitionMode.Instance.Scores.RegisterMissileHit(aName, tName, 1))
                                {
                                    if (vesselsHitByMissiles.ContainsKey(tName))
                                        ++vesselsHitByMissiles[tName];
                                    else
                                        vesselsHitByMissiles[tName] = 1;
                                    BDACompetitionMode.Instance.Scores.RegisterMissileDamage(aName, tName, damage);
                                    if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.NukeFX]: " + aName + " did " + damage + " blast damage to " + tName + " at " + distToG0.ToString("0.000") + "m");
                                }

                            }
                        }
                    }
                    else //this isn't finding buildings for some reason, FIXME later
                    {/*
                        DestructibleBuilding building = null;
                        try
                        {
                            building = blastHits.Current.gameObject.GetComponentUpwards<DestructibleBuilding>();
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning("[BDArmory.ProjectileUtils]: Exception thrown in CheckBuildingHit: " + e.Message + "\n" + e.StackTrace);
                        }
                        if (building != null && building.IsIntact)
                        {
                            var distToEpicenter = Mathf.Max((transform.position - building.transform.position).magnitude, 1f);
                            var blastImpulse = Mathf.Pow(3.01f * 1100f / distToEpicenter, 1.25f) * 6.894f * lastValidAtmDensity * yieldCubeRoot;
                            Debug.Log("[BDArmory.NukeFX]: Building hit; distToG0: " + distToEpicenter + ", yield: " + yield + ", building: " + building.name);
                            
                            if (!double.IsNaN(blastImpulse)) //140kPa, level at which reinforced concrete structures are destroyed
                            {
                                Debug.Log("[BDArmory.NukeFX]: Building Impulse: " + blastImpulse);
                                if (blastImpulse > 140)
                                {
                                    building.Demolish();
                                }
                            }
                        }
                        */
                        DestructibleBuilding building = blastHits.Current.GetComponentInParent<DestructibleBuilding>();

                        if (building != null)
                        {
                            Vector3 distToEpicenter = transform.position - building.transform.position;
                            blastImpulse = blastImpulse = Mathf.Pow(3.01f * 1100f / distToEpicenter.magnitude, 1.25f) * 6.894f * lastValidAtmDensity * yieldCubeRoot;
                        }
                        if (blastImpulse > 140) //140kPa, level at which reinforced concrete structures are destroyed
                        {
                            building.Demolish();
                        }
                    }
                }
            }
            gameObject.SetActive(false);
        }
        static void SetupPool(string ModelPath)
        {
            var key = ModelPath;
            if (!nukePool.ContainsKey(key) || nukePool[key] == null)
            {
                var templateFX = GameDatabase.Instance.GetModel(ModelPath);
                if (templateFX == null)
                {
                    templateFX = new GameObject("NukeFX");
                    Debug.LogError("[BDArmory.NukeFX]: " + ModelPath + " was not found.");
                }
                var eFx = templateFX.AddComponent<NukeFX>();

                templateFX.SetActive(false);
                nukePool[key] = ObjectPool.CreateObjectPool(templateFX, 5, true, true);
            }
        }

        public static void CreateExplosion(Vector3 position, ExplosionSourceType explosionSourceType, string sourceVesselName, string explosionPath = "BDArmory/Models/explosion/explosion", string soundPath = "BDArmory/Sounds/explode1", float delay = 2.5f, float blastRadius = 750, float Yield = 0.05f, float thermalShock = 0.05f, bool emp = true, string sourceWeaponName = "Nuke", string ModelPath = "BDArmory/Models/Mutators/NukeCore")
        {
            SetupPool(ModelPath);

            Quaternion rotation;
            rotation = Quaternion.LookRotation(VectorUtils.GetUpDirection(position));
            GameObject newExplosion = nukePool[ModelPath].GetPooledObject();
            NukeFX eFx = newExplosion.GetComponent<NukeFX>();
            newExplosion.transform.SetPositionAndRotation(position, rotation);

            eFx.Position = position;
            eFx.ExplosionSource = explosionSourceType;
            eFx.Sourcevessel = sourceVesselName;
            eFx.reportingName = sourceWeaponName;
            eFx.explModelPath = explosionPath;
            eFx.explSoundPath = soundPath;
            eFx.thermalRadius = blastRadius;
            eFx.yield = Yield;
            eFx.fluence = thermalShock;
            eFx.isEMP = emp;
            eFx.detonationTimer = delay;
            newExplosion.SetActive(true);
        }     
    }
}
