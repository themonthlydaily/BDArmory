using BDArmory.Core.Module;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BDArmory.Modules
{
    public class BDAdjustableArmor : PartModule
    {
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_ArmorWidth"),//Engage Range Min
 UI_FloatRange(minValue = 0.5f, maxValue = 16, stepIncrement = 0.5f, scene = UI_Scene.Editor)]
        public float Width = 1;

        private float OldWidth = 1;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_ArmorLength"),//Engage Range Min
 UI_FloatRange(minValue = 0.5f, maxValue = 16, stepIncrement = 0.5f, scene = UI_Scene.Editor)]
        public float Length = 1;

        private float OldLength = 1;

        [KSPField]
        public bool isTriangularPanel = false;

        //public bool isCurvedPanel = false;
        private float armorthickness = 1;
        private float Oldthickness = 1;

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
                armorTransforms[i].localScale = new Vector3(Width, Length, 1);
            }
            ThicknessTransform = part.FindModelTransform(ThicknessTransformName);
            //if (ThicknessTransform != null)
            //{
            //    isCurvedPanel = true;
            //}
            UI_FloatRange AWidth = (UI_FloatRange)Fields["Width"].uiControlEditor;
            //if (isCurvedPanel)
            //{
            //    AWidth.maxValue = 4f;
            //    AWidth.minValue = 0f;
            //    AWidth.stepIncrement = 1;
            //    Fields["Width"].guiName = "#LOC_BDArmory_CylArmorScale";
            //}
            AWidth.onFieldChanged = AdjustWidth;
            UI_FloatRange ALength = (UI_FloatRange)Fields["Length"].uiControlEditor;
            ALength.onFieldChanged = AdjustLength;
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorShipModified.Add(OnEditorShipModifiedEvent);
            }
            armor = GetComponent<HitpointTracker>();
            N1 = part.FindAttachNode("Node1");
            N1.nodeType = AttachNode.NodeType.Stack;
            N1Transform = part.FindModelTransform(Node1Name);
            N2 = part.FindAttachNode("Node2");
            N2.nodeType = AttachNode.NodeType.Stack;
            N2Transform = part.FindModelTransform(Node2Name);
            if (!isTriangularPanel)
            {
                N3 = part.FindAttachNode("Node3");
                N3Transform = part.FindModelTransform(Node3Name);
                N3.nodeType = AttachNode.NodeType.Stack;
                N4 = part.FindAttachNode("Node4");
                N4Transform = part.FindModelTransform(Node4Name);
                N4.nodeType = AttachNode.NodeType.Stack;
            }
            UpdateThickness();
        }
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight) return;
            armor = GetComponent<HitpointTracker>();
        }
        private void OnDestroy()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorShipModified.Remove(OnEditorShipModifiedEvent);
            }
        }

        public void AdjustWidth(BaseField field, object obj)
        {
            Width = Mathf.Clamp(Width, 0.5f, 16f);
            //if (!isCurvedPanel)
            {
                for (int i = 0; i < armorTransforms.Length; i++)
                {
                    armorTransforms[i].localScale = new Vector3(Width, Length, armorthickness);
                }
                using (List<Part>.Enumerator sym = part.symmetryCounterparts.GetEnumerator())
                    while (sym.MoveNext())
                    {
                        if (sym.Current == null) continue;
                        sym.Current.FindModuleImplementing<BDAdjustableArmor>().UpdateScale(Width, Length); //needs to be changed to use updatewitth() - FIXME later, future SI
                    }
            }
            /*
            else
            {
                for (int i = 0; i < armorTransforms.Length; i++)
                {
                    armorTransforms[i].localScale = new Vector3(Width, Length, Width);
                }
                using (List<Part>.Enumerator sym = part.symmetryCounterparts.GetEnumerator())
                    while (sym.MoveNext())
                    {
                        if (sym.Current == null) continue;
                        sym.Current.FindModuleImplementing<BDAdjustableArmor>().UpdateScale(Width, Length);
                    }
            }
            */
            updateArmorStats();
            HandleWidthChange(Width, OldWidth);
            OldWidth = Width;
        }
        public void AdjustLength(BaseField field, object obj)
        {
            Length = Mathf.Clamp(Length, 0.5f, 16f);
            for (int i = 0; i < armorTransforms.Length; i++)
            {
                armorTransforms[i].localScale = new Vector3(Width, Length, armorthickness);
            }
            using (List<Part>.Enumerator sym = part.symmetryCounterparts.GetEnumerator())
                while (sym.MoveNext())
                {
                    if (sym.Current == null) continue;
                    sym.Current.FindModuleImplementing<BDAdjustableArmor>().UpdateScale(Width, Length);
                }
            updateArmorStats();
            HandleLengthChange(Length, OldLength);
            OldLength = Length;
        }

        public void UpdateScale(float width, float length)
        {
            Width = width;
            Length = length;
            
            //if (!isCurvedPanel)
            {
                for (int i = 0; i < armorTransforms.Length; i++)
                {
                    armorTransforms[i].localScale = new Vector3(Width, Length, Mathf.Clamp((armor.Armor / 10), 0.1f, 1500));
                }
            }
            /*
            else
            {
                for (int i = 0; i < armorTransforms.Length; i++)
                {
                    armorTransforms[i].localScale = new Vector3(Width, Length, Width);
                }
            }
            */
            updateArmorStats();
            HandleWidthChange(Width, OldWidth);
            HandleLengthChange(Length, OldLength);
        }

        /// //////////////////////////////////
        //Borrowed/modified from ProceduralParts
        public virtual void HandleLengthChange(float length, float oldLength)
        {
            float trans = length - oldLength;

            N1.position.z = N1.position.z + (trans / 2);
            if (N1.attachedPart is Part N1pushTarget)
            {
                TranslatePart(N1pushTarget, N1Transform.forward * (trans / 2));
            }
            N1.size = Mathf.CeilToInt(length / 2);
            N1.breakingForce = length * 100;
            N1.breakingTorque = length * 100;
            if (!isTriangularPanel)
            {
                N3.position.z = N3.position.z + (-trans / 2);
                if (N3.attachedPart is Part N3pushTarget)
                {
                    TranslatePart(N3pushTarget, -N3Transform.forward * (trans / 2));
                }
                N3.size = Mathf.CeilToInt(length / 2);
                N3.breakingForce = length * 100;
                N3.breakingTorque = length * 100;
            }
            foreach (Part p in part.children)
            {
                if (p.FindAttachNodeByPart(part) is AttachNode node && node.nodeType == AttachNode.NodeType.Surface)
                {
                    Vector3 localSpace = part.transform.InverseTransformPoint(node.owner.transform.TransformPoint(node.position));
                    if (localSpace.z > 0.9f * (oldLength / 2))
                    {
                        //Debug.Log("[BDAA DEBUG] srfAttack detected on N1 edge, moving");
                        TranslatePart(p, N1Transform.up * (trans / 2)); 
                    }
                    if (!isTriangularPanel)
                    {
                        if (localSpace.z < 0.9f * -(oldLength / 2))
                        {
                            //Debug.Log("[BDAA DEBUG] srfAttack detected on N3 edge, moving");
                            TranslatePart(p, -N3Transform.up * (trans / 2));
                        }
                    }
                }
            }            
        }
   
        public virtual void HandleWidthChange(float width, float oldWidth)
        {
            float trans = width - oldWidth;
            N2.position.x = N2.position.x + (-trans / 2); 
            if (N2.attachedPart is Part N2pushTarget)
            {
                TranslatePart(N2pushTarget, -N2Transform.right * (trans / 2));
            }
            N2.size = Mathf.CeilToInt(width / 2);
            N2.breakingForce = width * 100;
            N2.breakingTorque = width * 100;
            if (!isTriangularPanel)
            {
                N4.position.x = N4.position.x + (trans / 2);
                if (N4.attachedPart is Part N4pushTarget)
                {
                    TranslatePart(N4pushTarget, N4Transform.right * (trans / 2));
                }
                N4.size = Mathf.CeilToInt(width / 2);
                N4.breakingForce = width * 100;
                N4.breakingTorque = width * 100;
            }

            foreach (Part p in part.children)
            {
                if (p.FindAttachNodeByPart(part) is AttachNode node && node.nodeType == AttachNode.NodeType.Surface)
                {
                    Vector3 localSpace = part.transform.InverseTransformPoint(node.owner.transform.TransformPoint(node.position));
                    if (localSpace.x < 0.9f * -(oldWidth / 2))
                    {
                        TranslatePart(p, N2Transform.right * (trans / 2));
                    }
                    if (!isTriangularPanel)
                    {
                        if (localSpace.x > 0.9f * (oldWidth / 2))
                        {
                            TranslatePart(p, N4Transform.right * (trans / 2));
                        }
                    }
                }
            }            
        }
        
        public Part GetEldestParent(Part p) => (p.parent is null) ? p : GetEldestParent(p.parent);
        public void TranslatePart(Part pushTarget, Vector3 translation)
        {
            // If the attached part is a child of ours, push it directly.
            // If it is our parent, then we need to find the eldest grandparent and push that, and also ourselves
            if (pushTarget == this.part.parent)
            {
                this.part.transform.Translate(-translation, Space.Self);    // Push ourselves normally
                float sibMult = part.symmetryCounterparts == null ? 1f : 1f / (part.symmetryCounterparts.Count + 1);
                pushTarget = GetEldestParent(this.part);
                translation *= sibMult; // Push once for each symmetry sibling, so scale the parent push.
            }
            // Convert to world space, to deal with bizarre orientation relationships.
            // (ex: pushTarget is inverted, and our top node connects to its top node)
            Vector3 worldSpaceTranslation = part.transform.TransformVector(translation);
            pushTarget.transform.Translate(worldSpaceTranslation, Space.World);
        }
        /// ///////////////////////////

        public void updateArmorStats()
        {
            /*
            if (isCurvedPanel)
            {
                armor.armorVolume = (Length * (Mathf.Clamp(Width, 0.5f, 4) * Mathf.Clamp(Width, 0.5f, 4))); //gives surface area for 1/4 cyl srf area
            }
            else
            */
            {
                armor.armorVolume = (Width * Length);
                if (isTriangularPanel)
                {
                    armor.armorVolume /= 2;
                }
            }
            armor.ArmorSetup(null, null);
        }
        void UpdateThickness()
        {
            if (armor != null && armorTransforms != null)
            {
                armorthickness = Mathf.Clamp((armor.Armor / 10), 0.1f, 1500);
                //if (!isCurvedPanel)
                {
                    for (int i = 0; i < armorTransforms.Length; i++)
                    {
                        armorTransforms[i].localScale = new Vector3(Width, Length, armorthickness);
                    }
                }
                /*
                else
                {
                    ThicknessTransform.localScale = new Vector3(armorthickness, 1, armorthickness);
                }
                */
            }
            else
            {
                if (armor == null) Debug.Log("[BDAAdjustableArmor] No HitpointTracker found! aborting UpdateThickness()!");
                if (armorTransforms == null) Debug.Log("[BDAAdjustableArmor] No ArmorTransform found! aborting UpdateThickness()!");
                return;
            }
            float ratio = (armorthickness - Oldthickness) / 100;
            foreach (Part p in part.children)
            {
                if (p.FindAttachNodeByPart(part) is AttachNode node && node.nodeType == AttachNode.NodeType.Surface)
                {
                    Vector3 localSpace = part.transform.InverseTransformPoint(node.owner.transform.TransformPoint(node.position));
                    if (localSpace.y > Mathf.Abs(0.8f * ratio))
                    {
                        TranslatePart(p, -armorTransforms[0].right * ratio);
                    }
                }
                Oldthickness = armorthickness;
            }            
        }
        private void OnEditorShipModifiedEvent(ShipConstruct data)
        {
            UpdateThickness();
        }
    }
}