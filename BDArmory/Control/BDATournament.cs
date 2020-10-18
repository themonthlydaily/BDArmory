using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using BDArmory.Core;
using BDArmory.Modules;
using BDArmory.Misc;
using BDArmory.UI;

namespace BDArmory.Control
{
    public class TournamentState
    {
        public TournamentState(string folder, int rounds, int vesselsPerHeat)
        {
            this.tournamentID = (uint)DateTime.UtcNow.Subtract(new DateTime(2020, 1, 1)).TotalSeconds;
            this.craftFiles = Directory.GetFiles(Environment.CurrentDirectory + $"/AutoSpawn/{folder}").Where(f => f.EndsWith(".craft")).ToList();
            GenerateHeats(rounds, Mathf.Clamp(vesselsPerHeat, 0, this.craftFiles.Count));
        }

        public uint tournamentID;
        List<string> craftFiles;

        public Dictionary<int, Dictionary<int, VesselSpawner.SpawnConfig>> rounds; // <Round, <Heat, Crafts>>
        public Dictionary<int, Dictionary<int, Dictionary<string, ScoringData>>> results = new Dictionary<int, Dictionary<int, Dictionary<string, ScoringData>>>(); // <Round, <Heat, <vessel, score>>>

        /* Generate rounds and heats by shuffling the crafts list and breaking it into groups.
         * The last heat in a round will have fewer craft if the number of craft is not divisible by the number of vessels per heat.
         */
        void GenerateHeats(int numberOfRounds, int vesselsPerHeat)
        {
            rounds = new Dictionary<int, Dictionary<int, VesselSpawner.SpawnConfig>>();
            for (int roundIndex = 0; roundIndex < numberOfRounds; ++roundIndex)
            {
                craftFiles.Shuffle();
                List<string> selectedFiles = craftFiles.Take(vesselsPerHeat).ToList();
                rounds.Add(rounds.Count, new Dictionary<int, VesselSpawner.SpawnConfig>());
                while (selectedFiles.Count > 0)
                {
                    rounds[roundIndex].Add(rounds[roundIndex].Count, new VesselSpawner.SpawnConfig(
                        BDArmorySettings.VESSEL_SPAWN_GEOCOORDS,
                        BDArmorySettings.VESSEL_SPAWN_ALTITUDE,
                        BDArmorySettings.VESSEL_SPAWN_DISTANCE,
                        BDArmorySettings.VESSEL_SPAWN_DISTANCE_TOGGLE,
                        BDArmorySettings.VESSEL_SPAWN_EASE_IN_SPEED,
                        true, // Kill everything first.
                        true, // Assign teams.
                        null, // No folder, we're going to specify the craft files.
                        selectedFiles.ToList() // Add a copy of the craft files list.
                    ));
                    selectedFiles = craftFiles.Skip(roundIndex * vesselsPerHeat).Take(vesselsPerHeat).ToList();
                }
            }
        }
    }

    public class Tournament : MonoBehaviour
    {
        public static Tournament Instance;
        TournamentState tournamentState;
        string stateFile = "GameData/BDArmory/tournament.state";
        string message;
        private Coroutine runTournamentCoroutine;

        void Awake()
        {
            if (Instance)
                Destroy(Instance);
            Instance = this;
        }

        void Start()
        {
            LoadTournamentState(); // Load the last state.
        }

        void OnDestroy()
        {
            SaveTournamentState(); // Save the last state.
        }

        // Load tournament state from disk
        bool LoadTournamentState(string stateFile = null)
        {
            if (stateFile != null) this.stateFile = stateFile;
            message = "Loading tournament state from " + stateFile;
            Debug.Log("[BDATournament]: " + message);
            if (BDACompetitionMode.Instance != null)
                BDACompetitionMode.Instance.competitionStatus.Add(message);
            return true;
        }

        // Save tournament state to disk
        bool SaveTournamentState()
        {
            message = "Saving tournament state to " + stateFile;
            Debug.Log("[BDATournament]: " + message);
            if (BDACompetitionMode.Instance != null)
                BDACompetitionMode.Instance.competitionStatus.Add(message);
            return true;
        }

