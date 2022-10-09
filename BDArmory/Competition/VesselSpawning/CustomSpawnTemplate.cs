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

        void Start()
        {
            if (customSpawnConfig == null) LoadTemplate();
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
            spawnConfig.craftFiles = spawnConfig.customVesselSpawnConfigs.SelectMany(c => c).Select(c => c.craftURL).ToList();
            spawnConfig.teamsSpecific = spawnConfig.craftFiles.Select(f => new List<string> { f }).ToHashSet().ToList(); // Teams are the unique vessel filenames.
            LogMessage("Spawning " + spawnConfig.craftFiles.Count + " vessels at an altitude of " + spawnConfig.altitude.ToString("G0") + "m" + (spawnConfig.craftFiles.Count > 8 ? ", this may take some time..." : "."));

            var vesselSpawnConfigs = new List<VesselSpawnConfig>();
            foreach(var customVesselSpawnConfig in spawnConfig.customVesselSpawnConfigs.SelectMany(c => c))
            {
                vesselSpawnConfigs.Add(new VesselSpawnConfig(
                    customVesselSpawnConfig.craftURL,
                    FlightGlobals.currentMainBody.GetWorldSurfacePosition(customSpawnConfig.latitude, customSpawnConfig.longitude, spawnConfig.altitude),
                    default,
                    (float)spawnConfig.altitude,
                    0,
                    false,
                    customVesselSpawnConfig.teamIndex,
                    false,
                    customVesselSpawnConfig.kerbalName
                ));
            }
            #endregion

            yield return AcquireSpawnPoint(spawnConfig, 100f, false);
            if (spawnFailureReason != SpawnFailureReason.None)
            {
                vesselsSpawning = false;
                yield break;
            }

            yield return SpawnVessels(vesselSpawnConfigs);
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
        public CustomSpawnConfig customSpawnConfig = null;
        public void LoadTemplate(string templateName = null)
        {
            if (string.IsNullOrEmpty(templateName)) // Empty template
            {
                customSpawnConfig = new CustomSpawnConfig(
                    "",
                    new SpawnConfig(
                        BDArmorySettings.VESSEL_SPAWN_WORLDINDEX,
                        BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.x,
                        BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.y,
                        BDArmorySettings.VESSEL_SPAWN_ALTITUDE,
                        BDArmorySettings.VESSEL_SPAWN_EASE_IN_SPEED),
                    new List<List<CustomVesselSpawnConfig>>());
            }
            else
            {
                // Load the template from disk.
            }
        }

        /// <summary>
        /// Save the current setup as a template.
        /// Vessel positions rotations and teams are saved.
        /// </summary>
        /// <param name="templateName"></param>
        public void SaveTemplate()
        {
            // For the vessels in the vessel switcher, save the position, rotations and teams.
            // Also, save the planet and centroid of the positions to the SpawnConfig
            customSpawnConfig.worldIndex = BDArmorySettings.VESSEL_SPAWN_WORLDINDEX;
            customSpawnConfig.latitude = BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.x;
            customSpawnConfig.longitude = BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.y;
            customSpawnConfig.altitude = BDArmorySettings.VESSEL_SPAWN_ALTITUDE;
            customSpawnConfig.easeInSpeed = BDArmorySettings.VESSEL_SPAWN_EASE_IN_SPEED;
            customSpawnConfig.customVesselSpawnConfigs.Clear();
            // foreach (var v in WeaponManagers.SelectMany(tm => tm.Value).Where(wm => wm != null && wm.vessel != null).Select(wm => wm.vessel))
            int teamCount = 0;
            foreach (var team in LoadedVesselSwitcher.Instance.WeaponManagers)
            {
                var teamConfigs = new List<CustomVesselSpawnConfig>();
                foreach (var member in team.Value)
                {
                    CustomVesselSpawnConfig vesselSpawnConfig = new CustomVesselSpawnConfig();
                    FlightGlobals.currentMainBody.GetLatLonAlt(member.vessel.transform.position, out vesselSpawnConfig.latitude, out vesselSpawnConfig.longitude, out vesselSpawnConfig.altitude);
                    vesselSpawnConfig.heading = (Vector3.SignedAngle(member.vessel.ReferenceTransform.up, member.vessel.north, member.vessel.up) + 360f) % 360f;
                    vesselSpawnConfig.teamIndex = teamCount;
                    teamConfigs.Add(vesselSpawnConfig);
                }
                customSpawnConfig.customVesselSpawnConfigs.Add(teamConfigs);
                ++teamCount;
            }
            // FIXME Save this config to disk.
            Debug.Log($"DEBUG {customSpawnConfig.ToString()}");
        }


        // UI: select vessel from switcher => automatically fill slots below with the same craft.
        public bool ConfigureTemplate(List<List<string>> vesselURLs, List<List<string>> kerbalNames)
        {
            // Sanity check
            int numberOfTeams = customSpawnConfig.customVesselSpawnConfigs.Count;
            if (vesselURLs.Count != numberOfTeams || kerbalNames.Count != numberOfTeams)
            {
                LogMessage($"Incorrect number of vessels or kerbal names for this spawn template!");
                return false;
            }
            // Update the vessel spawn configs with new vessel URLs and custom kerbal names.
            for (int team = 0; team < numberOfTeams; ++team)
            {
                var teamCount = customSpawnConfig.customVesselSpawnConfigs[team].Count;
                if (vesselURLs[team].Count != teamCount || kerbalNames[team].Count != teamCount)
                {
                    LogMessage($"Incorrect number of vessels or kerbal names for this spawn template!");
                    return false;
                }
                for (int member = 0; member < teamCount; ++member)
                {
                    var config = customSpawnConfig.customVesselSpawnConfigs[team][member];
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

        #region UI
        Vector2 _displayViewerPosition = default;
        List<ProtoCrewMember> SelectedCrewMembers = new List<ProtoCrewMember>();
        /// <summary>
        /// Crew selection window borrowed from VesselMover and modified.
        /// </summary>
        /// <param name="windowID"></param>
        public void CrewSelectionWindow(int windowID)
        {
            KerbalRoster kerbalRoster = HighLogic.CurrentGame.CrewRoster;
            GUILayout.BeginVertical();
            _displayViewerPosition = GUILayout.BeginScrollView(_displayViewerPosition, GUI.skin.box, GUILayout.Height(250), GUILayout.Width(280));
            using (var kerbals = kerbalRoster.Kerbals(ProtoCrewMember.RosterStatus.Available).GetEnumerator())
                while (kerbals.MoveNext())
                {
                    ProtoCrewMember crewMember = kerbals.Current;
                    if (crewMember == null) continue;
                    bool selected = SelectedCrewMembers.Contains(crewMember);
                    GUIStyle buttonStyle = selected ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button;
                    selected = GUILayout.Toggle(selected, $"{crewMember.name}, {crewMember.gender}, {crewMember.trait}", buttonStyle);
                    if (selected && !SelectedCrewMembers.Contains(crewMember))
                    {
                        SelectedCrewMembers.Clear();
                        SelectedCrewMembers.Add(crewMember);
                    }
                    else if (!selected && SelectedCrewMembers.Contains(crewMember))
                    {
                        SelectedCrewMembers.Clear();
                    }
                }
            GUILayout.EndScrollView();
            GUILayout.Space(20);
            if (GUILayout.Button("Select", BDArmorySetup.BDGuiSkin.button))
            {
                // VesselSpawn.SelectedCrewData = SelectedCrewMembers;
                // VesselSpawn.IsSelectingCrew = false;
                // VesselSpawn.IsCrewSelected = true;
            }
            GUILayout.EndVertical();
        }

        #endregion
    }
}
