using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using BDArmory.Competition.RemoteOrchestration;
using BDArmory.Control;
using BDArmory.UI;
using BDArmory.Utils;

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


            // var vessels = VesselSpawner.Instance.spawnedVessels;
            // AUBRANIUM The usual approach to getting the active vessels is via their weapon managers in the LoadedVesselSwitcher (the spawning routines wait for them to appear there as part of ensuring that the vessels actually spawned and are valid), i.e.,
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

            yield return new WaitWhile(() => pilot.IsFlyingWaypoints() && !(pilot.vessel.Landed || pilot.vessel.Splashed));

            var endedAt = Planetarium.GetUniversalTime();
            var elapsedTime = endedAt - startedAt;
            var deviation = pilot.GetWaypointScores().Sum();
            var waypointCount = pilot.GetWaypointIndex();
            service.TrackWaypoint(vessel.GetName(), (float)elapsedTime, waypointCount, deviation);

            Debug.Log(string.Format("[BDArmory.WaypointFollowingStrategy] Finished {0}, elapsed={1:0.00}, count={2}, deviation={3:0.00}", vessel.GetName(), elapsedTime, waypointCount, deviation));
        }

        //private IEnumerator WaitForArrival(Waypoint location)
        //{
        //    Debug.Log(string.Format("[BDArmory.WaypointFollowingStrategy] Waiting for arrival at ({0}, {1}, {2})", location.latitude, location.longitude, location.altitude));

        //    // define arrival expectation
        //    expectedArrival = DateTimeOffset.Now.ToUnixTimeSeconds() + expectedWaypointTraversalDuration;

        //    UpdateDistance(location);
        //    // find the inflection point where the time derivative of distance to target flips to positive
        //    while (DateTimeOffset.Now.ToUnixTimeSeconds() < expectedArrival)
        //    {
        //        yield return new WaitForFixedUpdate();
        //        UpdateDistance(location);
        //        if( dError > 0 )
        //        {
        //            break;
        //        }
        //    }
        //    Debug.Log("[BDArmory.WaypointFollowingStrategy] Arrived");
        //}

        //private void UpdateDistance(Waypoint location)
        //{
        //    // compute pseudo-distance from vessel to location
        //    double dlat = location.latitude - vessel.latitude;
        //    double dlng = location.longitude - vessel.longitude;
        //    double dalt = location.altitude - vessel.radarAltitude;
        //    double newError = dlat * dlat + dlng * dlng + dalt * dalt;

        //    dError = newError - error;
        //    error = newError;
        //}
    }
}
