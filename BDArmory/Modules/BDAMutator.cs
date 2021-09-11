using BDArmory.Core;
using BDArmory.Control;
using BDArmory.Misc;
using System.Collections.Generic;
using UnityEngine;
using BDArmory.Core.Extension;
using BDArmory.Core.Module;
using BDArmory.UI;
using BDArmory.FX;

namespace BDArmory.Modules
{
    class BDAMutator : PartModule
    {
        float startTime;
        bool mutatorEnabled = false;
        public List<string> mutators;
        private bool random = false;
        private MutatorInfo mutatorInfo;
        private float Vampirism = 0;
        private float Regen = 0;
        private float Strength = 1;
        private float Defense = 1;
        private float engineMult;
        private bool Vengeance = false;
        private List<string> ResourceTax;
        private double TaxRate = 0;

        private int oldScore = 0;
        bool applyVampirism = false;
        private float Accumulator;

        private string iconPath;
        private Texture2D icon;

        public override void OnStart(StartState state)
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                part.force_activate();
            }
            base.OnStart(state);
        }


        public void EnableMutator(string name = "def") //FIXME - when using apply on timer and !apply global, this NREs
        {
            if (mutatorEnabled) //replace current mutator with new one
            {
                DisableMutator();
            }
            if (name == "def") //mutator not specified, randomly choose from selected mutators
            {
                if (BDArmorySettings.MUTATOR_LIST.Count > 0)
                {                    
                    for (int d = 0; d < BDArmorySettings.MUTATOR_APPLY_NUM; d++)
                    {
                        int i = UnityEngine.Random.Range(0, BDArmorySettings.MUTATOR_LIST.Count);
                        if (!mutators.Contains(MutatorInfo.mutators[BDArmorySettings.MUTATOR_LIST[i]].name))
                        {
                            mutators.Add(MutatorInfo.mutators[BDArmorySettings.MUTATOR_LIST[i]].name + "; ");
                        }
                        else
                        {
                            if (d != 0)
                            {
                                d--;
                            }
                        }
                    }
                }
            }
            mutators = BDAcTools.ParseNames(name);
            for (int r = 0; r < BDArmorySettings.MUTATOR_APPLY_NUM; r++)
            {
                name = MutatorInfo.mutators[mutators[r]].name;
                Debug.Log("[ModuleMutator] current name( " + r + ") = " + name);
                mutatorInfo = MutatorInfo.mutators[name];
                
                Debug.Log("[ModuleMutator] beginning mutator initialization of " + name + " on " + part.vessel.name);
                if (mutatorInfo.weaponMod)
                {
                    using (var weapon = VesselModuleRegistry.GetModules<ModuleWeapon>(vessel).GetEnumerator())
                        while (weapon.MoveNext())
                        {
                            if (weapon.Current == null) continue;
                            if (mutatorInfo.weaponType != "def")
                            {
                                weapon.Current.ParseWeaponType(mutatorInfo.weaponType);
                            }
                            if (mutatorInfo.bulletType != "def")
                            {
                                weapon.Current.currentType = mutatorInfo.bulletType;
                                weapon.Current.useCustomBelt = false;
                                weapon.Current.SetupBulletPool();
                                weapon.Current.ParseAmmoStats();
                            }

                            if (mutatorInfo.RoF > 0)
                            {
                                weapon.Current.roundsPerMinute = mutatorInfo.RoF;
                            }
                            if (mutatorInfo.MaxDeviation > 0)
                            {
                                weapon.Current.maxDeviation = mutatorInfo.MaxDeviation;
                            }
                            if (mutatorInfo.laserDamage > 0)
                            {
                                weapon.Current.laserDamage = mutatorInfo.laserDamage;
                            }
                            if (mutatorInfo.instaGib)
                            {
                                weapon.Current.instagib = mutatorInfo.instaGib;
                            }
                            else
                            {
                                weapon.Current.strengthMutator = Mathf.Clamp(Strength, 0.01f, 9999);
                            }
                            weapon.Current.pulseLaser = true;
                            if (weapon.Current.eWeaponType == ModuleWeapon.WeaponTypes.Laser)
                            {
                                weapon.Current.SetupLaserSpecifics();
                            }
                            if (weapon.Current.eWeaponType == ModuleWeapon.WeaponTypes.Rocket && weapon.Current.weaponType != "rocket")
                            {
                                weapon.Current.rocketPod = false;
                                weapon.Current.externalAmmo = true;
                            }
                            weapon.Current.resourceSteal = mutatorInfo.resourceSteal;
                            //Debug.Log("[MUTATOR] current weapon status: " + weapon.Current.WeaponStatusdebug());
                        }
                }

                if (mutatorInfo.EngineMult != 0)
                {
                    using (var engine = VesselModuleRegistry.GetModuleEngines(vessel).GetEnumerator())
                        while (engine.MoveNext())
                        {
                            engine.Current.thrustPercentage *= mutatorInfo.EngineMult;
                        }
                    engineMult = mutatorInfo.EngineMult;
                }
                if (mutatorInfo.Vampirism > 0)
                {
                    Vampirism = mutatorInfo.Vampirism;
                }
                if (mutatorInfo.Regen != 0)
                {
                    Regen = mutatorInfo.Regen;
                }
                Defense = Mathf.Clamp(mutatorInfo.Defense, 0.05f, 99999);

                using (List<Part>.Enumerator part = vessel.Parts.GetEnumerator())
                    while (part.MoveNext())
                    {
                        if (Defense != 1)
                        {
                            var HPT = part.Current.FindModuleImplementing<HitpointTracker>();
                            HPT.defenseMutator = Defense;
                        }
                        if (mutatorInfo.MassMod != 0)
                        {
                            var MM = part.Current.FindModuleImplementing<ModuleMassAdjust>();
                            if (MM == null)
                            {
                                MM = (ModuleMassAdjust)part.Current.AddModule("ModuleMassAdjust");
                                if (BDArmorySettings.MUTATOR_DURATION > 0 && BDArmorySettings.MUTATOR_APPLY_TIMER)
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
                        //part.Current.highlightType = Part.HighlightType.AlwaysOn;
                        //part.Current.SetHighlight(true, false);
                        //part.Current.SetHighlightColor(mutatorColor[0]);
                    }
                if (!Vengeance && mutatorInfo.Vengeance)
                {
                    Vengeance = mutatorInfo.Vengeance;
                }
                if (Vengeance)
                {
                    /*
                    var nuke = vessel.rootPart.FindModuleImplementing<RWPS3R2NukeModule>();
                    if (nuke == null)
                    {
                        nuke = (RWPS3R2NukeModule)vessel.rootPart.AddModule("RWPS3R2NukeModule");
                        nuke.reportingName = "Vengeance";
                    }
                    */
                    part.OnJustAboutToBeDestroyed += Detonate;
                }
                if (!string.IsNullOrEmpty(mutatorInfo.resourceTax))
                {
                    ResourceTax = BDAcTools.ParseNames(mutatorInfo.resourceTax);
                }
                if (mutatorInfo.resourceTaxRate != 0)
                {
                    TaxRate = mutatorInfo.resourceTaxRate;
                }
            }
            startTime = Time.time;
            //colorTicker = 0;
            mutatorEnabled = true;
        }

        public void DisableMutator()
        {
            if (!mutatorEnabled) return;
            Debug.Log("[MUTATOR]: Disabling " + mutatorInfo.name + "Mutator on " + part.vessel.vesselName);

            using (var weapon = VesselModuleRegistry.GetModules<ModuleWeapon>(vessel).GetEnumerator())
                while (weapon.MoveNext())
                {
                    if (weapon.Current == null) continue;
                    weapon.Current.ParseWeaponType(weapon.Current.weaponType);
                    if (!string.IsNullOrEmpty(weapon.Current.ammoBelt) && weapon.Current.ammoBelt != "def")
                    {
                        weapon.Current.useCustomBelt = true;
                    }
                    weapon.Current.roundsPerMinute = weapon.Current.baseRPM;
                    weapon.Current.maxDeviation = weapon.Current.baseDeviation;
                    weapon.Current.laserDamage = weapon.Current.baseLaserdamage;
                    weapon.Current.pulseLaser = weapon.Current.pulseInConfig;
                    weapon.Current.instagib = false;
                    weapon.Current.strengthMutator = 1;
                    if (weapon.Current.eWeaponType == ModuleWeapon.WeaponTypes.Ballistic)
                    {
                        weapon.Current.SetupBulletPool(); //unnecessary?
                    }
                    weapon.Current.SetupAmmo(null, null);
                    if (weapon.Current.eWeaponType == ModuleWeapon.WeaponTypes.Laser)
                    {
                        weapon.Current.SetupLaserSpecifics();
                    }
                    weapon.Current.resourceSteal = false;
                }
            Debug.Log("[MUTATOR]: Disabling " + mutatorInfo.name + "... Weapons reset");
            if (engineMult != 0)
            {
                using (var engine = VesselModuleRegistry.GetModuleEngines(vessel).GetEnumerator())
                    while (engine.MoveNext())
                    {
                        engine.Current.thrustPercentage /= engineMult;
                    }
            }
            Vampirism = 0;
            Regen = 0;
            Strength = 1;
            Defense = 1;

            using (List<Part>.Enumerator part = vessel.Parts.GetEnumerator())
                while (part.MoveNext())
                {
                    var HPT = part.Current.FindModuleImplementing<HitpointTracker>();
                    HPT.defenseMutator = Defense;

                    //part.Current.highlightType = Part.HighlightType.OnMouseOver;
                    //part.Current.SetHighlightColor(Part.defaultHighlightPart);
                    //part.Current.SetHighlight(false, false);
                }
            Debug.Log("[MUTATOR]: Disabling " + mutatorInfo.name + "... relicMutations reset");
            if (Vengeance)
            {
                Vengeance = false;
                //part.OnJustAboutToBeDestroyed -= Detonate; //throwing an NRE?
            }
            ResourceTax.Clear();
            TaxRate = 0;
            mutatorEnabled = false;
            Debug.Log("[MUTATOR]: " + mutatorInfo.name + "disabled");
        }

        void Update()
        {
            if (HighLogic.LoadedSceneIsFlight && !BDArmorySetup.GameIsPaused && !vessel.packed)
            {
                if (!mutatorEnabled) return;
                if ((BDArmorySettings.MUTATOR_DURATION > 0 && Time.time - startTime > BDArmorySettings.MUTATOR_DURATION * 60) && BDArmorySettings.MUTATOR_APPLY_TIMER)
                {
                    DisableMutator();
                    Debug.Log("[Mutator]: mutator expired, disabling");
                }
                if (BDACompetitionMode.Instance.Scores.ScoreData.ContainsKey(vessel.vesselName))
                {
                    if (BDACompetitionMode.Instance.Scores.ScoreData[vessel.vesselName].hits > oldScore) //apply HP gain every time a hit is scored
                    {
                        oldScore = BDACompetitionMode.Instance.Scores.ScoreData[vessel.vesselName].hits;
                        applyVampirism = true;
                    }
                }
                if (Regen != 0 || Vampirism > 0)
                {
                    using (List<Part>.Enumerator part = vessel.Parts.GetEnumerator())
                        while (part.MoveNext())
                        {
                            if (Regen != 0 && Accumulator > 5) //Add regen HP every 5 seconds
                            {
                                part.Current.AddHealth(Regen);
                            }
                            if (Vampirism > 0 && applyVampirism)
                            {
                                part.Current.AddHealth(Vampirism, true);
                            }
                        }
                }
                applyVampirism = false;
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
        void OnGUI() 
        {
            if (HighLogic.LoadedSceneIsFlight && BDArmorySetup.GAME_UI_ENABLED && !MapView.MapIsEnabled && BDArmorySettings.MUTATOR_ICONS)
            {
                if (mutatorEnabled)
                {
                    Vector3 screenPos = BDGUIUtils.GetMainCamera().WorldToViewportPoint(vessel.CoM);
                    if (screenPos.z < 0) return; //dont draw if point is behind camera
                    if (screenPos.x != Mathf.Clamp01(screenPos.x)) return; //dont draw if off screen
                    if (screenPos.y != Mathf.Clamp01(screenPos.y)) return;
                    float yPos = ((1 - screenPos.y) * Screen.height) - (0.5f * (30 * BDTISettings.ICONSCALE)) - (30 * BDTISettings.ICONSCALE);

                    for (int i = 0; i < BDArmorySettings.MUTATOR_APPLY_NUM; i++)
                    {
                        float xPos = (screenPos.x * Screen.width) - (0.5f * 30 * BDTISettings.ICONSCALE) - ((BDArmorySettings.MUTATOR_APPLY_NUM-1) * 0.5f * 30 * BDTISettings.ICONSCALE);
                        Rect iconRect = new Rect(xPos + (i * 30 * BDTISettings.ICONSCALE), yPos, (30 * BDTISettings.ICONSCALE), (30 * BDTISettings.ICONSCALE));
                                               
                        iconPath = MutatorInfo.mutators[mutators[i]].icon;
                        switch (iconPath)
                        {
                            case "IconAccuracy":
                                icon = BDTISetup.Instance.MutatorIconAcc;
                                break;
                            case "IconAttack":
                                icon = BDTISetup.Instance.MutatorIconAtk;
                                break;
                            case "IconAttack2":
                                icon = BDTISetup.Instance.MutatorIconAtk2;
                                break;
                            case "IconBallistic":
                                icon = BDTISetup.Instance.MutatorIconBullet;
                                break;
                            case "IconDefense":
                                icon = BDTISetup.Instance.MutatorIconDefense;
                                break;
                            case "IconLaser":
                                icon = BDTISetup.Instance.MutatorIconLaser;
                                break;
                            case "IconMass":
                                icon = BDTISetup.Instance.MutatorIconMass;
                                break;
                            case "IconRegen":
                                icon = BDTISetup.Instance.MutatorIconRegen;
                                break;
                            case "IconRocket":
                                icon = BDTISetup.Instance.MutatorIconRocket;
                                break;
                            case "IconSkull":
                                icon = BDTISetup.Instance.MutatorIconDoom;
                                break;
                            case "IconSpeed":
                                icon = BDTISetup.Instance.MutatorIconSpeed;
                                break;
                            case "IconTarget":
                                icon = BDTISetup.Instance.MutatorIconTarget;
                                break;
                            case "IconVampire":
                                icon = BDTISetup.Instance.MutatorIconVampire;
                                break;
                            case "IconUnknown":
                                icon = BDTISetup.Instance.MutatorIconNull;
                                break;
                            default: // Other?
                                icon = BDTISetup.Instance.MutatorIconNull;
                                break;
                        }
                        if (icon != null)
                        {
                            GUI.DrawTexture(iconRect, icon);
                        }
                    }
                }
            }
        }
        void Detonate()
        {
            if (!Vengeance) return;
            if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.Mutator] triggering vengeance nuke");
            NukeFX.CreateExplosion(part.transform.position, ExplosionSourceType.BattleDamage, this.vessel.GetName(), "BDArmory/Models/explosion/explosion", "BDArmory/Sounds/explode1", 2.5f, 100, 500, 0.05f, 0.05f, true, "Vengeance Explosion");
        }
    }
}

