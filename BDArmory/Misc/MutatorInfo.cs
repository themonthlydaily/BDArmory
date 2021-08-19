﻿using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace BDArmory.Misc
{
	public class MutatorInfo
	{
        public string name { get; private set; }
        public bool weaponMod { get; private set; }
        public string weaponType { get; private set; }
        public string bulletType { get; private set; }
        public int RoF { get; private set; }
        public float MaxDeviation { get; private set; }
        public float laserDamage { get; private set; }
        public bool Vampirism { get; private set; }
        public float Regen { get; private set; }
        public bool resourceSteal { get; private set; }
        public string resourceTax { get; private set; }
        public float resourceTaxRate { get; private set; }
        public float engineMult { get; private set; }
        public string iconPath { get; private set; }
        public string iconColor{ get; private set; }

        public static MutatorInfos mutators;
        public static HashSet<string> mutatorNames;
        public static MutatorInfo defaultMutator;

        public MutatorInfo(string name, bool weaponMod, string weaponType, string bulletType, int RoF, float MaxDeviation, float laserDamage,
            bool Vampirism, float Regen, bool resourceSteal, string resourceTax, float resourceTaxRate, float engineMult, string iconPath, string iconColor)
        {
            this.name = name;
            this.weaponMod = weaponMod;
            this.weaponType = weaponType;
            this.bulletType = bulletType;
            this.RoF = RoF;
            this.MaxDeviation = MaxDeviation;
            this.laserDamage = laserDamage;
            this.Vampirism = Vampirism;
            this.Regen = Regen;
            this.resourceSteal = resourceSteal;
            this.resourceTax = resourceTax;
            this.resourceTaxRate = resourceTaxRate;
            this.engineMult = engineMult;
            this.iconPath = iconPath;
            this.iconColor = iconColor;
        }

        public static void Load()
        {
            if (mutators != null) return; // Only load them once on startup.
            mutators = new MutatorInfos();
            if (mutatorNames == null) mutatorNames = new HashSet<string>();
            UrlDir.UrlConfig[] nodes = GameDatabase.Instance.GetConfigs("MUTATOR");
            ConfigNode node;

            // First locate BDA's default rocket definition so we can fill in missing fields.
            if (defaultMutator == null)
                for (int i = 0; i < nodes.Length; ++i)
                {
                    if (nodes[i].parent.name != "BD_Mutators") continue; // Ignore other config files.
                    node = nodes[i].config;
                    if (!node.HasValue("name") || (string)ParseField(nodes[i].config, "name", typeof(string)) != "def") continue; // Ignore other configs.
                    Debug.Log("[BDArmory.MutatorInfo]: Parsing default mutator definition from " + nodes[i].parent.name);
                    defaultMutator = new MutatorInfo(
                        "def",
                        (bool)ParseField(node, "weaponMod", typeof(bool)),
                        (string)ParseField(node, "weaponType", typeof(string)),
                        (string)ParseField(node, "bulletType", typeof(string)),
                        (int)ParseField(node, "RoF", typeof(int)),
                        (float)ParseField(node, "MaxDeviation", typeof(float)),
                        (float)ParseField(node, "laserDamage", typeof(float)),
                        (bool)ParseField(node, "Vampirism", typeof(bool)),
                        (float)ParseField(node, "Regen", typeof(float)),
                        (bool)ParseField(node, "resourceSteal", typeof(bool)),
                        (string)ParseField(node, "resourceTax", typeof(string)),
                        (float)ParseField(node, "resourceTaxRate", typeof(float)),
                        (float)ParseField(node, "engineMult", typeof(float)),
                        (string)ParseField(node, "iconPath", typeof(string)),
                        (string)ParseField(node, "iconColor", typeof(string))
                    );
                    mutators.Add(defaultMutator);
                    mutatorNames.Add("def");
                    break;
                }
            if (defaultMutator == null) throw new ArgumentException("Failed to find BDArmory's default mutator definition.", "defaultMutator");

            // Now add in the rest of the rockets.
            for (int i = 0; i < nodes.Length; i++)
            {
                string name_ = "";
                try
                {
                    node = nodes[i].config;
                    name_ = (string)ParseField(node, "name", typeof(string));
                    if (mutatorNames.Contains(name_)) // Avoid duplicates.
                    {
                        if (nodes[i].parent.name != "BD_Mutators" || name_ != "def") // Don't report the default bullet definition as a duplicate.
                            Debug.LogError("[BDArmory.MutatorInfo]: Mutator definition " + name_ + " from " + nodes[i].parent.name + " already exists, skipping.");
                        continue;
                    }
                    Debug.Log("[BDArmory.MutatorInfo]: Parsing definition of mutator " + name_ + " from " + nodes[i].parent.name);
                    mutators.Add(
                        new MutatorInfo(
                            name_,
                        (bool)ParseField(node, "weaponMod", typeof(bool)),
                        (string)ParseField(node, "weaponType", typeof(string)),
                        (string)ParseField(node, "bulletType", typeof(string)),
                        (int)ParseField(node, "RoF", typeof(int)),
                        (float)ParseField(node, "MaxDeviation", typeof(float)),
                        (float)ParseField(node, "laserDamage", typeof(float)),
                        (bool)ParseField(node, "Vampirism", typeof(bool)),
                        (float)ParseField(node, "Regen", typeof(float)),
                        (bool)ParseField(node, "resourceSteal", typeof(bool)),
                        (string)ParseField(node, "resourceTax", typeof(string)),
                        (float)ParseField(node, "resourceTaxRate", typeof(float)),
                        (float)ParseField(node, "engineMult", typeof(float)),
                        (string)ParseField(node, "iconPath", typeof(string)),
                        (string)ParseField(node, "iconColor", typeof(string))
                        )
                    );
                    mutatorNames.Add(name_);
                }
                catch (Exception e)
                {
                    Debug.LogError("[BDArmory.MutatorInfo]: Error Loading Mutator Config '" + name_ + "' | " + e.ToString());
                }
            }
        }

        private static object ParseField(ConfigNode node, string field, Type type)
        {
            try
            {
                if (!node.HasValue(field))
                    throw new ArgumentNullException(field, "Field '" + field + "' is missing.");
                var value = node.GetValue(field);
                try
                {
                    if (type == typeof(string))
                    { return value; }
                    else if (type == typeof(bool))
                    { return bool.Parse(value); }
                    else if (type == typeof(int))
                    { return int.Parse(value); }
                    else if (type == typeof(float))
                    { return float.Parse(value); }
                    else
                    { throw new ArgumentException("Invalid type specified."); }
                }
                catch (Exception e)
                { throw new ArgumentException("Field '" + field + "': '" + value + "' could not be parsed as '" + type.ToString() + "' | " + e.ToString(), field); }
            }
            catch (Exception e)
            {
                if (defaultMutator != null)
                {
                    // Give a warning about the missing or invalid value, then use the default value using reflection to find the field.
                    var defaultValue = typeof(MutatorInfo).GetProperty(field, BindingFlags.Public | BindingFlags.Instance).GetValue(defaultMutator);
                    Debug.LogError("[BDArmory.RocketInfo]: Using default value of " + defaultValue.ToString() + " for " + field + " | " + e.ToString());
                    return defaultValue;
                }
                else
                    throw;
            }
        }
    }

    public class MutatorInfos : List<MutatorInfo>
    {
        public MutatorInfo this[string name]
        {
            get { return Find((value) => { return value.name == name; }); }
        }
    }
}
