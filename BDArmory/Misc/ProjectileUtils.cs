using BDArmory.Competition;
using BDArmory.Control;
using BDArmory.Core;
using BDArmory.Core.Extension;
using BDArmory.Core.Module;
using BDArmory.FX;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BDArmory.Misc
{
    class ProjectileUtils
    {
        public static void ApplyDamage(Part hitPart, RaycastHit hit, float multiplier, float penetrationfactor, float caliber, float projmass, float impactVelocity, float DmgMult, double distanceTraveled, bool explosive, bool hasRichocheted, Vessel sourceVessel, string name)
        {
            //hitting a vessel Part
            //No struts, they cause weird bugs :) -BahamutoD
            if (hitPart == null) return;
            if (hitPart.partInfo.name.Contains("Strut")) return;

            // Add decals
            if (BDArmorySettings.BULLET_HITS)
            {
                BulletHitFX.CreateBulletHit(hitPart, hit.point, hit, hit.normal, hasRichocheted, caliber, penetrationfactor);
            }
            // Apply damage
            float damage;
            damage = hitPart.AddBallisticDamage(projmass, caliber, multiplier, penetrationfactor, DmgMult, impactVelocity);

            if (BDArmorySettings.BATTLEDAMAGE)
            {
                BattleDamageHandler.CheckDamageFX(hitPart, caliber, penetrationfactor, explosive, sourceVessel.GetName(), hit);
            }
            // Debug.Log("DEBUG Ballistic damage to " + hitPart + ": " + damage + ", calibre: " + caliber + ", multiplier: " + multiplier + ", pen: " + penetrationfactor);

            // Update scoring structures
            ApplyScore(hitPart, sourceVessel, distanceTraveled, damage, name);
        }
        public static void ApplyScore(Part hitPart, Vessel sourceVessel, double distanceTraveled, float damage, string name)
        {
            var aName = sourceVessel.GetName();
            var tName = hitPart.vessel.GetName();

            if (aName != null && tName != null && aName != tName && BDACompetitionMode.Instance.Scores.ContainsKey(aName) && BDACompetitionMode.Instance.Scores.ContainsKey(tName))
            {
                //Debug.Log("[BDArmory.ProjectileUtils]: Weapon from " + aName + " damaged " + tName);

                if (BDArmorySettings.REMOTE_LOGGING_ENABLED)
                {
                    BDAScoreService.Instance.TrackHit(aName, tName, name, distanceTraveled);
                    BDAScoreService.Instance.TrackDamage(aName, tName, damage);
                }
                // update scoring structure on attacker
                {
                    var aData = BDACompetitionMode.Instance.Scores[aName];
                    aData.Score += 1;
                    // keep track of who shot who for point keeping

                    // competition logic for 'Pinata' mode - this means a pilot can't be named 'Pinata'
                    if (hitPart.vessel.GetName() == "Pinata")
                    {
                        aData.PinataHits++;
                    }
                }
                // update scoring structure on the defender.
                {
                    var tData = BDACompetitionMode.Instance.Scores[tName];
                    tData.lastPersonWhoHitMe = aName;
                    tData.lastHitTime = Planetarium.GetUniversalTime();
                    tData.everyoneWhoHitMe.Add(aName);
                    // Track hits
                    if (tData.hitCounts.ContainsKey(aName))
                        ++tData.hitCounts[aName];
                    else
                        tData.hitCounts.Add(aName, 1);
                    // Track damage
                    if (tData.damageFromBullets.ContainsKey(aName))
                        tData.damageFromBullets[aName] += damage;
                    else
                        tData.damageFromBullets.Add(aName, damage);
                }
            }
        }

        public static float CalculateArmorPenetration(Part hitPart, float penetration)
        {
            ///////////////////////////////////////////////////////////////////////
            // Armor Penetration
            ///////////////////////////////////////////////////////////////////////

            float thickness = (float)hitPart.GetArmorThickness();
			
            var penetrationFactor = penetration / thickness; 

            if (BDArmorySettings.DRAW_DEBUG_LABELS)
            {
                Debug.Log("[BDArmory.ProjectileUtils]: Armor penetration = " + penetration + " | Thickness = " + thickness);
            }
            if (penetrationFactor < 1)
            {
                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                {
                    Debug.Log("[BDArmory.ProjectileUtils]: Bullet Stopped by Armor");
                }
            }
            return penetrationFactor;
        }
        public static void CalculateArmorDamage(Part hitPart, float penetrationFactor, float caliber, float hardness, float ductility, float density, float impactVel, Vessel sourceVessel)
        {
            float thickness = (float)hitPart.GetArmorThickness();
            double volumeToReduce = -1;
            float caliberModifier = 1; //how many calibers wide is the armor loss/spall?       
            float spallMass = 0;
            float spallCaliber = 1;
            //Spalling/Armor damage
            if (ductility > 0.20f)
            {
                if (penetrationFactor > 2) //proj fast material can't stretch fast enough, necking/point embrittlelment/etc, material tears
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
                    spallMass = Mathf.Pow(0.5f * spallCaliber, 2) * Mathf.PI / 1000 * thickness * (density/1000000);
                    if (BDArmorySettings.DRAW_DEBUG_LABELS)
                    {
                        Debug.Log("[BDArmory.ProjectileUtils]: Armor spalling! Diameter: " + spallCaliber + "; mass: " + spallMass + "g");
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
                            volumeToReduce = (Mathf.Pow(Mathf.CeilToInt(caliber / 100), 2) * 100 * thickness);
                            //total failue of 10x10cm armor tile(s)
                            if (BDArmorySettings.DRAW_DEBUG_LABELS)
                            {
                                Debug.Log("[BDArmory.ProjectileUtils]: Armor failure!");
                            }
                        }
                        else; //0.05-0.19 ductility - harder steels, etc
                        {
                            caliberModifier = 2 + (20 / ductility * 10) * penetrationFactor;
                        }
                    }
                    if (penetrationFactor > 0.66 && penetrationFactor < 1)
                    {
                        spallCaliber = ((1 - penetrationFactor) + 1) * (Mathf.Pow(0.5f * caliber, 2) * Mathf.PI);
                        volumeToReduce = spallCaliber;
                        spallMass = spallCaliber * (density / 1000000000);
                        if (BDArmorySettings.DRAW_DEBUG_LABELS)
                        {
                            Debug.Log("[BDArmory.ProjectileUtils]: Armor failure!");
                            Debug.Log("[BDArmory.ProjectileUtils]: Armor spalling! Diameter: " + spallCaliber + "; mass: " + spallMass +"g");
                        }
                    }
                }
                //else //low hardness non ductile materials (i.e. kevlar/aramid) not going to spall
            }

            if (volumeToReduce < 0)
            {
                volumeToReduce = Mathf.Pow((0.5f * caliber * caliberModifier), 2) * Mathf.PI / 100 * thickness;
            }
            if (BDArmorySettings.DRAW_DEBUG_LABELS)
            {
                Debug.Log("[BDArmory.ProjectileUtils]: Armor volume lost: " + volumeToReduce + " cm3");
            }
            hitPart.ReduceArmor((double)volumeToReduce);
            if (penetrationFactor < 1)
            {
                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                {
                    Debug.Log("[BDArmory.ProjectileUtils]: Bullet Stopped by Armor");
                }
            }
            if (spallMass > 0)
            {
                float damage = hitPart.AddBallisticDamage(spallMass, spallCaliber, 1, 1.1f, 1, (impactVel / 3));
                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                {
                    Debug.Log("[BDArmory.ProjectileUtils]: Spall damage: "+damage);
                }
                ApplyScore(hitPart, sourceVessel, 1, damage, "Spall Damage");
            }
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
            if (BDArmorySettings.DRAW_DEBUG_LABELS)
            {
                Debug.Log("[BDArmory.ProjectileUtils]: Bullet Energy: " + bulletEnergy);
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
            if (BDArmorySettings.DRAW_DEBUG_LABELS)
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
            yieldStrength = Mathf.Pow(((0.5f * caliber) + ((0.5f * caliber) * (2 * (Ductility * Ductility)))), 2) * Mathf.PI / 100 * Strength * (Density / 7850) * thickness;
            //assumes bullet is perfect cyl, modded by ductility spreading impact over larger area, times strength/cm2 for threshold energy required to penetrate armor material
            // Ductility is a measure of brittleness, the lower the brittleness, the more the material is willing to bend before fracturing, allowing energy to be spread over more area
            if (Ductility > 0.25f) //up to a point, anyway. Stretch too much... 
            {
                yieldStrength *= 0.7f; //necking and point embrittlement reduce total tensile strength of material
            }
            if (hitpart.skinTemperature > SafeTemp) //has the armor started melting/denaturing/whatever?
            {
                yieldStrength *= 0.75f;
            }
            if (BDArmorySettings.DRAW_DEBUG_LABELS)
            {
                Debug.Log("[BDArmory.ProjectileUtils]: Armor yield Strength: " + yieldStrength);
            }

            return yieldStrength;
        }

        public static float CalculateDeformation(float yieldStrength, float bulletEnergy, float caliber, float impactVel, float hardness, float apBulletMod, float Density)
        {
            if (bulletEnergy < yieldStrength) return caliber; //armor stops the round, but calc armor damage
            else //bullet penetrates. Calculate what happens to the bullet
            {
                //deform bullet from impact
                if (yieldStrength < 1) yieldStrength = 1000;
                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                {
                    Debug.Log("[BDArmory.ProjectileUtils]: properties: yield:" + yieldStrength + "; Energy: " + bulletEnergy  + "; caliber: " + caliber + "; impactVel: " + impactVel);
                    Debug.Log("[BDArmory.ProjectileUtils]: properties: hardness:" + hardness + "; apBulletMod: " + apBulletMod + "; density: " + Density);
                }
                float newCaliber = ((((yieldStrength / bulletEnergy) * (hardness* Mathf.Sqrt(Density/1000))) / impactVel) / apBulletMod); //faster penetrating rounds less deformed, thin armor will impart less deformation before failing
                newCaliber = Mathf.Clamp(newCaliber, 1f, 5f);
                if (impactVel > 1250) //too fast and steel/lead begin to melt on impact - hence DU hypervelocity penetrators
                {
                    newCaliber *= (impactVel / 1000);
                }
                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                {
                    Debug.Log("[BDArmory.ProjectileUtils]: Bullet Deformation modifier " + newCaliber);
                }
                newCaliber *= caliber;
                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                {
                    Debug.Log("[BDArmory.ProjectileUtils]: Bullet Deformation; bullet now " + newCaliber + " mm");
                }
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
            float bulletLength = (projMass*1000) / (Mathf.Pow(0.5f * newCaliber, 2) * Mathf.PI / 1000 * density) + 10; //srf.Area in mmm2 x density of lead to get mass per 1 cm length of bullet / total mass to get total length,
                                                                                                                                       //+ 10 to accound for ogive/mushroom head post-deformation instead of perfect cylinder
            if (newCaliber > (bulletLength * 2)) //has the bullet flattened into a disc, and is no longer a viable penetrator?
            {
                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                {
                    Debug.Log("[BDArmory.ProjectileUtils]: Bullet deformed past usable limit");
                }
                return false;
            }
            else return true;
        }

        public static float CalculatePenetration(float caliber, float newCaliber, float projMass, float impactVel, float Ductility, float Density, float Strength, float thickness)
        {

            float Energy = CalculateProjectileEnergy(projMass, impactVel);
            //the harder the material, the more the bullet is deformed, and the more energy it needs to expend to deform the armor 
            float penetration;
            //bullet's deformed, penetration using larger crosssection  
            if (impactVel > 1500 && caliber < 30) //hypervelocity KE penetrators, for convenience, assume any round moving this fast is made of Tungsten/Depleted ranium
            {
                float length = (projMass * 1000) / (Mathf.Pow(0.5f * newCaliber, 2) * Mathf.PI / 1000 * 19) + 10;
                penetration = length * Mathf.Sqrt(19000 / Density); //at hypervelocity, impacts are akin to fluid displacement 
            }
            else
            {
                penetration = Energy / (Mathf.Pow(((0.5f * caliber) + ((0.5f * newCaliber) * (2 * (Ductility * Ductility)))), 2) * Mathf.PI / 100 * Strength * (Density / 7850) * thickness);
            }
            penetration *= 10; //convert from cm to mm
            if (BDArmorySettings.DRAW_DEBUG_LABELS)
            {
                Debug.Log("[BDArmory.ProjectileUtils]: Properties: Energy: " + Energy + "; caliber: " + caliber + "; newCaliber: " + newCaliber);
                Debug.Log("[BDArmory.ProjectileUtils]: Ductility:" + Ductility + "; Density: " + Density + "; Strength: " + Strength + "; thickness: " + thickness);
                Debug.Log("[BDArmory.ProjectileUtils]: Penetration: " + penetration + " cm");
            }
            return penetration;
        }
        public static float CalculateThickness(Part hitPart, float anglemultiplier)
        {
            float thickness = (float)hitPart.GetArmorThickness();
            return Mathf.Max(thickness / anglemultiplier, 1);
        }
        public static bool CheckGroundHit(Part hitPart, RaycastHit hit, float caliber)
        {
            if (hitPart == null)
            {
                if (BDArmorySettings.BULLET_HITS)
                {
                    BulletHitFX.CreateBulletHit(hitPart, hit.point, hit, hit.normal, true, caliber, 0);
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
                float damageToBuilding = ((0.5f * (projMass * Mathf.Pow(currentVelocity.magnitude, 2)))
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

        public static void CreateExplosion(Part part)
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
    }
}
