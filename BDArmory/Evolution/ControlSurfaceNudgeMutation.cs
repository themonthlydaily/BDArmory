﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BDArmory.Evolution
{
    public class ControlSurfaceNudgeMutation : VariantMutation
    {
        const string moduleName = "ModuleControlSurface";

        public string[] partNames;
        public string paramName;
        public float modifier;
        public string key;
        public int direction;
        private List<MutatedPart> mutatedParts = new List<MutatedPart>();

        public ControlSurfaceNudgeMutation(string[] partNames, string paramName, float modifier, string key, int direction)
        {
            this.partNames = partNames;
            this.paramName = paramName;
            this.modifier = modifier;
            this.key = key;
            this.direction = direction;
        }

        public ConfigNode Apply(ConfigNode craft, VariantEngine engine, float newValue = float.NaN)
        {
            ConfigNode mutatedCraft = craft.CreateCopy();

            // Build node map of copy
            Dictionary<string, ConfigNode> mutationNodeMap = engine.BuildNodeMap(mutatedCraft);

            // Apply mutation to all symmetric parts
            Debug.Log("[BDArmory.ControlSurfaceNudgeMutation]: Evolution ControlSurfaceNudgeMutation applying");
            Dictionary<string, ConfigNode> matchingNodeMap = new Dictionary<string, ConfigNode>();
            foreach (var partName in partNames)
            {
                matchingNodeMap[partName] = engine.GetNode(partName, mutationNodeMap);
            }
            MutateMap(matchingNodeMap, mutatedCraft, engine, newValue);

            return mutatedCraft;
        }

        public Variant GetVariant(string id, string name)
        {
            return new Variant(id, name, mutatedParts, key, direction);
        }

        private void MutateMap(Dictionary<string, ConfigNode> nodeMap, ConfigNode craft, VariantEngine engine, float value = float.NaN)
        {
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
            ConfigNode partNode = nodeMap[partName];
            ConfigNode node = engine.FindModuleNodes(partNode, moduleName).First();
            float existingValue;
            float.TryParse(node.GetValue(paramName), out existingValue);
            Debug.Log(string.Format("Evolution ControlSurfaceNudgeMutation found existing value {0} = {1}", paramName, existingValue));


            if (float.IsNaN(value))
            {
                value = existingValue * (1 + modifier);
            }

            if (engine.MutateNode(node, paramName, value))
            {
                value = Mathf.Clamp(value, -150f, 150f); // Clamp control surfaces to their limits (150%).
                Debug.Log(string.Format("Evolution ControlSurfaceNudgeMutation mutated part {0}, module {1}, param {2}, existing: {3}, value: {4}", partName, moduleName, paramName, existingValue, value));
                mutatedParts.Add(new MutatedPart(partName, moduleName, paramName, existingValue, value));
            }
            else
            {
                Debug.Log(string.Format("Evolution ControlSurfaceNudgeMutation unable to mutate {0}", paramName));
            }
        }
    }
}
