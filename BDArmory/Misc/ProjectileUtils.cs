﻿using BDArmory.Control;
using BDArmory.Core;
using BDArmory.Core.Extension;
using BDArmory.Core.Module;
using BDArmory.FX;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BDArmory.Misc
{
    class ProjectileUtils
    {
        static HashSet<string> FuelResources
        {
            get
            {
                if (_FuelResources == null)
                {
                    _FuelResources = new HashSet<string>();
                    foreach (var resource in PartResourceLibrary.Instance.resourceDefinitions)
                    {
                        if (resource.name.EndsWith("Fuel") || resource.name.EndsWith("Oxidizer") || resource.name.EndsWith("Air") || resource.name.EndsWith("Charge") || resource.name.EndsWith("Gas") || resource.name.EndsWith("Propellant")) // FIXME These ought to be configurable
                        { _FuelResources.Add(resource.name); }
                    }
                    Debug.Log("[BDArmory.ProjectileUtils]: Fuel resources: " + string.Join(", ", _FuelResources));
                }
                return _FuelResources;
            }
        }
        static HashSet<string> _FuelResources;
        static HashSet<string> AmmoResources
        {
            get
            {
                if (_AmmoResources == null)
                {
                    _AmmoResources = new HashSet<string>();
                    foreach (var resource in PartResourceLibrary.Instance.resourceDefinitions)
                    {
                        if (resource.name.EndsWith("Ammo") || resource.name.EndsWith("Shell") || resource.name.EndsWith("Shells") || resource.name.EndsWith("Rocket") || resource.name.EndsWith("Rockets") || resource.name.EndsWith("Bolt") || resource.name.EndsWith("Mauser"))
                        { _AmmoResources.Add(resource.name); }
                    }
                    Debug.Log("[BDArmory.ProjectileUtils]: Ammo resources: " + string.Join(", ", _AmmoResources));
                }
                return _AmmoResources;
            }
        }
        static HashSet<string> _AmmoResources;
        static HashSet<string> CMResources
        {
            get
            {
                if (_CMResources == null)
                {
                    _CMResources = new HashSet<string>();
                    foreach (var resource in PartResourceLibrary.Instance.resourceDefinitions)
                    {
                        if (resource.name.EndsWith("Flare") || resource.name.EndsWith("Smoke") || resource.name.EndsWith("Chaff"))
                        { _CMResources.Add(resource.name); }
                    }
                    Debug.Log("[BDArmory.ProjectileUtils]: Couter-measure resources: " + string.Join(", ", _CMResources));
                }
                return _CMResources;
            }
        }
        static HashSet<string> _CMResources;

        public static HashSet<string> IgnoredPartNames = new HashSet<string> { "bdPilotAI", "bdShipAI", "missileController", "bdammGuidanceModule" };
        public static bool IsIgnoredPart(Part part) { return ProjectileUtils.IgnoredPartNames.Contains(part.partInfo.name); }

        public static void ApplyDamage(Part hitPart, RaycastHit hit, float multiplier, float penetrationfactor, float caliber, float projmass, float impactVelocity, float DmgMult, double distanceTraveled, bool explosive, bool incendiary, bool hasRichocheted, Vessel sourceVessel, string name, string team, ExplosionSourceType explosionSource, bool firstHit, bool partAlreadyHit, bool cockpitPen)
        {
            //hitting a vessel Part
            //No struts, they cause weird bugs :) -BahamutoD
            if (hitPart == null) return;
            if (hitPart.partInfo.name.Contains("Strut")) return;
            if (IsIgnoredPart(hitPart)) return; // Ignore ignored parts.

            // Add decals
            if (BDArmorySettings.BULLET_HITS)
            {
                BulletHitFX.CreateBulletHit(hitPart, hit.point, hit, hit.normal, hasRichocheted, caliber, penetrationfactor, team);
            }
            // Apply damage
            float damage;
            damage = hitPart.AddBallisticDamage(projmass, caliber, multiplier, penetrationfactor, DmgMult, impactVelocity, explosionSource);

            if (BDArmorySettings.BATTLEDAMAGE)
            {
                BattleDamageHandler.CheckDamageFX(hitPart, caliber, penetrationfactor, explosive, incendiary, sourceVessel.GetName(), hit, partAlreadyHit, cockpitPen);
            }
            // Debug.Log("DEBUG Ballistic damage to " + hitPart + ": " + damage + ", calibre: " + caliber + ", multiplier: " + multiplier + ", pen: " + penetrationfactor);

            // Update scoring structures
            if (firstHit)
            {
                ApplyScore(hitPart, sourceVessel.GetName(), distanceTraveled, damage, name, explosionSource, true);
            }
            StealResources(hitPart, sourceVessel);
        }
        public static void ApplyScore(Part hitPart, string sourceVessel, double distanceTraveled, float damage, string name, ExplosionSourceType ExplosionSource, bool newhit = false)
        {
            var aName = sourceVessel;//.GetName();
            var tName = hitPart.vessel.GetName();
            switch (ExplosionSource)
            {
                case ExplosionSourceType.Bullet:
                    if (newhit) BDACompetitionMode.Instance.Scores.RegisterBulletHit(aName, tName, name, distanceTraveled);
                    BDACompetitionMode.Instance.Scores.RegisterBulletDamage(aName, tName, damage);
                    break;
                case ExplosionSourceType.Rocket:
                    //if (newhit) BDACompetitionMode.Instance.Scores.RegisterRocketStrike(aName, tName);
                    BDACompetitionMode.Instance.Scores.RegisterRocketDamage(aName, tName, damage);
                    break;
                case ExplosionSourceType.Missile:
                    BDACompetitionMode.Instance.Scores.RegisterMissileDamage(aName, tName, damage);
                    break;
                case ExplosionSourceType.BattleDamage:
                    BDACompetitionMode.Instance.Scores.RegisterBattleDamage(aName, hitPart.vessel, damage);
                    break;
            }
        }
        public static void StealResources(Part hitPart, Vessel sourceVessel, bool thiefWeapon = false)
        {
            // steal resources if enabled
            if (BDArmorySettings.RESOURCE_STEAL_ENABLED || thiefWeapon)
            {
                if (BDArmorySettings.RESOURCE_STEAL_FUEL_RATION > 0f) StealResource(hitPart.vessel, sourceVessel, FuelResources, BDArmorySettings.RESOURCE_STEAL_FUEL_RATION);
                if (BDArmorySettings.RESOURCE_STEAL_AMMO_RATION > 0f) StealResource(hitPart.vessel, sourceVessel, AmmoResources, BDArmorySettings.RESOURCE_STEAL_AMMO_RATION, true);
                if (BDArmorySettings.RESOURCE_STEAL_CM_RATION > 0f) StealResource(hitPart.vessel, sourceVessel, CMResources, BDArmorySettings.RESOURCE_STEAL_CM_RATION, true);
            }
        }
        public static float CalculateArmorPenetration(Part hitPart, float penetration, float thickness)
        {
            ///////////////////////////////////////////////////////////////////////
            // Armor Penetration
            ///////////////////////////////////////////////////////////////////////
            //if (thickness < 0) thickness = (float)hitPart.GetArmorThickness(); //returns mm
            //want thickness of armor, modified by angle of hit, use thickness val fro projectile
            if (thickness <= 0)
            {
                thickness = 1;
            }
            var penetrationFactor = penetration / thickness;

            if (BDArmorySettings.DRAW_ARMOR_LABELS)
            {
                Debug.Log("[BDArmory.ProjectileUtils{Armor Penetration}]:" + hitPart + ", " + hitPart.vessel.GetName() + ": Armor penetration = " + penetration + "mm | Thickness = " + thickness + "mm");
            }
            if (penetrationFactor < 1)
            {
                if (BDArmorySettings.DRAW_ARMOR_LABELS)
                {
                    Debug.Log("[BDArmory.ProjectileUtils{Armor Penetration}]: Bullet Stopped by Armor");
                }
            }
            return penetrationFactor;
        }
        public static void CalculateArmorDamage(Part hitPart, float penetrationFactor, float caliber, float hardness, float ductility, float density, float impactVel, string sourceVesselName, ExplosionSourceType explosionSource, float mass)
        {
            ///<summary>
            /// Calculate damage to armor from kinetic impact based on armor mechanical properties
            /// Sufficient penetration by bullet will result in armor spalling or failure
            /// </summary>
            if (mass <= 0) return;//ArmorType "None"; no armor to damage
            float thickness = (float)hitPart.GetArmorThickness();
            if (thickness <= 0) return; //No armor present to spall/damage

            double volumeToReduce = -1;
            float caliberModifier = 1; //how many calibers wide is the armor loss/spall?
            float spallMass = 0;
            float spallCaliber = 1;
            //Spalling/Armor damage
            if (ductility > 0.20f)
            {
                if (penetrationFactor > 2) //material can't stretch fast enough, necking/point embrittlelment/etc, material tears
                {
                    if (thickness < 2 * caliber)
                    {
                        caliberModifier = 4;                    // - bullet capped by necked material, add to caliber/bulletmass
                    }
                    else
                    {
                        caliberModifier = 2;
                    }
                    spallCaliber = caliber * (caliberModifier / 2);
                    spallMass = (spallCaliber * spallCaliber * Mathf.PI / 400) * (thickness / 10) * (density / 1000000) / 1000;
                    if (BDArmorySettings.DRAW_ARMOR_LABELS)
                    {
                        Debug.Log("[BDArmory.ProjectileUtils]: " + hitPart + ", " + hitPart.vessel.GetName() + ": Armor spalling! Diameter: " + spallCaliber + "; mass: " + spallMass + "g");
                    }
                }
                if (penetrationFactor > 0.75 && penetrationFactor < 2) //material deformed around impact point
                {
                    caliberModifier = 2;
                }
            }
            else //ductility < 0.20
            {
                if (hardness > 500)
                {
                    if (penetrationFactor > 1)
                    {
                        if (ductility < 0.05f) //ceramics
                        {
                            volumeToReduce = ((Mathf.CeilToInt(caliber / 500) * Mathf.CeilToInt(caliber / 500)) * (50 * 50) * ((float)hitPart.GetArmorMaxThickness() / 10)); //cm3 //replace thickness with starting thickness, to ensure armor failure removes proper amount of armor
                            //total failue of 50x50cm armor tile(s)
                            if (BDArmorySettings.DRAW_ARMOR_LABELS)
                            {
                                Debug.Log("[BDArmory.ProjectileUtils{CalcArmorDamage}]: Armor failure on " + hitPart + ", " + hitPart.vessel.GetName() + "!");
                            }
                        }
                        else //0.05-0.19 ductility - harder steels, etc
                        {
                            caliberModifier = (20 / (ductility * 100)) * Mathf.Clamp(penetrationFactor, 1, 3);
                        }
                    }
                    if (penetrationFactor > 0.66 && penetrationFactor < 1)
                    {
                        spallCaliber = ((1 - penetrationFactor) + 1) * (caliber * caliber * Mathf.PI / 400);

                        volumeToReduce = spallCaliber; //cm3
                        spallMass = spallCaliber * (density / 1000000) / 1000;
                        if (BDArmorySettings.DRAW_ARMOR_LABELS)
                        {
                            Debug.Log("[BDArmory.ProjectileUtils{CalcArmorDamage}]: Armor failure on " + hitPart + ", " + hitPart.vessel.GetName() + "!");
                            Debug.Log("[BDArmory.ProjectileUtils{CalcArmorDamage}]: Armor spalling! Diameter: " + spallCaliber + "; mass: " + spallMass + "kg");
                        }
                    }
                }
                //else //low hardness non ductile materials (i.e. kevlar/aramid) not going to spall
            }

            if (volumeToReduce < 0)
            {
                var modifiedCaliber = 0.5f * caliber * caliberModifier;
                volumeToReduce = modifiedCaliber * modifiedCaliber * Mathf.PI / 100 * (thickness / 10); //cm3
            }
            if (BDArmorySettings.DRAW_ARMOR_LABELS)
            {
                Debug.Log("[BDArmory.ProjectileUtils{CalcArmorDamage}]: " + hitPart + " on " + hitPart.vessel.GetName() + " Armor volume lost: " + Math.Round(volumeToReduce) + " cm3");
            }
            hitPart.ReduceArmor((double)volumeToReduce);
            if (penetrationFactor < 1)
            {
                if (BDArmorySettings.DRAW_ARMOR_LABELS)
                {
                    Debug.Log("[BDArmory.ProjectileUtils{CalcArmorDamage}]: Bullet Stopped by Armor");
                }
            }
            if (spallMass > 0)
            {
                float damage = hitPart.AddBallisticDamage(spallMass, spallCaliber, 1, 1.1f, 1, (impactVel / 2), explosionSource);
                if (BDArmorySettings.DRAW_ARMOR_LABELS)
                {
                    Debug.Log("[BDArmory.ProjectileUtils]: " + hitPart + " on " + hitPart.vessel.GetName() + " takes Spall Damage: " + damage);
                }
                ApplyScore(hitPart, sourceVesselName, 0, damage, "Spalling", explosionSource);
            }
        }
        public static void CalculateShrapnelDamage(Part hitPart, RaycastHit hit, float caliber, float HEmass, float detonationDist, string sourceVesselName, ExplosionSourceType explosionSource, float projmass = -1, float penetrationFactor = -1)
        {
            /// <summary>
            /// Calculates damage from flak/shrapnel, based on HEmass and projMass, of both contact and airburst detoantions.
            /// Calculates # hits per m^2 based on distribution across sphere detonationDist in radius
            /// Shrapnel penetration dist determined by caliber, penetration. Penetration = -1 is part only hit by blast/airburst
            /// </summary>
            float thickness = (float)hitPart.GetArmorThickness();
            if (thickness < 1)
            {
                thickness = 0.1f; //prevent divide by zero or other odd behavior
            }
            double volumeToReduce = 0;
            var Armor = hitPart.FindModuleImplementing<HitpointTracker>();
            if (Armor != null)
            {
                float Ductility = Armor.Ductility;
                float hardness = Armor.Hardness;
                float Strength = Armor.Strength;
                float Density = Armor.Density;
                float mass = Armor.armorMass;
                //Spalling/Armor damage
                //minimum armor thickness to stop shrapnel is 0.08 calibers for 1.4-3.5% HE by mass; 0.095 calibers for 3.5-5.99% HE by mass; and .11 calibers for 6% HE by mass, assuming detonation is > 5calibers away
                //works out to y = 0.0075x^(1.05)+0.06
                //20mm Vulcan is HE fraction 13%, so 0.17 calibers(3.4mm), GAU ~0.19, or 0.22calibers(6.6mm), AbramsHe 80%, so 0.8calibers(96mm)
                //HE contact detonation penetration; minimum thickness of armor to receive caliber sized hole: thickness = (2.576 * 10 ^ -20) * Caliber * ((velocity/3.2808) ^ 5.6084) * Cos(2 * angle - 45)) +(0.156 * diameter)
                //TL;Dr; armor thickness needed is .156*caliber, and if less than, will generate a caliber*proj length hole. half the min thickness will yield a 2x size hole
                //angle and impact vel have negligible impact on hole size
                //if the round penetrates, increased damage; min thickness of .187 calibers to prevent armor cracking //is this per the 6% HE fraction above, or ? could just do the shrapnelfraction * 1.41/1.7
                float HERatio = 0.06f;
                if (projmass < HEmass)
                {
                    projmass = HEmass * 1.25f; //sanity check in case this is 0
                }
                HERatio = Mathf.Clamp(HEmass / projmass, 0.01f, 0.95f);
                float frangibility = 5000 * HERatio;
                float shrapnelThickness = ((.0075f * Mathf.Pow((HERatio * 100), 1.05f)) + .06f) * caliber; //min thickness of material for HE to blow caliber size hole in steel
                shrapnelThickness *= (950 / Strength) * (8000 / Density) * (Mathf.Sqrt(1100 / hardness)); //adjusted min thickness after material hardness/strength/density
                float shrapnelCount;
                float radiativeArea = !double.IsNaN(hitPart.radiativeArea) ? (float)hitPart.radiativeArea : hitPart.GetArea();
                if (detonationDist > 0)
                {
                    shrapnelCount = Mathf.Clamp((frangibility / (4 * Mathf.PI * detonationDist * detonationDist)) * (float)(radiativeArea / 3), 0, (frangibility * .4f)); //fragments/m2
                }
                else //srf detonation
                {
                    shrapnelCount = frangibility * 0.4f;
                }
                //shrapnelCount *= (float)(radiativeArea / 3); //shrapnelhits/part
                float shrapnelMass = ((projmass * (1 - HERatio)) / frangibility) * shrapnelCount;
                float damage;
                // go through and make sure all unit conversions correct
                if (penetrationFactor < 0) //airburst/parts caught in AoE
                {
                    if (detonationDist > (5 * (caliber / 1000))) //caliber in mm, not m
                    {
                        if (thickness < shrapnelThickness && shrapnelCount > 0)
                        {
                            //armor penetration by subcaliber shrapnel; use dist to abstract # of fragments that hit to calculate damage, assuming 5k fragments for now
                            if (mass > 0)
                            {
                                volumeToReduce = (((caliber * caliber) * 1.5f) / shrapnelCount * thickness) / 1000; //rough approximation of volume / # of fragments
                                hitPart.ReduceArmor(volumeToReduce);
                            }
                            if (BDArmorySettings.DRAW_ARMOR_LABELS)
                            {
                                Debug.Log("[BDArmory.ProjectileUtils]: " + hitPart.name + " on " + hitPart.vessel.GetName() + ", " + shrapnelCount + " shrapnel hits; Armor damage: " + volumeToReduce + "cm3; part damage: ");
                            }
                            damage = hitPart.AddBallisticDamage(shrapnelMass, 0.1f, 1, (shrapnelThickness / thickness), 1, 430, explosionSource); //expansion rate of tnt/petn ~7500m/s
                            ApplyScore(hitPart, sourceVesselName, 0, damage, "Shrapnel", explosionSource);
                            CalculateArmorDamage(hitPart, (shrapnelThickness / thickness), Mathf.Sqrt((float)volumeToReduce / 3.14159f), hardness, Ductility, Density, 430, sourceVesselName, explosionSource, mass);
                            BattleDamageHandler.CheckDamageFX(hitPart, caliber, (shrapnelThickness / thickness), false, false, sourceVesselName, hit); //bypass score mechanic so HE rounds don't have inflated scores
                        }
                    }
                    else //within 5 calibers of detonation
                    {
                        if (shrapnelCount > 0)
                        {
                            if (thickness < (shrapnelThickness * 1.41f))
                            {
                                //armor breach
                                if (mass > 0)
                                {
                                    volumeToReduce = ((caliber * thickness * (caliber * 4)) / 1000); //cm3
                                    hitPart.ReduceArmor(volumeToReduce);
                                }
                                if (BDArmorySettings.DRAW_ARMOR_LABELS)
                                {
                                    Debug.Log("[BDArmory.ProjectileUtils]: Shrapnel penetration on " + hitPart.name + ",  " + hitPart.vessel.GetName() + "; " + +shrapnelCount + " hits; Armor damage: " + volumeToReduce + "; part damage: ");
                                }
                                damage = hitPart.AddBallisticDamage(shrapnelMass, 0.1f, 1, (shrapnelThickness / thickness), 1, 430, explosionSource); //within 5 calibers shrapnel still getting pushed/accelerated by blast
                                ApplyScore(hitPart, sourceVesselName, 0, damage, "Shrapnel", explosionSource);
                                CalculateArmorDamage(hitPart, (shrapnelThickness / thickness), (caliber * 0.4f), hardness, Ductility, Density, 430, sourceVesselName, explosionSource, mass);
                                BattleDamageHandler.CheckDamageFX(hitPart, caliber, (shrapnelThickness / thickness), true, false, sourceVesselName, hit);
                            }
                            else
                            {
                                if (thickness < (shrapnelThickness * 1.7))//armor cracks;
                                {
                                    if (mass > 0)
                                    {
                                        volumeToReduce = ((Mathf.CeilToInt(caliber / 500) * Mathf.CeilToInt(caliber / 500)) * (50 * 50) * ((float)hitPart.GetArmorMaxThickness() / 10)); //cm3
                                        hitPart.ReduceArmor(volumeToReduce);
                                        if (BDArmorySettings.DRAW_ARMOR_LABELS)
                                        {
                                            Debug.Log("[BDArmory.ProjectileUtils]: Explosive Armor failure; Armor damage: " + volumeToReduce + " on " + hitPart.name + ", " + hitPart.vessel.GetName());
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else //detonates in armor
                {
                    if (penetrationFactor < 1 && penetrationFactor > 0)
                    {
                        thickness *= (1 - penetrationFactor); //armor thickness reduced from projectile penetrating some distance, less distance from proj to back of plate
                        if (thickness < (shrapnelThickness * 1.41f))
                        {
                            //armor breach
                            if (mass > 0)
                            {
                                volumeToReduce = ((caliber * thickness * (caliber * 4)) * 2) / 1000; //cm3
                                hitPart.ReduceArmor(volumeToReduce);
                            }
                            if (BDArmorySettings.DRAW_ARMOR_LABELS)
                            {
                                Debug.Log("[BDArmory.ProjectileUtils]: Shrapnel penetration from in-armor detonation, " + hitPart.name + ",  " + hitPart.vessel.GetName() + "; Armor damage: " + volumeToReduce + "; part damage: ");
                            }
                            damage = hitPart.AddBallisticDamage((projmass * (1 - HERatio)), 0.1f, 1, (shrapnelThickness / thickness), 1, 430, explosionSource); //within 5 calibers shrapnel still getting pushed/accelerated by blast
                            ApplyScore(hitPart, sourceVesselName, 0, damage, "Shrapnel", explosionSource);
                            CalculateArmorDamage(hitPart, (shrapnelThickness / thickness), (caliber * 1.4f), hardness, Ductility, Density, 430, sourceVesselName, explosionSource, mass);
                            BattleDamageHandler.CheckDamageFX(hitPart, caliber, (shrapnelThickness / thickness), true, false, sourceVesselName, hit);
                        }
                    }
                    else //internal detonation
                    {
                        if (BDArmorySettings.DRAW_ARMOR_LABELS)
                        {
                            Debug.Log("[BDArmory.ProjectileUtils]: Through-armor detonation in " + hitPart.name + ", " + hitPart.vessel.GetName());
                        }
                        damage = hitPart.AddBallisticDamage((projmass * (1 - HERatio)), 0.1f, 1, 1.9f, 1, 430, explosionSource); //internal det catches entire shrapnel mass
                        ApplyScore(hitPart, sourceVesselName, 0, damage, "Shrapnel", explosionSource);
                    }
                }
            }
        }
        public static bool CalculateExplosiveArmorDamage(Part hitPart, double BlastPressure, string sourcevessel, RaycastHit hit, ExplosionSourceType explosionSource)
        {
            /// <summary>
            /// Calculates if shockwave from detonation is stopped by armor, and if not, how much damage is done to armor and part in case of armor rupture or spalling
            /// Returns boolean; True = armor stops explosion, False = armor blowthrough
            /// </summary>
            //use blastTotalPressure to get MPa of shock on plate, compare to armor mat tolerances
            float thickness = (float)hitPart.GetArmorThickness();
            if (thickness <= 0) return false; //no armor to stop explosion
            float spallArea;  //using this as a hack for affected srf. area, convert m2 to cm2
            if (hitPart.name.ToLower().Contains("armor"))
            {
                spallArea = hitPart.Modules.GetModule<HitpointTracker>().armorVolume * 10000;
            }
            else
            {
                spallArea = (!double.IsNaN(hitPart.radiativeArea) ? (float)hitPart.radiativeArea : hitPart.GetArea() / 3) * 10000;
            }
            if (BDArmorySettings.DRAW_ARMOR_LABELS && double.IsNaN(hitPart.radiativeArea))
            {
                Debug.Log("[BDArmory.ProjectileUtils{CalculateExplosiveArmorDamage}]: radiative area of part " + hitPart + " was NaN, using approximate area " + spallArea + " instead.");
            }

            float spallMass;
            float damage;
            var Armor = hitPart.FindModuleImplementing<HitpointTracker>();
            if (Armor != null)
            {
                if (Armor.armorMass <= 0) return false;//ArmorType "None"; no armor to block/reduce blast, take full damage
                float ductility = Armor.Ductility;
                float hardness = Armor.Hardness;
                float Strength = Armor.Strength;
                float Density = Armor.Density;
                if (Armor.ArmorPanel) spallArea = Armor.armorVolume;

				float ArmorTolerance = Strength * (1 + ductility) * (thickness/10); //FIXME - this is going to return a value an order of magnitude greater than blast
                                                                                    //Trying thickness /10, so the 10x increase in thickness in mm isn't massively increasing value

                float blowthroughFactor = (float)BlastPressure / ArmorTolerance;
                if (BDArmorySettings.DRAW_ARMOR_LABELS)
                {
                    Debug.Log("[BDArmory.ProjectileUtils]: Beginning ExplosiveArmorDamage(); " + hitPart.name + ", ArmorType:" + Armor.ArmorTypeNum + "; Armor Thickness: " + thickness + "; BlastPressure: " + BlastPressure + "; BlowthroughFactor: " + blowthroughFactor); ;
                }
                //is BlastUtils maxpressure in MPa? confirm blast pressure from ExplosionUtils on same scale/magnitude as armorTolerance
                //something is going on, 25mm steed is enough to no-sell Hellfires (13kg tnt, 33m blastRadius
                if (ductility > 0.20f)
                {
                    if (BlastPressure > ArmorTolerance) //material stress tolerance exceeded, armor rupture
                    {
                        spallMass = spallArea * (thickness / 10) * (Density / 1000000); //entirety of armor lost
                        hitPart.ReduceArmor(spallArea * thickness / 10); //cm3
                        if (BDArmorySettings.DRAW_ARMOR_LABELS)
                        {
                            Debug.Log("[BDArmory.ProjectileUtils]: Armor rupture on " + hitPart.name + ", " + hitPart.vessel.GetName() + "! Size: " + spallArea + "; mass: " + spallMass + "kg");
                        }
                        damage = hitPart.AddBallisticDamage(spallMass / 1000, spallArea, 1, blowthroughFactor, 1, 422.75f, explosionSource);
                        ApplyScore(hitPart, sourcevessel, 0, damage, "Spalling", explosionSource);


                        if (BDArmorySettings.BATTLEDAMAGE)
                        {
                            BattleDamageHandler.CheckDamageFX(hitPart, spallArea, blowthroughFactor, true, false, sourcevessel, hit);
                        }
                        return false;
                    }
                    if (blowthroughFactor > 0.66)
                    {
                        spallArea *= ((1 - ductility) * blowthroughFactor);
                        spallMass = spallArea * (thickness / 30) * (Density / 1000000); //lose 1/3rd thickness from spalling
                        if (BDArmorySettings.DRAW_ARMOR_LABELS)
                        {
                            Debug.Log("[BDArmory.ProjectileUtils]: Explosive Armor spalling" + hitPart.name + ", " + hitPart.vessel.GetName() + "! Size: " + spallArea + "; mass: " + spallMass + "kg");
                        }
                        if (hardness > 500)//armor holds, but spalling
                        {
                            damage = hitPart.AddBallisticDamage(spallMass / 1000, spallArea, 1, blowthroughFactor, 1, 422.75f, explosionSource);
                            ApplyScore(hitPart, sourcevessel, 0, damage, "Spalling", explosionSource);
                        }
                        //else soft enough to not spall. Armor has suffered some deformation, though, weakening it.
                        hitPart.ReduceArmor(spallArea * (thickness / 30)); //cm3
                        if (BDArmorySettings.BATTLEDAMAGE)
                        {
                            BattleDamageHandler.CheckDamageFX(hitPart, spallArea, blowthroughFactor, false, false, sourcevessel, hit);
                        }
                        return true;
                    }
                }
                else //ductility < 0.20
                {
                    if (blowthroughFactor > 1)
                    {
                        if (ductility < 0.05f) //ceramics
                        {
                            var volumeToReduce = (Mathf.CeilToInt(spallArea / 500) * Mathf.CeilToInt(spallArea / 500)) * (50 * 50) * ((float)hitPart.GetArmorMaxThickness() / 10); //cm3
                            //total failue of 50x50cm armor tile(s)
                            if (hardness > 500)
                            {
                                spallMass = volumeToReduce * (Density / 1000000);
                                damage = hitPart.AddBallisticDamage(spallMass / 1000, 500, 1, blowthroughFactor, 1, 422.75f, explosionSource);
                                ApplyScore(hitPart, sourcevessel, 0, damage, "Armor Shatter", explosionSource);
                            }
                            //soft stuff like Aramid not likely to cause major damage
                            hitPart.ReduceArmor(volumeToReduce); //cm3

                            if (BDArmorySettings.DRAW_ARMOR_LABELS)
                            {
                                Debug.Log("[BDArmory.ProjectileUtils]: Armor destruction on " + hitPart.name + ", " + hitPart.vessel.GetName() + "!");
                            }
                            if (BDArmorySettings.BATTLEDAMAGE)
                            {
                                BattleDamageHandler.CheckDamageFX(hitPart, 500, blowthroughFactor, true, false, sourcevessel, hit);
                            }
                        }
                        else //0.05-0.19 ductility - harder steels, etc
                        {
                            spallArea *= ((1.2f - ductility) * blowthroughFactor);
                            spallMass = spallArea * thickness / 10 * (Density / 1000000);
                            hitPart.ReduceArmor(spallArea * (thickness / 10)); //cm3
                            damage = hitPart.AddBallisticDamage(spallMass / 1000, spallArea * 10, 1, blowthroughFactor, 1, 422.75f, explosionSource);
                            ApplyScore(hitPart, sourcevessel, 0, damage, "Spalling", explosionSource);

                            if (BDArmorySettings.DRAW_ARMOR_LABELS)
                            {
                                Debug.Log("[BDArmory.ProjectileUtils]: Armor sundered, " + hitPart.name + ", " + hitPart.vessel.GetName() + "!");
                            }
                            if (BDArmorySettings.BATTLEDAMAGE)
                            {
                                BattleDamageHandler.CheckDamageFX(hitPart, spallArea, blowthroughFactor, true, false, sourcevessel, hit);
                            }
                        }
                        return false;
                    }
                    else
                    {
                        if (blowthroughFactor > 0.33)
                        {
                            if (ductility < 0.05f && hardness < 500) //flexible, non-ductile materials aren't going to absorb or deflect blast;
                            {
                                return false;
                                //but at least they aren't going to be taking much armor damage
                            }
                        }
                        if (blowthroughFactor > 0.66)
                        {
                            if (ductility < 0.05f)
                            {
                                var volumeToReduce = (Mathf.CeilToInt(spallArea / 500) * Mathf.CeilToInt(spallArea / 500)) * (50 * 50) * ((float)hitPart.GetArmorMaxThickness() / 10); //cm3
                                //total failue of 50x50cm armor tile(s)
                                if (hardness > 500)
                                {
                                    spallMass = volumeToReduce * (Density / 1000000);
                                    damage = hitPart.AddBallisticDamage(spallMass / 1000, 500, 1, blowthroughFactor, 1, 422.75f, explosionSource);
                                    ApplyScore(hitPart, sourcevessel, 0, damage, "Armor Shatter", explosionSource);
                                }
                                //soft stuff like Aramid not likely to cause major damage
                                hitPart.ReduceArmor(volumeToReduce); //cm3

                                if (BDArmorySettings.DRAW_ARMOR_LABELS)
                                {
                                    Debug.Log("[BDArmory.ProjectileUtils]: Armor destruction on " + hitPart.name + ", " + hitPart.vessel.GetName() + "!");
                                }
                                if (BDArmorySettings.BATTLEDAMAGE)
                                {
                                    BattleDamageHandler.CheckDamageFX(hitPart, 500, blowthroughFactor, true, false, sourcevessel, hit);
                                }
                            }
                            else //0.05-0.19 ductility - harder steels, etc
                            {
                                spallArea *= ((1.2f - ductility) * blowthroughFactor);
                                if (hardness > 500)
                                {
                                    spallMass = spallArea * thickness / 30 * (Density / 1000000);
                                    damage = hitPart.AddBallisticDamage(spallMass / 1000, spallArea * 10, 1, blowthroughFactor, 1, 422.75f, explosionSource);
                                    ApplyScore(hitPart, sourcevessel, 0, damage, "Spalling", explosionSource);
                                }
                                hitPart.ReduceArmor(spallArea * (thickness / 30)); //cm3

                                if (BDArmorySettings.DRAW_ARMOR_LABELS)
                                {
                                    Debug.Log("[BDArmory.ProjectileUtils]: Armor sundered, " + hitPart.name + ", " + hitPart.vessel.GetName() + "!");
                                }
                                if (BDArmorySettings.BATTLEDAMAGE)
                                {
                                    BattleDamageHandler.CheckDamageFX(hitPart, spallArea, blowthroughFactor, true, false, sourcevessel, hit);
                                }
                            }
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        /*
        public static float CalculatePenetration(float caliber, float projMass, float impactVel, float apBulletMod = 1)
        {
            float penetration = 0;
            if (apBulletMod <= 0) // sanity check/legacy compatibility
            {
                apBulletMod = 1;
            }

            if (caliber > 5) //use the "krupp" penetration formula for anything larger than HMGs
            {
                penetration = (float)(16f * impactVel * Math.Sqrt(projMass / 1000) / Math.Sqrt(caliber) * apBulletMod); //APBulletMod now actually implemented, serves as penetration multiplier, 1 being neutral, <1 for soft rounds, >1 for AP penetrators
            }

            return penetration;
        }
        */
        public static float CalculateProjectileEnergy(float projMass, float impactVel)
        {
            float bulletEnergy = (projMass * 1000) * impactVel; //(should this be 1/2(mv^2) instead? prolly at somepoint, but the abstracted calcs I have use mass x vel, and work, changing it would require refactoring calcs
            if (BDArmorySettings.DRAW_ARMOR_LABELS)
            {
                Debug.Log("[BDArmory.ProjectileUtils]: Bullet Energy: " + bulletEnergy + "; mass: " + projMass + "; vel: " + impactVel);
            }
            return bulletEnergy;
        }

        public static float CalculateArmorStrength(float caliber, float thickness, float Ductility, float Strength, float Density, float SafeTemp, Part hitpart)
        {
            /// <summary>
            /// Armor Penetration calcs for new Armor system
            /// return modified caliber, velocity for penetrating rounds
            /// Math is very much game-ified abstract rather than real-world calcs, but returns numbers consistant with legacy armor system, assuming legacy armor is mild steel (UST ~950 MPa, BHN ~200)
            /// so for now, Good Enough For Government Work^tm
            /// </summary>
            //initial impact calc
            //determine yieldstrength of material
            float yieldStrength;
            if (BDArmorySettings.DRAW_ARMOR_LABELS)
            {
                Debug.Log("[BDArmory.ProjectileUtils]: properties: Tensile:" + Strength + "; Ductility: " + Ductility + "; density: " + Density + "; thickness: " + thickness + "; caliber: " + caliber);
            }
            if (thickness < 1)
            {
                thickness = 1; //prevent divide by zero or other odd behavior
            }
            if (caliber < 1)
            {
                caliber = 20; //prevent divide by zero or other odd behavior
            }
            var modifiedCaliber = (0.5f * caliber) + (0.5f * caliber) * (2f * Ductility * Ductility);
            yieldStrength = modifiedCaliber * modifiedCaliber * Mathf.PI / 100f * Strength * (Density / 7850f) * thickness;
            //assumes bullet is perfect cyl, modded by ductility spreading impact over larger area, times strength/cm2 for threshold energy required to penetrate armor material
            // Ductility is a measure of brittleness, the lower the brittleness, the more the material is willing to bend before fracturing, allowing energy to be spread over more area
            if (Ductility > 0.25f) //up to a point, anyway. Stretch too much...
            {
                yieldStrength *= 0.7f; //necking and point embrittlement reduce total tensile strength of material
            }
            if (hitpart.skinTemperature > SafeTemp) //has the armor started melting/denaturing/whatever?
            {
                yieldStrength *= 0.75f;
                if (hitpart.skinTemperature > SafeTemp * 1.5f)
                {
                    yieldStrength *= 0.5f;
                }
            }
            if (BDArmorySettings.DRAW_ARMOR_LABELS)
            {
                Debug.Log("[BDArmory.ProjectileUtils]: Armor yield Strength: " + yieldStrength);
            }

            return yieldStrength;
        }

        public static float CalculateDeformation(float yieldStrength, float bulletEnergy, float caliber, float impactVel, float hardness, float Density, float HEratio, float apBulletMod)
        {
            if (bulletEnergy < yieldStrength) return caliber; //armor stops the round, but calc armor damage
            else //bullet penetrates. Calculate what happens to the bullet
            {
                //deform bullet from impact
                if (yieldStrength < 1) yieldStrength = 1000;
                float BulletDurabilityMod = ((1 - HEratio) * (caliber / 25)); //rounds that are larger, or have less HE, are structurally stronger and betterresist deformation. Add in a hardness factor for sabots/DU rounds?
                if (BDArmorySettings.DRAW_ARMOR_LABELS)
                {
                    Debug.Log("[BDArmory.ProjectileUtils{Calc Deformation}]: yield:" + yieldStrength + "; Energy: " + bulletEnergy + "; caliber: " + caliber + "; impactVel: " + impactVel);
                    Debug.Log("[BDArmory.ProjectileUtils{Calc Deformation}]: hardness:" + hardness + "; BulletDurabilityMod: " + BulletDurabilityMod + "; density: " + Density);
                }
                float newCaliber = ((((yieldStrength / bulletEnergy) * (hardness * Mathf.Sqrt(Density / 1000))) / impactVel) / (BulletDurabilityMod * apBulletMod)); //faster penetrating rounds less deformed, thin armor will impart less deformation before failing
                if (impactVel > 1250) //too fast and steel/lead begin to melt on impact - hence DU/Tungsten hypervelocity penetrators
                {
                    newCaliber *= (impactVel / 1250);
                }
                newCaliber = Mathf.Clamp(newCaliber, 1f, 5f);
                //replace this with tensile srength of bullet calcs?
                if (BDArmorySettings.DRAW_ARMOR_LABELS)
                {
                    Debug.Log("[BDArmory.ProjectileUtils{Calc Deformation}]: Bullet Deformation modifier " + newCaliber);
                }
                newCaliber *= caliber;
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.ProjectileUtils{Calc Deformation}]: bullet now " + (newCaliber) + " mm");
                return newCaliber;
            }
        }
        public static bool CalculateBulletStatus(float projMass, float newCaliber, bool sabot = false)
        {
            //does the bullet suvive its impact?
            //calculate bullet lengh, in mm
            float density = 11.34f;
            if (sabot)
            {
                density = 19;
            }
            float bulletLength = ((projMass * 1000) / ((newCaliber * newCaliber * Mathf.PI / 400) * density) + 1) * 10; //srf.Area in mmm2 x density of lead to get mass per 1 cm length of bullet / total mass to get total length,
                                                                                                                        //+ 10 to accound for ogive/mushroom head post-deformation instead of perfect cylinder
            if (newCaliber > (bulletLength * 2)) //has the bullet flattened into a disc, and is no longer a viable penetrator?
            {
                if (BDArmorySettings.DRAW_ARMOR_LABELS)
                {
                    Debug.Log("[BDArmory.ProjectileUtils]: Bullet deformed past usable limit");
                }
                return false;
            }
            else return true;
        }

        public static float CalculatePenetration(float caliber, float newCaliber, float projMass, float impactVel, float Ductility, float Density, float Strength, float thickness, float APmod, bool sabot = false)
        {
            float Energy = CalculateProjectileEnergy(projMass, impactVel);
            if (thickness < 1)
            {
                thickness = 1; //prevent divide by zero or other odd behavior
            }
            //the harder the material, the more the bullet is deformed, and the more energy it needs to expend to deform the armor
            float penetration;
            //bullet's deformed, penetration using larger crosssection

            {
                //caliber in mm, converted to length in cm, converted to mm
                float length = ((projMass * 1000) / ((newCaliber * newCaliber * Mathf.PI / 400) * (sabot ? 19 : 11.34f)) + 1) * 10;
                //if (impactVel > 1500)
                //penetration = length * Mathf.Sqrt((sabot ? 19000 : 11340) / Density); //at hypervelocity, impacts are akin to fluid displacement
                //penetration in mm

                var modifiedCaliber = (0.5f * caliber) + (0.5f * newCaliber) * (2f * Ductility * Ductility);
                float yieldStrength = modifiedCaliber * modifiedCaliber * Mathf.PI / 100f * Strength * (Density / 7850f) * thickness;
                if (Ductility > 0.25f) //up to a point, anyway. Stretch too much...
                {
                    yieldStrength *= 0.7f; //necking and point embrittlement reduce total tensile strength of material
                }
                penetration = Mathf.Min(((Energy / yieldStrength) * thickness * APmod), length * Mathf.Sqrt((sabot ? 19000 : 11340) / Density));
                //cap penetration to max possible pen depth from hypervelocity impact
            } //penetration in mm
            //apparently shattered projectiles add 30% to armor thickness; oblique impact beyond 55deg decreases effective thickness(splatted projectile digs in to plate instead of richochets)

            if (BDArmorySettings.DRAW_ARMOR_LABELS)
            {
                Debug.Log("[BDArmory.ProjectileUtils{Calc Penetration}]: Energy: " + Energy + "; caliber: " + caliber + "; newCaliber: " + newCaliber);
                Debug.Log("[BDArmory.ProjectileUtils{Calc Penetration}]: Ductility:" + Ductility + "; Density: " + Density + "; Strength: " + Strength + "; thickness: " + thickness);
                Debug.Log("[BDArmory.ProjectileUtils{Calc Penetration}]: Penetration: " + Mathf.Round(penetration / 10) + " cm");
            }
            return penetration;
        }

        public static float CalculateThickness(Part hitPart, float anglemultiplier)
        {
            float thickness = (float)hitPart.GetArmorThickness(); //return mm
            return Mathf.Max(thickness / anglemultiplier, 1);
        }
        public static bool CheckGroundHit(Part hitPart, RaycastHit hit, float caliber)
        {
            if (hitPart == null)
            {
                if (BDArmorySettings.BULLET_HITS)
                {
                    BulletHitFX.CreateBulletHit(hitPart, hit.point, hit, hit.normal, true, caliber, 0, null);
                }

                return true;
            }
            return false;
        }
        public static bool CheckBuildingHit(RaycastHit hit, float projMass, Vector3 currentVelocity, float DmgMult)
        {
            DestructibleBuilding building = null;
            try
            {
                building = hit.collider.gameObject.GetComponentUpwards<DestructibleBuilding>();
                if (building != null)
                    building.damageDecay = 600f;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BDArmory.ProjectileUtils]: Exception thrown in CheckBuildingHit: " + e.Message + "\n" + e.StackTrace);
            }

            if (building != null && building.IsIntact)
            {
                float damageToBuilding = ((0.5f * (projMass * (currentVelocity.magnitude * currentVelocity.magnitude)))
                            * (BDArmorySettings.DMG_MULTIPLIER / 100) * DmgMult
                            * 1e-4f);
                damageToBuilding /= 8f;
                building.AddDamage(damageToBuilding);
                if (building.Damage > building.impactMomentumThreshold * 150)
                {
                    building.Demolish();
                }
                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                    Debug.Log("[BDArmory.ProjectileUtils]: Ballistic hit destructible building! Hitpoints Applied: " + Mathf.Round(damageToBuilding) +
                             ", Building Damage : " + Mathf.Round(building.Damage) +
                             " Building Threshold : " + building.impactMomentumThreshold);

                return true;
            }
            return false;
        }

        public static void CheckPartForExplosion(Part hitPart)
        {
            if (!hitPart.FindModuleImplementing<HitpointTracker>()) return;

            switch (hitPart.GetExplodeMode())
            {
                case "Always":
                    CreateExplosion(hitPart);
                    break;

                case "Dynamic":
                    float probability = CalculateExplosionProbability(hitPart);
                    if (probability >= 3)
                        CreateExplosion(hitPart);
                    break;

                case "Never":
                    break;
            }
        }

        public static float CalculateExplosionProbability(Part part)
        {
            ///////////////////////////////////////////////////////////////
            float probability = 0;
            float fuelPct = 0;
            for (int i = 0; i < part.Resources.Count; i++)
            {
                PartResource current = part.Resources[i];
                switch (current.resourceName)
                {
                    case "LiquidFuel":
                        fuelPct = (float)(current.amount / current.maxAmount);
                        break;
                        //case "Oxidizer":
                        //   probability += (float) (current.amount/current.maxAmount);
                        //    break;
                }
            }

            if (fuelPct > 0 && fuelPct <= 0.60f)
            {
                probability = Core.Utils.BDAMath.RangedProbability(new[] { 50f, 25f, 20f, 5f });
            }
            else
            {
                probability = Core.Utils.BDAMath.RangedProbability(new[] { 50f, 25f, 20f, 2f });
            }

            if (fuelPct == 1f || fuelPct == 0f)
                probability = 0f;

            if (BDArmorySettings.DRAW_DEBUG_LABELS)
            {
                Debug.Log("[BDArmory.ProjectileUtils]: Explosive Probablitliy " + probability);
            }

            return probability;
        }

        public static void CreateExplosion(Part part) //REVIEW - remove/only activate if BattleDaamge fire disabled?
        {
            float explodeScale = 0;
            IEnumerator<PartResource> resources = part.Resources.GetEnumerator();
            while (resources.MoveNext())
            {
                if (resources.Current == null) continue;
                switch (resources.Current.resourceName)
                {
                    case "LiquidFuel":
                        explodeScale += (float)resources.Current.amount;
                        break;

                    case "Oxidizer":
                        explodeScale += (float)resources.Current.amount;
                        break;
                }
            }

            if (BDArmorySettings.DRAW_DEBUG_LABELS)
            {
                Debug.Log("[BDArmory.ProjectileUtils]: Penetration of bullet detonated fuel!");
            }

            resources.Dispose();

            explodeScale /= 100;
            part.explosionPotential = explodeScale;

            PartExploderSystem.AddPartToExplode(part);
        }

        private class PriorityQueue
        {
            private Dictionary<int, List<PartResource>> partResources = new Dictionary<int, List<PartResource>>();

            public PriorityQueue(HashSet<PartResource> elements)
            {
                foreach (PartResource r in elements)
                {
                    Add(r);
                }
            }

            public void Add(PartResource r)
            {
                int key = r.part.resourcePriorityOffset;
                if (partResources.ContainsKey(key))
                {
                    List<PartResource> existing = partResources[key];
                    existing.Add(r);
                    partResources[key] = existing;
                }
                else
                {
                    List<PartResource> newList = new List<PartResource>();
                    newList.Add(r);
                    partResources.Add(key, newList);
                }
            }

            public List<PartResource> Pop()
            {
                if (partResources.Count == 0)
                {
                    return new List<PartResource>();
                }
                int key = partResources.Keys.Max();
                List<PartResource> result = partResources[key];
                partResources.Remove(key);
                return result;
            }

            public bool HasNext()
            {
                return partResources.Count != 0;
            }
        }

        private static void StealResource(Vessel src, Vessel dst, HashSet<string> resourceNames, double ration, bool integerAmounts = false)
        {
            // identify all parts on source vessel with resource
            Dictionary<string, HashSet<PartResource>> srcParts = new Dictionary<string, HashSet<PartResource>>();
            DeepFind(src.rootPart, resourceNames, srcParts);

            // identify all parts on destination vessel with resource
            Dictionary<string, HashSet<PartResource>> dstParts = new Dictionary<string, HashSet<PartResource>>();
            DeepFind(dst.rootPart, resourceNames, dstParts);

            foreach (var resourceName in resourceNames)
            {
                if (!srcParts.ContainsKey(resourceName) || !dstParts.ContainsKey(resourceName))
                {
                    // if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log(string.Format("[BDArmory.ProjectileUtils]: Steal resource {0} failed; no parts.", resourceName));
                    continue;
                }

                double remainingAmount = srcParts[resourceName].Sum(p => p.amount);
                if (integerAmounts)
                {
                    remainingAmount = Math.Floor(remainingAmount);
                    if (remainingAmount == 0) continue; // Nothing left to steal.
                }
                double amount = remainingAmount * ration;
                if (integerAmounts) { amount = Math.Ceiling(amount); } // Round up steal amount so that something is always stolen if there's something to steal.
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.ProjectileUtils]: " + dst.vesselName + " is trying to steal " + amount.ToString("F1") + " of " + resourceName + " from " + src.vesselName);

                // transfer resource from src->dst parts, honoring their priorities
                PriorityQueue sourceQueue = new PriorityQueue(srcParts[resourceName]);
                PriorityQueue destinationQueue = new PriorityQueue(dstParts[resourceName]);
                List<PartResource> sources = null, destinations = null;
                double tolerance = 1e-3;
                double amountTaken = 0;
                while (amount - amountTaken >= (integerAmounts ? 1d : tolerance))
                {
                    if (sources == null)
                    {
                        sources = sourceQueue.Pop();
                        if (sources.Count() == 0) break;
                    }
                    if (destinations == null)
                    {
                        destinations = destinationQueue.Pop();
                        if (destinations.Count() == 0) break;
                    }
                    var availability = sources.Where(e => e.amount >= tolerance / sources.Count()); // All source parts with something in.
                    var opportunity = destinations.Where(e => e.maxAmount - e.amount >= tolerance / destinations.Count()); // All destination parts with room to spare.
                    if (availability.Count() == 0) { sources = null; }
                    if (opportunity.Count() == 0) { destinations = null; }
                    if (sources == null || destinations == null) continue;
                    if (integerAmounts)
                    {
                        if (availability.Sum(e => e.amount) < 1d) { sources = null; }
                        if (opportunity.Sum(e => e.maxAmount - e.amount) < 1d) { destinations = null; }
                        if (sources == null || destinations == null) continue;
                    }
                    var minFractionAvailable = availability.Min(r => r.amount / r.maxAmount); // Minimum fraction of container size available for transfer.
                    var minFractionOpportunity = opportunity.Min(r => (r.maxAmount - r.amount) / r.maxAmount); // Minimum fraction of container size available to fill a part.
                    var totalTransferAvailable = availability.Sum(r => r.maxAmount * minFractionAvailable);
                    var totalTransferOpportunity = opportunity.Sum(r => r.maxAmount * minFractionOpportunity);
                    var totalTransfer = Math.Min(amount, Math.Min(totalTransferAvailable, totalTransferOpportunity)); // Total amount to transfer that either transfers the amount required, empties a container or fills a container.
                    if (integerAmounts) { totalTransfer = Math.Floor(totalTransfer); }
                    var totalContainerSizeAvailable = availability.Sum(r => r.maxAmount);
                    var totalContainerSizeOpportunity = opportunity.Sum(r => r.maxAmount);
                    var transferFractionAvailable = totalTransfer / totalContainerSizeAvailable;
                    var transferFractionOpportunity = totalTransfer / totalContainerSizeOpportunity;

                    if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log($"[BDArmory.ProjectileUtils]: Transferring {totalTransfer:F1} of {resourceName} from {string.Join(", ", availability.Select(a => $"{a.part.name} ({a.amount:F1}/{a.maxAmount:F1})").ToList())} on {src.vesselName} to {string.Join(", ", opportunity.Select(o => $"{o.part.name} ({o.amount:F1}/{o.maxAmount:F1})").ToList())} on {dst.vesselName}");
                    // Transfer directly between parts doesn't seem to be working properly (it leaves the source, but doesn't arrive at the destination).
                    var measuredOut = 0d;
                    var measuredIn = 0d;
                    foreach (var sourceResource in availability)
                    { measuredOut += sourceResource.part.TransferResource(sourceResource.info.id, -transferFractionAvailable * sourceResource.maxAmount); }
                    foreach (var destinationResource in opportunity)
                    { measuredIn += -destinationResource.part.TransferResource(destinationResource.info.id, transferFractionOpportunity * destinationResource.maxAmount); }
                    if (Math.Abs(measuredIn - measuredOut) > tolerance)
                    { Debug.LogWarning($"[BDArmory.ProjectileUtils]: Discrepancy in the amount of {resourceName} transferred from {string.Join(", ", availability.Select(r => r.part.name))} ({measuredOut:F3}) to {string.Join(", ", opportunity.Select(r => r.part.name))} ({measuredIn:F3})"); }

                    amountTaken += totalTransfer;
                    if (totalTransfer < tolerance)
                    {
                        Debug.LogWarning($"[BDArmory.ProjectileUtils]: totalTransfer was {totalTransfer} for resource {resourceName}, amount: {amount}, availability: {string.Join(", ", availability.Select(r => r.amount))}, opportunity: {string.Join(", ", opportunity.Select(r => r.maxAmount - r.amount))}");
                        if (availability.Sum(r => r.amount) < opportunity.Sum(r => r.maxAmount - r.amount)) { sources = null; } else { destinations = null; }
                    }
                }
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log($"[BDArmory.ProjectileUtils]: Final amount of {resourceName} stolen: {amountTaken:F1}");
            }
        }

        private class ResourceAllocation
        {
            public PartResource sourceResource;
            public Part destPart;
            public double amount;
            public ResourceAllocation(PartResource r, Part p, double a)
            {
                this.sourceResource = r;
                this.destPart = p;
                this.amount = a;
            }
        }

        private static void DeepFind(Part p, HashSet<string> resourceNames, Dictionary<string, HashSet<PartResource>> accumulator)
        {
            foreach (PartResource r in p.Resources)
            {
                if (resourceNames.Contains(r.resourceName))
                {
                    if (!accumulator.ContainsKey(r.resourceName))
                        accumulator[r.resourceName] = new HashSet<PartResource>();
                    accumulator[r.resourceName].Add(r);
                }
            }
            foreach (Part child in p.children)
            {
                DeepFind(child, resourceNames, accumulator);
            }
        }
    }
}
