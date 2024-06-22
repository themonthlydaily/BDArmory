using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BDArmory.Evolution
{
    public class ControlSurfaceOffsetMutation : VariantMutation
    {
        const string moduleName = "ModuleControlSurface";

        public string[] partNames;
        public string paramName;
        public float modifier;
        public string key;
        public int direction;
        private List<MutatedPart> mutatedParts = new List<MutatedPart>();

        public ControlSurfaceOffsetMutation(string[] partNames, string paramName, float modifier, string key, int direction)
        {
            Debug.Log("Starting ControlSurfaceOffsetMutation.ControlSurfaceOffsetMutation");
            this.partNames = partNames;
            this.paramName = paramName;
            this.modifier = modifier;
            this.key = key;
            this.direction = direction;
        }

        public ConfigNode Apply(ConfigNode craft, VariantEngine engine, float newValue = float.NaN)
        {
            // Build node map of copy
            Dictionary<string, ConfigNode> mutationNodeMap = engine.BuildNodeMap(craft);

            // Apply mutation to all symmetric parts
            Debug.Log("[BDArmory.ControlSurfaceOffsetMutation]: Evolution ControlSurfaceOffsetMutation applying");
            Dictionary<string, ConfigNode> matchingNodeMap = new Dictionary<string, ConfigNode>();
            foreach (var partName in partNames)
            {
                matchingNodeMap[partName] = engine.GetNode(partName, mutationNodeMap);
            }
            MutateMap(matchingNodeMap, craft, engine, newValue);

            return craft;
        }

        public Variant GetVariant(string id, string name)
        {
            Debug.Log("Starting ControlSurfaceOffsetMutation.GetVariant");
            return new Variant(id, name, mutatedParts, key, direction);
        }

        private void MutateMap(Dictionary<string, ConfigNode> nodeMap, ConfigNode craft, VariantEngine engine, float value = float.NaN)
        {
            Debug.Log("Starting ControlSurfaceOffsetMutation.MutateMap");
            foreach (var partNames in nodeMap.Keys)
            {
                foreach (var partName in partNames.Split(','))
                {
                    MutateNode(nodeMap, engine, partName, value);
                }
            }
        }

        private void MutateNode(Dictionary<string, ConfigNode> nodeMap, VariantEngine engine, string partName, float value = float.NaN)
        {
            Debug.Log("Starting ControlSurfaceOffsetMutation.MutateNode");
            ConfigNode partNode = nodeMap[partName];
            Debug.Log(string.Format("Evolution ControlSurfaceOffsetMutation found existing value {0} = {1}, type{2}", "attPos0", partNode.GetValue("attPos0"), partNode.GetValue("attPos0").GetType()));
            Debug.Log(string.Format("Evolution ControlSurfaceOffsetMutation found existing value {0} = {1}, type{2}", "attPos", partNode.GetValue("attPos"), partNode.GetValue("attPos").GetType()));
            Debug.Log(string.Format("Evolution ControlSurfaceOffsetMutation found existing value {0} = {1}, type{2}", "pos", partNode.GetValue("pos"), partNode.GetValue("pos").GetType()));
            // Debug.Log(string.Format("Evolution ControlSurfaceOffsetMutation found existing value {0} = {1}", "position", partNode.GetValue("position"))); // Does not exist in node format
            // To modify part position, need to modify "pos" value, which appears to be saved from the part.transform.position property by method ShipConstruct.SaveShip() 

            //ConfigNode node = engine.FindModuleNodes(partNode, moduleName).First();
            float existingValue;
            Vector3 origAttPos;
            Vector3 origPos;
            // TODO: Apply rotation appropriately here in the future
            // Get the corresponding vector components
            origAttPos = KSPUtil.ParseVector3(partNode.GetValue("attPos"));
            origPos = KSPUtil.ParseVector3(partNode.GetValue("pos"));
            Quaternion attRot = KSPUtil.ParseQuaternion(partNode.GetValue("attRot"));
            Quaternion attRot0 = KSPUtil.ParseQuaternion(partNode.GetValue("attRot0"));
            Quaternion rot = KSPUtil.ParseQuaternion(partNode.GetValue("rot"));
            // Based on OnOffsetGizmoUpdate method, this is how the offsetGizmo update is applied/aligned to part rotation
            //selectedPart.attRotation * childToParent.position
            Vector3 localAttPos = Quaternion.Inverse(attRot) * origAttPos;
            Vector3 localAttPos0 = Quaternion.Inverse(attRot0) * origAttPos;
            Debug.Log(string.Format("Evolution ControlSurfaceOffsetMutation detransformed position attRot {0}", KSPUtil.WriteVector(localAttPos)));
            // attRot0 appears to store part rotation on attachment relative to parent
            // If rotation gizmo applied to part, it will modify rot property
            // Not sure what (if anything) modifies 
            Debug.Log(string.Format("Evolution ControlSurfaceOffsetMutation detransformed position attRot0 {0}", KSPUtil.WriteVector(localAttPos0)));
            Debug.Log(string.Format("Evolution ControlSurfaceOffsetMutation detransformed position rot {0}", KSPUtil.WriteVector(Quaternion.Inverse(rot) * origAttPos)));
            Debug.Log(string.Format("Evolution ControlSurfaceOffsetMutation detransformed position attRot * attRot0 {0}", KSPUtil.WriteVector(Quaternion.Inverse(attRot * attRot0) * origAttPos)));
            Debug.Log(string.Format("Evolution ControlSurfaceOffsetMutation detransformed position attRot0 * attRot {0}", KSPUtil.WriteVector(Quaternion.Inverse(attRot0 * attRot) * origAttPos)));
            Debug.Log(string.Format("Evolution ControlSurfaceOffsetMutation detransformed position rot * attRot0 * attRot {0}", KSPUtil.WriteVector(Quaternion.Inverse(rot * attRot0 * attRot) * origAttPos)));

            if (paramName.EndsWith("X"))
            {
                existingValue = localAttPos0.x;
            }
            else if (paramName.EndsWith("Y"))
            {
                existingValue = localAttPos0.y;
            }
            else if (paramName.EndsWith("Z"))
            {
                existingValue = localAttPos0.z;
            }
            else
            {
                existingValue = 1.0f;
            }

            Debug.Log(string.Format("Evolution ControlSurfaceOffsetMutation found existing value {0} = {1}", paramName, existingValue));
            
            if (float.IsNaN(value))
            {
                value = existingValue + (10f * modifier);
                value = Mathf.Clamp(value, -10f, 10f); // Clamp part repositioning to reasonably small vaues  (10 m).
                // TODO: Insert logic here using vessel checker, collision checker, etc. util from base KSP ()
            }

            // Recalculate final offset_distance value
            float offsetDist = value - existingValue;
            Vector3 localNudgeVector = Vector3.zero;

            if (paramName.EndsWith("X"))
            {
                localNudgeVector.x += offsetDist;
            }
            else if (paramName.EndsWith("Y"))
            {
                localNudgeVector.y += offsetDist;
            }
            else if (paramName.EndsWith("Z"))
            {
                localNudgeVector.z += offsetDist;
            }

            // This is correct for how to translate the offset back to the pos property (this actually affects the part placement on the craft)
            origPos += rot * localNudgeVector;

            // Appears to be correct for attPos modification. Still need to validate that attRot is not needed as well here. Final testing required
            origAttPos += attRot0 * localNudgeVector;
            Debug.Log(string.Format("Evolution ControlSurfaceOffsetMutation post mutate detransformed attRot0 * origAttPos update {0}", KSPUtil.WriteVector(Quaternion.Inverse(attRot0) * origAttPos)));
            Debug.Log(string.Format("Evolution ControlSurfaceOffsetMutation post mutate detransformed rot * origAttPos update {0}", KSPUtil.WriteVector(Quaternion.Inverse(rot) * origAttPos)));
            Debug.Log(string.Format("Evolution ControlSurfaceOffsetMutation post mutate detransformed position attRot * attRot0 {0}", KSPUtil.WriteVector(Quaternion.Inverse(attRot * attRot0) * origAttPos)));
            Debug.Log(string.Format("Evolution ControlSurfaceOffsetMutation post mutate detransformed position attRot0 * attRot {0}", KSPUtil.WriteVector(Quaternion.Inverse(attRot0 * attRot) * origAttPos)));
            Debug.Log(string.Format("Evolution ControlSurfaceOffsetMutationpost post mutate detransformed position rot * attRot0 * attRot {0}", KSPUtil.WriteVector(Quaternion.Inverse(rot * attRot0 * attRot) * origAttPos)));

            // Apply mutation and log if successful
            if (engine.MutateStringNode(partNode, "pos", KSPUtil.WriteVector(origPos)) && engine.MutateStringNode(partNode, "attPos", KSPUtil.WriteVector(origAttPos))) 
            {
                Debug.Log(string.Format("Evolution ControlSurfaceOffsetMutation mutated part {0}, module {1}, param {2}, existing: {3}, value: {4}", partName, moduleName, paramName, existingValue, value));
                mutatedParts.Add(new MutatedPart(partName, moduleName, paramName, existingValue, value));
            }
            else
            {
                Debug.Log(string.Format("Evolution ControlSurfaceOffsetMutation unable to mutate part {0}, module {1}, param {2}, existing: {3}, value: {4}", partName, moduleName, paramName, existingValue, value));
            }
        }
    }
}
