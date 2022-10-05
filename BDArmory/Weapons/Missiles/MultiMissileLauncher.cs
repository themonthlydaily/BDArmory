using BDArmory.Competition;
using BDArmory.Control;
using BDArmory.Radar;
using BDArmory.Settings;
using BDArmory.Targeting;
using BDArmory.UI;
using BDArmory.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static BDArmory.Weapons.Missiles.MissileBase;

namespace BDArmory.Weapons.Missiles
{
    /// <summary>
    /// Add-on Module to MissileLauncher to extend Launcher functionality to include cluster missiles and multi-missile pods
    /// </summary>

    public class MultiMissileLauncher : PartModule
	{
        public static Dictionary<string, ObjectPool> mslDummyPool = new Dictionary<string, ObjectPool>();
        Coroutine missileSalvo;

        Transform[] launchTransforms;
        [KSPField] public string subMunitionName; //name of missile in .cfg - e.g. "bahaAim120"
        [KSPField] public string subMunitionPath; //model path for missile
        [KSPField] public string launchTransformName; //name of transform launcTransforms are parented to - see Rocketlauncher transform hierarchy
        [KSPField] public int salvoSize = 1; //leave blank to have salvoSize = launchTransforms.count
        [KSPField] public bool isClusterMissile = false; //cluster submunitions deployed instead of standard detonation? Fold this into warHeadType?
        [KSPField] public bool isMultiLauncher = false; //is this a multimissile pod?
        [KSPField] public bool useSymCounterpart = false; //have symmetrically placed parts fire along with this part as part of salvo? Requires isMultMissileLauncher = true;
        [KSPField] public bool overrideReferenceTransform = false; //override the missileReferenceTransform in Missilelauncher to use vessel prograde
        [KSPField] public float rippleRPM = 650;
        [KSPField] public string deployAnimationName;
        [KSPField] public string RailNode = "rail"; //name of attachnode for VLS MMLs to set missile loadout
        AnimationState deployState;
        ModuleMissileRearm missileSpawner = null;
        MissileLauncher missileLauncher = null;
        MissileFire wpm = null;
        private int tubesFired = 0;
        Part SymPart = null;
        public BDTeam Team { get; set; } = BDTeam.Get("Neutral");
        public void Start()
        {
            MakeMissileArray();
            //List<MissileDummy> missileDummies = new List<MissileDummy>();
            missileLauncher = part.FindModuleImplementing<MissileLauncher>();
            missileSpawner = missileLauncher.reloadableRail;
            if (missileSpawner == null) //MultiMissile launchers/cluster missiles need a MMR module for spawning their submunitions, so add one if not present
            {
                missileSpawner = (ModuleMissileRearm)part.AddModule("ModuleMissileRearm");
                missileSpawner.maxAmmo = salvoSize = 10;
                Debug.LogError($"[BDArmory.MultiMissileLauncher] no ModuleMissileRearm on {part.name}. Please fix your .cfg");
                missileLauncher.reloadableRail = missileSpawner;
                missileLauncher.hasAmmo = true;
            }
            if (!isClusterMissile) missileSpawner.ammoCount = salvoSize;
            if (!string.IsNullOrEmpty(deployAnimationName))
            {
                deployState = GUIUtils.SetUpSingleAnimation(deployAnimationName, part);
                if (deployState != null)
                {
                    deployState.normalizedTime = 0;
                    deployState.speed = 0;
                    deployState.enabled = true;
                }
            }
            GameEvents.onEditorShipModified.Add(ShipModified);
            GameEvents.onPartDie.Add(OnPartDie);
            if (useSymCounterpart)
            {
                //find sym part, find sym.MissileLauncher, check symML !HasFired, sym ML.FireMissile()
                using (List<Part>.Enumerator pSym = part.symmetryCounterparts.GetEnumerator())
                    while (pSym.MoveNext())
                    {
                        if (pSym.Current == null) continue;
                        if (pSym.Current != part && pSym.Current.vessel == vessel)
                        {
                            SymPart = pSym.Current;
                        }
                    }                
            }
            wpm = VesselModuleRegistry.GetMissileFire(missileLauncher.vessel, true);
        }
        private void OnDestroy()
        {
            GameEvents.onEditorShipModified.Remove(ShipModified);
            GameEvents.onPartDie.Remove(OnPartDie);
        }

        void OnPartDie() { OnPartDie(part); }

