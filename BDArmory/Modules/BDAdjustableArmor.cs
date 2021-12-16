using BDArmory.Core.Module;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BDArmory.Modules
{
    public class BDAdjustableArmor : PartModule
    {
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_ArmorHeight"),//Engage Range Min
 UI_FloatRange(minValue = 0.5f, maxValue = 16, stepIncrement = 0.5f, scene = UI_Scene.Editor)]
        public float Height = 1;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_ArmorWidth"),//Engage Range Min
 UI_FloatRange(minValue = 0.5f, maxValue = 16, stepIncrement = 0.5f, scene = UI_Scene.Editor)]
        public float Length = 1;

        [KSPField]
        public bool isTriangularPanel = false;

        public bool isCurvedPanel = false;

        private float updateTimer = 0;
        private float armorthickness = 1;

        [KSPField]
        public string ArmorTransformName = "ArmorTransform"; //transform of armor panel mesh/box collider
        Transform[] armorTransforms;

        [KSPField]
        public string ThicknessTransformName = "ThicknessTransform"; //name of armature to control thickness of curved panels
        Transform ThicknessTransform;

		AttachNode N1;
		AttachNode N2;
		AttachNode N3;
        AttachNode N4;

        [KSPField]
        public string Node1Name = "Node1"; //name of attachnode node transform in model
		Transform N1Transform;
        [KSPField]
        public string Node2Name = "Node2";
		Transform N2Transform;
        [KSPField]
        public string Node3Name = "Node3"; 
		Transform N3Transform;
        [KSPField]
        public string Node4Name = "Node4"; 
        Transform N4Transform;

        HitpointTracker armor;

        public override void OnStart(StartState state)
        {
            armorTransforms = part.FindModelTransforms(ArmorTransformName);
			for (int i = 0; i < armorTransforms.Length; i++)
			{
				armorTransforms[i].localScale = new Vector3(Height, Length, 1);
			}
            ThicknessTransform = part.FindModelTransform(ThicknessTransformName);
            if (ThicknessTransform != null)
            {
                isCurvedPanel = true;
            }
            UI_FloatRange AHeight = (UI_FloatRange)Fields["Height"].uiControlEditor;
            if (isCurvedPanel)
            {
                AHeight.maxValue = 4f;
                AHeight.minValue = 0f;
                AHeight.stepIncrement = 1;
                Fields["Height"].guiName = "#LOC_BDArmory_CylArmorScale";
            }
            AHeight.onFieldChanged = AdjustHeight;
            UI_FloatRange ALength = (UI_FloatRange)Fields["Length"].uiControlEditor;
            ALength.onFieldChanged = AdjustLength;
            if (HighLogic.LoadedSceneIsEditor)
            {
                armor = GetComponent<HitpointTracker>();
            }
            N1 = part.FindAttachNode("Node1");
            N1Transform = part.FindModelTransform(Node1Name);
            N2 = part.FindAttachNode("Node2");
            N2Transform = part.FindModelTransform(Node2Name);
            if (!isTriangularPanel)
            {
                N3 = part.FindAttachNode("Node3");
                N3Transform = part.FindModelTransform(Node3Name);
                N4 = part.FindAttachNode("Node4");
                N4Transform = part.FindModelTransform(Node4Name);
            }
        }

        public void AdjustHeight(BaseField field, object obj)
        {
            Height = Mathf.Clamp(Height, 0.5f, 16f);
            if (!isCurvedPanel)
            {
                for (int i = 0; i < armorTransforms.Length; i++)
                {
                    armorTransforms[i].localScale = new Vector3(Height, Length, armorthickness);
                }
                using (List<Part>.Enumerator sym = part.symmetryCounterparts.GetEnumerator())
                    while (sym.MoveNext())
                    {
                        if (sym.Current == null) continue;
                        sym.Current.FindModuleImplementing<BDAdjustableArmor>().UpdateScale(Height, Length);
                    }
            }
            else
            {
                for (int i = 0; i < armorTransforms.Length; i++)
                {
                    armorTransforms[i].localScale = new Vector3(Height, Length, Height);
                }
                using (List<Part>.Enumerator sym = part.symmetryCounterparts.GetEnumerator())
                    while (sym.MoveNext())
                    {
                        if (sym.Current == null) continue;
                        sym.Current.FindModuleImplementing<BDAdjustableArmor>().UpdateScale(Height, Length);
                    }
            }
            updateArmorStats();
            UpdateNodes();
        }
        public void AdjustLength(BaseField field, object obj)
        {
            Length = Mathf.Clamp(Length, 0.5f, 16f);
            for (int i = 0; i < armorTransforms.Length; i++)
            {
                armorTransforms[i].localScale = new Vector3(Height, Length, armorthickness);
            }
            using (List<Part>.Enumerator sym = part.symmetryCounterparts.GetEnumerator())
                while (sym.MoveNext())
                {
                    if (sym.Current == null) continue;
                    sym.Current.FindModuleImplementing<BDAdjustableArmor>().UpdateScale(Height, Length);
                }
            updateArmorStats();
            UpdateNodes();
        }
        
        public void UpdateScale(float height, float length)
        {
            Height = height;
            Length = length;
            if (!isCurvedPanel)
            {
                for (int i = 0; i < armorTransforms.Length; i++)
                {
                    armorTransforms[i].localScale = new Vector3(Height, Length, Mathf.Clamp((armor.Armor / 10), 0.1f, 1500));
                }
            }
            else
            {
                for (int i = 0; i < armorTransforms.Length; i++)
                {
                    armorTransforms[i].localScale = new Vector3(Height, Length, Height);
                }
            }
            updateArmorStats();
            UpdateNodes(Mathf.Max(Mathf.CeilToInt(height/2), Mathf.CeilToInt(length/2)));
        }
        public void UpdateNodes(int size = 1)
        {
            N1.position = N1Transform.position;
			N1.size = size;
            N1.breakingForce = size * 100;
            N1.breakingTorque = size * 100;
            N2.position = N2Transform.position;
			N2.size = size;
            N2.breakingForce = size * 100;
            N2.breakingTorque = size * 100;
            if (!isTriangularPanel)
            {
                N3.position = N3Transform.position;
                N3.size = size;
                N3.breakingForce = size * 100;
                N3.breakingTorque = size * 100;
                N4.position = N4Transform.position;
                N4.size = size;
                N4.breakingForce = size * 100;
                N4.breakingTorque = size * 100;
            }
        }
        public void updateArmorStats()
        {
            if (isCurvedPanel)
            {
                armor.armorVolume = (Length * (Mathf.Clamp(Height, 0.5f, 4) * Mathf.Clamp(Height, 0.5f, 4))); //gives surface area for 1/4 cyl srf area
            }
            else
            {
                armor.armorVolume = (Height * Length);
                if (isTriangularPanel)
                {
                    armor.armorVolume /= 2;
                }
            }
            armor.ArmorSetup(null, null);
        }
        void UpdateThickness()
        {
            armorthickness = Mathf.Clamp((armor.Armor / 10), 0.1f, 1500);
            if (!isCurvedPanel)
            {
                for (int i = 0; i < armorTransforms.Length; i++)
                {
                    armorTransforms[i].localScale = new Vector3(Height, Length, armorthickness);
                }
            }
            else
            {
                ThicknessTransform.localScale = new Vector3(armorthickness, 1, armorthickness);
            }            
        }
        private void Update()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                updateTimer -= Time.fixedDeltaTime;
                if (updateTimer < 0)
                {
                    UpdateThickness(); //see if EditorShipModified catches adjusting armor thickness, and if so, use that instead
                    updateTimer = 0.5f;    //next update in half a sec only
                }
            }
        }
    }
}