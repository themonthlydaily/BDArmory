using System.Collections;
using UnityEngine;

using BDArmory.Competition.SpawnStrategies;
using BDArmory.Competition.VesselSpawning;


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
    }
}