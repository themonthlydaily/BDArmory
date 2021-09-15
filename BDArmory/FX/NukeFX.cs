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
                        ExplosionFx.CreateExplosion(Position, tntmass, explModelPath, explSoundPath, ExplosionSource, 0, null, Sourcevessel, reportingName);
                    }
                }
            }
        }

        void Detonate() //borrowed from Stockalike Project Orion
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
                                    var blastImpulse = Mathf.Pow(3.01f * 1100f / distToG0, 1.25f) * 6.894f * lastValidAtmDensity * yieldCubeRoot * radiativeArea / 3f;
                                    // Math.Pow(Math.Pow(Math.Pow(9.54e-3 * 2200.0 / distToG0, 1.95), 4.0) + Math.Pow(Math.Pow(3.01 * 1100.0 / distToG0, 1.25), 4.0), 0.25) * 6.894 * vessel.atmDensity * Math.Pow(yield, 1.0 / 3.0) * partHit.radiativeArea / 3.0; //assuming a 0.05 kT yield
                                    if (float.IsNaN(blastImpulse))
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
                    else
                    {

                        DestructibleBuilding building = blastHits.Current.GetComponentInParent<DestructibleBuilding>();

                        if (building != null)
                        {
                            var distToEpicenter = Mathf.Max((transform.position - building.transform.position).magnitude, 1f);
                            var blastImpulse = Mathf.Pow(3.01f * 1100f / distToEpicenter, 1.25f) * 6.894f * lastValidAtmDensity * yieldCubeRoot;
                            // blastImpulse = (((((Math.Pow((Math.Pow((Math.Pow((9.54 * Math.Pow(10.0, -3.0) * (2200.0 / distToEpicenter)), 1.95)), 4.0) + Math.Pow((Math.Pow((3.01 * (1100.0 / distToEpicenter)), 1.25)), 4.0)), 0.25)) * 6.894) * (vessel.atmDensity)) * Math.Pow(yield, (1.0 / 3.0))));
                            if (!double.IsNaN(blastImpulse) && blastImpulse > 140) //140kPa, level at which reinforced concrete structures are destroyed
                            {
                                building.Demolish();
                            }
                        }
                    }
                }
            }
            ExplosionFx.CreateExplosion(transform.position, 1, explModelPath, explSoundPath, ExplosionSourceType.Other, 0, null, Sourcevessel, reportingName);
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

        public static void CreateExplosion(Vector3 position, ExplosionSourceType explosionSourceType, string sourceVesselName, string explosionPath = "BDArmory/Models/explosion/explosion", string soundPath = "BDArmory/Sounds/explode1", float delay = 2.5f, float tntMassEquivalent = 100, float blastRadius = 750, float Yield = 0.05f, float thermalShock = 0.05f, bool emp = true, string sourceWeaponName = "Nuke", string ModelPath = "BDArmory/Models/Mutators/NukeCore")
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
            eFx.tntmass = tntMassEquivalent;
            newExplosion.SetActive(true);
        }     
    }
}
