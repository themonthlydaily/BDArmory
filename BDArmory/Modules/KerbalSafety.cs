using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BDArmory.Core;
using BDArmory.Control;
using BDArmory.Misc;
using BDArmory.UI;

namespace BDArmory.Modules
{
    // A class to manage the safety of kerbals in BDA.
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class KerbalSafetyManager : MonoBehaviour
    {
        #region Definitions
        static public KerbalSafetyManager Instance; // static instance for dealing with global stuff.

        public Dictionary<ProtoCrewMember, KerbalSafety> kerbals; // The kerbals being managed.
        #endregion

        public void Awake()
        {
            if (Instance != null)
                Destroy(Instance);
            Instance = this;

            kerbals = new Dictionary<ProtoCrewMember, KerbalSafety>();
        }

        public void Start()
        {
            Debug.Log("[KerbalSafety]: Safety manager started" + (BDArmorySettings.KERBAL_SAFETY ? " and enabled." : ", but currently disabled."));
            GameEvents.onGameSceneSwitchRequested.Add(HandleSceneChange);
        }

        public void OnDestroy()
        {
            GameEvents.onGameSceneSwitchRequested.Remove(HandleSceneChange);
        }

        public void HandleSceneChange(GameEvents.FromToAction<GameScenes, GameScenes> fromTo)
        {
            if (fromTo.from == GameScenes.FLIGHT)
            {
                DisableKerbalSafety();
                foreach (var ks in kerbals.Values)
                    ks.recovered = true;
            }
        }

        public void EnableKerbalSafety()
        {
            Debug.Log("[KerbalSafety]: Enabling kerbal safety.");
            foreach (var ks in kerbals.Values)
                ks.AddHandlers();
            CheckAllVesselsForKerbals(); // Check for new vessels that were added while we weren't active.
        }

        public void DisableKerbalSafety()
        {
            Debug.Log("[KerbalSafety]: Disabling kerbal safety.");
            foreach (var ks in kerbals.Values)
                ks.RemoveHandlers();
        }

        public void CheckAllVesselsForKerbals()
        {
            foreach (var vessel in FlightGlobals.Vessels)
                CheckVesselForKerbals(vessel);
        }

        public void CheckVesselForKerbals(Vessel vessel)
        {
            if (!BDArmorySettings.KERBAL_SAFETY) return;
            if (vessel == null) return;
            foreach (var part in vessel.parts)
            {
                if (part == null) continue;
                foreach (var crew in part.protoModuleCrew)
                {
                    if (crew == null) continue;
                    if (kerbals.ContainsKey(crew)) continue; // Already managed.
                    var ks = part.gameObject.AddComponent<KerbalSafety>();
                    StartCoroutine(ks.Configure(crew, part));
                }
            }
        }

        //     public void CheckForFallingKerbals(Vessel vessel)
        //     {
        //         if (vessel == null || !vessel.loaded) return;
        //         var kerbalEVA = vessel.FindPartModuleImplementing<KerbalEVA>();
        //         if (kerbalEVA != null && vessel.parts.Count == 1) // Check for a falling kerbal.
        //         {
        //             var chute = kerbalEVA.vessel.FindPartModuleImplementing<ModuleEvaChute>();
        //             if (chute != null && chute.deploymentState != ModuleParachute.deploymentStates.DEPLOYED && !chutesToDeploy.Contains(chute))
        //             {
        //                 chutesToDeploy.Add(chute);
        //                 StartCoroutine(DelayedChuteDeployment(chute, kerbalEVA));
        //                 // var crew = kerbalEVA.GetComponent<ProtoCrewMember>();
        //                 // if (crew != null && KerbalSafetyManager.Instance.kerbals.ContainsKey(crew.displayName))
        //                 StartCoroutine(KerbalSafetyManager.Instance.RecoverWhenPossible(kerbalEVA));
        //             }
        //             else if (chute == null)
        //                 Debug.LogError("[KerbalSafety]: " + kerbalEVA.vessel.vesselName + " didn't have a parachute!");
        //         }
        //     }

        //     IEnumerator DelayedChuteDeployment(ModuleEvaChute chute, KerbalEVA kerbal, float delay = 1f)
        //     {
        //         yield return new WaitForSeconds(delay);
        //         if (kerbal != null)
        //             kerbal.vessel.altimeterDisplayState = AltimeterDisplayState.AGL;
        //         if (chute != null && kerbal != null && !kerbal.IsSeated() && !kerbal.vessel.LandedOrSplashed) // Check that the kerbal hasn't regained their seat or already landed.
        //         {
        //             Debug.Log("[KerbalSafety]: Found a falling kerbal (" + kerbal.vessel.vesselName + "), deploying halo parachute.");
        //             if (chute.deploymentState != ModuleParachute.deploymentStates.SEMIDEPLOYED)
        //                 chute.deploymentState = ModuleParachute.deploymentStates.STOWED; // Reset the deployment state.
        //             chute.deployAltitude = 30f;
        //             chute.Deploy();
        //             // var crew = kerbal.GetComponent<ProtoCrewMember>();
        //             // if (crew != null && KerbalSafetyManager.Instance.kerbals.ContainsKey(crew.displayName))
        //             //     StartCoroutine(KerbalSafetyManager.Instance.kerbals[crew.displayName].PostEjection());
        //         }
        //         if (kerbal != null && kerbal.vessel.LandedOrSplashed)
        //             Debug.Log("[KerbalSafety]: " + kerbal.vessel.vesselName + " has already landed, not deploying chute.");
        //         chutesToDeploy.Remove(chute);
        //     }

        //     public IEnumerator RecoverWhenPossible(KerbalEVA kerbal)
        //     {
        //         var kerbalName = kerbal.vessel.vesselName;
        //         int count = 0;
        //         while (kerbal != null && string.IsNullOrEmpty(kerbalName = kerbal.vessel.vesselName))
        //         {
        //             Debug.Log("DEBUG nameless kerbal, waiting a frame (" + ++count + ")");
        //             yield return new WaitForFixedUpdate();
        //         }
        //         if (beingRecovered.Contains(kerbalName)) // Already being recovered.
        //         {
        //             Debug.Log("DEBUG " + kerbalName + " is already scheduled for recovery");
        //             yield break;
        //         }
        //         Debug.Log("DEBUG Scheduling " + kerbalName + " for recovery");
        //         beingRecovered.Add(kerbalName);
        //         yield return new WaitUntil(() => kerbal == null || kerbal.vessel.LandedOrSplashed);
        //         yield return new WaitForSeconds(5); // Give it around 5s after landing, then recover the kerbal
        //         yield return new WaitUntil(() => kerbal == null || FlightGlobals.ActiveVessel != kerbal.vessel);
        //         if (kerbal == null)
        //         {
        //             Debug.LogError("[KerbalSafety]: " + kerbalName + " on EVA is MIA.");
        //             beingRecovered.Remove(kerbalName);
        //             yield break;
        //         }
        //         Debug.Log("[KerbalSafety]: Recovering " + kerbalName);
        //         ShipConstruction.RecoverVesselFromFlight(kerbal.vessel.protoVessel, HighLogic.CurrentGame.flightState, true);
        //         beingRecovered.Remove(kerbalName);
        //     }

        public IEnumerator RecoverCrewFromVesselNow(Vessel vessel)
        {
            if (vessel.parts.Count == 1 && vessel.parts[0].isKerbalEVA()) // Vessel is just a kerbal on EVA.
            {
                // Recover the crew when possible.
                var crew = vessel.parts[0].protoModuleCrew[0];
                if (kerbals.ContainsKey(crew))
                {
                    Debug.Log("DEBUG Scheduling " + crew.displayName + " for recovery ASAP."); // FIXME This causes the 10s terrain settling to fail.
                    kerbals[crew].RecoverWhenPossible(true);
                    yield break;
                }
            }
            else
            {
                foreach (var part in vessel.parts.ToList())
                {
                    if (part.isKerbalEVA()) // Kerbal in a seat.
                    {
                        vessel.IgnoreGForces(Mathf.RoundToInt(10f / Time.fixedDeltaTime));
                        // Leave the seat, switch back to the vessel containing the seat and immediately recover the kerbal. FIXME This didn't work as planned
                        var crew = part.protoModuleCrew[0];
                        if (kerbals.ContainsKey(crew))
                        {
                            var before = vessel.vesselName;
                            Debug.Log("DEBUG Scheduling " + crew.displayName + " for immediate recovery from " + vessel.vesselName + ".");
                            kerbals[crew].seat.LeaveSeat(new KSPActionParam(KSPActionGroup.Abort, KSPActionType.Activate));
                            yield return new WaitForFixedUpdate();
                            Debug.Log("DEBUG vesselName before: " + before + ", after: " + vessel.vesselName);
                            kerbals[crew].RecoverWhenPossible(true);
                            FlightGlobals.SetActiveVessel(vessel);
                        }
                    }
                    else // Crew in cockpit/cabins.
                    {
                        // Just remove the crew.
                        foreach (var crew in part.protoModuleCrew.ToList())
                        {
                            Debug.Log("DEBUG Removing " + crew.displayName + " from " + vessel.vesselName);
                            part.RemoveCrewmember(crew);
                            crew.rosterStatus = ProtoCrewMember.RosterStatus.Available;
                            if (kerbals.ContainsKey(crew))
                            {
                                kerbals[crew].recovered = true;
                                Destroy(kerbals[crew]);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Register all the crew members as recovered, then recover the vessel.
        /// </summary>
        /// <param name="vessel">The vessel to recover.</param>
        public void RecoverVesselNow(Vessel vessel)
        {
            foreach (var part in vessel.parts.ToList())
            {
                foreach (var crew in part.protoModuleCrew.ToList())
                {
                    if (kerbals.ContainsKey(crew))
                    {
                        kerbals[crew].recovered = true;
                        Debug.Log("[KerbalSafety]: Recovering " + kerbals[crew].kerbalName + ".");
                        Destroy(kerbals[crew]);
                    }
                }
            }
            ShipConstruction.RecoverVesselFromFlight(vessel.protoVessel, HighLogic.CurrentGame.flightState, true);
        }
    }

    public class KerbalSafety : MonoBehaviour
    {
        #region Definitions
        public string kerbalName; // The name of the kerbal/crew member.
        public KerbalEVA kerbalEVA; // For kerbals that have ejected or are sitting in command seats.
        public ProtoCrewMember crew; // For kerbals that are in cockpits.
        public Part part; // The part the proto crew member is in.
        public KerbalSeat seat; // The seat the kerbalEVA is in (if they're in one).
        public ModuleEvaChute chute; // The chute of the crew member.
        // public BDModulePilotAI ai;
        public bool recovering = false; // Whether they're scheduled for recovery or not.
        public bool recovered = false; // Whether they've been recovered or not.
        public bool deployingChute = false; // Whether they're scheduled for deploying their chute or not.
        public bool ejected = false; // Whether the kerbal has ejected or not.
        #endregion

        #region Field definitions
        // [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_EjectOnImpendingDoom", // Eject if doomed
        //     groupName = "pilotAI_Ejection", groupDisplayName = "#LOC_BDArmory_PilotAI_Ejection", groupStartCollapsed = true),
        //     UI_FloatRange(minValue = 0f, maxValue = 1f, stepIncrement = 0.02f, scene = UI_Scene.All)]
        // public float ejectOnImpendingDoom = 0.2f; // Time to impact at which to eject.
        #endregion

        public IEnumerator Configure(ProtoCrewMember crew, Part part)
        {
            if (crew == null)
            {
                Debug.LogError("[KerbalSafety]: Cannot manage null crew.");
                Destroy(this);
                yield break;
            }
            if (part == null)
            {
                Debug.LogError("[KerbalSafety]: Crew cannot exist outside of a part.");
                Destroy(this);
                yield break;
            }
            while (!part.vessel.loaded) yield return new WaitForFixedUpdate(); // Wait for the vessel to be loaded. (Avoids kerbals not being registered in seats.)
            kerbalName = crew.displayName;
            this.crew = crew;
            this.crew.SetDefaultInventory(); // Reset the inventory to a chute and a jetpack.
            this.part = part;
            if (part.isKerbalEVA())
            {
                this.kerbalEVA = part.GetComponent<KerbalEVA>();
                if (kerbalEVA.IsSeated())
                {
                    var seats = part.vessel.FindPartModulesImplementing<KerbalSeat>();
                    bool found = false;
                    foreach (var s in seats)
                    {
                        if (s.Occupant == part)
                        {
                            seat = s;
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        Debug.LogError("[KerbalSafety]: Failed to find the kerbal seat that " + kerbalName + " occupies.");
                        yield break;
                    }
                }
                else // Free-falling EVA kerbal.
                {
                    ejected = true;
                }
                chute = kerbalEVA.vessel.FindPartModuleImplementing<ModuleEvaChute>();
                if (chute != null)
                    chute.deploymentState = ModuleEvaChute.deploymentStates.STOWED; // Make sure the chute is stowed.
            }
            AddHandlers();
            KerbalSafetyManager.Instance.kerbals.Add(crew, this);
            Debug.Log("[KerbalSafety]: Managing the safety of " + kerbalName + " in " + part.vessel.vesselName + ".");
        }

        public void OnDestroy()
        {
            StopAllCoroutines();
            if (BDArmorySettings.KERBAL_SAFETY && !recovered)
            {
                Debug.Log("[KerbalSafety]: " + kerbalName + " is MIA.");
            }
            if (crew != null)
            {
                if (KerbalSafetyManager.Instance.kerbals.ContainsKey(crew))
                {
                    KerbalSafetyManager.Instance.kerbals.Remove(crew); // Stop managing this kerbal.
                }
            }
            RemoveHandlers();
        }

        public void AddHandlers()
        {
            if (kerbalEVA)
            {
                if (seat && seat.part)
                    seat.part.OnJustAboutToBeDestroyed += Eject;
            }
            else
            {
                if (part)
                    part.OnJustAboutToBeDestroyed += Eject;
            }
            GameEvents.onVesselPartCountChanged.Add(OnVesselModified);
            GameEvents.onVesselCreate.Add(OnVesselModified);
        }

        public void RemoveHandlers()
        {
            if (part) part.OnJustAboutToBeDestroyed -= Eject;
            if (seat && seat.part) seat.part.OnJustAboutToBeDestroyed -= Eject;
            GameEvents.onVesselPartCountChanged.Remove(OnVesselModified);
            GameEvents.onVesselCreate.Remove(OnVesselModified);
        }

        #region Ejection
        public void Eject()
        {
            if (kerbalEVA != null)
            {
                if (kerbalEVA.isActiveAndEnabled)
                {
                    if (seat != null && kerbalEVA.IsSeated()) // Leave the seat.
                    {
                        Debug.Log("[KerbalSafety]: " + kerbalName + " is leaving their seat on " + seat.part.vessel.vesselName + ".");
                        seat.LeaveSeat(new KSPActionParam(KSPActionGroup.Abort, KSPActionType.Activate));
                        StartCoroutine(DelayedChuteDeployment());
                        StartCoroutine(RecoverWhenPossible());
                    }
                    else
                    {
                        Debug.Log("[KerbalSafety]: " + kerbalName + " has already left their seat.");
                    }
                    StartCoroutine(PostEjection());
                }
                else
                    Debug.Log("DEBUG " + kerbalName + " was not active, unable to eject.");
            }
            else if (crew != null) // Eject from a cockpit.
            {
                Debug.Log("DEBUG skipping ejection for " + kerbalName + " in " + part + "."); return;
                // kerbalEVA = FlightEVA.fetch.spawnEVA(kerbalPCM, part, part.airlock, true);
                // if (kerbalEVA)
                // {
                //     Debug.Log("[KerbalSafety]: " + kerbalPCM.displayName + " ejected from " + part.vessel.vesselName + " at " + part.vessel.radarAltitude.ToString("0.0") + "m.");
                //     kerbalEVA.autoGrabLadderOnStart = false; // Don't grab the vessel.
                //     chute = kerbalEVA.vessel.FindPartModuleImplementing<ModuleEvaChute>();
                //     if (chute != null)
                //         chute.deploymentState = ModuleEvaChute.deploymentStates.STOWED; // Make sure the chute is stowed.
                //     // DeactivatePilot();
                //     StartCoroutine(PostEjection());
                // }
                // else
                // {
                //     // All exits were blocked by something. FIXME Try adjusting the fromAirlock Transform in spawnEVA.
                //     // BDACompetitionMode.Instance.competitionStatus.Add(kerbalPCM.displayName + " failed to eject from " + part.vessel.vesselName + ", all exits were blocked. RIP.");
                //     Debug.Log("[KerbalSafety]: " + kerbalPCM.displayName + " failed to eject from " + part.vessel.vesselName + ", all exits were blocked. RIP.");
                //     part.vessel.RemoveCrew(kerbalPCM); // Save their ghost.
                //     // StartCoroutine(ExplodeStuffThenEject());
                //     // ExplodeStuffThenEject();
                // }
            }
            else
            {
                Debug.LogError("[KerbalSafety]: Ejection called without a kerbal present.");
            }
            ejected = true;
        }

        public IEnumerator PostEjection()
        {
            yield return new WaitForFixedUpdate(); // Wait 2 fixed updates so that vessel position and velocity get valid values.
            yield return new WaitForFixedUpdate();
            if (kerbalEVA == null || kerbalEVA.vessel == null) { Debug.Log("[KerbalSafety]: " + kerbalName + " is MIA after leaving their vessel. RIP."); yield break; }
            // KerbalSafetyManager.Instance.CheckForFallingKerbals(kerbalEVA.vessel); // Make sure the kerbal gets recovered.
            if (kerbalEVA.OnALadder) // Force the kerbal to let go.
            {
                var fsm = kerbalEVA.fsm;
                var letGoEvent = fsm.CurrentState.StateEvents.SingleOrDefault(e => e.name == "Ladder Let Go");
                if (letGoEvent == null) Debug.LogError("[KerbalSafety]: Did not find let go event");
                else
                {
                    Debug.Log("DEBUG running letGoEvent");
                    fsm.RunEvent(letGoEvent);
                }
            }
            if (kerbalEVA.vessel.LandedOrSplashed) { Debug.Log("[KerbalSafety]: " + kerbalName + " is already landed, not compensating for low altitude."); yield break; }
            var currentAltitude = Misc.Misc.GetRadarAltitudeAtPos(kerbalEVA.vessel.transform.position, false);
            var expectedAltitudeIn5s = Misc.Misc.GetRadarAltitudeAtPos(kerbalEVA.vessel.PredictPosition(5f), false);
            var gravity = FlightGlobals.getGeeForceAtPosition(kerbalEVA.vessel.transform.position);
            var upDirection = -gravity.normalized;
            var gee = (float)gravity.magnitude;
            if (currentAltitude < 2) kerbalEVA.vessel.transform.position += (2f - currentAltitude) * upDirection;
            kerbalEVA.vessel.srf_velocity += Mathf.Sqrt(Mathf.Max(0f, 200f - expectedAltitudeIn5s) * gee) * upDirection;
            Debug.Log("[KerbalSafety]: Adding " + (Mathf.Sqrt(Mathf.Max(0f, 200f - expectedAltitudeIn5s) * gee)).ToString("0.0") + "m/s to " + kerbalName + "'s vertical velocity giving " + Vector3.Dot(kerbalEVA.vessel.srf_velocity, upDirection).ToString("0.0") + "m/s at altitude " + kerbalEVA.vessel.radarAltitude.ToString("0.0"));
        }

        // // IEnumerator
        // void ExplodeStuffThenEject()
        // {
        //     Debug.Log("DEBUG exploding parts without kerbals in " + part.vessel.vesselName + " for " + kerbalName);
        //     foreach (var part in part.vessel.parts.ToList())
        //     {
        //         if (part.protoModuleCrew.Count == 0)
        //         {
        //             part.explode();
        //         }
        //     }
        //     // yield return new WaitForFixedUpdate();
        //     // Debug.Log("DEBUG try ejecting again");
        //     // Eject();
        // }

        // public void EjectOnJustAboutToBeDestroyed()
        // {
        //     if (ejected)
        //     {
        //         Debug.Log("[KerbalSafety]: " + kerbalName + " has already ejected, but is getting destroyed.");
        //     }
        //     // if (!ejected && !(part.vessel.parts.Count == 1 && part.isKerbalEVA()))
        //     if (!(part.vessel.parts.Count == 1 && part.isKerbalEVA()))
        //     {
        //         // if (BDArmorySettings.DRAW_DEBUG_LABELS)
        //         Debug.Log("[KerbalSafety]: Ejecting " + kerbalName + " from " + part.vessel.vesselName + " due to part about to be destroyed.");
        //         Eject();
        //     }
        //     else
        //     {
        //         Debug.Log("[KerbalSafety]: " + kerbalName + " was killed. RIP.");
        //         Debug.Log("DEBUG " + kerbalName + ": part count: " + part.vessel.parts.Count + ", isKerbalEVA:" + part.isKerbalEVA() + ", EVA module:" + part.FindModuleImplementing<KerbalEVA>());
        //     }
        // }

        // void EjectOnImpendingDoom()
        // {
        //     if (!ejected && ejectOnImpendingDoom * (float)vessel.srfSpeed > ai.terrainAlertDistance)
        //     {
        //         KerbalSafety.Instance.Eject(vessel, this); // Abandon ship!
        //         ai.avoidingTerrain = false;
        //     }
        // }

        public void OnVesselModified(Vessel vessel)
        {
            if (vessel == null || !vessel.loaded) return;
            if (part.vessel == vessel) // It's this vessel.
            {
                if (kerbalEVA != null)
                {
                    if (kerbalEVA.isActiveAndEnabled)
                    {
                        if (vessel.parts.Count == 1) // It's a falling kerbal.
                        {
                            ejected = true;
                            StartCoroutine(DelayedChuteDeployment());
                            StartCoroutine(RecoverWhenPossible());
                        }
                        else // It's a kerbal in a seat.
                        {
                            // FIXME Check if the kerbal needs to leave his seat.
                            // seat.LeaveSeat(new KSPActionParam(KSPActionGroup.Abort, KSPActionType.Activate)); // This will trigger a new OnVesselModified for the now falling kerbal.
                        }
                    }
                    else
                        Debug.Log("DEBUG " + kerbalName + " was not active.");
                }
                else // It's a crew.
                {
                    // FIXME Check if the crew needs to eject.
                }
            }
        }

        IEnumerator DelayedChuteDeployment(float delay = 1f)
        {
            if (deployingChute)
            {
                yield break;
            }
            deployingChute = true;
            yield return new WaitForSeconds(delay);
            if (kerbalEVA == null) yield break;
            kerbalEVA.vessel.altimeterDisplayState = AltimeterDisplayState.AGL;
            if (chute != null && !kerbalEVA.IsSeated() && !kerbalEVA.vessel.LandedOrSplashed) // Check that the kerbal hasn't regained their seat or already landed.
            {
                Debug.Log("[KerbalSafety]: " + kerbalName + " is falling, deploying halo parachute.");
                if (chute.deploymentState != ModuleParachute.deploymentStates.SEMIDEPLOYED)
                    chute.deploymentState = ModuleParachute.deploymentStates.STOWED; // Reset the deployment state.
                chute.deployAltitude = 30f;
                chute.Deploy();
                // var crew = kerbal.GetComponent<ProtoCrewMember>();
                // if (crew != null && KerbalSafetyManager.Instance.kerbals.ContainsKey(crew.displayName))
                //     StartCoroutine(KerbalSafetyManager.Instance.kerbals[crew.displayName].PostEjection());
            }
            if (kerbalEVA.vessel.LandedOrSplashed)
                Debug.Log("[KerbalSafety]: " + kerbalEVA.vessel.vesselName + " has already landed, not deploying chute.");
        }

        /// <summary>
        /// Recover the kerbal when possible (has landed and isn't the active vessel).
        /// </summary>
        /// <param name="asap">Don't wait until the kerbal has landed.</param>
        public IEnumerator RecoverWhenPossible(bool asap = false)
        {
            if (asap)
            {
                if (KerbalSafetyManager.Instance.kerbals.ContainsKey(crew))
                    KerbalSafetyManager.Instance.kerbals.Remove(crew); // Stop managing this kerbal.
            }
            if (recovering)
            {
                yield break;
            }
            recovering = true;
            if (!asap)
            {
                yield return new WaitUntil(() => kerbalEVA == null || kerbalEVA.vessel.LandedOrSplashed);
                yield return new WaitForSeconds(5); // Give it around 5s after landing, then recover the kerbal
            }
            yield return new WaitUntil(() => kerbalEVA == null || FlightGlobals.ActiveVessel != kerbalEVA.vessel);
            if (kerbalEVA == null)
            {
                Debug.LogError("[KerbalSafety]: " + kerbalName + " on EVA is MIA.");
                yield break;
            }
            Debug.Log("[KerbalSafety]: Recovering " + kerbalName + ".");
            recovered = true;
            ShipConstruction.RecoverVesselFromFlight(kerbalEVA.vessel.protoVessel, HighLogic.CurrentGame.flightState, true);
        }
        #endregion
    }
}