using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using BDArmory.Modules;
using BDArmory.Utils;
using BDArmory.Damage;
using BDArmory.Settings;
using System;

namespace BDArmory.CounterMeasure
{
    public class VesselCloakInfo : MonoBehaviour
    {
        List<ModuleCloakingDevice> cloaks;
        public Vessel vessel;

        bool cEnabled;
        Shader cloakShader;

        public bool cloakEnabled
        {
            get { return cEnabled; }
        }

        float orf = 1;
        public float opticalReductionFactor
        {
            get { return orf; }
        }

        float trf = 1;
        public float thermalReductionFactor
        {
            get { return trf; }
        }

        void Start()
        {
            vessel = GetComponent<Vessel>();
            if (!vessel)
            {
                Debug.Log("[BDArmory.VesselCloakInfo]: VesselCloakInfo was added to an object with no vessel component");
                Destroy(this);
                return;
            }
            cloaks = new List<ModuleCloakingDevice>();
            vessel.OnJustAboutToBeDestroyed += AboutToBeDestroyed;
            GameEvents.onVesselCreate.Add(OnVesselCreate);
            GameEvents.onPartJointBreak.Add(OnPartJointBreak);
            GameEvents.onPartDie.Add(OnPartDie);
            cloakShader = Shader.Find("KSP/Alpha/Unlit Transparent");
        }

        void OnDestroy()
        {
            if (vessel) vessel.OnJustAboutToBeDestroyed -= AboutToBeDestroyed;
            GameEvents.onVesselCreate.Remove(OnVesselCreate);
            GameEvents.onPartJointBreak.Remove(OnPartJointBreak);
            GameEvents.onPartDie.Remove(OnPartDie);
        }

        void AboutToBeDestroyed()
        {
            Destroy(this);
        }

        void OnPartDie(Part p = null)
        {
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(DelayedCleanCloakListRoutine());
            }
        }

