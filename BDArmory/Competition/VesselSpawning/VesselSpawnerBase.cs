using UnityEngine;
using System.Collections;

using BDArmory.Settings;
using BDArmory.Utils;

namespace BDArmory.Competition.VesselSpawning
{
    /// Base class for VesselSpawner classes so that it can work with spawn strategies.
    public abstract class VesselSpawnerBase : MonoBehaviour
    {
        public abstract IEnumerator Spawn(SpawnConfig spawnConfig); // AUBRANIUM, this is essentially a kludge to get the VesselSpawner class to be functional with the way that the SpawnStrategy interface is defined.

        public virtual void PreSpawnInitialisation(SpawnConfig spawnConfig)
        {
            //Reset gravity
            if (BDArmorySettings.GRAVITY_HACKS)
            {
                PhysicsGlobals.GraviticForceMultiplier = 1d;
                VehiclePhysics.Gravity.Refresh();
            }

            // If we're on another planetary body, first switch to the proper one.
            if (spawnConfig.worldIndex != FlightGlobals.currentMainBody.flightGlobalsIndex)
            { SpawnUtils.ShowSpawnPoint(spawnConfig.worldIndex, spawnConfig.latitude, spawnConfig.longitude, spawnConfig.altitude, 20); }
        }

        public bool vesselsSpawning { get { return VesselSpawnerStatus.vesselsSpawning; } set { VesselSpawnerStatus.vesselsSpawning = value; } }
        public bool vesselSpawnSuccess { get { return VesselSpawnerStatus.vesselSpawnSuccess; } set { VesselSpawnerStatus.vesselSpawnSuccess = value; } }
        public SpawnFailureReason spawnFailureReason { get { return VesselSpawnerStatus.spawnFailureReason; } set { VesselSpawnerStatus.spawnFailureReason = value; } }
        protected static readonly WaitForFixedUpdate waitForFixedUpdate = new WaitForFixedUpdate();

        protected double terrainAltitude { get; set; }
        protected Vector3d spawnPoint { get; set; }
        protected Vector3d radialUnitVector { get; set; }
        protected Vector3d localSurfaceNormal { get; set; }
        protected IEnumerator WaitForTerrain(SpawnConfig spawnConfig, float viewDistance, bool spawnAirborne)
        {
            // Update the floating origin offset, so that the vessels spawn within range of the physics.
            SpawnUtils.ShowSpawnPoint(spawnConfig.worldIndex, spawnConfig.latitude, spawnConfig.longitude, spawnConfig.altitude, viewDistance, true);
            // Re-acquire the spawning point after the floating origin shift.
            terrainAltitude = FlightGlobals.currentMainBody.TerrainAltitude(spawnConfig.latitude, spawnConfig.longitude);
            spawnPoint = FlightGlobals.currentMainBody.GetWorldSurfacePosition(spawnConfig.latitude, spawnConfig.longitude, terrainAltitude + spawnConfig.altitude);
            radialUnitVector = (spawnPoint - FlightGlobals.currentMainBody.transform.position).normalized;
            localSurfaceNormal = radialUnitVector;
            FloatingOrigin.SetOffset(spawnPoint); // This adjusts local coordinates, such that spawnPoint is (0,0,0), which should hopefully help with collider detection.
            Ray ray;
            RaycastHit hit;

            if (terrainAltitude > 0) // Not over the ocean or on a surfaceless body.
            {
                // Wait for the terrain to load in before continuing.
                var testPosition = spawnPoint + 1000f * radialUnitVector;
                var terrainDistance = 1000f + (float)spawnConfig.altitude;
                var lastTerrainDistance = terrainDistance;
                var distanceToCoMainBody = (testPosition - FlightGlobals.currentMainBody.transform.position).magnitude;
                ray = new Ray(testPosition, -radialUnitVector);
                var message = "Waiting up to 10s for terrain to settle.";
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
                    if (Mathf.Abs(lastTerrainDistance - terrainDistance) > 0.1f)
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
    }

    public static class VesselSpawnerStatus
    {
        public static bool vesselsSpawning = false; // Flag for when vessels are being spawned and other things should wait for them to finish being spawned.
        public static bool vesselSpawnSuccess = false; // Flag for whether vessel spawning was successful or not.
        public static SpawnFailureReason spawnFailureReason = SpawnFailureReason.None;
        public static bool inhibitCameraTools => vesselsSpawning; // Flag for CameraTools (currently just checks for vessels being spawned).
    }
}