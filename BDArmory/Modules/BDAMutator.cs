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

        private MutatorInfo mutatorInfo;
        private float Vampirism = 0;
        private float Regen = 0;
        private float Strength = 1;
        private float Defense = 1;
        private bool Vengeance = false;
        private List<string> ResourceTax;
        private double TaxRate = 0;

        private int oldScore = 0;
        bool applyVampirism = false;
        private float Accumulator;

        private Color mutatorColor;
        public Material IconMat;

        public static string textureDir = "BDArmory/Textures/Mutators/";
        string iconPath = "IconAttack";
        private Texture2D icon;
        public Texture2D mutatorIcon
        {
            //get { return icon ? icon : icon = GameDatabase.Instance.GetTexture(textureDir + iconPath, false); }
            get { return icon ? icon : icon = GameDatabase.Instance.GetTexture(iconPath, false); }
        }

        public override void OnStart(StartState state)
        {
            IconMat = new Material(Shader.Find("KSP/Particles/Alpha Blended"));
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
                DisableMutator(); //since weapons now only modify the scpecific fields needed for the mut, disable this and have mutators able to stack?
                //problem is that would only happen on timer/on kill mode, unless global is set to have a random (one at a time) and all (all selected mutators are loaded) modes
                //Debug.Log("[MUTATOR]: found active mutator, disabling");
            }
            if (name == "def") //mutator not specified, randomly choose from selected mutators
            {
                mutators = BDArmorySettings.MUTATOR_LIST;
                int i = UnityEngine.Random.Range(0, mutators.Count);
                name = MutatorInfo.mutators[mutators[i]].name;
            }
            mutatorInfo = MutatorInfo.mutators[name];
            if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[MUTATOR]: initializing " + mutatorInfo.name + "Mutator on " + part.vessel.vesselName);
            iconPath = mutatorInfo.iconPath;
            icon = null;
            icon = GameDatabase.Instance.GetTexture(textureDir + iconPath, false);
            Color.RGBToHSV(Misc.Misc.ParseColor255(mutatorInfo.iconColor), out float H, out float S, out float V);
            mutatorColor = Color.HSVToRGB(H, S, V);

            if (mutatorInfo.weaponMod)
            {
                using (var weapon = VesselModuleRegistry.GetModules<ModuleWeapon>(vessel).GetEnumerator())
                    while (weapon.MoveNext())
                    {
                        if (weapon.Current == null) continue;
                        if (mutatorInfo.weaponType != "def")
                        {
                            weapon.Current.ParseWeaponType(mutatorInfo.weaponType); //this is throwing an error with rockets?
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
            }
            Vampirism = mutatorInfo.Vampirism;
            Regen = mutatorInfo.Regen;
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
                    part.Current.highlightType = Part.HighlightType.AlwaysOn;
                    part.Current.SetHighlight(true, false);
                    part.Current.SetHighlightColor(mutatorColor);
                }
            Vengeance = mutatorInfo.Vengeance;
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

            ResourceTax = BDAcTools.ParseNames(mutatorInfo.resourceTax);
            TaxRate = mutatorInfo.resourceTaxRate;

            startTime = Time.time;
            mutatorEnabled = true;
        }

        public void DisableMutator()
        {
            if (!mutatorEnabled) return;
            //Debug.Log("[MUTATOR]: Disabling " + mutatorInfo.name + "Mutator on " + part.vessel.vesselName);
            if (mutatorInfo.weaponMod)
            {
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
            }

            if (mutatorInfo.EngineMult != 0)
            {
                using (var engine = VesselModuleRegistry.GetModuleEngines(vessel).GetEnumerator())
                    while (engine.MoveNext())
                    {
                        engine.Current.thrustPercentage /= mutatorInfo.EngineMult;
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

                    part.Current.highlightType = Part.HighlightType.OnMouseOver;
                    part.Current.SetHighlightColor(Part.defaultHighlightPart);
                    part.Current.SetHighlight(false, false);
                }
            if (Vengeance)
            {
                Vengeance = false;
                part.OnJustAboutToBeDestroyed -= Detonate;
            }
            /*
            if (Vengeance)
            {
                var nuke = vessel.rootPart.FindModuleImplementing<RWPS3R2NukeModule>();
                if (nuke != null)
                {
                    vessel.rootPart.RemoveModule(nuke);
                }
            }
            */
            ResourceTax.Clear();
            TaxRate = 0;
            mutatorEnabled = false;
        }

        void Update()
        {
            if (HighLogic.LoadedSceneIsFlight && !BDArmorySetup.GameIsPaused && !vessel.packed)
            {
                if (!mutatorEnabled) return;
                if ((BDArmorySettings.MUTATOR_DURATION > 0 && Time.time - startTime > BDArmorySettings.MUTATOR_DURATION * 60) && BDArmorySettings.MUTATOR_APPLY_TIMER)
                {
                    DisableMutator();
                    //Debug.Log("[Mutator]: mutator expired, disabling");
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

        void OnGUI() //honestly, if this is more bonus functionality than anything, an extra way to show which mutator is active on a craft for Mutators on kill or chaos mode
        {
            if ((HighLogic.LoadedSceneIsFlight && BDArmorySetup.GAME_UI_ENABLED && !MapView.MapIsEnabled && BDTISettings.TEAMICONS) ||
                HighLogic.LoadedSceneIsFlight && !BDArmorySetup.GAME_UI_ENABLED && !MapView.MapIsEnabled && BDTISettings.PERSISTANT && BDTISettings.TEAMICONS)
            {
                if (mutatorEnabled)
                {
                    bool offscreen = false;
                    Vector3 screenPos = BDGUIUtils.GetMainCamera().WorldToViewportPoint(vessel.CoM);
                    if (screenPos.z < 0)
                    {
                        offscreen = true;
                    }
                    if (screenPos.x != Mathf.Clamp01(screenPos.x))
                    {
                        offscreen = true;
                    }
                    if (screenPos.y != Mathf.Clamp01(screenPos.y))
                    {
                        offscreen = true;
                    }
                    float xPos = (screenPos.x * Screen.width) - (0.5f * (20 * BDTISettings.ICONSCALE));
                    float yPos = ((1 - screenPos.y) * Screen.height) - (0.5f * (20 * BDTISettings.ICONSCALE)) - (20 * BDTISettings.ICONSCALE);

                    if (!offscreen)
                    {
                        if (IconMat == null)
                        {
                            IconMat = new Material(Shader.Find("KSP/Particles/Alpha Blended"));
                            Debug.Log("[BDArmory.Mutator]: IconMat didn't get initialized on Start. initializing");
                        }
                        IconMat.SetColor("_TintColor", mutatorColor);
                        if (mutatorIcon != null)
                        {
                            IconMat.mainTexture = mutatorIcon;
                        }
                        else
                        {
                            IconMat.mainTexture = BDTISetup.Instance.TextureIconGeneric; //nope, still returning a missing texture error. Debug 
                        }
                        Rect iconRect = new Rect(xPos, yPos, (60 * BDTISettings.ICONSCALE), (60 * BDTISettings.ICONSCALE));
                        Graphics.DrawTexture(iconRect, mutatorIcon, IconMat);
                    }
                }
            }
        }
        void Detonate()
        {
            if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.Mutator] triggering vengeance nuke");
            NukeFX.CreateExplosion(part.transform.position, ExplosionSourceType.BattleDamage, this.vessel.GetName(), "BDArmory/Models/explosion/explosion", "BDArmory/Sounds/explode1", 2.5f, 100, 500, 0.05f, 0.05f, true, "Vengeance Explosion");
        }
    }
}
//figure out why the icon isn't getting drawn  - What's NRE'ing?
//figure out what's throwing an NRE when enabling a mutator with weaponType = rocket;