        void OnVesselCreate(Vessel v)
        {
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(DelayedCleanCloakListRoutine());
            }
        }

        void OnPartJointBreak(PartJoint j, float breakForce)
        {
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(DelayedCleanCloakListRoutine());
            }
            var r = j.Parent.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < r.Length; i++)
            {
                HitpointTracker a = j.Parent.GetComponent<HitpointTracker>();
                if (!a.defaultShader.ContainsKey(r[i].material.GetInstanceID())) continue; // Don't modify shaders that we don't have defaults for as we can't then replace them.
                if (r[i].GetComponentInParent<Part>() != j.Parent) continue; // Don't recurse to child parts.
                r[i].material.SetFloat("_Opacity", 1);
            }
        }

        public void AddCloak(ModuleCloakingDevice cloak)
        {
            if (!cloaks.Contains(cloak))
            {
                cloaks.Add(cloak);
            }

            UpdateCloakStrength();
        }

        public void RemoveCloak(ModuleCloakingDevice cloak)
        {
            cloaks.Remove(cloak);

            UpdateCloakStrength();
        }

        void UpdateCloakStrength()
        {
            cEnabled = cloaks.Count > 0;

            trf = 1;
            orf = 1;

            using (List<ModuleCloakingDevice>.Enumerator cloak = cloaks.GetEnumerator())
                while (cloak.MoveNext())
                {
                    if (cloak.Current == null) continue;
                    if (cloak.Current.thermalReductionFactor < trf)
                    {
                        trf = cloak.Current.thermalReductionFactor;
                    }
                    if (cloak.Current.opticalReductionFactor < orf)
                    {
                        orf = cloak.Current.opticalReductionFactor;
                    }
                }
        }

        public void DelayedCleanCloakList()
        {
            StartCoroutine(DelayedCleanCloakListRoutine());
        }

        IEnumerator DelayedCleanCloakListRoutine()
        {
            var wait = new WaitForFixedUpdate();
            yield return wait;
            yield return wait;
            CleanCloakList();
        }

        void CleanCloakList()
        {
            vessel = GetComponent<Vessel>();

            if (!vessel)
            {
                Destroy(this);
            }
            cloaks.RemoveAll(j => j == null);
            cloaks.RemoveAll(j => j.vessel != vessel);

            using (var cl = VesselModuleRegistry.GetModules<ModuleCloakingDevice>(vessel).GetEnumerator())
                while (cl.MoveNext())
                {
                    if (cl.Current == null) continue;
                    if (cl.Current.cloakEnabled)
                    {
                        AddCloak(cl.Current);
                    }
                }
            UpdateCloakStrength();
        }
        public IEnumerator UpdateVisuals(float CloakTime, bool deactivateCloak)
        {
            yield return new WaitForFixedUpdate();
            if (!deactivateCloak)
            {
                using (List<Part>.Enumerator parts = vessel.parts.GetEnumerator())
                    while (parts.MoveNext())
                    {
                        HitpointTracker a = parts.Current.GetComponent<HitpointTracker>();
                        var r = parts.Current.GetComponentsInChildren<Renderer>();
                        try
                        {
                            if (!a.RegisterProcWingShader && parts.Current.name.Contains("B9.Aero.Wing.Procedural"))
                            {
                                for (int s = 0; s < r.Length; s++)
                                {
                                    if (r[s].GetComponentInParent<Part>() != parts.Current) continue; // Don't recurse to child parts.
                                    int key = r[s].material.GetInstanceID();
                                    a.defaultShader.Add(key, r[s].material.shader);
                                    if (r[s].material.HasProperty("_Color"))
                                    {
                                        a.defaultColor.Add(key, r[s].material.color);
                                    }
                                }
                                a.RegisterProcWingShader = true;
                            }
                            for (int i = 0; i < r.Length; i++)
                            {
                                if (!a.defaultShader.ContainsKey(r[i].material.GetInstanceID())) continue; // Don't modify shaders that we don't have defaults for as we can't then replace them.
                                if (r[i].GetComponentInParent<Part>() != parts.Current) continue; // Don't recurse to child parts.
                                if (r[i].material.shader.name.Contains("Alpha")) continue;
                                if (r[i].material.shader.name.Contains("Waterfall")) continue;
                                if (r[i].material.shader.name.Contains("KSP/Particles")) continue;
                                r[i].material.shader = cloakShader;
                            }
                        }
                        catch
                        {
                            Debug.Log("[RadarUtils]: material on " + parts.Current.name + "could not find set RCS shader/color");
                        }
                    }
            }
            else
            {
                using (List<Part>.Enumerator parts = (vessel.parts.GetEnumerator()))
                    while (parts.MoveNext())
                    {
                        var r = parts.Current.GetComponentsInChildren<Renderer>();
                        for (int i = 0; i < r.Length; i++)
                        {
                            HitpointTracker a = parts.Current.GetComponent<HitpointTracker>();
                            try
                            {
                                if (r[i].GetComponentInParent<Part>() != parts.Current) continue; // Don't recurse to child parts.
                                int key = r[i].material.GetInstanceID();
                                if (!a.defaultShader.ContainsKey(key))
                                {
                                    if (BDArmorySettings.DEBUG_RADAR) Debug.Log($"[BDArmory.CloakingDevice]: {r[i].material.name} ({key}) not found in defaultShader for part {parts.Current.partInfo.name} on {vessel.vesselName}"); // Enable this to see what materials aren't getting cloak shader applied to them.
                                    continue;
                                }
                                if (r[i].material.shader != a.defaultShader[key])
                                {
                                    if (a.defaultShader[key] != null)
                                    {
                                        r[i].material.shader = a.defaultShader[key];
                                    }
                                    if (a.defaultColor.ContainsKey(key))
                                    {
                                        if (a.defaultColor[key] != null)
                                        {
                                            if (parts.Current.name.Contains("B9.Aero.Wing.Procedural"))
                                                r[i].material.SetColor("_MainTex", a.defaultColor[key]);
                                            else
                                                r[i].material.SetColor("_Color", a.defaultColor[key]);
                                        }
                                        else
                                        {
                                            if (parts.Current.name.Contains("B9.Aero.Wing.Procedural"))
                                                r[i].material.SetColor("_MainTex", Color.white);
                                            else
                                                r[i].material.SetColor("_Color", Color.white);
                                        }
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                Debug.Log($"[RadarUtils]: material on {parts.Current.name} could not find default shader/color: {e.Message}\n{e.StackTrace}");
                            }
                        }
                    }
            }
            float cloakTimer = deactivateCloak? CloakTime: 0; 
            while (deactivateCloak ? cloakTimer > 0 : cloakTimer < CloakTime)
            {
                using (var Part = vessel.Parts.GetEnumerator())
                    while (Part.MoveNext())
                    {
                        if (Part.Current == null) continue;
                        var r = Part.Current.GetComponentsInChildren<Renderer>();
                        for (int i = 0; i < r.Length; i++)
                        {
                            HitpointTracker a = Part.Current.GetComponent<HitpointTracker>();
                            if (!a.defaultShader.ContainsKey(r[i].material.GetInstanceID())) continue; // Don't modify shaders that we don't have defaults for as we can't then replace them.
                            if (r[i].GetComponentInParent<Part>() != Part.Current) continue; // Don't recurse to child parts.
                            r[i].material.SetFloat("_Opacity", Mathf.Lerp(1, opticalReductionFactor, (cloakTimer / CloakTime)));
                        }
                    }
                if (deactivateCloak) cloakTimer-= TimeWarp.fixedDeltaTime; else cloakTimer+= TimeWarp.fixedDeltaTime;
            }
        }
    }
}
