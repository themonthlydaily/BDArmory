using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BDArmory.Core;
using BDArmory.Control;
using BDArmory.UI;

namespace BDArmory.Modules
{
    // A class to manage the safety of kerbals in BDA.
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class KerbalSafetyManager : MonoBehaviour
    {
        #region Definitions
        static public KerbalSafetyManager Instance; // static instance for dealing with global stuff.
        static public Dictionary<string, KerbalSafety> kerbals; // The kerbals to be managed.
        #endregion

        public void Awake()
        {
            if (Instance != null)
                Destroy(Instance);
            Instance = this;

            kerbals = new Dictionary<string, KerbalSafety>();
        }

        public void Start()
        {
            GameEvents.onNewVesselCreated.Add(CheckVesselForKerbals);
        }

        public void OnDestroy()
        {
            GameEvents.onNewVesselCreated.Remove(CheckVesselForKerbals);
        }

        private void CheckVesselForKerbals(Vessel vessel)
        {
            // Find the external crew
            foreach (var kerbalEVA in vessel.FindPartModulesImplementing<KerbalEVA>())
            {
                if (kerbalEVA == null) continue;
                if (kerbals.ContainsKey(kerbalEVA.name)) continue; // Already managed.
                var ks = new KerbalSafety();
                ks.kerbalEVA = kerbalEVA;
                kerbals.Add(ks.kerbalName, ks);
            }

            // Find the internal crew
            foreach (var part in vessel.parts)
            {
                if (part == null) continue;
                foreach (var pcm in part.protoModuleCrew)
                {
                    if (pcm == null) continue;
                    if (kerbals.ContainsKey(pcm.name)) continue; // Already managed.
                    var ks = new KerbalSafety();
                    ks.kerbalPCM = pcm;
                    ks.part = part;
                    kerbals.Add(ks.kerbalName, ks);
                }
            }
        }
    }

    public class KerbalSafety : MonoBehaviour
    {
        #region Definitions
        public KerbalEVA kerbalEVA; // For kerbals that have ejected or are sitting in command seats.
        public ProtoCrewMember kerbalPCM; // For kerbals that are in cockpits.
        public Part part; // The part the proto crew member is in.
        public string kerbalName;
        // public BDModulePilotAI ai;
        public bool ejected = false; // Whether the kerbal has ejected or not.
        #endregion

        #region Field definitions
        // [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_EjectOnImpendingDoom", // Eject if doomed
        //     groupName = "pilotAI_Ejection", groupDisplayName = "#LOC_BDArmory_PilotAI_Ejection", groupStartCollapsed = true),
        //     UI_FloatRange(minValue = 0f, maxValue = 1f, stepIncrement = 0.02f, scene = UI_Scene.All)]
        public float ejectOnImpendingDoom = 0.2f; // Time to impact at which to eject.
        #endregion

        public void Start()
        {
            if (kerbalEVA != null) // Setup for an external kerbal.
            {
                part = kerbalEVA.part;
                kerbalName = kerbalEVA.name;
                if (part.vessel.parts.Count == 1) // It's just a kerbal on EVA. They've already ejected, so ignore them.
                {
                    ejected = true;
                    Destroy(this);
                }
            }
            else if (kerbalPCM != null) // Setup for an internal kerbal.
            {
                kerbalName = kerbalPCM.displayName;
            }
            else
            {
                Debug.LogError("[KerbalSafety]: Instantiated KerbalSafety without a kerbal in sight.");
            }
        }

        public void OnDestroy()
        {
            if (KerbalSafetyManager.kerbals.ContainsKey(kerbalName))
            {
                KerbalSafetyManager.kerbals.Remove(kerbalName); // Stop managing this kerbal.
            }
        }

        // void EjectOnImpendingDoom()
        // {
        //     if (!ejected && ejectOnImpendingDoom * (float)vessel.srfSpeed > ai.terrainAlertDistance)
        //     {
        //         KerbalSafety.Instance.Eject(vessel, this); // Abandon ship!
        //         ai.avoidingTerrain = false;
        //     }
        // }


