using System.Linq;
using System.Reflection;
using UnityEngine;

using System.IO;
using System.Collections.Generic;
using System;
using BDArmory.UI;

namespace BDArmory.Settings
{
	public class CompSettings
	{
		// Settings overrides for AI/WM settings for competition rules compliance

		static readonly string CompSettingsPath = Path.GetFullPath(Path.Combine(KSPUtil.ApplicationRootPath, "GameData/BDArmory/PluginData/Comp_settings.cfg"));
		static public bool CompOverridesEnabled = false;
		static readonly Dictionary<string, object> ComPOverrides = new()
		{
				// FIXME there's probably a few more things that could get set here for AI/WM override if needed in specific rounds.
				//AI Min/max Alt?
				{"PS_EXTEND_TIMEOUT", -1},
				{"PS_EXTEND_DIST", -1},
				{"PS_MONOCOCKPIT_VIEWRANGE", -1},
				{"PS_DUALCOCKPIT_VIEWRANGE", -1},
				{"PS_COCKPIT_FOV", -1},
				{"PS_AVOID_THRESH", -1},
				{"PS_AVOID_LA", -1},
				{"PS_AVOID_STR", -1 },
				{"PS_IDLE_SPEED", -1},
				{"PS_DISABLE_SAS", false},
		};

        /// <summary>
        /// Load RWP settings from file.
        /// </summary>
        public static void Load()
        {
            if (!File.Exists(CompSettingsPath))
            {
                Debug.LogError($"[BDArmory.CompSettings]: Override settings not present, skipping.");
                return;
            }
            ConfigNode fileNode = ConfigNode.Load(CompSettingsPath);
            if (!fileNode.HasNode("AIWMChecks")) return;
            CompOverridesEnabled = true;
            ConfigNode settings = fileNode.GetNode("AIWMChecks");

            foreach (ConfigNode.Value fieldNode in settings.values)
            {
                var field = typeof(BDArmorySettings).GetField(fieldNode.name, BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
                if (field == null)
                {
                    Debug.LogError($"[BDArmory.CompSettings]: Unknown field {fieldNode.name} when loading AI/WM override settings.");
                    continue;
                }
                var fieldValue = BDAPersistentSettingsField.ParseValue(field.FieldType, fieldNode.value);
                ComPOverrides[fieldNode.name] = fieldValue; // Add or set the override.
            }
            if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.CompSettings]: Setting Comp AI/WM overrides");
            foreach (var setting in ComPOverrides.Keys)
            {
                var field = typeof(BDArmorySettings).GetField(setting, BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
                try
                {
                    field.SetValue(null, Convert.ChangeType(ComPOverrides[setting], field.FieldType)); // Convert the type to the correct type (e.g., double vs float) so unboxing works correctly.
                    if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.CompSettings]: setting value {ComPOverrides[setting]} to {setting}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[BDArmory.CompSettings]: Failed to set value {ComPOverrides[setting]} for {setting}: {e.Message}");
                }
            }
        }
    }
}