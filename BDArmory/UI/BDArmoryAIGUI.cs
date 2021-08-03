using System.Collections;
using BDArmory.Core;
using BDArmory.Modules;
using UnityEngine;
using KSP.Localization;
using KSP.UI.Screens;
using static UnityEngine.GUILayout;
using System;

namespace BDArmory.UI
{
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    public class BDArmoryAIGUI : MonoBehaviour
    {       
        //toolbar gui
        public static bool infoLinkEnabled = false;
        public static bool contextTipsEnabled = false;
        public static bool NumFieldsEnabled = false;
        public static bool windowBDAAIGUIEnabled;
        float WindowWidth = 500;
        float WindowHeight = 250;
        float height = 0;
        float infoHeight = 0;
        float ColumnWidth = 350;
        float _buttonSize = 26;
        float _windowMargin = 4;
        float contentTop = 10;
        float entryHeight = 20;
        bool showPID;
        bool showAltitude;
        bool showSpeed;
        bool showControl;
        bool showEvade;
        bool showTerrain;
        bool showRam;
        bool showMisc;
        float Drivertype = 0;
        float broadsideDir = 0;

        private Vector2 scrollViewVector;
        private Vector2 scrollInfoVector;

        public BDModulePilotAI ActivePilot;
        public BDModuleSurfaceAI ActiveDriver;

        public static BDArmoryAIGUI Instance;
        public static bool buttonSetup;

        GUIStyle BoldLabel;
        GUIStyle Label;
        GUIStyle rightLabel;
        GUIStyle Title;
        GUIStyle contextLabel;
        GUIStyle infoLinkStyle;

        void Awake()
        {
            Instance = this;
            BDArmorySetup.WindowRectAI = new Rect(BDArmorySetup.WindowRectAI.x, BDArmorySetup.WindowRectAI.y, WindowWidth, WindowHeight);
        }

        void Start()
        {    
            if (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight)
            {               
                GameEvents.onVesselChange.Add(VesselChange);
                StartCoroutine(ToolbarButtonRoutine());
            }
            Label = new GUIStyle();
            Label.alignment = TextAnchor.UpperLeft;
            Label.normal.textColor = Color.white;

            rightLabel = new GUIStyle();
            rightLabel.alignment = TextAnchor.UpperRight;
            rightLabel.normal.textColor = Color.white;

            contextLabel = new GUIStyle();
            contextLabel.alignment = TextAnchor.UpperCenter;
            contextLabel.normal.textColor = Color.white;

            BoldLabel = new GUIStyle();
            BoldLabel.alignment = TextAnchor.UpperLeft;
            BoldLabel.fontStyle = FontStyle.Bold;
            BoldLabel.normal.textColor = Color.white;

            Title = new GUIStyle();
            Title.normal.textColor = BDArmorySetup.BDGuiSkin.window.normal.textColor;
            Title.font = BDArmorySetup.BDGuiSkin.window.font;
            Title.fontSize = BDArmorySetup.BDGuiSkin.window.fontSize;
            Title.fontStyle = BDArmorySetup.BDGuiSkin.window.fontStyle;
            Title.alignment = TextAnchor.UpperCenter;

            infoLinkStyle = new GUIStyle(BDArmorySetup.BDGuiSkin.label);
            infoLinkStyle.alignment = TextAnchor.UpperLeft;
            infoLinkStyle.normal.textColor = Color.white;


            if (HighLogic.LoadedSceneIsFlight)
            {
                GetAI();
            }
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorPartPlaced.Add(OnEditorPartPlacedEvent); //do per part placement instead of calling a findModule call every time *anything* changes on thevessel
                GameEvents.onEditorPartDeleted.Add(OnEditorPartDeletedEvent);
            }
        }
        void AddToolbarButton()
        {
            if (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight)
            {
                if (!buttonSetup)
                {
                    Texture buttonTexture = GameDatabase.Instance.GetTexture(BDArmorySetup.textureDir + "icon_ai", false);
                    ApplicationLauncher.Instance.AddModApplication(ShowToolbarGUI, HideToolbarGUI, Dummy, Dummy, Dummy, Dummy, ApplicationLauncher.AppScenes.SPH | ApplicationLauncher.AppScenes.VAB | ApplicationLauncher.AppScenes.FLIGHT, buttonTexture);
                    buttonSetup = true;
                }
            }
        }

        IEnumerator ToolbarButtonRoutine()
        {
            if (buttonSetup) yield break;
            if (!HighLogic.LoadedSceneIsFlight) yield break;
            while (!ApplicationLauncher.Ready)
            {
                yield return null;
            }

            AddToolbarButton();
        }

        public void ShowToolbarGUI()
        {
            windowBDAAIGUIEnabled = true;
        }

        public void HideToolbarGUI()
        {
            windowBDAAIGUIEnabled = false;
        }

        void Dummy()
        { }

