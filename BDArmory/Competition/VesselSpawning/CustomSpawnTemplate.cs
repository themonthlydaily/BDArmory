using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

using BDArmory.Settings;
using BDArmory.UI;

namespace BDArmory.Competition.VesselSpawning
{
    /// <summary>
    /// Spawn teams of craft in a custom template.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class CustomTemplateSpawning : VesselSpawnerBase
    {
        public static CustomTemplateSpawning Instance;
        protected override void Awake()
        {
            base.Awake();
            if (Instance != null) Destroy(Instance);
            Instance = this;
        }

        void LogMessage(string message, bool toScreen = true, bool toLog = true) => LogMessageFrom("CustomTemplateSpawning", message, toScreen, toLog);

        public override IEnumerator Spawn(SpawnConfig spawnConfig)
        {
            var customSpawnConfig = spawnConfig as CustomSpawnConfig;
            if (customSpawnConfig == null) yield break;
            SpawnCustomTemplateAsCoroutine(customSpawnConfig);
        }

        public void CancelSpawning()
        {
            if (vesselsSpawning)
            {
                vesselsSpawning = false;
                LogMessage("Vessel spawning cancelled.");
            }
            if (spawnCustomTemplateCoroutine != null)
            {
                StopCoroutine(spawnCustomTemplateCoroutine);
                spawnCustomTemplateCoroutine = null;
            }
        }

        #region Custom template spawning
        /// <summary>
        /// Prespawn initialisation to handle camera and body changes and to ensure that only a single spawning coroutine is running.
        /// </summary>
        /// <param name="spawnConfig">The spawn config for the new spawning.</param>
        public override void PreSpawnInitialisation(SpawnConfig spawnConfig)
        {
            base.PreSpawnInitialisation(spawnConfig);

            vesselsSpawning = true; // Signal that we've started the spawning vessels routine.
            vesselSpawnSuccess = false; // Set our success flag to false for now.
            spawnFailureReason = SpawnFailureReason.None; // Reset the spawn failure reason.
            if (spawnCustomTemplateCoroutine != null)
                StopCoroutine(spawnCustomTemplateCoroutine);
        }

        public void SpawnCustomTemplate(CustomSpawnConfig spawnConfig)
        {
            PreSpawnInitialisation(spawnConfig);
            spawnCustomTemplateCoroutine = StartCoroutine(SpawnCustomTemplateCoroutine(spawnConfig));
            LogMessage("Triggering vessel spawning at " + spawnConfig.latitude.ToString("G6") + ", " + spawnConfig.longitude.ToString("G6") + ", with altitude " + spawnConfig.altitude + "m.", false);
        }

        /// <summary>
        /// A coroutine version of the SpawnCustomTemplate function that performs the required prespawn initialisation.
        /// </summary>
        /// <param name="spawnConfig">The spawn config to use.</param>
        public IEnumerator SpawnCustomTemplateAsCoroutine(CustomSpawnConfig spawnConfig)
        {
            PreSpawnInitialisation(spawnConfig);
            LogMessage("Triggering vessel spawning at " + spawnConfig.latitude.ToString("G6") + ", " + spawnConfig.longitude.ToString("G6") + ", with altitude " + spawnConfig.altitude + "m.", false);
            yield return SpawnCustomTemplateCoroutine(spawnConfig);
        }

        private Coroutine spawnCustomTemplateCoroutine;
        // Spawns all vessels in an outward facing ring and lowers them to the ground. An altitude of 5m should be suitable for most cases.
        private IEnumerator SpawnCustomTemplateCoroutine(CustomSpawnConfig spawnConfig)
        {
            #region Initialisation and sanity checks
            // Tally up the craft to spawn and figure out teams.
            spawnConfig.craftFiles = spawnConfig.vesselSpawnConfigs.SelectMany(c => c).Select(c => c.craftURL).ToList();
            spawnConfig.teamsSpecific = spawnConfig.craftFiles.Select(f => new List<string> { f }).ToHashSet().ToList(); // Teams are the unique vessel filenames.
            LogMessage("Spawning " + spawnConfig.craftFiles.Count + " vessels at an altitude of " + spawnConfig.altitude.ToString("G0") + "m" + (spawnConfig.craftFiles.Count > 8 ? ", this may take some time..." : "."));
            #endregion

            yield return AcquireSpawnPoint(spawnConfig, 100f, false);
            if (spawnFailureReason != SpawnFailureReason.None)
            {
                vesselsSpawning = false;
                yield break;
            }

            yield return SpawnVessels(spawnConfig.vesselSpawnConfigs.SelectMany(c => c).ToList());
            if (spawnFailureReason != SpawnFailureReason.None)
            {
                vesselsSpawning = false;
                yield break;
            }

            #region Post-spawning
            // Spawning has succeeded, vessels have been renamed where necessary and vessels are ready. Time to assign teams and any other stuff.
            yield return PostSpawnMainSequence(spawnConfig, false);
            if (spawnFailureReason != SpawnFailureReason.None)
            {
                LogMessage("Vessel spawning FAILED! " + spawnFailureReason);
                vesselsSpawning = false;
                yield break;
            }

            // Revert the camera and focus on one of the vessels.
            SpawnUtils.RevertSpawnLocationCamera(true);
            if ((FlightGlobals.ActiveVessel == null || FlightGlobals.ActiveVessel.state == Vessel.State.DEAD) && spawnedVessels.Count > 0)
            {
                LoadedVesselSwitcher.Instance.ForceSwitchVessel(spawnedVessels.Take(UnityEngine.Random.Range(1, spawnedVessels.Count)).Last().Value); // Update the camera.
            }
            FlightCamera.fetch.SetDistance(50);

            // Assign the vessels to teams.
            LogMessage("Assigning vessels to teams.", false);
            var teamVesselNames = new List<List<string>>();
            for (int i = 0; i < spawnedVesselsTeamIndex.Max(kvp => kvp.Value); ++i)
                teamVesselNames.Add(spawnedVesselsTeamIndex.Where(kvp => kvp.Value == i).Select(kvp => kvp.Key).ToList());
            LoadedVesselSwitcher.Instance.MassTeamSwitch(true, false, null, teamVesselNames); // Assign A, B, ...
            #endregion

            LogMessage("Vessel spawning SUCCEEDED!", true, BDArmorySettings.DEBUG_SPAWNING);
            vesselSpawnSuccess = true;
            vesselsSpawning = false;
        }
        #endregion

        #region Templates
        public void LoadTemplate(string templateName)
        { }

        /// <summary>
        /// Save the current setup as a template.
        /// Vessel positions rotations and teams are saved.
        /// </summary>
        /// <param name="templateName"></param>
        public void SaveTemplate(string templateName)
        {
            // For the vessels in the vessel switcher, save the position, rotations and teams.
            // Also, save the planet and centroid of the positions to the SpawnConfig
            
        }


        // UI: select vessel from switcher => automatically fill slots below with the same craft.
        public bool ConfigureTemplate(CustomSpawnConfig customSpawnConfig, List<List<string>> vesselURLs, List<List<string>> kerbalNames)
        {
            // Sanity check
            int numberOfTeams = customSpawnConfig.vesselSpawnConfigs.Count;
            if (vesselURLs.Count != numberOfTeams || kerbalNames.Count != numberOfTeams)
            {
                LogMessage($"Incorrect number of vessels or kerbal names for this spawn template!");
                return false;
            }
            // Update the vessel spawn configs with new vessel URLs and custom kerbal names.
            for (int team = 0; team < numberOfTeams; ++team)
            {
                var teamCount = customSpawnConfig.vesselSpawnConfigs[team].Count;
                if (vesselURLs[team].Count != teamCount || kerbalNames[team].Count != teamCount)
                {
                    LogMessage($"Incorrect number of vessels or kerbal names for this spawn template!");
                    return false;
                }
                for (int member = 0; member < teamCount; ++member)
                {
                    var config = customSpawnConfig.vesselSpawnConfigs[team][member];
                    config.craftURL = vesselURLs[team][member];
                    config.kerbalName = kerbalNames[team][member];
                }
            }
            customSpawnConfig.altitude = Mathf.Clamp(BDArmorySettings.VESSEL_SPAWN_ALTITUDE, 2f, 10f);
            customSpawnConfig.easeInSpeed = BDArmorySettings.VESSEL_SPAWN_EASE_IN_SPEED;
            customSpawnConfig.killEverythingFirst = true;
            return true;
        }
        #endregion
    }
}
