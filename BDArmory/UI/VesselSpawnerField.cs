using System;
using System.Collections.Generic;
using System.Reflection;
using BDArmory.Core;
using BDArmory.UI;
using UniLinq;
using UnityEngine;

namespace BDArmory.UI
{
    public class SpawnLocation
    {
        public string name;
        public Vector2d location;

        public SpawnLocation(string _name, Vector2d _location) { name = _name; location = _location; }
        public override string ToString() { return name + ", " + location.ToString("G6"); }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class VesselSpawnerField : Attribute
    {
        public VesselSpawnerField()
        {
        }

        static List<SpawnLocation> defaultLocations = new List<SpawnLocation>{
            new SpawnLocation("KSC", new Vector2d(0.07, -74.67)),
            new SpawnLocation("Inland KSC", new Vector2d(20.657, -146.421)),
            new SpawnLocation("Ice field", new Vector2d(80.0821, -81.1997)),
            new SpawnLocation("Canyon", new Vector2d(-52.67, -4.67)),
            new SpawnLocation("Bowl 1", new Vector2d(35.6559, -77.4941)),
            new SpawnLocation("Bowl 2", new Vector2d(3.85, -78.0)),
            new SpawnLocation("Bowl 3", new Vector2d(0.25, -80.5)),
            new SpawnLocation("Bowl 4", new Vector2d(6.925, -170.705)),
            new SpawnLocation("Kurgan's spot", new Vector2d(-28.4595, -9.15156)),
            new SpawnLocation("Half-pipe", new Vector2d(-21.10, 72.70)),
        };

        public static void Save()
        {
            ConfigNode fileNode = ConfigNode.Load(VesselSpawner.spawnLocationsCfg);
            if (fileNode == null)
                fileNode = new ConfigNode();

            if (!fileNode.HasNode("BDASpawnLocations"))
                fileNode.AddNode("BDASpawnLocations");

            ConfigNode settings = fileNode.GetNode("BDASpawnLocations");

            settings.ClearValues();
            foreach (var spawnLocation in VesselSpawner.spawnLocations)
                settings.AddValue("LOCATION", spawnLocation.ToString());

            fileNode.Save(VesselSpawner.spawnLocationsCfg);
        }

        public static void Load()
        {
            VesselSpawner.spawnLocations = new List<SpawnLocation>();
            ConfigNode fileNode = ConfigNode.Load(VesselSpawner.spawnLocationsCfg);
            if (fileNode != null && fileNode.HasNode("BDASpawnLocations"))
            {
                ConfigNode settings = fileNode.GetNode("BDASpawnLocations");
                foreach (var spawnLocation in settings.GetValues("LOCATION"))
                {
                    var parsedValue = (SpawnLocation)ParseValue(typeof(SpawnLocation), spawnLocation);
                    if (parsedValue != null)
                    {
                        VesselSpawner.spawnLocations.Add(parsedValue);
                    }
                }
            }
            // Add defaults if nothing got loaded.
            if (VesselSpawner.spawnLocations.Count == 0)
            {
                Debug.Log("DEBUG No locations found, adding defaults.");
                VesselSpawner.spawnLocations = defaultLocations.ToList();
            }
        }

        public static object ParseValue(Type type, string value)
        {
            try
            {
                if (type == typeof(string))
                {
                    return value;
                }
                else if (type == typeof(Vector2d))
                {
                    char[] charsToTrim = { '(', ')', ' ' };
                    string[] strings = value.Trim(charsToTrim).Split(',');
                    if (strings.Length == 2)
                    {
                        double x = double.Parse(strings[0]);
                        double y = double.Parse(strings[1]);
                        return new Vector2d(x, y);
                    }
                }
                else if (type == typeof(SpawnLocation))
                {
                    var parts = value.Split(new char[] { ',' }, 2);
                    if (parts.Length == 2)
                    {
                        var name = (string)ParseValue(typeof(string), parts[0]);
                        var location = (Vector2d)ParseValue(typeof(Vector2d), parts[1]);
                        if (name != null && location != null)
                            return new SpawnLocation(name, location);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            Debug.LogError("[VesselSpawnerField]: Failed to parse settings field of type " + type + " and value " + value);
            return null;
        }
    }
}
