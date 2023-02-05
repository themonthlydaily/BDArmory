using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using BDArmory.Settings;
using BDArmory.UI;
using BDArmory.Utils;

namespace BDArmory.Competition.VesselMover
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class VesselMover : MonoBehaviour
    {
        public static VesselMover Instance;

        enum State { None, Moving, Spawning };
        State state
        {
            get { return _state; }
            set { _state = value; ResetWindowHeight(); }
        }
        State _state;
        internal WaitForFixedUpdate wait = new WaitForFixedUpdate();
        HashSet<Vessel> movingVessels = new HashSet<Vessel>();

        void Awake()
        {
            if (Instance != null) Destroy(Instance);
            Instance = this;
        }

        void Start()
        {
            ready = false;
            StartCoroutine(WaitForBdaSettings());
            GameEvents.onVesselChange.Add(OnVesselChanged);
        }

        void OnDestroy()
        {
            GameEvents.onVesselChange.Remove(OnVesselChanged);
        }

        private IEnumerator WaitForBdaSettings()
        {
            yield return new WaitUntil(() => BDArmorySettings.ready);

            BDArmorySetup.WindowRectVesselMover.height = 0;
            if (guiCheckIndex < 0) guiCheckIndex = GUIUtils.RegisterGUIRect(new Rect());
            ready = true;
            BDArmorySetup.Instance.hasVesselMover = true;
        }

        IEnumerator MoveVessel(Vessel vessel)
        {
            if (vessel == null || vessel.packed) yield break;
            Debug.Log($"[BDArmory.VesselMover]: Moving {vessel.vesselName}...");
            state = State.Moving;
            movingVessels.Add(vessel);

            while (vessel != null && !vessel.packed && movingVessels.Contains(vessel))
            {
                yield return wait;
            }
        }

        IEnumerator PlaceVessel(Vessel vessel)
        {
            if (vessel == null || vessel.packed || !movingVessels.Contains(vessel)) yield break;
            Debug.Log($"[BDArmory.VesselMover]: Placing {vessel.vesselName}...");
            movingVessels.Remove(vessel);

            yield return wait;
            state = State.None;
        }

        void DropVessel(Vessel vessel)
        {
            if (vessel == null || vessel.packed || !movingVessels.Contains(vessel)) return;
            Debug.Log($"[BDArmory.VesselMover]: Dropping {vessel.vesselName}...");
            movingVessels.Remove(vessel);
            state = State.None;
        }

        IEnumerator SpawnVessel()
        {
            Debug.Log($"[BDArmory.VesselMover]: Spawning vessel...");
            state = State.Spawning;

            yield return wait;

            state = State.None;
        }


        #region GUI
        static int guiCheckIndex = -1;
        bool helpShowing = false;
        bool ready = false;

        private void OnGUI()
        {
            if (!(ready && BDArmorySetup.GAME_UI_ENABLED && BDArmorySetup.Instance.showVesselMoverGUI))
                return;

            BDArmorySetup.SetGUIOpacity();
            BDArmorySetup.WindowRectVesselMover = GUILayout.Window(
                GUIUtility.GetControlID(FocusType.Passive),
                BDArmorySetup.WindowRectVesselMover,
                WindowVesselMover,
                StringUtils.Localize("#LOC_BDArmory_BDAVesselMover_Title"), // "BDA Vessel Mover"
                BDArmorySetup.BDGuiSkin.window,
                GUILayout.Width(250)
            );
            BDArmorySetup.SetGUIOpacity(false);
            GUIUtils.UpdateGUIRect(BDArmorySetup.WindowRectVesselMover, guiCheckIndex);
        }

        void OnVesselChanged(Vessel vessel)
        {
            if (vessel == null || vessel.packed)
            { state = State.None; }
            if (movingVessels.Contains(vessel))
            { state = State.Moving; }
            else
            { state = State.None; }
            movingVessels = movingVessels.Where(v => v != null && !v.packed).ToHashSet(); // Clean the moving vessels hashset.
        }

        private void WindowVesselMover(int id)
        {
            GUI.DragWindow(new Rect(0, 0, BDArmorySetup.WindowRectVesselMover.width, 20));
            GUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            switch (state)
            {
                case State.None:
                    {
                        // Two buttons: Move Vessel, Spawn Vessel. Two toggles: Spawn Crew, Choose Crew
                        if (GUILayout.Button(StringUtils.Localize("#LOC_BDArmory_BDAVesselMover_MoveVessel"), BDArmorySetup.BDGuiSkin.button)) StartCoroutine(MoveVessel(FlightGlobals.ActiveVessel));
                        if (GUILayout.Button(StringUtils.Localize("#LOC_BDArmory_BDAVesselMover_SpawnVessel"), BDArmorySetup.BDGuiSkin.button)) StartCoroutine(SpawnVessel());
                        GUILayout.BeginHorizontal();
                        BDArmorySettings.VESSEL_MOVER_CHOOSE_CREW = GUILayout.Toggle(BDArmorySettings.VESSEL_MOVER_CHOOSE_CREW, StringUtils.Localize("#LOC_BDArmory_BDAVesselMover_ChooseCrew"));
                        GUILayout.EndHorizontal();
                        break;
                    }
                case State.Moving:
                    {
                        // Two buttons: Place Vessel, Drop Vessel. Small Help button -> expands to show keys
                        if (GUILayout.Button(StringUtils.Localize("#LOC_BDArmory_BDAVesselMover_PlaceVessel"), BDArmorySetup.BDGuiSkin.button)) StartCoroutine(PlaceVessel(FlightGlobals.ActiveVessel));
                        if (GUILayout.Button(StringUtils.Localize("#LOC_BDArmory_BDAVesselMover_DropVessel"), BDArmorySetup.BDGuiSkin.button)) DropVessel(FlightGlobals.ActiveVessel);
                        if (GUILayout.Button(StringUtils.Localize("#LOC_BDArmory_Generic_Help"), helpShowing ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button, GUILayout.Height(20)))
                        {
                            helpShowing = !helpShowing;
                            if (!helpShowing) ResetWindowHeight();
                        }
                        if (helpShowing)
                        {
                            GUILayout.BeginVertical();
                            GUILayout.Label("Help text");
                            GUILayout.Label("goes here");
                            GUILayout.EndVertical();
                        }
                        break;
                    }
                case State.Spawning:
                    {
                        // Opens craft browser. Opens crew selection after craft selection if Choose Crew is enabled.
                        if (BDArmorySettings.DEBUG_SPAWNING) Debug.Log($"[BDArmory.VesselMover]: Spawning a vessel...");
                        break;
                    }
            }
            GUILayout.EndVertical();
            GUIUtils.RepositionWindow(ref BDArmorySetup.WindowRectVesselMover);
            GUIUtils.UpdateGUIRect(BDArmorySetup.WindowRectVesselMover, guiCheckIndex);
            GUIUtils.UseMouseEventInRect(BDArmorySetup.WindowRectVesselMover);
        }

        /// <summary>
        /// Reset the height of the window so that it shrinks.
        /// </summary>
        void ResetWindowHeight()
        {
            BDArmorySetup.WindowRectVesselMover.height = 0;
        }

        public void SetVisible(bool visible)
        {
            BDArmorySetup.Instance.showVesselMoverGUI = visible;
            GUIUtils.SetGUIRectVisible(guiCheckIndex, visible);
        }
        #endregion
    }
}