using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using BDArmory.Core;
using BDArmory.Core.Utils;
using BDArmory.Misc;
using BDArmory.Modules;
using BDArmory.UI;

namespace BDArmory.Competition.VesselSpawning
{
    /// <summary>
    /// Continous spawning in an airborne ring cycling through all the vessels in the spawn folder.
    ///
    /// TODO: This should probably be subsumed into its own spawn strategy eventually.
    /// The central block of the SpawnVesselsContinuouslyCoroutine function should eventually switch to using SingleVesselSpawning.Instance.SpawnVessel (plus local coroutines for the extra stuff) to do the actual spawning of the vessels once that's ready.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class ContinuousSpawning : VesselSpawner
    {
        public static ContinuousSpawning Instance;

        public bool vesselsSpawningContinuously = false;
        int continuousSpawnedVesselCount = 0;
        private string message;

        void Awake()
        {
            if (Instance != null) Destroy(Instance);
            Instance = this;
        }

        public override IEnumerator Spawn(SpawnConfig spawnConfig) => SpawnVesselsContinuouslyAsCoroutine(spawnConfig);

        public void CancelSpawning()
        {
            // Continuous spawn
            if (vesselsSpawningContinuously)
            {
                vesselsSpawningContinuously = false;
                if (continuousSpawningScores != null)
                    DumpContinuousSpawningScores();
                continuousSpawningScores = null;
                message = "Continuous vessel spawning cancelled.";
                Debug.Log("[BDArmory.VesselSpawner]: " + message);
                BDACompetitionMode.Instance.competitionStatus.Add(message);
                BDACompetitionMode.Instance.ResetCompetitionStuff();
            }
            if (spawnVesselsContinuouslyCoroutine != null)
            {
                StopCoroutine(spawnVesselsContinuouslyCoroutine);
                spawnVesselsContinuouslyCoroutine = null;
            }
        }

        public override void PreSpawnInitialisation(SpawnConfig spawnConfig)
        {
            base.PreSpawnInitialisation(spawnConfig);

            vesselsSpawningContinuously = true;
            spawnFailureReason = SpawnFailureReason.None;
            continuousSpawningScores = new Dictionary<string, ContinuousSpawningScores>();
            if (spawnVesselsContinuouslyCoroutine != null)
                StopCoroutine(spawnVesselsContinuouslyCoroutine);
        }

        public void SpawnVesselsContinuously(int worldIndex, double latitude, double longitude, double altitude = 1000, float distance = 20f, bool absDistanceOrFactor = false, bool killEverythingFirst = true, string spawnFolder = null)
        { SpawnVesselsContinuously(new SpawnConfig(worldIndex, latitude, longitude, altitude, distance, absDistanceOrFactor, BDArmorySettings.VESSEL_SPAWN_EASE_IN_SPEED, killEverythingFirst, true, 1, null, null, spawnFolder)); }

        public void SpawnVesselsContinuously(SpawnConfig spawnConfig)
        {
            PreSpawnInitialisation(spawnConfig);
            Debug.Log("[BDArmory.VesselSpawner]: Triggering continuous vessel spawning at " + spawnConfig.latitude.ToString("G6") + ", " + spawnConfig.longitude.ToString("G6") + ", with altitude " + spawnConfig.altitude + "m.");
            spawnVesselsContinuouslyCoroutine = StartCoroutine(SpawnVesselsContinuouslyCoroutine(spawnConfig));
        }

        /// <summary>
        /// A coroutine version of the SpawnAllVesselsContinuously function that performs the required prespawn initialisation.
        /// </summary>
        /// <param name="spawnConfig">The spawn config to use.</param>
        public IEnumerator SpawnVesselsContinuouslyAsCoroutine(SpawnConfig spawnConfig)
        {
            PreSpawnInitialisation(spawnConfig);
            Debug.Log("[BDArmory.VesselSpawner]: Triggering continuous vessel spawning at " + spawnConfig.latitude.ToString("G6") + ", " + spawnConfig.longitude.ToString("G6") + ", with altitude " + spawnConfig.altitude + "m.");
            yield return SpawnVesselsContinuouslyCoroutine(spawnConfig);
        }

        private Coroutine spawnVesselsContinuouslyCoroutine;
        HashSet<Vessel> vesselsToActivate = new HashSet<Vessel>();
        // Spawns all vessels in a downward facing ring and activates them (autopilot and AG10, then stage if no engines are firing), then respawns any that die. An altitude of 1000m should be plenty.
        // Note: initial vessel separation tends towards 2*pi*spawnDistanceFactor from above for >3 vessels.
        private IEnumerator SpawnVesselsContinuouslyCoroutine(SpawnConfig spawnConfig)
        {
            #region Initialisation and sanity checks
            // Tally up the craft to spawn.
            if (spawnConfig.craftFiles == null) // Prioritise the list of craftFiles if we're given them.
                spawnConfig.craftFiles = Directory.GetFiles(Path.Combine(KSPUtil.ApplicationRootPath, "AutoSpawn", spawnConfig.folder)).Where(f => f.EndsWith(".craft")).ToList();
            if (spawnConfig.craftFiles.Count == 0)
            {
                message = "Vessel spawning: found no craft files in " + Path.Combine(KSPUtil.ApplicationRootPath, "AutoSpawn", spawnConfig.folder);
                Debug.Log("[BDArmory.VesselSpawner]: " + message);
                BDACompetitionMode.Instance.competitionStatus.Add(message);
                vesselsSpawningContinuously = false;
                spawnFailureReason = SpawnFailureReason.NoCraft;
                yield break;
            }
            spawnConfig.craftFiles.Shuffle(); // Randomise the spawn order.
            spawnConfig.altitude = Math.Max(100, spawnConfig.altitude); // Don't spawn too low.
            var spawnDistance = spawnConfig.craftFiles.Count > 1 ? (spawnConfig.absDistanceOrFactor ? spawnConfig.distance : spawnConfig.distance * (1 + (BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS > 0 ? Math.Min(spawnConfig.craftFiles.Count, BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS) : spawnConfig.craftFiles.Count))) : 0f; // If it's a single craft, spawn it at the spawn point.
            continuousSpawnedVesselCount = 0; // Reset our spawned vessel count.
            if (BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS == 0)
                message = "Spawning " + spawnConfig.craftFiles.Count + " vessels at an altitude of " + spawnConfig.altitude.ToString("G0") + (spawnConfig.craftFiles.Count > 8 ? "m, this may take some time..." : "m.");
            else
                message = "Spawning " + Math.Min(BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS, spawnConfig.craftFiles.Count) + " of " + spawnConfig.craftFiles.Count + " vessels at an altitude of " + spawnConfig.altitude.ToString("G0") + "m with rolling-spawning.";
            Debug.Log("[BDArmory.VesselSpawner]: " + message);
            if (BDACompetitionMode.Instance) // Reset competition stuff.
            {
                BDACompetitionMode.Instance.competitionStatus.Add(message);
                BDACompetitionMode.Instance.LogResults("due to continuous spawning", "auto-dump-from-spawning"); // Log results first.
                BDACompetitionMode.Instance.StopCompetition();
                BDACompetitionMode.Instance.ResetCompetitionStuff(); // Reset competition scores.
            }
            vesselsToActivate.Clear(); // Clear any pending vessel activations.
            yield return waitForFixedUpdate;
            #endregion

            #region Pre-spawning
            if (spawnConfig.killEverythingFirst)
            {
                yield return SpawnUtils.RemoveAllVessels();
            }
            #endregion

            #region Spawning
            // Get the spawning point in world position coordinates.
            var terrainAltitude = FlightGlobals.currentMainBody.TerrainAltitude(spawnConfig.latitude, spawnConfig.longitude);
            var spawnPoint = FlightGlobals.currentMainBody.GetWorldSurfacePosition(spawnConfig.latitude, spawnConfig.longitude, terrainAltitude + spawnConfig.altitude);
            var radialUnitVector = (spawnPoint - FlightGlobals.currentMainBody.transform.position).normalized;
            Ray ray;
            RaycastHit hit;

            if (spawnConfig.killEverythingFirst)
            {
                // Update the floating origin offset, so that the vessels spawn within range of the physics. The terrain takes several frames to load, so we need to wait for the terrain to settle.
                FloatingOrigin.SetOffset(spawnPoint); // This adjusts local coordinates, such that spawnPoint is (0,0,0).
                SpawnUtils.ShowSpawnPoint(spawnConfig.worldIndex, spawnConfig.latitude, spawnConfig.longitude, spawnConfig.altitude, 2 * spawnDistance, true);

                if (terrainAltitude > 0) // Not over the ocean or on a surfaceless body.
                {
                    // Wait for the terrain to load in before continuing.
                    var testPosition = 1000f * radialUnitVector;
                    var terrainDistance = 1000f + (float)spawnConfig.altitude;
                    var lastTerrainDistance = terrainDistance;
                    var distanceToCoMainBody = (testPosition - FlightGlobals.currentMainBody.transform.position).magnitude;
                    ray = new Ray(testPosition, -radialUnitVector);
                    message = "Waiting up to 10s for terrain to settle.";
                    if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.VesselSpawner]: " + message);
                    BDACompetitionMode.Instance.competitionStatus.Add(message);
                    var startTime = Planetarium.GetUniversalTime();
                    double lastStableTimeStart = startTime;
                    double stableTime = 0;
                    do
                    {
                        lastTerrainDistance = terrainDistance;
                        yield return waitForFixedUpdate;
                        terrainDistance = Physics.Raycast(ray, out hit, 2f * (float)(spawnConfig.altitude + distanceToCoMainBody), (int)LayerMasks.Scenery) ? hit.distance : -1f; // Oceans shouldn't be more than 10km deep...
                        if (terrainDistance < 0f || Math.Abs(lastTerrainDistance - terrainDistance) > 0.1f)
                            lastStableTimeStart = Planetarium.GetUniversalTime(); // Reset the stable time tracker.
                        stableTime = Planetarium.GetUniversalTime() - lastStableTimeStart;
                    } while (Planetarium.GetUniversalTime() - startTime < 10 && stableTime < 1f);
                }
            }
            else if ((spawnPoint - FloatingOrigin.fetch.offset).magnitude > 100e3)
            {
                message = "WARNING The spawn point is " + ((spawnPoint - FloatingOrigin.fetch.offset).magnitude / 1000).ToString("G4") + "km away. Expect vessels to be killed immediately.";
                BDACompetitionMode.Instance.competitionStatus.Add(message);
            }

            var craftURLToVesselName = new Dictionary<string, string>();
            var invalidVesselCount = new Dictionary<string, int>();
            Vector3d craftGeoCoords;
            Vector3 craftSpawnPosition;
            var shipFacility = EditorFacility.None;
            var refDirection = Math.Abs(Vector3.Dot(Vector3.up, radialUnitVector)) < 0.71f ? Vector3.up : Vector3.forward; // Avoid that the reference direction is colinear with the local surface normal.
            var geeDirection = FlightGlobals.getGeeForceAtPosition(Vector3.zero);
            var spawnSlots = OptimiseSpawnSlots(BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS > 0 ? Math.Min(spawnConfig.craftFiles.Count, BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS) : spawnConfig.craftFiles.Count);
            var spawnCounts = spawnConfig.craftFiles.ToDictionary(c => c, c => 0);
            var spawnQueue = new Queue<string>();
            var craftToSpawn = new Queue<string>();
            bool initialSpawn = true;
            double currentUpdateTick;
            while (vesselsSpawningContinuously)
            {
                currentUpdateTick = BDACompetitionMode.Instance.nextUpdateTick;
                // Reacquire the spawn point as the local coordinate system may have changed (floating origin adjustments, local body rotation, etc.).
                spawnPoint = FlightGlobals.currentMainBody.GetWorldSurfacePosition(spawnConfig.latitude, spawnConfig.longitude, terrainAltitude + spawnConfig.altitude);
                radialUnitVector = (spawnPoint - FlightGlobals.currentMainBody.transform.position).normalized;
                // Check if sliders have changed.
                if (spawnSlots.Count != (BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS > 0 ? Math.Min(spawnConfig.craftFiles.Count, BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS) : spawnConfig.craftFiles.Count))
                {
                    spawnSlots = OptimiseSpawnSlots(BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS > 0 ? Math.Min(spawnConfig.craftFiles.Count, BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS) : spawnConfig.craftFiles.Count);
                    continuousSpawnedVesselCount %= spawnSlots.Count;
                }
                // Add any craft that hasn't been spawned or has died to the spawn queue if it isn't already in the queue. Note: we need to also check that the vessel isn't null as Unity makes it a fake null!
                foreach (var craftURL in spawnConfig.craftFiles.Where(craftURL => (BDArmorySettings.VESSEL_SPAWN_LIVES_PER_VESSEL > 0 ? spawnCounts[craftURL] < BDArmorySettings.VESSEL_SPAWN_LIVES_PER_VESSEL : true) && !spawnQueue.Contains(craftURL) && (!craftURLToVesselName.ContainsKey(craftURL) || (BDACompetitionMode.Instance.Scores.Players.Contains(craftURLToVesselName[craftURL]) && BDACompetitionMode.Instance.Scores.ScoreData[craftURLToVesselName[craftURL]].deathTime >= 0))))
                {
                    spawnQueue.Enqueue(craftURL);
                    ++spawnCounts[craftURL];
                }
                LoadedVesselSwitcher.Instance.UpdateList();
                var currentlyActive = LoadedVesselSwitcher.Instance.WeaponManagers.SelectMany(tm => tm.Value).ToList().Count;
                if (spawnQueue.Count + vesselsToActivate.Count == 0 && currentlyActive < 2)// Nothing left to spawn or activate and only 1 vessel surviving. Time to call it quits and let the competition end.
                {
                    message = "Spawn queue is empty and not enough vessels are active, ending competition.";
                    Debug.Log("[BDArmory.VesselSpawner]: " + message);
                    BDACompetitionMode.Instance.StopCompetition();
                    break;
                }
                while (craftToSpawn.Count + vesselsToActivate.Count + currentlyActive < spawnSlots.Count && spawnQueue.Count > 0)
                    craftToSpawn.Enqueue(spawnQueue.Dequeue());
                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                {
                    var missing = spawnConfig.craftFiles.Where(craftURL => craftURLToVesselName.ContainsKey(craftURL) && !craftToSpawn.Contains(craftURL) && !FlightGlobals.Vessels.Where(v => !VesselModuleRegistry.ignoredVesselTypes.Contains(v.vesselType) && VesselModuleRegistry.GetModuleCount<MissileFire>(v) > 0).Select(v => v.vesselName).Contains(craftURLToVesselName[craftURL])).ToList();
                    if (missing.Count > 0)
                    {
                        Debug.Log("[BDArmory.VesselSpawner]: MISSING vessels: " + string.Join(", ", craftURLToVesselName.Where(c => missing.Contains(c.Key)).Select(c => c.Value)));
                    }
                }
                if (craftToSpawn.Count > 0)
                {
                    VesselModuleRegistry.CleanRegistries(); // Clean out any old entries.
                    // Spawn the craft in a downward facing ring.
                    string failedVessels = "";
                    foreach (var craftURL in craftToSpawn)
                    {
                        if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log($"[BDArmory.VesselSpawner]: Spawning vessel from {craftURL}");
                        var heading = 360f * spawnSlots[continuousSpawnedVesselCount] / spawnSlots.Count;
                        var direction = Vector3.ProjectOnPlane(Quaternion.AngleAxis(heading, radialUnitVector) * refDirection, radialUnitVector).normalized;
                        craftSpawnPosition = spawnPoint + spawnDistance * direction;
                        FlightGlobals.currentMainBody.GetLatLonAlt(craftSpawnPosition, out craftGeoCoords.x, out craftGeoCoords.y, out craftGeoCoords.z); // Convert spawn point to geo-coords for the actual spawning function.
                        Vessel vessel = null;
                        try
                        {
                            vessel = VesselLoader.SpawnVesselFromCraftFile(craftURL, craftGeoCoords, 0f, 0f, 0f, out shipFacility); // SPAWN
                        }
                        catch { vessel = null; }
                        if (vessel == null)
                        {
                            var craftName = craftURL.Substring(Path.Combine(KSPUtil.ApplicationRootPath, "AutoSpawn", spawnConfig.folder).Length + 1);
                            Debug.Log("[BDArmory.VesselSpawner]: Failed to spawn craft " + craftName);
                            failedVessels += "\n  -  " + craftName;
                            continue;
                        }
                        vessel.Landed = false; // Tell KSP that it's not landed.
                        vessel.ResumeStaging(); // Trigger staging to resume to get staging icons to work properly.
                        if (!craftURLToVesselName.ContainsKey(craftURL))
                        {
                            if (craftURLToVesselName.ContainsValue(vessel.vesselName)) // Avoid duplicate names.
                            {
                                var count = 1;
                                var potentialName = vessel.vesselName + "_" + count;
                                while (craftURLToVesselName.ContainsValue(potentialName))
                                    potentialName = vessel.vesselName + "_" + (++count);
                                vessel.vesselName = potentialName;
                            }
                            craftURLToVesselName.Add(craftURL, vessel.vesselName); // Store the craftURL -> vessel name.
                        }
                        vessel.vesselName = craftURLToVesselName[craftURL]; // Assign the same (potentially modified) name to the craft each time.
                        // If a competition is active, update the scoring structure.
                        if ((BDACompetitionMode.Instance.competitionStarting || BDACompetitionMode.Instance.competitionIsActive) && !BDACompetitionMode.Instance.Scores.Players.Contains(vessel.vesselName))
                        {
                            BDACompetitionMode.Instance.Scores.AddPlayer(vessel);
                        }
                        if (!vesselsToActivate.Contains(vessel))
                            vesselsToActivate.Add(vessel);
                        if (!continuousSpawningScores.ContainsKey(vessel.vesselName))
                            continuousSpawningScores.Add(vessel.vesselName, new ContinuousSpawningScores());
                        continuousSpawningScores[vessel.vesselName].vessel = vessel; // Update some values in the scoring structure.
                        continuousSpawningScores[vessel.vesselName].outOfAmmoTime = 0;
                        ++continuousSpawnedVesselCount;
                        continuousSpawnedVesselCount %= spawnSlots.Count;
                        Debug.Log("[BDArmory.VesselSpawner]: Vessel " + vessel.vesselName + " spawned!");
                        BDACompetitionMode.Instance.competitionStatus.Add("Spawned " + vessel.vesselName);
                    }
                    craftToSpawn.Clear(); // Clear the queue since we just spawned all those vessels.
                    if (failedVessels != "")
                    {
                        message = "Some vessels failed to spawn, aborting: " + failedVessels;
                        Debug.Log("[BDArmory.VesselSpawner]: " + message);
                        BDACompetitionMode.Instance.competitionStatus.Add(message);
                        spawnFailureReason = SpawnFailureReason.VesselFailedToSpawn;
                        break;
                    }

                    // Wait for a couple of updates so that the spawned vessels' parts list and reference transform gets updated.
                    yield return waitForFixedUpdate;
                    yield return waitForFixedUpdate;

                    // Fix control point orientation by setting the reference transformations to that of the root parts and re-orient the vessels accordingly.
                    foreach (var vessel in vesselsToActivate)
                    {
                        int count = 0;
                        // Sometimes if a vessel camera switch occurs, the craft appears unloaded for a couple of frames. This avoids NREs for control surfaces triggered by the change in reference transform.
                        while (vessel != null && vessel.rootPart != null && (vessel.ReferenceTransform == null || vessel.rootPart.GetReferenceTransform() == null) && ++count < 5) yield return waitForFixedUpdate;
                        if (vessel == null || vessel.rootPart == null)
                        {
                            Debug.Log($"[BDArmory.VesselSpawner]: " + (vessel == null ? "Spawned vessel was destroyed before it could be activated!" : $"{vessel.vesselName} has no root part!"));
                            continue; // In case the vessel got destroyed in the mean time.
                        }
                        vessel.SetReferenceTransform(vessel.rootPart);
                        if (BDArmorySettings.SF_GRAVITY)
                        {
                            vessel.SetRotation(Quaternion.FromToRotation(shipFacility == EditorFacility.SPH ? -vessel.ReferenceTransform.forward : vessel.ReferenceTransform.up, -geeDirection) * vessel.transform.rotation); // Re-orient the vessel to the gravity direction.
                            vessel.SetRotation(Quaternion.AngleAxis(Vector3.SignedAngle(shipFacility == EditorFacility.SPH ? vessel.ReferenceTransform.up : -vessel.ReferenceTransform.forward, vessel.transform.position - spawnPoint, -geeDirection), -geeDirection) * vessel.transform.rotation); // Re-orient the vessel to the point outwards.
                        }
                        else
                        {
                            vessel.SetRotation(Quaternion.FromToRotation(-vessel.ReferenceTransform.up, -geeDirection) * vessel.transform.rotation); // Re-orient the vessel to the local gravity direction.
                            vessel.SetRotation(Quaternion.AngleAxis(Vector3.SignedAngle(-vessel.ReferenceTransform.forward, vessel.transform.position - spawnPoint, -geeDirection), -geeDirection) * vessel.transform.rotation); // Re-orient the vessel to the right direction.
                            vessel.SetRotation(Quaternion.AngleAxis(-10f, vessel.ReferenceTransform.right) * vessel.transform.rotation); // Tilt 10° outwards.
                        }
                        if (BDArmorySettings.SPACE_HACKS)
                        {
                            var SF = vessel.rootPart.FindModuleImplementing<ModuleSpaceFriction>();
                            if (SF == null)
                            {
                                SF = (ModuleSpaceFriction)vessel.rootPart.AddModule("ModuleSpaceFriction");
                            }
                        }
                    }
                }
                // Activate the AI and fire up any new weapon managers that appeared.
                if (vesselsToActivate.Count > 0)
                {
                    // Wait for an update so that the spawned vessels' FindPart... functions have time to have their internal data updated.
                    yield return waitForFixedUpdate;

                    var weaponManagers = LoadedVesselSwitcher.Instance.WeaponManagers.SelectMany(tm => tm.Value).ToList();
                    var vesselsToCheck = vesselsToActivate.ToList(); // Take a copy to avoid modifying the original while iterating over it.
                    foreach (var vessel in vesselsToCheck)
                    {
                        if (!vessel.loaded || vessel.packed) continue;
                        // Make sure the vessel module registry is up to date.
                        VesselModuleRegistry.OnVesselModified(vessel, true);
                        // Check that the vessel is valid.
                        var invalidReason = BDACompetitionMode.Instance.IsValidVessel(vessel);
                        if (invalidReason != BDACompetitionMode.InvalidVesselReason.None)
                        {
                            bool killIt = false;
                            var craftURL = craftURLToVesselName.ToDictionary(i => i.Value, i => i.Key)[vessel.vesselName];
                            if (invalidVesselCount.ContainsKey(craftURL))
                                ++invalidVesselCount[craftURL];
                            else
                                invalidVesselCount.Add(craftURL, 1);
                            if (invalidVesselCount[craftURL] == 3) // After 3 attempts try spawning it again.
                            {
                                message = vessel.vesselName + " is INVALID due to " + invalidReason + ", attempting to respawn it.";
                                craftToSpawn.Enqueue(craftURL); // Re-add the craft to the vessels to spawn.
                                killIt = true;
                            }
                            if (invalidVesselCount[craftURL] > 5) // After 3 more attempts, mark it as defunct.
                            {
                                message = vessel.vesselName + " is STILL INVALID due to " + invalidReason + ", removing it.";
                                spawnConfig.craftFiles.Remove(craftURL);
                                killIt = true;
                            }
                            if (killIt)
                            {
                                BDACompetitionMode.Instance.competitionStatus.Add(message);
                                Debug.Log("[BDArmory.VesselSpawner]: " + message);
                                vesselsToActivate.Remove(vessel);
                                BDACompetitionMode.Instance.Scores.RemovePlayer(vessel.vesselName);
                                SpawnUtils.RemoveVessel(vessel); // Remove the vessel
                            }
                            continue;
                        }

                        // Check if the weapon manager has been added to the weapon managers list.
                        var weaponManager = VesselModuleRegistry.GetModule<MissileFire>(vessel);
                        if (weaponManager != null && weaponManagers.Contains(weaponManager)) // The weapon manager has been added, let's go!
                        {
                            // Activate the vessel with AG10.
                            vessel.ActionGroups.ToggleGroup(BDACompetitionMode.KM_dictAG[10]); // Modular Missiles use lower AGs (1-3) for staging, use a high AG number to not affect them
                            weaponManager.AI.ActivatePilot();
                            weaponManager.AI.CommandTakeOff();
                            if (!BDArmorySettings.NO_ENGINES && SpawnUtils.CountActiveEngines(vessel) == 0) // If the vessel didn't activate their engines on AG10, then activate all their engines and hope for the best.
                            {
                                Debug.Log("[BDArmory.VesselSpawner]: " + vessel.vesselName + " didn't activate engines on AG10! Activating ALL their engines.");
                                SpawnUtils.ActivateAllEngines(vessel);
                            }
                            else if (BDArmorySettings.NO_ENGINES && SpawnUtils.CountActiveEngines(vessel) > 0) // Vessel had some active engines. Turn them off if possible.
                            {
                                SpawnUtils.ActivateAllEngines(vessel, false);
                            }
                            if (BDArmorySettings.TAG_MODE && !string.IsNullOrEmpty(BDACompetitionMode.Instance.Scores.currentlyIT))
                            { weaponManager.SetTeam(BDTeam.Get("NO")); }
                            else
                            {
                                // Assign the vessel to an unassigned team.
                                var currentTeams = weaponManagers.Where(wm => wm != weaponManager).Select(wm => wm.Team).ToHashSet(); // Current teams, excluding us.
                                char team = 'A';
                                while (currentTeams.Contains(BDTeam.Get(team.ToString())))
                                    ++team;
                                weaponManager.SetTeam(BDTeam.Get(team.ToString()));
                            }
                            weaponManager.ForceScan();
                            var craftURL = craftURLToVesselName.ToDictionary(i => i.Value, i => i.Key)[vessel.vesselName];
                            // Enable guard mode if a competition is active.
                            if (BDACompetitionMode.Instance.competitionIsActive)
                                if (!weaponManager.guardMode)
                                    weaponManager.ToggleGuardMode();
                            weaponManager.AI.ReleaseCommand();
                            vessel.altimeterDisplayState = AltimeterDisplayState.AGL;
                            // Adjust BDACompetitionMode's scoring structures.
                            UpdateCompetitionScores(vessel, true);
                            ++continuousSpawningScores[vessel.vesselName].spawnCount;
                            if (invalidVesselCount.ContainsKey(craftURL))// Reset the invalid spawn counter.
                                invalidVesselCount.Remove(craftURL);
                            // Update the ramming information for the new vessel.
                            if (BDACompetitionMode.Instance.rammingInformation != null)
                            { BDACompetitionMode.Instance.AddPlayerToRammingInformation(vessel); }
                            vesselsToActivate.Remove(vessel);
                            SpawnUtils.RevertSpawnLocationCamera(true); // Undo the camera adjustment and reset the camera distance. This has an internal check so that it only occurs once.
                            if (initialSpawn || FlightGlobals.ActiveVessel == null || FlightGlobals.ActiveVessel.state == Vessel.State.DEAD)
                            {
                                initialSpawn = false;
                                LoadedVesselSwitcher.Instance.ForceSwitchVessel(vessel); // Update the camera.
                                FlightCamera.fetch.SetDistance(50);
                            }
                        }
                        if (BDArmorySettings.MUTATOR_MODE)
                        {
                            var MM = vessel.rootPart.FindModuleImplementing<BDAMutator>();
                            if (MM == null)
                            {
                                MM = (BDAMutator)vessel.rootPart.AddModule("BDAMutator");
                            }
                            if (BDArmorySettings.MUTATOR_LIST.Count > 0)
                            {
                                if (BDArmorySettings.MUTATOR_APPLY_GLOBAL) //same mutator for all craft
                                {
                                    MM.EnableMutator(BDACompetitionMode.Instance.currentMutator);
                                }
                                if (!BDArmorySettings.MUTATOR_APPLY_GLOBAL && BDArmorySettings.MUTATOR_APPLY_TIMER) //mutator applied on a per-craft basis
                                {
                                    MM.EnableMutator(); //random mutator
                                }
                            }
                        }
                        if (BDArmorySettings.HACK_INTAKES)
                        {
                            SpawnUtils.HackIntakes(vessel, true);
                        }
                    }
                }

                // Kill off vessels that are out of ammo for too long if we're in continuous spawning mode and a competition is active.
                if (BDACompetitionMode.Instance.competitionIsActive)
                    KillOffOutOfAmmoVessels();

                // Wait for any pending vessel removals.
                while (SpawnUtils.removingVessels)
                    yield return waitForFixedUpdate;

                if (BDACompetitionMode.Instance.competitionIsActive)
                {
                    yield return new WaitUntil(() => Planetarium.GetUniversalTime() > currentUpdateTick); // Wait for the current update tick in BDACompetitionMode so that spawning occurs after checks for dead vessels there.
                    yield return waitForFixedUpdate;
                }
                else
                {
                    yield return new WaitForSeconds(1); // 1s between checks. Nothing much happens if nothing needs spawning.
                }
            }
            #endregion
            vesselsSpawningContinuously = false;
            Debug.Log("[BDArmory.VesselSpawner]: Continuous vessel spawning ended.");
        }

