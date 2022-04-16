using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using BDArmory.Control;
using BDArmory.UI;
using BDArmory.Utils;

namespace BDArmory.Competition.VesselSpawning
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class SingleVesselSpawning : VesselSpawnerBase
    {
        public static SingleVesselSpawning Instance;

        void Awake()
        {
            if (Instance)
                Destroy(Instance);
            Instance = this;
        }

        // AUBRANIUM Most of this class is really more suitable as a suplement to VesselLoader to be used as a work-horse for the individual spawning of the vessels in the other VesselSpawner classes.
        // However, it also needs to be a VesselSpawner derived class in its own right for use in the PointSpawnStrategy.
        public override IEnumerator Spawn(SpawnConfig spawnConfig) => SpawnVessel(spawnConfig.craftFiles.First(), spawnConfig.latitude, spawnConfig.longitude, spawnConfig.altitude); // AUBRANIUM, this is a kludge to get the VesselSpawner class to work with the SpawnStrategy interface.

        public List<Vessel> spawnedVessels = new List<Vessel>();
        // TODO DOCNAPPERS
        // AUBNRANIUM, I haven't managed to check this function properly yet, but here are some notes in any case.
        //   This will need to call most of what's in the main part of the continuous spawning coroutine (lines 1298—1524). (I think this is up-to-date with the current state of the regular single spawning, but there may be one or two edge cases.)
        //   Additionally, if ground spawning is also desired, then a fair bit of what's in the "Post-spawning" region of SpawnAllVesselsOnceCoroutine will also be needed.
        //   Why -0.7f pitch? Also, angles are measured in degrees for the most part in Unity in case this is meant to be -0.7rad.
        public IEnumerator SpawnVessel(string craftUrl, double latitude, double longitude, double altitude, float initialHeading = 0.0f, float initialPitch = -0.7f)
        {
            // AUBRANIUM, this shouldn't use vesselsSpawning as a guard as it's desirable to be able to spawn multiple vessels this way concurrently in a similar manner to how RemoveVessel works.
            if (vesselsSpawning)
            {
                Debug.Log("[BDArmory.VesselSpawner]: Already spawning craft");
                yield break;
            }
            spawnedVessels.Clear();
            vesselsSpawning = true;
            vesselSpawnSuccess = false;
            var terrainAltitude = FlightGlobals.currentMainBody.TerrainAltitude(latitude, longitude);
            var spawnPoint = FlightGlobals.currentMainBody.GetWorldSurfacePosition(latitude, longitude, terrainAltitude + altitude);
            Vector3d craftGeoCoords;
            var shipFacility = EditorFacility.None;
            FlightGlobals.currentMainBody.GetLatLonAlt(spawnPoint, out craftGeoCoords.x, out craftGeoCoords.y, out craftGeoCoords.z); // Convert spawn point to geo-coords for the actual spawning function.
            Vessel vessel = null;
            try
            {
                vessel = VesselSpawner.SpawnVesselFromCraftFile(craftUrl, craftGeoCoords, initialHeading, initialPitch, 0f, out shipFacility); // SPAWN
            }
            catch { vessel = null; }
            if (vessel == null)
            {
                var location = string.Format("({0:##.###}, {1:##.###}, {2:####}", latitude, longitude, altitude);
                Debug.Log("[BDArmory.VesselSpawner]: Failed to spawn craft " + craftUrl + " " + location);
                vesselsSpawning = false;
                yield break;
            }
            vessel.Landed = false;
            vessel.ResumeStaging();

            yield return waitForFixedUpdate;
            yield return waitForFixedUpdate;
            yield return waitForFixedUpdate;

            // wait for loaded vessel switcher
            var result = new List<Vessel> { vessel };
            yield return WaitForLoadedVesselSwitcher(result);
            if (!loadedVesselResult || waitingForLoadedVesselSwitcher)
            {
                Debug.Log("[BDArmory.VesselSpawner]: Timeout waiting for weapon managers");
                vesselsSpawning = false;
                yield break;
            }

            // check for AI
            yield return CheckForAI(result);
            if (!checkResultAI || waitingForAICheck)
            {
                Debug.Log("[BDArmory.VesselSpawner]: Timeout waiting for AIPilot");
                vesselsSpawning = false;
                yield break;
            }

            Debug.Log(string.Format("[BDArmory.VesselSpawner]: Spawned {0} craft successfully!", result.Count));
            spawnedVessels = result;
            vesselsSpawning = false;
            vesselSpawnSuccess = true;
        }


        // TODO DOCNAPPERS: Check whether the following performs the correct procedures that are done in SpawnAllVesselsOnceCoroutine and SpawnVesselsContinuouslyCoroutine as mentioned in the SpawnVessel coroutine above.
        private bool waitingForLoadedVesselSwitcher = false;
        private bool loadedVesselResult = false;
        private IEnumerator WaitForLoadedVesselSwitcher(List<Vessel> vessels)
        {
            waitingForLoadedVesselSwitcher = true;
            loadedVesselResult = false;
            var timeoutAt = Planetarium.GetUniversalTime() + 10.0;
            bool timeoutElapsed = false;
            var remainingVessels = vessels.ToList();
            var checkedVessels = new List<Vessel>();

            while (!timeoutElapsed)
            {
                if (!remainingVessels.Any())
                {
                    break;
                }
                LoadedVesselSwitcher.Instance.UpdateList();
                var weaponManagers = LoadedVesselSwitcher.Instance.WeaponManagers.SelectMany(tm => tm.Value).ToList();

                foreach (var vessel in remainingVessels)
                {
                    if (!vessel.loaded || vessel.packed) continue;
                    VesselModuleRegistry.OnVesselModified(vessel, true);
                    var weaponManager = VesselModuleRegistry.GetModule<MissileFire>(vessel);
                    if (weaponManager != null && weaponManagers.Contains(weaponManager)) // The weapon manager has been added, let's go!
                    {
                        checkedVessels.Add(vessel);
                        vessel.ActionGroups.ToggleGroup(BDACompetitionMode.KM_dictAG[10]); // Modular Missiles use lower AGs (1-3) for staging, use a high AG number to not affect them
                        if (weaponManager.guardMode) weaponManager.ToggleGuardMode(); // Disable guard mode (in case someone enabled it on AG10).
                        var engines = VesselModuleRegistry.GetModules<ModuleEngines>(vessel);
                        if (!engines.Any(engine => engine.EngineIgnited)) // If the vessel didn't activate their engines on AG10, then activate all their engines and hope for the best.
                        {
                            engines.ForEach(e => e.Activate());
                        }
                    }
                }
                checkedVessels.ForEach(e => remainingVessels.Remove(e));
                yield return waitForFixedUpdate;
                timeoutElapsed = Planetarium.GetUniversalTime() > timeoutAt;
            }

            loadedVesselResult = !timeoutElapsed;
            waitingForLoadedVesselSwitcher = false;
        }

        private bool checkResultAI = false;
        private bool waitingForAICheck = false;

        private IEnumerator CheckForAI(List<Vessel> vessels)
        {
            Debug.Log("[BDArmory.VesselSpawner]: Checking for AIPilot");
            waitingForAICheck = true;
            checkResultAI = false;
            bool timeoutElapsed = false;
            var remainingVessels = vessels.ToList();
            var checkedVessels = new List<Vessel>();
            var timeoutAt = Planetarium.GetUniversalTime() + 10.0;


            while (!checkResultAI && !timeoutElapsed)
            {
                // iterate through vessels and check for pilot module on each
                if (remainingVessels.Any())
                {
                    foreach (var vessel in remainingVessels)
                    {
                        if (!vessel.loaded)
                        {
                            Debug.Log("[BDArmory.VesselSpawner] Vessel not loaded");
                            continue;
                        }
                        if (vessel.packed)
                        {
                            Debug.Log("[BDArmory.VesselSpawner] Vessel packed");
                            continue;
                        }
                        var vesselPilot = VesselModuleRegistry.GetBDModulePilotAI(vessel, true);
                        if (vesselPilot != null)
                        {
                            vesselPilot.ActivatePilot();
                            checkedVessels.Add(vessel);
                        }
                    }
                    checkedVessels.ForEach(e => remainingVessels.Remove(e));
                }
                else
                {
                    checkResultAI = true;
                }
                yield return waitForFixedUpdate;
                timeoutElapsed = Planetarium.GetUniversalTime() > timeoutAt;
            }

            waitingForAICheck = false;
        }
    }
}