using BDArmory.Competition;
using BDArmory.Control;
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
                // Debug.Log("[BDArmory.ProjectileUtils]: Weapon from " + aName + " damaged " + tName);

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

                // steal resources if enabled
                if (BDArmorySettings.RESOURCE_STEAL_ENABLED)
                {
                    if (BDArmorySettings.RESOURCE_STEAL_FUEL_RATION > 0f) StealResource(hitPart.vessel, sourceVessel, FuelResources, BDArmorySettings.RESOURCE_STEAL_FUEL_RATION);
                    if (BDArmorySettings.RESOURCE_STEAL_AMMO_RATION > 0f) StealResource(hitPart.vessel, sourceVessel, AmmoResources, BDArmorySettings.RESOURCE_STEAL_AMMO_RATION, true);
                    if (BDArmorySettings.RESOURCE_STEAL_CM_RATION > 0f) StealResource(hitPart.vessel, sourceVessel, CMResources, BDArmorySettings.RESOURCE_STEAL_CM_RATION, true);
                }
            }
        }
        public static float CalculateArmorPenetration(Part hitPart, float anglemultiplier, RaycastHit hit, float penetration, float thickness, float caliber)
        {
            ///////////////////////////////////////////////////////////////////////
            // Armor Penetration
            ///////////////////////////////////////////////////////////////////////

            //TODO: Extract bdarmory settings from this values
            if (thickness < 1) thickness = 1; //prevent divide by zero or other odd behavior

            var penetrationFactor = penetration / thickness;

            if (BDArmorySettings.DRAW_DEBUG_LABELS)
            {
                Debug.Log("[BDArmory.ProjectileUtils]: Armor penetration = " + penetration + " | Thickness = " + thickness);
            }

            bool fullyPenetrated = penetration > thickness; //check whether bullet penetrates the plate

            double massToReduce = Math.PI * Math.Pow((caliber * 0.001) / 2, 2) * (penetration);

            if (!fullyPenetrated)
            {
                massToReduce *= 0.125f;

                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                {
                    Debug.Log("[BDArmory.ProjectileUtils]: Bullet Stopped by Armor");
                }
            }
            hitPart.ReduceArmor(massToReduce);
            return penetrationFactor;
        }
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
