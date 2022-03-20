using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BDArmory.Competition.RemoteOrchestration;
using BDArmory.Modules;
using BDArmory.Core;
using BDArmory.UI;
using UnityEngine;

namespace BDArmory.Competition
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
            yield return new WaitWhile(() => pilots.Any(pilot => pilot != null && pilot.IsFlyingWaypoints && !(pilot.vessel.Landed || pilot.vessel.Splashed)));
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
            Debug.Log("[BDArmory.BDACompetitionMode:" + BDACompetitionMode.Instance.CompetitionID.ToString() + "]: Starting Competition");
        }

        public void CleanUp()
        {
            if (BDACompetitionMode.Instance.competitionIsActive) BDACompetitionMode.Instance.StopCompetition(); // Competition is done, so stop it and do the rest of the book-keeping.
        }
    }
}