        void Update()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (Input.GetKeyDown(KeyCode.KeypadDivide))
                {
                    windowBDAAIGUIEnabled = !windowBDAAIGUIEnabled;
                }
            }            
        }       

        void VesselChange(Vessel v)
        {
            if (v.isActiveVessel)
            {
                GetAI();
            }
        }
        private void OnEditorPartPlacedEvent(Part p)
        {
            if (ActivePilot == null)
            {
                var AI = p.FindModuleImplementing<BDModulePilotAI>();
                if (AI != null)
                {
                    ActivePilot = AI;
                }
            }
        }

        private void OnEditorPartDeletedEvent(Part p)
        {
            if (ActivePilot != null)
            {
                var AI = p.FindModuleImplementing<BDModulePilotAI>();
                if (AI != null)
                {
                    ActivePilot = null;
                }
            }
        }

        void GetAI()
        {
            ActivePilot = VesselModuleRegistry.GetBDModulePilotAI(FlightGlobals.ActiveVessel, true);
            if (ActivePilot == null)
            {
                ActiveDriver = VesselModuleRegistry.GetBDModuleSurfaceAI(FlightGlobals.ActiveVessel, true);
            }
        }
        
        #region GUI

        void OnGUI()
        {
            if (!BDArmorySetup.GAME_UI_ENABLED) return;

            if (!windowBDAAIGUIEnabled || (!HighLogic.LoadedSceneIsFlight && !HighLogic.LoadedSceneIsEditor)) return;
            //BDArmorySetup.WindowRectAI = new Rect(BDArmorySetup.WindowRectAI.x, BDArmorySetup.WindowRectAI.y, WindowWidth, WindowHeight);
            BDArmorySetup.WindowRectAI = GUI.Window(GetInstanceID(), BDArmorySetup.WindowRectAI, WindowRectAI, "", BDArmorySetup.BDGuiSkin.window);//"BDA Weapon Manager"
            BDGUIUtils.UseMouseEventInRect(BDArmorySetup.WindowRectAI);       
        }

        float pidHeight;
        float speedHeight;
        float altitudeHeight;
        float evasionHeight;
        float controlHeight;
        float terrainHeight;
        float rammingHeight;
        float miscHeight;

        Rect TitleButtonRect(float offset)
        {
            return new Rect((ColumnWidth * 2) - _windowMargin - (offset * _buttonSize), _windowMargin, _buttonSize, _buttonSize);
        }

        Rect SubsectionRect(float indent, float line)
        {
            return new Rect(indent, contentTop + (line * entryHeight), 100, entryHeight);
        }

        Rect SettinglabelRect(float indent, float lines)
        {
            return new Rect(indent, (lines * entryHeight), 85, entryHeight);
        }
        Rect SettingSliderRect(float indent, float lines, float contentWidth)
        {
            return new Rect(indent + 150, (lines * entryHeight), contentWidth - (indent * 2) - (150 + 10), entryHeight);
        }
        Rect ContextLabelRect(float indent, float lines)
        {
            return new Rect(150 + indent, (lines * entryHeight), 85, entryHeight);
        }

        Rect ToggleButtonRect(float indent, float lines, float contentWidth)
        {
            return new Rect(indent, (lines * entryHeight), contentWidth - (2 * indent), entryHeight);
        }

        void WindowRectAI(int windowID)
        {
            float line = 0;
            float leftIndent = 10;
            float windowColumns = 2;
            float contentWidth = ((ColumnWidth * 2) - 100 - 20);

            GUI.DragWindow(new Rect(_windowMargin +_buttonSize * 6, 0, (ColumnWidth * 2) - (2 * _windowMargin) - (10 * _buttonSize), _windowMargin + _buttonSize));

            GUI.Label(new Rect(100, contentTop, contentWidth, entryHeight),
               Localizer.Format("#LOC_BDArmory_AIWindow_title"), Title);// "No AI found."

            line += 1.25f;
            line += 0.25f;

            //Exit Button
            GUIStyle buttonStyle = windowBDAAIGUIEnabled ? BDArmorySetup.BDGuiSkin.button : BDArmorySetup.BDGuiSkin.box;
            if (GUI.Button(TitleButtonRect(1), "X", buttonStyle))
            {
                windowBDAAIGUIEnabled = !windowBDAAIGUIEnabled;
            }

            //Infolink button
            buttonStyle = infoLinkEnabled ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button;
            if (GUI.Button(TitleButtonRect(2), "i", buttonStyle))
            {
                infoLinkEnabled = !infoLinkEnabled;
            }

            //Context labels button
            buttonStyle = contextTipsEnabled ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button;
            if (GUI.Button(TitleButtonRect(3), "?", buttonStyle))
            {
                contextTipsEnabled = !contextTipsEnabled;
            }

            //Numeric fields button
            buttonStyle = NumFieldsEnabled ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button;
            if (GUI.Button(TitleButtonRect(4), "#", buttonStyle))
            {
                NumFieldsEnabled = !NumFieldsEnabled;
            }

            if (ActivePilot != null)
            {
                GUIStyle saveStyle = BDArmorySetup.BDGuiSkin.button;
                if (GUI.Button(new Rect(_windowMargin, _windowMargin, _buttonSize * 3, _buttonSize), "Save", saveStyle))
                {
                    ActivePilot.StoreSettings();
                }

                if (ActivePilot.Events["RestoreSettings"].active == true)
                {
                    GUIStyle restoreStyle = BDArmorySetup.BDGuiSkin.button;
                    if (GUI.Button(new Rect(_windowMargin + _buttonSize * 3, _windowMargin, _buttonSize * 3, _buttonSize), "Restore", restoreStyle))
                    {
                        ActivePilot.RestoreSettings();
                    }
                }

                showPID = GUI.Toggle(SubsectionRect(leftIndent, line),
                            showPID, Localizer.Format("#LOC_BDArmory_PilotAI_PID"), showPID ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"PiD"                    
                line += 1.5f;

                showAltitude = GUI.Toggle(SubsectionRect(leftIndent, line),
                    showAltitude, Localizer.Format("#LOC_BDArmory_PilotAI_Altitudes"), showAltitude ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"Altitude"        
                line += 1.5f;

                showSpeed = GUI.Toggle(SubsectionRect(leftIndent, line),
                    showSpeed, Localizer.Format("#LOC_BDArmory_PilotAI_Speeds"), showSpeed ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"Speed"                    
                line += 1.5f;

                showControl = GUI.Toggle(SubsectionRect(leftIndent, line),
                    showControl, Localizer.Format("#LOC_BDArmory_AIWindow_ControlLimits"), showControl ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"Control"  
                line += 1.5f;

                showEvade = GUI.Toggle(SubsectionRect(leftIndent, line),
                    showEvade, Localizer.Format("#LOC_BDArmory_AIWindow_EvadeExtend"), showEvade ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"Evasion"                    
                line += 1.5f;

                showTerrain = GUI.Toggle(SubsectionRect(leftIndent, line),
                    showTerrain, Localizer.Format("#LOC_BDArmory_AIWindow_Terrain"), showTerrain ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"Terrain"  
                line += 1.5f;

                showRam = GUI.Toggle(SubsectionRect(leftIndent, line),
                    showRam, Localizer.Format("#LOC_BDArmory_PilotAI_Ramming"), showRam ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"rammin"                    
                line += 1.5f;

                showMisc = GUI.Toggle(SubsectionRect(leftIndent, line),
                    showMisc, Localizer.Format("#LOC_BDArmory_PilotAI_Misc"), showMisc ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"Misc"  
                line += 1.5f;

                ActivePilot.UpToEleven = GUI.Toggle(SubsectionRect(leftIndent, line),
                    ActivePilot.UpToEleven, ActivePilot.UpToEleven ? Localizer.Format("#LOC_BDArmory_UnclampTuning_enabledText") : Localizer.Format("#LOC_BDArmory_UnclampTuning_disabledText"), ActivePilot.UpToEleven ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"Misc"  
                line += 5;

                float pidLines = 0;
                float altLines = 0;
                float spdLines = 0;
                float ctrlLines = 0;
                float evadeLines = 0;
                float gndLines = 0;
                float ramLines = 0;
                float miscLines = 0;

                if (height < WindowHeight)
                {
                    height = WindowHeight - (entryHeight * 1.5f);
                }

                if (infoLinkEnabled)
                {
                    float infoLines = 0;
                    int infoLength = 1;
                    windowColumns = 3;

                    GUI.Label(new Rect(leftIndent + (ColumnWidth * 2), (contentTop), ColumnWidth - (leftIndent), entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_infoLink"), Title);//"infolink"
                    BeginArea(new Rect(leftIndent + (ColumnWidth * 2), contentTop + (entryHeight * 1.5f), ColumnWidth - (leftIndent), WindowHeight - (entryHeight * 1.5f) - (2 * contentTop)));
                    using (var scrollViewScope = new ScrollViewScope(scrollInfoVector, Width(ColumnWidth - (leftIndent)), Height(WindowHeight - (entryHeight * 1.5f) - (2 * contentTop))))
                    {
                        scrollInfoVector = scrollViewScope.scrollPosition;

                        if (showPID)
                        {
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_PilotAI_PID"), BoldLabel, Width(ColumnWidth - (leftIndent * 4) - 20)); //PID label
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_AIWindow_PidHelp"), infoLinkStyle, Width(ColumnWidth - (leftIndent * 4) - 20)); //Pid desc
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_AIWindow_PidHelp_SteerMult"), infoLinkStyle, Width(ColumnWidth - (leftIndent * 4) - 20)); //steer mult desc
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_AIWindow_PidHelp_SteerKi"), infoLinkStyle, Width(ColumnWidth - (leftIndent * 4) - 20)); //steer ki desc.
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_AIWindow_PidHelp_Steerdamp"), infoLinkStyle, Width(ColumnWidth - (leftIndent * 4) - 20)); //steer damp description
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_AIWindow_PidHelp_Dyndamp"), infoLinkStyle, Width(ColumnWidth - (leftIndent * 4) - 20)); //dynamic damping desc
                        }
                        if (showAltitude)
                        {
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_PilotAI_Altitudes"), BoldLabel, Width(ColumnWidth - (leftIndent * 4) - 20)); //Altitude label
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_AIWindow_AltHelp"), infoLinkStyle, Width(ColumnWidth - (leftIndent * 4) - 20)); //altitude description
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_AIWindow_AltHelp_Def"), infoLinkStyle, Width(ColumnWidth - (leftIndent * 4) - 20)); //default alt desc
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_AIWindow_AltHelp_min"), infoLinkStyle, Width(ColumnWidth - (leftIndent * 4) - 20)); //min alt desc
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_AIWindow_AltHelp_max"), infoLinkStyle, Width(ColumnWidth - (leftIndent * 4) - 20)); //max alt desc
                        }
                        if (showSpeed)
                        {
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_PilotAI_Speeds"), BoldLabel, Width(ColumnWidth - (leftIndent * 4) - 20)); //Speed header
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_AIWindow_SpeedHelp"), infoLinkStyle, Width(ColumnWidth - (leftIndent * 4) - 20)); //speed explanation
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_AIWindow_SpeedHelp_min"), infoLinkStyle, Width(ColumnWidth - (leftIndent * 4) - 20)); //min+mas speed desc
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_AIWindow_SpeedHelp_takeoff"), infoLinkStyle, Width(ColumnWidth - (leftIndent * 4) - 20)); //takeoff speed
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_AIWindow_SpeedHelp_idle"), infoLinkStyle, Width(ColumnWidth - (leftIndent * 4) - 20)); //idle speed
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_AIWindow_SpeedHelp_gnd"), infoLinkStyle, Width(ColumnWidth - (leftIndent * 4) - 20)); //strafe speed
                        }
                        if (showControl)
                        {
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_PilotAI_ControlLimits"), BoldLabel, Width(ColumnWidth - (leftIndent * 4) - 20)); //conrrol header
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_AIWindow_ControlHelp"), infoLinkStyle, Width(ColumnWidth - (leftIndent * 4) - 20)); //control desc
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_AIWindow_ControlHelp_limiters"), infoLinkStyle, Width(ColumnWidth - (leftIndent * 4) - 20)); //low + high speed limiters
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_AIWindow_ControlHelp_bank"), infoLinkStyle, Width(ColumnWidth - (leftIndent * 4) - 20)); //max bank desc
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_AIWindow_ControlHelp_clamps"), infoLinkStyle, Width(ColumnWidth - (leftIndent * 4) - 20)); //max G + max AoA
                        }
                        if (showEvade)
                        {
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_PilotAI_EvadeExtend"), BoldLabel, Width(ColumnWidth - (leftIndent * 4) - 20)); //evade header
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_AIWindow_EvadeHelp"), infoLinkStyle, Width(ColumnWidth - (leftIndent * 4) - 20)); //evade description
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_AIWindow_EvadeHelp_Evade"), infoLinkStyle, Width(ColumnWidth - (leftIndent * 4) - 20)); //evade dist/ time/ time threshold
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_AIWindow_EvadeHelp_Dodge"), infoLinkStyle, Width(ColumnWidth - (leftIndent * 4) - 20)); //collision avoid
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_AIWindow_EvadeHelp_standoff"), infoLinkStyle, Width(ColumnWidth - (leftIndent * 4) - 20)); //standoff distance
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_AIWindow_EvadeHelp_Extend"), infoLinkStyle, Width(ColumnWidth - (leftIndent * 4) - 20)); //extend mult
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_AIWindow_EvadeHelp_ExtendVars"), infoLinkStyle, Width(ColumnWidth - (leftIndent * 4) - 20)); //extend target dist/angle/vel
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_AIWindow_EvadeHelp_ExtendAngle"), infoLinkStyle, Width(ColumnWidth - (leftIndent * 4) - 20)); //extend target angle
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_AIWindow_EvadeHelp_ExtendDist"), infoLinkStyle, Width(ColumnWidth - (leftIndent * 4) - 20)); //extend target dist
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_AIWindow_EvadeHelp_ExtendVel"), infoLinkStyle, Width(ColumnWidth - (leftIndent * 4) - 20)); //estend target velocity
                        }
                        if (showTerrain)
                        {
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_PilotAI_Terrain"), BoldLabel, Width(ColumnWidth - (leftIndent * 4) - 20)); //Terrain avoid header
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_AIWindow_TerrainHelp"), infoLinkStyle, Width(ColumnWidth - (leftIndent * 4) - 20)); //terrain avoid desc
                        }
                        if (showRam)
                        {
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_PilotAI_Ramming"), BoldLabel, Width(ColumnWidth - (leftIndent * 4) - 20)); //ramming header
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_AIWindow_RamHelp"), infoLinkStyle, Width(ColumnWidth - (leftIndent * 4) - 20));// ramming desc
                        }
                        if (showMisc)
                        {
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_PilotAI_Misc"), BoldLabel, Width(ColumnWidth - (leftIndent * 4) - 20)); //misc header
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_AIWindow_miscHelp"), infoLinkStyle, Width(ColumnWidth - (leftIndent * 4) - 20)); //misc desc
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_AIWindow_orbitHelp"), infoLinkStyle, Width(ColumnWidth - (leftIndent * 4) - 20)); //orbit dir
                            GUILayout.Label(Localizer.Format("#LOC_BDArmory_AIWindow_standbyHelp"), infoLinkStyle, Width(ColumnWidth - (leftIndent * 4) - 20)); //standby
                        }
                    }
                    EndArea();
                }

                scrollViewVector = GUI.BeginScrollView(new Rect(leftIndent + 100, contentTop + (entryHeight * 1.5f), (ColumnWidth * 2) - 100 - (leftIndent), WindowHeight - (entryHeight * 1.5f) - (2 * contentTop)), scrollViewVector,
                                       new Rect(0, 0, (ColumnWidth * 2) - 120 - (leftIndent * 2), height + contentTop));

                GUI.BeginGroup(new Rect(leftIndent, 0, (ColumnWidth * 2) - 120 - (leftIndent * 2), height), GUIContent.none, BDArmorySetup.BDGuiSkin.box); //darker box

                //GUI.Box(new Rect(0, 0, (ColumnWidth * 2) - leftIndent, height - contentTop), "", BDArmorySetup.BDGuiSkin.window);
                contentWidth -= 24;
                leftIndent += 3;

                if (showPID)
                {
                    pidLines += 0.2f;
                    GUI.BeginGroup(
                        new Rect(0, (pidLines * entryHeight), contentWidth, pidHeight * entryHeight),
                        GUIContent.none, BDArmorySetup.BDGuiSkin.box);
                    pidLines += 0.25f;

                    GUI.Label(SettinglabelRect(leftIndent, pidLines), Localizer.Format("#LOC_BDArmory_PilotAI_PID"), BoldLabel);//"Pid Controller"
                    pidLines++;

                    if (!NumFieldsEnabled)
                    {
                        ActivePilot.steerMult =
                            GUI.HorizontalSlider(SettingSliderRect(leftIndent, pidLines, contentWidth),
                                ActivePilot.steerMult, 0.1f, ActivePilot.UpToEleven ? 200 : 20);
                        ActivePilot.steerMult = Mathf.Round(ActivePilot.steerMult * 10f) / 10f;
                    }
                    else
                    {
                        float SMult;
                        string SteerMult = GUI.TextField(SettingSliderRect(leftIndent, pidLines, contentWidth), ActivePilot.steerMult.ToString("0.0"));
                        if (Single.TryParse(SteerMult, out SMult))
                        {
                            ActivePilot.steerMult = SMult;
                        }
                    }
                    GUI.Label(SettinglabelRect(leftIndent, pidLines), Localizer.Format("#LOC_BDArmory_SteerFactor") + " :" + ActivePilot.steerMult.ToString("0.0"), Label);//"Steer Mult"


                    pidLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(ContextLabelRect(leftIndent, pidLines), Localizer.Format("#LOC_BDArmory_AIWindow_SteerMultLow"), Label);//"sluggish"
                        GUI.Label(new Rect(150 + leftIndent + (contentWidth - leftIndent - 150 - 85 - 20), (pidLines * entryHeight), 85, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_SteerMultHi"), rightLabel);//"twitchy"
                        pidLines++;
                    }
                    if (!NumFieldsEnabled)
                    {
                        ActivePilot.steerKiAdjust =
                            GUI.HorizontalSlider(SettingSliderRect(leftIndent, pidLines, contentWidth),
                                ActivePilot.steerKiAdjust, 0.01f, ActivePilot.UpToEleven ? 20 : 1);
                        ActivePilot.steerKiAdjust = Mathf.Round(ActivePilot.steerKiAdjust * 100f) / 100f;
                    }
                    else
                    {
                        float SKi;
                        string SteerKI = GUI.TextField(SettingSliderRect(leftIndent, pidLines, contentWidth), ActivePilot.steerKiAdjust.ToString("0.00"));
                        if (Single.TryParse(SteerKI, out SKi))
                        {
                            ActivePilot.steerKiAdjust = SKi;
                        }
                    }
                    GUI.Label(SettinglabelRect(leftIndent, pidLines), Localizer.Format("#LOC_BDArmory_SteerKi") + " :" + ActivePilot.steerKiAdjust.ToString("0.00"), Label);//"Steer Ki"
                    pidLines++;

                    if (contextTipsEnabled)
                    {
                        GUI.Label(ContextLabelRect(leftIndent, pidLines), Localizer.Format("#LOC_BDArmory_AIWindow_SteerKiLow"), Label);//"undershoot"
                        GUI.Label(new Rect(150 + leftIndent + (contentWidth - leftIndent - 150 - 85 - 20), (pidLines * entryHeight), 85, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_SteerKiHi"), rightLabel);//"Overshoot"
                        pidLines++;
                    }
                    if (!NumFieldsEnabled)
                    {
                        ActivePilot.steerDamping =
                            GUI.HorizontalSlider(SettingSliderRect(leftIndent, pidLines, contentWidth),
                                ActivePilot.steerDamping, 0.01f, ActivePilot.UpToEleven ? 100 : 8);
                        ActivePilot.steerDamping = Mathf.Round(ActivePilot.steerDamping * 100f) / 100f;
                    }
                    else
                    {
                        float SDamp;
                        string steerDamp = GUI.TextField(SettingSliderRect(leftIndent, pidLines, contentWidth), ActivePilot.steerDamping.ToString("0.00"));
                        if (Single.TryParse(steerDamp, out SDamp))
                        {
                            ActivePilot.steerDamping = SDamp;
                        }
                    }
                    GUI.Label(SettinglabelRect(leftIndent, pidLines), Localizer.Format("#LOC_BDArmory_SteerDamping") + " :" + ActivePilot.steerDamping.ToString("0.00"), Label);//"Steer Damping"

                    pidLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(ContextLabelRect(leftIndent, pidLines), Localizer.Format("#LOC_BDArmory_AIWindow_SteerDampLow"), Label);//"Wobbly"
                        GUI.Label(new Rect(150 + leftIndent + (contentWidth - leftIndent - 150 - 85 - 20), (pidLines * entryHeight), 85, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_SteerDampHi"), rightLabel);//"Stiff"
                        pidLines++;
                    }

                    ActivePilot.dynamicSteerDamping =
                       GUI.Toggle(ToggleButtonRect(leftIndent, pidLines, contentWidth),
                           ActivePilot.dynamicSteerDamping, Localizer.Format("#LOC_BDArmory_DynamicDamping"), ActivePilot.dynamicSteerDamping ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"Dynamic damping"
                    pidLines += 1.25f;

                    if (ActivePilot.dynamicSteerDamping)
                    {
                        float dynPidLines = 0;
                        ActivePilot.CustomDynamicAxisFields = GUI.Toggle(ToggleButtonRect(leftIndent, pidLines, contentWidth),
                        ActivePilot.CustomDynamicAxisFields, Localizer.Format("#LOC_BDArmory_3AxisDynamicSteerDamping"), ActivePilot.CustomDynamicAxisFields ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"3 axis damping"
                        dynPidLines++;
                        if (!ActivePilot.CustomDynamicAxisFields)
                        {
                            dynPidLines += 0.25f;

                            GUI.Label(SettinglabelRect(leftIndent, pidLines + dynPidLines), Localizer.Format("#LOC_BDArmory_DynamicDamping"), BoldLabel);//"Dynamic Damping"
                            dynPidLines++;
                            if (!NumFieldsEnabled)
                            {
                                ActivePilot.DynamicDampingMin =
                                    GUI.HorizontalSlider(SettingSliderRect(leftIndent, pidLines + dynPidLines, contentWidth),
                                        ActivePilot.DynamicDampingMin, 1f, ActivePilot.UpToEleven ? 100 : 8);
                                ActivePilot.DynamicDampingMin = Mathf.Round(ActivePilot.DynamicDampingMin * 10f) / 10f;
                            }
                            else
                            {
                                float SDminDamp;
                                string DminDamp = GUI.TextField(SettingSliderRect(leftIndent, pidLines + dynPidLines, contentWidth), ActivePilot.DynamicDampingMin.ToString("0.0"));
                                if (Single.TryParse(DminDamp, out SDminDamp))
                                {
                                    ActivePilot.DynamicDampingMin = SDminDamp;
                                }
                            }
                            GUI.Label(SettinglabelRect(leftIndent, pidLines + dynPidLines), Localizer.Format("#LOC_BDArmory_DynamicDampingMin") + " :" + ActivePilot.DynamicDampingMin.ToString("0.0"), Label);//"dynamic damping min"
                            dynPidLines++;
                            if (contextTipsEnabled)
                            {
                                GUI.Label(ContextLabelRect(leftIndent, pidLines + dynPidLines), Localizer.Format("#LOC_BDArmory_AIWindow_DynDampMin"), Label);//"dynamic damp min"
                                dynPidLines++;
                            }
                            if (!NumFieldsEnabled)
                            {
                                ActivePilot.DynamicDampingMax =
                                    GUI.HorizontalSlider(SettingSliderRect(leftIndent, pidLines + dynPidLines, contentWidth),
                                        ActivePilot.DynamicDampingMax, 1f, ActivePilot.UpToEleven ? 100 : 8);
                                ActivePilot.DynamicDampingMax = Mathf.Round(ActivePilot.DynamicDampingMax * 10f) / 10f;
                            }
                            else
                            {
                                float SDmaxDamp;
                                string DmaxSamp = GUI.TextField(SettingSliderRect(leftIndent, pidLines + dynPidLines, contentWidth), ActivePilot.DynamicDampingMax.ToString("0.0"));
                                if (Single.TryParse(DmaxSamp, out SDmaxDamp))
                                {
                                    ActivePilot.DynamicDampingMax = SDmaxDamp;
                                }
                            }
                            GUI.Label(SettinglabelRect(leftIndent, pidLines + dynPidLines), Localizer.Format("#LOC_BDArmory_DynamicDampingMax") + " :" + ActivePilot.DynamicDampingMax.ToString("0.0"), Label);//"dynamic damping max"

                            dynPidLines++;
                            if (contextTipsEnabled)
                            {
                                GUI.Label(ContextLabelRect(leftIndent, pidLines + dynPidLines), Localizer.Format("#LOC_BDArmory_AIWindow_DynDampMax"), Label);//"dynamic damp max"
                                dynPidLines++;
                            }
                            if (!NumFieldsEnabled)
                            {
                                ActivePilot.dynamicSteerDampingFactor =
                                    GUI.HorizontalSlider(SettingSliderRect(leftIndent, pidLines + dynPidLines, contentWidth),
                                        ActivePilot.dynamicSteerDampingFactor, 0.1f, ActivePilot.UpToEleven ? 100 : 10);
                                ActivePilot.dynamicSteerDampingFactor = Mathf.Round(ActivePilot.dynamicSteerDampingFactor * 10f) / 10f;
                            }
                            else
                            {
                                float SDDampMult;
                                string DDampMult = GUI.TextField(SettingSliderRect(leftIndent, pidLines + dynPidLines, contentWidth), ActivePilot.dynamicSteerDampingFactor.ToString("0.0"));
                                if (Single.TryParse(DDampMult, out SDDampMult))
                                {
                                    ActivePilot.dynamicSteerDampingFactor = SDDampMult;
                                }
                            }
                            GUI.Label(SettinglabelRect(leftIndent, pidLines + dynPidLines), Localizer.Format("#LOC_BDArmory_AIWindow_DynDampMult") + " :" + ActivePilot.dynamicSteerDampingFactor.ToString("0.0"), Label);//"dynamic damping mult"

                            dynPidLines++;
                            if (contextTipsEnabled)
                            {
                                GUI.Label(ContextLabelRect(leftIndent, pidLines + dynPidLines), Localizer.Format("#LOC_BDArmory_AIWindow_DynDampMult"), Label);//"dynamic damp mult"
                                dynPidLines++;
                            }
                        }
                        else
                        {
                            ActivePilot.dynamicDampingPitch = GUI.Toggle(ToggleButtonRect(leftIndent, pidLines + dynPidLines, contentWidth),
                            ActivePilot.dynamicDampingPitch, Localizer.Format("#LOC_BDArmory_DynamicDampingPitch"), ActivePilot.dynamicDampingPitch ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"Dynamic damp pitch"
                            dynPidLines += 1.25f;

                            if (ActivePilot.dynamicDampingPitch)
                            {
                                if (!NumFieldsEnabled)
                                {
                                    ActivePilot.DynamicDampingPitchMin =
                                        GUI.HorizontalSlider(SettingSliderRect(leftIndent, pidLines + dynPidLines, contentWidth),
                                            ActivePilot.DynamicDampingPitchMin, 1f, ActivePilot.UpToEleven ? 100 : 8);
                                    ActivePilot.DynamicDampingPitchMin = Mathf.Round(ActivePilot.DynamicDampingPitchMin * 10f) / 10f;
                                }
                                else
                                {
                                    float DDPitchMin;
                                    string pitchDampmin = GUI.TextField(SettingSliderRect(leftIndent, pidLines + dynPidLines, contentWidth), ActivePilot.DynamicDampingPitchMin.ToString("0.0"));
                                    if (Single.TryParse(pitchDampmin, out DDPitchMin))
                                    {
                                        ActivePilot.DynamicDampingPitchMin = DDPitchMin;
                                    }
                                }
                                GUI.Label(SettinglabelRect(leftIndent, pidLines + dynPidLines), Localizer.Format("#LOC_BDArmory_DynamicDampingPitchMin") + " :" + ActivePilot.DynamicDampingPitchMin.ToString("0.0"), Label);//"dynamic damping min"
                                dynPidLines++;
                                if (contextTipsEnabled)
                                {
                                    GUI.Label(ContextLabelRect(leftIndent, pidLines + dynPidLines), Localizer.Format("#LOC_BDArmory_AIWindow_DynDampMin"), contextLabel);//"dynamic damp min"
                                    dynPidLines++;
                                }
                                if (!NumFieldsEnabled)
                                {
                                    ActivePilot.DynamicDampingPitchMax =
                                        GUI.HorizontalSlider(SettingSliderRect(leftIndent, pidLines + dynPidLines, contentWidth),
                                            ActivePilot.DynamicDampingPitchMax, 1f, ActivePilot.UpToEleven ? 100 : 8);
                                    ActivePilot.DynamicDampingPitchMax = Mathf.Round(ActivePilot.DynamicDampingPitchMax * 10f) / 10f;
                                }
                                else
                                {
                                    float DDPitchMax;
                                    string pitchDampmax = GUI.TextField(SettingSliderRect(leftIndent, pidLines + dynPidLines, contentWidth), ActivePilot.DynamicDampingPitchMax.ToString("0.0"));
                                    if (Single.TryParse(pitchDampmax, out DDPitchMax))
                                    {
                                        ActivePilot.DynamicDampingPitchMax = DDPitchMax;
                                    }
                                }
                                GUI.Label(SettinglabelRect(leftIndent, pidLines + dynPidLines), Localizer.Format("#LOC_BDArmory_DynamicDampingMax") + " :" + ActivePilot.DynamicDampingPitchMax.ToString("0.0"), Label);//"dynamic damping max"

                                dynPidLines++;
                                if (contextTipsEnabled)
                                {
                                    GUI.Label(ContextLabelRect(leftIndent, pidLines + dynPidLines), Localizer.Format("#LOC_BDArmory_AIWindow_DynDampMax"), contextLabel);//"damp max"
                                    dynPidLines++;
                                }
                                if (!NumFieldsEnabled)
                                {
                                    ActivePilot.dynamicSteerDampingPitchFactor =
                                        GUI.HorizontalSlider(SettingSliderRect(leftIndent, pidLines + dynPidLines, contentWidth),
                                            ActivePilot.dynamicSteerDampingPitchFactor, 0.1f, ActivePilot.UpToEleven ? 100 : 10);
                                    ActivePilot.dynamicSteerDampingPitchFactor = Mathf.Round(ActivePilot.dynamicSteerDampingPitchFactor * 10f) / 10f;
                                }
                                else
                                {
                                    float DpitchMult;
                                    string DPmult = GUI.TextField(SettingSliderRect(leftIndent, pidLines + dynPidLines, contentWidth), ActivePilot.dynamicSteerDampingPitchFactor.ToString("0.0"));
                                    if (Single.TryParse(DPmult, out DpitchMult))
                                    {
                                        ActivePilot.dynamicSteerDampingPitchFactor = DpitchMult;
                                    }
                                }
                                GUI.Label(SettinglabelRect(leftIndent, pidLines + dynPidLines), Localizer.Format("#LOC_BDArmory_DynamicDampingPitchFactor") + " :" + ActivePilot.dynamicSteerDampingPitchFactor.ToString("0.0"), Label);//"dynamic damping mult"

                                dynPidLines++;
                                if (contextTipsEnabled)
                                {
                                    GUI.Label(ContextLabelRect(leftIndent, pidLines + dynPidLines), Localizer.Format("#LOC_BDArmory_AIWindow_DynDampMult"), contextLabel);//"dynamic damp Mult"
                                    dynPidLines++;
                                }
                            }

                            ActivePilot.dynamicDampingYaw = GUI.Toggle(ToggleButtonRect(leftIndent, pidLines + dynPidLines, contentWidth),
                           ActivePilot.dynamicDampingYaw, Localizer.Format("#LOC_BDArmory_DynamicDampingYaw"), ActivePilot.dynamicDampingYaw ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"Dynamic damp yaw"
                            dynPidLines += 1.25f;
                            if (ActivePilot.dynamicDampingYaw)
                            {
                                if (!NumFieldsEnabled)
                                {
                                    ActivePilot.DynamicDampingYawMin =
                                        GUI.HorizontalSlider(SettingSliderRect(leftIndent, pidLines + dynPidLines, contentWidth),
                                            ActivePilot.DynamicDampingYawMin, 1f, ActivePilot.UpToEleven ? 100 : 8);
                                    ActivePilot.DynamicDampingYawMin = Mathf.Round(ActivePilot.DynamicDampingYawMin * 10f) / 10f;
                                }
                                else
                                {
                                    float DyawMin;
                                    string DYmin = GUI.TextField(SettingSliderRect(leftIndent, pidLines + dynPidLines, contentWidth), ActivePilot.DynamicDampingYawMin.ToString("0.0"));
                                    if (Single.TryParse(DYmin, out DyawMin))
                                    {
                                        ActivePilot.DynamicDampingYawMin = DyawMin;
                                    }
                                }
                                GUI.Label(SettinglabelRect(leftIndent, pidLines + dynPidLines), Localizer.Format("#LOC_BDArmory_DynamicDampingYawMin") + " :" + ActivePilot.DynamicDampingYawMin.ToString("0.0"), Label);//"dynamic damping min"

                                dynPidLines++;
                                if (contextTipsEnabled)
                                {
                                    GUI.Label(ContextLabelRect(leftIndent, pidLines + dynPidLines), Localizer.Format("#LOC_BDArmory_AIWindow_DynDampMin"), contextLabel);//"dynamic damp min"
                                    dynPidLines++;
                                }
                                if (!NumFieldsEnabled)
                                {
                                    ActivePilot.DynamicDampingYawMax =
                                        GUI.HorizontalSlider(SettingSliderRect(leftIndent, pidLines + dynPidLines, contentWidth),
                                            ActivePilot.DynamicDampingYawMax, 1f, ActivePilot.UpToEleven ? 100 : 8);
                                    ActivePilot.DynamicDampingYawMax = Mathf.Round(ActivePilot.DynamicDampingYawMax * 10f) / 10f;
                                }
                                else
                                {
                                    float DyawMax;
                                    string DYmax = GUI.TextField(SettingSliderRect(leftIndent, pidLines + dynPidLines, contentWidth), ActivePilot.DynamicDampingYawMax.ToString("0.0"));
                                    if (Single.TryParse(DYmax, out DyawMax))
                                    {
                                        ActivePilot.DynamicDampingYawMax = DyawMax;
                                    }
                                }
                                GUI.Label(SettinglabelRect(leftIndent, pidLines + dynPidLines), Localizer.Format("#LOC_BDArmory_DynamicDampingYawMax") + " :" + ActivePilot.DynamicDampingYawMax.ToString("0.0"), Label);//"dynamic damping max"

                                dynPidLines++;
                                if (contextTipsEnabled)
                                {
                                    GUI.Label(ContextLabelRect(leftIndent, pidLines + dynPidLines), Localizer.Format("#LOC_BDArmory_AIWindow_DynDampMax"), contextLabel);//"dynamic damp max"
                                    dynPidLines++;
                                }
                                if (!NumFieldsEnabled)
                                {
                                    ActivePilot.dynamicSteerDampingYawFactor =
                                        GUI.HorizontalSlider(SettingSliderRect(leftIndent, pidLines + dynPidLines, contentWidth),
                                            ActivePilot.dynamicSteerDampingYawFactor, 0.1f, ActivePilot.UpToEleven ? 100 : 10);
                                    ActivePilot.dynamicSteerDampingYawFactor = Mathf.Round(ActivePilot.dynamicSteerDampingYawFactor / 10) * 10;
                                }
                                else
                                {
                                    float DyawMult;
                                    string DYmult = GUI.TextField(SettingSliderRect(leftIndent, pidLines + dynPidLines, contentWidth), ActivePilot.dynamicSteerDampingYawFactor.ToString("0.0"));
                                    if (Single.TryParse(DYmult, out DyawMult))
                                    {
                                        ActivePilot.dynamicSteerDampingYawFactor = DyawMult;
                                    }
                                }
                                GUI.Label(SettinglabelRect(leftIndent, pidLines + dynPidLines), Localizer.Format("#LOC_BDArmory_DynamicDampingYawFactor") + " :" + ActivePilot.dynamicSteerDampingYawFactor.ToString("0.0"), Label);//"dynamic damping yaw mult"

                                dynPidLines++;
                                if (contextTipsEnabled)
                                {
                                    GUI.Label(ContextLabelRect(leftIndent, pidLines + dynPidLines), Localizer.Format("#LOC_BDArmory_AIWindow_DynDampMult"), contextLabel);//"dynamic damp mult"
                                    dynPidLines++;
                                }
                            }

                            ActivePilot.dynamicDampingRoll = GUI.Toggle(ToggleButtonRect(leftIndent, pidLines + dynPidLines, contentWidth),
                            ActivePilot.dynamicDampingRoll, Localizer.Format("#LOC_BDArmory_DynamicDampingRoll"), ActivePilot.dynamicDampingRoll ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"Dynamic damp roll"
                            dynPidLines += 1.25f;
                            if (ActivePilot.dynamicDampingRoll)
                            {
                                if (!NumFieldsEnabled)
                                {
                                    ActivePilot.DynamicDampingRollMin =
                                        GUI.HorizontalSlider(SettingSliderRect(leftIndent, pidLines + dynPidLines, contentWidth),
                                            ActivePilot.DynamicDampingRollMin, 1f, ActivePilot.UpToEleven ? 100 : 8);
                                    ActivePilot.DynamicDampingRollMin = Mathf.Round(ActivePilot.DynamicDampingRollMin * 10f) / 10f;
                                }
                                else
                                {
                                    float DrollMin;
                                    string DRmin = GUI.TextField(SettingSliderRect(leftIndent, pidLines + dynPidLines, contentWidth), ActivePilot.DynamicDampingRollMin.ToString("0.0"));
                                    if (Single.TryParse(DRmin, out DrollMin))
                                    {
                                        ActivePilot.DynamicDampingRollMin = DrollMin;
                                    }
                                }
                                GUI.Label(SettinglabelRect(leftIndent, pidLines + dynPidLines), Localizer.Format("#LOC_BDArmory_DynamicDampingRollMin") + " :" + ActivePilot.DynamicDampingRollMin.ToString("0.0"), Label);//"dynamic damping min"

                                dynPidLines++;
                                if (contextTipsEnabled)
                                {
                                    GUI.Label(ContextLabelRect(leftIndent, pidLines + dynPidLines), Localizer.Format("#LOC_BDArmory_AIWindow_DynDampMin"), contextLabel);//"dynamic damp min"
                                    dynPidLines++;
                                }
                                if (!NumFieldsEnabled)
                                {
                                    ActivePilot.DynamicDampingRollMax =
                                        GUI.HorizontalSlider(SettingSliderRect(leftIndent, pidLines + dynPidLines, contentWidth),
                                            ActivePilot.DynamicDampingRollMax, 1f, ActivePilot.UpToEleven ? 100 : 8);
                                    ActivePilot.DynamicDampingRollMax = Mathf.Round(ActivePilot.DynamicDampingRollMax * 10f) / 10f;
                                }
                                else
                                {
                                    float DrollMax;
                                    string DRmax = GUI.TextField(SettingSliderRect(leftIndent, pidLines + dynPidLines, contentWidth), ActivePilot.DynamicDampingRollMax.ToString("0.0"));
                                    if (Single.TryParse(DRmax, out DrollMax))
                                    {
                                        ActivePilot.DynamicDampingRollMax = DrollMax;
                                    }
                                }
                                GUI.Label(SettinglabelRect(leftIndent, pidLines + dynPidLines), Localizer.Format("#LOC_BDArmory_DynamicDampingRollMax") + " :" + ActivePilot.DynamicDampingRollMax.ToString("0.0"), Label);//"dynamic damping max"

                                dynPidLines++;
                                if (contextTipsEnabled)
                                {
                                    GUI.Label(ContextLabelRect(leftIndent, pidLines + dynPidLines), Localizer.Format("#LOC_BDArmory_AIWindow_DynDampMax"), contextLabel);//"dynamic damp max"
                                    dynPidLines++;
                                }
                                if (!NumFieldsEnabled)
                                {
                                    ActivePilot.dynamicSteerDampingRollFactor =
                                        GUI.HorizontalSlider(SettingSliderRect(leftIndent, pidLines + dynPidLines, contentWidth),
                                            ActivePilot.dynamicSteerDampingRollFactor, 0.1f, ActivePilot.UpToEleven ? 100 : 10);
                                    ActivePilot.dynamicSteerDampingRollFactor = Mathf.Round(ActivePilot.dynamicSteerDampingRollFactor * 10f) / 10f;
                                }
                                else
                                {
                                    float DrollMult;
                                    string DRmult = GUI.TextField(SettingSliderRect(leftIndent, pidLines + dynPidLines, contentWidth), ActivePilot.dynamicSteerDampingRollFactor.ToString("0.0"));
                                    if (Single.TryParse(DRmult, out DrollMult))
                                    {
                                        ActivePilot.dynamicSteerDampingRollFactor = DrollMult;
                                    }
                                }
                                GUI.Label(SettinglabelRect(leftIndent, pidLines + dynPidLines), Localizer.Format("#LOC_BDArmory_DynamicDampingRollFactor") + " :" + ActivePilot.dynamicSteerDampingRollFactor.ToString("0.0"), Label);//"dynamic damping roll mult"
                                dynPidLines++;
                                if (contextTipsEnabled)
                                {
                                    GUI.Label(ContextLabelRect(leftIndent, pidLines + dynPidLines), Localizer.Format("#LOC_BDArmory_AIWindow_DynDampMult"), contextLabel);//"dynamic damp mult"
                                    dynPidLines++;
                                }
                            }
                        }
                        pidLines += dynPidLines;
                        pidLines += 1.25f;
                    }
                    GUI.EndGroup();
                    pidHeight = Mathf.Lerp(pidHeight, pidLines, 0.15f);
                    pidLines += 0.1f;

                }

                if (showAltitude)
                {
                    altLines += 0.2f;
                    GUI.BeginGroup(
                        new Rect(0, ((pidLines + altLines) * entryHeight), contentWidth, altitudeHeight * entryHeight),
                        GUIContent.none, BDArmorySetup.BDGuiSkin.box);
                    altLines += 0.25f;

                    GUI.Label(SettinglabelRect(leftIndent, altLines), Localizer.Format("#LOC_BDArmory_PilotAI_Altitudes"), BoldLabel);//"Altitudes"
                    altLines++;
                    if (!NumFieldsEnabled)
                    {
                        ActivePilot.defaultAltitude =
                            GUI.HorizontalSlider(SettingSliderRect(leftIndent, altLines, contentWidth),
                                ActivePilot.defaultAltitude, 100, ActivePilot.UpToEleven ? 100000 : 15000);
                        ActivePilot.defaultAltitude = Mathf.Round(ActivePilot.defaultAltitude / 25) * 25;
                    }
                    else
                    {
                        float DefAlt;
                        string defAlt = GUI.TextField(SettingSliderRect(leftIndent, altLines, contentWidth), ActivePilot.defaultAltitude.ToString("0"));
                        if (Single.TryParse(defAlt, out DefAlt))
                        {
                            ActivePilot.defaultAltitude = DefAlt;
                        }
                    }
                        GUI.Label(SettinglabelRect(leftIndent, altLines), Localizer.Format("#LOC_BDArmory_DefaultAltitude") + " :" + ActivePilot.defaultAltitude.ToString("0"), Label);//"default altitude"
                    altLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(ContextLabelRect(leftIndent, altLines), Localizer.Format("#LOC_BDArmory_AIWindow_DefAlt"), contextLabel);//"defalult alt"
                        altLines++;
                    }
                    if (!NumFieldsEnabled)
                    {
                        ActivePilot.minAltitude =
                            GUI.HorizontalSlider(SettingSliderRect(leftIndent, altLines, contentWidth),
                                ActivePilot.minAltitude, 25, ActivePilot.UpToEleven ? 60000 : 6000);
                        ActivePilot.minAltitude = Mathf.Round(ActivePilot.minAltitude / 25) * 25;
                    }
                    else
                    {
                        float MinAlt;
                        string AltMin = GUI.TextField(SettingSliderRect(leftIndent, altLines, contentWidth), ActivePilot.minAltitude.ToString("0"));
                        if (Single.TryParse(AltMin, out MinAlt))
                        {
                            ActivePilot.minAltitude = MinAlt;
                        }
                    }
                    GUI.Label(SettinglabelRect(leftIndent, altLines), Localizer.Format("#LOC_BDArmory_MinAltitude") + " :" + ActivePilot.minAltitude.ToString("0"), Label);//"min altitude"
                    altLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(ContextLabelRect(leftIndent, altLines), Localizer.Format("#LOC_BDArmory_AIWindow_MinAlt"), contextLabel);//"min alt"
                        altLines++;
                    }

                    ActivePilot.maxAltitudeToggle = GUI.Toggle(new Rect(leftIndent, altLines * entryHeight, contentWidth - (2 * leftIndent), entryHeight),
                    ActivePilot.maxAltitudeToggle, Localizer.Format("#LOC_BDArmory_MaxAltitude"), ActivePilot.maxAltitudeToggle ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"max altitude AGL"
                    altLines += 1.25f;

                    if (ActivePilot.maxAltitudeToggle)
                    {
                        if (!NumFieldsEnabled)
                        {
                            ActivePilot.maxAltitude =
                                GUI.HorizontalSlider(SettingSliderRect(leftIndent, altLines, contentWidth),
                                    ActivePilot.maxAltitude, 100, ActivePilot.UpToEleven ? 100000 : 15000);
                            ActivePilot.maxAltitude = Mathf.Round(ActivePilot.maxAltitude / 25) * 25;
                        }
                        else
                        {
                            float MaxAlt;
                            string AltMax = GUI.TextField(SettingSliderRect(leftIndent, altLines, contentWidth), ActivePilot.maxAltitude.ToString("0"));
                            if (Single.TryParse(AltMax, out MaxAlt))
                            {
                                ActivePilot.maxAltitude = MaxAlt;
                            }
                        }
                        GUI.Label(SettinglabelRect(leftIndent, altLines), Localizer.Format("#LOC_BDArmory_MaxAltitude") + " :" + ActivePilot.maxAltitude.ToString("0"), Label);//"max altitude"
                        altLines++;
                        if (contextTipsEnabled)
                        {
                            GUI.Label(ContextLabelRect(leftIndent, altLines), Localizer.Format("#LOC_BDArmory_AIWindow_MaxAlt"), contextLabel);//"max alt"
                            altLines++;
                        }
                    }
                    GUI.EndGroup();
                    altitudeHeight = Mathf.Lerp(altitudeHeight, altLines, 0.15f);
                    altLines += 0.1f;
                }

                if (showSpeed)
                {
                    spdLines += 0.2f;
                    GUI.BeginGroup(
                        new Rect(0, ((pidLines + altLines + spdLines) * entryHeight), contentWidth, speedHeight * entryHeight),
                        GUIContent.none, BDArmorySetup.BDGuiSkin.box);
                    spdLines += 0.25f;

                    GUI.Label(SettinglabelRect(leftIndent, spdLines), Localizer.Format("#LOC_BDArmory_PilotAI_Speeds"), BoldLabel);//"Speed"
                    spdLines++;
                    if (!NumFieldsEnabled)
                    {
                        ActivePilot.maxSpeed =
                            GUI.HorizontalSlider(SettingSliderRect(leftIndent, spdLines, contentWidth),
                                ActivePilot.maxSpeed, 20, ActivePilot.UpToEleven ? 3000 : 800);
                        ActivePilot.maxSpeed = Mathf.Round(ActivePilot.maxSpeed);
                    }
                    else
                    {
                        float MaxSpd;
                        string maxSpeed = GUI.TextField(SettingSliderRect(leftIndent, spdLines, contentWidth), ActivePilot.maxSpeed.ToString("0"));
                        if (Single.TryParse(maxSpeed, out MaxSpd))
                        {
                            ActivePilot.maxSpeed = MaxSpd;
                        }
                    }
                    GUI.Label(SettinglabelRect(leftIndent, spdLines), Localizer.Format("#LOC_BDArmory_MaxSpeed") + " :" + ActivePilot.maxSpeed.ToString("0"), Label);//"max speed"

                    spdLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(ContextLabelRect(leftIndent, spdLines), Localizer.Format("#LOC_BDArmory_AIWindow_maxSpeed"), contextLabel);//"max speed"
                        spdLines++;
                    }
                    if (!NumFieldsEnabled)
                    {
                        ActivePilot.takeOffSpeed =
                            GUI.HorizontalSlider(SettingSliderRect(leftIndent, spdLines, contentWidth),
                                ActivePilot.takeOffSpeed, 10f, ActivePilot.UpToEleven ? 2000 : 200);
                        ActivePilot.takeOffSpeed = Mathf.Round(ActivePilot.takeOffSpeed);
                    }
                    else
                    {
                        float takeoff;
                        string takeoffspd = GUI.TextField(SettingSliderRect(leftIndent, spdLines, contentWidth), ActivePilot.takeOffSpeed.ToString("0"));
                        if (Single.TryParse(takeoffspd, out takeoff))
                        {
                            ActivePilot.takeOffSpeed = takeoff;
                        }
                    }
                    GUI.Label(SettinglabelRect(leftIndent, spdLines), Localizer.Format("#LOC_BDArmory_TakeOffSpeed") + " :" + ActivePilot.takeOffSpeed.ToString("0"), Label);//"takeoff speed"

                    spdLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(ContextLabelRect(leftIndent, spdLines), Localizer.Format("#LOC_BDArmory_AIWindow_takeoff"), contextLabel);//"takeoff speed help"
                        spdLines++;
                    }
                    if (!NumFieldsEnabled)
                    {
                        ActivePilot.minSpeed =
                            GUI.HorizontalSlider(SettingSliderRect(leftIndent, spdLines, contentWidth),
                                ActivePilot.minSpeed, 10, ActivePilot.UpToEleven ? 2000 : 200);
                        ActivePilot.minSpeed = Mathf.Round(ActivePilot.minSpeed);
                    }
                    else
                    {
                        float minSpeed;
                        string SpeedMin = GUI.TextField(SettingSliderRect(leftIndent, spdLines, contentWidth), ActivePilot.minSpeed.ToString("0"));
                        if (Single.TryParse(SpeedMin, out minSpeed))
                        {
                            ActivePilot.minSpeed = minSpeed;
                        }
                    }
                    GUI.Label(SettinglabelRect(leftIndent, spdLines), Localizer.Format("#LOC_BDArmory_MinSpeed") + " :" + ActivePilot.minSpeed.ToString("0"), Label);//"min speed"

                    spdLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(ContextLabelRect(leftIndent, spdLines), Localizer.Format("#LOC_BDArmory_AIWindow_minSpeed"), contextLabel);//"min speed help"
                        spdLines++;
                    }
                    if (!NumFieldsEnabled)
                    {
                        ActivePilot.strafingSpeed =
                            GUI.HorizontalSlider(SettingSliderRect(leftIndent, spdLines, contentWidth),
                                ActivePilot.strafingSpeed, 10, 200);
                        ActivePilot.strafingSpeed = Mathf.Round(ActivePilot.strafingSpeed);
                    }
                    else
                    {
                        float gndSpeed;
                        string strafespeed = GUI.TextField(SettingSliderRect(leftIndent, spdLines, contentWidth), ActivePilot.strafingSpeed.ToString("0"));
                        if (Single.TryParse(strafespeed, out gndSpeed))
                        {
                            ActivePilot.strafingSpeed = gndSpeed;
                        }
                    }
                    GUI.Label(SettinglabelRect(leftIndent, spdLines), Localizer.Format("#LOC_BDArmory_StrafingSpeed") + " :" + ActivePilot.strafingSpeed.ToString("0"), Label);//"strafing speed"

                    spdLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(ContextLabelRect(leftIndent, spdLines), Localizer.Format("#LOC_BDArmory_AIWindow_atkSpeed"), contextLabel);//"strafe speed"
                        spdLines++;
                    }
                    if (!NumFieldsEnabled)
                    {
                        ActivePilot.idleSpeed =
                            GUI.HorizontalSlider(SettingSliderRect(leftIndent, spdLines, contentWidth),
                                ActivePilot.idleSpeed, 10, ActivePilot.UpToEleven ? 3000 : 200);
                        ActivePilot.idleSpeed = Mathf.Round(ActivePilot.idleSpeed);
                    }
                    else
                    {
                        float idleSpeed;
                        string cruisespeed = GUI.TextField(SettingSliderRect(leftIndent, spdLines, contentWidth), ActivePilot.idleSpeed.ToString("0"));
                        if (Single.TryParse(cruisespeed, out idleSpeed))
                        {
                            ActivePilot.idleSpeed = idleSpeed;
                        }
                    }
                    GUI.Label(SettinglabelRect(leftIndent, spdLines), Localizer.Format("#LOC_BDArmory_IdleSpeed") + " :" + ActivePilot.idleSpeed.ToString("0"), Label);//"idle speed"

                    spdLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(ContextLabelRect(leftIndent, spdLines), Localizer.Format("#LOC_BDArmory_AIWindow_idleSpeed"), contextLabel);//"idle speed context help"
                        spdLines++;
                    }
                    GUI.EndGroup();
                    speedHeight = Mathf.Lerp(speedHeight, spdLines, 0.15f);
                    spdLines += 0.1f;
                }

                if (showControl)
                {
                    ctrlLines += 0.2f;
                    GUI.BeginGroup(
                        new Rect(0, ((pidLines + altLines + spdLines + ctrlLines) * entryHeight), contentWidth, controlHeight * entryHeight),
                        GUIContent.none, BDArmorySetup.BDGuiSkin.box);
                    ctrlLines += 0.25f;

                    GUI.Label(SettinglabelRect(leftIndent, ctrlLines), Localizer.Format("#LOC_BDArmory_AIWindow_ControlLimits"), BoldLabel);//"Control"
                    ctrlLines++;
                    if (!NumFieldsEnabled)
                    {
                        ActivePilot.maxSteer =
                            GUI.HorizontalSlider(SettingSliderRect(leftIndent, ctrlLines, contentWidth),
                                ActivePilot.maxSteer, 0.1f, 1);
                        ActivePilot.maxSteer = Mathf.Round(ActivePilot.maxSteer * 20f) / 20f;
                    }
                    else
                    {
                        float maxSteer;
                        string steermax = GUI.TextField(SettingSliderRect(leftIndent, ctrlLines, contentWidth), ActivePilot.maxSteer.ToString("0.00"));
                        if (Single.TryParse(steermax, out maxSteer))
                        {
                            ActivePilot.maxSteer = maxSteer;
                        }
                    }
                    GUI.Label(SettinglabelRect(leftIndent, ctrlLines), Localizer.Format("#LOC_BDArmory_AIWindow_LowSpeedSteerLimiter") + " :" + ActivePilot.maxSteer.ToString("0.00"), Label);//"Low speed Limiter"

                    ctrlLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(ContextLabelRect(leftIndent, ctrlLines), Localizer.Format("#LOC_BDArmory_AIWindow_LSSL"), contextLabel);//"Low limiter context"
                        ctrlLines++;
                    }
                    if (!NumFieldsEnabled)
                    {
                        ActivePilot.lowSpeedSwitch =
                            GUI.HorizontalSlider(SettingSliderRect(leftIndent, ctrlLines, contentWidth),
                                ActivePilot.lowSpeedSwitch, 100f, 500);
                        ActivePilot.lowSpeedSwitch = Mathf.Round(ActivePilot.lowSpeedSwitch);
                    }
                    else
                    {
                        float lowspdlimit;
                        string lsls = GUI.TextField(SettingSliderRect(leftIndent, ctrlLines, contentWidth), ActivePilot.lowSpeedSwitch.ToString("0"));
                        if (Single.TryParse(lsls, out lowspdlimit))
                        {
                            ActivePilot.lowSpeedSwitch = lowspdlimit;
                        }
                    }
                    GUI.Label(SettinglabelRect(leftIndent, ctrlLines), Localizer.Format("#LOC_BDArmory_AIWindow_LowSpeedLimiterSpeed") + " :" + ActivePilot.lowSpeedSwitch.ToString("0"), Label);//"dynamic damping max"

                    ctrlLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(ContextLabelRect(leftIndent, ctrlLines), Localizer.Format("#LOC_BDArmory_AIWindow_LSLS"), contextLabel);//"dynamic damp max"
                        ctrlLines++;
                    }
                    if (!NumFieldsEnabled)
                    {
                        ActivePilot.maxSteerAtMaxSpeed =
                            GUI.HorizontalSlider(SettingSliderRect(leftIndent, ctrlLines, contentWidth),
                                ActivePilot.maxSteerAtMaxSpeed, 0.1f, 1);
                        ActivePilot.maxSteerAtMaxSpeed = Mathf.Round(ActivePilot.maxSteerAtMaxSpeed * 20f) / 20f;
                    }
                    else
                    {
                        float hispdlimiter;
                        string hssl = GUI.TextField(SettingSliderRect(leftIndent, ctrlLines, contentWidth), ActivePilot.maxSteerAtMaxSpeed.ToString("0.00"));
                        if (Single.TryParse(hssl, out hispdlimiter))
                        {
                            ActivePilot.maxSteerAtMaxSpeed = hispdlimiter;
                        }
                    }
                    GUI.Label(SettinglabelRect(leftIndent, ctrlLines), Localizer.Format("#LOC_BDArmory_AIWindow_HighSpeedSteerLimiter") + " :" + ActivePilot.maxSteerAtMaxSpeed.ToString("0.00"), Label);//"dynamic damping min"

                    ctrlLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(ContextLabelRect(leftIndent, ctrlLines), Localizer.Format("#LOC_BDArmory_AIWindow_HSSL"), contextLabel);//"dynamic damp min"
                        ctrlLines++;
                    }
                    if (!NumFieldsEnabled)
                    {
                        ActivePilot.cornerSpeed =
                            GUI.HorizontalSlider(SettingSliderRect(leftIndent, ctrlLines, contentWidth),
                                ActivePilot.cornerSpeed, 10, 200);
                        ActivePilot.cornerSpeed = Mathf.Round(ActivePilot.cornerSpeed);
                    }
                    else
                    {
                        float hispeedlimit;
                        string hsls = GUI.TextField(SettingSliderRect(leftIndent, ctrlLines, contentWidth), ActivePilot.cornerSpeed.ToString("0"));
                        if (Single.TryParse(hsls, out hispeedlimit))
                        {
                            ActivePilot.cornerSpeed = hispeedlimit;
                        }
                    }
                    GUI.Label(SettinglabelRect(leftIndent, ctrlLines), Localizer.Format("#LOC_BDArmory_AIWindow_HighSpeedLimiterSpeed") + " :" + ActivePilot.cornerSpeed.ToString("0"), Label);//"dynamic damping min"

                    ctrlLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(ContextLabelRect(leftIndent, ctrlLines), Localizer.Format("#LOC_BDArmory_AIWindow_HSLS"), contextLabel);//"dynamic damp min"
                        ctrlLines++;
                    }
                    if (!NumFieldsEnabled)
                    {
                        ActivePilot.maxBank =
                            GUI.HorizontalSlider(SettingSliderRect(leftIndent, ctrlLines, contentWidth),
                                ActivePilot.maxBank, 10, 180);
                        ActivePilot.maxBank = Mathf.Round(ActivePilot.maxBank / 5) * 5;
                    }
                    else
                    {
                        float maxbank;
                        string bank = GUI.TextField(SettingSliderRect(leftIndent, ctrlLines, contentWidth), ActivePilot.maxBank.ToString("0"));
                        if (Single.TryParse(bank, out maxbank))
                        {
                            ActivePilot.maxBank = maxbank;
                        }
                    }
                    GUI.Label(SettinglabelRect(leftIndent, ctrlLines), Localizer.Format("#LOC_BDArmory_BankLimiter") + " :" + ActivePilot.maxBank.ToString("0"), Label);//"dynamic damping min"

                    ctrlLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(ContextLabelRect(leftIndent, ctrlLines), Localizer.Format("#LOC_BDArmory_AIWindow_bankLimit"), contextLabel);//"dynamic damp min"
                        ctrlLines++;
                    }
                    if (!NumFieldsEnabled)
                    {
                        ActivePilot.maxAllowedGForce =
                            GUI.HorizontalSlider(SettingSliderRect(leftIndent, ctrlLines, contentWidth),
                                ActivePilot.maxAllowedGForce, 2, ActivePilot.UpToEleven ? 1000 : 45);
                        ActivePilot.maxAllowedGForce = Mathf.Round(ActivePilot.maxAllowedGForce * 4f) / 4f;
                    }
                    else
                    {
                        float Gforce;
                        string maxG = GUI.TextField(SettingSliderRect(leftIndent, ctrlLines, contentWidth), ActivePilot.maxAllowedGForce.ToString("0.00"));
                        if (Single.TryParse(maxG, out Gforce))
                        {
                            ActivePilot.maxAllowedGForce = Gforce;
                        }
                    }
                    GUI.Label(SettinglabelRect(leftIndent, ctrlLines), Localizer.Format("#LOC_BDArmory_maxAllowedGForce") + " :" + ActivePilot.maxAllowedGForce.ToString("0.00"), Label);//"dynamic damping min"

                    ctrlLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(ContextLabelRect(leftIndent, ctrlLines), Localizer.Format("#LOC_BDArmory_AIWindow_GForce"), contextLabel);//"dynamic damp min"
                        ctrlLines++;
                    }
                    if (!NumFieldsEnabled)
                    {
                        ActivePilot.maxAllowedAoA =
                            GUI.HorizontalSlider(SettingSliderRect(leftIndent, ctrlLines, contentWidth),
                                ActivePilot.maxAllowedAoA, 0, ActivePilot.UpToEleven ? 180 : 85);
                        ActivePilot.maxAllowedAoA = Mathf.Round(ActivePilot.maxAllowedAoA * 0.4f) / 0.4f;
                    }
                    else
                    {
                        float AoAlimit;
                        string maxAoA = GUI.TextField(SettingSliderRect(leftIndent, ctrlLines, contentWidth), ActivePilot.maxAllowedAoA.ToString("0.00"));
                        if (Single.TryParse(maxAoA, out AoAlimit))
                        {
                            ActivePilot.maxAllowedAoA = AoAlimit;
                        }
                    }
                    GUI.Label(SettinglabelRect(leftIndent, ctrlLines), Localizer.Format("#LOC_BDArmory_maxAllowedAoA") + " :" + ActivePilot.maxAllowedAoA.ToString("0.0"), Label);//"dynamic damping min"

                    ctrlLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(ContextLabelRect(leftIndent, ctrlLines), Localizer.Format("#LOC_BDArmory_AIWindow_AoA"), contextLabel);//"dynamic damp min"
                        ctrlLines++;
                    }
                    GUI.EndGroup();
                    controlHeight = Mathf.Lerp(controlHeight, ctrlLines, 0.15f);
                    ctrlLines += 0.1f;
                }

                if (showEvade)
                {
                    evadeLines += 0.2f;
                    GUI.BeginGroup(
                        new Rect(0, ((pidLines + altLines + spdLines + ctrlLines + evadeLines) * entryHeight), contentWidth, evasionHeight * entryHeight),
                        GUIContent.none, BDArmorySetup.BDGuiSkin.box);
                    evadeLines += 0.25f;

                    GUI.Label(SettinglabelRect(leftIndent, evadeLines), Localizer.Format("#LOC_BDArmory_AIWindow_EvadeExtend"), BoldLabel);//"Speed"
                    evadeLines++;
                    if (!NumFieldsEnabled)
                    {
                        ActivePilot.minEvasionTime =
                            GUI.HorizontalSlider(SettingSliderRect(leftIndent, evadeLines, contentWidth),
                                ActivePilot.minEvasionTime, 0f, ActivePilot.UpToEleven ? 10 : 1);
                        ActivePilot.minEvasionTime = Mathf.Round(ActivePilot.minEvasionTime * 20f) / 20f;
                    }
                    else
                    {
                        float evademin;
                        string minEtime = GUI.TextField(SettingSliderRect(leftIndent, evadeLines, contentWidth), ActivePilot.minEvasionTime.ToString("0.00"));
                        if (Single.TryParse(minEtime, out evademin))
                        {
                            ActivePilot.minEvasionTime = evademin;
                        }
                    }
                    GUI.Label(SettinglabelRect(leftIndent, evadeLines), Localizer.Format("#LOC_BDArmory_MinEvasionTime") + " :" + ActivePilot.minEvasionTime.ToString("0.00"), Label);//"dynamic damping min"

                    evadeLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(ContextLabelRect(leftIndent, evadeLines), Localizer.Format("#LOC_BDArmory_AIWindow_MinEvade"), contextLabel);//"dynamic damp min"
                        evadeLines++;
                    }
                    if (!NumFieldsEnabled)
                    {
                        ActivePilot.evasionThreshold =
                            GUI.HorizontalSlider(
                                SettingSliderRect(leftIndent, evadeLines, contentWidth),
                                ActivePilot.evasionThreshold, 0, ActivePilot.UpToEleven ? 300 : 100);
                        ActivePilot.evasionThreshold = Mathf.Round(ActivePilot.evasionThreshold);
                    }
                    else
                    {
                        float evadethresh;
                        string minEdist = GUI.TextField(SettingSliderRect(leftIndent, evadeLines, contentWidth), ActivePilot.evasionThreshold.ToString("0"));
                        if (Single.TryParse(minEdist, out evadethresh))
                        {
                            ActivePilot.evasionThreshold = evadethresh;
                        }
                    }
                    GUI.Label(SettinglabelRect(leftIndent, evadeLines), Localizer.Format("#LOC_BDArmory_AIWindow_EvasionThreshold") + " :" + ActivePilot.evasionThreshold.ToString("0"), Label);//"dynamic damping max"

                    evadeLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(ContextLabelRect(leftIndent, evadeLines), Localizer.Format("#LOC_BDArmory_AIWindow_evadeDist"), contextLabel);//"dynamic damp max"
                        evadeLines++;
                    }
                    if (!NumFieldsEnabled)
                    {
                        ActivePilot.evasionTimeThreshold =
                            GUI.HorizontalSlider(SettingSliderRect(leftIndent, evadeLines, contentWidth),
                                ActivePilot.evasionTimeThreshold, 0, ActivePilot.UpToEleven ? 1 : 3);
                        ActivePilot.evasionTimeThreshold = Mathf.Round(ActivePilot.evasionTimeThreshold * 100f) / 100f;
                    }
                    else
                    {
                        float evadeTthresh;
                        string minTdist = GUI.TextField(SettingSliderRect(leftIndent, evadeLines, contentWidth), ActivePilot.evasionTimeThreshold.ToString("0.00"));
                        if (Single.TryParse(minTdist, out evadeTthresh))
                        {
                            ActivePilot.evasionTimeThreshold = evadeTthresh;
                        }
                    }
                    GUI.Label(SettinglabelRect(leftIndent, evadeLines), Localizer.Format("#LOC_BDArmory_AIWindow_EvasionTimeThreshold") + " :" + ActivePilot.evasionTimeThreshold.ToString("0.00"), Label);//"dynamic damping min"

                    evadeLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(ContextLabelRect(leftIndent, evadeLines), Localizer.Format("#LOC_BDArmory_AIWindow_evadetimeDist"), contextLabel);//"dynamic damp min"
                        evadeLines++;
                    }
                    if (!NumFieldsEnabled)
                    {
                        ActivePilot.collisionAvoidanceThreshold =
                            GUI.HorizontalSlider(SettingSliderRect(leftIndent, evadeLines, contentWidth),
                                ActivePilot.collisionAvoidanceThreshold, 0, 50);
                        ActivePilot.collisionAvoidanceThreshold = Mathf.Round(ActivePilot.collisionAvoidanceThreshold);
                    }
                    else
                    {
                        float avoidThresh;
                        string avoiddist = GUI.TextField(SettingSliderRect(leftIndent, evadeLines, contentWidth), ActivePilot.collisionAvoidanceThreshold.ToString("0"));
                        if (Single.TryParse(avoiddist, out avoidThresh))
                        {
                            ActivePilot.collisionAvoidanceThreshold = avoidThresh;
                        }
                    }
                    GUI.Label(SettinglabelRect(leftIndent, evadeLines), Localizer.Format("#LOC_BDArmory_AIWindow_CollisionAvoidanceThreshold") + " :" + ActivePilot.collisionAvoidanceThreshold.ToString("0"), Label);//"dynamic damping min"

                    evadeLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(ContextLabelRect(leftIndent, evadeLines), Localizer.Format("#LOC_BDArmory_AIWindow_ColDist"), contextLabel);//"dynamic damp min"
                        evadeLines++;
                    }
                    if (!NumFieldsEnabled)
                    {
                        ActivePilot.vesselCollisionAvoidancePeriod =
                            GUI.HorizontalSlider(SettingSliderRect(leftIndent, evadeLines, contentWidth),
                                ActivePilot.vesselCollisionAvoidancePeriod, 0, 3);
                        ActivePilot.vesselCollisionAvoidancePeriod = Mathf.Round(ActivePilot.vesselCollisionAvoidancePeriod * 10f) / 10f;
                    }
                    else
                    {
                        float avoidperiod;
                        string avoidtime = GUI.TextField(SettingSliderRect(leftIndent, evadeLines, contentWidth), ActivePilot.vesselCollisionAvoidancePeriod.ToString("0.0"));
                        if (Single.TryParse(avoidtime, out avoidperiod))
                        {
                            ActivePilot.vesselCollisionAvoidancePeriod = avoidperiod;
                        }
                    }
                    GUI.Label(SettinglabelRect(leftIndent, evadeLines), Localizer.Format("#LOC_BDArmory_AIWindow_CollisionAvoidancePeriod") + " :" + ActivePilot.vesselCollisionAvoidancePeriod.ToString("0.0"), Label);//"dynamic damping min"

                    evadeLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(ContextLabelRect(leftIndent, evadeLines), Localizer.Format("#LOC_BDArmory_AIWindow_ColTime"), contextLabel);//"dynamic damp min"
                        evadeLines++;
                    }
                    if (!NumFieldsEnabled)
                    {
                        ActivePilot.vesselStandoffDistance =
                            GUI.HorizontalSlider(SettingSliderRect(leftIndent, evadeLines, contentWidth),
                                ActivePilot.vesselStandoffDistance, 2, ActivePilot.UpToEleven ? 5000 : 1000);
                        ActivePilot.vesselStandoffDistance = Mathf.Round(ActivePilot.vesselStandoffDistance / 50) * 50;
                    }
                    else
                    {
                        float socialDist;
                        string stayaway = GUI.TextField(SettingSliderRect(leftIndent, evadeLines, contentWidth), ActivePilot.vesselStandoffDistance.ToString("0"));
                        if (Single.TryParse(stayaway, out socialDist))
                        {
                            ActivePilot.vesselStandoffDistance = socialDist;
                        }
                    }
                    GUI.Label(SettinglabelRect(leftIndent, evadeLines), Localizer.Format("#LOC_BDArmory_AIWindow_StandoffDistance") + " :" + ActivePilot.vesselStandoffDistance.ToString("0"), Label);//"dynamic damping min"

                    evadeLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(ContextLabelRect(leftIndent, evadeLines), Localizer.Format("#LOC_BDArmory_AIWindow_standoff"), contextLabel);//"dynamic damp min"
                        evadeLines += 1.25f;
                    }
                    if (ActivePilot.canExtend)
                    {
                        if (!NumFieldsEnabled)
                        {
                            ActivePilot.extendMult =
                                GUI.HorizontalSlider(SettingSliderRect(leftIndent, evadeLines, contentWidth),
                                    ActivePilot.extendMult, 0, ActivePilot.UpToEleven ? 200 : 2);
                            ActivePilot.extendMult = Mathf.Round(ActivePilot.extendMult * 10f) / 10f;
                        }
                        else
                        {
                            float extendmult;
                            string fleedist = GUI.TextField(SettingSliderRect(leftIndent, evadeLines, contentWidth), ActivePilot.extendMult.ToString("0.0"));
                            if (Single.TryParse(fleedist, out extendmult))
                            {
                                ActivePilot.extendMult = extendmult;
                            }
                        }
                            GUI.Label(SettinglabelRect(leftIndent, evadeLines), Localizer.Format("#LOC_BDArmory_AIWindow_ExtendMultiplier") + " :" + ActivePilot.extendMult.ToString("0.0"), Label);//"dynamic damping min"

                        evadeLines++;
                        if (contextTipsEnabled)
                        {
                            GUI.Label(ContextLabelRect(leftIndent, evadeLines), Localizer.Format("#LOC_BDArmory_AIWindow_ExtendMult"), contextLabel);//"dynamic damp min"
                            evadeLines++;
                        }
                        if (!NumFieldsEnabled)
                        {
                            ActivePilot.extendTargetVel =
                                GUI.HorizontalSlider(SettingSliderRect(leftIndent, evadeLines, contentWidth),
                                    ActivePilot.extendTargetVel, 0, 2);
                            ActivePilot.extendTargetVel = Mathf.Round(ActivePilot.extendTargetVel * 10f) / 10f;
                        }
                        else
                        {
                            float EtargetVel;
                            string relativeV = GUI.TextField(SettingSliderRect(leftIndent, evadeLines, contentWidth), ActivePilot.extendTargetVel.ToString("0.0"));
                            if (Single.TryParse(relativeV, out EtargetVel))
                            {
                                ActivePilot.extendTargetVel = EtargetVel;
                            }
                        }
                        GUI.Label(SettinglabelRect(leftIndent, evadeLines), Localizer.Format("#LOC_BDArmory_AIWindow_ExtendTargetVel") + " :" + ActivePilot.extendTargetVel.ToString("0.0"), Label);//"dynamic damping min"

                        evadeLines++;
                        if (contextTipsEnabled)
                        {
                            GUI.Label(ContextLabelRect(leftIndent, evadeLines), Localizer.Format("#LOC_BDArmory_AIWindow_Extendvel"), contextLabel);//"dynamic damp min"
                            evadeLines++;
                        }
                        if (!NumFieldsEnabled)
                        {
                            ActivePilot.extendTargetAngle =
                                GUI.HorizontalSlider(SettingSliderRect(leftIndent, evadeLines, contentWidth),
                                    ActivePilot.extendTargetAngle, 0, 180);
                            ActivePilot.extendTargetAngle = Mathf.Round(ActivePilot.extendTargetAngle);
                        }
                        else
                        {
                            float EtargetAngle;
                            string relativeA = GUI.TextField(SettingSliderRect(leftIndent, evadeLines, contentWidth), ActivePilot.extendTargetAngle.ToString("0"));
                            if (Single.TryParse(relativeA, out EtargetAngle))
                            {
                                ActivePilot.extendTargetAngle = EtargetAngle;
                            }
                        }
                        GUI.Label(SettinglabelRect(leftIndent, evadeLines), Localizer.Format("#LOC_BDArmory_AIWindow_ExtendTargetAngle") + " :" + ActivePilot.extendTargetAngle.ToString("0"), Label);// "dynamic damping min"

                        evadeLines++;
                        if (contextTipsEnabled)
                        {
                            GUI.Label(ContextLabelRect(leftIndent, evadeLines), Localizer.Format("#LOC_BDArmory_AIWindow_ExtendAngle"), contextLabel);//"dynamic damp min"
                            evadeLines++;
                        }
                        if (!NumFieldsEnabled)
                        {
                            ActivePilot.extendTargetDist =
                                GUI.HorizontalSlider(SettingSliderRect(leftIndent, evadeLines, contentWidth),
                                    ActivePilot.extendTargetDist, 0, 5000);
                            ActivePilot.extendTargetDist = Mathf.Round(ActivePilot.extendTargetDist / 25) * 25;
                        }
                        else
                        {
                            float EtargetDist;
                            string relativeD = GUI.TextField(SettingSliderRect(leftIndent, evadeLines, contentWidth), ActivePilot.extendTargetDist.ToString("0.00"));
                            if (Single.TryParse(relativeD, out EtargetDist))
                            {
                                ActivePilot.extendTargetDist = EtargetDist;
                            }
                        }
                            GUI.Label(SettinglabelRect(leftIndent, evadeLines), Localizer.Format("#LOC_BDArmory_AIWindow_ExtendTargetDist") + " :" + ActivePilot.extendTargetDist.ToString("0.00"), Label);//"dynamic damping min"

                        evadeLines++;
                        if (contextTipsEnabled)
                        {
                            GUI.Label(ContextLabelRect(leftIndent, evadeLines), Localizer.Format("#LOC_BDArmory_AIWindow_ExtendDist"), contextLabel);//"dynamic damp min"
                            evadeLines++;
                        }
                    }
                    ActivePilot.canExtend = GUI.Toggle(ToggleButtonRect(leftIndent, evadeLines, contentWidth),
ActivePilot.canExtend, Localizer.Format("#LOC_BDArmory_ExtendToggle"), ActivePilot.canExtend ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"Dynamic pid"
                    evadeLines++;

                    GUI.EndGroup();
                    evasionHeight = Mathf.Lerp(evasionHeight, evadeLines, 0.15f);
                    evadeLines += 0.1f;
                }

                if (showTerrain)
                {
                    gndLines += 0.2f;
                    GUI.BeginGroup(
                        new Rect(0, ((pidLines + altLines + spdLines + ctrlLines + evadeLines + gndLines) * entryHeight), contentWidth, terrainHeight * entryHeight),
                        GUIContent.none, BDArmorySetup.BDGuiSkin.box);
                    gndLines += 0.25f;

                    GUI.Label(SettinglabelRect(leftIndent, gndLines), Localizer.Format("#LOC_BDArmory_PilotAI_Terrain"), BoldLabel);//"Speed"
                    gndLines++;
                    if (!NumFieldsEnabled)
                    {
                        ActivePilot.turnRadiusTwiddleFactorMin =
                            GUI.HorizontalSlider(SettingSliderRect(leftIndent, gndLines, contentWidth),
                                ActivePilot.turnRadiusTwiddleFactorMin, 1, ActivePilot.UpToEleven ? 10 : 5);
                        ActivePilot.turnRadiusTwiddleFactorMin = Mathf.Round(ActivePilot.turnRadiusTwiddleFactorMin * 10f) / 10f;
                    }
                    else
                    {
                        float TwiddleMin;
                        string mintwiddle = GUI.TextField(SettingSliderRect(leftIndent, gndLines, contentWidth), ActivePilot.turnRadiusTwiddleFactorMin.ToString("0.0"));
                        if (Single.TryParse(mintwiddle, out TwiddleMin))
                        {
                            ActivePilot.turnRadiusTwiddleFactorMin = TwiddleMin;
                        }
                    }
                    GUI.Label(SettinglabelRect(leftIndent, gndLines), Localizer.Format("#LOC_BDArmory_AIWindow_TurnRadiusMin") + " :" + ActivePilot.turnRadiusTwiddleFactorMin.ToString("0.0"), Label); //"dynamic damping min"

                    gndLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(ContextLabelRect(leftIndent, gndLines), Localizer.Format("#LOC_BDArmory_AIWindow_terrainMin"), contextLabel);//"dynamic damp min"
                        gndLines++;
                    }
                    if (!NumFieldsEnabled)
                    {
                        ActivePilot.turnRadiusTwiddleFactorMax =
                            GUI.HorizontalSlider(SettingSliderRect(leftIndent, gndLines, contentWidth),
                                ActivePilot.turnRadiusTwiddleFactorMax, 1, ActivePilot.UpToEleven ? 10 : 5);
                        ActivePilot.turnRadiusTwiddleFactorMax = Mathf.Round(ActivePilot.turnRadiusTwiddleFactorMax * 10) / 10;
                    }
                    else
                    {
                        float TwiddleMax;
                        string maxtwiddle = GUI.TextField(SettingSliderRect(leftIndent, gndLines, contentWidth), ActivePilot.turnRadiusTwiddleFactorMax.ToString("0.0"));
                        if (Single.TryParse(maxtwiddle, out TwiddleMax))
                        {
                            ActivePilot.turnRadiusTwiddleFactorMax = TwiddleMax;
                        }
                    }
                    GUI.Label(SettinglabelRect(leftIndent, gndLines), Localizer.Format("#LOC_BDArmory_AIWindow_TurnRadiusMax") + " :" + ActivePilot.turnRadiusTwiddleFactorMax.ToString("0.0"), Label);//"dynamic damping min"

                    gndLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(ContextLabelRect(leftIndent, gndLines), Localizer.Format("#LOC_BDArmory_AIWindow_terrainMax"), contextLabel);//"dynamic damp min"
                        gndLines++;
                    }
                    GUI.EndGroup();
                    terrainHeight = Mathf.Lerp(terrainHeight, gndLines, 0.15f);
                    gndLines += 0.1f;
                }

                if (showRam)
                {
                    ramLines += 0.2f;
                    GUI.BeginGroup(
                        new Rect(0, ((pidLines + altLines + spdLines + ctrlLines + evadeLines + gndLines + ramLines) * entryHeight), contentWidth, rammingHeight * entryHeight),
                        GUIContent.none, BDArmorySetup.BDGuiSkin.box);
                    ramLines += 0.25f;

                    GUI.Label(SettinglabelRect(leftIndent, ramLines), Localizer.Format("#LOC_BDArmory_PilotAI_Ramming"), BoldLabel);//"Speed"
                    ramLines++;

                    ActivePilot.allowRamming = GUI.Toggle(ToggleButtonRect(leftIndent, ramLines, contentWidth),
ActivePilot.allowRamming, Localizer.Format("#LOC_BDArmory_AllowRamming"), ActivePilot.allowRamming ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"Dynamic pid"
                    ramLines += 1.25f;

                    if (ActivePilot.allowRamming)
                    {
                        if (!NumFieldsEnabled)
                        {
                            ActivePilot.controlSurfaceLag =
                                GUI.HorizontalSlider(SettingSliderRect(leftIndent, ramLines, contentWidth),
                                    ActivePilot.controlSurfaceLag, 0, ActivePilot.UpToEleven ? 01f : 0.2f);
                            ActivePilot.controlSurfaceLag = Mathf.Round(ActivePilot.controlSurfaceLag * 100) / 100;
                        }
                        else
                        {
                            float ramLag;
                            string rammin = GUI.TextField(SettingSliderRect(leftIndent, ramLines, contentWidth), ActivePilot.controlSurfaceLag.ToString("0.00"));
                            if (Single.TryParse(rammin, out ramLag))
                            {
                                ActivePilot.controlSurfaceLag = ramLag;
                            }
                        }
                        GUI.Label(SettinglabelRect(leftIndent, ramLines), Localizer.Format("#LOC_BDArmory_AIWindow_ControlSurfaceLag") + " :" + ActivePilot.controlSurfaceLag.ToString("0.00"), Label);//"dynamic damping min"

                        ramLines++;
                        if (contextTipsEnabled)
                        {
                            GUI.Label(ContextLabelRect(leftIndent, ramLines), Localizer.Format("#LOC_BDArmory_AIWindow_ramLag"), contextLabel);//"dynamic damp min"
                            ramLines++;
                        }
                    }
                    GUI.EndGroup();
                    rammingHeight = Mathf.Lerp(rammingHeight, ramLines, 0.15f);
                    ramLines += 0.1f;
                }

                if (showMisc)
                {
                    miscLines += 0.2f;
                    GUI.BeginGroup(
                        new Rect(leftIndent, ((pidLines + altLines + spdLines + ctrlLines + evadeLines + gndLines + ramLines + miscLines) * entryHeight), contentWidth, miscHeight * entryHeight),
                        GUIContent.none, BDArmorySetup.BDGuiSkin.box);
                    miscLines += 0.25f;

                    GUI.Label(SettinglabelRect(leftIndent, miscLines), Localizer.Format("#LOC_BDArmory_Orbit"), BoldLabel);//"Speed"
                    miscLines++;

                    ActivePilot.ClockwiseOrbit = GUI.Toggle(ToggleButtonRect(leftIndent, miscLines, contentWidth),
                    ActivePilot.ClockwiseOrbit, ActivePilot.ClockwiseOrbit ? Localizer.Format("#LOC_BDArmory_Orbit_enabledText") : Localizer.Format("#LOC_BDArmory_Orbit_disabledText"), ActivePilot.ClockwiseOrbit ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"Dynamic pid"
                    miscLines += 1.25f;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(ContextLabelRect(leftIndent, miscLines), Localizer.Format("#LOC_BDArmory_AIWindow_orbit"), Label);//"dynamic damp min"
                        miscLines++;
                    }

                    GUI.Label(SettinglabelRect(leftIndent, miscLines), Localizer.Format("#LOC_BDArmory_StandbyMode"), BoldLabel);//"Speed"
                    miscLines++;

                    ActivePilot.standbyMode = GUI.Toggle(ToggleButtonRect(leftIndent, miscLines, contentWidth),
                    ActivePilot.standbyMode, ActivePilot.standbyMode ? Localizer.Format("#LOC_BDArmory_On") : Localizer.Format("#LOC_BDArmory_Off"), ActivePilot.standbyMode ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"Dynamic pid"
                    miscLines += 1.25f;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(ContextLabelRect(leftIndent, miscLines), Localizer.Format("#LOC_BDArmory_AIWindow_standby"), Label);//"dynamic damp min"
                        miscLines++;
                    }

                    GUI.EndGroup();
                    miscHeight = Mathf.Lerp(miscHeight, miscLines, 0.15f);
                    miscLines += 0.1f;
                }
                height = Mathf.Max(Mathf.Lerp(height, (pidLines + altLines + spdLines + ctrlLines + evadeLines + gndLines + ramLines + miscLines) * entryHeight + contentTop + 5, 1), (line * entryHeight) + contentTop);
                GUI.EndGroup();
                GUI.EndScrollView();
            }
            else if (ActiveDriver != null)
            {
                line++;
                ActiveDriver.UpToEleven = GUI.Toggle(SubsectionRect(leftIndent, line),
                    ActiveDriver.UpToEleven, ActiveDriver.UpToEleven ? Localizer.Format("#LOC_BDArmory_UnclampTuning_enabledText") : Localizer.Format("#LOC_BDArmory_UnclampTuning_disabledText"), ActivePilot.UpToEleven ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"Misc"  
                line += 12;

                float driverLines = 0;

                if (height < WindowHeight)
                {
                    height = WindowHeight - (entryHeight * 1.5f);
                }

                if (infoLinkEnabled)
                {
                    float infoLines = 0;
                    int infoLength = 1;
                    windowColumns = 3;

                    GUI.Label(new Rect(leftIndent + (ColumnWidth * 2), (contentTop), ColumnWidth - (leftIndent), entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_infoLink"), Title);//"infolink"
                    BeginArea(new Rect(leftIndent + (ColumnWidth * 2), contentTop + (entryHeight * 1.5f), ColumnWidth - (leftIndent), WindowHeight - (entryHeight * 1.5f) - (2 * contentTop)));
                    using (var scrollViewScope = new ScrollViewScope(scrollInfoVector, Width(ColumnWidth - (leftIndent)), Height(WindowHeight - (entryHeight * 1.5f) - (2 * contentTop))))
                    {
                        scrollInfoVector = scrollViewScope.scrollPosition;

                        GUILayout.Label(Localizer.Format("#LOC_BDArmory_DriverAI_Help"), infoLinkStyle, Width(ColumnWidth - (leftIndent * 4) - 20)); //Pid desc
                    }
                    EndArea();
                }

                scrollViewVector = GUI.BeginScrollView(new Rect(leftIndent + 100, contentTop + (entryHeight * 1.5f), (ColumnWidth * 2) - 100 - (leftIndent), WindowHeight - (entryHeight * 1.5f) - (2 * contentTop)), scrollViewVector,
                                       new Rect(0, 0, (ColumnWidth * 2) - 120 - (leftIndent * 2), height + contentTop));

                GUI.BeginGroup(new Rect(leftIndent, 0, (ColumnWidth * 2) - 120 - (leftIndent * 2), height), GUIContent.none, BDArmorySetup.BDGuiSkin.box); //darker box

                //GUI.Box(new Rect(0, 0, (ColumnWidth * 2) - leftIndent, height - contentTop), "", BDArmorySetup.BDGuiSkin.window);
                contentWidth -= 24;
                leftIndent += 3;

                driverLines += 0.2f;
                GUI.BeginGroup(
                    new Rect(0, (driverLines * entryHeight), contentWidth, pidHeight * entryHeight),
                    GUIContent.none, BDArmorySetup.BDGuiSkin.box);
                driverLines += 0.25f;

                GUI.Label(SettinglabelRect(leftIndent, driverLines), Localizer.Format("#LOC_BDArmory_PilotAI_PID"), BoldLabel);//"Pid Controller"
                driverLines++;

                Drivertype = GUI.HorizontalSlider(SettingSliderRect(leftIndent, driverLines, contentWidth),
                            Drivertype, 0, 2);
                Drivertype = Mathf.Round(Drivertype);
                if (Drivertype == 0)
                {
                    ActiveDriver.SurfaceTypeName = "Land";
                }
                else if (Drivertype == 1)
                {
                    ActiveDriver.SurfaceTypeName = "Amphibious";
                }
                else
                {
                    ActiveDriver.SurfaceTypeName = "Water";
                }
                GUI.Label(ContextLabelRect(leftIndent, driverLines), Localizer.Format("#LOC_BDArmory_VehicleType") + ActiveDriver.SurfaceTypeName, Label);//"Wobbly"

                driverLines++;
                if (contextTipsEnabled)
                {
                    GUI.Label(ContextLabelRect(leftIndent, driverLines), Localizer.Format("#LOC_BDArmory_DriverAI_VeeType"), Label);//"Wobbly"
                    driverLines++;
                }
                if (!NumFieldsEnabled)
                {
                    ActiveDriver.MaxSlopeAngle =
                        GUI.HorizontalSlider(SettingSliderRect(leftIndent, driverLines, contentWidth),
                            ActiveDriver.MaxSlopeAngle, 10, ActiveDriver.UpToEleven ? 90 : 30);
                    ActiveDriver.MaxSlopeAngle = Mathf.Round(ActiveDriver.MaxSlopeAngle);
                }
                else
                {
                    float maxslope;
                    string slopeangle = GUI.TextField(SettingSliderRect(leftIndent, driverLines, contentWidth), ActiveDriver.MaxSlopeAngle.ToString("0"));
                    if (Single.TryParse(slopeangle, out maxslope))
                    {
                        ActiveDriver.MaxSlopeAngle = maxslope;
                    }
                }
                GUI.Label(SettinglabelRect(leftIndent, driverLines), Localizer.Format("#LOC_BDArmory_MaxSlopeAngle") + " :" + ActiveDriver.MaxSlopeAngle.ToString("0"), Label);//"Steer Ki"
                driverLines++;

                if (contextTipsEnabled)
                {
                    GUI.Label(ContextLabelRect(leftIndent, driverLines), Localizer.Format("#LOC_BDArmory_DriverAI_SlopeAngle"), Label);//"undershoot"
                    driverLines++;
                }
                if (!NumFieldsEnabled)
                {
                    ActiveDriver.CruiseSpeed =
                            GUI.HorizontalSlider(SettingSliderRect(leftIndent, driverLines, contentWidth),
                                ActiveDriver.CruiseSpeed, 5, ActivePilot.UpToEleven ? 300 : 60);
                    ActiveDriver.CruiseSpeed = Mathf.Round(ActiveDriver.CruiseSpeed);
                }
                else
                {
                    float cruise;
                    string idlespd = GUI.TextField(SettingSliderRect(leftIndent, driverLines, contentWidth), ActiveDriver.CruiseSpeed.ToString("0"));
                    if (Single.TryParse(idlespd, out cruise))
                    {
                        ActiveDriver.CruiseSpeed = cruise;
                    }
                }
                GUI.Label(SettinglabelRect(leftIndent, driverLines), Localizer.Format("#LOC_BDArmory_CruiseSpeed") + " :" + ActivePilot.steerDamping.ToString("0"), Label);//"Steer Damping"

                driverLines++;
                if (contextTipsEnabled)
                {
                    GUI.Label(ContextLabelRect(leftIndent, driverLines), Localizer.Format("#LOC_BDArmory_AIWindow_idleSpeed"), Label);//"Wobbly"
                    driverLines++;
                }
                if (!NumFieldsEnabled)
                {
                    ActiveDriver.MaxSpeed =
                           GUI.HorizontalSlider(SettingSliderRect(leftIndent, driverLines, contentWidth),
                        ActiveDriver.MaxSpeed, 5, ActivePilot.UpToEleven ? 400 : 80);
                    ActiveDriver.MaxSpeed = Mathf.Round(ActiveDriver.MaxSpeed);
                }
                else
                {
                    float maxspd;
                    string fullspeed = GUI.TextField(SettingSliderRect(leftIndent, driverLines, contentWidth), ActiveDriver.MaxSpeed.ToString("0"));
                    if (Single.TryParse(fullspeed, out maxspd))
                    {
                        ActiveDriver.MaxSpeed = maxspd;
                    }
                }
                GUI.Label(SettinglabelRect(leftIndent, driverLines), Localizer.Format("#LOC_BDArmory_MaxSpeed") + " :" + ActivePilot.steerDamping.ToString("0"), Label);//"Steer Damping"

                driverLines++;
                if (contextTipsEnabled)
                {
                    GUI.Label(ContextLabelRect(leftIndent, driverLines), Localizer.Format("#LOC_BDArmory_DriverAI_MaxSpeed"), Label);//"Wobbly"
                    driverLines++;
                }
                if (!NumFieldsEnabled)
                {
                    ActiveDriver.MaxDrift =
                    GUI.HorizontalSlider(SettingSliderRect(leftIndent, driverLines, contentWidth),
                        ActiveDriver.MaxDrift, 1, 180);
                    ActiveDriver.MaxDrift = Mathf.Round(ActiveDriver.MaxDrift);
                }
                else
                {
                    float driftangle;
                    string tokyo = GUI.TextField(SettingSliderRect(leftIndent, driverLines, contentWidth), ActiveDriver.MaxDrift.ToString("0"));
                    if (Single.TryParse(tokyo, out driftangle))
                    {
                        ActiveDriver.MaxDrift = driftangle;
                    }
                }
                GUI.Label(SettinglabelRect(leftIndent, driverLines), Localizer.Format("#LOC_BDArmory_MaxDrift") + " :" + ActivePilot.steerDamping.ToString("0"), Label);//"Steer Damping"

                driverLines++;
                if (contextTipsEnabled)
                {
                    GUI.Label(ContextLabelRect(leftIndent, driverLines), Localizer.Format("#LOC_BDArmory_DriverAI_MaxDrift"), Label);//"Wobbly"
                    driverLines++;
                }
                if (!NumFieldsEnabled)
                {
                    ActiveDriver.TargetPitch =
                        GUI.HorizontalSlider(SettingSliderRect(leftIndent, driverLines, contentWidth),
                            ActiveDriver.TargetPitch, -10, 10);
                    ActiveDriver.TargetPitch = Mathf.Round(ActiveDriver.TargetPitch * 10) / 10;
                }
                else
                {
                    float tarpitch;
                    string TgtP = GUI.TextField(SettingSliderRect(leftIndent, driverLines, contentWidth), ActiveDriver.TargetPitch.ToString("0.0"));
                    if (Single.TryParse(TgtP, out tarpitch))
                    {
                        ActiveDriver.TargetPitch = tarpitch;
                    }
                }
                GUI.Label(SettinglabelRect(leftIndent, driverLines), Localizer.Format("#LOC_BDArmory_TargetPitch") + " :" + ActivePilot.steerDamping.ToString("0.0"), Label);//"Steer Damping"

                driverLines++;
                if (contextTipsEnabled)
                {
                    GUI.Label(ContextLabelRect(leftIndent, driverLines), Localizer.Format("#LOC_BDArmory_DriverAI_Pitch"), Label);//"Wobbly"
                    driverLines++;
                }
                if (!NumFieldsEnabled)
                {
                    ActiveDriver.BankAngle =
                        GUI.HorizontalSlider(SettingSliderRect(leftIndent, driverLines, contentWidth),
                            ActiveDriver.BankAngle, -45, 45);
                    ActiveDriver.BankAngle = Mathf.Round(ActiveDriver.BankAngle);
                }
                else
                {
                    float banking;
                    string VeeRoll = GUI.TextField(SettingSliderRect(leftIndent, driverLines, contentWidth), ActiveDriver.BankAngle.ToString("0"));
                    if (Single.TryParse(VeeRoll, out banking))
                    {
                        ActiveDriver.BankAngle = banking;
                    }
                }
                GUI.Label(SettinglabelRect(leftIndent, driverLines), Localizer.Format("#LOC_BDArmory_BankAngle") + " :" + ActivePilot.steerDamping.ToString("0"), Label);//"Steer Damping"

                driverLines++;
                if (contextTipsEnabled)
                {
                    GUI.Label(ContextLabelRect(leftIndent, driverLines), Localizer.Format("#LOC_BDArmory_AIWindow_bankLimit"), Label);//"Wobbly"
                    driverLines++;
                }
                if (!NumFieldsEnabled)
                {
                    ActiveDriver.steerMult =
                        GUI.HorizontalSlider(SettingSliderRect(leftIndent, driverLines, contentWidth),
                        ActiveDriver.steerMult, 0.2f, ActivePilot.UpToEleven ? 200 : 20);
                    ActiveDriver.steerMult = Mathf.Round(ActiveDriver.steerMult * 10) / 10;
                }
                else
                {
                    float steerM;
                    string drivefactor = GUI.TextField(SettingSliderRect(leftIndent, driverLines, contentWidth), ActiveDriver.steerMult.ToString("0.0"));
                    if (Single.TryParse(drivefactor, out steerM))
                    {
                        ActiveDriver.steerMult = steerM;
                    }
                }
                GUI.Label(SettinglabelRect(leftIndent, driverLines), Localizer.Format("#LOC_BDArmory_SteerFactor") + " :" + ActivePilot.steerDamping.ToString("0.0"), Label);//"Steer Damping"

                driverLines++;
                if (contextTipsEnabled)
                {
                    GUI.Label(ContextLabelRect(leftIndent, driverLines), Localizer.Format("#LOC_BDArmory_DriverAI_SteerMult"), Label);//"Wobbly"
                    driverLines++;
                }
                if (!NumFieldsEnabled)
                {
                    ActiveDriver.steerDamping =
                        GUI.HorizontalSlider(SettingSliderRect(leftIndent, driverLines, contentWidth),
                        ActiveDriver.steerDamping, 0.1f, ActivePilot.UpToEleven ? 100 : 10);
                    ActiveDriver.steerDamping = Mathf.Round(ActiveDriver.steerDamping * 10) / 10;
                }
                else
                {
                    float steerD;
                    string drivedamp = GUI.TextField(SettingSliderRect(leftIndent, driverLines, contentWidth), ActiveDriver.steerDamping.ToString("0.0"));
                    if (Single.TryParse(drivedamp, out steerD))
                    {
                        ActiveDriver.steerDamping = steerD;
                    }
                }
                GUI.Label(SettinglabelRect(leftIndent, driverLines), Localizer.Format("#LOC_BDArmory_SteerDamping") + " :" + ActivePilot.steerDamping.ToString("0.0"), Label);//"Steer Damping"

                driverLines++;
                if (contextTipsEnabled)
                {
                    GUI.Label(ContextLabelRect(leftIndent, driverLines), Localizer.Format("#LOC_BDArmory_AIWindow_DynDampMult"), Label);//"Wobbly"
                    driverLines++;
                }

                ActiveDriver.BroadsideAttack = GUI.Toggle(ToggleButtonRect(leftIndent, driverLines, contentWidth),
ActiveDriver.BroadsideAttack, Localizer.Format("#LOC_BDArmory_BroadsideAttack") + " : " + (ActiveDriver.BroadsideAttack ? Localizer.Format("#LOC_BDArmory_BroadsideAttack_enabledText") : Localizer.Format("#LOC_BDArmory_BroadsideAttack_disabledText")), ActiveDriver.BroadsideAttack ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"Dynamic pid"
                driverLines += 1.25f;
                if (contextTipsEnabled)
                {
                    GUI.Label(ContextLabelRect(leftIndent, driverLines), Localizer.Format("#LOC_BDArmory_DriverAI_AtkVector"), Label);//"dynamic damp min"
                    driverLines++;
                }
                if (!NumFieldsEnabled)
                {
                    ActiveDriver.MinEngagementRange =
                        GUI.HorizontalSlider(SettingSliderRect(leftIndent, driverLines, contentWidth),
                        ActiveDriver.MinEngagementRange, 0, ActivePilot.UpToEleven ? 20000 : 6000);
                    ActiveDriver.MinEngagementRange = Mathf.Round(ActiveDriver.MinEngagementRange / 100) * 100;
                }
                else
                {
                    float minRange;
                    string engageMin = GUI.TextField(SettingSliderRect(leftIndent, driverLines, contentWidth), ActiveDriver.MinEngagementRange.ToString("0"));
                    if (Single.TryParse(engageMin, out minRange))
                    {
                        ActiveDriver.MinEngagementRange = minRange;
                    }
                }
                GUI.Label(SettinglabelRect(leftIndent, driverLines), Localizer.Format("#LOC_BDArmory_EngageRangeMin") + " :" + ActivePilot.steerDamping.ToString("0"), Label);//"Steer Damping"

                driverLines++;
                if (contextTipsEnabled)
                {
                    GUI.Label(ContextLabelRect(leftIndent, driverLines), Localizer.Format("#LOC_BDArmory_DriverAI_MinEngage"), Label);//"Wobbly"
                    driverLines++;
                }
                if (!NumFieldsEnabled)
                {
                    ActiveDriver.MaxEngagementRange =
                        GUI.HorizontalSlider(SettingSliderRect(leftIndent, driverLines, contentWidth),
                        ActiveDriver.MaxEngagementRange, 500, ActivePilot.UpToEleven ? 20000 : 8000);
                    ActiveDriver.MaxEngagementRange = Mathf.Round(ActiveDriver.MaxEngagementRange / 100) * 100;
                }
                else
                {
                    float maxRange;
                    string engageMax = GUI.TextField(SettingSliderRect(leftIndent, driverLines, contentWidth), ActiveDriver.MaxEngagementRange.ToString("0"));
                    if (Single.TryParse(engageMax, out maxRange))
                    {
                        ActiveDriver.MaxEngagementRange = maxRange;
                    }
                }
                    GUI.Label(SettinglabelRect(leftIndent, driverLines), Localizer.Format("#LOC_BDArmory_EngageRangeMax") + " :" + ActivePilot.steerDamping.ToString("0"), Label);//"Steer Damping"

                driverLines++;
                if (contextTipsEnabled)
                {
                    GUI.Label(ContextLabelRect(leftIndent, driverLines), Localizer.Format("#LOC_BDArmory_DriverAI_MaxEngage"), Label);//"Wobbly"
                    driverLines++;
                }

                ActiveDriver.ManeuverRCS = GUI.Toggle(ToggleButtonRect(leftIndent, driverLines, contentWidth),
ActiveDriver.ManeuverRCS, Localizer.Format("#LOC_BDArmory_ManeuverRCS") + " : " + (ActiveDriver.ManeuverRCS ? Localizer.Format("#LOC_BDArmory_ManeuverRCS_enabledText") : Localizer.Format("#LOC_BDArmory_ManeuverRCS_disabledText")), ActiveDriver.BroadsideAttack ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"Dynamic pid"
                driverLines += 1.25f;
                if (contextTipsEnabled)
                {
                    GUI.Label(new Rect(150 + leftIndent, driverLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_DriverAI_RCS"), Label);//"dynamic damp min"
                    driverLines++;
                }
                if (!NumFieldsEnabled)
                {
                    ActiveDriver.AvoidMass =
                        GUI.HorizontalSlider(SettingSliderRect(leftIndent, driverLines, contentWidth),
                        ActiveDriver.AvoidMass, 0, ActivePilot.UpToEleven ? 1000000f : 100);
                    ActiveDriver.AvoidMass = Mathf.Round(ActiveDriver.AvoidMass);
                }
                else
                {
                    float Objmass;
                    string obstacle = GUI.TextField(SettingSliderRect(leftIndent, driverLines, contentWidth), ActiveDriver.AvoidMass.ToString("0"));
                    if (Single.TryParse(obstacle, out Objmass))
                    {
                        ActiveDriver.AvoidMass = Objmass;
                    }
                }
                GUI.Label(SettinglabelRect(leftIndent, driverLines), Localizer.Format("#LOC_BDArmory_MinObstacleMass") + " :" + ActivePilot.steerDamping.ToString("0"), Label);//"Steer Damping"

                driverLines++;
                if (contextTipsEnabled)
                {
                    GUI.Label(ContextLabelRect(leftIndent, driverLines), Localizer.Format("#LOC_BDArmory_DriverAI_Mass"), Label);//"Wobbly"
                    driverLines++;
                }

                broadsideDir = GUI.HorizontalSlider(SettingSliderRect(leftIndent, driverLines, contentWidth),Drivertype, 0, 2);
                broadsideDir = Mathf.Round(broadsideDir);
                if (broadsideDir == 0)
                {
                    ActiveDriver.OrbitDirectionName = "Starboard";
                }
                else if (broadsideDir == 1)
                {
                    ActiveDriver.OrbitDirectionName = "Whatever";
                }
                else
                {
                    ActiveDriver.OrbitDirectionName = "Port";
                }
                GUI.Label(ContextLabelRect(leftIndent, driverLines), Localizer.Format("#LOC_BDArmory_PreferredBroadsideDirection") + ActiveDriver.OrbitDirectionName, Label);//"Wobbly"

                driverLines++;
                if (contextTipsEnabled)
                {
                    GUI.Label(ContextLabelRect(leftIndent, driverLines), Localizer.Format("#LOC_BDArmory_DriverAI_BroadsideDir"), Label);//"Wobbly"
                    driverLines++;
                }


                GUI.EndGroup();

                height = Mathf.Max(Mathf.Lerp(height, (driverLines) * entryHeight + contentTop + 5, 1), (line * entryHeight) + contentTop);
                GUI.EndGroup();
                GUI.EndScrollView();
            }
            else
            {
                GUI.Label(new Rect(leftIndent, contentTop + (1.75f * entryHeight), contentWidth, entryHeight),
                   Localizer.Format("#LOC_BDArmory_AIWindow_NoAI"), Title);// "No AI found."
            }     


            WindowHeight = contentTop + (line * entryHeight) + 5;
            WindowWidth = Mathf.Lerp(WindowWidth, windowColumns * ColumnWidth, 0.15f);
            var previousWindowHeight = BDArmorySetup.WindowRectAI.height;
            BDArmorySetup.WindowRectAI.height = WindowHeight;
            BDArmorySetup.WindowRectAI.width = WindowWidth;
            if (BDArmorySettings.STRICT_WINDOW_BOUNDARIES && WindowHeight < previousWindowHeight && Mathf.Round(BDArmorySetup.WindowRectAI.y + previousWindowHeight) == Screen.height) // Window shrunk while being at edge of screen.
                BDArmorySetup.WindowRectAI.y = Screen.height - BDArmorySetup.WindowRectAI.height;
            BDGUIUtils.RepositionWindow(ref BDArmorySetup.WindowRectAI);
        }
        #endregion GUI

        internal void OnDestroy()
        {
            GameEvents.onVesselChange.Remove(VesselChange);
            GameEvents.onEditorPartPlaced.Remove(OnEditorPartPlacedEvent); 
            GameEvents.onEditorPartDeleted.Remove(OnEditorPartDeletedEvent);
        }
    }
}
