using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using BDArmory.Core;
using BDArmory.Core.Utils;
using BDArmory.Modules;
using BDArmory.Misc;
using BDArmory.UI;

namespace BDArmory.Competition.VesselSpawning
{
    /// <summary>
    /// Spawning of a group of craft in a ring.
    /// 
    /// This is the default spawning code for RWP competitions currently and is essentially what the CircularSpawnStrategy needs to perform before it can take over as the default.
    /// 
    /// TODO:
    /// The central block of the SpawnAllVesselsOnce function should eventually switch to using SingleVesselSpawning.Instance.SpawnVessel (plus local coroutines for the extra stuff) to do the actual spawning of the vessels once that's ready.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class CircularSpawning : VesselSpawner
    {
        public static CircularSpawning Instance;

        void Awake()
        {
            if (Instance)
                Destroy(Instance);
            Instance = this;
        }

        private int spawnedVesselCount = 0;
        private string message;

        public override IEnumerator Spawn(SpawnConfig spawnConfig) => SpawnAllVesselsOnceAsCoroutine(spawnConfig);
        public void CancelSpawning()
        {
            // Single spawn
            if (vesselsSpawning)
            {
                vesselsSpawning = false;
                message = "Vessel spawning cancelled.";
                BDACompetitionMode.Instance.competitionStatus.Add(message);
                Debug.Log("[BDArmory.CircularSpawning]: " + message);
            }
            if (spawnAllVesselsOnceCoroutine != null)
            {
                StopCoroutine(spawnAllVesselsOnceCoroutine);
                spawnAllVesselsOnceCoroutine = null;
            }

            // Continuous single spawn
            if (vesselsSpawningOnceContinuously)
            {
                vesselsSpawningOnceContinuously = false;
                message = "Continuous single spawning cancelled.";
                Debug.Log("[BDArmory.CircularSpawning]: " + message);
                BDACompetitionMode.Instance.competitionStatus.Add(message);
            }
            if (spawnAllVesselsOnceContinuouslyCoroutine != null)
            {
                StopCoroutine(spawnAllVesselsOnceContinuouslyCoroutine);
                spawnAllVesselsOnceContinuouslyCoroutine = null;
            }
        }

        #region Single spawning
        /// <summary>
        /// Prespawn initialisation to handle camera and body changes and to ensure that only a single spawning coroutine is running.
        /// Note: This currently has some specifics to the SpawnAllVesselsOnceCoroutine, so it may not be suitable for other spawning strategies yet.
        /// </summary>
        /// <param name="spawnConfig">The spawn config for the new spawning.</param>
        public override void PreSpawnInitialisation(SpawnConfig spawnConfig)
        {
            base.PreSpawnInitialisation(spawnConfig);

            vesselsSpawning = true; // Signal that we've started the spawning vessels routine.
            vesselSpawnSuccess = false; // Set our success flag to false for now.
            spawnFailureReason = SpawnFailureReason.None;
            if (spawnAllVesselsOnceCoroutine != null)
                StopCoroutine(spawnAllVesselsOnceCoroutine);
        }

        public void SpawnAllVesselsOnce(int worldIndex, double latitude, double longitude, double altitude = 0, float distance = 10f, bool absDistanceOrFactor = false, float easeInSpeed = 1f, bool killEverythingFirst = true, bool assignTeams = true, int numberOfTeams = 0, List<int> teamCounts = null, List<List<string>> teamsSpecific = null, string spawnFolder = null, List<string> craftFiles = null)
        {
            SpawnAllVesselsOnce(new SpawnConfig(worldIndex, latitude, longitude, altitude, distance, absDistanceOrFactor, easeInSpeed, killEverythingFirst, assignTeams, numberOfTeams, teamCounts, teamsSpecific, spawnFolder, craftFiles));
        }

        public void SpawnAllVesselsOnce(SpawnConfig spawnConfig)
        {
            PreSpawnInitialisation(spawnConfig);
            spawnAllVesselsOnceCoroutine = StartCoroutine(SpawnAllVesselsOnceCoroutine(spawnConfig));
            Debug.Log("[BDArmory.CircularSpawning]: Triggering vessel spawning at " + spawnConfig.latitude.ToString("G6") + ", " + spawnConfig.longitude.ToString("G6") + ", with altitude " + spawnConfig.altitude + "m.");
        }

        /// <summary>
        /// A coroutine version of the SpawnAllVesselsOnce function that performs the required prespawn initialisation.
        /// </summary>
        /// <param name="spawnConfig">The spawn config to use.</param>
        public IEnumerator SpawnAllVesselsOnceAsCoroutine(SpawnConfig spawnConfig)
        {
            PreSpawnInitialisation(spawnConfig);
            Debug.Log("[BDArmory.CircularSpawning]: Triggering vessel spawning at " + spawnConfig.latitude.ToString("G6") + ", " + spawnConfig.longitude.ToString("G6") + ", with altitude " + spawnConfig.altitude + "m.");
            yield return SpawnAllVesselsOnceCoroutine(spawnConfig);
        }

        private Coroutine spawnAllVesselsOnceCoroutine;
        // Spawns all vessels in an outward facing ring and lowers them to the ground. An altitude of 5m should be suitable for most cases.
        private IEnumerator SpawnAllVesselsOnceCoroutine(int worldIndex, double latitude, double longitude, double altitude, float spawnDistanceFactor, bool absDistanceOrFactor, float easeInSpeed, bool killEverythingFirst = true, bool assignTeams = true, int numberOfTeams = 0, List<int> teamCounts = null, List<List<string>> teamsSpecific = null, string folder = null, List<string> craftFiles = null)
        {
            yield return SpawnAllVesselsOnceCoroutine(new SpawnConfig(worldIndex, latitude, longitude, altitude, spawnDistanceFactor, absDistanceOrFactor, easeInSpeed, killEverythingFirst, assignTeams, numberOfTeams, teamCounts, teamsSpecific, folder, craftFiles));
        }
        private IEnumerator SpawnAllVesselsOnceCoroutine(SpawnConfig spawnConfig)
        {
            #region Initialisation and sanity checks
            // Tally up the craft to spawn.
            if (spawnConfig.teamsSpecific == null)
            {
                if (spawnConfig.numberOfTeams == 1) // Scan subfolders
                {
                    spawnConfig.teamsSpecific = new List<List<string>>();
                    var teamDirs = Directory.GetDirectories(Path.Combine(KSPUtil.ApplicationRootPath, "AutoSpawn", spawnConfig.folder));
                    if (teamDirs.Length == 0) // Make teams from each vessel in the spawn folder.
                    {
                        spawnConfig.numberOfTeams = -1; // Flag for treating craft files as folder names.
                        spawnConfig.craftFiles = Directory.GetFiles(Path.Combine(KSPUtil.ApplicationRootPath, "AutoSpawn", spawnConfig.folder)).Where(f => f.EndsWith(".craft")).ToList();
                        spawnConfig.teamsSpecific = spawnConfig.craftFiles.Select(f => new List<string> { f }).ToList();
                    }
                    else
                    {
                        var stripStartCount = Path.Combine(KSPUtil.ApplicationRootPath, "AutoSpawn").Length;
                        Debug.Log("[BDArmory.CircularSpawning]: Spawning teams from folders " + string.Join(", ", teamDirs.Select(d => d.Substring(stripStartCount))));
                        foreach (var teamDir in teamDirs)
                        {
                            spawnConfig.teamsSpecific.Add(Directory.GetFiles(teamDir).Where(f => f.EndsWith(".craft")).ToList());
                        }
                        spawnConfig.craftFiles = spawnConfig.teamsSpecific.SelectMany(v => v.ToList()).ToList();
                    }
                }
                else // Just the specified folder.
                {
                    if (spawnConfig.craftFiles == null) // Prioritise the list of craftFiles if we're given them.
                        spawnConfig.craftFiles = Directory.GetFiles(Path.Combine(KSPUtil.ApplicationRootPath, "AutoSpawn", spawnConfig.folder)).Where(f => f.EndsWith(".craft")).ToList();
                }
            }
            else // Spawn the specific vessels.
            {
                spawnConfig.craftFiles = spawnConfig.teamsSpecific.SelectMany(v => v.ToList()).ToList();
            }
            if (spawnConfig.craftFiles.Count == 0)
            {
                message = "Vessel spawning: found no craft files in " + Path.Combine(KSPUtil.ApplicationRootPath, "AutoSpawn", spawnConfig.folder);
                Debug.Log("[BDArmory.CircularSpawning]: " + message);
                BDACompetitionMode.Instance.competitionStatus.Add(message);
                vesselsSpawning = false;
                spawnFailureReason = SpawnFailureReason.NoCraft;
                yield break;
            }
            bool useOriginalTeamNames = spawnConfig.assignTeams && (spawnConfig.numberOfTeams == 1 || spawnConfig.numberOfTeams == -1); // We'll be using the folders or craft filenames as team names in the originalTeams dictionary.
            if (spawnConfig.teamsSpecific != null && !useOriginalTeamNames)
            {
                spawnConfig.teamCounts = spawnConfig.teamsSpecific.Select(tl => tl.Count).ToList();
            }
            if (BDArmorySettings.VESSEL_SPAWN_RANDOM_ORDER) spawnConfig.craftFiles.Shuffle(); // Randomise the spawn order.
            spawnedVesselCount = 0; // Reset our spawned vessel count.
            message = "Spawning " + spawnConfig.craftFiles.Count + " vessels at an altitude of " + spawnConfig.altitude.ToString("G0") + "m" + (spawnConfig.craftFiles.Count > 8 ? ", this may take some time..." : ".");
            Debug.Log("[BDArmory.CircularSpawning]: " + message);
            var spawnAirborne = spawnConfig.altitude > 10;
            var spawnDistance = spawnConfig.craftFiles.Count > 1 ? (spawnConfig.absDistanceOrFactor ? spawnConfig.distance : (spawnConfig.distance + spawnConfig.distance * spawnConfig.craftFiles.Count)) : 0f; // If it's a single craft, spawn it at the spawn point.
            if (BDACompetitionMode.Instance) // Reset competition stuff.
            {
                BDACompetitionMode.Instance.competitionStatus.Add(message);
                BDACompetitionMode.Instance.LogResults("due to spawning", "auto-dump-from-spawning"); // Log results first.
                BDACompetitionMode.Instance.StopCompetition();
                BDACompetitionMode.Instance.ResetCompetitionStuff(); // Reset competition scores.
                BDACompetitionMode.Instance.RemoveDebrisNow(); // Remove debris and space junk.
            }
            yield return waitForFixedUpdate;
            #endregion

            #region Pre-spawning
            if (spawnConfig.killEverythingFirst)
            {
                var vesselsToKill = FlightGlobals.Vessels.ToList();
                // Spawn in the SpawnProbe at the camera position and switch to it so that we can clean up the other vessels properly.
                var dummyVar = EditorFacility.None;
                Vector3d dummySpawnCoords;
                FlightGlobals.currentMainBody.GetLatLonAlt(FlightCamera.fetch.transform.position + 100f * (FlightCamera.fetch.transform.position - FlightGlobals.currentMainBody.transform.position).normalized, out dummySpawnCoords.x, out dummySpawnCoords.y, out dummySpawnCoords.z);
                if (SpawnUtils.spawnProbeLocation == null) yield break;
                Vessel spawnProbe = VesselLoader.SpawnVesselFromCraftFile(SpawnUtils.spawnProbeLocation, dummySpawnCoords, 0f, 0f, 0f, out dummyVar);
                if (spawnProbe == null)
                {
                    message = "Failed to spawn SpawnProbe!";
                    BDACompetitionMode.Instance.competitionStatus.Add(message);
                    Debug.LogError("[BDArmory.CircularSpawning]: " + message);
                    yield break;
                }
                spawnProbe.Landed = false; // Tell KSP that it's not landed so KSP doesn't mess with its position.
                yield return new WaitWhile(() => spawnProbe != null && (!spawnProbe.loaded || spawnProbe.packed));
                while (spawnProbe != null && FlightGlobals.ActiveVessel != spawnProbe)
                {
                    LoadedVesselSwitcher.Instance.ForceSwitchVessel(spawnProbe);
                    yield return waitForFixedUpdate;
                }
                // Kill all other vessels (including debris).
                foreach (var vessel in vesselsToKill)
                    SpawnUtils.RemoveVessel(vessel);
                // Finally, remove the SpawnProbe
                SpawnUtils.RemoveVessel(spawnProbe);
                SpawnUtils.originalTeams.Clear();
            }
            while (SpawnUtils.removingVessels)
                yield return waitForFixedUpdate;
            #endregion

            #region Spawning
            // Get the spawning point in world position coordinates.
            var terrainAltitude = FlightGlobals.currentMainBody.TerrainAltitude(spawnConfig.latitude, spawnConfig.longitude);
            var spawnPoint = FlightGlobals.currentMainBody.GetWorldSurfacePosition(spawnConfig.latitude, spawnConfig.longitude, terrainAltitude + spawnConfig.altitude);
            var radialUnitVector = (spawnPoint - FlightGlobals.currentMainBody.transform.position).normalized;
            var localSurfaceNormal = radialUnitVector;
            Ray ray;
            RaycastHit hit;

            if (spawnConfig.killEverythingFirst)
            {
                // Update the floating origin offset, so that the vessels spawn within range of the physics.
                FloatingOrigin.SetOffset(spawnPoint); // This adjusts local coordinates, such that spawnPoint is (0,0,0).
                SpawnUtils.ShowSpawnPoint(spawnConfig.worldIndex, spawnConfig.latitude, spawnConfig.longitude, spawnConfig.altitude, 2 * spawnDistance, true);
                // Re-acquire the spawning point after the floating origin shift.
                terrainAltitude = FlightGlobals.currentMainBody.TerrainAltitude(spawnConfig.latitude, spawnConfig.longitude);
                spawnPoint = FlightGlobals.currentMainBody.GetWorldSurfacePosition(spawnConfig.latitude, spawnConfig.longitude, terrainAltitude + spawnConfig.altitude);
                radialUnitVector = (spawnPoint - FlightGlobals.currentMainBody.transform.position).normalized;

                if (terrainAltitude > 0) // Not over the ocean or on a surfaceless body.
                {
                    // Wait for the terrain to load in before continuing.
                    var testPosition = spawnPoint + 1000f * radialUnitVector;
                    var terrainDistance = 1000f + (float)spawnConfig.altitude;
                    var lastTerrainDistance = terrainDistance;
                    var distanceToCoMainBody = (testPosition - FlightGlobals.currentMainBody.transform.position).magnitude;
                    ray = new Ray(testPosition, -radialUnitVector);
                    message = "Waiting up to 10s for terrain to settle.";
                    if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.CircularSpawning]: " + message);
                    BDACompetitionMode.Instance.competitionStatus.Add(message);
                    var startTime = Planetarium.GetUniversalTime();
                    double lastStableTimeStart = startTime;
                    double stableTime = 0;
                    do
                    {
                        lastTerrainDistance = terrainDistance;
                        yield return waitForFixedUpdate;
                        terrainDistance = Physics.Raycast(ray, out hit, 2f * (float)(spawnConfig.altitude + distanceToCoMainBody), (int)LayerMasks.Scenery) ? hit.distance : -1f; // Oceans shouldn't be more than 10km deep...
                        if (terrainDistance < 0f) // Raycast is failing to find terrain.
                        {
                            if (Planetarium.GetUniversalTime() - startTime < 1) continue; // Give the terrain renderer a chance to spawn the terrain.
                            else break;
                        }
                        if (Math.Abs(lastTerrainDistance - terrainDistance) > 0.1f)
                            lastStableTimeStart = Planetarium.GetUniversalTime(); // Reset the stable time tracker.
                        stableTime = Planetarium.GetUniversalTime() - lastStableTimeStart;
                    } while (Planetarium.GetUniversalTime() - startTime < 10 && stableTime < 1f);
                    if (terrainDistance < 0)
                    {
                        if (!spawnAirborne)
                        {
                            message = "Failed to find terrain at the spawning point! Try increasing the spawn altitude.";
                            Debug.Log("[BDArmory.CircularSpawning]: " + message);
                            BDACompetitionMode.Instance.competitionStatus.Add(message);
                            vesselsSpawning = false;
                            spawnFailureReason = SpawnFailureReason.NoTerrain;
                            yield break;
                        }
                    }
                    else
                    {
                        spawnPoint = hit.point + (float)spawnConfig.altitude * hit.normal;
                        localSurfaceNormal = hit.normal;
                    }
                }
            }
            else if ((spawnPoint - FloatingOrigin.fetch.offset).magnitude > 100e3)
            {
                message = "WARNING The spawn point is " + ((spawnPoint - FloatingOrigin.fetch.offset).magnitude / 1000).ToString("G4") + "km away. Expect vessels to be killed immediately.";
                BDACompetitionMode.Instance.competitionStatus.Add(message);
            }

            // Spawn the craft in an outward facing ring.
            Debug.Log("[BDArmory.CircularSpawning]: Spawning vessels...");
            var spawnedVessels = new Dictionary<string, Tuple<Vessel, Vector3d, Vector3, float, EditorFacility>>();
            Vector3d craftGeoCoords;
            Vector3 craftSpawnPosition;
            var refDirection = Math.Abs(Vector3.Dot(Vector3.up, radialUnitVector)) < 0.71f ? Vector3.up : Vector3.forward; // Avoid that the reference direction is colinear with the local surface normal.
            string failedVessels = "";
            var shipFacility = EditorFacility.None;
            List<List<string>> teamVesselNames = null;
            if (spawnConfig.teamsSpecific == null)
            {
                foreach (var craftUrl in spawnConfig.craftFiles) // First spawn the vessels in the air.
                {
                    var heading = 360f * spawnedVesselCount / spawnConfig.craftFiles.Count;
                    var direction = Vector3.ProjectOnPlane(Quaternion.AngleAxis(heading, radialUnitVector) * refDirection, radialUnitVector).normalized;
                    craftSpawnPosition = spawnPoint + 1000f * radialUnitVector + spawnDistance * direction; // Spawn 1000m higher than asked for, then adjust the altitude later once the craft's loaded.
                    FlightGlobals.currentMainBody.GetLatLonAlt(craftSpawnPosition, out craftGeoCoords.x, out craftGeoCoords.y, out craftGeoCoords.z); // Convert spawn point to geo-coords for the actual spawning function.
                    Vessel vessel = null;
                    try
                    {
                        vessel = VesselLoader.SpawnVesselFromCraftFile(craftUrl, craftGeoCoords, 0f, 0f, 0f, out shipFacility); // SPAWN
                    }
                    catch { vessel = null; }
                    if (vessel == null)
                    {
                        var craftName = craftUrl.Substring(Path.Combine(KSPUtil.ApplicationRootPath, "AutoSpawn", spawnConfig.folder).Length + 1);
                        Debug.LogWarning("[BDArmory.CircularSpawning]: Failed to spawn craft " + craftName);
                        failedVessels += "\n  -  " + craftName;
                        continue;
                    }
                    vessel.Landed = false; // Tell KSP that it's not landed so KSP doesn't mess with its position.
                    vessel.ResumeStaging(); // Trigger staging to resume to get staging icons to work properly.
                    if (spawnedVessels.ContainsKey(vessel.vesselName))
                    {
                        var count = 1;
                        var potentialName = vessel.vesselName + "_" + count;
                        while (spawnedVessels.ContainsKey(potentialName))
                            potentialName = vessel.vesselName + "_" + (++count);
                        vessel.vesselName = potentialName;
                    }
                    spawnedVessels.Add(vessel.vesselName, new Tuple<Vessel, Vector3d, Vector3, float, EditorFacility>(vessel, craftSpawnPosition, direction, vessel.GetHeightFromTerrain() - 35f, shipFacility)); // Store the vessel, its spawning point (which is different from its position) and height from the terrain!
                    ++spawnedVesselCount;
                }
            }
            else
            {
                teamVesselNames = new List<List<string>>();
                var currentTeamNames = new List<string>();
                int spawnedTeamCount = 0;
                Vector3 teamSpawnPosition;
                foreach (var team in spawnConfig.teamsSpecific)
                {
                    currentTeamNames.Clear();
                    var teamHeading = 360f * spawnedTeamCount / spawnConfig.teamsSpecific.Count;
                    var teamDirection = Vector3.ProjectOnPlane(Quaternion.AngleAxis(teamHeading, radialUnitVector) * refDirection, radialUnitVector).normalized;
                    teamSpawnPosition = spawnPoint + 1000f * radialUnitVector + spawnDistance * teamDirection; // Spawn 1000m hight than asked for, then adjust the altitude later once the craft's loaded.
                    int teamSpawnCount = 0;
                    foreach (var craftUrl in team)
                    {
                        var heading = 360f / team.Count * (teamSpawnCount - (team.Count - 1) / 2f) / team.Count + teamHeading;
                        var direction = Vector3.ProjectOnPlane(Quaternion.AngleAxis(heading, radialUnitVector) * refDirection, radialUnitVector).normalized;
                        craftSpawnPosition = teamSpawnPosition + spawnDistance / 4f * direction; // Spawn in clusters around the team spawn points.
                        FlightGlobals.currentMainBody.GetLatLonAlt(craftSpawnPosition, out craftGeoCoords.x, out craftGeoCoords.y, out craftGeoCoords.z); // Convert spawn point to geo-coords for the actual spawning function.
                        Vessel vessel = null;
                        try
                        {
                            vessel = VesselLoader.SpawnVesselFromCraftFile(craftUrl, craftGeoCoords, 0f, 0f, 0f, out shipFacility); // SPAWN
                        }
                        catch { vessel = null; }
                        if (vessel == null)
                        {
                            var craftName = craftUrl.Substring(Path.Combine(KSPUtil.ApplicationRootPath, "AutoSpawn", spawnConfig.folder).Length + 1);
                            Debug.Log("[BDArmory.CircularSpawning]: Failed to spawn craft " + craftName);
                            failedVessels += "\n  -  " + craftName;
                            continue;
                        }
                        vessel.Landed = false; // Tell KSP that it's not landed so KSP doesn't mess with its position.
                        vessel.ResumeStaging(); // Trigger staging to resume to get staging icons to work properly.
                        if (spawnedVessels.ContainsKey(vessel.vesselName))
                        {
                            var count = 1;
                            var potentialName = vessel.vesselName + "_" + count;
                            while (spawnedVessels.ContainsKey(potentialName))
                                potentialName = vessel.vesselName + "_" + (++count);
                            vessel.vesselName = potentialName;
                        }
                        currentTeamNames.Add(vessel.vesselName);
                        if (spawnConfig.assignTeams)
                        {
                            switch (spawnConfig.numberOfTeams)
                            {
                                case 1: // Assign team names based on folders.
                                    {
                                        var paths = craftUrl.Split(Path.DirectorySeparatorChar);
                                        SpawnUtils.originalTeams[vessel.vesselName] = paths[paths.Length - 2];
                                        break;
                                    }
                                case -1: // Assign team names based on craft filename. We can't use vessel name as that can get adjusted above to avoid conflicts.
                                    {
                                        var paths = craftUrl.Split(Path.DirectorySeparatorChar);
                                        SpawnUtils.originalTeams[vessel.vesselName] = paths[paths.Length - 1].Substring(0, paths[paths.Length - 1].Length - 6);
                                        break;
                                    }
                            }
                        }
                        spawnedVessels.Add(vessel.vesselName, new Tuple<Vessel, Vector3d, Vector3, float, EditorFacility>(vessel, craftSpawnPosition, direction, vessel.GetHeightFromTerrain() - 35f, shipFacility)); // Store the vessel, its spawning point (which is different from its position) and height from the terrain!
                        ++spawnedVesselCount;
                        ++teamSpawnCount;
                    }
                    teamVesselNames.Add(currentTeamNames.ToList());
                    ++spawnedTeamCount;
                }
            }
            if (failedVessels != "")
            {
                message = "Some vessels failed to spawn: " + failedVessels;
                BDACompetitionMode.Instance.competitionStatus.Add(message);
                Debug.Log("[BDArmory.CircularSpawning]: " + message);
            }

            // Wait for an update so that the vessels' parts list gets updated.
            yield return waitForFixedUpdate;

            // Count the vessels' parts for checking later.
            var spawnedVesselPartCounts = new Dictionary<string, int>();
            foreach (var vesselName in spawnedVessels.Keys)
                spawnedVesselPartCounts.Add(vesselName, spawnedVessels[vesselName].Item1.parts.Count);

            // Wait another update so that the reference transforms get updated.
            yield return waitForFixedUpdate;

            // Now rotate them and put them at the right altitude.
            var finalSpawnPositions = new Dictionary<string, Vector3d>();
            var finalSpawnRotations = new Dictionary<string, Quaternion>();
            {
                var startTime = Time.time;
                // Sometimes if a vessel camera switch occurs, the craft appears unloaded for a couple of frames. This avoids NREs for control surfaces triggered by the change in reference transform.
                while (spawnedVessels.Values.All(vessel => vessel.Item1 != null && (vessel.Item1.ReferenceTransform == null || vessel.Item1.rootPart == null || vessel.Item1.rootPart.GetReferenceTransform() == null)) && (Time.time - startTime < 1f)) yield return waitForFixedUpdate;
                var nullVessels = spawnedVessels.Where(kvp => kvp.Value.Item1 == null).Select(kvp => kvp.Key).ToList();
                if (nullVessels.Count() > 0)
                {
                    message = string.Join(", ", nullVessels) + " disappeared during spawning!";
                    Debug.Log("[BDArmory.CircularSpawning]: " + message);
                    BDACompetitionMode.Instance.competitionStatus.Add(message);
                    vesselsSpawning = false;
                    spawnFailureReason = SpawnFailureReason.VesselLostParts;
                    yield break;
                }
                var noRootVessels = spawnedVessels.Where(kvp => kvp.Value.Item1.rootPart == null).Select(kvp => kvp.Key).ToList();
                if (noRootVessels.Count() > 0)
                {
                    message = string.Join(", ", noRootVessels) + " had no root part during spawning!";
                    Debug.Log("[BDArmory.CircularSpawning]: " + message);
                    BDACompetitionMode.Instance.competitionStatus.Add(message);
                    vesselsSpawning = false;
                    spawnFailureReason = SpawnFailureReason.VesselLostParts;
                    yield break;
                }
                foreach (var vessel in spawnedVessels.Values.Select(sv => sv.Item1))
                {
                    vessel.SetReferenceTransform(vessel.rootPart); // Set the reference transform to the root part's transform.
                }
            }
            foreach (var vesselName in spawnedVessels.Keys)
            {
                var vessel = spawnedVessels[vesselName].Item1;
                if (vessel == null)
                {
                    message = "A vessel disappeared during spawning!";
                    Debug.Log("[BDArmory.CircularSpawning]: " + message);
                    BDACompetitionMode.Instance.competitionStatus.Add(message);
                    vesselsSpawning = false;
                    spawnFailureReason = SpawnFailureReason.VesselLostParts;
                    yield break;
                }
                craftSpawnPosition = spawnedVessels[vesselName].Item2;
                var direction = spawnedVessels[vesselName].Item3;
                var heightFromTerrain = spawnedVessels[vesselName].Item4;
                shipFacility = spawnedVessels[vesselName].Item5;
                var localRadialUnitVector = (craftSpawnPosition - FlightGlobals.currentMainBody.transform.position).normalized;
                ray = new Ray(craftSpawnPosition, -localRadialUnitVector);
                var distanceToCoMainBody = (craftSpawnPosition - FlightGlobals.currentMainBody.transform.position).magnitude;
                float distance;
                if (terrainAltitude > 0 && Physics.Raycast(ray, out hit, (float)(spawnConfig.altitude + distanceToCoMainBody), (int)LayerMasks.Scenery))
                {
                    distance = hit.distance;
                    localSurfaceNormal = hit.normal;
                    if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.CircularSpawning]: found terrain for spawn adjustments");
                }
                else
                {
                    distance = FlightGlobals.getAltitudeAtPos(craftSpawnPosition) - (float)terrainAltitude; // If the raycast fails or we're spawning over water, use the value from FlightGlobals and terrainAltitude of the original spawn point.
                    if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.CircularSpawning]: failed to find terrain for spawn adjustments");
                }