        void OnPartDie(Part p)
        {
            if (p == part)
            {
                foreach (var existingDummy in part.GetComponents<MissileDummy>())
                {
                    existingDummy.Deactivate();
                }
            }
        }

        public void ShipModified(ShipConstruct data)
        {
            if (part.children.Count > 0)
            {
                using (List<AttachNode>.Enumerator stackNode = part.attachNodes.GetEnumerator())
                    while (stackNode.MoveNext())
                    {
                        if (stackNode.Current?.nodeType != AttachNode.NodeType.Stack) continue;
                        if (stackNode.Current.id != RailNode) continue;
                        {
                            if (stackNode.Current.attachedPart is Part missile)
                            {
                                if (missile == null) return;

                                if (missile.FindModuleImplementing<MissileLauncher>()) 
                                {
                                    subMunitionName = missile.name;
                                    Debug.Log($"[BDArmory.MultiMissileLauncher] new submunition name: {subMunitionName}"); //find out what is throwing an error, NRE
                                    subMunitionPath = GetMeshurl((UrlDir.UrlConfig)GameDatabase.Instance.root.GetConfigs(missile.partInfo.partConfig.name)); //might be easier to mess around with MeshFilter instead?
                                    PopulateMissileDummies(true);
                                }
                            }
                        }
                    }
            }
        }
        private string GetMeshurl(UrlDir.UrlConfig cfgdir)
        {
            string mesh = "model";
            //in case the mesh is not model.mu
            if (cfgdir.config.HasValue("mesh"))
            {
                mesh = cfgdir.config.GetValue("mesh");
                char[] sep = { '.' };
                string[] words = mesh.Split(sep);
                mesh = words[0];
            }
            string filename = string.Format("{0}" + "/" + "{1}.mu", cfgdir.parent.parent.path, mesh);
            string url = string.Format("{0}/{1}", cfgdir.parent.parent.url, mesh);
            Debug.Log($"[BDArmory.MultiMissileLauncher] Found model URL of {url}");
            if (!System.IO.File.Exists(filename))
            {
                string[] files = System.IO.Directory.GetFiles(cfgdir.parent.parent.path, "*.mu");
                if (files.Length != 0)
                {
                    files[0] = files[0].Remove(0, cfgdir.parent.parent.path.Length);
                    char[] sep = { '\\', '.', '/' };
                    string[] words = files[0].Split(sep);
                    url = url.Substring(0, url.LastIndexOf('/') + 1) + words[1];
                    Debug.Log($"[BDArmory.MultiMissileLauncher] Original URL not found! Using {url}");
                }
                else
                {
                    Debug.LogWarning("No mesh found, using default");
                }
            }
            return url;
        }

