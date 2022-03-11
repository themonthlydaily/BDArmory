using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BDArmory.Competition.RemoteOrchestration;
using BDArmory.Modules;
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
        private Vessel vessel;
        private BDModulePilotAI pilot;

        //private double expectedWaypointTraversalDuration = 20.0;
        //private double expectedArrival;
        //private double error = double.MaxValue;
        //private double dError = -1.0;

        public WaypointFollowingStrategy(List<Waypoint> waypoints)
        {
            this.waypoints = waypoints;
        }

        public IEnumerator Execute(BDAScoreClient client, BDAScoreService service)
        {
            Debug.Log("[BDArmory.WaypointFollowingStrategy] Started");
            var startedAt = Planetarium.GetUniversalTime();

            var vessels = LoadedVesselSwitcher.Instance.WeaponManagers.SelectMany(tm => tm.Value).Select(wm => wm.vessel).ToList();
            if( vessels.Any() )
            {
                this.vessel = vessels.First();
            }
            if ( vessel == null )
            {
                Debug.Log("[BDArmory.WaypointFollowingStrategy] Null vessel");
                yield break;
            }

            if( !vessel.loaded )
            {
                Debug.Log("[BDArmory.WaypointFollowingStrategy] Vessel not loaded!");
                yield break;
            }

            this.pilot = VesselModuleRegistry.GetBDModulePilotAI(vessel);
            if( this.pilot == null )
            {
                Debug.Log("[BDArmory.WaypointFollowingStrategy] Failed to acquire pilot");
                yield break;
            }

            var mappedWaypoints = waypoints.Select(e => new Vector3(e.latitude, e.longitude, e.altitude)).ToList();
            Debug.Log(string.Format("[BDArmory.WaypointFollowingStrategy] Setting {0} waypoints", mappedWaypoints.Count));
            this.pilot.SetWaypoints(mappedWaypoints);

            yield return new WaitWhile(() => pilot != null && pilot.IsFlyingWaypoints && !(pilot.vessel.Landed || pilot.vessel.Splashed));

            // AUBRANIUM, this needs to handle the case of pilot being null (if they kill themselves while running the waypoints), which is another reason for the waypoint scoring to use the Scores in BDACompetitionMode.cs
            var endedAt = Planetarium.GetUniversalTime();
            var elapsedTime = endedAt - startedAt;
            var deviation = pilot.GetWaypointScores().Sum();
            var waypointCount = pilot.GetWaypointIndex();
            service.TrackWaypoint(vessel.GetName(), (float)elapsedTime, waypointCount, deviation);

            Debug.Log(string.Format("[BDArmory.WaypointFollowingStrategy] Finished {0}, elapsed={1:0.00}, count={2}, deviation={3:0.00}", vessel.GetName(), elapsedTime, waypointCount, deviation));
        }
    }
}
