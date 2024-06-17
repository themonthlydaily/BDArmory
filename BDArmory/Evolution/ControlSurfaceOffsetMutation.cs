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

            if (paramName.EndsWith("X"))
            {
                existingValue = origAttPos.x;
            }
            else if (paramName.EndsWith("Y"))
            {
                existingValue = origAttPos.y;
            }
            else if (paramName.EndsWith("Z"))
            {
                existingValue = origAttPos.z;
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

            if (paramName.EndsWith("X"))
            {
                origAttPos.x += offsetDist;
                origPos.x += offsetDist;
            }
            else if (paramName.EndsWith("Y"))
            {
                origAttPos.y += offsetDist;
                origPos.y += offsetDist;
            }
            else if (paramName.EndsWith("Z"))
            {
                origAttPos.z += offsetDist;
                origPos.z += offsetDist;
            }

            // Apply mutation and log if successful
            if(engine.MutateStringNode(partNode, "pos", KSPUtil.WriteVector(origPos)) && engine.MutateStringNode(partNode, "attPos", KSPUtil.WriteVector(origAttPos))) 
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