        #region Ejection
        public void Eject()
        {
            Debug.Log("DEBUG eject triggered for " + kerbalName);
            if (kerbalEVA != null)
            {
                if (kerbalEVA.IsSeated()) // Leave the seat.
                {
                    var parentPart = part.parent;
                    while (parentPart != null && !parentPart.isKerbalSeat()) // Walk up the part hierarchy to find the seat we're in. It ought to be the first parent?
                    {
                        Debug.Log("DEBUG parent part (" + parentPart.name + ") was not a kerbal seat.");
                        parentPart = parentPart.parent;
                    }
                    if (parentPart.isKerbalSeat())
                    {
                        parentPart.FindModuleImplementing<KerbalSeat>().LeaveSeat(new KSPActionParam(KSPActionGroup.Abort, KSPActionType.Activate));
                    }
                    else
                    {
                        Debug.LogError("[KerbalSafety]: Failed to find the kerbal seat to leave.");
                        return;
                    }
                }
            }
            else // Eject from a cockpit.
            {
                foreach (var part in vessel.parts)
                {
                    if (part.protoModuleCrew.Count > 0)
                    {
                        var kerbals = part.protoModuleCrew.ToList();
                        foreach (var kerbal in kerbals)
                        {
                            KerbalEVA spawned = FlightEVA.fetch.spawnEVA(kerbal, part, part.airlock, true);
                            if (spawned)
                            {
                                Debug.Log("[KerbalSafety]: " + kerbal.displayName + " ejected from " + vessel.vesselName + " at " + vessel.radarAltitude.ToString("0.0") + "m");
                                spawned.autoGrabLadderOnStart = false; // Don't grab the vessel.
                                spawned.vessel.srf_velocity = Vector3.zero;
                                if (ai)
                                {
                                    ai.DeactivatePilot();
                                    StartCoroutine(ApplyEjectionForce(spawned));
                                }
                                LoadedVesselSwitcher.Instance.ForceSwitchVessel(spawned.vessel);
                            }
                            else
                            {
                                // All exits were blocked by something.
                                BDACompetitionMode.Instance.competitionStatus.Add(kerbal.displayName + " failed to eject from " + vessel.vesselName + ", all exits were blocked. RIP.");
                                Debug.Log("[KerbalSafety]: " + kerbal.displayName + " failed to eject from " + vessel.vesselName + ", all exits were blocked. RIP.");
                                if (ai)
                                {
                                    StartCoroutine(ExplodeEverythingElseThenEject());
                                }
                            }
                        }
                    }
                }
            }
            // var kerbalEVAs = vessel.FindPartModulesImplementing<KerbalEVA>();
            ejected = true;
        }

        IEnumerator ApplyEjectionForce(KerbalEVA kerbal)
        {
            Debug.Log("DEBUG zeroing velocity then starting ejection forces for " + kerbal.name);
            kerbal.vessel.srf_velocity = Vector3.zero;
            while (kerbal != null && !kerbal.Ready)
                yield return new WaitForFixedUpdate();
            Debug.Log("DEBUG " + kerbal.name + " is ready");
            if (kerbal != null && kerbal.OnALadder) // Force the kerbal to let go.
            {
                var fsm = kerbal.fsm;
                var letGoEvent = fsm.CurrentState.StateEvents.SingleOrDefault(e => e.name == "Ladder Let Go");
                if (letGoEvent == null) Debug.LogError("[KerbalSafety]: Did not find let go event");
                else
                {
                    Debug.Log("DEBUG running letGoEvent");
                    fsm.RunEvent(letGoEvent);
                }
            }
            Debug.Log("DEBUG Applying force");
            int count = (int)(0.4f / Time.fixedDeltaTime);
            var gee = FlightGlobals.getGeeForceAtPosition(kerbal.vessel.transform.position);
            while (kerbal != null && count-- > 0)
            {
                kerbal.vessel.rootPart.AddForce(-count * gee);
                yield return new WaitForFixedUpdate();
            }
        }

        IEnumerator ExplodeEverythingElseThenEject()
        {
            Debug.Log("DEBUG exploding everything else");
            var partsWithCrew = new List<Part>();
            foreach (var part in vessel.parts)
            {
                if (part.protoModuleCrew.Count == 0)
                {
                    part.explode();
                }
                else
                {
                    partsWithCrew.Add(part);
                }
            }
            yield return new WaitForFixedUpdate();
            foreach (var part in partsWithCrew)
            {
                Eject(part.vessel, null);
            }
        }

        void ResetEjectOnPartCountChange(Vessel v)
        {
            if (v == vessel && !vessel.LandedOrSplashed && ejected)
            {
                ejected = false;
                Debug.Log("DEBUG resetting ejection check for " + vessel.vesselName);
            }
        }

        void EjectOnAboutToDie()
        {
            Debug.Log("DEBUG ejecting from " + vessel.vesselName + " due to about to die.");
            Eject(vessel, this);
        }

        // FIXME move ejected kerbals to their own class so that they continue past when the AI that ejected them dies


        #endregion
    }
}