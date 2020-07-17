using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BDArmory.Competition
{

    [Serializable]
    public class CompetitionModel
    {
        public int id;
        public string name;
        public int status;
        public int stage;
        public int remaining_heats;
        public string started_at;
        public string ended_at;
        public string created_at;
        public string updated_at;

        public override string ToString() { return "{id: " + id + ", name: " + name + ", status: " + status + ", stage: " + stage + ", started_at: " + started_at + ", ended_at: " + ended_at + "}"; }
    }

    [Serializable]
    public class CompetitionResponse
    {
        public CompetitionModel competition;
    }

    [Serializable]
    public class PlayerCollection
    {
        public PlayerModel[] players;
    }

    [Serializable]
    public class PlayerModel
    {
        public int id;
        public string name;
        public string created_at;
        public string updated_at;

        public override string ToString() { return "{id: "+id+", name: "+name+"}"; }
    }

    [Serializable]
    public class HeatCollection
    {
        public HeatModel[] heats;
    }

    [Serializable]
    public class HeatModel
    {
        public int id;
        public int competition_id;
        public int order;
        public int stage;
        public string started_at;
        public string ended_at;
        public string created_at;
        public string updated_at;

        public bool Started() { return started_at != null && ended_at == null; }
        public bool Available() { return started_at == null && ended_at == null; }
        public override string ToString() { return "{id: " + id + ", competition_id: " + competition_id + ", order: " + order + ", started_at: " + started_at + ", ended_at: " + ended_at + "}"; }
    }

    [Serializable]
    public class VesselCollection
    {
        public VesselModel[] vessels;
    }

    [Serializable]
    public class VesselModel
    {
        public int id;
        public int player_id;
        public int competition_id;
        public string craft_url;
        public string created_at;
        public string updated_at;

        public override string ToString() { return "{id: " + id + ", competition_id: " + competition_id + ", player_id: " + player_id + ", craft_url: " + craft_url + "}"; }
    }

    [Serializable]
    public class RecordModel
    {
        public int competition_id;
        public int vessel_id;
        public int heat_id;
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
