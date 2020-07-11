using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Runtime.CompilerServices;
using BDArmory.Core;
using BDArmory.FX;
using BDArmory.Misc;
using BDArmory.Modules;
using BDArmory.UI;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;
using Object = UnityEngine.Object;

namespace BDArmory.Control
{
    // trivial score keeping structure
    public class ScoringData
    {
        public int Score;
        public int PinataHits;
        public int DeathOrder;
        public int totalDamagedParts = 0;
        public string lastPersonWhoHitMe;
        public string lastPersonWhoRammedMe;
        public double lastHitTime;
        public double lastFiredTime;
        public double lastRammedTime;
        public double lastPossibleRammingTime = -1;
        public float closestTimeToCPA;
        public Vessel rammedVessel;
        public Vessel rammingVessel;
        public ScoringData otherVesselScoringData;
        public bool isRamming;
        public bool isHeadOn;
        public bool hasReachedVessel;
        public bool landedState;
        public double lastLandedTime;
        public double landerKillTimer;
        public double AverageSpeed;
        public double AverageAltitude;
        public int averageCount;
        public int previousPartCount;
        public int partCountBeforeRam;
        public int closestVesselPartCountBeforeRam;
        public string debugState;
        public HashSet<string> everyoneWhoHitMe = new HashSet<string>();
        public HashSet<string> everyoneWhoRammedMe = new HashSet<string>();
        public HashSet<string> everyoneWhoDamagedMe = new HashSet<string>();

        public string LastPersonWhoDamagedMe()
        {
            //check if vessel got rammed
            if (lastHitTime > lastRammedTime)
                return lastPersonWhoHitMe;
            if (lastHitTime < lastRammedTime)
                return lastPersonWhoRammedMe;
            return "";
        }

