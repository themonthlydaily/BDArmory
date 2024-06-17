﻿using System;
using System.Collections.Generic;
using UnityEngine;

namespace BDArmory.Evolution
{
    public class WeaponManagerNudgeMutation : VariantMutation
    {
        const string moduleName = "MissileFire";

        public string paramName;
        public float modifier;
        public string key;
        public int direction;
        private List<MutatedPart> mutatedParts = new List<MutatedPart>();
        public WeaponManagerNudgeMutation(string paramName, float modifier, string key, int direction)
        {
            this.paramName = paramName;
            this.modifier = modifier;
            this.key = key;
            this.direction = direction;
        }

        public ConfigNode Apply(ConfigNode craft, VariantEngine engine, float newValue = float.NaN)
        {
            Debug.Log("[BDArmory.WeaponManagerNudgeMutation]: Evolution WeaponManagerNudgeMutation applying");
            List<ConfigNode> matchingNodes = engine.FindModuleNodes(craft, "MissileFire");
            if (matchingNodes.Count == 1)
            {
                Debug.Log("[BDArmory.WeaponManagerNudgeMutation]: Evolution WeaponManagerNudgeMutation found module");
                var node = matchingNodes[0];
                float existingValue;
                float.TryParse(node.GetValue(paramName), out existingValue);
                Debug.Log(string.Format("[BDArmory.WeaponManagerNudgeMutation]: Evolution WeaponManagerNudgeMutation found existing value {0} = {1}", paramName, existingValue));

                if (float.IsNaN(newValue))
                {
                    newValue = existingValue * (1 + modifier);
                }

                if (engine.MutateNode(node, paramName, newValue))
                {
                    ConfigNode partNode = engine.FindParentPart(craft, node);
                    if( partNode == null )
                    {
                        Debug.Log("[BDArmory.WeaponManagerNudgeMutation]: Evolution WeaponManagerNudgeMutation failed to find parent part for module");
                        return craft;
                    }
                    string partName = partNode.GetValue("part");
                    Debug.Log(string.Format("[BDArmory.WeaponManagerNudgeMutation]: Evolution WeaponManagerNudgeMutation mutated part {0}, module {1}, param {2}, existing: {3}, value: {4}", partName, moduleName, paramName, existingValue, newValue));
                    mutatedParts.Add(new MutatedPart(partName, moduleName, paramName, existingValue, newValue));
                }
                else
                {
                    Debug.Log(string.Format("[BDArmory.WeaponManagerNudgeMutation]: Evolution WeaponManagerNudgeMutation unable to mutate {0}", paramName));
                }
            }
            else
            {
                Debug.Log("[BDArmory.WeaponManagerNudgeMutation]: Evolution WeaponManagerNudgeMutation wrong number of weapon managers");
            }
            return craft;
        }

        public Variant GetVariant(string id, string name)
        {
            return new Variant(id, name, mutatedParts, key, direction);
        }
    }
}
