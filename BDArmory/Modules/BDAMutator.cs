using BDArmory.Core;
using BDArmory.Control;
using BDArmory.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BDArmory.Modules
{
	class BDAMutator : PartModule
	{
        float startTime;
        bool mutatorEnabled = false;
        public List<string> mutators;

        private MutatorInfo mutatorInfo;

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
                        weapon.Current.SetupBulletPool();
                        weapon.Current.ParseAmmoStats();
                        if (weapon.Current.eWeaponType == ModuleWeapon.WeaponTypes.Laser)
                        {
                            weapon.Current.SetupLaserSpecifics();
                        }
                    }
            }
            //how do I want to do vampirism? concept is steal Hp with every hit - need to be able to register a bullet hit, and need to get damage from that hit, then need to iterate through all parts on the vessel to apply a little bit of HP
            //would be a Update() thing, maybe have it watch tData.hitCounts[vesselname], and eveytime hitcounts > oldHitcount, grab the difference between tData.DamageFromBullets and olddamage
            startTime = Time.time;
            mutatorEnabled = true; 
        }

        public void DisableMutator()
        {

        }

        public void Update()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (Time.time - startTime > BDArmorySettings.MUTATOR_DURATION)
                {
                    DisableMutator();
                }
            }
        }
    }
}