        // Stagger the spawn slots to avoid consecutive craft being launched too close together.
        private List<int> OptimiseSpawnSlots(int slotCount)
        {
            var availableSlots = Enumerable.Range(0, slotCount).ToList();
            if (slotCount < 4) return availableSlots; // Can't do anything about it for < 4 craft.
            var separation = Mathf.CeilToInt(slotCount / 3f); // Start with approximately 120° separation.
            var pos = 0;
            var optimisedSlots = new List<int>();
            while (optimisedSlots.Count < slotCount)
            {
                while (optimisedSlots.Contains(pos)) { ++pos; pos %= slotCount; }
                optimisedSlots.Add(pos);
                pos += separation;
                pos %= slotCount;
            }
            return optimisedSlots;
        }

        #region Scoring
        // For tracking scores across multiple spawns.
        public class ContinuousSpawningScores
        {
            public Vessel vessel; // The vessel.
            public int spawnCount = 0; // The number of times a craft has been spawned.
            public double outOfAmmoTime = 0; // The time the vessel ran out of ammo.
            public Dictionary<int, ScoringData> scoreData = new Dictionary<int, ScoringData>();
            public double cumulativeTagTime = 0;
            public int cumulativeHits = 0;
            public int cumulativeDamagedPartsDueToRamming = 0;
            public int cumulativeDamagedPartsDueToRockets = 0;
            public int cumulativeDamagedPartsDueToMissiles = 0;
        };
        public Dictionary<string, ContinuousSpawningScores> continuousSpawningScores;
        public void UpdateCompetitionScores(Vessel vessel, bool newSpawn = false)
        {
            var vesselName = vessel.vesselName;
            if (!continuousSpawningScores.ContainsKey(vesselName)) return;
            var spawnCount = continuousSpawningScores[vesselName].spawnCount - 1;
            if (spawnCount < 0) return; // Initial spawning after scores were reset.
            var scoreData = continuousSpawningScores[vesselName].scoreData;
            if (BDACompetitionMode.Instance.Scores.Players.Contains(vesselName))
            {
                scoreData[spawnCount] = BDACompetitionMode.Instance.Scores.ScoreData[vesselName]; // Save the Score instance for the vessel.
                if (newSpawn)
                {
                    continuousSpawningScores[vesselName].cumulativeTagTime = scoreData.Sum(kvp => kvp.Value.tagTotalTime);
                    continuousSpawningScores[vesselName].cumulativeHits = scoreData.Sum(kvp => kvp.Value.hits);
                    continuousSpawningScores[vesselName].cumulativeDamagedPartsDueToRamming = scoreData.Sum(kvp => kvp.Value.totalDamagedPartsDueToRamming);
                    continuousSpawningScores[vesselName].cumulativeDamagedPartsDueToRockets = scoreData.Sum(kvp => kvp.Value.totalDamagedPartsDueToRockets);
                    continuousSpawningScores[vesselName].cumulativeDamagedPartsDueToMissiles = scoreData.Sum(kvp => kvp.Value.totalDamagedPartsDueToMissiles);
                    BDACompetitionMode.Instance.Scores.RemovePlayer(vesselName);
                    BDACompetitionMode.Instance.Scores.AddPlayer(vessel);
                    BDACompetitionMode.Instance.Scores.ScoreData[vesselName].lastDamageTime = scoreData[spawnCount].lastDamageTime;
                    BDACompetitionMode.Instance.Scores.ScoreData[vesselName].lastPersonWhoDamagedMe = scoreData[spawnCount].lastPersonWhoDamagedMe;
                }
            }
        }

