using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BDArmory.Control;
using UnityEngine;
using static BDArmory.Control.VesselSpawner;

namespace BDArmory.Competition
{
    public class CircularSpawnStrategy : SpawnStrategy
    {
        private VesselSource vesselSource;
        private List<int> vesselIds;
        private double latitude;
        private double longitude;
        private double altitude;
        private float radius;
        private bool success = false;

        public CircularSpawnStrategy(VesselSource vesselSource, List<int> vesselIds, double latitude, double longitude, double altitude, float radius)
        {
            this.vesselSource = vesselSource;
            this.vesselIds = vesselIds;
            this.latitude = latitude;
            this.longitude = longitude;
            this.altitude = altitude;
            this.radius = radius;
        }

        public IEnumerator Spawn(VesselSpawner spawner)
        {
            // use vesselSource to resolve local paths for active vessels
            var craftUrls = vesselIds.Select(e => vesselSource.GetLocalPath(e));
            // spawn all craftUrls in a circle around the center point
            SpawnConfig spawnConfig = new SpawnConfig(
                0,
                latitude,
                longitude,
                altitude,
                radius,
                true,
                craftFiles: new List<string>(craftUrls)
            );
            // AUBRANIUM Don't call these coroutines directly, use the regular functions as they set up other stuff before launching the coroutine (e.g., resetting various variables,
            //  ensuring that only a single instance of the spawning coroutine is running, planetary body switching and camera location).
            // If your planned further changes to the spawning routines include taking care of these things as a pre-spawn step (I expect some of this would be dependent on the spawn strategy being used),
            //  then you could use those updated coroutines with yield return statements, in which case the "while (spawner.vesselsSpawning)" loop would become unnecessary
            // yield return spawner.SpawnAllVesselsOnceCoroutine(spawnConfig);
            spawner.SpawnAllVesselsOnce(spawnConfig);

            yield return new WaitWhile(() => spawner.vesselsSpawning);
            // AUBRANIUM The above wait function is more efficient since it avoids allocating a new WaitForFixedUpdate each frame.
            // Another alternative that I've used in several places is to create a "var wait = new WaitForFixedUpdate();" variable and just yield it repeatedly in different locations within a function.
            // while (spawner.vesselsSpawning)
            // {
            //     yield return new WaitForFixedUpdate();
            // }

            if (!spawner.vesselSpawnSuccess)
            {
                Debug.Log("[BDArmory.BDAScoreService] Vessel spawning failed.");
                yield break;
            }

            success = true;
        }

        public bool DidComplete()
        {
            return success;
        }
    }
}