                if (!spawnAirborne || BDArmorySettings.SF_GRAVITY) //spawn parallel to ground if surface or space spawning
                {
                    vessel.SetRotation(Quaternion.FromToRotation(shipFacility == EditorFacility.SPH ? -vessel.ReferenceTransform.forward : vessel.ReferenceTransform.up, localSurfaceNormal) * vessel.transform.rotation); // Re-orient the vessel to the terrain normal.
                    vessel.SetRotation(Quaternion.AngleAxis(Vector3.SignedAngle(shipFacility == EditorFacility.SPH ? vessel.ReferenceTransform.up : -vessel.ReferenceTransform.forward, direction, localSurfaceNormal), localSurfaceNormal) * vessel.transform.rotation); // Re-orient the vessel to the right direction.
                }
                else
                {
                    vessel.SetRotation(Quaternion.FromToRotation(-vessel.ReferenceTransform.up, localRadialUnitVector) * vessel.transform.rotation); // Re-orient the vessel to the local gravity direction.
                    vessel.SetRotation(Quaternion.AngleAxis(Vector3.SignedAngle(-vessel.ReferenceTransform.forward, direction, localRadialUnitVector), localRadialUnitVector) * vessel.transform.rotation); // Re-orient the vessel to the right direction.
                    vessel.SetRotation(Quaternion.AngleAxis(-10f, vessel.ReferenceTransform.right) * vessel.transform.rotation); // Tilt 10° outwards.
                }
                finalSpawnRotations[vesselName] = vessel.transform.rotation;
                if (FlightGlobals.currentMainBody.hasSolidSurface)
                    finalSpawnPositions[vesselName] = craftSpawnPosition + localRadialUnitVector * (float)(spawnConfig.altitude + heightFromTerrain - distance);
                else
                    finalSpawnPositions[vesselName] = craftSpawnPosition - 1000f * localRadialUnitVector;
                if (vessel.mainBody.ocean) // Check for being under water.
                {
                    var distanceUnderWater = -FlightGlobals.getAltitudeAtPos(finalSpawnPositions[vesselName]);
                    if (distanceUnderWater >= 0) // Under water, move the vessel to the surface.
                    {
                        vessel.Splashed = true; // Set the vessel as splashed.
                    }
                }
                vessel.SetPosition(finalSpawnPositions[vesselName]);
                if (BDArmorySettings.SPACE_HACKS)
                {
                    var SF = vessel.rootPart.FindModuleImplementing<ModuleSpaceFriction>();
                    if (SF == null)
                    {
                        SF = (ModuleSpaceFriction)vessel.rootPart.AddModule("ModuleSpaceFriction");
                    }
                }
                if (BDArmorySettings.MUTATOR_MODE)
                {
                    var MM = vessel.rootPart.FindModuleImplementing<BDAMutator>();
                    if (MM == null)
                    {
                        MM = (BDAMutator)vessel.rootPart.AddModule("BDAMutator");
                    }
                }
                if (BDArmorySettings.HACK_INTAKES)
                {
                    SpawnUtils.HackIntakes(vessel, true);
                }
                Debug.Log("[BDArmory.CircularSpawning]: Vessel " + vessel.vesselName + " spawned!");
            }
            #endregion

            #region Post-spawning
            yield return waitForFixedUpdate;
            // Revert the camera and focus on one as it lowers to the terrain.
            SpawnUtils.RevertSpawnLocationCamera(true);
            if ((FlightGlobals.ActiveVessel == null || FlightGlobals.ActiveVessel.state == Vessel.State.DEAD) && spawnedVessels.Count > 0)
            {
                LoadedVesselSwitcher.Instance.ForceSwitchVessel(spawnedVessels.First().Value.Item1); // Update the camera.
                FlightCamera.fetch.SetDistance(50);
            }

            // Validate vessels and wait for weapon managers to be registered.
            var postSpawnCheckStartTime = Planetarium.GetUniversalTime();
            var allWeaponManagersAssigned = false;
            var vesselsToCheck = spawnedVessels.Select(v => v.Value.Item1).ToList();
            if (vesselsToCheck.Count > 0)
            {
                List<Tuple<string, BDACompetitionMode.InvalidVesselReason>> invalidVessels;
                // Check that the spawned vessels are valid craft
                do
                {
                    yield return waitForFixedUpdate;
                    invalidVessels = vesselsToCheck.Select(vessel => new Tuple<string, BDACompetitionMode.InvalidVesselReason>(vessel.vesselName, BDACompetitionMode.Instance.IsValidVessel(vessel))).Where(t => t.Item2 != BDACompetitionMode.InvalidVesselReason.None).ToList();
                } while (invalidVessels.Count > 0 && Planetarium.GetUniversalTime() - postSpawnCheckStartTime < 1); // Give it up to 1s for KSP to populate the vessel's AI and WM.
                if (invalidVessels.Count > 0)
                {
                    BDACompetitionMode.Instance.competitionStatus.Add("The following vessels are invalid:\n - " + string.Join("\n - ", invalidVessels.Select(t => t.Item1 + " : " + t.Item2)));
                    Debug.Log("[BDArmory.CircularSpawning]: Invalid vessels: " + string.Join(", ", invalidVessels.Select(t => t.Item1 + ":" + t.Item2)));
                    spawnFailureReason = SpawnFailureReason.InvalidVessel;
                }
                else
                {
                    do
                    {
                        yield return waitForFixedUpdate;
                        CheckForRenamedVessels(spawnedVessels);

                        // Check that none of the vessels have lost parts.
                        if (spawnedVessels.Any(kvp => kvp.Value.Item1 == null || kvp.Value.Item1.parts.Count < spawnedVesselPartCounts[kvp.Key]))
                        {
                            var offendingVessels = spawnedVessels.Where(kvp => kvp.Value.Item1.parts.Count < spawnedVesselPartCounts[kvp.Key]);
                            message = "One of the vessels lost parts after spawning: " + string.Join(", ", offendingVessels.Select(kvp => kvp.Value.Item1 != null ? kvp.Value.Item1.vesselName : null));
                            BDACompetitionMode.Instance.competitionStatus.Add(message);
                            Debug.Log("[BDArmory.CircularSpawning]: " + message);
                            spawnFailureReason = SpawnFailureReason.VesselLostParts;
                            break;
                        }

                        // Wait for all the weapon managers to be added to LoadedVesselSwitcher.
                        LoadedVesselSwitcher.Instance.UpdateList();
                        var weaponManagers = LoadedVesselSwitcher.Instance.WeaponManagers.SelectMany(tm => tm.Value).ToList();
                        foreach (var vessel in vesselsToCheck.ToList())
                        {
                            var weaponManager = VesselModuleRegistry.GetModule<MissileFire>(vessel);
                            if (weaponManager != null && weaponManagers.Contains(weaponManager)) // The weapon manager has been added, let's go!
                            {
                                // Turn on the brakes.
                                spawnedVessels[vessel.vesselName].Item1.ActionGroups.SetGroup(KSPActionGroup.Brakes, false); // Disable them first to make sure they trigger on toggling.
                                spawnedVessels[vessel.vesselName].Item1.ActionGroups.SetGroup(KSPActionGroup.Brakes, true);

                                vesselsToCheck.Remove(vessel);
                            }
                        }
                        if (vesselsToCheck.Count == 0)
                            allWeaponManagersAssigned = true;

                        if (allWeaponManagersAssigned)
                        {
                            if (spawnConfig.numberOfTeams != 1 && spawnConfig.numberOfTeams != -1) // Already assigned.
                                SpawnUtils.SaveTeams();
                            break;
                        }
                    } while (Planetarium.GetUniversalTime() - postSpawnCheckStartTime < 10); // Give it up to 10s for the weapon managers to get added to the LoadedVesselSwitcher's list.
                    if (!allWeaponManagersAssigned && spawnFailureReason == SpawnFailureReason.None)
                    {
                        BDACompetitionMode.Instance.competitionStatus.Add("Timed out waiting for weapon managers to appear in the Vessel Switcher.");
                        spawnFailureReason = SpawnFailureReason.TimedOut;
                    }
                }
            }

            // Reset craft positions and rotations as sometimes KSP packs and unpacks vessels between frames and resets things!
            foreach (var vesselName in spawnedVessels.Keys)
            {
                if (spawnedVessels[vesselName].Item1 == null) continue;
                spawnedVessels[vesselName].Item1.SetPosition(finalSpawnPositions[vesselName]);
                spawnedVessels[vesselName].Item1.SetRotation(finalSpawnRotations[vesselName]);
            }

            if (allWeaponManagersAssigned)
            {
                if (spawnConfig.altitude >= 0 && !spawnAirborne)
                {
                    // Prevent the vessels from falling too fast and check if their velocities in the surface normal direction is below a threshold.
                    var vesselsHaveLanded = spawnedVessels.Keys.ToDictionary(v => v, v => (int)0); // 1=started moving, 2=landed.
                    var landingStartTime = Planetarium.GetUniversalTime();
                    do
                    {
                        yield return waitForFixedUpdate;
                        foreach (var vesselName in spawnedVessels.Keys)
                        {
                            var vessel = spawnedVessels[vesselName].Item1;
                            if (vessel.LandedOrSplashed && Utils.GetRadarAltitudeAtPos(vessel.transform.position) <= 0) // Wait for the vessel to settle a bit in the water. The 15s buffer should be more than sufficient.
                            {
                                vesselsHaveLanded[vesselName] = 2;
                            }
                            if (vesselsHaveLanded[vesselName] == 0 && Vector3.Dot(vessel.srf_velocity, radialUnitVector) < 0) // Check that vessel has started moving.
                                vesselsHaveLanded[vesselName] = 1;
                            if (vesselsHaveLanded[vesselName] == 1 && Vector3.Dot(vessel.srf_velocity, radialUnitVector) >= 0) // Check if the vessel has landed.
                            {
                                vesselsHaveLanded[vesselName] = 2;
                                if (Utils.GetRadarAltitudeAtPos(vessel.transform.position) > 0)
                                    vessel.Landed = true; // Tell KSP that the vessel is landed.
                                else
                                    vessel.Splashed = true; // Tell KSP that the vessel is splashed.
                            }
                            if (vesselsHaveLanded[vesselName] == 1 && vessel.srf_velocity.sqrMagnitude > spawnConfig.easeInSpeed) // While the vessel hasn't landed, prevent it from moving too fast.
                                vessel.SetWorldVelocity(0.99 * spawnConfig.easeInSpeed * vessel.srf_velocity); // Move at VESSEL_SPAWN_EASE_IN_SPEED m/s at most.
                        }

                        // Check that none of the vessels have lost parts.
                        if (spawnedVessels.Any(kvp => kvp.Value.Item1.parts.Count < spawnedVesselPartCounts[kvp.Key]))
                        {
                            var offendingVessels = spawnedVessels.Where(kvp => kvp.Value.Item1.parts.Count < spawnedVesselPartCounts[kvp.Key]);
                            message = "One of the vessels lost parts after spawning: " + string.Join(", ", offendingVessels.Select(kvp => kvp.Value.Item1 != null ? kvp.Value.Item1.vesselName : null));
                            BDACompetitionMode.Instance.competitionStatus.Add(message);
                            spawnFailureReason = SpawnFailureReason.VesselLostParts;
                            break;
                        }

                        if (vesselsHaveLanded.Values.All(v => v == 2))
                        {
                            vesselSpawnSuccess = true;
                            message = "Vessel spawning SUCCEEDED!";
                            BDACompetitionMode.Instance.competitionStatus.Add(message);
                            break;
                        }
                    } while (Planetarium.GetUniversalTime() - landingStartTime < 15 + spawnConfig.altitude / spawnConfig.easeInSpeed); // Give the vessels up to (15 + altitude / VESSEL_SPAWN_EASE_IN_SPEED) seconds to land.
                    if (!vesselSpawnSuccess && spawnFailureReason == SpawnFailureReason.None)
                    {
                        BDACompetitionMode.Instance.competitionStatus.Add("Timed out waiting for the vessels to land.");
                        spawnFailureReason = SpawnFailureReason.TimedOut;
                    }
                }
                else
                {
                    // Check that none of the vessels have lost parts.
                    if (spawnedVessels.Any(kvp => kvp.Value.Item1.parts.Count < spawnedVesselPartCounts[kvp.Key]))
                    {
                        var offendingVessels = spawnedVessels.Where(kvp => kvp.Value.Item1.parts.Count < spawnedVesselPartCounts[kvp.Key]);
                        message = "One of the vessels lost parts after spawning: " + string.Join(", ", offendingVessels.Select(kvp => kvp.Value.Item1 != null ? kvp.Value.Item1.vesselName : null));
                        BDACompetitionMode.Instance.competitionStatus.Add(message);
                        spawnFailureReason = SpawnFailureReason.VesselLostParts;
                    }
                    else
                    {
                        foreach (var vessel in spawnedVessels.Select(v => v.Value.Item1))
                        {
                            var weaponManager = VesselModuleRegistry.GetModule<MissileFire>(vessel);
                            if (!weaponManager) continue; // Safety check in case the vessel got destroyed.

                            // Activate the vessel with AG10, or failing that, staging.
                            vessel.ActionGroups.ToggleGroup(BDACompetitionMode.KM_dictAG[10]); // Modular Missiles use lower AGs (1-3) for staging, use a high AG number to not affect them
                            weaponManager.AI.ActivatePilot();
                            weaponManager.AI.CommandTakeOff();
                            if (weaponManager.guardMode)
                            {
                                Debug.Log($"[BDArmory.CircularSpawning]: Disabling guardMode on {vessel.vesselName}.");
                                weaponManager.ToggleGuardMode(); // Disable guard mode (in case someone enabled it on AG10 or in the SPH).
                                weaponManager.SetTarget(null);
                            }

                            if (!BDArmorySettings.NO_ENGINES && SpawnUtils.CountActiveEngines(vessel) == 0) // If the vessel didn't activate their engines on AG10, then activate all their engines and hope for the best.
                            {
                                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.CircularSpawning]: " + vessel.vesselName + " didn't activate engines on AG10! Activating ALL their engines.");
                                SpawnUtils.ActivateAllEngines(vessel);
                            }
                            else if (BDArmorySettings.NO_ENGINES && SpawnUtils.CountActiveEngines(vessel) > 0) // Vessel had some active engines. Turn them off if possible.
                            {
                                SpawnUtils.ActivateAllEngines(vessel, false);
                            }
                        }

                        vesselSpawnSuccess = true;
                    }
                }
                foreach (var vessel in spawnedVessels.Select(v => v.Value.Item1))
                    vessel.altimeterDisplayState = AltimeterDisplayState.AGL;
            }
            if (!vesselSpawnSuccess)
            {
                message = "Vessel spawning FAILED! Reason: " + spawnFailureReason;
                BDACompetitionMode.Instance.competitionStatus.Add(message);
            }
            else
            {
                CheckForRenamedVessels(spawnedVessels);
                if (spawnConfig.assignTeams)
                {
                    // Assign the vessels to teams.
                    Debug.Log("[BDArmory.CircularSpawning]: Assigning vessels to teams.");
                    if (spawnConfig.teamsSpecific == null && spawnConfig.teamCounts == null && spawnConfig.numberOfTeams > 1)
                    {
                        int numberPerTeam = spawnedVesselCount / spawnConfig.numberOfTeams;
                        int residue = spawnedVesselCount - numberPerTeam * spawnConfig.numberOfTeams;
                        spawnConfig.teamCounts = new List<int>();
                        for (int team = 0; team < spawnConfig.numberOfTeams; ++team)
                            spawnConfig.teamCounts.Add(numberPerTeam + (team < residue ? 1 : 0));
                    }
                    LoadedVesselSwitcher.Instance.MassTeamSwitch(true, useOriginalTeamNames, spawnConfig.teamCounts, teamVesselNames);
                    yield return waitForFixedUpdate;
                }
            }

            if (BDArmorySettings.RUNWAY_PROJECT)
            {
                // Check AI/WM counts and placement for RWP.
                foreach (var vesselName in spawnedVessels.Keys)
                {
                    SpawnUtils.CheckAIWMCounts(spawnedVessels[vesselName].Item1);
                    SpawnUtils.CheckAIWMPlacement(spawnedVessels[vesselName].Item1);
                }
            }
            #endregion

            Debug.Log("[BDArmory.CircularSpawning]: Vessel spawning " + (vesselSpawnSuccess ? "SUCCEEDED!" : "FAILED! " + spawnFailureReason));
            vesselsSpawning = false;
        }
        #endregion

        // TODO Continuous Single Spawning and Team Spawning should probably, at some point, be separated into their own spawn strategies that make use of the above spawning functions. Also, they need cleaning up, which would probably remove the need for some of the SpawnAllVesselsOnce variants above.
        #region Continuous Single Spawning
        private bool vesselsSpawningOnceContinuously = false;
        public Coroutine spawnAllVesselsOnceContinuouslyCoroutine = null;

        public void SpawnAllVesselsOnceContinuously(int worldIndex, double latitude, double longitude, double altitude = 0, float distance = 10f, bool absDistanceOrFactor = false, float easeInSpeed = 1f, bool killEverythingFirst = true, bool assignTeams = true, int numberOfTeams = 0, List<int> teamCounts = null, List<List<string>> teamsSpecific = null, string spawnFolder = null, List<string> craftFiles = null)
        {
            SpawnAllVesselsOnceContinuously(new SpawnConfig(worldIndex, latitude, longitude, altitude, distance, absDistanceOrFactor, easeInSpeed, killEverythingFirst, assignTeams, numberOfTeams, teamCounts, teamsSpecific, spawnFolder, craftFiles));
        }
        public void SpawnAllVesselsOnceContinuously(SpawnConfig spawnConfig)
        {
            vesselsSpawningOnceContinuously = true;
            if (spawnAllVesselsOnceContinuouslyCoroutine != null)
                StopCoroutine(spawnAllVesselsOnceContinuouslyCoroutine);
            spawnAllVesselsOnceContinuouslyCoroutine = StartCoroutine(SpawnAllVesselsOnceContinuouslyCoroutine(spawnConfig));
            Debug.Log("[BDArmory.CircularSpawning]: Triggering vessel spawning (continuous single) at " + spawnConfig.latitude.ToString("G6") + ", " + spawnConfig.longitude.ToString("G6") + ", with altitude " + spawnConfig.altitude + "m.");
        }

        public IEnumerator SpawnAllVesselsOnceContinuouslyCoroutine(SpawnConfig spawnConfig)
        {
            while ((vesselsSpawningOnceContinuously) && (BDArmorySettings.VESSEL_SPAWN_CONTINUE_SINGLE_SPAWNING))
            {
                SpawnAllVesselsOnce(spawnConfig);
                while (vesselsSpawning)
                    yield return waitForFixedUpdate;
                if (!vesselSpawnSuccess)
                {
                    vesselsSpawningOnceContinuously = false;
                    yield break;
                }
                yield return waitForFixedUpdate;

                // NOTE: runs in separate coroutine
                BDACompetitionMode.Instance.StartCompetitionMode(BDArmorySettings.COMPETITION_DISTANCE);
                yield return waitForFixedUpdate; // Give the competition start a frame to get going.

                // start timer coroutine for the duration specified in settings UI
                var duration = Core.BDArmorySettings.COMPETITION_DURATION * 60f;
                message = "Starting " + (duration > 0 ? "a " + duration.ToString("F0") + "s" : "an unlimited") + " duration competition.";
                Debug.Log("[BDArmory.CircularSpawning]: " + message);
                BDACompetitionMode.Instance.competitionStatus.Add(message);
                while (BDACompetitionMode.Instance.competitionStarting)
                    yield return waitForFixedUpdate; // Wait for the competition to actually start.
                if (!BDACompetitionMode.Instance.competitionIsActive)
                {
                    var message = "Competition failed to start.";
                    BDACompetitionMode.Instance.competitionStatus.Add(message);
                    Debug.Log("[BDArmory.CircularSpawning]: " + message);
                    vesselsSpawningOnceContinuously = false;
                    yield break;
                }
                while (BDACompetitionMode.Instance.competitionIsActive) // Wait for the competition to finish (limited duration and log dumping is handled directly by the competition now).
                    yield return new WaitForSeconds(1);

                // Wait 10s for any user action
                double startTime = Planetarium.GetUniversalTime();
                if ((vesselsSpawningOnceContinuously) && (BDArmorySettings.VESSEL_SPAWN_CONTINUE_SINGLE_SPAWNING))
                {
                    while ((Planetarium.GetUniversalTime() - startTime) < BDArmorySettings.TOURNAMENT_DELAY_BETWEEN_HEATS)
                    {
                        BDACompetitionMode.Instance.competitionStatus.Add("Waiting " + (BDArmorySettings.TOURNAMENT_DELAY_BETWEEN_HEATS - (Planetarium.GetUniversalTime() - startTime)).ToString("0") + "s, then respawning pilots");
                        yield return new WaitForSeconds(1);
                    }
                }
            }
            vesselsSpawningOnceContinuously = false; // For when VESSEL_SPAWN_CONTINUE_SINGLE_SPAWNING gets toggled.
        }

        /// <summary>
        /// If the VESSELNAMING tag exists in the craft file, then KSP renames the vessel at some point after spawning.
        /// This function checks for renamed vessels and sets the name back to what it was.
        /// This must be called once after a yield, before using vessel.vesselName as an index in spawnedVessels.Keys.
        /// </summary>
        /// <param name="spawnedVessels"></param>
        void CheckForRenamedVessels(Dictionary<string, Tuple<Vessel, Vector3d, Vector3, float, EditorFacility>> spawnedVessels)
        {
            foreach (var vesselName in spawnedVessels.Keys.ToList())
            {
                if (vesselName != spawnedVessels[vesselName].Item1.vesselName)
                {
                    spawnedVessels[vesselName].Item1.vesselName = vesselName;
                }
            }
        }
        #endregion

        #region Team Spawning
        /// <summary>
        /// Spawn multiple groups of vessels using the CircularSpawning using multiple SpawnConfigs.
        /// </summary>
        /// <param name="spawnConfigs"></param>
        /// <param name="startCompetition"></param>
        /// <param name="competitionStartDelay"></param>
        /// <param name="startCompetitionNow"></param>
        public void TeamSpawn(List<SpawnConfig> spawnConfigs, bool startCompetition = false, double competitionStartDelay = 0d, bool startCompetitionNow = false)
        {
            vesselsSpawning = true; // Indicate that vessels are spawning here to avoid timing issues with Update in other modules.
            SpawnUtils.RevertSpawnLocationCamera(true);
            if (teamSpawnCoroutine != null)
                StopCoroutine(teamSpawnCoroutine);
            teamSpawnCoroutine = StartCoroutine(TeamsSpawnCoroutine(spawnConfigs, startCompetition, competitionStartDelay, startCompetitionNow));
        }
        private Coroutine teamSpawnCoroutine;
        public IEnumerator TeamsSpawnCoroutine(List<SpawnConfig> spawnConfigs, bool startCompetition = false, double competitionStartDelay = 0d, bool startCompetitionNow = false)
        {
            bool killAllFirst = true;
            List<int> spawnCounts = new List<int>();
            spawnFailureReason = SpawnFailureReason.None;
            // Spawn each team.
            foreach (var spawnConfig in spawnConfigs)
            {
                vesselsSpawning = true; // Gets set to false each time spawning is finished, so we need to re-enable it again.
                vesselSpawnSuccess = false;
                yield return SpawnAllVesselsOnceCoroutine(spawnConfig.worldIndex, spawnConfig.latitude, spawnConfig.longitude, spawnConfig.altitude, spawnConfig.distance, spawnConfig.absDistanceOrFactor, BDArmorySettings.VESSEL_SPAWN_EASE_IN_SPEED, killAllFirst, false, 0, null, null, spawnConfig.folder, spawnConfig.craftFiles);
                if (!vesselSpawnSuccess)
                {
                    message = "Vessel spawning failed, aborting.";
                    BDACompetitionMode.Instance.competitionStatus.Add(message);
                    Debug.Log("[BDArmory.CircularSpawning]: " + message);
                    yield break;
                }
                spawnCounts.Add(spawnedVesselCount);
                // LoadedVesselSwitcher.Instance.MassTeamSwitch(false); // Reset everyone to team 'A' so that the order doesn't get messed up.
                killAllFirst = false;
            }
            yield return waitForFixedUpdate;
            SpawnUtils.SaveTeams(); // Save the teams in case they've been pre-configured.
            LoadedVesselSwitcher.Instance.MassTeamSwitch(false, false, spawnCounts); // Assign teams based on the groups of spawns. Right click the 'T' to revert to the original team names if they were defined.
            if (startCompetition) // Start the competition.
            {
                var competitionStartDelayStart = Planetarium.GetUniversalTime();
                while (Planetarium.GetUniversalTime() - competitionStartDelayStart < competitionStartDelay - Time.fixedDeltaTime)
                {
                    var timeLeft = competitionStartDelay - (Planetarium.GetUniversalTime() - competitionStartDelayStart);
                    if ((int)(timeLeft - Time.fixedDeltaTime) < (int)timeLeft)
                        BDACompetitionMode.Instance.competitionStatus.Add("Competition starting in T-" + timeLeft.ToString("0") + "s");
                    yield return waitForFixedUpdate;
                }
                BDACompetitionMode.Instance.StartCompetitionMode(BDArmorySettings.COMPETITION_DISTANCE);
                if (startCompetitionNow)
                {
                    yield return waitForFixedUpdate;
                    BDACompetitionMode.Instance.StartCompetitionNow();
                }
            }
        }
        #endregion

    }
}