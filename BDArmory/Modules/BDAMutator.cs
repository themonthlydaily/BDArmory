using BDArmory.Core;
using BDArmory.Control;
using BDArmory.Misc;
using System.Collections.Generic;
using UnityEngine;
using BDArmory.Core.Extension;
using BDArmory.Core.Module;
using BDArmory.UI;

namespace BDArmory.Modules
{
	class BDAMutator : PartModule
	{
        float startTime;
        bool mutatorEnabled = false;
        public List<string> mutators;

        private MutatorInfo mutatorInfo;
        private float Vampirism = 0;
        private float Regen = 0;
        private float Strength = 1;
        private float Defense = 1;
        private bool Vengeance = false;
        private List<string> ResourceTax;
        private double TaxRate = 0;

        private int oldScore = 0;
        private float Accumulator;
        public override void OnStart(StartState state)
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                part.force_activate();
            }
            base.OnStart(state);
        }

        public void EnableMutator(string name = "def")
        {
            if (mutatorEnabled) //replace current mutator with new one
            {
                DisableMutator();
            }
            if (name == "def") //mutator not specified, randomly choose from selected mutators
            {
                mutators = BDAcTools.ParseNames(BDArmorySettings.MUTATOR_LIST);
                int i = UnityEngine.Random.Range(0, mutators.Count);
                name = MutatorInfo.mutators[mutators[i]].name;
            }
            mutatorInfo = MutatorInfo.mutators[name];
            if (mutatorInfo.weaponMod)
            {
                using (var weapon = VesselModuleRegistry.GetModules<ModuleWeapon>(vessel).GetEnumerator())
                    while (weapon.MoveNext())
                    {
                        if (weapon.Current == null) continue;
                        weapon.Current.ParseWeaponType(mutatorInfo.weaponType);
                        weapon.Current.bulletType = mutatorInfo.bulletType;
                        weapon.Current.useCustomBelt = false;
                        weapon.Current.roundsPerMinute = mutatorInfo.RoF;
                        weapon.Current.maxDeviation = mutatorInfo.MaxDeviation;
                        weapon.Current.laserDamage = mutatorInfo.laserDamage;
                        weapon.Current.pulseLaser = true;
                        weapon.Current.strengthMutator = Mathf.Clamp(Strength, 0.01f, 9999);
                        weapon.Current.SetupBulletPool();
                        weapon.Current.ParseAmmoStats();
                        if (weapon.Current.eWeaponType == ModuleWeapon.WeaponTypes.Laser)
                        {
                            weapon.Current.SetupLaserSpecifics();
                        }
                        weapon.Current.resourceSteal = mutatorInfo.resourceSteal;
                    }
            }
            using (var engine = VesselModuleRegistry.GetModuleEngines(vessel).GetEnumerator())
                while (engine.MoveNext())
                {
                    engine.Current.thrustPercentage *= mutatorInfo.engineMult;
                }
            Vampirism = mutatorInfo.Vampirism;
            Regen = mutatorInfo.Regen;
            Strength = mutatorInfo.Strength;
            Defense = mutatorInfo.Defense;
            if (Defense != 0)
            {
                using (List<Part>.Enumerator part = vessel.Parts.GetEnumerator())
                    while (part.MoveNext())
                    {
                        var HPT = part.Current.FindModuleImplementing<HitpointTracker>();
                        HPT.defenseMutator = Mathf.Clamp(Defense, 0.01f, 9999);
                    }
            }
            Vengeance = mutatorInfo.Vengeance;
            if (Vengeance)
            {
                var nuke = vessel.rootPart.FindModuleImplementing<RWPS3R2NukeModule>();
                if (nuke == null)
                {
                    nuke = (RWPS3R2NukeModule)vessel.rootPart.AddModule("RWPS3R2NukeModule");
                    nuke.reportingName = "Vengeance";
                }
            }
            using (List<Part>.Enumerator part = vessel.Parts.GetEnumerator())
                while (part.MoveNext())
                {
                    var MM = part.Current.FindModuleImplementing<ModuleMassAdjust>();
                    if (MM == null)
                    {
                        MM = (ModuleMassAdjust)vessel.rootPart.AddModule("ModuleMassAdjust");
                        if (BDArmorySettings.MUTATOR_DURATION > 0)
                        {
                            MM.duration = BDArmorySettings.MUTATOR_DURATION; //MMA will time out and remove itself when mutator expires
                        }
                        else
                        {
                            MM.duration = BDArmorySettings.COMPETITION_DURATION;
                        }
                        MM.massMod = mutatorInfo.MassMod / vessel.Parts.Count; //evenly distribute mass change across entire vessel
                    }
                }
            ResourceTax = BDAcTools.ParseNames(mutatorInfo.resourceTax);
            TaxRate = mutatorInfo.resourceTaxRate;

            startTime = Time.time;
            mutatorEnabled = true; 
        }

        public void DisableMutator()
        {
            if (mutatorInfo.weaponMod)
            {
                using (var weapon = VesselModuleRegistry.GetModules<ModuleWeapon>(vessel).GetEnumerator())
                    while (weapon.MoveNext())
                    {
                        if (weapon.Current == null) continue;
                        weapon.Current.ParseWeaponType(weapon.Current.weaponType);
                        weapon.Current.bulletType = weapon.Current.bulletType;
                        if (!string.IsNullOrEmpty(weapon.Current.ammoBelt) && weapon.Current.ammoBelt != "def")
                        {
                            weapon.Current.useCustomBelt = true;
                        }
                        weapon.Current.roundsPerMinute = weapon.Current.baseRPM;
                        weapon.Current.maxDeviation = weapon.Current.baseDeviation;
                        weapon.Current.laserDamage = weapon.Current.baseLaserdamage;
                        weapon.Current.pulseLaser = weapon.Current.pulseInConfig;
                        weapon.Current.strengthMutator = 1;
                        weapon.Current.SetupBulletPool();
                        weapon.Current.ParseAmmoStats();
                        weapon.Current.resourceSteal = false; ;
                    }
            }
            using (var engine = VesselModuleRegistry.GetModuleEngines(vessel).GetEnumerator())
                while (engine.MoveNext())
                {
                    engine.Current.thrustPercentage /= mutatorInfo.engineMult;
                }
            Vampirism = 0;
            Regen = 0;
            Strength = 0;
            Defense = 0;

            using (List<Part>.Enumerator part = vessel.Parts.GetEnumerator())
                while (part.MoveNext())
                {
                    var HPT = part.Current.FindModuleImplementing<HitpointTracker>();
                    HPT.defenseMutator = Defense;
                }

            Vengeance = false;
            if (Vengeance)
            {
                var nuke = vessel.rootPart.FindModuleImplementing<RWPS3R2NukeModule>();
                if (nuke != null)
                {
                    vessel.rootPart.RemoveModule(nuke);
                }
            }
            ResourceTax.Clear();
            TaxRate = 0;
            mutatorEnabled = false;
        }

        public void Update()
        {
            if (HighLogic.LoadedSceneIsFlight && !BDArmorySetup.GameIsPaused && !vessel.packed)
            {
                if (!mutatorEnabled) return;
                if (Time.time - startTime > BDArmorySettings.MUTATOR_DURATION)
                {
                    DisableMutator();
                }
                if (Regen != 0 || Vampirism != 0)
                {
                    {
                        using (List<Part>.Enumerator part = vessel.Parts.GetEnumerator())
                            while (part.MoveNext())
                            {
                                if (Regen != 0 && Accumulator > 5) //Add regen HP every 5 seconds
                                {
                                    part.Current.AddHealth(Regen);
                                }
                                if (Vampirism != 0)
                                {
                                    if (BDACompetitionMode.Instance.Scores.ScoreData.ContainsKey(vessel.vesselName))
                                    {
                                        if (BDACompetitionMode.Instance.Scores.ScoreData[vessel.vesselName].hits > oldScore) //apply HP gain every time a hit is scored
                                        {
                                            part.Current.AddHealth(Vampirism, true);
                                            oldScore = BDACompetitionMode.Instance.Scores.ScoreData[vessel.vesselName].hits;
                                        }

                                    }
                                }
                            }
                    }
                }
                if (ResourceTax.Count > 0 && TaxRate != 0)
                {
                    if (TaxRate != 0 && Accumulator > 5) //Apply resource tax every 5 seconds
                    {
                        for (int i = 0; i < ResourceTax.Count; i++)
                        {
                            part.RequestResource(ResourceTax[i], TaxRate, ResourceFlowMode.ALL_VESSEL);
                        }
                    }
                }
                if (Regen != 0 || TaxRate != 0)
                {
                    if (Accumulator > 5)
                    {
                        Accumulator = 0;
                    }
                    else
                    {
                        Accumulator += TimeWarp.fixedDeltaTime;
                    }
                }
            }
        }
    }
}

