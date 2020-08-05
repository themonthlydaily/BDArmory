using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using BDArmory.Control;

namespace BDArmory.Competition
{

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class BDAScoreService : MonoBehaviour
    {
        public static BDAScoreService Instance;

        public Dictionary<string, Dictionary<string, int>> hitsOnTarget = new Dictionary<string, Dictionary<string, int>>();
        public Dictionary<string, Dictionary<string, int>> killsOnTarget = new Dictionary<string, Dictionary<string, int>>();
        public Dictionary<string, int> deaths = new Dictionary<string, int>();
        public Dictionary<string, string> longestHitWeapon = new Dictionary<string, string>();
        public Dictionary<string, double> longestHitDistance = new Dictionary<string, double>();

        public enum StatusType
        {
            [Description("Offline")]
            Offline,
            [Description("Fetching Competition")]
            FetchingCompetition,
            [Description("Fetching Players")]
            FetchingPlayers,
            [Description("Waiting for Players")]
            PendingPlayers,
            [Description("Selecting a Heat")]
            FindingNextHeat,
            [Description("Fetching Heat")]
            FetchingHeat,
            [Description("Fetching Vessels")]
            FetchingVessels,
            [Description("Downloading Craft Files")]
            DownloadingCraftFiles,
            [Description("Starting Heat")]
            StartingHeat,
            [Description("Spawning Vessels")]
            SpawningVessels,
            [Description("Running Heat")]
            RunningHeat,
            [Description("Removing Vessels")]
            RemovingVessels,
            [Description("Stopping Heat")]
            StoppingHeat,
            [Description("Reporting Results")]
            ReportingResults,
            [Description("No Pending Heats")]
            StalledNoPendingHeats,
            [Description("Completed")]
            Completed,
            [Description("Invalid")]
            Invalid
        }

        private bool pendingSync = false;
        private StatusType status = StatusType.Offline;

        private Coroutine syncCoroutine;

        //        protected CompetitionModel competition = null;

        //        protected HeatModel activeHeat = null;


        public BDAScoreClient client;

        void Awake()
        {
            if (Instance)
            {
                Destroy(Instance);
            }

            Instance = this;
        }

        void Update()
        {
            if( pendingSync && !Core.BDArmorySettings.REMOTE_LOGGING_ENABLED )
            {
                Debug.Log("[BDAScoreService] Cancel due to disable");
                pendingSync = false;
                StopCoroutine(syncCoroutine);
                return;
            }
        }

        public void Configure(string vesselPath, string hash)
        {
            this.client = new BDAScoreClient(this, vesselPath, hash);
            syncCoroutine = StartCoroutine(SynchronizeWithService(hash));
        }

        public IEnumerator SynchronizeWithService(string hash)
        {
            if (pendingSync)
            {
                Debug.Log("[BDAScoreService] Sync in progress");
                yield break;
            }
            pendingSync = true;

            Debug.Log(string.Format("[BDAScoreService] Sync started {0}", hash));

            status = StatusType.FetchingCompetition;
            // first, get competition metadata
            yield return client.GetCompetition(hash);

            status = StatusType.FetchingPlayers;
            // next, get player metadata
            yield return client.GetPlayers(hash);

            // abort if we didn't receive a valid competition
            if (client.competition == null)
            {
                status = StatusType.Invalid;
                pendingSync = false;
                syncCoroutine = null;
                yield break;
            }

            switch (client.competition.status)
            {
                case 0:
                    status = StatusType.PendingPlayers;
                    // waiting for players; nothing to do
                    Debug.Log(string.Format("[BDAScoreService] Waiting for players {0}", hash));
                    break;
                case 1:
                    status = StatusType.FindingNextHeat;
                    // heats generated; find next heat
                    yield return FindNextHeat(hash);
                    break;
                case 2:
                    status = StatusType.StalledNoPendingHeats;
                    Debug.Log(string.Format("[BDAScoreService] Competition status 2 {0}", hash));
                    break;
            }

            pendingSync = false;
            Debug.Log(string.Format("[BDAScoreService] Sync completed {0}", hash));
        }

        private IEnumerator FindNextHeat(string hash)
        {
            Debug.Log(string.Format("[BDAScoreService] Find next heat for {0}", hash));

            status = StatusType.FetchingHeat;
            // fetch heat metadata
            yield return client.GetHeats(hash);

            // find an unstarted heat
            HeatModel model = client.heats.Values.FirstOrDefault(e => e.Available());
            if (model == null)
            {
                status = StatusType.StalledNoPendingHeats;
                Debug.Log(string.Format("[BDAScoreService] No inactive heat found {0}", hash));
                yield return RetryFind(hash);
            }
            else
            {
                Debug.Log(string.Format("[BDAScoreService] Found heat {1} in {0}", hash, model.order));
                yield return FetchAndExecuteHeat(hash, model);
            }
        }

        private IEnumerator RetryFind(string hash)
        {
            yield return new WaitForSeconds(30);
            yield return FindNextHeat(hash);
        }

        private IEnumerator FetchAndExecuteHeat(string hash, HeatModel model)
        {
            status = StatusType.FetchingVessels;
            // fetch vessel metadata for heat
            yield return client.GetVessels(hash, model);

            status = StatusType.DownloadingCraftFiles;
            // fetch craft files for vessels
            yield return client.GetCraftFiles(hash, model);

            status = StatusType.StartingHeat;
            // notify web service to start heat
            yield return client.StartHeat(hash, model);

            // execute heat
            yield return ExecuteHeat(hash, model);

            status = StatusType.ReportingResults;
            // report scores
            yield return SendScores(hash, model);

            status = StatusType.StoppingHeat;
            // notify web service to stop heat
            yield return client.StopHeat(hash, model);

            status = StatusType.FindingNextHeat;
            yield return RetryFind(hash);
        }

        private IEnumerator ExecuteHeat(string hash, HeatModel model)
        {
            Debug.Log(string.Format("[BDAScoreService] Running heat {0}/{1}", hash, model.order));
            UI.VesselSpawner spawner = UI.VesselSpawner.Instance;

            // orchestrate the match

            status = StatusType.SpawningVessels;
            // spawn vessels around CompetitionHub
            Vessel hub = FlightGlobals.Vessels.First(e => e.vesselName.Equals("CompetitionHub"));
            float spawnAltitude = (float)hub.altitude + 1500.0f;
            spawner.SpawnAllVesselsOnce(new Vector2d(hub.latitude, hub.longitude), spawnAltitude, false, hash); // FIXME Use a subfolder of AutoSpawn to add and remove vessels to avoid messing with anyone's current setup. GetCraftFiles needs to be adjusted to use this folder too.
            while(spawner.vesselsSpawning)
                yield return new WaitForFixedUpdate();
            if (!spawner.vesselSpawnSuccess)
            {
                Debug.Log("[BDAScoreService] Vessel spawning failed."); // FIXME Now what?
                yield break;
            }

            status = StatusType.RunningHeat;
            // NOTE: runs in separate coroutine
            BDACompetitionMode.Instance.StartCompetitionMode(1000);

            // start timer coroutine for the duration specified in settings UI
            var duration = Core.BDArmorySettings.COMPETITION_DURATION;
            yield return new WaitForSeconds(duration * 60.0f);

            // stop competition
            BDACompetitionMode.Instance.StopCompetition();

            status = StatusType.RemovingVessels;
            // remove all spawned vehicles
            foreach (Vessel v in FlightGlobals.Vessels.Where(e => !e.vesselName.Equals("CompetitionHub")))
            {
                v.Die();
            }
        }

        private IEnumerator SendScores(string hash, HeatModel heat)
        {
            var records = BuildRecords(hash, heat);
            yield return client.PostRecords(hash, heat.order, records.ToList());
        }

        private List<RecordModel> BuildRecords(string hash, HeatModel heat)
        {
            List<RecordModel> results = new List<RecordModel>();
            var playerNames = killsOnTarget.Keys;
            foreach (string playerName in playerNames)
            {
                PlayerModel player = client.players.Values.FirstOrDefault(e => e.name == playerName);
                if (player == null)
                {
                    Debug.Log(string.Format("[BDAScoreService] Unmatched player {0}", playerName));
                    continue;
                }
                VesselModel vessel = client.vessels.Values.FirstOrDefault(e => e.player_id == player.id);
                if (vessel == null)
                {
                    Debug.Log(string.Format("[BDAScoreService] Unmatched vessel for playerId {0}", player.id));
                    continue;
                }
                RecordModel record = new RecordModel();
                record.vessel_id = vessel.id;
                record.competition_id = int.Parse(hash);
                record.heat_id = heat.id;
                record.hits = ComputeTotalHits(player.name);
                record.kills = ComputeTotalKills(player.name);
                record.deaths = ComputeTotalDeaths(player.name);
                if (longestHitDistance.ContainsKey(player.name))
                {
                    record.distance = (float)longestHitDistance[player.name];
                    record.weapon = longestHitWeapon[player.name];
                }
            }
            return results;
        }

        private int ComputeTotalHits(string playerName)
        {
            int result = 0;
            if (hitsOnTarget.ContainsKey(playerName))
            {
                result = hitsOnTarget[playerName].Values.Sum();
            }
            return result;
        }

        private int ComputeTotalKills(string playerName)
        {
            int result = 0;
            if (killsOnTarget.ContainsKey(playerName))
            {
                result = killsOnTarget[playerName].Values.Sum();
            }
            return result;
        }

        private int ComputeTotalDeaths(string playerName)
        {
            int result = 0;
            if (deaths.ContainsKey(playerName))
            {
                result = deaths[playerName];
            }
            return result;
        }

        public void TrackHit(string attacker, string target, string weaponName, double hitDistance)
        {
            if (hitsOnTarget.ContainsKey(attacker))
            {
                Dictionary<string, int> hits = hitsOnTarget[attacker];
                if (hits.ContainsKey(target))
                {
                    hits[target] += 1;
                    hitsOnTarget[attacker] = hits;
                }
                else
                {
                    hits.Add(target, 1);
                    hitsOnTarget.Add(attacker, hits);
                }
            }
            else
            {
                Dictionary<string, int> newHits = new Dictionary<string, int>();
                newHits.Add(target, 1);
                hitsOnTarget.Add(attacker, newHits);
            }
            if (!longestHitDistance.ContainsKey(attacker) || hitDistance > longestHitDistance[attacker])
            {
                Debug.Log(string.Format("[BDACompetitionMode] Tracked hit for {0} with {1} at {2}", attacker, weaponName, hitDistance));
                if (longestHitDistance.ContainsKey(attacker))
                {
                    longestHitWeapon[attacker] = weaponName;
                    longestHitDistance[attacker] = hitDistance;
                }
                else
                {
                    longestHitWeapon.Add(attacker, weaponName);
                    longestHitDistance.Add(attacker, hitDistance);
                }
            }
        }

        public void TrackKill(string attacker, string target)
        {
            List<string> list = new List<string>();
            list.Add(attacker);
            TrackKill(list, target);
        }

        public void TrackKill(List<string> attackers, string target)
        {
            if (deaths.ContainsKey(target))
            {
                deaths[target] += 1;
            }
            else
            {
                deaths.Add(target, 1);
            }
            foreach (string attacker in attackers)
            {
                if (killsOnTarget.ContainsKey(attacker))
                {
                    Dictionary<string, int> attackerKills = killsOnTarget[attacker];
                    attackerKills[target] += 1;
                    killsOnTarget[attacker] = attackerKills;
                }
                else
                {
                    Dictionary<string, int> newKills = new Dictionary<string, int>();
                    newKills.Add(target, 1);
                    killsOnTarget.Add(attacker, newKills);
                }
            }
        }

        public string Status()
        {
            return status.ToString();
        }

        public class JsonListHelper<T>
        {
            [Serializable]
            private class Wrapper<S>
            {
                public S[] items;
            }
            public List<T> FromJSON(string json)
            {
                if (json == null)
                {
                    return new List<T>();
                }
                //string wrappedJson = string.Format("{{\"items\":{0}}}", json);
                Wrapper<T> wrapper = new Wrapper<T>();
                wrapper.items = JsonUtility.FromJson<T[]>(json);
                if (wrapper == null || wrapper.items == null)
                {
                    Debug.Log(string.Format("[BDAScoreService] Failed to decode {0}", json));
                    return new List<T>();
                }
                else
                {
                    return new List<T>(wrapper.items);
                }
            }
        }
    }


}
