using BDArmory.Control;
using BDArmory.Core.Extension;
using BDArmory.Core.Module;
using BDArmory.FX;
using System;
using System.Linq;
using UnityEngine;

namespace BDArmory.Modules
{
    class RWPS3R2NukeModule : PartModule
    {
        [KSPField(isPersistant = true, guiActive = true, guiName = "WARNING: Reactor Safeties:", guiActiveEditor = true), UI_Label(affectSymCounterparts = UI_Scene.All, scene = UI_Scene.All)]//Weapon Name 
        public string status = "OFFLINE";

        [KSPField(isPersistant = true, guiActive = true, guiName = "Coolant Remaining", guiActiveEditor = false), UI_Label(scene = UI_Scene.All)]
        public double fuelleft;

        [KSPField]
        public string explModelPath = "BDArmory/Models/explosion/explosion";

        [KSPField]
        public string explSoundPath = "BDArmory/Sounds/explode1";

        [KSPField(isPersistant = true)]
        public float thermalRadius = 750;

        [KSPField(isPersistant = true)]
        public float yield = 0.05f;

        [KSPField(isPersistant = true)]
        public float tntEquivilent = 500;

        [KSPField(isPersistant = true)]
        public float ADTimer = 20;

        double blastImpulse;
        private int FuelID;
        private bool hasDetonated = false;
        public override void OnStart(StartState state)
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                FuelID = PartResourceLibrary.Instance.GetDefinition("LiquidFuel").id;
                vessel.GetConnectedResourceTotals(FuelID, out double fuelCurrent, out double fuelMax);
                fuelleft = fuelCurrent;
                var engine = part.FindModuleImplementing<ModuleEngines>();
                if (engine != null)
                {
                    engine.allowShutdown = false;
                }
                part.force_activate();
                part.OnJustAboutToBeDestroyed += Detonate;
            }
            base.OnStart(state);
        }

        public void Update()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (BDACompetitionMode.Instance.competitionIsActive) //only begin checking engine state after comp start
                {
                    vessel.GetConnectedResourceTotals(FuelID, out double fuelCurrent, out double fuelMax);
                    fuelleft = fuelCurrent;
                    if (fuelleft <= 0)
                    {
                        if (!hasDetonated)
                        {
                            Debug.Log("[NukeTest] nerva out of fuel, detonating");
                            Detonate(); //bingo fuel, detonate
                        }
                    }
                    var engine = part.FindModuleImplementing<ModuleEngines>();
                    if (engine != null)
                    {
                        if (!engine.isEnabled || !engine.EngineIgnited)
                        {
                            if (!hasDetonated)
                            {
                                Debug.Log("[NukeTest] nerva Off, detonating");
                                Detonate(); //nuke engine off after comp start, detonate.
                            }
                        }
                        if (engine.thrustPercentage < 100)
                        {
                            if (part.Modules.GetModule<HitpointTracker>().Hitpoints == part.Modules.GetModule<HitpointTracker>().GetMaxHitpoints())
                            {
                                if (!hasDetonated)
                                {
                                    Debug.Log("[NukeTest] nerva manually thrust limited, detonating");
                                    Detonate(); //nuke engine off after comp start, detonate.
                                }
                            }
                        }
                    }
                }

            }
        }

        void Detonate() //borrowed from Stockalike Project Orion
        {
            hasDetonated = true;
            Debug.Log("[NukeTest] Running Detonate()");
            //affect any nearby parts/vessels that aren't the source vessel		

            using (var blastHits = Physics.OverlapSphere(part.transform.position, 750, 9076737).AsEnumerable().GetEnumerator())
            {
                while (blastHits.MoveNext())
                {
                    if (blastHits.Current == null) continue;
                    try
                    {
                        Part partHit = blastHits.Current.GetComponentInParent<Part>();
                        if (partHit != null && partHit.mass > 0)
                        {
                            Rigidbody rb = partHit.Rigidbody;
                            Vector3 distToG0 = part.transform.position - partHit.transform.position;
                            //if (partHit.vessel != this.vessel)
                            if (partHit != this.part)
                            {
                                blastImpulse = ((((((((Math.Pow((Math.Pow((Math.Pow((9.54 * Math.Pow(10.0, -3.0) * (2200.0 / distToG0.magnitude)), 1.95)), 4.0) + Math.Pow((Math.Pow((3.01 * (1100.0 / distToG0.magnitude)), 1.25)), 4.0)), 0.25)) * 6.894)
                                    * vessel.atmDensity) * Math.Pow(yield, (1.0 / 3.0))))))) * (partHit.radiativeArea / 3.0); //assuming a 0.05 kT yield
                                partHit.skinTemperature += (((((yield * 337000000) / (4 * Math.PI * Math.Pow(distToG0.magnitude, 2.0))) * (partHit.radiativeArea / 2.0))) / partHit.skinThermalMass); // Fluence scales linearly w/ yield, 1 Kt will produce between 33 TJ and 337 kJ at 0-1000m,
                            } // everything gets heated via atmosphere

                            Ray LoSRay = new Ray(part.transform.position, partHit.transform.position - part.transform.position);
                            RaycastHit hit;
                            if (Physics.Raycast(LoSRay, out hit, distToG0.magnitude, 9076737)) // only add impulse to parts with line of sight to detonation
                            {
                                KerbalEVA eva = hit.collider.gameObject.GetComponentUpwards<KerbalEVA>();
                                Part p = eva ? eva.part : hit.collider.gameObject.GetComponentInParent<Part>();
                                if (p == partHit)
                                {
                                    if (rb == null) return;
                                    if (p.vessel != this.vessel)
                                    //if (p != this.part)
                                    {
                                        p.rb.AddForceAtPosition((partHit.transform.position - part.transform.position).normalized * (float)blastImpulse, partHit.transform.position, ForceMode.Impulse);
                                        if (distToG0.magnitude < (thermalRadius * .66f))
                                        {
                                            var choked = p.FindModuleImplementing<ModuleDrainIntakes>();
                                            if (choked != null)
                                            {
                                                choked.drainDuration += ADTimer;
                                            }
                                            else
                                            {
                                                choked = (ModuleDrainIntakes)p.AddModule("ModuleDrainIntakes");
                                                choked.drainDuration = ADTimer;
                                            }
                                            Debug.Log("[BDArmory]: Localized Atmospheric deprevation; intakes choked on " + p.vessel.vesselName);
                                        }
                                    }
                                }
                            }

                        }
                        else
                        {
                            DestructibleBuilding building = blastHits.Current.GetComponentInParent<DestructibleBuilding>();

                            if (building != null)
                            {
                                Vector3 distToEpicenter = part.transform.position - building.transform.position;
                                blastImpulse = (((((Math.Pow((Math.Pow((Math.Pow((9.54 * Math.Pow(10.0, -3.0) * (2200.0 / distToEpicenter.magnitude)), 1.95)), 4.0) + Math.Pow((Math.Pow((3.01 * (1100.0 / distToEpicenter.magnitude)), 1.25)), 4.0)), 0.25)) * 6.894)
                                    * (vessel.atmDensity)) * Math.Pow(yield, (1.0 / 3.0))));
                            }
                            if (blastImpulse > 140) //140kPa, level at which reinforced concrete structures are destroyed
                            {
                                building.Demolish();
                            }

                        }
                    }
                    catch
                    {
                    }

                }
            }
            ExplosionFx.CreateExplosion(part.transform.position, tntEquivilent, explModelPath, explSoundPath, ExplosionSourceType.Missile, 0, null, this.part.vessel.vesselName, "Reactor Containment Failure");
            Debug.Log("[NukeTest] ExplosionFX spawned");
            this.part.Destroy();
            Debug.Log("[NukeTest] part did not destruct; Debug"); // Note: Destroy doesn't actually happen until after the update loop, so this is always going to get logged.
        }
    }
}
