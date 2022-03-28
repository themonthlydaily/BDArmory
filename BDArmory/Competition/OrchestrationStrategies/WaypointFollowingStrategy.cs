using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BDArmory.Competition.RemoteOrchestration;
using BDArmory.Modules;
using BDArmory.Core;
using BDArmory.UI;
using UnityEngine;
using BDArmory.Misc;

namespace BDArmory.Competition.OrchestrationStrategies
{
    public class WaypointFollowingStrategy : OrchestrationStrategy
    {
        public class Waypoint
        {
            public float latitude;
            public float longitude;
            public float altitude;
            public Waypoint(float latitude, float longitude, float altitude)
            {
                this.latitude = latitude;
                this.longitude = longitude;
                this.altitude = altitude;
            }
        }

        private List<Waypoint> waypoints;
        private List<BDModulePilotAI> pilots;

        static string ModelPath = "BDArmory/Models/WayPoint/model";

        public WaypointFollowingStrategy(List<Waypoint> waypoints)
        {
            this.waypoints = waypoints;
        }

        public IEnumerator Execute(BDAScoreClient client, BDAScoreService service)
        {
            if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.WaypointFollowingStrategy]: Started");
            pilots = LoadedVesselSwitcher.Instance.WeaponManagers.SelectMany(tm => tm.Value).Select(wm => wm.vessel).Where(v => v != null && v.loaded).Select(v => VesselModuleRegistry.GetBDModulePilotAI(v)).Where(p => p != null).ToList();
            PrepareCompetition();

            // Configure the pilots' waypoints.
            var mappedWaypoints = waypoints.Select(e => new Vector3(e.latitude, e.longitude, e.altitude)).ToList();
            BDACompetitionMode.Instance.competitionStatus.Add($"Starting waypoints competition {BDACompetitionMode.Instance.CompetitionID}.");
            if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log(string.Format("[BDArmory.WaypointFollowingStrategy]: Setting {0} waypoints", mappedWaypoints.Count));
            foreach (var pilot in pilots)
                pilot.SetWaypoints(mappedWaypoints);

            // Wait for the pilots to complete the course.
            var startedAt = Planetarium.GetUniversalTime();
            yield return new WaitWhile(() => pilots.Any(pilot => pilot != null && pilot.weaponManager != null && pilot.IsFlyingWaypoints && !(pilot.vessel.Landed || pilot.vessel.Splashed)));
            var endedAt = Planetarium.GetUniversalTime();

            BDACompetitionMode.Instance.competitionStatus.Add("Waypoints competition finished. Scores:");
            foreach (var player in BDACompetitionMode.Instance.Scores.Players)
            {
                var waypointScores = BDACompetitionMode.Instance.Scores.ScoreData[player].waypointsReached;
                var waypointCount = waypointScores.Count();
                var deviation = waypointScores.Sum(w => w.deviation);
                var elapsedTime = waypointCount == 0 ? 0 : waypointScores.Last().timestamp - waypointScores.First().timestamp;
                if (service != null) service.TrackWaypoint(player, (float)elapsedTime, waypointCount, deviation);

                BDACompetitionMode.Instance.competitionStatus.Add($"  - {player}: Time: {elapsedTime:F1}s, Waypoints reached: {waypointCount}, Deviation: {deviation}");
                Debug.Log(string.Format("[BDArmory.WaypointFollowingStrategy]: Finished {0}, elapsed={1:0.00}, count={2}, deviation={3:0.00}", player, elapsedTime, waypointCount, deviation));
            }

            CleanUp();
        }