        public HashSet<string> EveryOneWhoDamagedMe()
        {
            foreach (var hit in everyoneWhoHitMe)
            {
                everyoneWhoDamagedMe.Add(hit);
            }

            foreach (var ram in everyoneWhoRammedMe)
            {
                if (!everyoneWhoDamagedMe.Contains(ram))
                {
                    everyoneWhoDamagedMe.Add(ram);
                }
            }

            return everyoneWhoDamagedMe;
        }
    }


    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class BDACompetitionMode : MonoBehaviour
    {
        public static BDACompetitionMode Instance;



        // keep track of scores, these probably need to be somewhere else
        public Dictionary<string, ScoringData> Scores = new Dictionary<string, ScoringData>();
        //public Dictionary<string, int> Scores = new Dictionary<string, int>();
        //public Dictionary<string, int> PinataHits = new Dictionary<string, int>();

        //public Dictionary<string, string> whoKilledVessels = new Dictionary<string, string>();
        //public Dictionary<string, double> lastHitTime = new Dictionary<string, double>();


        // KILLER GM - how we look for slowest planes
        //public Dictionary<string, double> AverageSpeed = new Dictionary<string, double>();
        //public Dictionary<string, double> AverageAltitude = new Dictionary<string, double>();
        //public Dictionary<string, int> FireCount = new Dictionary<string, int>();
        //public Dictionary<string, int> FireCount2 = new Dictionary<string, int>();

        public Dictionary<string, int> DeathOrder = new Dictionary<string, int>();
        public Dictionary<string, int> whoShotWho = new Dictionary<string, int>();
        public Dictionary<string, int> whoRammedWho = new Dictionary<string, int>();

        public bool killerGMenabled = false;
        public bool pinataAlive = false;

        private double competitionStartTime = -1;
        private double nextUpdateTick = -1;
        private double gracePeriod = -1;
        private double decisionTick = -1;
        private int dumpedResults = 4;

        public bool OneOfAKind = false;

        // count up until killing the object 
        public Dictionary<string, int> KillTimer = new Dictionary<string, int>();


        private HashSet<int> ammoIds = new HashSet<int>();

        // time competition was started
        int CompetitionID;

        // pilot actions
        private Dictionary<string, string> pilotActions = new Dictionary<string, string>();
        private string deadOrAlive = "";

        void Awake()
        {
            if (Instance)
            {
                Destroy(Instance);
            }

            Instance = this;
        }

        void OnGUI()
        {
            if (competitionStarting || competitionStartTime > 0)
            {
                GUIStyle cStyle = new GUIStyle(BDArmorySetup.BDGuiSkin.label);
                cStyle.fontStyle = FontStyle.Bold;
                cStyle.fontSize = 22;
                cStyle.alignment = TextAnchor.UpperLeft;

                var displayRow = 100;
                if (!BDArmorySetup.GAME_UI_ENABLED)
                {
                    displayRow = 30;
                }

                Rect cLabelRect = new Rect(30, displayRow, Screen.width, 100);

                GUIStyle cShadowStyle = new GUIStyle(cStyle);
                Rect cShadowRect = new Rect(cLabelRect);
                cShadowRect.x += 2;
                cShadowRect.y += 2;
                cShadowStyle.normal.textColor = new Color(0, 0, 0, 0.75f);
                string message = competitionStatus;
                if (competitionStatus == "")
                {
                    if (FlightGlobals.ActiveVessel != null)
                    {
                        string postFix = "";
                        if (pilotActions.ContainsKey(message))
                        {
                            postFix = " " + pilotActions[message];
                        }
                        if (Scores.ContainsKey(message))
                        {
                            ScoringData vData = Scores[message];
                            if (Planetarium.GetUniversalTime() - vData.lastHitTime < 2)
                            {
                                postFix = " is taking damage from " + vData.lastPersonWhoHitMe;
                            }
                        }
                        if (lastCompetitionStatus != "")
                        {
                            message = lastCompetitionStatus + "\n" + FlightGlobals.ActiveVessel.GetName() + postFix;
                            lastCompetitionStatusTimer -= Time.deltaTime;
                            if (lastCompetitionStatusTimer < 0f)
                                lastCompetitionStatus = "";
                        }
                        else
                            message = FlightGlobals.ActiveVessel.GetName() + postFix;
                    }
                }
                else
                {
                    lastCompetitionStatus = competitionStatus;
                    lastCompetitionStatusTimer = 10f; // Show for 5s.
                }

                GUI.Label(cShadowRect, message, cShadowStyle);
                GUI.Label(cLabelRect, message, cStyle);
                if (!BDArmorySetup.GAME_UI_ENABLED && competitionStartTime > 0)
                {
                    Rect clockRect = new Rect(10, 6, Screen.width, 20);
                    GUIStyle clockStyle = new GUIStyle(cStyle);
                    clockStyle.fontSize = 14;
                    GUIStyle clockShadowStyle = new GUIStyle(clockStyle);
                    clockShadowStyle.normal.textColor = new Color(0, 0, 0, 0.75f);
                    Rect clockShadowRect = new Rect(clockRect);
                    clockShadowRect.x += 2;
                    clockShadowRect.y += 2;
                    var gTime = Planetarium.GetUniversalTime() - competitionStartTime;
                    var minutes = (int)(Math.Floor(gTime / 60));
                    var seconds = (int)(gTime % 60);
                    string pTime = minutes.ToString("00") + ":" + seconds.ToString("00") + "     " + deadOrAlive;
                    GUI.Label(clockShadowRect, pTime, clockShadowStyle);
                    GUI.Label(clockRect, pTime, clockStyle);
                }
            }
        }

        public void ResetCompetitionScores()
        {

            // reinitilize everything when the button get hit.
            // ammo names
            // 50CalAmmo, 30x173Ammo, 20x102Ammo, CannonShells
            if (ammoIds.Count == 0)
            {
                ammoIds.Add(PartResourceLibrary.Instance.GetDefinition("50CalAmmo").id);
                ammoIds.Add(PartResourceLibrary.Instance.GetDefinition("30x173Ammo").id);
                ammoIds.Add(PartResourceLibrary.Instance.GetDefinition("20x102Ammo").id);
                ammoIds.Add(PartResourceLibrary.Instance.GetDefinition("CannonShells").id);
            }
            CompetitionID = (int)DateTime.UtcNow.Subtract(new DateTime(2020, 1, 1)).TotalSeconds;
            DoPreflightChecks();
            Scores.Clear();
            DeathOrder.Clear();
            whoShotWho.Clear();
            whoRammedWho.Clear();
            KillTimer.Clear();
            dumpedResults = 5;
            competitionStartTime = Planetarium.GetUniversalTime();
            nextUpdateTick = competitionStartTime + 2; // 2 seconds before we start tracking
            gracePeriod = competitionStartTime + 60;
            decisionTick = competitionStartTime + 60; // every 60 seconds we do nasty things
            // now find all vessels with weapons managers
            using (var loadedVessels = BDATargetManager.LoadedVessels.GetEnumerator())
                while (loadedVessels.MoveNext())
                {
                    if (loadedVessels.Current == null || !loadedVessels.Current.loaded)
                        continue;
                    IBDAIControl pilot = loadedVessels.Current.FindPartModuleImplementing<IBDAIControl>();
                    if (pilot == null || !pilot.weaponManager || pilot.weaponManager.Team.Neutral)
                        continue;
                    // put these in the scoring dictionary - these are the active participants
                    ScoringData vDat = new ScoringData();
                    vDat.lastPersonWhoHitMe = "";
                    vDat.lastPersonWhoRammedMe = "";
                    vDat.lastFiredTime = Planetarium.GetUniversalTime();
                    vDat.previousPartCount = loadedVessels.Current.parts.Count();
                    Scores[loadedVessels.Current.GetName()] = vDat;
                }
        }

        //Competition mode
        public bool competitionStarting;
        public string competitionStatus = "";
        string lastCompetitionStatus = "";
        float lastCompetitionStatusTimer = 0f;
        public bool competitionIsActive = false;
        Coroutine competitionRoutine;

        public void StartCompetitionMode(float distance)
        {

            if (!competitionStarting)
            {
                ResetCompetitionScores();
                Debug.Log("[BDArmoryCompetition:" + CompetitionID.ToString() + "]: Starting Competition ");
                competitionRoutine = StartCoroutine(DogfightCompetitionModeRoutine(distance));
                GameEvents.onCollision.Add(AnalyseCollision);
            }
        }


        public void StartRapidDeployment(float distance)
        {

            if (!competitionStarting)
            {
                ResetCompetitionScores();
                Debug.Log("[BDArmoryCompetition:" + CompetitionID.ToString() + "]: Starting Rapid Deployment ");
                string commandString = "0:SetThrottle:100\n0:ActionGroup:14:0\n0:Stage\n35:ActionGroup:1\n10:ActionGroup:2\n3:RemoveFairings\n0:ActionGroup:3\n0:ActionGroup:12:1\n1:TogglePilot:1\n6:ToggleGuard:1\n0:ActionGroup:16:0\n5:EnableGM\n5:RemoveDebris\n0:ActionGroup:16:0\n";
                competitionRoutine = StartCoroutine(SequencedCompetition(commandString));
                GameEvents.onCollision.Add(AnalyseCollision);
            }
        }

        public void StopCompetition()
        {
            if (competitionRoutine != null)
            {
                StopCoroutine(competitionRoutine);
            }

            competitionStarting = false;
            competitionIsActive = false;
            GameEvents.onCollision.Remove(AnalyseCollision); // FIXME Do we need to test that it was actually added previously?
            rammingInformation = null; // Reset the ramming information.
        }


        IEnumerator DogfightCompetitionModeRoutine(float distance)
        {
            competitionStarting = true;
            competitionStatus = "Competition: Pilots are taking off.";
            var pilots = new Dictionary<BDTeam, List<IBDAIControl>>();
            HashSet<IBDAIControl> readyToLaunch = new HashSet<IBDAIControl>();
            using (var loadedVessels = BDATargetManager.LoadedVessels.GetEnumerator())
                while (loadedVessels.MoveNext())
                {
                    if (loadedVessels.Current == null || !loadedVessels.Current.loaded)
                        continue;
                    IBDAIControl pilot = loadedVessels.Current.FindPartModuleImplementing<IBDAIControl>();
                    if (pilot == null || !pilot.weaponManager || pilot.weaponManager.Team.Neutral)
                        continue;

                    if (!pilots.TryGetValue(pilot.weaponManager.Team, out List<IBDAIControl> teamPilots))
                    {
                        teamPilots = new List<IBDAIControl>();
                        pilots.Add(pilot.weaponManager.Team, teamPilots);
                        Debug.Log("[BDArmoryCompetition:" + CompetitionID.ToString() + "]: Adding Team " + pilot.weaponManager.Team.Name);
                    }
                    teamPilots.Add(pilot);
                    Debug.Log("[BDArmoryCompetition:" + CompetitionID.ToString() + "]: Adding Pilot " + pilot.vessel.GetName());
                    readyToLaunch.Add(pilot);
                }

            foreach (var pilot in readyToLaunch)
            {
                pilot.vessel.ActionGroups.ToggleGroup(KM_dictAG[1]);
                pilot.CommandTakeOff();
                if (pilot.weaponManager.guardMode)
                {
                    pilot.weaponManager.ToggleGuardMode();
                }
            }

            //clear target database so pilots don't attack yet
            BDATargetManager.ClearDatabase();

            foreach (var vname in Scores.Keys)
            {
                Debug.Log("[BDACompetitionMode] Adding Score Tracker For " + vname);
            }

            if (pilots.Count < 2)
            {
                Debug.Log("[BDArmory]: Unable to start competition mode - one or more teams is empty");
                competitionStatus = "Competition: Failed!  One or more teams is empty.";
                yield return new WaitForSeconds(2);
                competitionStarting = false;
                competitionIsActive = false;
                yield break;
            }

            var leaders = new List<IBDAIControl>();
            using (var pilotList = pilots.GetEnumerator())
                while (pilotList.MoveNext())
                {
                    leaders.Add(pilotList.Current.Value[0]);
                    pilotList.Current.Value[0].weaponManager.wingCommander.CommandAllFollow();
                }

            //wait till the leaders are ready to engage (airborne for PilotAI)
            bool ready = false;
            while (!ready)
            {
                ready = true;
                using (var leader = leaders.GetEnumerator())
                    while (leader.MoveNext())
                        if (leader.Current != null && !leader.Current.CanEngage())
                        {
                            ready = false;
                            yield return new WaitForSeconds(1);
                            break;
                        }
            }

            using (var leader = leaders.GetEnumerator())
                while (leader.MoveNext())
                    if (leader.Current == null)
                        StopCompetition();

            competitionStatus = "Competition: Sending pilots to start position.";
            Vector3 center = Vector3.zero;
            using (var leader = leaders.GetEnumerator())
                while (leader.MoveNext())
                    center += leader.Current.vessel.CoM;
            center /= leaders.Count;
            Vector3 startDirection = Vector3.ProjectOnPlane(leaders[0].vessel.CoM - center, VectorUtils.GetUpDirection(center)).normalized;
            startDirection *= (distance * leaders.Count / 4) + 1250f;
            Quaternion directionStep = Quaternion.AngleAxis(360f / leaders.Count, VectorUtils.GetUpDirection(center));

            for (var i = 0; i < leaders.Count; ++i)
            {
                leaders[i].CommandFlyTo(VectorUtils.WorldPositionToGeoCoords(startDirection, FlightGlobals.currentMainBody));
                startDirection = directionStep * startDirection;
            }

            Vector3 centerGPS = VectorUtils.WorldPositionToGeoCoords(center, FlightGlobals.currentMainBody);

            //wait till everyone is in position
            competitionStatus = "Competition: Waiting for teams to get in position.";
            bool waiting = true;
            var sqrDistance = distance * distance;
            while (waiting)
            {
                waiting = false;

                using (var leader = leaders.GetEnumerator())
                    while (leader.MoveNext())
                    {
                        if (leader.Current == null)
                            StopCompetition();

                        using (var otherLeader = leaders.GetEnumerator())
                            while (otherLeader.MoveNext())
                            {
                                if (leader.Current == otherLeader.Current)
                                    continue;
                                if ((leader.Current.transform.position - otherLeader.Current.transform.position).sqrMagnitude < sqrDistance)
                                    waiting = true;
                            }

                        // Increase the distance for large teams
                        var sqrTeamDistance = (800 + 100 * pilots[leader.Current.weaponManager.Team].Count) * (800 + 100 * pilots[leader.Current.weaponManager.Team].Count);
                        using (var pilot = pilots[leader.Current.weaponManager.Team].GetEnumerator())
                            while (pilot.MoveNext())
                                if (pilot.Current != null
                                        && pilot.Current.currentCommand == PilotCommands.Follow
                                        && (pilot.Current.vessel.CoM - pilot.Current.commandLeader.vessel.CoM).sqrMagnitude > 1000f * 1000f)
                                    waiting = true;

                        if (waiting) break;
                    }

                yield return null;
            }

            //start the match
            using (var teamPilots = pilots.GetEnumerator())
                while (teamPilots.MoveNext())
                    using (var pilot = teamPilots.Current.Value.GetEnumerator())
                        while (pilot.MoveNext())
                        {
                            if (pilot.Current == null) continue;

                            if (!pilot.Current.weaponManager.guardMode)
                                pilot.Current.weaponManager.ToggleGuardMode();

                            using (var leader = leaders.GetEnumerator())
                                while (leader.MoveNext())
                                    BDATargetManager.ReportVessel(pilot.Current.vessel, leader.Current.weaponManager);

                            pilot.Current.ReleaseCommand();
                            pilot.Current.CommandAttack(centerGPS);
                            pilot.Current.vessel.altimeterDisplayState = AltimeterDisplayState.AGL;
                        }

            competitionStatus = "Competition starting!  Good luck!";
            yield return new WaitForSeconds(2);
            competitionStatus = "";
            lastCompetitionStatus = "";
            competitionStarting = false;
            competitionIsActive = true; //start logging ramming now that the competition has officially started
        }

        public static Dictionary<int, KSPActionGroup> KM_dictAG = new Dictionary<int, KSPActionGroup> {
            { 0,  KSPActionGroup.None },
            { 1,  KSPActionGroup.Custom01 },
            { 2,  KSPActionGroup.Custom02 },
            { 3,  KSPActionGroup.Custom03 },
            { 4,  KSPActionGroup.Custom04 },
            { 5,  KSPActionGroup.Custom05 },
            { 6,  KSPActionGroup.Custom06 },
            { 7,  KSPActionGroup.Custom07 },
            { 8,  KSPActionGroup.Custom08 },
            { 9,  KSPActionGroup.Custom09 },
            { 10, KSPActionGroup.Custom10 },
            { 11, KSPActionGroup.Light },
            { 12, KSPActionGroup.RCS },
            { 13, KSPActionGroup.SAS },
            { 14, KSPActionGroup.Brakes },
            { 15, KSPActionGroup.Abort },
            { 16, KSPActionGroup.Gear }
        };

        // transmits a bunch of commands to make things happen
        // this is a really dumb sequencer with text commands
        // 0:ThrottleMax
        // 0:Stage
        // 30:ActionGroup:1
        // 35:ActionGroup:2
        // 40:ActionGroup:3
        // 41:TogglePilot
        // 45:ToggleGuard

        private List<IBDAIControl> getAllPilots()
        {
            var pilots = new List<IBDAIControl>();
            HashSet<string> vesselNames = new HashSet<string>();
            int count = 0;
            using (var loadedVessels = BDATargetManager.LoadedVessels.GetEnumerator())
                while (loadedVessels.MoveNext())
                {
                    if (loadedVessels.Current == null || !loadedVessels.Current.loaded)
                        continue;
                    IBDAIControl pilot = loadedVessels.Current.FindPartModuleImplementing<IBDAIControl>();
                    if (pilot == null || !pilot.weaponManager || pilot.weaponManager.Team.Neutral)
                        continue;
                    pilots.Add(pilot);
                    if (vesselNames.Contains(loadedVessels.Current.vesselName))
                    {
                        loadedVessels.Current.vesselName += "_" + (++count);
                    }
                    vesselNames.Add(loadedVessels.Current.vesselName);
                }
            return pilots;
        }

        private void DoPreflightChecks()
        {
            var pilots = getAllPilots();
            foreach (var pilot in pilots)
            {
                if (pilot.vessel == null) continue;

                enforcePartCount(pilot.vessel);
            }
        }
        // "JetEngine", "miniJetEngine", "turboFanEngine", "turboJet", "turboFanSize2", "RAPIER"
        static string[] allowedEngineList = { "JetEngine", "miniJetEngine", "turboFanEngine", "turboJet", "turboFanSize2", "RAPIER" };
        static HashSet<string> allowedEngines = new HashSet<string>(allowedEngineList);

        // allow duplicate landing gear
        static string[] allowedDuplicateList = { "GearLarge", "GearFixed", "GearFree", "GearMedium", "GearSmall", "SmallGearBay", "fuelLine", "strutConnector" };
        static HashSet<string> allowedLandingGear = new HashSet<string>(allowedDuplicateList);

        // don't allow "SaturnAL31"
        static string[] bannedPartList = { "SaturnAL31" };
        static HashSet<string> bannedParts = new HashSet<string>(bannedPartList);

        // ammo boxes
        static string[] ammoPartList = { "baha20mmAmmo", "baha30mmAmmo", "baha50CalAmmo", "BDAcUniversalAmmoBox", "UniversalAmmoBoxBDA" };
        static HashSet<string> ammoParts = new HashSet<string>(ammoPartList);

        // outOfAmmo register
        static HashSet<string> outOfAmmo = new HashSet<string>(); // For tracking which planes are out of ammo.

        public void enforcePartCount(Vessel vessel)
        {
            if (!OneOfAKind) return;
            using (List<Part>.Enumerator parts = vessel.parts.GetEnumerator())
            {
                Dictionary<string, int> partCounts = new Dictionary<string, int>();
                List<Part> partsToKill = new List<Part>();
                List<Part> ammoBoxes = new List<Part>();
                int engineCount = 0;
                while (parts.MoveNext())
                {
                    if (parts.Current == null) continue;
                    var partName = parts.Current.name;
                    //Debug.Log("Part " + vessel.GetName() + " " + partName);
                    if (partCounts.ContainsKey(partName))
                    {
                        partCounts[partName]++;
                    }
                    else
                    {
                        partCounts[partName] = 1;
                    }
                    if (allowedEngines.Contains(partName))
                    {
                        engineCount++;
                    }
                    if (bannedParts.Contains(partName))
                    {
                        partsToKill.Add(parts.Current);
                    }
                    if (allowedLandingGear.Contains(partName))
                    {
                        // duplicates allowed
                        continue;
                    }
                    if (ammoParts.Contains(partName))
                    {
                        // can only figure out limits after counting engines.
                        ammoBoxes.Add(parts.Current);
                        continue;
                    }
                    if (partCounts[partName] > 1)
                    {
                        partsToKill.Add(parts.Current);
                    }
                }
                if (engineCount == 0)
                {
                    engineCount = 1;
                }

                while (ammoBoxes.Count > engineCount * 3)
                {
                    partsToKill.Add(ammoBoxes[ammoBoxes.Count - 1]);
                    ammoBoxes.RemoveAt(ammoBoxes.Count - 1);
                }
                if (partsToKill.Count > 0)
                {
                    Debug.Log("[BDArmoryCompetition:" + CompetitionID.ToString() + "] Vessel Breaking Part Count Rules " + vessel.GetName());
                    foreach (var part in partsToKill)
                    {
                        Debug.Log("[BDArmoryCompetition:" + CompetitionID.ToString() + "] KILLPART:" + part.name + ":" + vessel.GetName());
                        PartExploderSystem.AddPartToExplode(part);
                    }
                }
            }
        }

        private void DoRapidDeploymentMassTrim()
        {
            // in rapid deployment this verified masses etc. 
            var oreID = PartResourceLibrary.Instance.GetDefinition("Ore").id;
            var pilots = getAllPilots();
            var lowestMass = 100000000000000f;
            var highestMass = 0f;
            foreach (var pilot in pilots)
            {

                if (pilot.vessel == null) continue;

                var notShieldedCount = 0;
                using (List<Part>.Enumerator parts = pilot.vessel.parts.GetEnumerator())
                {
                    while (parts.MoveNext())
                    {
                        if (parts.Current == null) continue;
                        // count the unshielded parts
                        if (!parts.Current.ShieldedFromAirstream)
                        {
                            notShieldedCount++;
                        }
                        using (IEnumerator<PartResource> resources = parts.Current.Resources.GetEnumerator())
                            while (resources.MoveNext())
                            {
                                if (resources.Current == null) continue;

                                if (resources.Current.resourceName == "Ore")
                                {
                                    if (resources.Current.maxAmount == 1500)
                                    {
                                        resources.Current.amount = 0;
                                    }
                                    // oreMass = 10;
                                    // ore to add = difference / 10;
                                    // is mass in tons or KG?
                                    //Debug.Log("[BDACompetitionMode] RESOURCE:" + parts.Current.partName + ":" + resources.Current.maxAmount);

                                }
                                else if (resources.Current.resourceName == "LiquidFuel")
                                {
                                    if (resources.Current.maxAmount == 3240)
                                    {
                                        resources.Current.amount = 2160;
                                    }
                                }
                                else if (resources.Current.resourceName == "Oxidizer")
                                {
                                    if (resources.Current.maxAmount == 3960)
                                    {
                                        resources.Current.amount = 2640;
                                    }
                                }
                            }
                    }
                }
                var mass = pilot.vessel.GetTotalMass();

                Debug.Log("[BDArmoryCompetition:" + CompetitionID.ToString() + "] UNSHIELDED:" + notShieldedCount.ToString() + ":" + pilot.vessel.GetName());

                Debug.Log("[BDArmoryCompetition:" + CompetitionID.ToString() + "] MASS:" + mass.ToString() + ":" + pilot.vessel.GetName());
                if (mass < lowestMass)
                {
                    lowestMass = mass;
                }
                if (mass > highestMass)
                {
                    highestMass = mass;
                }
            }

            var difference = highestMass - lowestMass;
            //
            foreach (var pilot in pilots)
            {
                if (pilot.vessel == null) continue;
                var mass = pilot.vessel.GetTotalMass();
                var extraMass = highestMass - mass;
                using (List<Part>.Enumerator parts = pilot.vessel.parts.GetEnumerator())
                    while (parts.MoveNext())
                    {
                        bool massAdded = false;
                        if (parts.Current == null) continue;
                        using (IEnumerator<PartResource> resources = parts.Current.Resources.GetEnumerator())
                            while (resources.MoveNext())
                            {
                                if (resources.Current == null) continue;
                                if (resources.Current.resourceName == "Ore")
                                {
                                    // oreMass = 10;
                                    // ore to add = difference / 10;
                                    // is mass in tons or KG?
                                    if (resources.Current.maxAmount == 1500)
                                    {
                                        var oreAmount = extraMass / 0.01; // 10kg per unit of ore
                                        if (oreAmount > 1500) oreAmount = 1500;
                                        resources.Current.amount = oreAmount;
                                    }
                                    Debug.Log("[BDArmoryCompetition:" + CompetitionID.ToString() + "] RESOURCEUPDATE:" + pilot.vessel.GetName() + ":" + resources.Current.amount);
                                    massAdded = true;
                                }
                            }
                        if (massAdded) break;
                    }
            }
        }

        IEnumerator SequencedCompetition(string commandList)
        {
            competitionStarting = true;
            double startTime = Planetarium.GetUniversalTime();
            double nextStep = startTime;
            // split list of events into lines
            var events = commandList.Split('\n');

            foreach (var cmdEvent in events)
            {
                // parse the event
                competitionStatus = cmdEvent;
                var parts = cmdEvent.Split(':');
                if (parts.Count() == 1)
                {
                    Debug.Log("[BDACompetitionMode] Competition Command not parsed correctly " + cmdEvent);
                    break;
                }
                var timeStep = int.Parse(parts[0]);
                nextStep = Planetarium.GetUniversalTime() + timeStep;
                while (Planetarium.GetUniversalTime() < nextStep)
                {
                    yield return null;
                }

                List<IBDAIControl> pilots;
                var command = parts[1];

                switch (command)
                {
                    case "Stage":
                        // activate stage
                        pilots = getAllPilots();
                        foreach (var pilot in pilots)
                        {
                            Misc.Misc.fireNextNonEmptyStage(pilot.vessel);
                        }
                        break;
                    case "ActionGroup":
                        pilots = getAllPilots();
                        foreach (var pilot in pilots)
                        {
                            if (parts.Count() == 3)
                            {
                                pilot.vessel.ActionGroups.ToggleGroup(KM_dictAG[int.Parse(parts[2])]);
                            }
                            else if (parts.Count() == 4)
                            {
                                bool state = false;
                                if (parts[3] != "0")
                                {
                                    state = true;
                                }
                                pilot.vessel.ActionGroups.SetGroup(KM_dictAG[int.Parse(parts[2])], state);
                            }
                            else
                            {
                                Debug.Log("[BDACompetitionMode] Competition Command not parsed correctly " + cmdEvent);
                            }
                        }
                        break;
                    case "TogglePilot":
                        if (parts.Count() == 3)
                        {
                            var newState = true;
                            if (parts[2] == "0")
                            {
                                newState = false;
                            }
                            pilots = getAllPilots();
                            foreach (var pilot in pilots)
                            {
                                if (newState != pilot.pilotEnabled)
                                    pilot.TogglePilot();
                            }
                        }
                        else
                        {
                            pilots = getAllPilots();
                            foreach (var pilot in pilots)
                            {
                                pilot.TogglePilot();
                            }
                        }
                        break;
                    case "ToggleGuard":
                        if (parts.Count() == 3)
                        {
                            var newState = true;
                            if (parts[2] == "0")
                            {
                                newState = false;
                            }
                            pilots = getAllPilots();
                            foreach (var pilot in pilots)
                            {
                                if (pilot.weaponManager != null && pilot.weaponManager.guardMode != newState)
                                    pilot.weaponManager.ToggleGuardMode();
                            }
                        }
                        else
                        {
                            pilots = getAllPilots();
                            foreach (var pilot in pilots)
                            {
                                if (pilot.weaponManager != null) pilot.weaponManager.ToggleGuardMode();
                            }
                        }

                        break;
                    case "SetThrottle":
                        if (parts.Count() == 3)
                        {
                            pilots = getAllPilots();
                            foreach (var pilot in pilots)
                            {
                                var throttle = int.Parse(parts[2]) * 0.01f;
                                pilot.vessel.ctrlState.mainThrottle = throttle;
                                pilot.vessel.ctrlState.killRot = true;
                            }
                        }
                        break;
                    case "RemoveDebris":
                        // remove anything that doesn't contain BD Armory modules
                        RemoveDebris();
                        break;
                    case "RemoveFairings":
                        // removes the fairings after deplyment to stop the physical objects consuming CPU
                        var rmObj = new List<physicalObject>();
                        foreach (var phyObj in FlightGlobals.physicalObjects)
                        {
                            if (phyObj.name == "FairingPanel") rmObj.Add(phyObj);
                            Debug.Log("[RemoveFairings] " + phyObj.name);
                        }
                        foreach (var phyObj in rmObj)
                        {
                            FlightGlobals.removePhysicalObject(phyObj);
                        }

                        break;
                    case "EnableGM":
                        killerGMenabled = true;
                        decisionTick = Planetarium.GetUniversalTime() + 60;
                        ResetSpeeds();
                        break;
                }
            }
            competitionStatus = "";
            lastCompetitionStatus = "";
            // will need a terminator routine
            competitionStarting = false;
        }

        private void RemoveDebris()
        {
            // only call this if enabled
            // remove anything that doesn't contain BD Armory modules
            var debrisToKill = new List<Vessel>();
            foreach (var vessel in FlightGlobals.Vessels)
            {
                bool activePilot = false;
                if (vessel.GetName() == "Pinata")
                {
                    activePilot = true;
                }
                else
                {
                    int foundActiveParts = 0;
                    using (var wms = vessel.FindPartModulesImplementing<MissileFire>().GetEnumerator())
                        while (wms.MoveNext())
                            if (wms.Current != null)
                            {
                                foundActiveParts++;
                                break;
                            }

                    using (var wms = vessel.FindPartModulesImplementing<IBDAIControl>().GetEnumerator())
                        while (wms.MoveNext())
                            if (wms.Current != null)
                            {
                                foundActiveParts++;
                                break;
                            }

                    using (var wms = vessel.FindPartModulesImplementing<ModuleCommand>().GetEnumerator())
                        while (wms.MoveNext())
                            if (wms.Current != null)
                            {
                                foundActiveParts++;
                                break;
                            }
                    activePilot = foundActiveParts == 3;
                }
                if (!activePilot)
                    debrisToKill.Add(vessel);
            }
            foreach (var vessel in debrisToKill)
            {
                Debug.Log("[RemoveObjects] " + vessel.GetName());
                vessel.Die();
            }
        }


        // ask the GM to find a 'victim' which means a slow pilot who's not shooting very much
        // obviosly this is evil. 
        // it's enabled by right clicking the M button.
        // I also had it hooked up to the death of the Pinata but that's disconnected right now
        private void FindVictim()
        {
            if (decisionTick < 0) return;
            if (Planetarium.GetUniversalTime() < decisionTick) return;
            decisionTick = Planetarium.GetUniversalTime() + 60;
            RemoveDebris();
            if (!killerGMenabled) return;
            if (Planetarium.GetUniversalTime() - competitionStartTime < 150) return;
            // arbitrary and capbricious decisions of life and death

            bool hasFired = true;
            Vessel worstVessel = null;
            double slowestSpeed = 100000;
            int vesselCount = 0;
            using (var loadedVessels = BDATargetManager.LoadedVessels.GetEnumerator())
                while (loadedVessels.MoveNext())
                {
                    if (loadedVessels.Current == null || !loadedVessels.Current.loaded)
                        continue;
                    IBDAIControl pilot = loadedVessels.Current.FindPartModuleImplementing<IBDAIControl>();
                    if (pilot == null || !pilot.weaponManager || pilot.weaponManager.Team.Neutral)
                        continue;



                    var vesselName = loadedVessels.Current.GetName();
                    if (!Scores.ContainsKey(vesselName))
                        continue;

                    vesselCount++;
                    ScoringData vData = Scores[vesselName];

                    var averageSpeed = vData.AverageSpeed / vData.averageCount;
                    var averageAltitude = vData.AverageAltitude / vData.averageCount;
                    averageSpeed = averageAltitude + (averageSpeed * averageSpeed / 200); // kinetic & potential energy
                    if (pilot.weaponManager != null)
                    {
                        if (!pilot.weaponManager.guardMode) averageSpeed *= 0.5;
                    }

                    bool vesselNotFired = (Planetarium.GetUniversalTime() - vData.lastFiredTime) > 120; // if you can't shoot in 2 minutes you're at the front of line

                    Debug.Log("[BDArmory] Victim Check " + vesselName + " " + averageSpeed.ToString() + " " + vesselNotFired.ToString());
                    if (hasFired)
                    {
                        if (vesselNotFired)
                        {
                            // we found a vessel which hasn't fired
                            worstVessel = loadedVessels.Current;
                            slowestSpeed = averageSpeed;
                            hasFired = false;
                        }
                        else if (averageSpeed < slowestSpeed)
                        {
                            // this vessel fired but is slow
                            worstVessel = loadedVessels.Current;
                            slowestSpeed = averageSpeed;
                        }
                    }
                    else
                    {
                        if (vesselNotFired)
                        {
                            // this vessel was slow and hasn't fired
                            worstVessel = loadedVessels.Current;
                            slowestSpeed = averageSpeed;
                        }
                    }
                }
            // if we have 3 or more vessels kill the slowest
            if (vesselCount > 2 && worstVessel != null)
            {
                if (!Scores.ContainsKey(worstVessel.GetName()))
                {
                    if (Scores[worstVessel.GetName()].lastPersonWhoHitMe == "")
                    {
                        Scores[worstVessel.GetName()].lastPersonWhoHitMe = "GM";
                    }
                }
                Debug.Log("[BDArmory] killing " + worstVessel.GetName());
                Misc.Misc.ForceDeadVessel(worstVessel);
            }
            ResetSpeeds();
        }

        // reset all the tracked speeds, and copy the shot clock over, because I wanted 2 minutes of shooting to count
        private void ResetSpeeds()
        {
            Debug.Log("[BDArmory] resetting kill clock");
            foreach (var vname in Scores.Keys)
            {
                if (Scores[vname].averageCount == 0)
                {
                    Scores[vname].AverageAltitude = 0;
                    Scores[vname].AverageSpeed = 0;
                }
                else
                {
                    // ensures we always have a sensible value in here
                    Scores[vname].AverageAltitude /= Scores[vname].averageCount;
                    Scores[vname].AverageSpeed /= Scores[vname].averageCount;
                    Scores[vname].averageCount = 1;
                }
            }
        }

        // the competition update system
        // cleans up dead vessels, tries to track kills (badly)
        // all of these are based on the vessel name which is probably sub optimal
        public void DoUpdate()
        {
            // should be called every frame during flight scenes
            if (competitionStartTime < 0) return;
            LogRamming();
            if (Planetarium.GetUniversalTime() < nextUpdateTick)
                return;
            int updateTickLength = 2;
            HashSet<Vessel> vesselsToKill = new HashSet<Vessel>();
            nextUpdateTick = nextUpdateTick + updateTickLength;
            int numberOfCompetitiveVessels = 0;
            if (!competitionStarting)
                competitionStatus = "";
            HashSet<string> alive = new HashSet<string>();
            string doaUpdate = "ALIVE: ";
            //Debug.Log("[BDArmoryCompetitionMode] Calling Update");
            // check all the planes
            using (List<Vessel>.Enumerator v = FlightGlobals.Vessels.GetEnumerator())
                while (v.MoveNext())
                {
                    if (v.Current == null || !v.Current.loaded || v.Current.packed)
                        continue;

                    MissileFire mf = null;

                    using (var wms = v.Current.FindPartModulesImplementing<MissileFire>().GetEnumerator())
                        while (wms.MoveNext())
                            if (wms.Current != null)
                            {
                                mf = wms.Current;
                                break;
                            }

                    if (mf != null)
                    {
                        // things to check
                        // does it have fuel?
                        string vesselName = v.Current.GetName();
                        ScoringData vData = null;
                        if (Scores.ContainsKey(vesselName))
                        {
                            vData = Scores[vesselName];
                        }

                        // this vessel really is alive
                        if (!vesselName.EndsWith("Debris") && !vesselName.EndsWith("Plane") && !vesselName.EndsWith("Probe"))
                        {
                            if (DeathOrder.ContainsKey(vesselName))
                            {
                                Debug.Log("[BDArmoryCompetition] Dead vessel found alive " + vesselName);
                                //DeathOrder.Remove(vesselName);
                            }
                            // vessel is still alive
                            alive.Add(vesselName);
                            doaUpdate += " *" + vesselName + "* ";
                            numberOfCompetitiveVessels++;
                        }
                        pilotActions[vesselName] = "";

                        // try to create meaningful activity strings
                        if (mf.AI != null && mf.AI.currentStatus != null)
                        {
                            pilotActions[vesselName] = "";
                            if (mf.vessel.LandedOrSplashed)
                            {
                                if (mf.vessel.Landed)
                                {
                                    pilotActions[vesselName] = " landed";
                                }
                                else
                                {
                                    pilotActions[vesselName] = " splashed";
                                }
                            }
                            var activity = mf.AI.currentStatus;
                            if (activity == "Follow")
                            {
                                if (mf.AI.commandLeader != null && mf.AI.commandLeader.vessel != null)
                                {
                                    pilotActions[vesselName] = " following " + mf.AI.commandLeader.vessel.GetName();
                                }
                            }
                            else if (activity == "Gain Alt")
                            {
                                pilotActions[vesselName] = " gaining altitude";
                            }
                            else if (activity == "Orbiting")
                            {
                                pilotActions[vesselName] = " orbiting";
                            }
                            else if (activity == "Extending")
                            {
                                pilotActions[vesselName] = " extending ";
                            }
                            else if (activity == "AvoidCollision")
                            {
                                pilotActions[vesselName] = " avoiding collision";
                            }
                            else if (activity == "Evading")
                            {
                                if (mf.incomingThreatVessel != null)
                                {
                                    pilotActions[vesselName] = " evading " + mf.incomingThreatVessel.GetName();
                                }
                                else
                                {
                                    pilotActions[vesselName] = " taking evasive action";
                                }
                            }
                            else if (activity == "Attack")
                            {
                                if (mf.currentTarget != null && mf.currentTarget.name != null)
                                {
                                    pilotActions[vesselName] = " attacking " + mf.currentTarget.Vessel.GetName();
                                }
                                else
                                {
                                    pilotActions[vesselName] = " attacking ";
                                }
                            }
                        }

                        // update the vessel scoring structure
                        if (vData != null)
                        {
                            var partCount = v.Current.parts.Count();
                            if (partCount != vData.previousPartCount)
                            {
                                // part count has changed, check for broken stuff
                                enforcePartCount(v.Current);
                            }
                            vData.previousPartCount = v.Current.parts.Count();

                            if (v.Current.LandedOrSplashed)
                            {
                                if (!vData.landedState)
                                {
                                    // was flying, is now landed
                                    vData.lastLandedTime = Planetarium.GetUniversalTime();
                                    vData.landedState = true;
                                    if (vData.landerKillTimer == 0)
                                    {
                                        vData.landerKillTimer = Planetarium.GetUniversalTime();
                                    }
                                }
                            }
                            else
                            {
                                if (vData.landedState)
                                {
                                    vData.lastLandedTime = Planetarium.GetUniversalTime();
                                    vData.landedState = false;
                                }
                                if (vData.landerKillTimer != 0)
                                {
                                    // safely airborne for 15 seconds
                                    if (Planetarium.GetUniversalTime() - vData.landerKillTimer > 15)
                                    {
                                        vData.landerKillTimer = 0;
                                    }
                                }
                            }
                        }

                        // after this point we're checking things that might result in kills.
                        if (Planetarium.GetUniversalTime() < gracePeriod) continue;

                        // keep track if they're shooting for the GM
                        if (mf.currentGun != null)
                        {
                            if (mf.currentGun.recentlyFiring)
                            {
                                // keep track that this aircraft was shooting things
                                if (vData != null)
                                {
                                    vData.lastFiredTime = Planetarium.GetUniversalTime();
                                }
                                if (mf.currentTarget != null && mf.currentTarget.Vessel != null)
                                {
                                    pilotActions[vesselName] = " shooting at " + mf.currentTarget.Vessel.GetName();
                                }
                            }
                        }
                        // does it have ammunition: no ammo => Disable guard mode
                        if (!BDArmorySettings.INFINITE_AMMO)
                        {
                            var vesselAI = v.Current.FindPartModuleImplementing<BDModulePilotAI>(); // Get the pilot AI if the vessel has one.
                            if (vesselAI != null && vesselAI.outOfAmmo && !outOfAmmo.Contains(vesselName)) // Report being out of ammo/guns once.
                            {
                                outOfAmmo.Add(vesselName);
                                if (vData != null && (Planetarium.GetUniversalTime() - vData.lastHitTime < 2))
                                {
                                    competitionStatus = vesselName + " damaged by " + vData.LastPersonWhoDamagedMe() + " and lost weapons";
                                }
                                else
                                {
                                    competitionStatus = vesselName + " is out of Ammunition";
                                }
                            }
                            if ((vesselAI == null || (vesselAI.outOfAmmo && (BDArmorySettings.DISABLE_RAMMING || !vesselAI.allowRamming))) && mf.guardMode) // disable guard mode when out of ammo/guns if ramming is not allowed.
                                mf.guardMode = false;
                        }

                        // update the vessel scoring structure
                        if (vData != null)
                        {
                            vData.AverageSpeed += v.Current.srfSpeed;
                            vData.AverageAltitude += v.Current.altitude;
                            vData.averageCount++;
                            if (vData.landedState)
                            {
                                if (Planetarium.GetUniversalTime() - vData.landerKillTimer > 15)
                                {
                                    vesselsToKill.Add(mf.vessel);
                                    competitionStatus = vesselName + " landed too long.";
                                }
                            }
                        }


                        bool shouldKillThis = false;

                        // if vessels is Debris, kill it
                        if (vesselName.Contains("Debris"))
                        {
                            // reap this vessel
                            shouldKillThis = true;
                        }

                        if (vData == null) shouldKillThis = true;

                        // 15 second time until kill, maybe they recover?
                        if (KillTimer.ContainsKey(vesselName))
                        {
                            if (shouldKillThis)
                            {
                                KillTimer[vesselName] += updateTickLength;
                            }
                            else
                            {
                                KillTimer[vesselName] -= updateTickLength;
                            }
                            if (KillTimer[vesselName] > 15)
                            {
                                vesselsToKill.Add(mf.vessel);
                                competitionStatus = vesselName + " exceeded kill timer";
                            }
                            else if (KillTimer[vesselName] < 0)
                            {
                                KillTimer.Remove(vesselName);
                            }
                        }
                        else
                        {
                            if (shouldKillThis)
                                KillTimer[vesselName] = updateTickLength;
                        }
                    }
                }
            string aliveString = string.Join(",", alive.ToArray());
            Debug.Log("[BDArmoryCompetition:" + CompetitionID.ToString() + "] STILLALIVE: " + aliveString);
            // If we find a vessel named "Pinata" that's a special case object
            // this should probably be configurable.
            if (!pinataAlive && alive.Contains("Pinata"))
            {
                Debug.Log("[BDACompetitionMode] Setting Pinata Flag to Alive!");
                pinataAlive = true;
                competitionStatus = "Enabling Pinata";
            }
            else if (pinataAlive && !alive.Contains("Pinata"))
            {
                // switch everyone onto separate teams when the Pinata Dies
                LoadedVesselSwitcher.MassTeamSwitch();
                pinataAlive = false;
                competitionStatus = "Pinata is dead - competition is now a Free for all";
                // start kill clock
                if (!killerGMenabled)
                {
                    // disabled for now, should be in a competition settings UI
                    //BDACompetitionMode.Instance.killerGMenabled = true;

                }

            }
            doaUpdate += "     DEAD: ";
            foreach (string key in Scores.Keys)
            {
                // check everyone who's no longer alive
                if (!alive.Contains(key))
                {
                    if (key != "Pinata")
                    {
                        if (!DeathOrder.ContainsKey(key))
                        {

                            // adding pilot into death order
                            DeathOrder[key] = DeathOrder.Count;
                            pilotActions[key] = " is Dead";
                            var whoKilledMe = "";

                            if (Scores.ContainsKey(key))
                            {
                                if (Planetarium.GetUniversalTime() - Scores[key].lastHitTime < 10 || Planetarium.GetUniversalTime() - Scores[key].lastRammedTime < 10)
                                {
                                    // if last hit was recent that person gets the kill
                                    whoKilledMe = Scores[key].LastPersonWhoDamagedMe();
                                    if (Scores[key].lastHitTime > Scores[key].lastRammedTime)
                                    {
                                        // twice - so 2 points
                                        Debug.Log("[BDArmoryCompetition: " + CompetitionID.ToString() + "]: " + key + ":CLEANKILL:" + whoKilledMe);
                                        Debug.Log("[BDArmoryCompetition: " + CompetitionID.ToString() + "]: " + key + ":KILLED:" + whoKilledMe);
                                        whoKilledMe += " (BOOM! HEADSHOT!)";
                                    }
                                    else if (Scores[key].lastHitTime < Scores[key].lastRammedTime)
                                    {
                                        // if ram killed
                                        Debug.Log("[BDArmoryCompetition: " + CompetitionID.ToString() + "]: " + key + ":CLEANRAMKILL:" + whoKilledMe);
                                        Debug.Log("[BDArmoryCompetition: " + CompetitionID.ToString() + "]: " + key + ":KILLED VIA RAMMERY BY:" + whoKilledMe);
                                        whoKilledMe += " (BOOM! HEADSHOT!)";
                                    }

                                }
                                else if (Scores[key].everyoneWhoHitMe.Count > 0 || Scores[key].everyoneWhoRammedMe.Count > 0)
                                {
                                    //check if anyone got rammed
                                    if (Scores[key].everyoneWhoRammedMe.Count != 0)
                                        whoKilledMe = "Ram Hits: " + String.Join(", ", Scores[key].everyoneWhoRammedMe) + " ";
                                    else if (Scores[key].everyoneWhoHitMe.Count != 0)
                                        if (whoKilledMe != "") whoKilledMe += "Hits: " + String.Join(", ", Scores[key].everyoneWhoHitMe); else whoKilledMe = "Hits: " + String.Join(", ", Scores[key].everyoneWhoHitMe);

                                    foreach (var killer in Scores[key].EveryOneWhoDamagedMe())
                                    {
                                        Debug.Log("[BDArmoryCompetition: " + CompetitionID.ToString() + "]: " + key + ":KILLED:" + killer);
                                    }
                                }
                            }
                            if (whoKilledMe != "")
                            {
                                if (Scores[key].lastHitTime > Scores[key].lastRammedTime)
                                    competitionStatus = key + " was killed by " + whoKilledMe;
                                else if (Scores[key].lastHitTime < Scores[key].lastRammedTime)
                                    competitionStatus = key + " was rammed by " + whoKilledMe;
                            }
                            else
                            {
                                competitionStatus = key + " was killed";
                                Debug.Log("[BDArmoryCompetition: " + CompetitionID.ToString() + "]: " + key + ":KILLED:NOBODY");
                            }
                        }
                        doaUpdate += " :" + key + ": ";
                    }
                }
            }
            deadOrAlive = doaUpdate;

            if ((Planetarium.GetUniversalTime() > gracePeriod) && numberOfCompetitiveVessels < 2)
            {
                competitionStatus = "All Pilots are Dead";
                foreach (string key in alive)
                {
                    competitionStatus = key + " wins the round!";
                }
                if (dumpedResults > 0)
                {
                    dumpedResults--;
                }
                else if (dumpedResults == 0)
                {
                    Debug.Log("[BDArmoryCompetition: " + CompetitionID.ToString() + "]:No viable competitors, Automatically dumping scores");
                    LogResults();
                    dumpedResults--;
                    //competitionStartTime = -1;
                }
                competitionIsActive = false;
            }
            else
            {
                dumpedResults = 5;
            }

            // use the exploder system to remove vessels that should be nuked
            foreach (var vessel in vesselsToKill)
            {
                var vesselName = vessel.GetName();
                var killerName = "";
                if (Scores.ContainsKey(vesselName))
                {
                    killerName = Scores[vesselName].LastPersonWhoDamagedMe();
                    if (killerName == "")
                    {
                        Scores[vesselName].lastPersonWhoHitMe = "Landed Too Long"; // only do this if it's not already damaged
                        killerName = "Landed Too Long";
                    }
                }
                Debug.Log("[BDArmoryCompetition: " + CompetitionID.ToString() + "]: " + vesselName + ":REMOVED:" + killerName);
                Misc.Misc.ForceDeadVessel(vessel);
                KillTimer.Remove(vesselName);
            }

            FindVictim();
            Debug.Log("[BDArmoryCompetition] Done With Update");
        }

        public void LogResults()
        {
            // get everyone who's still alive
            HashSet<string> alive = new HashSet<string>();
            Debug.Log("[BDArmoryCompetition:" + CompetitionID.ToString() + "]: Dumping Results ");


            using (List<Vessel>.Enumerator v = FlightGlobals.Vessels.GetEnumerator())
                while (v.MoveNext())
                {
                    if (v.Current == null || !v.Current.loaded || v.Current.packed)
                        continue;
                    using (var wms = v.Current.FindPartModulesImplementing<MissileFire>().GetEnumerator())
                        while (wms.MoveNext())
                            if (wms.Current != null)
                            {
                                if (wms.Current.vessel != null)
                                {
                                    alive.Add(wms.Current.vessel.GetName());
                                    Debug.Log("[BDArmoryCompetition:" + CompetitionID.ToString() + "]: ALIVE:" + wms.Current.vessel.GetName());
                                }
                                break;
                            }
                }


            //  find out who's still alive
            foreach (string key in Scores.Keys)
            {
                if (!alive.Contains(key))
                    Debug.Log("[BDArmoryCompetition:" + CompetitionID.ToString() + "]: DEAD:" + DeathOrder[key] + ":" + key);
            }

            foreach (string key in whoShotWho.Keys)
            {
                Debug.Log("[BDArmoryCompetition:" + CompetitionID.ToString() + "]: WHOSHOTWHO:" + whoShotWho[key] + ":" + key);
            }

            foreach (string key in whoRammedWho.Keys)
            {
                Debug.Log("[BDArmoryCompetition:" + CompetitionID.ToString() + "]: WHORAMMEDWHO" + whoRammedWho[key] + ":" + key);
            }
        }


        // Ramming Logging
        public class RammingTargetInformation
        {
            public Vessel vessel; // The other vessel involved in a collision.
            public double lastUpdateTime; // Last time the timeToCPA was updated.
            public float timeToCPA; // Time to closest point of approach.
            public bool potentialCollision; // Whether a collision might happen shortly.
            public double potentialCollisionDetectionTime; // The latest time the potential collision was detected.
            public int partCount; // The part count of a vessel.
            public float angleToCoM; // The angle from a vessel's velocity direction to the center of mass of the target.
            public bool collisionDetected; // Whether a collision has actually been detected.
            public bool ramming; // True if a ram was attempted between the detection of a potential ram and the actual collision.
        };
        public class RammingInformation
        {
            public Vessel vessel; // This vessel.
            public string vesselName; // The GetName() name of the vessel (in case vessel gets destroyed and we can't get it from there).
            public Dictionary<string, RammingTargetInformation> targetInformation; // Information about the ramming target.
        };
        public Dictionary<string, RammingInformation> rammingInformation;

        // Initialise the rammingInformation dictionary with the required vessels.
        public void InitialiseRammingInformation()
        {
            double currentTime = Planetarium.GetUniversalTime();
            rammingInformation = new Dictionary<string, RammingInformation>();
            foreach (var vessel in BDATargetManager.LoadedVessels)
            {
                var pilotAI = vessel.FindPartModuleImplementing<BDModulePilotAI>(); // Get the pilot AI if the vessel has one.
                if (pilotAI == null) continue;
                var targetRammingInformation = new Dictionary<string, RammingTargetInformation>();
                foreach (var otherVessel in BDATargetManager.LoadedVessels)
                {
                    var otherPilotAI = otherVessel.FindPartModuleImplementing<BDModulePilotAI>(); // Get the pilot AI if the vessel has one.
                    if (otherPilotAI == null) continue;
                    targetRammingInformation.Add(otherVessel.vesselName, new RammingTargetInformation
                    {
                        vessel = otherVessel,
                        lastUpdateTime = currentTime,
                        timeToCPA = 0f,
                        potentialCollision = false,
                        partCount = vessel.parts.Count,
                        angleToCoM = 0f,
                        collisionDetected = false,
                        ramming = false,
                    });
                }
                rammingInformation.Add(vessel.vesselName, new RammingInformation
                {
                    vessel = vessel,
                    vesselName = vessel.GetName(),
                    targetInformation = targetRammingInformation,
                });
            }
        }

        // Update the ramming information dictionary with expected times to closest point of approach.
        public void UpdateTimesToCPAs()
        {
            float maxTime = 5f;
            double currentTime = Planetarium.GetUniversalTime();
            foreach (var vesselName in rammingInformation.Keys)
            {
                var vessel = rammingInformation[vesselName].vessel;
                if (vessel == null) continue; // Vessel has been destroyed.
                var pilotAI = vessel.FindPartModuleImplementing<BDModulePilotAI>(); // Get the pilot AI if the vessel has one.
                if (pilotAI == null) continue; // Pilot AI has been destroyed.
                foreach (var otherVesselName in rammingInformation[vesselName].targetInformation.Keys)
                {
                    var otherVessel = rammingInformation[vesselName].targetInformation[otherVesselName].vessel;
                    if (otherVessel == null) continue; // Vessel has been destroyed.
                    var otherPilotAI = otherVessel.FindPartModuleImplementing<BDModulePilotAI>(); // Get the pilot AI if the vessel has one.
                    if (otherPilotAI == null) continue; // Pilot AI has been destroyed.
                    if (currentTime - rammingInformation[vesselName].targetInformation[otherVesselName].lastUpdateTime > rammingInformation[vesselName].targetInformation[otherVesselName].timeToCPA / 2f) // When half the time is gone, update it.
                    {
                        float timeToCPA = pilotAI.ClosestTimeToCPA(otherPilotAI.vessel, maxTime); // Look up to maxTime ahead.
                        if (timeToCPA > 0f && timeToCPA < maxTime) // If the closest approach is within the next maxTime, log it.
                            rammingInformation[vesselName].targetInformation[otherVesselName].timeToCPA = timeToCPA;
                        else // Otherwise set it to the max value.
                            rammingInformation[vesselName].targetInformation[otherVesselName].timeToCPA = maxTime;
                        // This is symmetric, so update the symmetric value and set the lastUpdateTime for both so that we don't bother calculating the same thing twice.
                        rammingInformation[vesselName].targetInformation[otherVesselName].timeToCPA = rammingInformation[otherVesselName].targetInformation[vesselName].timeToCPA;
                        rammingInformation[vesselName].targetInformation[otherVesselName].lastUpdateTime = currentTime;
                        rammingInformation[otherVesselName].targetInformation[vesselName].lastUpdateTime = currentTime;
                    }
                }
            }
        }

        // Check for potential collisions in the near future and update data structures as necessary.
        private void CheckForPotentialCollisions()
        {
            float detectionTime = BDArmorySettings.RAM_LOGGING_COLLISION_UPDATE;
            float collisionMargin = BDArmorySettings.RAM_LOGGING_RADIUS_OFFSET;
            double currentTime = Planetarium.GetUniversalTime();
            foreach (var vesselName in rammingInformation.Keys)
            {
                var vessel = rammingInformation[vesselName].vessel;
                foreach (var otherVesselName in rammingInformation[vesselName].targetInformation.Keys)
                {
                    var otherVessel = rammingInformation[vesselName].targetInformation[otherVesselName].vessel;
                    if (rammingInformation[vesselName].targetInformation[otherVesselName].timeToCPA < detectionTime) // Closest point of approach is within the detectionTime.
                    {
                        if (vessel == null || otherVessel == null) continue; // One of the vessels have been destroyed. Don't calculate new potential collisions, but allow the timer on existing potential collisions to run out so that collision analysis can still use it.
                        var separation = Vector3.Magnitude(vessel.transform.position - otherVessel.transform.position);
                        if (separation < GetRadius(vessel) + GetRadius(otherVessel) + collisionMargin) // Potential collision detected.
                        {
                            // FIXME If we get shot since the potential collision detection time, we should reset the part count.
                            if (!rammingInformation[vesselName].targetInformation[otherVesselName].potentialCollision) // Register the part counts and angles when the potential collision is first detected.
                            {
                                rammingInformation[vesselName].targetInformation[otherVesselName].partCount = otherVessel.parts.Count;
                                rammingInformation[otherVesselName].targetInformation[vesselName].partCount = vessel.parts.Count;
                                rammingInformation[vesselName].targetInformation[otherVesselName].angleToCoM = Vector3.Angle(vessel.srf_vel_direction, otherVessel.CoM - vessel.CoM);
                                rammingInformation[otherVesselName].targetInformation[vesselName].angleToCoM = Vector3.Angle(otherVessel.srf_vel_direction, vessel.CoM - otherVessel.CoM);
                            }
                            // Set the potentialCollision flag to true and update the latest potential collision detection time.
                            rammingInformation[vesselName].targetInformation[otherVesselName].potentialCollision = true;
                            rammingInformation[vesselName].targetInformation[otherVesselName].potentialCollisionDetectionTime = currentTime;
                            rammingInformation[otherVesselName].targetInformation[vesselName].potentialCollision = true;
                            rammingInformation[otherVesselName].targetInformation[vesselName].potentialCollisionDetectionTime = currentTime;

                            // Register intent to ram.
                            var pilotAI = vessel.FindPartModuleImplementing<BDModulePilotAI>();
                            rammingInformation[vesselName].targetInformation[otherVesselName].ramming |= (pilotAI != null && pilotAI.ramming); // Pilot AI is alive and trying to ram.
                            var otherPilotAI = otherVessel.FindPartModuleImplementing<BDModulePilotAI>();
                            rammingInformation[otherVesselName].targetInformation[vesselName].ramming |= (otherPilotAI != null && otherPilotAI.ramming); // Other pilot AI is alive and trying to ram.
                        }
                    }
                    else if (currentTime - rammingInformation[vesselName].targetInformation[otherVesselName].potentialCollisionDetectionTime > 2f * detectionTime) // Potential collision is no longer relevant.
                    {
                        rammingInformation[vesselName].targetInformation[otherVesselName].potentialCollision = false;
                        rammingInformation[otherVesselName].targetInformation[vesselName].potentialCollision = false;
                    }
                }
            }
        }

        // Get a vessel's "radius".
        private float GetRadius(Vessel v)
        {
            //get vessel size
            Vector3 size = v.vesselSize;

            //get largest dimension
            float radius;

            if (size.x > size.y && size.x > size.z)
            {
                radius = size.x / 2;
            }
            else if (size.y > size.x && size.y > size.z)
            {
                radius = size.y / 2;
            }
            else if (size.z > size.x && size.z > size.y)
            {
                radius = size.z / 2;
            }
            else
            {
                radius = size.x / 2;
            }

            return radius;
        }

        // Analyse a collision to figure out if someone rammed someone else and who should get awarded for it.
        private void AnalyseCollision(EventReport data)
        {
            float collisionMargin = BDArmorySettings.RAM_LOGGING_RADIUS_OFFSET / 2f;
            var vessel = data.origin.vessel;
            if (vessel == null) // Can vessel be null here?
            {
                Debug.Log("DEBUG in AnalyseCollision the colliding part belonged to a null vessel!");
                return;
            }
            if (rammingInformation.ContainsKey(vessel.vesselName)) // If the part was attached to a vessel,
                foreach (var otherVesselName in rammingInformation[vessel.vesselName].targetInformation.Keys) // for each other vessel,
                    if (rammingInformation[vessel.vesselName].targetInformation[otherVesselName].potentialCollision) // if it was potentially about to collide,
                    {
                        var otherVessel = rammingInformation[vessel.vesselName].targetInformation[otherVesselName].vessel;
                        if (otherVessel == null) // Vessel that was potentially colliding has been destroyed. Assume it was in the collision.
                        {
                            rammingInformation[vessel.vesselName].targetInformation[otherVesselName].collisionDetected = true; // register it as involved in the collision. We'll check for damaged parts in CheckForDamagedParts.
                            continue;
                        }
                        var separation = Vector3.Magnitude(vessel.transform.position - otherVessel.transform.position);
                        if (separation < GetRadius(vessel) + GetRadius(otherVessel) + collisionMargin) // and their separation is less than the sum of their radii, // FIXME Is this sufficient? It ought to be.
                        {
                            rammingInformation[vessel.vesselName].targetInformation[otherVesselName].collisionDetected = true; // register it as involved in the collision. We'll check for damaged parts in CheckForDamagedParts.
                            rammingInformation[otherVesselName].targetInformation[vessel.vesselName].collisionDetected = true; // The information is symmetric.
                            Debug.Log("DEBUG Collision detected between " + vessel.vesselName + " and " + otherVesselName);
                        }
                    }
        }

        // Check for parts being lost on the various vessels for which collisions have been detected.
        private void CheckForDamagedParts()
        {
            double currentTime = Planetarium.GetUniversalTime();
            float headOnLimit = 20f;
            foreach (var vesselName in rammingInformation.Keys)
            {
                var vessel = rammingInformation[vesselName].vessel;
                foreach (var otherVesselName in rammingInformation[vesselName].targetInformation.Keys)
                {
                    if (!rammingInformation[vesselName].targetInformation[otherVesselName].collisionDetected) continue; // Other vessel wasn't involved in a collision with this vessel.
                    if (currentTime - rammingInformation[vesselName].targetInformation[otherVesselName].potentialCollisionDetectionTime > BDArmorySettings.RAM_LOGGING_COLLISION_UPDATE) // We've waited long enough for the parts that are going to explode to explode.
                    {
                        var otherVessel = rammingInformation[vesselName].targetInformation[otherVesselName].vessel;
                        var pilotAI = vessel?.FindPartModuleImplementing<BDModulePilotAI>();
                        var otherPilotAI = otherVessel?.FindPartModuleImplementing<BDModulePilotAI>();
                        // Count the number of parts lost. If the vessel or pilot AI on the rammed vessel is destroyed, then it's a headshot. FIXME Add option for showing this in LogRammingVesselScore.
                        var partsLost = (otherPilotAI == null) ? rammingInformation[vesselName].targetInformation[otherVesselName].partCount : rammingInformation[vesselName].targetInformation[otherVesselName].partCount - otherVessel.parts.Count;
                        var otherPartsLost = (pilotAI == null) ? rammingInformation[otherVesselName].targetInformation[vesselName].partCount : rammingInformation[otherVesselName].targetInformation[vesselName].partCount - vessel.parts.Count; // Count the number of parts lost.
                        var headOn = false;

                        // Figure out who should be awarded the ram.
                        var rammingVessel = rammingInformation[vesselName].vesselName;
                        var rammedVessel = rammingInformation[otherVesselName].vesselName;
                        if (rammingInformation[vesselName].targetInformation[otherVesselName].ramming ^ rammingInformation[otherVesselName].targetInformation[vesselName].ramming) // Only one of the vessels was ramming.
                        {
                            if (!rammingInformation[vesselName].targetInformation[otherVesselName].ramming) // Switch who rammed who if the default is backwards.
                            {
                                rammingVessel = rammingInformation[otherVesselName].vesselName;
                                rammedVessel = rammingInformation[vesselName].vesselName;
                                var tmp = partsLost;
                                partsLost = otherPartsLost;
                                otherPartsLost = tmp;
                            }
                        }
                        else // Both or neither of the vessels were ramming.
                        {
                            if (rammingInformation[vesselName].targetInformation[otherVesselName].angleToCoM < headOnLimit && rammingInformation[otherVesselName].targetInformation[vesselName].angleToCoM < headOnLimit) // Head-on collision detected, both get awarded with ramming the other.
                            {
                                headOn = true;
                            }
                            else
                            {
                                if (rammingInformation[vesselName].targetInformation[otherVesselName].angleToCoM > rammingInformation[otherVesselName].targetInformation[vesselName].angleToCoM) // Other vessel had a better angleToCoM, so switch who rammed who.
                                {
                                    rammingVessel = rammingInformation[otherVesselName].vesselName;
                                    rammedVessel = rammingInformation[vesselName].vesselName;
                                    var tmp = partsLost;
                                    partsLost = otherPartsLost;
                                    otherPartsLost = tmp;
                                }
                            }
                        }

                        LogRammingVesselScore(rammingVessel, rammedVessel, partsLost, otherPartsLost, headOn, true, true); // Log the ram.

                        // Set the collisionDetected flag to false, since we've now logged this collision. We set both so that the collision only gets logged once.
                        rammingInformation[vesselName].targetInformation[otherVesselName].collisionDetected = false;
                        rammingInformation[otherVesselName].targetInformation[vesselName].collisionDetected = false;
                    }
                }
            }
        }

        // Actually log the ram to various places. Note: vesselName and targetVesselName need to be those returned by the GetName() function to match the keys in Scores.
        public void LogRammingVesselScore(string vesselName, string targetVesselName, int partsLost, int otherPartsLost, bool headOn, bool logToCompetitionStatus, bool logToDebug)
        {
            if (logToCompetitionStatus)
            {
                if (!headOn)
                    competitionStatus = targetVesselName + " got RAMMED by " + vesselName + " and lost " + partsLost + " parts.";
                else
                    competitionStatus = targetVesselName + " and " + vesselName + " RAMMED each other and lost " + partsLost + " and " + otherPartsLost + " parts, respectively.";
            }
            if (logToDebug)
            {
                if (!headOn)
                    Debug.Log("[BDArmoryCompetition:" + CompetitionID.ToString() + "]: " + targetVesselName + " got RAMMED by " + vesselName + " and lost " + partsLost + " parts.");
                else
                    Debug.Log("[BDArmoryCompetition:" + CompetitionID.ToString() + "]: " + targetVesselName + " and " + vesselName + " RAMMED each other and lost " + partsLost + " and " + otherPartsLost + " parts, respectively.");
            }

            //log scores
            var vData = Scores[vesselName];
            vData.totalDamagedParts += partsLost;
            var key = vesselName + ":" + targetVesselName;
            if (whoRammedWho.ContainsKey(key))
                whoRammedWho[key] += partsLost;
            else
                whoRammedWho.Add(key, partsLost);
            if (headOn)
            {
                var tData = Scores[targetVesselName];
                tData.totalDamagedParts += partsLost;
                key = targetVesselName + ":" + vesselName;
                if (whoRammedWho.ContainsKey(key))
                    whoRammedWho[key] += partsLost;
                else
                    whoRammedWho.Add(key, partsLost);
            }
        }

        Dictionary<string, int> partsCheck;
        void CheckForMissingParts()
        {
            if (partsCheck == null)
            {
                partsCheck = new Dictionary<string, int>();
                foreach (var vesselName in rammingInformation.Keys)
                    partsCheck.Add(vesselName, rammingInformation[vesselName].vessel.parts.Count);
            }
            foreach (var vesselName in rammingInformation.Keys)
            {
                var vessel = rammingInformation[vesselName].vessel;
                if (vessel != null)
                {
                    if (partsCheck[vesselName] != vessel.parts.Count)
                    {
                        Debug.Log("DEBUG Parts Check: " + vesselName + " has lost " + (partsCheck[vesselName] - vessel.parts.Count) + " parts.");
                        partsCheck[vesselName] = vessel.parts.Count;
                    }
                }
                else if (partsCheck[vesselName] > 0)
                {
                    Debug.Log("DEBUG Parts Check: " + vesselName + " has been destroyed.");
                    partsCheck[vesselName] = 0;
                }
            }
        }

        // Main calling function to control ramming logging.
        private void LogRamming()
        {
            if (!competitionIsActive) return;
            if (rammingInformation == null) InitialiseRammingInformation();
            UpdateTimesToCPAs();
            CheckForPotentialCollisions();
            CheckForDamagedParts();
            CheckForMissingParts(); // DEBUG
        }
    }
}
