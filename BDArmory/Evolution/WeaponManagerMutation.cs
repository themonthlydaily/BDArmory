using System;
using System.Collections.Generic;
using UnityEngine;

namespace BDArmory.Evolution
{
    public class WeaponManagerMutation : VariantMutation
    {
        const string moduleName = "MissileFire";

        public string paramName;
        public float value;
        public string key;
        public int direction;
        private List<MutatedPart> mutatedParts = new List<MutatedPart>();
        public WeaponManagerMutation(string paramName, float value, string key, int direction)
        {
            this.paramName = paramName;
            this.value = value;
            this.key = key;
            this.direction = direction;
        }

        public ConfigNode Apply(ConfigNode craft, VariantEngine engine, float newValue = float.NaN)
        {
            List<ConfigNode> matchingNodes = engine.FindModuleNodes(craft, moduleName);
            if( matchingNodes.Count == 1 )
            {
                var node = matchingNodes[0];
                float existingValue;
                float.TryParse(node.GetValue(paramName), out existingValue);

                if (float.IsNaN(newValue))
                {
                    newValue = value;
                }

                if ( engine.MutateNode(node, paramName, newValue) )
                {
                    ConfigNode partNode = engine.FindParentPart(craft, node);
                    string partName = partNode.GetValue("part");
                    mutatedParts.Add(new MutatedPart(partName, moduleName, paramName, existingValue, newValue));
                }
            }
            else
            {
                Debug.Log("[BDArmory.WeaponManagerMutation]: Evolution WeaponManagerMutation wrong number of weapon managers");
            }
            return craft;
        }

        public Variant GetVariant(string id, string name)
        {
            return new Variant(id, name, mutatedParts, key, direction);
        }
    }
}