        void PrepareCompetition()
        {
            if (BDACompetitionMode.Instance.competitionIsActive) BDACompetitionMode.Instance.StopCompetition(); // Stop any currently active competition.
            BDACompetitionMode.Instance.competitionIsActive = true; // Set the competition as now active so the competition start type is correct.
            BDACompetitionMode.Instance.ResetCompetitionStuff(); // Reset a bunch of stuff related to competitions so they don't interfere.
            BDACompetitionMode.Instance.competitionType = CompetitionType.WAYPOINTS;
            BDACompetitionMode.Instance.Scores.ConfigurePlayers(pilots.Select(p => p.vessel).ToList());
            if (BDArmorySettings.AUTO_ENABLE_VESSEL_SWITCHING)
                LoadedVesselSwitcher.Instance.EnableAutoVesselSwitching(true);
            if (BDArmorySettings.TIME_OVERRIDE && BDArmorySettings.TIME_SCALE != 0)
            { Time.timeScale = BDArmorySettings.TIME_SCALE; }
            Debug.Log("[BDArmory.BDACompetitionMode:" + BDACompetitionMode.Instance.CompetitionID.ToString() + "]: Starting Competition");
            if (BDArmorySettings.WAYPOINTS_VISUALIZE)
            {
                Vector3 previousLocation = FlightGlobals.ActiveVessel.transform.position;
                for (int i = 0; i < waypoints.Count; i++)
                {
                    float terrainAltitude = (float)FlightGlobals.currentMainBody.TerrainAltitude(waypoints[i].latitude, waypoints[i].longitude);
                    Vector3d WorldCoords = VectorUtils.GetWorldSurfacePostion(new Vector3(waypoints[i].latitude, waypoints[i].longitude, waypoints[i].altitude + terrainAltitude), FlightGlobals.currentMainBody);
                    //FlightGlobals.currentMainBody.GetLatLonAlt(new Vector3(waypoints[i].latitude, waypoints[i].longitude, waypoints[i].altitude), out WorldCoords.x, out WorldCoords.y, out WorldCoords.z);
                    var direction = (WorldCoords - previousLocation).normalized;
                    WayPointMarker.CreateWaypoint(WorldCoords, direction, ModelPath);
                    previousLocation = WorldCoords;
                    var location = string.Format("({0:##.###}, {1:##.###}, {2:####}", waypoints[i].latitude, waypoints[i].longitude, waypoints[i].altitude);
                    Debug.Log("[BDArmory.Waypoints]: Creating waypoint marker at  " + " " + location);
                }
            }
        }

        public void CleanUp()
        {
            if (BDACompetitionMode.Instance.competitionIsActive) BDACompetitionMode.Instance.StopCompetition(); // Competition is done, so stop it and do the rest of the book-keeping.

        }
    }

    public class WayPointMarker : MonoBehaviour
    {
        public static ObjectPool WaypointPool;

        public Vector3 Position { get; set; }

        public bool disabled = false;

        static void CreateObjectPool(string ModelPath)
        {
            if (WaypointPool != null) return;
            GameObject WPTemplate = GameDatabase.Instance.GetModel(ModelPath);
            WPTemplate.SetActive(false);
            WPTemplate.AddComponent<WayPointMarker>();
            WaypointPool = ObjectPool.CreateObjectPool(WPTemplate, 10, true, true);
        }

        public static void CreateWaypoint(Vector3 position, Vector3 direction, string ModelPath)
        {
            CreateObjectPool(ModelPath);

            Quaternion rotation = Quaternion.LookRotation(direction);

            GameObject newWayPoint = WaypointPool.GetPooledObject();
            newWayPoint.transform.SetPositionAndRotation(position, rotation);
            WayPointMarker NWP = newWayPoint.GetComponent<WayPointMarker>();
            NWP.Position = position;

            newWayPoint.SetActive(true);
        }
        void Awake()
        {
            transform.parent = FlightGlobals.ActiveVessel.mainBody.transform; //FIXME need to update this to grab worldindex for non-kerbin spawns for custom track building
        }
        private void OnEnable()
        {
            disabled = false;
        }
        void Update()
        {
            if (!gameObject.activeInHierarchy) return;
            if (disabled || !BDACompetitionMode.Instance.competitionIsActive || !HighLogic.LoadedSceneIsFlight)
            {
                gameObject.SetActive(false);
                return;
            }
        }
    }
}
