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

//        private string competitionHash = "";

//        private int heat = 0;

//        private JsonListHelper listHelper = new JsonListHelper();

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
            BDACompetitionMode.Instance.StartCompetitionMode(1000, true);

            // start timer coroutine for 5mins
            yield return new WaitForSecondsRealtime(5 * 60.0f);

            // timer coroutine stops competition
            BDACompetitionMode.Instance.StopCompetition();

            // TODO: remove all spawned vehicles
        }



        /*public void SubmitResults()
        {
            if (!BDArmorySettings.REMOTE_LOGGING_ENABLED)
            {
                return;
            }
            Dictionary<string, ScoringData> scores = BDACompetitionMode.Instance.Scores;
            int competitionId = BDACompetitionMode.Instance.CompetitionID;
            var records = scores.Select(e => TranslateScoreData(competitionId, e.Key, e.Value));
            StartCoroutine(SendRecords(competitionId.ToString(), heat, records.ToList()));
        }*/

        private RecordModel TranslateScoreData(string playerName, HeatModel heat, ScoringData scoreData)
        {
            PlayerModel player = client.players.Values.First(e => e.name.Equals(playerName));
            VesselModel vessel = client.vessels.Values.First(e => e.player_id == player.id);
            RecordModel record = new RecordModel();
            record.competition_id = vessel.competition_id;
            record.vessel_id = vessel.id;
            record.heat_id = heat.id;
            record.kills = scoreData.kills;
            record.deaths = scoreData.deaths;
            if (BDACompetitionMode.Instance.longestHitDistance.ContainsKey(player.name))
            {
                record.distance = (float)BDACompetitionMode.Instance.longestHitDistance[player.name];
                record.weapon = BDACompetitionMode.Instance.longestHitWeapon[player.name];
            }
            return record;
        }

        private IEnumerator SendScores(string hash, HeatModel heat)
        {
            Dictionary<string, ScoringData> scores = BDACompetitionMode.Instance.Scores;
            var records = scores.Select(e => TranslateScoreData(e.Key, heat, e.Value));
            yield return client.PostRecords(hash, heat.order, records.ToList());
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
