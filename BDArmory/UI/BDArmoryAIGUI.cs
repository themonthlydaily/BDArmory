using System.Collections;
using BDArmory.Core;
using BDArmory.Modules;
using UnityEngine;
using KSP.Localization;
using KSP.UI.Screens;

namespace BDArmory.UI
{
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    public class BDArmoryAIGUI : MonoBehaviour
    {       
        //toolbar gui
        public static bool infoLinkEnabled = false;
        public static bool contextTipsEnabled = false;
        public static bool windowBDAAIGUIEnabled;
        float WindowWidth = 500;
        float WindowHeight = 250;
        float height = 0;
        float infoHeight = 0;
        float margin = 5;
        float buttonHeight = 20;
        float buttonGap = 2;
        float ColumnWidth = 350;
        bool showPID;
        bool showAltitude;
        bool showSpeed;
        bool showControl;
        bool showEvade;
        bool showTerrain;
        bool showRam;
        bool showMisc;


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

            infoLinkStyle = BDArmorySetup.BDGuiSkin.label;
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

        void WindowRectAI(int windowID)
        {
            float line = 0;
            float leftIndent = 10;
            float contentTop = 10;
            float entryHeight = 20;
            float _buttonSize = 26;
            float _windowMargin = 4;
            float windowColumns = 2;
            float contentWidth = ((ColumnWidth * 2) - 100 - 20);

            GUI.DragWindow(new Rect(_windowMargin +_buttonSize * 6, 0, (ColumnWidth * 2) - (2 * _windowMargin) - (9 * _buttonSize), _windowMargin + _buttonSize));

            GUI.Label(new Rect(100, contentTop, contentWidth, entryHeight),
               Localizer.Format("#LOC_BDArmory_AIWindow_title"), Title);// "No AI found."

            line += 1.25f;
            line += 0.25f;

            //Exit Button
            GUIStyle buttonStyle = windowBDAAIGUIEnabled ? BDArmorySetup.BDGuiSkin.button : BDArmorySetup.BDGuiSkin.box;
            if (GUI.Button(new Rect((ColumnWidth*2) - _windowMargin - _buttonSize, _windowMargin, _buttonSize, _buttonSize), "X", buttonStyle))
            {
                windowBDAAIGUIEnabled = !windowBDAAIGUIEnabled;
            }

            //Infolink button
            buttonStyle = infoLinkEnabled ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button; 
            if (GUI.Button(new Rect((ColumnWidth * 2) - _windowMargin - 2 * _buttonSize, _windowMargin, _buttonSize, _buttonSize), "i", buttonStyle))
            {
                infoLinkEnabled = !infoLinkEnabled;
            }

            //Context labels button
            buttonStyle = contextTipsEnabled ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button;
            if (GUI.Button(new Rect((ColumnWidth * 2) - _windowMargin - 3 * _buttonSize, _windowMargin, _buttonSize, _buttonSize), "?", buttonStyle))
            {
                contextTipsEnabled = !contextTipsEnabled;
            }

            GUIStyle saveStyle = BDArmorySetup.BDGuiSkin.button;
            if (GUI.Button(new Rect(_windowMargin, _windowMargin, _buttonSize*3, _buttonSize), "Save", saveStyle))
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

            if (ActivePilot != null)
            {
                showPID = GUI.Toggle(new Rect(leftIndent, contentTop + (line * entryHeight), 100, entryHeight),
                            showPID, Localizer.Format("#LOC_BDArmory_PilotAI_PID"), showPID ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"PiD"                    
                line += 1.2f;

                showAltitude = GUI.Toggle(new Rect(leftIndent, contentTop + (line * entryHeight), 100, entryHeight),
                    showAltitude, Localizer.Format("#LOC_BDArmory_PilotAI_Altitudes"), showAltitude ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"Altitude"        
                line += 1.2f;

                showSpeed = GUI.Toggle(new Rect(leftIndent, contentTop + (line * entryHeight), 100, entryHeight),
                    showSpeed, Localizer.Format("#LOC_BDArmory_PilotAI_Speeds"), showSpeed ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"Speed"                    
                line += 1.2f;

                showControl = GUI.Toggle(new Rect(leftIndent, contentTop + (line * entryHeight), 100, entryHeight),
                    showControl, Localizer.Format("#LOC_BDArmory_AIWindow_ControlLimits"), showControl ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"Control"  
                line += 1.2f;

                showEvade = GUI.Toggle(new Rect(leftIndent, contentTop + (line * entryHeight), 100, entryHeight),
                    showEvade, Localizer.Format("#LOC_BDArmory_AIWindow_EvadeExtend"), showEvade ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"Evasion"                    
                line += 1.2f;

                showTerrain = GUI.Toggle(new Rect(leftIndent, contentTop + (line * entryHeight), 100, entryHeight),
                    showTerrain, Localizer.Format("#LOC_BDArmory_AIWindow_Terrain"), showTerrain ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"Terrain"  
                line += 1.2f;

                showRam = GUI.Toggle(new Rect(leftIndent, contentTop + (line * entryHeight), 100, entryHeight),
                    showRam, Localizer.Format("#LOC_BDArmory_PilotAI_Ramming"), showRam ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"rammin"                    
                line += 1.2f;

                showMisc = GUI.Toggle(new Rect(leftIndent, contentTop + (line * entryHeight), 100, entryHeight),
                    showMisc, Localizer.Format("#LOC_BDArmory_PilotAI_Misc"), showMisc ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"Misc"  
                line += 1.2f;

                ActivePilot.UpToEleven = GUI.Toggle(new Rect(leftIndent, contentTop + (line * entryHeight), 100, entryHeight),
                    ActivePilot.UpToEleven, ActivePilot.UpToEleven ? Localizer.Format("#LOC_BDArmory_UnclampTuning_enabledText") : Localizer.Format("#LOC_BDArmory_UnclampTuning_disabledText"), ActivePilot.UpToEleven ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"Misc"  
                line += 1.2f;

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

                    GUI.Label(new Rect(leftIndent, (contentTop), ColumnWidth - (leftIndent * 4) - 20, entryHeight), Localizer.Format("#LOC_BDArmory_PilotAI_PID"), Title);//"infolink"


                    scrollInfoVector = GUI.BeginScrollView(new Rect(leftIndent + (ColumnWidth*2), contentTop + (entryHeight * 1.5f), ColumnWidth - (leftIndent), WindowHeight - (entryHeight * 1.5f) - (2 * contentTop)), scrollInfoVector,
                                        new Rect(0, 0, ColumnWidth - (leftIndent*2) - 20, infoHeight + contentTop));

                    if (showPID)
                    {
                        infoLines += 0.5f;
                        GUI.Label(new Rect(leftIndent, (infoLines * entryHeight), ColumnWidth - (leftIndent * 4) - 20, entryHeight), Localizer.Format("#LOC_BDArmory_PilotAI_PID"), BoldLabel);//"Pid Controller"
                        infoLines+= 1.25f;

                        infoLength = Mathf.CeilToInt((Localizer.Format("#LOC_BDArmory_AIWindow_PidHelp").Length / 50));
                        GUI.Label(new Rect(leftIndent, infoLines * entryHeight, ColumnWidth - (leftIndent * 4) - 20, entryHeight * infoLength),
                           Localizer.Format("#LOC_BDArmory_AIWindow_PidHelp"), infoLinkStyle);// "No AI found."
                        infoLines += infoLength;

                        infoLength = Mathf.CeilToInt((Localizer.Format("#LOC_BDArmory_AIWindow_PidHelp_SteerMult").Length / 50));
                        GUI.Label(new Rect(leftIndent, infoLines * entryHeight, ColumnWidth - (leftIndent * 4) - 20, entryHeight * infoLength),
                           Localizer.Format("#LOC_BDArmory_AIWindow_PidHelp_SteerMult"), infoLinkStyle);// "No AI found."
                        infoLines += infoLength;

                        infoLength = Mathf.CeilToInt((Localizer.Format("#LOC_BDArmory_AIWindow_PidHelp_SteerKi").Length / 50));
                        GUI.Label(new Rect(leftIndent, infoLines * entryHeight, ColumnWidth - (leftIndent * 4) - 20, entryHeight * infoLength),
                           Localizer.Format("#LOC_BDArmory_AIWindow_PidHelp_SteerKi"), infoLinkStyle);// "No AI found."
                        infoLines += infoLength;

                        infoLength = Mathf.CeilToInt((Localizer.Format("#LOC_BDArmory_AIWindow_PidHelp_Steerdamp").Length / 50));
                        GUI.Label(new Rect(leftIndent, infoLines * entryHeight, ColumnWidth - (leftIndent * 4) - 20, entryHeight * infoLength),
                           Localizer.Format("#LOC_BDArmory_AIWindow_PidHelp_Steerdamp"), infoLinkStyle);// "No AI found."
                        infoLines += infoLength;

                        infoLength = Mathf.CeilToInt((Localizer.Format("#LOC_BDArmory_AIWindow_PidHelp_Dyndamp").Length / 50));
                        GUI.Label(new Rect(leftIndent, infoLines * entryHeight, ColumnWidth - (leftIndent * 4) - 20, entryHeight * infoLength),
                           Localizer.Format("#LOC_BDArmory_AIWindow_PidHelp_Dyndamp"), infoLinkStyle);// "No AI found."
                        infoLines += infoLength;
                    }
                    if (showAltitude)
                    {
                        infoLines += 0.5f;
                        GUI.Label(new Rect(leftIndent, (infoLines * entryHeight), ColumnWidth - (leftIndent * 4) - 20, entryHeight), Localizer.Format("#LOC_BDArmory_PilotAI_Altitudes"), BoldLabel);//"Pid Controller"
                        infoLines += 1.25f;

                        infoLength = Mathf.CeilToInt((Localizer.Format("#LOC_BDArmory_AIWindow_AltHelp").Length / 50));
                        GUI.Label(new Rect(leftIndent, infoLines * entryHeight, ColumnWidth - (leftIndent * 4) - 20, entryHeight * infoLength),
                           Localizer.Format("#LOC_BDArmory_AIWindow_AltHelp"), infoLinkStyle);// "No AI found."
                        infoLines += infoLength;

                        infoLength = Mathf.CeilToInt((Localizer.Format("#LOC_BDArmory_AIWindow_AltHelp_Def").Length / 50));
                        GUI.Label(new Rect(leftIndent, infoLines * entryHeight, ColumnWidth - (leftIndent * 4) - 20, entryHeight * infoLength),
                           Localizer.Format("#LOC_BDArmory_AIWindow_AltHelp_Def"), infoLinkStyle);// "No AI found."
                        infoLines += infoLength;

                        infoLength = Mathf.CeilToInt((Localizer.Format("#LOC_BDArmory_AIWindow_AltHelp_min").Length / 50));
                        GUI.Label(new Rect(leftIndent, infoLines * entryHeight, ColumnWidth - (leftIndent * 4) - 20, entryHeight * infoLength),
                           Localizer.Format("#LOC_BDArmory_AIWindow_AltHelp_min"), infoLinkStyle);// "No AI found."
                        infoLines += infoLength;

                        infoLength = Mathf.CeilToInt((Localizer.Format("#LOC_BDArmory_AIWindow_AltHelp_max").Length / 50));
                        GUI.Label(new Rect(leftIndent, infoLines * entryHeight, ColumnWidth - (leftIndent * 4) - 20, entryHeight * infoLength),
                           Localizer.Format("#LOC_BDArmory_AIWindow_AltHelp_max"), infoLinkStyle);// "No AI found."
                        infoLines += infoLength;
                    }
                    if (showSpeed)
                    {
                        infoLines += 0.5f;
                        GUI.Label(new Rect(leftIndent, (infoLines * entryHeight), ColumnWidth - (leftIndent * 4) - 20, entryHeight), Localizer.Format("#LOC_BDArmory_PilotAI_Speeds"), BoldLabel);//"Pid Controller"
                        infoLines += 1.25f;

                        infoLength = Mathf.CeilToInt((Localizer.Format("#LOC_BDArmory_AIWindow_SpeedHelp").Length / 50));
                        GUI.Label(new Rect(leftIndent, infoLines * entryHeight, ColumnWidth - (leftIndent * 4) - 20, entryHeight * infoLength),
                           Localizer.Format("#LOC_BDArmory_AIWindow_SpeedHelp"), infoLinkStyle);// "No AI found."
                        infoLines += infoLength;

                        infoLength = Mathf.CeilToInt((Localizer.Format("#LOC_BDArmory_AIWindow_SpeedHelp_min").Length / 50));
                        GUI.Label(new Rect(leftIndent, infoLines * entryHeight, ColumnWidth - (leftIndent * 4) - 20, entryHeight * infoLength),
                           Localizer.Format("#LOC_BDArmory_AIWindow_SpeedHelp_min"), infoLinkStyle);// "No AI found."
                        infoLines += infoLength;

                        infoLength = Mathf.CeilToInt((Localizer.Format("#LOC_BDArmory_AIWindow_SpeedHelp_takeoff").Length / 50));
                        GUI.Label(new Rect(leftIndent, infoLines * entryHeight, ColumnWidth - (leftIndent * 4) - 20, entryHeight * infoLength),
                           Localizer.Format("#LOC_BDArmory_AIWindow_SpeedHelp_takeoff"), infoLinkStyle);// "No AI found."
                        infoLines += infoLength;

                        infoLength = Mathf.CeilToInt((Localizer.Format("#LOC_BDArmory_AIWindow_SpeedHelp_idle").Length / 50));
                        GUI.Label(new Rect(leftIndent, infoLines * entryHeight, ColumnWidth - (leftIndent * 4) - 20, entryHeight * infoLength),
                           Localizer.Format("#LOC_BDArmory_AIWindow_SpeedHelp_idle"), infoLinkStyle);// "No AI found."
                        infoLines += infoLength;

                        infoLength = Mathf.CeilToInt((Localizer.Format("#LOC_BDArmory_AIWindow_SpeedHelp_gnd").Length / 50));
                        GUI.Label(new Rect(leftIndent, infoLines * entryHeight, ColumnWidth - (leftIndent * 4) - 20, entryHeight * infoLength),
                           Localizer.Format("#LOC_BDArmory_AIWindow_SpeedHelp_gnd"), infoLinkStyle);// "No AI found."
                        infoLines += infoLength;
                    }
                    if (showControl)
                    {
                        infoLines += 0.5f;
                        GUI.Label(new Rect(leftIndent, (infoLines * entryHeight), ColumnWidth - (leftIndent * 4) - 20, entryHeight), Localizer.Format("#LOC_BDArmory_PilotAI_ControlLimits"), BoldLabel);//"Pid Controller"
                        infoLines += 1.25f;

                        infoLength = Mathf.CeilToInt((Localizer.Format("#LOC_BDArmory_AIWindow_ControlHelp").Length / 50));
                        GUI.Label(new Rect(leftIndent, infoLines * entryHeight, ColumnWidth - (leftIndent * 4) - 20, entryHeight * infoLength),
                           Localizer.Format("#LOC_BDArmory_AIWindow_ControlHelp"), infoLinkStyle);// "No AI found."
                        infoLines += infoLength;

                        infoLength = Mathf.CeilToInt((Localizer.Format("#LOC_BDArmory_AIWindow_ControlHelp_limiters").Length / 50));
                        GUI.Label(new Rect(leftIndent, infoLines * entryHeight, ColumnWidth - (leftIndent * 4) - 20, entryHeight * infoLength),
                           Localizer.Format("#LOC_BDArmory_AIWindow_ControlHelp_limiters"), infoLinkStyle);// "No AI found."
                        infoLines += infoLength;

                        infoLength = Mathf.CeilToInt((Localizer.Format("#LOC_BDArmory_AIWindow_ControlHelp_clamps").Length / 50));
                        GUI.Label(new Rect(leftIndent, infoLines * entryHeight, ColumnWidth - (leftIndent * 4) - 20, entryHeight * infoLength),
                           Localizer.Format("#LOC_BDArmory_AIWindow_ControlHelp_clamps"), infoLinkStyle);// "No AI found."
                        infoLines += infoLength;
                    }
                    if (showEvade)
                    {
                        infoLines += 0.5f;
                        GUI.Label(new Rect(leftIndent, (infoLines * entryHeight), ColumnWidth - (leftIndent * 4) - 20, entryHeight), Localizer.Format("#LOC_BDArmory_PilotAI_EvadeExtend"), BoldLabel);//"Pid Controller"
                        infoLines += 1.25f;

                        infoLength = Mathf.CeilToInt((Localizer.Format("#LOC_BDArmory_AIWindow_EvadeHelp").Length / 50));
                        GUI.Label(new Rect(leftIndent, infoLines * entryHeight, ColumnWidth - (leftIndent * 4) - 20, entryHeight * infoLength),
                           Localizer.Format("#LOC_BDArmory_AIWindow_EvadeHelp"), infoLinkStyle);// "No AI found."
                        infoLines += infoLength;

                        infoLength = Mathf.CeilToInt((Localizer.Format("#LOC_BDArmory_AIWindow_EvadeHelp_Evade").Length / 50));
                        GUI.Label(new Rect(leftIndent, infoLines * entryHeight, ColumnWidth - (leftIndent * 4) - 20, entryHeight * infoLength),
                           Localizer.Format("#LOC_BDArmory_AIWindow_EvadeHelp_Evade"), infoLinkStyle);// "No AI found."
                        infoLines += infoLength;

                        infoLength = Mathf.CeilToInt((Localizer.Format("#LOC_BDArmory_AIWindow_EvadeHelp_Dodge").Length / 50));
                        GUI.Label(new Rect(leftIndent, infoLines * entryHeight, ColumnWidth - (leftIndent * 4) - 20, entryHeight * infoLength),
                           Localizer.Format("#LOC_BDArmory_AIWindow_EvadeHelp_Dodge"), infoLinkStyle);// "No AI found."
                        infoLines += infoLength;

                        infoLength = Mathf.CeilToInt((Localizer.Format("#LOC_BDArmory_AIWindow_EvadeHelp_standoff").Length / 50));
                        GUI.Label(new Rect(leftIndent, infoLines * entryHeight, ColumnWidth - (leftIndent * 4) - 20, entryHeight * infoLength),
                           Localizer.Format("#LOC_BDArmory_AIWindow_EvadeHelp_standoff"), infoLinkStyle);// "No AI found."
                        infoLines += infoLength;

                        infoLength = Mathf.CeilToInt((Localizer.Format("#LOC_BDArmory_AIWindow_EvadeHelp_Extend").Length / 50));
                        GUI.Label(new Rect(leftIndent, infoLines * entryHeight, ColumnWidth - (leftIndent * 4) - 20, entryHeight * infoLength),
                           Localizer.Format("#LOC_BDArmory_AIWindow_EvadeHelp_Extend"), infoLinkStyle);// "No AI found."
                        infoLines += infoLength;
                    }
                    if (showTerrain)
                    {
                        infoLines += 0.5f;
                        GUI.Label(new Rect(leftIndent, (infoLines * entryHeight), ColumnWidth - (leftIndent * 4) - 20, entryHeight), Localizer.Format("#LOC_BDArmory_PilotAI_Terrain"), BoldLabel);//"Pid Controller"
                        infoLines += 1.25f;

                        infoLength = Mathf.CeilToInt((Localizer.Format("#LOC_BDArmory_AIWindow_TerrainHelp").Length / 50));
                        GUI.Label(new Rect(leftIndent, infoLines * entryHeight, ColumnWidth - (leftIndent * 4) - 20, entryHeight * infoLength),
                           Localizer.Format("#LOC_BDArmory_AIWindow_TerrainHelp"), infoLinkStyle);// "No AI found."
                        infoLines += infoLength;
                    }
                    if (showRam)
                    {
                        infoLines += 0.5f;
                        GUI.Label(new Rect(leftIndent, (infoLines * entryHeight), ColumnWidth - (leftIndent * 4) - 20, entryHeight), Localizer.Format("#LOC_BDArmory_PilotAI_Ramming"), BoldLabel);//"Pid Controller"
                        infoLines += 1.25f;

                        infoLength = Mathf.CeilToInt((Localizer.Format("#LOC_BDArmory_AIWindow_RamHelp").Length / 50));
                        GUI.Label(new Rect(leftIndent, infoLines * entryHeight, ColumnWidth - (leftIndent * 4) - 20, entryHeight * infoLength),
                           Localizer.Format("#LOC_BDArmory_AIWindow_RamHelp"), infoLinkStyle);// "No AI found."
                        infoLines += infoLength;
                    }
                    if (showMisc)
                    {
                        infoLines += 0.5f;
                        GUI.Label(new Rect(leftIndent, (infoLines * entryHeight), ColumnWidth - (leftIndent * 4) - 20, entryHeight), Localizer.Format("#LOC_BDArmory_PilotAI_Misc"), BoldLabel);//"Pid Controller"
                        infoLines += 1.25f;

                        infoLength = Mathf.CeilToInt((Localizer.Format("#LOC_BDArmory_AIWindow_miscHelp").Length / 50));
                        GUI.Label(new Rect(leftIndent, infoLines * entryHeight, ColumnWidth - (leftIndent * 4) - 20, entryHeight * infoLength),
                           Localizer.Format("#LOC_BDArmory_AIWindow_miscHelp"), infoLinkStyle);// "No AI found."
                        infoLines += infoLength;
                    }
                    infoHeight = Mathf.Max(Mathf.Lerp(infoHeight, infoLines * entryHeight + contentTop + 5, 1), (line * entryHeight) + contentTop);
                    GUI.EndScrollView();                    
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

                    GUI.Label(new Rect(leftIndent, (pidLines * entryHeight), 85, entryHeight), Localizer.Format("#LOC_BDArmory_PilotAI_PID"), BoldLabel);//"Pid Controller"
                    pidLines++;
                  
                    ActivePilot.steerMult =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + 150, (pidLines * entryHeight), contentWidth - (leftIndent*2) - (150 + 10) , entryHeight),
                            ActivePilot.steerMult, 0.1f, ActivePilot.UpToEleven ? 200 : 20);
                    ActivePilot.steerMult = Mathf.Round(ActivePilot.steerMult * 10f) / 10f;
                    GUI.Label(new Rect(leftIndent, (pidLines * entryHeight), 85, entryHeight), Localizer.Format("#LOC_BDArmory_SteerFactor") + " :" + ActivePilot.steerMult.ToString("0.0"), Label);//"Steer Mult"

                    pidLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(new Rect(150 + leftIndent, (pidLines * entryHeight), 85, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_SteerMultLow"), Label);//"Steer Mult"
                        GUI.Label(new Rect(150 + leftIndent + (contentWidth - leftIndent - 150 - 85 - 20), (pidLines * entryHeight), 85, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_SteerMultHi"), rightLabel);//"Steer Mult"
                        pidLines++;
                    }                   

                    ActivePilot.steerKiAdjust =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + 150, (pidLines * entryHeight), contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                            ActivePilot.steerKiAdjust, 0.01f, ActivePilot.UpToEleven ? 20 : 1);
                    ActivePilot.steerKiAdjust = Mathf.Round(ActivePilot.steerKiAdjust * 100f) / 100f;
                    GUI.Label(new Rect(leftIndent, (pidLines * entryHeight), 85, entryHeight), Localizer.Format("#LOC_BDArmory_SteerKi") + " :" + ActivePilot.steerKiAdjust.ToString("0.00"), Label);//"Steer Mult"
                    pidLines++;

                    if (contextTipsEnabled)
                    {
                        GUI.Label(new Rect(150 + leftIndent, (pidLines * entryHeight), 85, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_SteerKiLow"), Label);//"Steer Mult"
                        GUI.Label(new Rect(150 + leftIndent + (contentWidth - leftIndent - 150 - 85 - 20), (pidLines * entryHeight), 85, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_SteerKiHi"), rightLabel);//"Steer Mult"
                        pidLines++;
                    }
                   
                    ActivePilot.steerDamping =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + 150, (pidLines * entryHeight), contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                            ActivePilot.steerDamping, 0.01f, ActivePilot.UpToEleven ? 100 : 8);
                    ActivePilot.steerDamping = Mathf.Round(ActivePilot.steerDamping * 100f) / 100f;
                    GUI.Label(new Rect(leftIndent, (pidLines * entryHeight), 85, entryHeight), Localizer.Format("#LOC_BDArmory_SteerDamping") + " :" + ActivePilot.steerDamping.ToString("0.00"), Label);//"Steer Mult"

                    pidLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(new Rect(150 + leftIndent, (pidLines * entryHeight), 85, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_SteerDampLow"), Label);//"Steer Mult"
                        GUI.Label(new Rect(150 + leftIndent + (contentWidth - leftIndent - 150 - 85 - 20), (pidLines * entryHeight), 85, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_SteerDampHi"), rightLabel);//"Steer Mult"
                        pidLines++;
                    }

                    ActivePilot.dynamicSteerDamping =
                       GUI.Toggle(new Rect(leftIndent, (pidLines * entryHeight), contentWidth - (2 * leftIndent), entryHeight),
                           ActivePilot.dynamicSteerDamping, Localizer.Format("#LOC_BDArmory_DynamicDamping"), ActivePilot.dynamicSteerDamping ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"Dynamic pid"
                    pidLines += 1.25f;

                    if (ActivePilot.dynamicSteerDamping)
                    {
                        float dynPidLines = 0;
                        ActivePilot.CustomDynamicAxisFields = GUI.Toggle(new Rect(leftIndent, (pidLines * entryHeight), contentWidth - (2 * leftIndent), entryHeight),
                        ActivePilot.CustomDynamicAxisFields, Localizer.Format("#LOC_BDArmory_3AxisDynamicSteerDamping"), ActivePilot.CustomDynamicAxisFields ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"Dynamic pid"
                        dynPidLines++;
                        if (!ActivePilot.CustomDynamicAxisFields)
                        {
                            dynPidLines += 0.25f;

                            GUI.Label(new Rect(leftIndent, ((pidLines + dynPidLines) * entryHeight), 85, entryHeight), Localizer.Format("#LOC_BDArmory_DynamicDamping"), BoldLabel);//"Dynamic Damping"
                            dynPidLines++;

                            ActivePilot.DynamicDampingMin =
                                GUI.HorizontalSlider(
                                    new Rect(leftIndent + 150, ((pidLines + dynPidLines) * entryHeight), contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                                    ActivePilot.DynamicDampingMin, 1f, ActivePilot.UpToEleven ? 100 : 8);
                            ActivePilot.DynamicDampingMin = Mathf.Round(ActivePilot.DynamicDampingMin * 10f) / 10f;
                            GUI.Label(new Rect(leftIndent, ((pidLines + dynPidLines) * entryHeight), 85, entryHeight), Localizer.Format("#LOC_BDArmory_DynamicDampingMin") + " :" + ActivePilot.DynamicDampingMin.ToString("0.0"), Label);//"dynamic damping min"
                            dynPidLines++;
                            if (contextTipsEnabled)
                            {
                                GUI.Label(new Rect(150 + leftIndent, ((pidLines + dynPidLines) * entryHeight), 85, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_DynDampMin"), Label);//"dynamic damp min"
                                dynPidLines++;
                            }

                            ActivePilot.DynamicDampingMax =
                                GUI.HorizontalSlider(
                                    new Rect(leftIndent + 150, ((pidLines + dynPidLines) * entryHeight), contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                                    ActivePilot.DynamicDampingMax, 1f, ActivePilot.UpToEleven ? 100 : 8);
                            ActivePilot.DynamicDampingMax = Mathf.Round(ActivePilot.DynamicDampingMax * 10f) / 10f;
                            GUI.Label(new Rect(leftIndent, ((pidLines + dynPidLines) * entryHeight), 85, entryHeight), Localizer.Format("#LOC_BDArmory_DynamicDampingMax") + " :" + ActivePilot.DynamicDampingMax.ToString("0.0"), Label);//"dynamic damping max"

                            dynPidLines++;
                            if (contextTipsEnabled)
                            {
                                GUI.Label(new Rect(150 + leftIndent, ((pidLines + dynPidLines) * entryHeight), 85, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_DynDampMax"), Label);//"dynamic damp max"
                                dynPidLines++;
                            }

                            ActivePilot.dynamicSteerDampingFactor =
                                GUI.HorizontalSlider(
                                    new Rect(leftIndent + 150, ((pidLines + dynPidLines) * entryHeight), contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                                    ActivePilot.dynamicSteerDampingFactor, 0.1f, ActivePilot.UpToEleven ? 100 : 10);
                            ActivePilot.dynamicSteerDampingFactor = Mathf.Round(ActivePilot.dynamicSteerDampingFactor * 10f) / 10f;
                            GUI.Label(new Rect(leftIndent, ((pidLines + dynPidLines) * entryHeight), 85, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_DynDampMult") + " :" + ActivePilot.dynamicSteerDampingFactor.ToString("0.0"), Label);//"dynamic damping mult"

                            dynPidLines++;
                            if (contextTipsEnabled)
                            {
                                GUI.Label(new Rect(150 + leftIndent, ((pidLines + dynPidLines) * entryHeight), 85, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_DynDampMult"), Label);//"dynamic damp mult"
                                dynPidLines++;
                            }
                        }
                        else
                        {
                            ActivePilot.dynamicDampingPitch = GUI.Toggle(new Rect(leftIndent, ((pidLines + dynPidLines) * entryHeight), contentWidth - (2 * leftIndent), entryHeight),
                            ActivePilot.dynamicDampingPitch, Localizer.Format("#LOC_BDArmory_DynamicDampingPitch"), ActivePilot.dynamicDampingPitch ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"Dynamic pid"
                            dynPidLines += 1.25f;

                            if (ActivePilot.dynamicDampingPitch)
                            {                                
                                ActivePilot.DynamicDampingPitchMin =
                                    GUI.HorizontalSlider(
                                        new Rect(leftIndent + 150, ((pidLines + dynPidLines) * entryHeight), contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                                        ActivePilot.DynamicDampingPitchMin, 1f, ActivePilot.UpToEleven ? 100 : 8);
                                ActivePilot.DynamicDampingPitchMin = Mathf.Round(ActivePilot.DynamicDampingPitchMin * 10f) / 10f;
                                GUI.Label(new Rect(leftIndent, ((pidLines + dynPidLines) * entryHeight), 85, entryHeight), Localizer.Format("#LOC_BDArmory_DynamicDampingPitchMin") + " :" + ActivePilot.DynamicDampingPitchMin.ToString("0.0"), Label);//"dynamic damping min"
                                dynPidLines++;
                                if (contextTipsEnabled)
                                {
                                    GUI.Label(new Rect(150, ((pidLines + dynPidLines) * entryHeight), contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_DynDampMin"), contextLabel);//"Steer Mult"
                                    dynPidLines++;
                                }

                                ActivePilot.DynamicDampingPitchMax =
                                    GUI.HorizontalSlider(
                                        new Rect(leftIndent + 150, ((pidLines + dynPidLines) * entryHeight), contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                                        ActivePilot.DynamicDampingPitchMax, 1f, ActivePilot.UpToEleven ? 100 : 8);
                                ActivePilot.DynamicDampingPitchMax = Mathf.Round(ActivePilot.DynamicDampingPitchMax * 10f) / 10f;
                                GUI.Label(new Rect(leftIndent, ((pidLines + dynPidLines) * entryHeight), 85, entryHeight), Localizer.Format("#LOC_BDArmory_DynamicDampingMax") + " :" + ActivePilot.DynamicDampingPitchMax.ToString("0.0"), Label);//"dynamic damping min"

                                dynPidLines++;
                                if (contextTipsEnabled)
                                {
                                    GUI.Label(new Rect(150, ((pidLines + dynPidLines) * entryHeight), contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_DynDampMax"), contextLabel);//"Steer Mult"
                                    dynPidLines++;
                                }
                                
                                ActivePilot.dynamicSteerDampingPitchFactor =
                                    GUI.HorizontalSlider(
                                        new Rect(leftIndent + 150, ((pidLines + dynPidLines) * entryHeight), contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                                        ActivePilot.dynamicSteerDampingPitchFactor, 0.1f, ActivePilot.UpToEleven ? 100 : 10);
                                ActivePilot.dynamicSteerDampingPitchFactor = Mathf.Round(ActivePilot.dynamicSteerDampingPitchFactor * 10f) / 10f;
                                GUI.Label(new Rect(leftIndent, ((pidLines + dynPidLines) * entryHeight), 85, entryHeight), Localizer.Format("#LOC_BDArmory_DynamicDampingPitchFactor") + " :" + ActivePilot.dynamicSteerDampingPitchFactor.ToString("0.0"), Label);//"dynamic damping min"

                                dynPidLines++;
                                if (contextTipsEnabled)
                                {
                                    GUI.Label(new Rect(150, ((pidLines + dynPidLines) * entryHeight), contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_DynDampMult"), contextLabel);//"Steer Mult"
                                    dynPidLines++;
                                }
                            }

                            ActivePilot.dynamicDampingYaw = GUI.Toggle(new Rect(leftIndent, ((pidLines + dynPidLines) * entryHeight), contentWidth - (2 * leftIndent), entryHeight),
                           ActivePilot.dynamicDampingYaw, Localizer.Format("#LOC_BDArmory_DynamicDampingYaw"), ActivePilot.dynamicDampingYaw ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"Dynamic pid"
                            dynPidLines += 1.25f;
                            if (ActivePilot.dynamicDampingYaw)
                            {                               
                                ActivePilot.DynamicDampingYawMin =
                                    GUI.HorizontalSlider(
                                        new Rect(leftIndent + 150, ((pidLines + dynPidLines) * entryHeight), contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                                        ActivePilot.DynamicDampingYawMin, 1f, ActivePilot.UpToEleven ? 100 : 8);
                                ActivePilot.DynamicDampingYawMin = Mathf.Round(ActivePilot.DynamicDampingYawMin * 10f) / 10f;
                                GUI.Label(new Rect(leftIndent, ((pidLines + dynPidLines) * entryHeight), 85, entryHeight), Localizer.Format("#LOC_BDArmory_DynamicDampingYawMin") + " :" + ActivePilot.DynamicDampingYawMin.ToString("0.0"), Label);//"dynamic damping min"

                                dynPidLines++;
                                if (contextTipsEnabled)
                                {
                                    GUI.Label(new Rect(150, ((pidLines + dynPidLines) * entryHeight), contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_DynDampMin"), contextLabel);//"Steer Mult"
                                    dynPidLines++;
                                }
                                
                                ActivePilot.DynamicDampingYawMax =
                                    GUI.HorizontalSlider(
                                        new Rect(leftIndent + 150, ((pidLines + dynPidLines) * entryHeight), contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                                        ActivePilot.DynamicDampingYawMax, 1f, ActivePilot.UpToEleven ? 100 : 8);
                                ActivePilot.DynamicDampingYawMax = Mathf.Round(ActivePilot.DynamicDampingYawMax * 10f) / 10f;
                                GUI.Label(new Rect(leftIndent, ((pidLines + dynPidLines) * entryHeight), 85, entryHeight), Localizer.Format("#LOC_BDArmory_DynamicDampingYawMax") + " :" + ActivePilot.DynamicDampingYawMax.ToString("0.0"), Label);//"dynamic damping min"

                                dynPidLines++;
                                if (contextTipsEnabled)
                                {
                                    GUI.Label(new Rect(150, ((pidLines + dynPidLines) * entryHeight), contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_DynDampMax"), contextLabel);//"Steer Mult"
                                    dynPidLines++;
                                }
                               
                                ActivePilot.dynamicSteerDampingYawFactor =
                                    GUI.HorizontalSlider(
                                        new Rect(leftIndent + 150, ((pidLines + dynPidLines) * entryHeight), contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                                        ActivePilot.dynamicSteerDampingYawFactor, 0.1f, ActivePilot.UpToEleven ? 100 : 10);
                                ActivePilot.dynamicSteerDampingYawFactor = Mathf.Round(ActivePilot.dynamicSteerDampingYawFactor / 10) * 10;
                                GUI.Label(new Rect(leftIndent, ((pidLines + dynPidLines) * entryHeight), 85, entryHeight), Localizer.Format("#LOC_BDArmory_DynamicDampingYawFactor") + " :" + ActivePilot.dynamicSteerDampingYawFactor.ToString("0.0"), Label);//"dynamic damping min"

                                dynPidLines++;
                                if (contextTipsEnabled)
                                {
                                    GUI.Label(new Rect(150, ((pidLines + dynPidLines) * entryHeight), contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_DynDampMult"), contextLabel);//"Steer Mult"
                                    dynPidLines++;
                                }
                            }

                            ActivePilot.dynamicDampingRoll = GUI.Toggle(new Rect(leftIndent, ((pidLines + dynPidLines) * entryHeight), contentWidth - (2 * leftIndent), entryHeight),
                            ActivePilot.dynamicDampingRoll, Localizer.Format("#LOC_BDArmory_DynamicDampingRoll"), ActivePilot.dynamicDampingRoll ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"Dynamic pid"
                            dynPidLines += 1.25f;
                            if (ActivePilot.dynamicDampingRoll)
                            {                                
                                ActivePilot.DynamicDampingRollMin =
                                    GUI.HorizontalSlider(
                                        new Rect(leftIndent + 150, ((pidLines + dynPidLines) * entryHeight), contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                                        ActivePilot.DynamicDampingRollMin, 1f, ActivePilot.UpToEleven ? 100 : 8);
                                ActivePilot.DynamicDampingRollMin = Mathf.Round(ActivePilot.DynamicDampingRollMin * 10f) / 10f;
                                GUI.Label(new Rect(leftIndent, ((pidLines + dynPidLines) * entryHeight), 85, entryHeight), Localizer.Format("#LOC_BDArmory_DynamicDampingRollMin") + " :" + ActivePilot.DynamicDampingRollMin.ToString("0.0"), Label);//"dynamic damping min"

                                dynPidLines++;
                                if (contextTipsEnabled)
                                {
                                    GUI.Label(new Rect(150, ((pidLines + dynPidLines) * entryHeight), contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_DynDampMin"), contextLabel);//"Steer Mult"
                                    dynPidLines++;
                                }
                                
                                ActivePilot.DynamicDampingRollMax =
                                    GUI.HorizontalSlider(
                                        new Rect(leftIndent + 150, ((pidLines + dynPidLines) * entryHeight), contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                                        ActivePilot.DynamicDampingRollMax, 1f, ActivePilot.UpToEleven ? 100 : 8);
                                ActivePilot.DynamicDampingRollMax = Mathf.Round(ActivePilot.DynamicDampingRollMax * 10f) / 10f;
                                GUI.Label(new Rect(leftIndent, ((pidLines + dynPidLines) * entryHeight), 85, entryHeight), Localizer.Format("#LOC_BDArmory_DynamicDampingRollMax") + " :" + ActivePilot.DynamicDampingRollMax.ToString("0.0"), Label);//"dynamic damping min"

                                dynPidLines++;
                                if (contextTipsEnabled)
                                {
                                    GUI.Label(new Rect(150, ((pidLines + dynPidLines) * entryHeight), contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_DynDampMax"), contextLabel);//"Steer Mult"
                                    dynPidLines++;
                                }
                                                                
                                ActivePilot.dynamicSteerDampingRollFactor =
                                    GUI.HorizontalSlider(
                                        new Rect(leftIndent + 150, ((pidLines + dynPidLines) * entryHeight), contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                                        ActivePilot.dynamicSteerDampingRollFactor, 0.1f, ActivePilot.UpToEleven ? 100 : 10);
                                ActivePilot.dynamicSteerDampingRollFactor = Mathf.Round(ActivePilot.dynamicSteerDampingRollFactor * 10f) / 10f;
                                GUI.Label(new Rect(leftIndent, ((pidLines + dynPidLines) * entryHeight), 85, entryHeight), Localizer.Format("#LOC_BDArmory_DynamicDampingRollFactor") + " :" + ActivePilot.dynamicSteerDampingRollFactor.ToString("0.0"), Label);//"dynamic damping min"

                                dynPidLines++;
                                if (contextTipsEnabled)
                                {
                                    GUI.Label(new Rect(150, ((pidLines + dynPidLines) * entryHeight), contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_DynDampMult"), contextLabel);//"Steer Mult"
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

                    GUI.Label(new Rect(leftIndent, altLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_PilotAI_Altitudes"), BoldLabel);//"Dynamic Damping"
                    altLines++;
                    
                    ActivePilot.defaultAltitude =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + 150, altLines * entryHeight, contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                            ActivePilot.defaultAltitude, 100, ActivePilot.UpToEleven ? 100000 : 15000);
                    ActivePilot.defaultAltitude = Mathf.Round(ActivePilot.defaultAltitude / 25) * 25;
                    GUI.Label(new Rect(leftIndent, altLines * entryHeight, 200, entryHeight), Localizer.Format("#LOC_BDArmory_DefaultAltitude") + " :" + ActivePilot.defaultAltitude.ToString("0"), Label);//"dynamic damping min"
                    altLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(new Rect(150, altLines * entryHeight, contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_DefAlt"), contextLabel);//"dynamic damp min"
                        altLines++;
                    }

                    ActivePilot.minAltitude =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + 150, altLines * entryHeight, contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                            ActivePilot.minAltitude, 25, ActivePilot.UpToEleven ? 60000 : 6000);
                    ActivePilot.minAltitude = Mathf.Round(ActivePilot.minAltitude / 25) * 25;
                    GUI.Label(new Rect(leftIndent, altLines * entryHeight, 200, entryHeight), Localizer.Format("#LOC_BDArmory_MinAltitude") + " :" + ActivePilot.minAltitude.ToString("0"), Label);//"dynamic damping min"
                    altLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(new Rect(150, altLines * entryHeight, contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_MinAlt"), contextLabel);//"dynamic damp max"
                        altLines++;
                    }

                    ActivePilot.maxAltitudeToggle = GUI.Toggle(new Rect(leftIndent, altLines * entryHeight, contentWidth - (2 * leftIndent), entryHeight),
                    ActivePilot.maxAltitudeToggle, Localizer.Format("#LOC_BDArmory_MaxAltitude"), ActivePilot.maxAltitudeToggle ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"Dynamic pid"
                    altLines += 1.25f;

                    if (ActivePilot.maxAltitudeToggle)
                    {                       
                        ActivePilot.maxAltitude =
                            GUI.HorizontalSlider(
                                new Rect(leftIndent + 150, altLines * entryHeight, contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                                ActivePilot.maxAltitude, 100, ActivePilot.UpToEleven ? 100000 : 15000);
                        ActivePilot.maxAltitude = Mathf.Round(ActivePilot.maxAltitude / 25) * 25;
                        GUI.Label(new Rect(leftIndent, altLines * entryHeight, 200, entryHeight), Localizer.Format("#LOC_BDArmory_MaxAltitude") + " :" + ActivePilot.maxAltitude.ToString("0"), Label);//"dynamic damping min"
                        altLines++;
                        if (contextTipsEnabled)
                        {
                            GUI.Label(new Rect(150, altLines * entryHeight, contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_MaxAlt"), contextLabel);//"dynamic damp mult"
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

                    GUI.Label(new Rect(leftIndent, spdLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_PilotAI_Speeds"), BoldLabel);//"Speed"
                    spdLines++;

                    ActivePilot.maxSpeed =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + 150, spdLines * entryHeight, contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                            ActivePilot.maxSpeed, 20, ActivePilot.UpToEleven ? 3000 : 800);
                    ActivePilot.maxSpeed = Mathf.Round(ActivePilot.maxSpeed);
                    GUI.Label(new Rect(leftIndent, spdLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_MaxSpeed") + " :" + ActivePilot.maxSpeed.ToString("0"), Label);//"dynamic damping min"

                    spdLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(new Rect(150, spdLines * entryHeight, contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_maxSpeed"), contextLabel);//"dynamic damp min"
                        spdLines++;
                    }

                    ActivePilot.takeOffSpeed =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + 150, spdLines * entryHeight, contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                            ActivePilot.takeOffSpeed, 10f, ActivePilot.UpToEleven ? 2000 : 200);
                    ActivePilot.takeOffSpeed = Mathf.Round(ActivePilot.takeOffSpeed);
                    GUI.Label(new Rect(leftIndent, spdLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_TakeOffSpeed") + " :" + ActivePilot.takeOffSpeed.ToString("0"), Label);//"dynamic damping max"

                    spdLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(new Rect(150, spdLines * entryHeight, contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_takeoff"), contextLabel);//"dynamic damp max"
                        spdLines++;
                    }

                    ActivePilot.minSpeed =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + 150, spdLines * entryHeight, contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                            ActivePilot.minSpeed, 10, ActivePilot.UpToEleven ? 2000 : 200);
                    ActivePilot.minSpeed = Mathf.Round(ActivePilot.minSpeed);
                    GUI.Label(new Rect(leftIndent, spdLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_MinSpeed") + " :" + ActivePilot.minSpeed.ToString("0"), Label);//"dynamic damping min"

                    spdLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(new Rect(150, spdLines * entryHeight, contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_minSpeed"), contextLabel);//"dynamic damp min"
                        spdLines++;
                    }

                    ActivePilot.strafingSpeed =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + 150, spdLines * entryHeight, contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                            ActivePilot.strafingSpeed, 10, 200);
                    ActivePilot.strafingSpeed = Mathf.Round(ActivePilot.strafingSpeed);
                    GUI.Label(new Rect(leftIndent, spdLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_StrafingSpeed") + " :" + ActivePilot.strafingSpeed.ToString("0"), Label);//"dynamic damping min"

                    spdLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(new Rect(150, spdLines * entryHeight, contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_atkSpeed"), contextLabel);//"dynamic damp min"
                        spdLines++;
                    }

                    ActivePilot.idleSpeed =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + 150, spdLines * entryHeight, contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                            ActivePilot.idleSpeed, 10, ActivePilot.UpToEleven ? 3000 : 200);
                    ActivePilot.idleSpeed = Mathf.Round(ActivePilot.idleSpeed);
                    GUI.Label(new Rect(leftIndent, spdLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_IdleSpeed") + " :" + ActivePilot.idleSpeed.ToString("0"), Label);//"dynamic damping min"

                    spdLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(new Rect(150, spdLines * entryHeight, contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_atkSpeed"), contextLabel);//"dynamic damp min"
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

                    GUI.Label(new Rect(leftIndent, ctrlLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_ControlLimits"), BoldLabel);//"Control"
                    ctrlLines++;

                    ActivePilot.maxSteer =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + 150, ctrlLines * entryHeight, contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                            ActivePilot.maxSteer, 0.1f, 1);
                    ActivePilot.maxSteer = Mathf.Round(ActivePilot.maxSteer * 20f) / 20f;
                    GUI.Label(new Rect(leftIndent, ctrlLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_LowSpeedSteerLimiter") + " :" + ActivePilot.maxSteer.ToString("0.00"), Label);//"dynamic damping min"

                    ctrlLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(new Rect(150, ctrlLines * entryHeight, contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_LSSL"), contextLabel);//"dynamic damp min"
                        ctrlLines++;
                    }

                    ActivePilot.lowSpeedSwitch =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + 150, ctrlLines * entryHeight, contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                            ActivePilot.lowSpeedSwitch, 100f, 500);
                    ActivePilot.lowSpeedSwitch = Mathf.Round(ActivePilot.lowSpeedSwitch);
                    GUI.Label(new Rect(leftIndent, ctrlLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_LowSpeedLimiterSpeed") + " :" + ActivePilot.lowSpeedSwitch.ToString("0"), Label);//"dynamic damping max"

                    ctrlLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(new Rect(150, ctrlLines * entryHeight, contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_LSLS"), contextLabel);//"dynamic damp max"
                        ctrlLines++;
                    }

                    ActivePilot.maxSteerAtMaxSpeed =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + 150, ctrlLines * entryHeight, contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                            ActivePilot.maxSteerAtMaxSpeed, 0.1f, 1);
                    ActivePilot.maxSteerAtMaxSpeed = Mathf.Round(ActivePilot.maxSteerAtMaxSpeed * 20f) / 20f;
                    GUI.Label(new Rect(leftIndent, ctrlLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_HighSpeedSteerLimiter") + " :" + ActivePilot.maxSteerAtMaxSpeed.ToString("0.00"), Label);//"dynamic damping min"

                    ctrlLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(new Rect(150, ctrlLines * entryHeight, contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_HSSL"), contextLabel);//"dynamic damp min"
                        ctrlLines++;
                    }

                    ActivePilot.cornerSpeed =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + 150, ctrlLines * entryHeight, contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                            ActivePilot.cornerSpeed, 10, 200);
                    ActivePilot.cornerSpeed = Mathf.Round(ActivePilot.cornerSpeed);
                    GUI.Label(new Rect(leftIndent, ctrlLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_HighSpeedLimiterSpeed") + " :" + ActivePilot.cornerSpeed.ToString("0"), Label);//"dynamic damping min"

                    ctrlLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(new Rect(150, ctrlLines * entryHeight, contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_HSLS"), contextLabel);//"dynamic damp min"
                        ctrlLines++;
                    }

                    ActivePilot.maxBank =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + 150, ctrlLines * entryHeight, contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                            ActivePilot.maxBank, 10, 180);
                    ActivePilot.maxBank = Mathf.Round(ActivePilot.maxBank / 5) * 5;
                    GUI.Label(new Rect(leftIndent, ctrlLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_BankLimiter") + " :" + ActivePilot.maxBank.ToString("0"), Label);//"dynamic damping min"

                    ctrlLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(new Rect(150, ctrlLines * entryHeight, contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_bankLimit"), contextLabel);//"dynamic damp min"
                        ctrlLines++;
                    }

                    ActivePilot.maxAllowedGForce =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + 150, ctrlLines * entryHeight, contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                            ActivePilot.maxAllowedGForce, 2, ActivePilot.UpToEleven ? 1000 : 45);
                    ActivePilot.maxAllowedGForce = Mathf.Round(ActivePilot.maxAllowedGForce * 4f) / 4f;
                    GUI.Label(new Rect(leftIndent, ctrlLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_maxAllowedGForce") + " :" + ActivePilot.maxAllowedGForce.ToString("0.00"), Label);//"dynamic damping min"

                    ctrlLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(new Rect(150, ctrlLines * entryHeight, contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_GForce"), contextLabel);//"dynamic damp min"
                        ctrlLines++;
                    }

                    ActivePilot.maxAllowedAoA =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + 150, ctrlLines * entryHeight, contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                            ActivePilot.maxAllowedAoA, 0, ActivePilot.UpToEleven ? 180 : 85);
                    ActivePilot.maxAllowedAoA = Mathf.Round(ActivePilot.maxAllowedAoA * 0.4f) / 0.4f;
                    GUI.Label(new Rect(leftIndent, ctrlLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_maxAllowedAoA") + " :" + ActivePilot.maxAllowedAoA.ToString("0.0"), Label);//"dynamic damping min"

                    ctrlLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(new Rect(150, ctrlLines * entryHeight, contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_AoA"), contextLabel);//"dynamic damp min"
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

                    GUI.Label(new Rect(leftIndent, evadeLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_EvadeExtend"), BoldLabel);//"Speed"
                    evadeLines++;

                    ActivePilot.minEvasionTime =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + 150, evadeLines * entryHeight, contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                            ActivePilot.minEvasionTime, 0f, ActivePilot.UpToEleven ? 10 : 1);
                    ActivePilot.minEvasionTime = Mathf.Round(ActivePilot.minEvasionTime * 20f) / 20f;
                    GUI.Label(new Rect(leftIndent, evadeLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_MinEvasionTime") + " :" + ActivePilot.minEvasionTime.ToString("0.00"), Label);//"dynamic damping min"

                    evadeLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(new Rect(150, evadeLines * entryHeight, contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_MinEvade"), contextLabel);//"dynamic damp min"
                        evadeLines++;
                    }

                    ActivePilot.evasionThreshold =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + 150, evadeLines * entryHeight, contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                            ActivePilot.evasionThreshold, 0, ActivePilot.UpToEleven ? 300 : 100);
                    ActivePilot.evasionThreshold = Mathf.Round(ActivePilot.evasionThreshold);
                    GUI.Label(new Rect(leftIndent, evadeLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_EvasionThreshold") + " :" + ActivePilot.evasionThreshold.ToString("0"), Label);//"dynamic damping max"

                    evadeLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(new Rect(150, ((pidLines + altLines) * entryHeight), contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_evadeDist"), contextLabel);//"dynamic damp max"
                        evadeLines++;
                    }

                    ActivePilot.evasionTimeThreshold =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + 150, evadeLines * entryHeight, contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                            ActivePilot.evasionTimeThreshold, 0, ActivePilot.UpToEleven ? 1 : 3);
                    ActivePilot.evasionTimeThreshold = Mathf.Round(ActivePilot.evasionTimeThreshold * 100f) / 100f;
                    GUI.Label(new Rect(leftIndent, evadeLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_EvasionTimeThreshold") + " :" + ActivePilot.evasionTimeThreshold.ToString("0.00"), Label);//"dynamic damping min"

                    evadeLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(new Rect(150, evadeLines * entryHeight, contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_evadetimeDist"), contextLabel);//"dynamic damp min"
                        evadeLines++;
                    }

                    ActivePilot.collisionAvoidanceThreshold =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + 150, evadeLines * entryHeight, contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                            ActivePilot.collisionAvoidanceThreshold, 0, 50);
                    ActivePilot.collisionAvoidanceThreshold = Mathf.Round(ActivePilot.collisionAvoidanceThreshold);
                    GUI.Label(new Rect(leftIndent, evadeLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_CollisionAvoidanceThreshold") + " :" + ActivePilot.collisionAvoidanceThreshold.ToString("0"), Label);//"dynamic damping min"

                    evadeLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(new Rect(150, evadeLines * entryHeight, contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_ColDist"), contextLabel);//"dynamic damp min"
                        evadeLines++;
                    }

                    ActivePilot.vesselCollisionAvoidancePeriod =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + 150, evadeLines * entryHeight, contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                            ActivePilot.vesselCollisionAvoidancePeriod, 0, 3);
                    ActivePilot.vesselCollisionAvoidancePeriod = Mathf.Round(ActivePilot.vesselCollisionAvoidancePeriod * 10f) / 10f;
                    GUI.Label(new Rect(leftIndent, evadeLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_CollisionAvoidancePeriod") + " :" + ActivePilot.vesselCollisionAvoidancePeriod.ToString("0.0"), Label);//"dynamic damping min"

                    evadeLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(new Rect(150, evadeLines * entryHeight, contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_ColTime"), contextLabel);//"dynamic damp min"
                        evadeLines++;
                    }

                    ActivePilot.vesselStandoffDistance =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + 150, evadeLines * entryHeight, contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                            ActivePilot.vesselStandoffDistance, 2, ActivePilot.UpToEleven ? 5000 : 1000);
                    ActivePilot.vesselStandoffDistance = Mathf.Round(ActivePilot.vesselStandoffDistance / 50) * 50;
                    GUI.Label(new Rect(leftIndent, evadeLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_StandoffDistance") + " :" + ActivePilot.vesselStandoffDistance.ToString("0"), Label);//"dynamic damping min"

                    evadeLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(new Rect(150, evadeLines * entryHeight, contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_standoff"), contextLabel);//"dynamic damp min"
                        evadeLines+= 1.25f;
                    }
                    if (ActivePilot.canExtend)
                    {
                        ActivePilot.extendMult =
                            GUI.HorizontalSlider(
                                new Rect(leftIndent + 150, evadeLines * entryHeight, contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                                ActivePilot.extendMult, 0, ActivePilot.UpToEleven ? 200 : 2);
                        ActivePilot.extendMult = Mathf.Round(ActivePilot.extendMult * 10f) / 10f;
                        GUI.Label(new Rect(leftIndent, evadeLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_ExtendMultiplier") + " :" + ActivePilot.extendMult.ToString("0.0"), Label);//"dynamic damping min"

                        evadeLines++;
                        if (contextTipsEnabled)
                        {
                            GUI.Label(new Rect(150, evadeLines * entryHeight, contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_ExtendMult"), contextLabel);//"dynamic damp min"
                            evadeLines++;
                        }

                        ActivePilot.extendTargetVel =
                            GUI.HorizontalSlider(
                                new Rect(leftIndent + 150, evadeLines * entryHeight, contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                                ActivePilot.extendTargetVel, 0, 2);
                        ActivePilot.extendTargetVel = Mathf.Round(ActivePilot.extendTargetVel * 10f) / 10f;
                        GUI.Label(new Rect(leftIndent, evadeLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_ExtendTargetVel") + " :" + ActivePilot.extendTargetVel.ToString("0.0"), Label);//"dynamic damping min"

                        evadeLines++;
                        if (contextTipsEnabled)
                        {
                            GUI.Label(new Rect(150, evadeLines * entryHeight, contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_Extendvel"), contextLabel);//"dynamic damp min"
                            evadeLines++;
                        }

                        ActivePilot.extendTargetAngle =
                            GUI.HorizontalSlider(
                                new Rect(leftIndent + 150, evadeLines * entryHeight, contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                                ActivePilot.extendTargetAngle, 0, 180);
                        ActivePilot.extendTargetAngle = Mathf.Round(ActivePilot.extendTargetAngle);
                        GUI.Label(new Rect(leftIndent, evadeLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_ExtendTargetAngle") + " :" + ActivePilot.extendTargetAngle.ToString("0"), Label);// "dynamic damping min"

                        evadeLines++;
                        if (contextTipsEnabled)
                        {
                            GUI.Label(new Rect(150, evadeLines * entryHeight, contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_ExtendAngle"), contextLabel);//"dynamic damp min"
                            evadeLines++;
                        }

                        ActivePilot.extendTargetDist =
                            GUI.HorizontalSlider(
                                new Rect(leftIndent + 150, evadeLines * entryHeight, contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                                ActivePilot.extendTargetDist, 0, 5000);
                        ActivePilot.extendTargetDist = Mathf.Round(ActivePilot.extendTargetDist / 25) * 25;
                        GUI.Label(new Rect(leftIndent, evadeLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_ExtendTargetDist") + " :" + ActivePilot.extendTargetDist.ToString("0.00"), Label);//"dynamic damping min"

                        evadeLines++;
                        if (contextTipsEnabled)
                        {
                            GUI.Label(new Rect(150, evadeLines * entryHeight, contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_ExtendDist"), contextLabel);//"dynamic damp min"
                            evadeLines++;
                        }
                    }
                    ActivePilot.canExtend = GUI.Toggle(new Rect(leftIndent, evadeLines * entryHeight, contentWidth - (2 * leftIndent), entryHeight),
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

                    GUI.Label(new Rect(leftIndent, gndLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_PilotAI_Terrain"), BoldLabel);//"Speed"
                    gndLines++;

                    ActivePilot.turnRadiusTwiddleFactorMin =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + 150, gndLines * entryHeight, contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                            ActivePilot.turnRadiusTwiddleFactorMin, 1, ActivePilot.UpToEleven ? 10 : 5);
                    ActivePilot.turnRadiusTwiddleFactorMin = Mathf.Round(ActivePilot.turnRadiusTwiddleFactorMin * 10f) / 10f;
                    GUI.Label(new Rect(leftIndent, gndLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_TurnRadiusMin") + " :" + ActivePilot.turnRadiusTwiddleFactorMin.ToString("0.0"), Label); //"dynamic damping min"

                    gndLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(new Rect(150, gndLines * entryHeight, contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_terrainMin"), contextLabel);//"dynamic damp min"
                        gndLines++;
                    }

                    ActivePilot.turnRadiusTwiddleFactorMax =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + 150, gndLines * entryHeight, contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                            ActivePilot.turnRadiusTwiddleFactorMax, 1, ActivePilot.UpToEleven ? 10 : 5);
                    ActivePilot.turnRadiusTwiddleFactorMax = Mathf.Round(ActivePilot.turnRadiusTwiddleFactorMax * 10) / 10;
                    GUI.Label(new Rect(leftIndent, gndLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_TurnRadiusMax") + " :" + ActivePilot.turnRadiusTwiddleFactorMax.ToString("0.0"), Label);//"dynamic damping min"

                    gndLines++;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(new Rect(150, gndLines * entryHeight, contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_terrainMax"), contextLabel);//"dynamic damp min"
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

                    GUI.Label(new Rect(leftIndent, ramLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_PilotAI_Ramming"), BoldLabel);//"Speed"
                    ramLines++;

                    ActivePilot.allowRamming = GUI.Toggle(new Rect(leftIndent, ramLines * entryHeight, contentWidth - 2 * leftIndent, entryHeight),
ActivePilot.allowRamming, Localizer.Format("#LOC_BDArmory_AllowRamming"), ActivePilot.allowRamming ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"Dynamic pid"
                    ramLines+= 1.25f;

                    if (ActivePilot.allowRamming)
                    {
                        ActivePilot.controlSurfaceLag =
                            GUI.HorizontalSlider(
                                new Rect(leftIndent + 150, ramLines * entryHeight, contentWidth - (leftIndent*2) - (150 + 10), entryHeight),
                                ActivePilot.controlSurfaceLag, 0, ActivePilot.UpToEleven ? 01f : 0.2f);
                        ActivePilot.controlSurfaceLag = Mathf.Round(ActivePilot.controlSurfaceLag * 100) / 100;
                        GUI.Label(new Rect(leftIndent, ramLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_ControlSurfaceLag") + " :" + ActivePilot.controlSurfaceLag.ToString("0.00"), Label);//"dynamic damping min"

                        ramLines++;
                        if (contextTipsEnabled)
                        {
                            GUI.Label(new Rect(150, ramLines * entryHeight, contentWidth - 150, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_ramLag"), contextLabel);//"dynamic damp min"
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

                    GUI.Label(new Rect(leftIndent, miscLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_Orbit"), BoldLabel);//"Speed"
                    miscLines++;

                    ActivePilot.ClockwiseOrbit = GUI.Toggle(new Rect(leftIndent, miscLines * entryHeight, contentWidth - 2 * leftIndent, entryHeight),
ActivePilot.ClockwiseOrbit, ActivePilot.ClockwiseOrbit ? Localizer.Format("#LOC_BDArmory_Orbit_enabledText") : Localizer.Format("#LOC_BDArmory_Orbit_disabledText"), ActivePilot.ClockwiseOrbit ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"Dynamic pid"
                    miscLines += 1.25f;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(new Rect(150 + leftIndent, miscLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_orbit"), Label);//"dynamic damp min"
                        miscLines++;
                    }

                    GUI.Label(new Rect(leftIndent, miscLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_StandbyMode"), BoldLabel);//"Speed"
                    miscLines++;

                    ActivePilot.standbyMode = GUI.Toggle(new Rect(leftIndent, miscLines * entryHeight, contentWidth - 2 * leftIndent, entryHeight),
ActivePilot.standbyMode, ActivePilot.standbyMode ? Localizer.Format("#LOC_BDArmory_On") : Localizer.Format("#LOC_BDArmory_Off"), ActivePilot.standbyMode ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);//"Dynamic pid"
                    miscLines += 1.25f;
                    if (contextTipsEnabled)
                    {
                        GUI.Label(new Rect(150 + leftIndent, miscLines * entryHeight, 85, entryHeight), Localizer.Format("#LOC_BDArmory_AIWindow_standby"), Label);//"dynamic damp min"
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