        void MakeMissileArray()
        {
            Transform launchTransform = part.FindModelTransform(launchTransformName);
            int missileNum = launchTransform.childCount;
            launchTransforms = new Transform[missileNum];
            for (int i = 0; i < missileNum; i++)
            {
                string launcherName = launchTransform.GetChild(i).name;
                int launcherIndex = int.Parse(launcherName.Substring(7)) - 1; //by coincidence, this is the same offset as rocket pods, which means the existing rocketlaunchers could potentially be converted over to homing munitions...

                launchTransforms[launcherIndex] = launchTransform.GetChild(i);
            }
            salvoSize = Mathf.Min(salvoSize, launchTransforms.Length);
            if (subMunitionPath != "")
            {
                PopulateMissileDummies(true);
            }
        }
        public void PopulateMissileDummies(bool refresh = false)
        {
            SetupMissileDummyPool(subMunitionPath);
            if (refresh)
            {
                foreach (var existingDummy in part.GetComponents<MissileDummy>())
                {
                    existingDummy.Deactivate(); //if changing out missiles loaded into a VLS or similar, reset missile dummies
                }
            }
            for (int i = 0; i < launchTransforms.Length; i++)
            {
                if (refresh)
                {
                    GameObject dummy = mslDummyPool[subMunitionPath].GetPooledObject();
                    MissileDummy dummyThis = dummy.GetComponentInChildren<MissileDummy>();
                    dummyThis.AttachAt(part, launchTransforms[i]);
                }
                else
                {
                    if (missileSpawner.ammoCount > i)
                    {
                        if (launchTransforms[i].localScale != Vector3.one) launchTransforms[i].localScale = Vector3.one;
                    }
                    tubesFired = 0;
                }
            }
        }
        public void fireMissile(bool killWhenDone = false)
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            if (isClusterMissile) salvoSize = launchTransforms.Length;
            if (!(missileSalvo != null))
            {
                missileSalvo = StartCoroutine(salvoFire(killWhenDone));
                if (SymPart != null)
                {
                    using (var ml = VesselModuleRegistry.GetModules<MissileBase>(vessel).GetEnumerator())
                        while (ml.MoveNext())
                        {
                            if (ml.Current == null) continue;
                            if (ml.Current.part != SymPart) continue;
                            if (wpm != null) wpm.SendTargetDataToMissile(ml.Current);
                            MissileLauncher launcher = ml.Current as MissileLauncher;
                            if (launcher != null)
                            {
                                if (launcher.HasFired) continue;
                                launcher.FireMissile();
                            }
                        }
                }
            }
        }
        IEnumerator salvoFire(bool LaunchThenDestroy) 
        {
            int launchesThisSalvo = 0;
            float timeGap = (60 / rippleRPM) * TimeWarp.CurrentRate;
            int TargetID = 0;
            bool missileRegistry = false;
            //missileSpawner.MissileName = subMunitionName;
            if (deployState != null)
            {
                deployState.enabled = true;
                deployState.speed = 1;
                yield return new WaitWhileFixed(() => deployState.normalizedTime < 1); //wait for animation here
                deployState.normalizedTime = 1;
                deployState.speed = 0;
                deployState.enabled = false;
                if (BDArmorySettings.DEBUG_MISSILES) Debug.Log("[BDArmory.MultiMissileLauncher] deploy anim complete");
            }
            for (int m = tubesFired; m < launchTransforms.Length; m++)
            {
                if (BDArmorySettings.DEBUG_MISSILES) Debug.Log($"[BDArmory.MultiMissileLauncher] starting ripple launch on tube {m}, ripple delay: {timeGap:F3}");
                yield return new WaitForSecondsFixed(timeGap);
                if (launchesThisSalvo > salvoSize) //catch if launcher is trying to launch more missiles than it has
                {
                    //if (BDArmorySettings.DEBUG_MISSILES) Debug.Log("[BDArmory.MultiMissileLauncher] oops! firing more missiles than tubes or ammo");
                    break;
                }
                if (missileSpawner.ammoCount < 1 && !BDArmorySettings.INFINITE_ORDINANCE)
                {
                    tubesFired = 0;
                    break;
                }
                missileSpawner.SpawnMissile(launchTransforms[m], true);
                if (!BDArmorySettings.INFINITE_ORDINANCE) missileSpawner.ammoCount--;
                MissileLauncher ml = missileSpawner.SpawnedMissile.FindModuleImplementing<MissileLauncher>();
                if (!missileRegistry)
                {
                    BDATargetManager.FiredMissiles.Add(ml); //so multi-missile salvoes only count as a single missile fired by the WM for maxMissilesPerTarget
                    if (salvoSize > 1) missileRegistry = true;
                }
                yield return new WaitUntilFixed(() => ml.SetupComplete); // Wait until missile fully initialized.
                ml.Team = Team;
                ml.SourceVessel = missileLauncher.SourceVessel;
                ml.TimeFired = Time.time;
                ml.DetonationDistance = missileLauncher.DetonationDistance;
                ml.DetonateAtMinimumDistance = missileLauncher.DetonateAtMinimumDistance;
                ml.decoupleForward = missileLauncher.decoupleForward;
                ml.dropTime = 0;
                ml.guidanceActive = true;
                ml.detonationTime = missileLauncher.detonationTime;
                ml.engageAir = missileLauncher.engageAir;
                ml.engageGround = missileLauncher.engageGround;
                ml.engageMissile = missileLauncher.engageMissile;
                ml.engageSLW = missileLauncher.engageSLW;
                if (missileLauncher.GuidanceMode == GuidanceModes.AGMBallistic)
                {
                    ml.BallisticOverShootFactor = missileLauncher.BallisticOverShootFactor;
                    ml.BallisticAngle = missileLauncher.BallisticAngle;
                }
                if (missileLauncher.GuidanceMode == GuidanceModes.Cruise)
                {
                    ml.CruiseAltitude = missileLauncher.CruiseAltitude;
                    ml.CruiseSpeed = missileLauncher.CruiseSpeed;
                    ml.CruisePredictionTime = missileLauncher.CruisePredictionTime;
                }
                ml.decoupleForward = missileLauncher.decoupleForward;
                ml.decoupleSpeed = 5;
                if (missileLauncher.GuidanceMode == GuidanceModes.AGM)
                    ml.maxAltitude = missileLauncher.maxAltitude;
                ml.terminalGuidanceShouldActivate = missileLauncher.terminalGuidanceShouldActivate;
                if (isClusterMissile) ml.multiLauncher.overrideReferenceTransform = true;
                if (ml.TargetingMode == MissileBase.TargetingModes.Heat || ml.TargetingMode == MissileBase.TargetingModes.Radar)
                {
                    if (wpm.multiMissileTgtNum >= 2 && wpm != null)
                    {
                        if (TargetID > Mathf.Min((wpm.targetsAssigned.Count - 1), wpm.multiMissileTgtNum))
                        {
                            TargetID = 0; //if more missiles than targets, loop target list
                        }

                        if (wpm.targetsAssigned.Count > 0 && wpm.targetsAssigned[TargetID].Vessel != null)
                        {
                            if ((ml.engageAir && wpm.targetsAssigned[TargetID].isFlying) ||
                                (ml.engageGround && wpm.targetsAssigned[TargetID].isLandedOrSurfaceSplashed) ||
                                (ml.engageSLW && wpm.targetsAssigned[TargetID].isUnderwater)) //check engagement envelope
                            {
                                if (Vector3.Angle(wpm.targetsAssigned[TargetID].position - missileLauncher.MissileReferenceTransform.position, missileLauncher.GetForwardTransform()) < missileLauncher.maxOffBoresight) //is the target more-or-less in front of the missile(launcher)?
                                {
                                    if (ml.TargetingMode == MissileBase.TargetingModes.Heat) //need to input a heattarget, else this will just return MissileFire.CurrentTarget
                                    {
                                        Vector3 direction = (wpm.targetsAssigned[TargetID].position * wpm.targetsAssigned[TargetID].velocity.magnitude) - missileLauncher.MissileReferenceTransform.position;
                                        ml.heatTarget = BDATargetManager.GetHeatTarget(ml.SourceVessel, vessel, new Ray(missileLauncher.MissileReferenceTransform.position + (50 * missileLauncher.GetForwardTransform()), direction), TargetSignatureData.noTarget, ml.lockedSensorFOV * 0.5f, ml.heatThreshold, true, ml.lockedSensorFOVBias, ml.lockedSensorVelocityBias, wpm, wpm.targetsAssigned[TargetID]);
                                    }
                                    if (ml.TargetingMode == MissileBase.TargetingModes.Radar)
                                    {
                                        //ml.radarLOAL = true;
                                        ml.vrd = missileLauncher.vrd; //look into better method of assigning multiple radar targets - link into sourcevessel's vessleradardata.lockedtargetdata, iterate though target list?
                                        TargetSignatureData[] scannedTargets = new TargetSignatureData[(int)wpm.multiMissileTgtNum];
                                        RadarUtils.RadarUpdateMissileLock(new Ray(ml.transform.position, ml.GetForwardTransform()), ml.lockedSensorFOV * 5, ref scannedTargets, 0.4f, ml);
                                        TargetSignatureData lockedTarget = TargetSignatureData.noTarget;

                                        for (int i = 0; i < scannedTargets.Length; i++)
                                        {
                                            if (scannedTargets[i].exists && scannedTargets[i].vessel == wpm.targetsAssigned[TargetID].Vessel)
                                            {
                                                if(BDArmorySettings.DEBUG_MISSILES)
                                                    Debug.Log($"[BDArmory.MultiMissileLauncher] Found Radar target");
                                                ml.radarTarget = scannedTargets[i];
                                                break;
                                            }
                                        }
                                    }
                                    ml.targetVessel = wpm.targetsAssigned[TargetID];
                                    //if (BDArmorySettings.DEBUG_MISSILES)
                                        Debug.Log($"[BDArmory.MultiMissileLauncher] Assigning target {TargetID}: {wpm.targetsAssigned[TargetID].Vessel.GetName()}; total possible targets {wpm.targetsAssigned.Count}");
                                }
                                else //else try remaining targets on the list. 
                                {
                                    for (int t = TargetID; t < wpm.targetsAssigned.Count; t++)
                                    {
                                        if ((ml.engageAir && !wpm.targetsAssigned[t].isFlying) ||
                                            (ml.engageGround && !wpm.targetsAssigned[t].isLandedOrSurfaceSplashed) ||
                                            (ml.engageSLW && !wpm.targetsAssigned[t].isUnderwater)) continue; //check engagement envelope

                                        if (Vector3.Angle(wpm.targetsAssigned[t].position - missileLauncher.MissileReferenceTransform.position, missileLauncher.GetForwardTransform()) < missileLauncher.maxOffBoresight) //is the target more-or-less in front of the missile(launcher)?
                                        {
                                            if (ml.TargetingMode == MissileBase.TargetingModes.Heat)
                                            {
                                                Vector3 direction = (wpm.targetsAssigned[t].position * wpm.targetsAssigned[t].velocity.magnitude) - missileLauncher.MissileReferenceTransform.position;
                                                ml.heatTarget = BDATargetManager.GetHeatTarget(ml.SourceVessel, vessel, new Ray(missileLauncher.MissileReferenceTransform.position + (50 * missileLauncher.GetForwardTransform()), direction), TargetSignatureData.noTarget, ml.lockedSensorFOV * 0.5f, ml.heatThreshold, true, ml.lockedSensorFOVBias, ml.lockedSensorVelocityBias, wpm, wpm.targetsAssigned[t]);
                                            }
                                            if (ml.TargetingMode == MissileBase.TargetingModes.Radar)
                                            {
                                                //ml.radarLOAL = true;
                                                ml.vrd = missileLauncher.vrd; 
                                                TargetSignatureData[] scannedTargets = new TargetSignatureData[(int)wpm.multiMissileTgtNum];
                                                RadarUtils.RadarUpdateMissileLock(new Ray(ml.transform.position, ml.GetForwardTransform()), ml.lockedSensorFOV * 3, ref scannedTargets, 0.4f, ml);
                                                TargetSignatureData lockedTarget = TargetSignatureData.noTarget;

                                                for (int i = 0; i < scannedTargets.Length; i++)
                                                {
                                                    if (scannedTargets[i].exists && scannedTargets[i].vessel == wpm.targetsAssigned[TargetID].Vessel)
                                                    {
                                                        if (BDArmorySettings.DEBUG_MISSILES)
                                                            Debug.Log($"[BDArmory.MultiMissileLauncher] Found Radar target");
                                                        ml.radarTarget = scannedTargets[i];
                                                        break;
                                                    }
                                                }
                                            }
                                            ml.targetVessel = wpm.targetsAssigned[t];
                                            //if (BDArmorySettings.DEBUG_MISSILES)
                                                Debug.Log($"[BDArmory.MultiMissileLauncher] Assigning backup target (taretID {TargetID}) {wpm.targetsAssigned[t].Vessel.GetName()}");
                                        }
                                    }
                                    //if (BDArmorySettings.DEBUG_MISSILES) 
                                    Debug.Log($"[BDArmory.MultiMissileLauncher] Couldn't assign valid target, trying from beginning of target list");
                                    if (ml.targetVessel == null) //check targets that were already assigned and passed. using the above iterator to prevent all targets outisde allowed FoV or engagement enveolpe from being assigned the firest possible target by checking later ones first
                                    { 
                                        using (List<TargetInfo>.Enumerator item = wpm.targetsAssigned.GetEnumerator())
                                            while (item.MoveNext())
                                            {
                                                if (item.Current.Vessel == null) continue;
                                                if (Vector3.Angle(item.Current.position - missileLauncher.MissileReferenceTransform.position, missileLauncher.GetForwardTransform()) < missileLauncher.maxOffBoresight) //is the target more-or-less in front of the missile(launcher)?
                                                {
                                                    if (ml.TargetingMode == MissileBase.TargetingModes.Heat)
                                                    {
                                                        Vector3 direction = (item.Current.position * item.Current.velocity.magnitude) - missileLauncher.MissileReferenceTransform.position;
                                                        ml.heatTarget = BDATargetManager.GetHeatTarget(ml.SourceVessel, vessel, new Ray(missileLauncher.MissileReferenceTransform.position + (50 * missileLauncher.GetForwardTransform()), direction), TargetSignatureData.noTarget, ml.lockedSensorFOV * 0.5f, ml.heatThreshold, true, ml.lockedSensorFOVBias, ml.lockedSensorVelocityBias, wpm, item.Current);
                                                    }
                                                    if (ml.TargetingMode == MissileBase.TargetingModes.Radar)
                                                    {
                                                        ml.radarLOAL = true;
                                                        ml.vrd = missileLauncher.vrd;
                                                        TargetSignatureData[] scannedTargets = new TargetSignatureData[(int)wpm.multiMissileTgtNum];
                                                        RadarUtils.RadarUpdateMissileLock(new Ray(ml.transform.position, ml.GetForwardTransform()), ml.lockedSensorFOV * 3, ref scannedTargets, 0.4f, ml);
                                                        TargetSignatureData lockedTarget = TargetSignatureData.noTarget;

                                                        for (int i = 0; i < scannedTargets.Length; i++)
                                                        {
                                                            if (scannedTargets[i].exists && scannedTargets[i].vessel == wpm.targetsAssigned[TargetID].Vessel)
                                                            {
                                                                if (BDArmorySettings.DEBUG_MISSILES)
                                                                    Debug.Log($"[BDArmory.MultiMissileLauncher] Found Radar target");
                                                                ml.radarTarget = scannedTargets[i];
                                                                break;
                                                            }
                                                        }
                                                    }
                                                    ml.targetVessel = item.Current;
                                                    //if (BDArmorySettings.DEBUG_MISSILES)
                                                    Debug.Log($"[BDArmory.MultiMissileLauncher] original target out of sensor range; engaging {item.Current.Vessel.GetName()}");
                                                    break;
                                                }
                                            }
                                    }
                                }
                            }
                            TargetID++;
                        }
                    }
                    else
                    {
                        if (wpm != null) wpm.SendTargetDataToMissile(ml);
                    }
                }
                else
                {
                    wpm.SendTargetDataToMissile(ml);
                }
                ml.launched = true;
                ml.TargetPosition = vessel.ReferenceTransform.position + (vessel.ReferenceTransform.up * 5000); //set initial target position so if no target update, missileBase will count a miss if it nears this point or is flying post-thrust
                ml.MissileLaunch();
                launchTransforms[m].localScale = Vector3.zero;
                tubesFired++;
                launchesThisSalvo++;
            }
            wpm.heatTarget = TargetSignatureData.noTarget;
            if (deployState != null)
            {
                deployState.enabled = true;
                deployState.speed = -1;
                yield return new WaitWhileFixed(() => deployState.normalizedTime > 0);
                deployState.normalizedTime = 0;
                deployState.speed = 0;
                deployState.enabled = false;
            }
            if (tubesFired >= launchTransforms.Length) //add a timer for reloading a partially emptied MML if it hasn't been used for a while?
            {
                if (BDArmorySettings.INFINITE_AMMO || missileSpawner.ammoCount >= salvoSize)
                    if (!(missileLauncher.reloadRoutine != null))
                    {
                        missileLauncher.reloadRoutine = StartCoroutine(missileLauncher.MissileReload());
                        if (BDArmorySettings.DEBUG_MISSILES) Debug.Log("[BDArmory.MultiMissileLauncher] all submunitions fired. Reloading");
                    }
            }
            if (LaunchThenDestroy)
            {
                if (part != null)
                {
                    missileLauncher.DestroyMissile();
                }
            }
            missileSalvo = null;
        }

        public void SetupMissileDummyPool(string modelpath)
        {
            var key = modelpath;
            if (!mslDummyPool.ContainsKey(key) || mslDummyPool[key] == null)
            {
                var Template = GameDatabase.Instance.GetModel(modelpath);
                if (Template == null)
                {
                    Debug.LogError("[BDArmory.MultiMissilelauncher]: model '" + modelpath + "' not found. Expect exceptions if trying to use this missile.");
                    return;
                }
                Template.SetActive(false);
                Template.AddComponent<MissileDummy>();
                mslDummyPool[key] = ObjectPool.CreateObjectPool(Template, 10, true, true);
            }
        }
    }
}

//huh. Vessel is a Monobehavior... Could a Vessel component be added to, say, rockets? if So, going the rocket route for missile pods/reloads would much simpler as most of the targeting code would no longer need to be rewritted to look for TargetInfos instead of Vessels. Would still need to mod the Bullet/Rocket/laser/Explosion hit code, and add an internal HP value, but...
//performance hit/memleaks from spawning in new parts/vessels? Investigate
