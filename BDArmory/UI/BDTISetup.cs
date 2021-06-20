using System;
using System.Collections;
using UnityEngine;
using KSP.UI.Screens;
using System.Collections.Generic;
using BDArmory.Modules;
using BDArmory.Misc;
using System.Linq;
using KSP.Localization;

/*
* *Milestone 6: Figure out how to have TI activation toggle the F4 SHOW_LABELS (or is it Flt_Show_labels?) method to sim a keypress?
*/
namespace BDArmory.UI
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	class BDTISetup : MonoBehaviour
	{
		private ApplicationLauncherButton toolbarButton = null;
		public static Rect WindowRectGUI;

		private string windowTitle = Localizer.Format("#LOC_BDArmory_Icons_title");
		public static BDTISetup Instance = null;
		public static GUIStyle TILabel;
		private bool showTeamIconGUI = false;
		float toolWindowWidth = 250;
		float toolWindowHeight = 150;
		float teamWindowHeight = 25;
		public string selectedTeam;
		public bool UpdateTeamColor = false;
		private float updateList = 0;
		private bool maySavethisInstance = false;

		public SortedList<string, List<MissileFire>> weaponManagers = new SortedList<string, List<MissileFire>>();

		public static string textureDir = "BDArmory/Textures/Icons/";

		//legacy version check
		bool LegacyTILoaded = false;
		bool showPSA = false;
		private Texture2D dit;
		public Texture2D TextureIconDebris
		{
			get { return dit ? dit : dit = GameDatabase.Instance.GetTexture(textureDir + "debrisIcon", false); }
		}
		private Texture2D mit;
		public Texture2D TextureIconMissile
		{
			get { return mit ? mit : mit = GameDatabase.Instance.GetTexture(textureDir + "missileIcon", false); }
		}
		private Texture2D rit;
		public Texture2D TextureIconRocket
		{
			get { return rit ? rit : rit = GameDatabase.Instance.GetTexture(textureDir + "rocketIcon", false); }
		}
		private Texture2D ti7;
		public Texture2D TextureIconGeneric
		{
			get { return ti7 ? ti7 : ti7 = GameDatabase.Instance.GetTexture(textureDir + "Icon_Generic", false); }
		}
		private Texture2D ti1A;
		public Texture2D TextureIconShip
		{
			get { return ti1A ? ti1A : ti1A = GameDatabase.Instance.GetTexture(textureDir + "Icon_Ship", false); }
		}
		private Texture2D ti2A;
		public Texture2D TextureIconPlane
		{
			get { return ti2A ? ti2A : ti2A = GameDatabase.Instance.GetTexture(textureDir + "Icon_Plane", false); }
		}
		private Texture2D ti3A;
		public Texture2D TextureIconRover
		{
			get { return ti3A ? ti3A : ti3A = GameDatabase.Instance.GetTexture(textureDir + "Icon_Rover", false); }
		}
		private Texture2D ti4A;
		public Texture2D TextureIconBase
		{
			get { return ti4A ? ti4A : ti4A = GameDatabase.Instance.GetTexture(textureDir + "Icon_Base", false); }
		}
		private Texture2D ti5A;
		public Texture2D TextureIconProbe
		{
			get { return ti5A ? ti5A : ti5A = GameDatabase.Instance.GetTexture(textureDir + "Icon_Probe", false); }
		}
		private Texture2D ti6A;
		public Texture2D TextureIconSub
		{
			get { return ti6A ? ti6A : ti6A = GameDatabase.Instance.GetTexture(textureDir + "Icon_Sub", false); }
		}

		void Start()
		{
			Instance = this;
			if (HighLogic.LoadedSceneIsFlight)
				maySavethisInstance = true;
			if (ConfigNode.Load(BDTISettings.settingsConfigURL) == null)
			{
				var node = new ConfigNode();
				node.AddNode("IconSettings");
				node.Save(BDTISettings.settingsConfigURL);
			}

			AddToolbarButton();
			LoadConfig();
			UpdateList();

			using (var a = AppDomain.CurrentDomain.GetAssemblies().ToList().GetEnumerator())
				while (a.MoveNext())
				{
					string name = a.Current.FullName.Split(new char[1] { ',' })[0];
					switch (name)
					{
						case "BDATeamIcons":
							LegacyTILoaded = true;
							break;
					}
				}
			if (HighLogic.LoadedSceneIsFlight)
			{
				if (LegacyTILoaded)
				{
					ScreenMessages.PostScreenMessage(Localizer.Format("#LOC_BDArmory_Icons_legacyinstall"), 20.0f, ScreenMessageStyle.UPPER_CENTER);
				}
			}
			TILabel = new GUIStyle();
			TILabel.font = BDArmorySetup.BDGuiSkin.window.font;
			TILabel.fontSize = BDArmorySetup.BDGuiSkin.window.fontSize;
			TILabel.fontStyle = BDArmorySetup.BDGuiSkin.window.fontStyle;
		}

		private void MissileFireOnToggleTeam(MissileFire wm, BDTeam team)
		{
			if (BDTISettings.TEAMICONS)
			{
				UpdateList();
			}
		}
		private void VesselEventUpdate(Vessel v)
		{
			if (BDTISettings.TEAMICONS)
			{
				UpdateList();
			}
		}
		private void Update()
		{
			if (BDTISettings.TEAMICONS)
			{
				updateList -= Time.fixedDeltaTime;
				if (updateList < 0)
				{
					UpdateList();
					updateList = 0.5f; // check team lists less often than every frame
				}
			}
		}
		public Dictionary<String, Color> ColorAssignments = new Dictionary<string, Color>();
		private void UpdateList()
		{
			weaponManagers.Clear();

			using (List<Vessel>.Enumerator v = FlightGlobals.Vessels.GetEnumerator())
				while (v.MoveNext())
				{
					if (v.Current == null || !v.Current.loaded || v.Current.packed)
						continue;
					using (var wms = v.Current.FindPartModulesImplementing<MissileFire>().GetEnumerator())
						while (wms.MoveNext())
							if (wms.Current != null)
							{
								if (!ColorAssignments.ContainsKey(wms.Current.teamString))
								{
									float rnd = UnityEngine.Random.Range(0f, 100f);
									ColorAssignments.Add(wms.Current.Team.Name, Color.HSVToRGB((rnd / 100f), 1f, 1f));
								}
								if (weaponManagers.TryGetValue(wms.Current.Team.Name, out var teamManagers))
									teamManagers.Add(wms.Current);
								else
									weaponManagers.Add(wms.Current.Team.Name, new List<MissileFire> { wms.Current });
								break;
							}
				}
		}

		private void OnDestroy()
		{
			if (toolbarButton)
			{
				ApplicationLauncher.Instance.RemoveModApplication(toolbarButton);
				toolbarButton = null;
			}
			if (maySavethisInstance)
			{
				SaveConfig();
			}
		}

		IEnumerator ToolbarButtonRoutine()
		{
			if (toolbarButton || (!HighLogic.LoadedSceneIsEditor)) yield break;
			while (!ApplicationLauncher.Ready)
			{
				yield return null;
			}
			AddToolbarButton();
		}

		void AddToolbarButton()
		{
			if (HighLogic.LoadedSceneIsFlight)
			{
				if (toolbarButton == null)
				{
					Texture buttonTexture = GameDatabase.Instance.GetTexture("BDArmory/Textures/Icons/icon", false);
					toolbarButton = ApplicationLauncher.Instance.AddModApplication(ShowToolbarGUI, HideToolbarGUI, null, null, null, null, ApplicationLauncher.AppScenes.FLIGHT, buttonTexture);
				}
			}
		}

		public void ShowToolbarGUI()
		{
			if (LegacyTILoaded)
			{
				ScreenMessages.PostScreenMessage(Localizer.Format("#LOC_BDArmory_Icons_legacyinstall"), 5.0f, ScreenMessageStyle.UPPER_CENTER);
			}
			else
			{				
				showTeamIconGUI = true;
				LoadConfig();
			}
		}

		public void HideToolbarGUI()
		{
			showTeamIconGUI = false;
			SaveConfig();
		}

		public static void LoadConfig()
		{
			try
			{
				Debug.Log("[BDTeamIcons]=== Loading settings.cfg ===");

				SettingsDataField.Load();
			}
			catch (NullReferenceException)
			{
				Debug.Log("[BDTeamIcons]=== Failed to load settings config ===");
			}
		}

		public static void SaveConfig()
		{
			try
			{
				Debug.Log("[BDTeamIcons] == Saving settings.cfg ==	");
				SettingsDataField.Save();
			}
			catch (NullReferenceException)
			{
				Debug.Log("[BDTeamIcons]: === Failed to save settings.cfg ====");
			}
		}

		GUIStyle title;

		void OnGUI()
		{
			if (LegacyTILoaded) return;

			if (showTeamIconGUI)
			{
				if (HighLogic.LoadedSceneIsFlight)
				{
					maySavethisInstance = true;
				}
				WindowRectGUI = new Rect(Screen.width - toolWindowWidth - 40, 150, toolWindowWidth, toolWindowHeight);
				WindowRectGUI = GUI.Window(this.GetInstanceID(), WindowRectGUI, TeamIconGUI, windowTitle, BDArmorySetup.BDGuiSkin.window);
			}
			title = new GUIStyle(GUI.skin.label);
			title.fontSize = 30;
			title.alignment = TextAnchor.MiddleLeft;
			title.wordWrap = false;

		}
		public bool showTeamIconSelect = false;
		public bool showColorSelect = false;

		void TeamIconGUI(int windowID)
		{
			int line = 0;
			int i = 0;
			BDTISettings.TEAMICONS = GUI.Toggle(new Rect(5, 25, 300, 20), BDTISettings.TEAMICONS, Localizer.Format("#LOC_BDArmory_Enable_Icons"), BDArmorySetup.BDGuiSkin.toggle);
			if (BDTISettings.TEAMICONS)
			{
				if (GameSettings.FLT_VESSEL_LABELS && !showPSA)
				{
					ScreenMessages.PostScreenMessage(Localizer.Format("#LOC_BDArmory_Icons_PSA"), 7.0f, ScreenMessageStyle.UPPER_CENTER);
					showPSA = true;
				}
				Rect IconOptionsGroup = new Rect(15, 55, toolWindowWidth - 20, 280);
				GUI.BeginGroup(IconOptionsGroup, GUIContent.none, BDArmorySetup.BDGuiSkin.box);
				BDTISettings.TEAMNAMES = GUI.Toggle(new Rect(15, line * 25, toolWindowWidth - 20, 20), BDTISettings.TEAMNAMES, Localizer.Format("#LOC_BDArmory_Icon_teams"), BDArmorySetup.BDGuiSkin.toggle);
				line++;
				BDTISettings.VESSELNAMES = GUI.Toggle(new Rect(15, line * 25, toolWindowWidth - 20, 20), BDTISettings.VESSELNAMES, Localizer.Format("#LOC_BDArmory_Icon_names"), BDArmorySetup.BDGuiSkin.toggle);
				line++;
				BDTISettings.SCORE = GUI.Toggle(new Rect(15, line * 25, toolWindowWidth - 20, 20), BDTISettings.SCORE, Localizer.Format("#LOC_BDArmory_Icon_score"), BDArmorySetup.BDGuiSkin.toggle);
				line++;
				BDTISettings.HEALTHBAR = GUI.Toggle(new Rect(15, line * 25, toolWindowWidth - 20, 20), BDTISettings.HEALTHBAR, Localizer.Format("#LOC_BDArmory_Icon_healthbars"), BDArmorySetup.BDGuiSkin.toggle);
				line++;
				BDTISettings.MISSILES = GUI.Toggle(new Rect(15, line * 25, toolWindowWidth - 20, 20), BDTISettings.MISSILES, Localizer.Format("#LOC_BDArmory_Icon_missiles"), BDArmorySetup.BDGuiSkin.toggle);
				line++;
				BDTISettings.DEBRIS = GUI.Toggle(new Rect(15, line * 25, toolWindowWidth - 20, 20), BDTISettings.DEBRIS, Localizer.Format("#LOC_BDArmory_Icon_debris"), BDArmorySetup.BDGuiSkin.toggle);
				line++;
				BDTISettings.PERSISTANT = GUI.Toggle(new Rect(15, line * 25, toolWindowWidth - 20, 20), BDTISettings.PERSISTANT, Localizer.Format("#LOC_BDArmory_Icon_persist"), BDArmorySetup.BDGuiSkin.toggle);
				line++;
				BDTISettings.THREATICON = GUI.Toggle(new Rect(15, line * 25, toolWindowWidth - 20, 20), BDTISettings.THREATICON, Localizer.Format("#LOC_BDArmory_Icon_threats"), BDArmorySetup.BDGuiSkin.toggle);
				line++;
				BDTISettings.POINTERS = GUI.Toggle(new Rect(15, line * 25, toolWindowWidth - 20, 20), BDTISettings.POINTERS, Localizer.Format("#LOC_BDArmory_Icon_pointers"), BDArmorySetup.BDGuiSkin.toggle);
				line++;
				BDTISettings.TELEMETRY = GUI.Toggle(new Rect(15, line * 25, toolWindowWidth - 20, 20), BDTISettings.TELEMETRY, Localizer.Format("#LOC_BDArmory_Icon_telemetry"), BDArmorySetup.BDGuiSkin.toggle);
				line++;
				GUI.Label(new Rect(75, line * 25, toolWindowWidth - 20, 20), Localizer.Format("#LOC_BDArmory_Icon_scale") + " " +(BDTISettings.ICONSCALE * 100f).ToString("0") + "%");
				line++;
				BDTISettings.ICONSCALE = GUI.HorizontalSlider(new Rect(10, line * 25, toolWindowWidth - 40, 20), BDTISettings.ICONSCALE, 0.25f, 2f);
				line++;
				GUI.EndGroup();

				Rect TeamColorsGroup = new Rect(15, 365, toolWindowWidth - 20, teamWindowHeight);
				GUI.BeginGroup(TeamColorsGroup, GUIContent.none, BDArmorySetup.BDGuiSkin.box);

				using (var teamManagers = weaponManagers.GetEnumerator())
					while (teamManagers.MoveNext())
					{
						i++;
						Rect buttonRect = new Rect(30, -20 + (i * 25), 190, 20);
						GUIStyle vButtonStyle = showColorSelect ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button;
						if (GUI.Button(buttonRect, $"{teamManagers.Current.Key}", vButtonStyle))
						{
							showColorSelect = !showColorSelect;
							selectedTeam = teamManagers.Current.Key;
						}
						if (ColorAssignments.ContainsKey(teamManagers.Current.Key))
						{
							title.normal.textColor = ColorAssignments[teamManagers.Current.Key];
						}
						GUI.Label(new Rect(5, -20 + (i * 25), 25, 25), "*", title);
					}
				teamWindowHeight = Mathf.Lerp(teamWindowHeight, (i * 25) + 5, 1);
				GUI.EndGroup();
			}
			toolWindowHeight = Mathf.Lerp(toolWindowHeight, (50 + (line * 25) + (i * 25) + 5) + 15, 1);
			WindowRectGUI.height = toolWindowHeight + 10;
		}
	}
}
