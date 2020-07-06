using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BDArmory.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace BDArmory.Control
{

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class BDAScoreService : MonoBehaviour
    {
        public static BDAScoreService Instance;

        void Awake()
        {
            if (Instance)
            {
                Destroy(Instance);
            }

            Instance = this;
        }

        public void ResetScores()
        {
            // TODO: move score tracking code into this class
        }

        public void SubmitResults()
        {
            if( !BDArmorySettings.REMOTE_LOGGING_ENABLED )
            {
                return;
            }
            Dictionary<string, ScoringData> scores = BDACompetitionMode.Instance.Scores;
            int competitionId = BDACompetitionMode.Instance.CompetitionID;
            var records = scores.Select(e => TranslateScoreData(competitionId, e.Key, e.Value));
            StartCoroutine(SendRecords(records.ToList()));
        }

        private ScoreRecord TranslateScoreData(int competitionId, string playerName, ScoringData scoreData)
        {
            ScoreRecord record = new ScoreRecord();
            record.competition_id = competitionId;
            record.player = playerName;
            record.kills = scoreData.kills;
            record.deaths = scoreData.deaths;
            if( BDACompetitionMode.Instance.longestHitDistance.ContainsKey(playerName) )
            {
                record.distance = (float)BDACompetitionMode.Instance.longestHitDistance[playerName];
                record.weapon = BDACompetitionMode.Instance.longestHitWeapon[playerName];
            }
            return record;
        }

        private IEnumerator SendRecords(List<ScoreRecord> records)
        {
            IEnumerable<string> recordsJson = records.Select(e => JsonUtility.ToJson(e));
            string recordsJsonStr = string.Join(",", recordsJson);
            string requestBody = string.Format("{{\"records\":[{0}]}}", recordsJsonStr);

            byte[] rawBody = Encoding.UTF8.GetBytes(requestBody);
            string uri = "https://bdascores.herokuapp.com/records/batch.json";
            using (UnityWebRequest webRequest = new UnityWebRequest(uri))
            {
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.uploadHandler = new UploadHandlerRaw(rawBody);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.method = UnityWebRequest.kHttpVerbPOST;
                Debug.Log(string.Format("[BDAScoreService] sending {0}", requestBody));
                
                yield return webRequest.SendWebRequest();
            }
        }

    }

    [Serializable]
    class ScoreRecord
    {
        public int competition_id;
        public string player;
        public int hits;
        public int kills;
        public int deaths;
        public float distance;
        public string weapon;

        public string ToJSON()
        {
            return JsonUtility.ToJson(this);
        }
    }

}
