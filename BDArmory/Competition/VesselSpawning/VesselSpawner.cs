using UnityEngine;
using System.Collections;

using BDArmory.Settings;

namespace BDArmory.Competition.VesselSpawning
{
    /// Base class for VesselSpawner classes so that it can work with spawn strategies.
    public abstract class VesselSpawner : MonoBehaviour
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
    }

    public static class VesselSpawnerStatus
    {
        public static bool vesselsSpawning = false; // Flag for when vessels are being spawned and other things should wait for them to finish being spawned.
        public static bool vesselSpawnSuccess = false; // Flag for whether vessel spawning was successful or not.
        public static SpawnFailureReason spawnFailureReason = SpawnFailureReason.None;
        public static bool inhibitCameraTools => vesselsSpawning; // Flag for CameraTools (currently just checks for vessels being spawned).
    }
}