        public void SetupTournament(string folder, int rounds, int vesselsPerHeat, string stateFile = null)
        {
            if (stateFile != null) this.stateFile = stateFile;
            tournamentState = new TournamentState(folder, rounds, vesselsPerHeat);
        }

        void RunTournament()
        {
            if (runTournamentCoroutine != null)
                StopCoroutine(runTournamentCoroutine);
            runTournamentCoroutine = StartCoroutine(RunTournamentCoroutine());
        }

        void StopTournament()
        {
            if (runTournamentCoroutine != null)
            {
                StopCoroutine(runTournamentCoroutine);
                runTournamentCoroutine = null;
            }
        }

        private IEnumerator RunTournamentCoroutine()
        {
            foreach (var roundIndex in tournamentState.rounds.Keys)
            {
                foreach (var heatIndex in tournamentState.rounds[roundIndex].Keys)
                {
                    if (tournamentState.results.ContainsKey(roundIndex) && tournamentState.results[roundIndex].ContainsKey(heatIndex)) continue; // We've done that heat.

                    message = "Running heat " + heatIndex + " of round " + roundIndex + " of tournament " + tournamentState.tournamentID;
                    BDACompetitionMode.Instance.competitionStatus.Add(message);
                    Debug.Log("[BDATournament]: " + message);

                    VesselSpawner.Instance.SpawnAllVesselsOnce(tournamentState.rounds[roundIndex][heatIndex]);
                    while (VesselSpawner.Instance.vesselsSpawning)
                        yield return new WaitForFixedUpdate();
                    if (!VesselSpawner.Instance.vesselSpawnSuccess)
                        yield break;
                    yield return new WaitForFixedUpdate();

                    // NOTE: runs in separate coroutine
                    BDACompetitionMode.Instance.StartCompetitionMode(BDArmorySettings.COMPETITION_DISTANCE);
                    yield return new WaitForFixedUpdate(); // Give the competition start a frame to get going.

                    // start timer coroutine for the duration specified in settings UI
                    var duration = Core.BDArmorySettings.COMPETITION_DURATION * 60f;
                    message = "Starting " + (duration > 0 ? "a " + duration.ToString("F0") + "s" : "an unlimited") + " duration competition.";
                    Debug.Log("[BDATournament]: " + message);
                    BDACompetitionMode.Instance.competitionStatus.Add(message);
                    while (BDACompetitionMode.Instance.competitionStarting)
                        yield return new WaitForFixedUpdate(); // Wait for the competition to actually start.
                    if (!BDACompetitionMode.Instance.competitionIsActive)
                    {
                        var message = "Competition failed to start.";
                        BDACompetitionMode.Instance.competitionStatus.Add(message);
                        Debug.Log("[BDATournament]: " + message);
                        yield break;
                    }
                    while (BDACompetitionMode.Instance.competitionIsActive) // Wait for the competition to finish (limited duration and log dumping is handled directly by the competition now).
                        yield return new WaitForSeconds(1);

                    // Copy the competition scores.
                    if (!tournamentState.results.ContainsKey(roundIndex)) tournamentState.results.Add(roundIndex, new Dictionary<int, Dictionary<string, ScoringData>>());
                    tournamentState.results[roundIndex][heatIndex] = BDACompetitionMode.Instance.Scores.ToDictionary(entry => entry.Key, entry => entry.Value);

                    // Wait 10s for any user action
                    double startTime = Planetarium.GetUniversalTime();
                    while ((Planetarium.GetUniversalTime() - startTime) < 10d)
                    {
                        BDACompetitionMode.Instance.competitionStatus.Add("Waiting " + (10d - (Planetarium.GetUniversalTime() - startTime)).ToString("0") + "s, then running next heat.");
                        yield return new WaitForSeconds(1);
                    }
                    SaveTournamentState();
                }
                message = "All heats in round " + roundIndex + " have been run.";
                BDACompetitionMode.Instance.competitionStatus.Add(message);
                Debug.Log("[BDATournament]: " + message);
            }
            message = "All rounds in tournament " + tournamentState.tournamentID + " have been run.";
            BDACompetitionMode.Instance.competitionStatus.Add(message);
            Debug.Log("[BDATournament]: " + message);
        }
    }
}