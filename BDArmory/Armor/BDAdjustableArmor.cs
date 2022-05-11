using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using BDArmory.Damage;

namespace BDArmory.Armor
{
    public class BDAdjustableArmor : PartModule
    {
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_ArmorWidth"),//Engage Range Min
 UI_FloatRange(minValue = 0.5f, maxValue = 16, stepIncrement = 0.1f, scene = UI_Scene.Editor)]
        public float Width = 1;

        private float OldWidth = 1;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_ArmorLength"),//Engage Range Min
 UI_FloatRange(minValue = 0.5f, maxValue = 16, stepIncrement = 0.1f, scene = UI_Scene.Editor)]
        public float Length = 1;

        private float OldLength = 1;

        //public bool isCurvedPanel = false;
        private float armorthickness = 1;
        private float Oldthickness = 1;

        [KSPField]
        public bool isTriangularPanel = false;

        [KSPField]
        public string TriangleType;

        [KSPField]
        public string ArmorTransformName = "ArmorTransform"; //transform of armor panel mesh/box collider
        Transform[] armorTransforms;

        [KSPField]
        public string ThicknessTransformName = "ThicknessTransform"; //name of armature to control thickness of curved panels
        Transform ThicknessTransform;

        [KSPField] public string stackNodePosition;

        Dictionary<string, Vector3> originalStackNodePosition;

        HitpointTracker armor;

        public override void OnStart(StartState state)
        {
            armorTransforms = part.FindModelTransforms(ArmorTransformName);
            ThicknessTransform = part.FindModelTransform(ThicknessTransformName);
            if (HighLogic.LoadedSceneIsEditor)
            {
                ParseStackNodePosition();
                StartCoroutine(DelayedUpdateStackNode());
            }
            UpdateThickness(true);
            UI_FloatRange AWidth = (UI_FloatRange)Fields["Width"].uiControlEditor;
            AWidth.onFieldChanged = AdjustWidth;
            UI_FloatRange ALength = (UI_FloatRange)Fields["Length"].uiControlEditor;
            ALength.onFieldChanged = AdjustLength;
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorShipModified.Add(OnEditorShipModifiedEvent);
            }
            armor = GetComponent<HitpointTracker>();
            UpdateScale(Width, Length);
        }
        void ParseStackNodePosition()
        {
            originalStackNodePosition = new Dictionary<string, Vector3>();
            string[] nodes = stackNodePosition.Split(new char[] { ';' });
            for (int i = 0; i < nodes.Length; i++)
            {
                string[] split = nodes[i].Split(new char[] { ',' });
                string id = split[0];
                Vector3 position = new Vector3(float.Parse(split[1]), float.Parse(split[2]), float.Parse(split[3]));
                originalStackNodePosition.Add(id, position);
            }
        }

        IEnumerator DelayedUpdateStackNode()
        {
            yield return null;
            UpdateStackNode();
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
            updateArmorStats();
            UpdateStackNode();
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
            UpdateStackNode();
            OldLength = Length;
        }

        public void UpdateScale(float width, float length)
        {
            Width = width;
            Length = length;
            for (int i = 0; i < armorTransforms.Length; i++)
            {
                armorTransforms[i].localScale = new Vector3(Width, Length, Mathf.Clamp((armor.Armor / 10), 0.1f, 1500));
            }
			updateArmorStats();
            UpdateStackNode();
        }

        public void UpdateStackNode()
        {
            using (List<AttachNode>.Enumerator stackNode = part.attachNodes.GetEnumerator())
                while (stackNode.MoveNext())
                {
                    if (stackNode.Current?.nodeType != AttachNode.NodeType.Stack ||
                        !originalStackNodePosition.ContainsKey(stackNode.Current.id)) continue;
                    Debug.Log("[ADJUSTABLEARMOR] Adjusting Node for " + stackNode.Current.id);

                        Vector3 delta = Vector3.zero;
                    if (stackNode.Current.id == "top" || stackNode.Current.id == "bottom")
                    {
                        if (Width == 1) continue;
                        stackNode.Current.size = Mathf.CeilToInt(Width / 2);
                        stackNode.Current.breakingForce = Width * 100;
                        stackNode.Current.breakingTorque = Width * 100;
                        Debug.Log("[ADJUSTABLEARMOR] node size and strength updated");
                        Vector3 prevPos = stackNode.Current.position;
                        int offsetScale = 2;
                        if (TriangleType == "Eqilateral")
                        {
                            offsetScale = 4;
                        }
                        if (stackNode.Current.id == "top")
                            stackNode.Current.position.x = originalStackNodePosition[stackNode.Current.id].x + ((Width - 1) / offsetScale); //if eqi tri this needs to be /4
                        else
                            stackNode.Current.position.x = originalStackNodePosition[stackNode.Current.id].x - ((Width - 1) / offsetScale);// and a right tri hypotenuse node shouldn't move at all
                        delta = stackNode.Current.position - prevPos;
                        Debug.Log("[ADJUSTABLEARMOR] Node moved");
                        if (stackNode.Current.attachedPart is Part pushTarget)
                        {
                            List<Part>.Enumerator p = part.children.GetEnumerator();
                            while (p.MoveNext())
                            {
                                if (p.Current == null) continue;
                                if (pushTarget != stackNode.Current.attachedPart) continue;
                                Vector3 worldDelta = part.transform.TransformVector(delta);
                                pushTarget.transform.position += worldDelta;
                            }
                            Debug.Log("[ADJUSTABLEARMOR] attached part moved");
                        }
                    }
                    if (stackNode.Current.id == "left" || stackNode.Current.id == "right")
                    {
                        if (Length == 1) continue;
                        stackNode.Current.size = Mathf.CeilToInt(Length / 2);
                        stackNode.Current.breakingForce = Length * 100;
                        stackNode.Current.breakingTorque = Length * 100;
                        Vector3 prevPos = stackNode.Current.position;
                        if (stackNode.Current.id == "right")
                            stackNode.Current.position.z = originalStackNodePosition[stackNode.Current.id].z + ((Length) / 2);
                        else
                            stackNode.Current.position.z = originalStackNodePosition[stackNode.Current.id].z - ((Length) / 2);
                        delta = stackNode.Current.position - prevPos;
                        if (stackNode.Current.attachedPart is Part pushTarget)
                        {
                            List<Part>.Enumerator p = part.children.GetEnumerator();
                            while (p.MoveNext())
                            {
                                if (p.Current == null) continue;
                                if (pushTarget != stackNode.Current.attachedPart) continue;
                                Vector3 worldDelta = part.transform.TransformVector(delta);
                                pushTarget.transform.position += worldDelta;
                            }
                        }
                    }
                }
        }

        public void updateArmorStats()
        {
            armor.armorVolume = (Width * Length);
            if (isTriangularPanel)
            {
                armor.armorVolume /= 2;
            }
            armor.ArmorSetup(null, null);
        }
        void UpdateThickness(bool onLoad = false)
        {
            if (armor != null && armorTransforms != null)
            {
                armorthickness = Mathf.Clamp((armor.Armor / 10), 0.1f, 1500);

                    if (armorthickness != Oldthickness)
                    {
                        for (int i = 0; i < armorTransforms.Length; i++)
                        {
                            armorTransforms[i].localScale = new Vector3(Width, Length, armorthickness);
                        }
                    }
            }
            else
            {
                //if (armor == null) Debug.Log("[BDAAdjustableArmor] No HitpointTracker found! aborting UpdateThickness()!");
                //if (armorTransforms == null) Debug.Log("[BDAAdjustableArmor] No ArmorTransform found! aborting UpdateThickness()!");
                return;
            }
            if (onLoad) return; //don't adjust part placement on load
            /*
            if (armorthickness != Oldthickness)
            {
                float ratio = (armorthickness - Oldthickness) / 100;

                Vector3 prevPos = new Vector3(0f, Oldthickness / 100, 0f);
                Vector3 delta = new Vector3(0f, armorthickness / 100, 0f);
                Vector3 worldDelta = part.transform.TransformVector(delta);
                List<Part>.Enumerator p = part.children.GetEnumerator();
                while (p.MoveNext())
                {
                    if (p.Current == null) continue;
                    if (p.Current.FindAttachNodeByPart(part) is AttachNode node && node.nodeType == AttachNode.NodeType.Surface)
                    {

                        p.Current.transform.position += worldDelta;
                    }
                }
                Oldthickness = armorthickness;
            }
            */
        }
        private void OnEditorShipModifiedEvent(ShipConstruct data)
        {
            UpdateThickness();
        }
    }
}