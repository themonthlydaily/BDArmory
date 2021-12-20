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

        public bool isCurvedPanel = false;

        private float updateTimer = 0;
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
            if (ThicknessTransform != null)
            {
                isCurvedPanel = true;
            }
            UI_FloatRange AWidth = (UI_FloatRange)Fields["Width"].uiControlEditor;
            if (isCurvedPanel)
            {
                AWidth.maxValue = 4f;
                AWidth.minValue = 0f;
                AWidth.stepIncrement = 1;
                Fields["Width"].guiName = "#LOC_BDArmory_CylArmorScale";
            }
            AWidth.onFieldChanged = AdjustWidth;
            UI_FloatRange ALength = (UI_FloatRange)Fields["Length"].uiControlEditor;
            ALength.onFieldChanged = AdjustLength;
            if (HighLogic.LoadedSceneIsEditor)
            {
                armor = GetComponent<HitpointTracker>();
            }
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
        }

        public void AdjustWidth(BaseField field, object obj)
        {
            Width = Mathf.Clamp(Width, 0.5f, 16f);
            if (!isCurvedPanel)
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
            updateArmorStats();
            UpdateNodes(Mathf.Max(Mathf.CeilToInt(Length / 2), Mathf.CeilToInt(Width / 2)));
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
            UpdateNodes(Mathf.Max(Mathf.CeilToInt(Length / 2), Mathf.CeilToInt(Width / 2)));
            HandleLengthChange(Length, OldLength);
            OldLength = Length;
        }

        public void UpdateScale(float width, float length)
        {
            Width = width;
            Length = length;
            if (!isCurvedPanel)
            {
                for (int i = 0; i < armorTransforms.Length; i++)
                {
                    armorTransforms[i].localScale = new Vector3(Width, Length, Mathf.Clamp((armor.Armor / 10), 0.1f, 1500));
                }
            }
            else
            {
                for (int i = 0; i < armorTransforms.Length; i++)
                {
                    armorTransforms[i].localScale = new Vector3(Width, Length, Width);
                }
            }
            updateArmorStats();
            UpdateNodes(Mathf.Max(Mathf.CeilToInt(Width / 2), Mathf.CeilToInt(length / 2)));
        }

        public void UpdateNodes(int size)
        {
            foreach (AttachNode node in part.attachNodes)
            {
                node.size = size;
                node.breakingForce = size * 100;
                node.breakingTorque = size * 100;
            }
        }
        /// //////////////////////////////////
        //Borrowed from ProceduralParts
        public virtual void HandleLengthChange(float length, float oldLength)
        {
            float trans = length - oldLength;

            MoveNode(N1, N1.position + (N1Transform.forward * (trans / 2)));
            if (N1.attachedPart is Part N1pushTarget)
            {
                TranslatePart(N1pushTarget, N1Transform.forward * (trans / 2));
            }
            MoveNode(N3, N3.position + (N3Transform.forward * (trans / 2)));
            if (N3.attachedPart is Part N3pushTarget)
            {
                TranslatePart(N3pushTarget, -N3Transform.forward * (trans / 2));
            }
            //nope, missing something; this isn't moving srfattach parts. maybe turn the bool into a within threshold test for stuff mounted on edges of plate
            foreach (Part p in part.children)
            {
                if (p.FindAttachNodeByPart(part) is AttachNode node && node.nodeType == AttachNode.NodeType.Surface)
                {
                    Vector3 localSpace = part.transform.InverseTransformPoint(node.owner.transform.TransformPoint(node.position));
                    if (localSpace.z == (oldLength / 2) || localSpace.z == -(oldLength / 2))
                    {
                        float ratio = length / oldLength;
                        localSpace.z *= ratio;
                        MovePartByAttachNode(node, localSpace); //this an be translatePart instead; just need to add some code to grab part if above/below, and if bounted on variable thickness face of plate
                    }
                }
            }
            
        }
   
        public virtual void HandleWidthChange(float width, float oldWidth)
        {
            float trans = width - oldWidth;
            MoveNode(N2, N2.position + (-N2Transform.right * (trans / 2)));
            if (N2.attachedPart is Part N2pushTarget)
            {
                TranslatePart(N2pushTarget, -N2Transform.right * (trans / 2));
            }
            MoveNode(N4, N4.position + (N4Transform.right * (trans / 2)));
            if (N4.attachedPart is Part N4pushTarget)
            {
                TranslatePart(N4pushTarget, N4Transform.right * (trans / 2));
            }
            
            foreach (Part p in part.children)
            {
                if (p.FindAttachNodeByPart(part) is AttachNode node && node.nodeType == AttachNode.NodeType.Surface)
                {
                    Vector3 localSpace = part.transform.InverseTransformPoint(node.owner.transform.TransformPoint(node.position));
                    if (localSpace.x == (oldWidth / 2) || localSpace.x == -(oldWidth / 2))
                    {
                        float ratio = width / oldWidth;
                        localSpace.x *= ratio;
                        MovePartByAttachNode(node, localSpace); //this an be translatePart instead; just need to add some code to grab part if above/below, and if bounted on variable thickness face of plate
                    }
                }
            }
            
        }
        private void MoveNode(AttachNode node, Vector3 destination) 
        {
            if (Vector3.Distance(node.position, destination) > 0.01f)
            {
                if (node.nodeTransform is Transform)
                {
                    node.nodeTransform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    node.nodeTransform.Translate(destination, Space.Self); //so lets try this method again - TESTME

                    //Vector3 worldSpaceTranslation = part.transform.TransformVector(destination); //this isn't moving nodes, and I don't know why; gone through ProcParts code and I'm net seeing what I've missed
                    //node.nodeTransform.transform.Translate(worldSpaceTranslation, Space.World);

                    Debug.Log($"[BDAA] MoveNode() moved {node.id} from {node.position} to {destination} = {part.transform.TransformPoint(destination)} (worldspace) via transform translation");
                }
                else
                {
                    node.position = destination;
                    Debug.Log($"[BDAA] MoveNode() moved {node.id} from {node.position} to {destination} = {part.transform.TransformPoint(destination)} (worldspace) via NodePos = destination ");
                }
                node.originalPosition = node.position;
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
            //Will need to figure out surface-attached parts, these currently are not being affected - SI
        }
        public virtual void MovePartByAttachNode(AttachNode node, Vector3 coord)
        {
            Vector3 oldWorldSpace = node.owner.transform.TransformPoint(node.position);
            Vector3 newWorldspace = part.transform.TransformPoint(coord);
            node.owner.transform.Translate(newWorldspace - oldWorldSpace, Space.World);
        }
        /// ///////////////////////////

        public void updateArmorStats()
        {
            if (isCurvedPanel)
            {
                armor.armorVolume = (Length * (Mathf.Clamp(Width, 0.5f, 4) * Mathf.Clamp(Width, 0.5f, 4))); //gives surface area for 1/4 cyl srf area
            }
            else
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
            armorthickness = Mathf.Clamp((armor.Armor / 10), 0.1f, 1500);
            if (!isCurvedPanel)
            {
                for (int i = 0; i < armorTransforms.Length; i++)
                {
                    armorTransforms[i].localScale = new Vector3(Width, Length, armorthickness);
                }
            }
            else
            {
                ThicknessTransform.localScale = new Vector3(armorthickness, 1, armorthickness);
            }
            /*
            foreach (Part p in part.children)
            {
                if (p.FindAttachNodeByPart(part) is AttachNode node && node.nodeType == AttachNode.NodeType.Surface)
                {
                    Vector3 localSpace = part.transform.InverseTransformPoint(node.owner.transform.TransformPoint(node.position));
                    if (localSpace.y > part.transform.position.y)
                    {
                        float ratio = (armorthickness - Oldthickness) / 1000;
                        localSpace.y += ratio;
                        MovePartByAttachNode(node, localSpace); 
                    }
                }
            }
            Oldthickness = armorthickness;
            */
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