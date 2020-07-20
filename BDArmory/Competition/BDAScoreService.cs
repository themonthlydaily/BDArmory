using System;
using System.Collections;
using System.Collections.Generic;
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

        private bool pendingSync = false;

//        protected CompetitionModel competition = null;

//        protected HeatModel activeHeat = null;


        private BDAScoreClient client;

        void Awake()
        {
            if (Instance)
            {
                Destroy(Instance);
            }

            Instance = this;
        }

        public void Configure(string vesselPath, string hash)
        {
            this.client = new BDAScoreClient(this, vesselPath, hash);
            StartCoroutine(SynchronizeWithService(hash));
        }

        public IEnumerator SynchronizeWithService(string hash)
        {
            if( pendingSync )
            {
                Debug.Log("[BDAScoreService] Sync in progress");
                yield break;
            }
            pendingSync = true;

            Debug.Log(string.Format("[BDAScoreService] Sync started {0}", hash));

            // first, get competition metadata
            yield return client.GetCompetition(hash);

            // next, get player metadata
            yield return client.GetPlayers(hash);

            switch (client.competition.status)
            {
                case 0:
                    // waiting for players; nothing to do
                    Debug.Log(string.Format("[BDAScoreService] Waiting for players {0}", hash));
                    break;
                case 1:
                    // heats generated; find next heat
                    yield return FindNextHeat(hash);
                    break;
                case 2:
                    Debug.Log(string.Format("[BDAScoreService] Competition status 2 {0}", hash));
                    break;
            }

            pendingSync = false;
            Debug.Log(string.Format("[BDAScoreService] Sync completed {0}", hash));
        }

        private IEnumerator FindNextHeat(string hash)
        {
            Debug.Log(string.Format("[BDAScoreService] Find next heat for {0}", hash));

            // fetch heat metadata
            yield return client.GetHeats(hash);

            // find an unstarted heat
            HeatModel model = client.heats.Values.FirstOrDefault(e => e.Available());
            if (model == null)
            {
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
            // fetch vessel metadata for heat
            yield return client.GetVessels(hash, model);

            // fetch craft files for vessels
            yield return client.GetCraftFiles(hash, model);

            // notify web service to start heat
            yield return client.StartHeat(hash, model);

            // execute heat
            yield return ExecuteHeat(hash, model);

            // report scores
            yield return SendScores(hash, model);

            // notify web service to stop heat
            yield return client.StopHeat(hash, model);

            yield return RetryFind(hash);
        }


        private IEnumerator ExecuteHeat(string hash, HeatModel model)
        {
            Debug.Log(string.Format("[BDAScoreService] Running heat {0}/{1} in 5sec", hash, model.order));
            yield return new WaitForSeconds(5.0f);

            // orchestrate the match

            // start competition to begin spawning
            // NOTE: runs in separate coroutine
            BDACompetitionMode.Instance.StartCompetitionMode(1000);

            // start timer coroutine for 1min
            var duration = Core.BDArmorySettings.COMPETITION_DURATION;
            yield return new WaitForSeconds(duration * 60.0f);

            // timer coroutine stops competition
            BDACompetitionMode.Instance.StopCompetition();

            // TODO: remove all spawned vehicles
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
            if( killsOnTarget.ContainsKey(playerName) )
            {
                result = killsOnTarget[playerName].Values.Sum();
            }
            return result;
        }

        private int ComputeTotalDeaths(string playerName)
        {
            int result = 0;
            if( deaths.ContainsKey(playerName) )
            {
                result = deaths[playerName];
            }
            return result;
        }

        public void TrackHit(string attacker, string target, string weaponName, double hitDistance)
        {
            if( hitsOnTarget.ContainsKey(attacker) )
            {
                Dictionary<string, int> hits = hitsOnTarget[attacker];
                if( hits.ContainsKey(target) )
                {
                    hits[target] += 1;
                    hitsOnTarget[attacker] = hits;
                }
            }
            else
            {
                Dictionary<string, int> newHits = new Dictionary<string, int>();
                newHits[target] = 1;
                hitsOnTarget[attacker] = newHits;
            }
            if (!longestHitDistance.ContainsKey(attacker) || hitDistance > longestHitDistance[attacker])
            {
                Debug.Log(string.Format("[BDACompetitionMode] Tracked hit for {0} with {1} at {2}", attacker, weaponName, hitDistance));
                longestHitWeapon[attacker] = weaponName;
                longestHitDistance[attacker] = hitDistance;
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
            if( deaths.ContainsKey(target) )
            {
                deaths[target] += 1;
            }
            else
            {
                deaths[target] = 1;
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
                    newKills[target] = 1;
                    killsOnTarget[attacker] = newKills;
                }
            }
        }

        public class JsonListHelper<T>
        {
            [Serializable]
            private class Wrapper<T>
            {
                public T[] items;
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
