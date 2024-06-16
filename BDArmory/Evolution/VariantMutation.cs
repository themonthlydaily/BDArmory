using System;
using System.Collections.Generic;

namespace BDArmory.Evolution
{
    public interface VariantMutation
    {
        public ConfigNode Apply(ConfigNode craft, VariantEngine engine, float newValue = float.NaN);
        public Variant GetVariant(string id, string name);
    }
}