        public void DumpContinuousSpawningScores(string tag = "")
        {
            var logStrings = new List<string>();

            if (continuousSpawningScores == null || continuousSpawningScores.Count == 0) return;
            foreach (var vesselName in continuousSpawningScores.Keys)
                UpdateCompetitionScores(continuousSpawningScores[vesselName].vessel);
            BDACompetitionMode.Instance.competitionStatus.Add("Dumping scores for competition " + BDACompetitionMode.Instance.CompetitionID.ToString() + (tag != "" ? " " + tag : ""));
            logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]: Dumping Results at " + (int)(Planetarium.GetUniversalTime() - BDACompetitionMode.Instance.competitionStartTime) + "s");
            foreach (var vesselName in continuousSpawningScores.Keys)
            {
                var vesselScore = continuousSpawningScores[vesselName];
                var scoreData = vesselScore.scoreData;
                logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]: Name:" + vesselName);
                logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  DEATHCOUNT:" + scoreData.Values.Where(v => v.deathTime >= 0).Count());
                var deathTimes = string.Join(";", scoreData.Values.Where(v => v.deathTime >= 0).Select(v => v.deathTime.ToString("0.0")));
                if (deathTimes != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  DEATHTIMES:" + deathTimes);
                #region Bullets
                var whoShotMeScores = string.Join(", ", scoreData.Where(kvp => kvp.Value.hitCounts.Count > 0).Select(kvp => kvp.Key + ":" + string.Join(";", kvp.Value.hitCounts.Select(kvp2 => kvp2.Value + ":" + kvp2.Key))));
                if (whoShotMeScores != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  WHOSHOTME:" + whoShotMeScores);
                var whoDamagedMeWithBulletsScores = string.Join(", ", scoreData.Where(kvp => kvp.Value.damageFromGuns.Count > 0).Select(kvp => kvp.Key + ":" + string.Join(";", kvp.Value.damageFromGuns.Select(kvp2 => kvp2.Value.ToString("0.0") + ":" + kvp2.Key))));
                if (whoDamagedMeWithBulletsScores != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  WHODAMAGEDMEWITHBULLETS:" + whoDamagedMeWithBulletsScores);
                #endregion
                #region Rockets
                var whoStruckMeWithRocketsScores = string.Join(", ", scoreData.Where(kvp => kvp.Value.rocketStrikeCounts.Count > 0).Select(kvp => kvp.Key + ":" + string.Join(";", kvp.Value.rocketStrikeCounts.Select(kvp2 => kvp2.Value + ":" + kvp2.Key))));
                if (whoStruckMeWithRocketsScores != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  WHOSTRUCKMEWITHROCKETS:" + whoStruckMeWithRocketsScores);
                var whoPartsHitMeWithRocketsScores = string.Join(", ", scoreData.Where(kvp => kvp.Value.rocketPartDamageCounts.Count > 0).Select(kvp => kvp.Key + ":" + string.Join(";", kvp.Value.rocketPartDamageCounts.Select(kvp2 => kvp2.Value + ":" + kvp2.Key))));
                if (whoPartsHitMeWithRocketsScores != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  WHOPARTSHITMEWITHROCKETS:" + whoPartsHitMeWithRocketsScores);
                var whoDamagedMeWithRocketsScores = string.Join(", ", scoreData.Where(kvp => kvp.Value.damageFromRockets.Count > 0).Select(kvp => kvp.Key + ":" + string.Join(";", kvp.Value.damageFromRockets.Select(kvp2 => kvp2.Value.ToString("0.0") + ":" + kvp2.Key))));
                if (whoDamagedMeWithRocketsScores != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  WHODAMAGEDMEWITHROCKETS:" + whoDamagedMeWithRocketsScores);
                #endregion
                #region Missiles
                var whoStruckMeWithMissilesScores = string.Join(", ", scoreData.Where(kvp => kvp.Value.missileHitCounts.Count > 0).Select(kvp => kvp.Key + ":" + string.Join(";", kvp.Value.missileHitCounts.Select(kvp2 => kvp2.Value + ":" + kvp2.Key))));
                if (whoStruckMeWithMissilesScores != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  WHOSTRUCKMEWITHMISSILES:" + whoStruckMeWithMissilesScores);
                var whoPartsHitMeWithMissilesScores = string.Join(", ", scoreData.Where(kvp => kvp.Value.missilePartDamageCounts.Count > 0).Select(kvp => kvp.Key + ":" + string.Join(";", kvp.Value.missilePartDamageCounts.Select(kvp2 => kvp2.Value + ":" + kvp2.Key))));
                if (whoPartsHitMeWithMissilesScores != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  WHOPARTSHITMEWITHMISSILES:" + whoPartsHitMeWithMissilesScores);
                var whoDamagedMeWithMissilesScores = string.Join(", ", scoreData.Where(kvp => kvp.Value.damageFromMissiles.Count > 0).Select(kvp => kvp.Key + ":" + string.Join(";", kvp.Value.damageFromMissiles.Select(kvp2 => kvp2.Value.ToString("0.0") + ":" + kvp2.Key))));
                if (whoDamagedMeWithMissilesScores != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  WHODAMAGEDMEWITHMISSILES:" + whoDamagedMeWithMissilesScores);
                #endregion
                #region Rams
                var whoRammedMeScores = string.Join(", ", scoreData.Where(kvp => kvp.Value.rammingPartLossCounts.Count > 0).Select(kvp => kvp.Key + ":" + string.Join(";", kvp.Value.rammingPartLossCounts.Select(kvp2 => kvp2.Value + ":" + kvp2.Key))));
                if (whoRammedMeScores != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  WHORAMMEDME:" + whoRammedMeScores);
                #endregion
                #region Kills
                var GMKills = string.Join(", ", scoreData.Where(kvp => kvp.Value.gmKillReason != GMKillReason.None).Select(kvp => kvp.Key + ":" + kvp.Value.gmKillReason));
                if (GMKills != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  GMKILL:" + GMKills);
                var specialKills = new HashSet<AliveState> { AliveState.CleanKill, AliveState.HeadShot, AliveState.KillSteal }; // FIXME expand these to the separate special kill types
                var cleanKills = string.Join(", ", scoreData.Where(kvp => specialKills.Contains(kvp.Value.aliveState) && kvp.Value.lastDamageWasFrom == DamageFrom.Guns).Select(kvp => kvp.Key + ":" + kvp.Value.lastPersonWhoDamagedMe));
                if (cleanKills != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  CLEANKILL:" + cleanKills);
                var cleanFrags = string.Join(", ", scoreData.Where(kvp => specialKills.Contains(kvp.Value.aliveState) && kvp.Value.lastDamageWasFrom == DamageFrom.Rockets).Select(kvp => kvp.Key + ":" + kvp.Value.lastPersonWhoDamagedMe));
                if (cleanFrags != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  CLEANFRAG:" + cleanFrags);
                var cleanRams = string.Join(", ", scoreData.Where(kvp => specialKills.Contains(kvp.Value.aliveState) && kvp.Value.lastDamageWasFrom == DamageFrom.Ramming).Select(kvp => kvp.Key + ":" + kvp.Value.lastPersonWhoDamagedMe));
                if (cleanRams != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  CLEANRAM:" + cleanRams);
                var cleanMissileKills = string.Join(", ", scoreData.Where(kvp => specialKills.Contains(kvp.Value.aliveState) && kvp.Value.lastDamageWasFrom == DamageFrom.Missiles).Select(kvp => kvp.Key + ":" + kvp.Value.lastPersonWhoDamagedMe));
                if (cleanMissileKills != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  CLEANMISSILEKILL:" + cleanMissileKills);
                #endregion
                var accuracy = string.Join(", ", scoreData.Select(kvp => kvp.Key + ":" + kvp.Value.hits + "/" + kvp.Value.shotsFired + ":" + kvp.Value.rocketStrikes + "/" + kvp.Value.rocketsFired));
                if (accuracy != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  ACCURACY:" + accuracy);
                if (BDArmorySettings.TAG_MODE)
                {
                    if (scoreData.Sum(kvp => kvp.Value.tagScore) > 0) logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  TAGSCORE:" + string.Join(", ", scoreData.Where(kvp => kvp.Value.tagScore > 0).Select(kvp => kvp.Key + ":" + kvp.Value.tagScore.ToString("0.0"))));
                    if (scoreData.Sum(kvp => kvp.Value.tagTotalTime) > 0) logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  TIMEIT:" + string.Join(", ", scoreData.Where(kvp => kvp.Value.tagTotalTime > 0).Select(kvp => kvp.Key + ":" + kvp.Value.tagTotalTime.ToString("0.0"))));
                    if (scoreData.Sum(kvp => kvp.Value.tagKillsWhileIt) > 0) logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  KILLSWHILEIT:" + string.Join(", ", scoreData.Where(kvp => kvp.Value.tagKillsWhileIt > 0).Select(kvp => kvp.Key + ":" + kvp.Value.tagKillsWhileIt)));
                    if (scoreData.Sum(kvp => kvp.Value.tagTimesIt) > 0) logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  TIMESIT:" + string.Join(", ", scoreData.Where(kvp => kvp.Value.tagTimesIt > 0).Select(kvp => kvp.Key + ":" + kvp.Value.tagTimesIt)));
                }
            }

            // Dump the log results to a file.
            if (BDACompetitionMode.Instance.CompetitionID > 0)
            {
                var folder = Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "BDArmory", "Logs");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);
                File.WriteAllLines(Path.Combine(folder, BDACompetitionMode.Instance.CompetitionID.ToString() + (tag != "" ? "-" + tag : "") + ".log"), logStrings);
            }
        }
        #endregion

        public void KillOffOutOfAmmoVessels()
        {
            if (BDArmorySettings.OUT_OF_AMMO_KILL_TIME < 0) return; // Never
            var now = Planetarium.GetUniversalTime();
            Vessel vessel;
            MissileFire weaponManager;
            ContinuousSpawningScores score;
            foreach (var vesselName in continuousSpawningScores.Keys)
            {
                score = continuousSpawningScores[vesselName];
                vessel = score.vessel;
                if (vessel == null) continue; // Vessel hasn't been respawned yet.
                weaponManager = VesselModuleRegistry.GetModule<MissileFire>(vessel);
                if (weaponManager == null) continue; // Weapon manager hasn't registered yet.
                if (score.outOfAmmoTime == 0 && !weaponManager.HasWeaponsAndAmmo())
                    score.outOfAmmoTime = Planetarium.GetUniversalTime();
                if (score.outOfAmmoTime > 0 && now - score.outOfAmmoTime > BDArmorySettings.OUT_OF_AMMO_KILL_TIME)
                {
                    var m = "Killing off " + vesselName + " as they exceeded the out-of-ammo kill time.";
                    BDACompetitionMode.Instance.competitionStatus.Add(m);
                    Debug.Log("[BDArmory.VesselSpawner]: " + m);
                    BDACompetitionMode.Instance.Scores.RegisterDeath(vesselName, GMKillReason.OutOfAmmo); // Indicate that it was us who killed it and remove any "clean" kills.
                    SpawnUtils.RemoveVessel(vessel);
                }
            }
        }
    }
}