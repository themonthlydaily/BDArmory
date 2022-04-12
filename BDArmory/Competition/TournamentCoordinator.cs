using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using BDArmory.Competition.OrchestrationStrategies;
using BDArmory.Competition.SpawnStrategies;
using BDArmory.Competition.VesselSpawning;
using BDArmory.Core;

namespace BDArmory.Competition
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class TournamentCoordinator : MonoBehaviour
    {
        public static TournamentCoordinator Instance;
        private SpawnStrategy spawnStrategy;
        private OrchestrationStrategy orchestrator;
        private VesselSpawner vesselSpawner;
        private Coroutine executing = null;
        private Coroutine executingForEach = null;
        public bool IsRunning { get; private set; }

        void Awake()
        {
            if (Instance != null) Destroy(Instance);
            Instance = this;
        }

        public void Configure(SpawnStrategy spawner, OrchestrationStrategy orchestrator, VesselSpawner vesselSpawner)
        {
            this.spawnStrategy = spawner;
            this.orchestrator = orchestrator;
            this.vesselSpawner = vesselSpawner;
        }

        public void Run()
        {
            Stop();
            executing = StartCoroutine(Execute());
        }

        public void Stop()
        {
            if (executing != null)
            {
                StopCoroutine(executing);
                executing = null;
                orchestrator.CleanUp();
            }
        }

        public IEnumerator Execute()
        {
            IsRunning = true;

            // clear all vessels
            yield return SpawnUtils.RemoveAllVessels();

            // first, spawn vessels
            yield return spawnStrategy.Spawn(vesselSpawner);

            if (!spawnStrategy.DidComplete())
            {
                Debug.Log($"[BDArmory.TournamentCoordinator]: TournamentCoordinator spawn failed: {vesselSpawner.spawnFailureReason}");
                yield break;
            }

            // now, hand off to orchestrator
            yield return orchestrator.Execute(null, null);

            IsRunning = false;
        }

        public void RunForEach<T>(List<T> strategies, OrchestrationStrategy orchestrator, VesselSpawner spawner) where T : SpawnStrategy
        {
            StopForEach();
            executingForEach = StartCoroutine(ExecuteForEach(strategies, orchestrator, spawner));
        }

        public void StopForEach()
        {
            if (executingForEach != null)
            {
                StopCoroutine(executingForEach);
                executingForEach = null;
                orchestrator.CleanUp();
            }
        }

        IEnumerator ExecuteForEach<T>(List<T> strategies, OrchestrationStrategy orchestrator, VesselSpawner spawner) where T : SpawnStrategy
        {
            int i = 0;
            foreach (var strategy in strategies)
            {
                Configure(strategy, orchestrator, spawner);
                Run();
                yield return new WaitWhile(() => IsRunning);
                if (++i < strategies.Count())
                {
                    double startTime = Planetarium.GetUniversalTime();
                    while ((Planetarium.GetUniversalTime() - startTime) < BDArmorySettings.TOURNAMENT_DELAY_BETWEEN_HEATS)
                    {
                        BDACompetitionMode.Instance.competitionStatus.Add("Waiting " + (BDArmorySettings.TOURNAMENT_DELAY_BETWEEN_HEATS - (Planetarium.GetUniversalTime() - startTime)).ToString("0") + "s, then running the next round.");
                        yield return new WaitForSeconds(1);
                    }
                }
            }
        }

        public static float canyonSpawnLatitude = 27.97f;
        public static float canyonSpawnLongitude = -39.35f;
        public static List<WaypointFollowingStrategy.Waypoint> BuildCanyonCourse()
        {
            return new List<WaypointFollowingStrategy.Waypoint> {
                new WaypointFollowingStrategy.Waypoint(28.33f, -39.11f, BDArmorySettings.WAYPOINTS_ALTITUDE),
                new WaypointFollowingStrategy.Waypoint(28.83f, -38.06f, BDArmorySettings.WAYPOINTS_ALTITUDE),
                new WaypointFollowingStrategy.Waypoint(29.54f, -38.68f, BDArmorySettings.WAYPOINTS_ALTITUDE),
                new WaypointFollowingStrategy.Waypoint(30.15f, -38.6f, BDArmorySettings.WAYPOINTS_ALTITUDE),
                new WaypointFollowingStrategy.Waypoint(30.83f, -38.87f, BDArmorySettings.WAYPOINTS_ALTITUDE),
                new WaypointFollowingStrategy.Waypoint(30.73f, -39.6f, BDArmorySettings.WAYPOINTS_ALTITUDE),
                new WaypointFollowingStrategy.Waypoint(30.9f, -40.23f, BDArmorySettings.WAYPOINTS_ALTITUDE),
                new WaypointFollowingStrategy.Waypoint(30.83f, -41.26f, BDArmorySettings.WAYPOINTS_ALTITUDE)
            };
        }

        public static float slalomSpawnLatitude = -21.0158f;
        public static float slalomSpawnLongitude = 72.2085f;
        public static Vector3 BuildSlalomSpawnPoint()
        {
            return new Vector3(slalomSpawnLatitude, slalomSpawnLongitude, BDArmorySettings.VESSEL_SPAWN_ALTITUDE);
        }

        public static List<WaypointFollowingStrategy.Waypoint> BuildSlalomCourse()
        {
            return new List<WaypointFollowingStrategy.Waypoint>
            {
                new WaypointFollowingStrategy.Waypoint(-21.0763f, 72.7194f, BDArmorySettings.WAYPOINTS_ALTITUDE),
                new WaypointFollowingStrategy.Waypoint(-21.3509f, 73.7466f, BDArmorySettings.WAYPOINTS_ALTITUDE),
                new WaypointFollowingStrategy.Waypoint(-20.8125f, 73.8125f, BDArmorySettings.WAYPOINTS_ALTITUDE),
                new WaypointFollowingStrategy.Waypoint(-20.6478f, 74.8177f, BDArmorySettings.WAYPOINTS_ALTITUDE),
                new WaypointFollowingStrategy.Waypoint(-20.2468f, 74.5046f, BDArmorySettings.WAYPOINTS_ALTITUDE),
                new WaypointFollowingStrategy.Waypoint(-19.7469f, 75.1252f, BDArmorySettings.WAYPOINTS_ALTITUDE),
                new WaypointFollowingStrategy.Waypoint(-19.2360f, 75.1363f, BDArmorySettings.WAYPOINTS_ALTITUDE),
                new WaypointFollowingStrategy.Waypoint(-18.8954f, 74.6530f, BDArmorySettings.WAYPOINTS_ALTITUDE),
            };
        }

        public static float coastalCircuitSpawnLatitude = -7.7134f;
        public static float coastalCircuitSpawnLongitude = -42.7633f;
        public static Vector3 BuildCoastalCircuitSpawnPoint()
        {
            return new Vector3(coastalCircuitSpawnLatitude, coastalCircuitSpawnLongitude, BDArmorySettings.VESSEL_SPAWN_ALTITUDE);
        }

        public static List<WaypointFollowingStrategy.Waypoint> BuildCoastalCircuit()
        {
            return new List<WaypointFollowingStrategy.Waypoint>
            {
                //LatLng(-8.162842, -42.747803
                //LatLng(-8.673706, -42.74231
                //LatLng(-9.223022, -42.528076
                //LatLng(-9.662476, -43.335571
                //LatLng(-10.673218, -43.341064
                //LatLng(-11.337891, -42.923584
                //LatLng(-10.94152, -42.344937
                //LatLng(-10.859123, -41.867031
                //LatLng(-10.551505, -41.619839
                //LatLng(-10.474601, -41.213345
                //LatLng(-9.694572, -41.284756
                //LatLng(-9.540763, -42.191128
                //LatLng(-9.134269, -42.075772
                new WaypointFollowingStrategy.Waypoint(-8.1628f, -42.7478f, BDArmorySettings.WAYPOINTS_ALTITUDE),
                new WaypointFollowingStrategy.Waypoint(-8.6737f, -42.7423f, BDArmorySettings.WAYPOINTS_ALTITUDE),
                new WaypointFollowingStrategy.Waypoint(-9.2230f, -42.5208f, BDArmorySettings.WAYPOINTS_ALTITUDE),
                new WaypointFollowingStrategy.Waypoint(-9.6624f, -43.3355f, BDArmorySettings.WAYPOINTS_ALTITUDE),
                new WaypointFollowingStrategy.Waypoint(-10.6732f, -43.3410f, BDArmorySettings.WAYPOINTS_ALTITUDE),
                new WaypointFollowingStrategy.Waypoint(-11.3379f, -42.9236f, BDArmorySettings.WAYPOINTS_ALTITUDE),
                new WaypointFollowingStrategy.Waypoint(-10.9415f, -42.3449f, BDArmorySettings.WAYPOINTS_ALTITUDE),
                new WaypointFollowingStrategy.Waypoint(-10.8591f, -41.8670f, BDArmorySettings.WAYPOINTS_ALTITUDE),
                new WaypointFollowingStrategy.Waypoint(-10.5515f, -41.6198f, BDArmorySettings.WAYPOINTS_ALTITUDE),
                new WaypointFollowingStrategy.Waypoint(-10.4746f, -41.2133f, BDArmorySettings.WAYPOINTS_ALTITUDE),
                new WaypointFollowingStrategy.Waypoint(-9.6945f, -41.2847f, BDArmorySettings.WAYPOINTS_ALTITUDE),
                new WaypointFollowingStrategy.Waypoint(-9.5407f, -42.1911f, BDArmorySettings.WAYPOINTS_ALTITUDE),
                new WaypointFollowingStrategy.Waypoint(-9.1342f, -42.0757f, BDArmorySettings.WAYPOINTS_ALTITUDE),
            };
        }
    }
}