using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BDArmory.Competition.VesselSpawning;
using UnityEngine;
using BDArmory.Competition.RemoteOrchestration;
using static BDArmory.Competition.VesselSpawning.VesselSpawner;

namespace BDArmory.Competition.SpawnStrategies
{
    public class CircularSpawnStrategy : SpawnStrategy
    {
        private VesselSource vesselSource;
        private List<int> vesselIds;
        private int bodyIndex;
        private double latitude;
        private double longitude;
        private double altitude;
        private float radius;
        private bool success = false;

        public CircularSpawnStrategy(VesselSource vesselSource, List<int> vesselIds, int bodyIndex, double latitude, double longitude, double altitude, float radius)
        {
            this.vesselSource = vesselSource;
            this.vesselIds = vesselIds;
            this.bodyIndex = bodyIndex;
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
                bodyIndex,
                latitude,
                longitude,
                altitude,
                radius,
                true,
                craftFiles: new List<string>(craftUrls)
            );
            // AUBRANIUM I've added in a coroutine version of the SpawnAllVesselsOnce function that you can call here that handles the required initialisation.
            yield return spawner.SpawnAllVesselsOnceAsCoroutine(spawnConfig);

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
