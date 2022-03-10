using System;
using System.Linq;
using System.Collections.Generic;

namespace BDArmory.Competition.VesselSpawning
{
    /// <summary>
    /// Configuration for spawning groups of vessels.
    /// 
    /// Note:
    /// This is currently partially specific to SpawnAllVesselsOnce and SpawnVesselsContinuosly.
    /// Once the spawn strategies take over that functionality, those components may be dropped from here.
    /// </summary>
    [Serializable]
    public class SpawnConfig
    {
        public SpawnConfig(int worldIndex, double latitude, double longitude, double altitude, float distance, bool absDistanceOrFactor, float easeInSpeed = 1f, bool killEverythingFirst = true, bool assignTeams = true, int numberOfTeams = 0, List<int> teamCounts = null, List<List<string>> teamsSpecific = null, string folder = "", List<string> craftFiles = null)
        {
            this.worldIndex = worldIndex;
            this.latitude = latitude;
            this.longitude = longitude;
            this.altitude = altitude;
            this.distance = distance;
            this.absDistanceOrFactor = absDistanceOrFactor;
            this.easeInSpeed = easeInSpeed;
            this.killEverythingFirst = killEverythingFirst;
            this.assignTeams = assignTeams;
            this.numberOfTeams = numberOfTeams;
            this.teamCounts = teamCounts; if (teamCounts != null) this.numberOfTeams = this.teamCounts.Count;
            this.teamsSpecific = teamsSpecific;
            this.folder = folder ?? "";
            this.craftFiles = craftFiles;
        }
        public SpawnConfig(SpawnConfig other)
        {
            this.worldIndex = other.worldIndex;
            this.latitude = other.latitude;
            this.longitude = other.longitude;
            this.altitude = other.altitude;
            this.distance = other.distance;
            this.absDistanceOrFactor = other.absDistanceOrFactor;
            this.easeInSpeed = other.easeInSpeed;
            this.killEverythingFirst = other.killEverythingFirst;
            this.assignTeams = other.assignTeams;
            this.numberOfTeams = other.numberOfTeams;
            this.teamCounts = other.teamCounts;
            this.teamsSpecific = other.teamsSpecific;
            this.folder = other.folder;
            this.craftFiles = other.craftFiles?.ToList();
        }
        public int worldIndex;
        public double latitude;
        public double longitude;
        public double altitude;
        public float distance;
        public bool absDistanceOrFactor; // If true, the distance value is used as-is, otherwise it is used as a factor giving the actual distance: (N+1)*distance, where N is the number of vessels.
        public float easeInSpeed;
        public bool killEverythingFirst = true;
        public bool assignTeams = true;
        public int numberOfTeams = 0; // Number of teams (or FFA, Folders or Inf). For evenly (as possible) splitting vessels into teams.
        public List<int> teamCounts; // List of team numbers. For unevenly splitting vessels into teams based on their order in the tournament state file for the round. E.g., when spawning from folders.
        public List<List<string>> teamsSpecific; // Dictionary of vessels and teams. For splitting specific vessels into specific teams.
        public string folder = "";
        public List<string> craftFiles = null;
    }
}