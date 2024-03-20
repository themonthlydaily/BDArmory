using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BDArmory.Competition.OrchestrationStrategies;
using BDArmory.GameModes.Waypoints;
using BDArmory.Settings;
using BDArmory.Utils;
using BDArmory.VesselSpawning;
using System.Linq.Expressions;

namespace BDArmory.UI
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class CourseBuilderGUI : MonoBehaviour
    {
        #region Fields
        public static CourseBuilderGUI Instance;
        private static int _guiCheckIndex = -1;
        private static readonly float _buttonSize = 20;
        private static readonly float _margin = 5;
        private static readonly float _lineHeight = _buttonSize;
        private float _windowHeight; //auto adjusting
        private float _windowWidth;
        public bool _ready = false;
        Dictionary<string, NumericInputField> spawnFields;

        int selected_index = 1;
        int selected_gate_index = 1;
        public float SelectedGate = 0;
        public static string Gatepath;
        public string SelectedModel;
        double movementIncrement = 1;
        float recordingIncrement = 10;

        private bool ShowLoadMenu = false;
        private bool ShowNewCourse = false;
        private string newCourseName = "";
        private bool recording = false;
        private bool showCourseWPsComboBox = false;
        private bool showPositioningControls = false;
        public List<WayPointMarker> loadedGates;
        #endregion

        #region Styles
        /// <summary>
        /// Need Left 1/3 width button
        /// Need Right 2/3 textfield
        /// Needleft/mid/right numfied boxes
        /// need Left <</>> Mid <</>> right <</>> buttons to flank the L/M/R numfields
        /// Need 1/2 width buttons
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>

        Rect SLineRect(float line)
        {
            return new Rect(_margin, line * _lineHeight, _windowWidth - 2 * _margin, _lineHeight);
        }

        Rect SLeftButtonRect(float line)
        {
            return new Rect(_margin, line * _lineHeight, (_windowWidth - 2 * _margin) / 2 - _margin / 4, _lineHeight);
        }

        Rect SRightButtonRect(float line)
        {
            return new Rect(_windowWidth / 2 + _margin / 4, line * _lineHeight, (_windowWidth - 2 * _margin) / 2 - _margin / 4, _lineHeight);
        }

        Rect SQuarterRect(float line, int pos, int span = 1, float indent = 0)
        {
            return new Rect(_margin + (pos % 4) * (_windowWidth - 2f * _margin) / 4f + indent, (line + (int)(pos / 4)) * _lineHeight, span * (_windowWidth - 2f * _margin) / 4f - indent, _lineHeight);
        }

        Rect SFieldButtonRect(float line, float offset)
        {
            var rectGap = _windowWidth / 24;
            return new Rect(rectGap * offset, line * _lineHeight, rectGap, _lineHeight);
        }

        List<Rect> SRight3Rects(float line)
        {
            var rectGap = _windowWidth / 24;
            var rectWidth = _windowWidth / 6;
            var rects = new List<Rect>();
            rects.Add(new Rect(rectGap * 2.5f, line * _lineHeight, rectWidth, _lineHeight));
            rects.Add(new Rect(rectGap * 10, line * _lineHeight, rectWidth, _lineHeight));
            rects.Add(new Rect(rectGap * 17.5f, line * _lineHeight, rectWidth, _lineHeight));
            return rects;
        }

        GUIStyle leftLabel;
        GUIStyle listStyle;
        GUIStyle centreLabel;
        #endregion
        private string txtName = string.Empty;
        private void Awake()
        {
            if (Instance)
                Destroy(this);
            Instance = this;
        }

        private void Start()
        {
            _ready = false;
            StartCoroutine(WaitForBdaSettings());

            leftLabel = new GUIStyle();
            leftLabel.alignment = TextAnchor.UpperLeft;
            leftLabel.normal.textColor = Color.white;
            listStyle = new GUIStyle(BDArmorySetup.BDGuiSkin.button);
            listStyle.fixedHeight = 18; //make list contents slightly smaller
            centreLabel = new GUIStyle();
            centreLabel.alignment = TextAnchor.UpperCenter;
            centreLabel.normal.textColor = Color.white;

            // Spawn fields
            spawnFields = new Dictionary<string, NumericInputField> {
                { "lat", gameObject.AddComponent<NumericInputField>().Initialise(0, FlightGlobals.currentMainBody.GetLatitudeAndLongitude(FlightGlobals.ActiveVessel.CoM).x, -90, 90) },
                { "lon", gameObject.AddComponent<NumericInputField>().Initialise(0, FlightGlobals.currentMainBody.GetLatitudeAndLongitude(FlightGlobals.ActiveVessel.CoM).y, -180, 180) },
                { "alt", gameObject.AddComponent<NumericInputField>().Initialise(0, BDArmorySettings.VESSEL_SPAWN_ALTITUDE) },
                { "increment", gameObject.AddComponent<NumericInputField>().Initialise(0.001f, movementIncrement) },
                { "diameter", gameObject.AddComponent<NumericInputField>().Initialise(0, 500) },
                { "interval", gameObject.AddComponent<NumericInputField>().Initialise(0, recordingIncrement) },
            };
            selected_index = FlightGlobals.currentMainBody != null ? FlightGlobals.currentMainBody.flightGlobalsIndex : 1;
            loadedGates = new List<WayPointMarker>();
        }

        private IEnumerator WaitForBdaSettings()
        {
            yield return new WaitUntil(() => BDArmorySetup.Instance is not null);

            BDArmorySetup.Instance.hasWPCourseSpawner = true;
            if (_guiCheckIndex < 0) _guiCheckIndex = GUIUtils.RegisterGUIRect(new Rect());
            _ready = true;
            SetVisible(BDArmorySetup.showWPBuilderGUI);
        }
        float startTime = 0;

        IEnumerator RecordCourse()
        {
            startTime = Time.time;
            while (recording)
            {
                while (Time.time - recordingIncrement < startTime)
                {
                    yield return null;
                }
                if (!recording) yield break;
                string newName = "Waypoint " + WaypointCourses.CourseLocations[selected_index].waypoints.Count.ToString();
                AddGate(newName);
                startTime = Time.time;
            }
        }

        void SnapCameraToGate() //this is locking cam to gate, can zoom, but can't rotate cam, can't return to vessel, cam sometimes shoots into orbit; Ask doc about Camera modification - FIXME
        {
            return; //returning until I can get Doc to look at this and tell me what I'm doing wrong
            //ideally, camera should snap to selected gate, but still allow camera movement to allow user to look around, return cam to vessel when coursebuilder GUi closed.
            var flightCamera = FlightCamera.fetch;
            var cameraHeading = FlightCamera.CamHdg;
            var cameraPitch = FlightCamera.CamPitch;
            var distance = 1000;
            if (FlightGlobals.ActiveVessel != null && FlightGlobals.ActiveVessel.PatchedConicsAttached) FlightGlobals.ActiveVessel.DetachPatchedConicsSolver();
            Waypoint gate = WaypointCourses.CourseLocations[selected_index].waypoints[selected_gate_index];
            var terrainAltitude = FlightGlobals.currentMainBody.TerrainAltitude(gate.location.x, gate.location.y);
            var spawnPoint = FlightGlobals.currentMainBody.GetWorldSurfacePosition(gate.location.x, gate.location.y, terrainAltitude + gate.location.z);
            FloatingOrigin.SetOffset(gate.location); // This adjusts local coordinates, such that spawnPoint is (0,0,0).
            var radialUnitVector = -FlightGlobals.currentMainBody.transform.position.normalized;
            var cameraPosition = Vector3.RotateTowards(distance * radialUnitVector, Quaternion.AngleAxis(cameraHeading * Mathf.Rad2Deg, radialUnitVector) * -VectorUtils.GetNorthVector(spawnPoint, FlightGlobals.currentMainBody), 70f * Mathf.Deg2Rad, 0);

            flightCamera.transform.parent = loadedGates[selected_gate_index].transform;
            flightCamera.SetTarget(loadedGates[selected_gate_index].transform);
            flightCamera.transform.localPosition = cameraPosition;
            flightCamera.transform.localRotation = Quaternion.identity;
            flightCamera.ActivateUpdate();

            flightCamera.SetDistanceImmediate(distance);
            FlightCamera.CamHdg = cameraHeading;
            FlightCamera.CamPitch = cameraPitch;
        }

        private void OnGUI()
        {
            if (!(_ready && BDArmorySetup.GAME_UI_ENABLED && BDArmorySetup.showWPBuilderGUI && HighLogic.LoadedSceneIsFlight))
                return;

            _windowWidth = BDArmorySettings.VESSEL_WAYPOINT_WINDOW_WIDTH;
            SetNewHeight(_windowHeight);
            BDArmorySetup.WindowRectWayPointSpawner = new Rect(
                BDArmorySetup.WindowRectWayPointSpawner.x,
                BDArmorySetup.WindowRectWayPointSpawner.y,
                _windowWidth,
                _windowHeight
            );
            BDArmorySetup.SetGUIOpacity();
            var guiMatrix = GUI.matrix;
            if (BDArmorySettings.UI_SCALE != 1) GUIUtility.ScaleAroundPivot(BDArmorySettings.UI_SCALE * Vector2.one, BDArmorySetup.WindowRectWayPointSpawner.position);
            BDArmorySetup.WindowRectWayPointSpawner = GUI.Window(
                GUIUtility.GetControlID(FocusType.Passive),
                BDArmorySetup.WindowRectWayPointSpawner,
                WindowWaypointSpawner,
                StringUtils.Localize("#LOC_BDArmory_BDAWaypointBuilder_Title"),//"BDA Vessel Spawner"
                BDArmorySetup.BDGuiSkin.window
            );
            BDArmorySetup.SetGUIOpacity(false);
            GUIUtils.UpdateGUIRect(BDArmorySetup.WindowRectWayPointSpawner, _guiCheckIndex);
        }

        private void SetNewHeight(float windowHeight)
        {
            var previousWindowHeight = BDArmorySetup.WindowRectWayPointSpawner.height;
            BDArmorySetup.WindowRectWayPointSpawner.height = windowHeight;
            GUIUtils.RepositionWindow(ref BDArmorySetup.WindowRectWayPointSpawner, previousWindowHeight);
        }

        private void WindowWaypointSpawner(int id)
        {
            GUI.DragWindow(new Rect(0, 0, _windowWidth - _buttonSize - _margin, _buttonSize + _margin));
            if (GUI.Button(new Rect(_windowWidth - _buttonSize - (_margin - 2), _margin, _buttonSize - 2, _buttonSize - 2), " X", BDArmorySetup.CloseButtonStyle))
            {
                SetVisible(false);
                BDArmorySetup.SaveConfig();
            }

            float line = 0.25f;
            var rects = new List<Rect>();

            if (GUI.Button(SLeftButtonRect(++line), $"{StringUtils.Localize("Load Course")}", ShowLoadMenu ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button))//Load Course
            {
                ShowLoadMenu = !ShowLoadMenu;
            }

            if (GUI.Button(SRightButtonRect(line), $"{StringUtils.Localize("New Course")}", ShowNewCourse ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button))//Load Course
            {
                ShowNewCourse = !ShowNewCourse;
            }
            if (ShowLoadMenu)
            {
                line++;                
                int i = 0;
                foreach (var wpCourse in WaypointCourses.CourseLocations)
                {
                    if (GUI.Button(SQuarterRect(line, i++), wpCourse.name, i - 1 == selected_index ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button))
                    {
                        switch (Event.current.button)
                        {
                            case 1: // right click
                                WaypointCourses.CourseLocations.Remove(wpCourse);
                                WaypointField.Save();
                                if (selected_index >= WaypointCourses.CourseLocations.Count) selected_index = WaypointCourses.CourseLocations.Count - 1;
                                break;
                            default:
                                //if loading an off-world course, warp to world
                                //if (wpCourse.worldIndex != FlightGlobals.GetBodyIndex(FlightGlobals.currentMainBody))
                                SpawnUtils.ShowSpawnPoint(wpCourse.worldIndex, wpCourse.spawnPoint.x, wpCourse.spawnPoint.y, 500);
                                Vector3 previousLocation = FlightGlobals.ActiveVessel.transform.position;
                                //clear any previously loaded gates
                                foreach (var gate in loadedGates)
                                {
                                    gate.disabled = true;
                                    gate.gameObject.SetActive(false);
                                }
                                loadedGates.Clear();
                                ShowLoadMenu = false;
                                selected_index = i -1;
                                showCourseWPsComboBox = true;
                                //spawn in course gates
                                Debug.Log($"Loading Course; selected index: {selected_index}, ({WaypointCourses.CourseLocations[selected_index].name}) starting gate spawn");
                                for (int wp = 0; wp < wpCourse.waypoints.Count; wp++)
                                {
                                    float terrainAltitude = (float)FlightGlobals.currentMainBody.TerrainAltitude(wpCourse.waypoints[wp].location.x, wpCourse.waypoints[wp].location.y);
                                    Vector3d WorldCoords = VectorUtils.GetWorldSurfacePostion(new Vector3(wpCourse.waypoints[wp].location.x, wpCourse.waypoints[wp].location.y, (BDArmorySettings.WAYPOINTS_ALTITUDE == 0 ? wpCourse.waypoints[wp].location.z : BDArmorySettings.WAYPOINTS_ALTITUDE) + terrainAltitude), FlightGlobals.currentMainBody);
                                    var direction = (WorldCoords - previousLocation).normalized;
                                    WayPointMarker.CreateWaypoint(WorldCoords, direction, "BDArmory/Models/WayPoint/Ring", wpCourse.waypoints[wp].scale);

                                    previousLocation = WorldCoords;
                                    var location = string.Format("({0:##.###}, {1:##.###}, {2:####}", wpCourse.waypoints[wp].location.x, wpCourse.waypoints[wp].location.y, wpCourse.waypoints[wp].location.z);
                                    Debug.Log("[BDArmory.Waypoints]: Creating waypoint marker at  " + " " + location + " World: " + FlightGlobals.currentMainBody.flightGlobalsIndex + " scale: " + wpCourse.waypoints[wp].scale);
                                }
                                break;
                        }
                    }
                }
                line += (int)((i - 1) / 4);
                line += 0.3f;
            }

            if (ShowNewCourse)
            {
                newCourseName = GUI.TextField(SLeftButtonRect(++line), newCourseName);
                if (!string.IsNullOrEmpty(newCourseName))
                {
                    if (GUI.Button(SQuarterRect(line, 2), StringUtils.Localize("Create"), BDArmorySetup.BDGuiSkin.button))
                    {
                        Vector3d spawnCoords = VectorUtils.GetWorldSurfacePostion(FlightGlobals.ActiveVessel.transform.position, FlightGlobals.currentMainBody);
                        if (!WaypointCourses.CourseLocations.Select(l => l.name).ToList().Contains(newCourseName))
                            WaypointCourses.CourseLocations.Add(new WaypointCourse(newCourseName, FlightGlobals.GetBodyIndex(FlightGlobals.currentMainBody), new Vector2d(spawnCoords.x, spawnCoords.y), new List<GameModes.Waypoints.Waypoint>()));
                        WaypointField.Save();
                        selected_index = WaypointCourses.CourseLocations.FindIndex(l => l.name == newCourseName);
                        newCourseName = "";
                        ShowNewCourse = false;
                        showCourseWPsComboBox = true;
                        //clear any previously loaded gates
                        foreach (var gate in loadedGates)
                        {
                            gate.disabled = true;
                            gate.gameObject.SetActive(false);
                        }
                        loadedGates.Clear();
                    }
                    if (GUI.Button(SQuarterRect(line, 3), StringUtils.Localize("Record"), BDArmorySetup.BDGuiSkin.button))
                    {
                        Vector3d spawnCoords = VectorUtils.GetWorldSurfacePostion(FlightGlobals.ActiveVessel.transform.position, FlightGlobals.currentMainBody);
                        if (!WaypointCourses.CourseLocations.Select(l => l.name).ToList().Contains(newCourseName))
                            WaypointCourses.CourseLocations.Add(new WaypointCourse(newCourseName, FlightGlobals.GetBodyIndex(FlightGlobals.currentMainBody), new Vector2d(spawnCoords.x, spawnCoords.y), new List<GameModes.Waypoints.Waypoint>()));
                        WaypointField.Save();
                        selected_index = WaypointCourses.CourseLocations.FindIndex(l => l.name == newCourseName);
                        newCourseName = "";
                        ShowNewCourse = false;
                        showCourseWPsComboBox = true;
                        recording = true;
                        //clear any previously loaded gates
                        foreach (var gate in loadedGates)
                        {
                            gate.disabled = true;
                            gate.gameObject.SetActive(false);
                        }
                        loadedGates.Clear();
                        StartCoroutine(RecordCourse());
                    }
                    GUI.Label(SQuarterRect(++line, 2), "Timestep (s)", leftLabel);
                    spawnFields["interval"].tryParseValue(GUI.TextField(SQuarterRect(line, 3), spawnFields["interval"].possibleValue, 8, spawnFields["interval"].style));
                    if (spawnFields["interval"].currentValue != recordingIncrement) recordingIncrement = (float)spawnFields["interval"].currentValue;
                }
            }

            if (showCourseWPsComboBox)
            {
                if (WaypointCourses.CourseLocations[selected_index].worldIndex != FlightGlobals.GetBodyIndex(FlightGlobals.currentMainBody)) return;
                line+= 1.25f;
                GUI.Label(SLineRect(line++), WaypointCourses.CourseLocations[selected_index].name, centreLabel);
                line+= 0.25f;
                if (recording)
                {
                    GUI.Label(SLineRect(line++), "Recording Course....", centreLabel);
                    line += 0.25f;
                    GUI.Label(SQuarterRect(++line, 1), "Timestep (s)", leftLabel);
                    spawnFields["interval"].tryParseValue(GUI.TextField(SQuarterRect(line, 2), spawnFields["interval"].possibleValue, 8, spawnFields["interval"].style));
                    if (spawnFields["interval"].currentValue != recordingIncrement) recordingIncrement = (float)spawnFields["interval"].currentValue;

                    line += 0.25f;
                    if (GUI.Button(SLineRect(++line), "Finish Recording", BDArmorySetup.BDGuiSkin.button))
                    {
                        recording = false;
                    }
                }
                int i = 0;
                foreach (var gate in WaypointCourses.CourseLocations[selected_index].waypoints)
                {
                    if (GUI.Button(SQuarterRect(line, i++), gate.name, i - 1 == selected_gate_index ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button))
                    {
                        selected_gate_index = i - 1;
                        Debug.Log($"selected gate index: {selected_gate_index}, ({WaypointCourses.CourseLocations[selected_index].waypoints[selected_gate_index].name})");

                        switch (Event.current.button)
                        {
                            case 1: // right click, remove gate from course
                                loadedGates[selected_gate_index].disabled = true;
                                loadedGates[selected_gate_index].gameObject.SetActive(false);
                                WaypointCourses.CourseLocations[selected_index].waypoints.Remove(gate);
                                if (selected_gate_index >= WaypointCourses.CourseLocations[selected_index].waypoints.Count) selected_index = WaypointCourses.CourseLocations[selected_index].waypoints.Count - 1;
                                WaypointField.Save();
                                break;
                            default:
                                //snap camera to selected gate
                                SnapCameraToGate();
                                //show gate position buttons/location textboxes
                                spawnFields["lat"].SetCurrentValue(gate.location.x);
                                spawnFields["lon"].SetCurrentValue(gate.location.y);
                                spawnFields["alt"].SetCurrentValue(gate.location.z);
                                spawnFields["diameter"].SetCurrentValue(gate.scale);
                                showPositioningControls = true;
                                break;
                        }
                    }
                }
                line += (int)((i - 1) / 4);
                line += 0.5f;
                if (!recording)
                {
                    txtName = GUI.TextField(SRightButtonRect(++line), txtName);
                    if (GUI.Button(SLeftButtonRect(line), StringUtils.Localize("Add Gate"), BDArmorySetup.BDGuiSkin.button))
                    {
                        string newName = string.IsNullOrEmpty(txtName.Trim()) ? ("Waypoint " + WaypointCourses.CourseLocations[selected_index].waypoints.Count.ToString()) : txtName.Trim();
                        AddGate(newName);
                     }
                }
            }
            else showPositioningControls = false;

            if (showPositioningControls)
            {
                //need to get these setup and configured - TODO
                var currGate = WaypointCourses.CourseLocations[selected_index].waypoints[selected_gate_index];
                if (loadedGates[selected_gate_index] != null)
                {
                    line += 0.5f;
                    rects = SRight3Rects(++line);
                    GUI.Label(rects[0], "lat.", centreLabel);
                    GUI.Label(rects[1], "lon.", centreLabel);
                    GUI.Label(rects[2], "alt.", centreLabel);
                    rects = SRight3Rects(++line);
                    if (GUI.Button(SFieldButtonRect(line, 1), "<", BDArmorySetup.BDGuiSkin.button))
                    {
                        spawnFields["lat"].SetCurrentValue(spawnFields["lat"].currentValue - (movementIncrement / 10)); //having lat/long increase by 1 per frame while the button is held is going to cause gates to go *flying* across the continent
                        if (spawnFields["lat"].currentValue < -90) spawnFields["lat"].SetCurrentValue(spawnFields["lat"].currentValue + 180);
                    }
                    spawnFields["lat"].tryParseValue(GUI.TextField(rects[0], spawnFields["lat"].possibleValue, 8, spawnFields["lat"].style));
                    if (GUI.Button(SFieldButtonRect(line, 7), ">", BDArmorySetup.BDGuiSkin.button))
                    {
                        spawnFields["lat"].SetCurrentValue(spawnFields["lat"].currentValue + (movementIncrement / 10));
                        if (spawnFields["lat"].currentValue > 90) spawnFields["lat"].SetCurrentValue(spawnFields["lat"].currentValue - 180);
                    }

                    if (GUI.Button(SFieldButtonRect(line, 8.5f), "<", BDArmorySetup.BDGuiSkin.button))
                    {
                        spawnFields["lon"].SetCurrentValue(spawnFields["lon"].currentValue - (movementIncrement / 10));
                        if (spawnFields["lon"].currentValue < -180) spawnFields["lon"].SetCurrentValue(spawnFields["lon"].currentValue + 360);
                    }
                    spawnFields["lon"].tryParseValue(GUI.TextField(rects[1], spawnFields["lon"].possibleValue, 8, spawnFields["lon"].style));
                    if (GUI.Button(SFieldButtonRect(line, 14.5f), ">", BDArmorySetup.BDGuiSkin.button))
                    {
                        spawnFields["lon"].SetCurrentValue(spawnFields["lon"].currentValue + (movementIncrement / 10));
                        if (spawnFields["lon"].currentValue > 180) spawnFields["lon"].SetCurrentValue(spawnFields["lon"].currentValue - 360);
                    }

                    if (GUI.Button(SFieldButtonRect(line, 16), "<", BDArmorySetup.BDGuiSkin.button))
                    {
                        spawnFields["alt"].SetCurrentValue(spawnFields["alt"].currentValue - movementIncrement);
                        if (spawnFields["alt"].currentValue < 0) spawnFields["alt"].SetCurrentValue(0);
                    }
                    spawnFields["alt"].tryParseValue(GUI.TextField(rects[2], spawnFields["alt"].possibleValue, 8, spawnFields["alt"].style));
                    if (GUI.Button(SFieldButtonRect(line, 22), ">", BDArmorySetup.BDGuiSkin.button))
                    {
                        spawnFields["alt"].SetCurrentValue(spawnFields["alt"].currentValue + movementIncrement);
                        if (spawnFields["alt"].currentValue > (FlightGlobals.currentMainBody.atmosphere ? FlightGlobals.currentMainBody.atmosphereDepth : 40000)) spawnFields["alt"].SetCurrentValue((FlightGlobals.currentMainBody.atmosphere ? FlightGlobals.currentMainBody.atmosphereDepth : 40000));
                    }

                    rects = SRight3Rects(++line);
                    GUI.Label(rects[0], "diameter", centreLabel);
                    GUI.Label(rects[1], "increment", centreLabel);
                    rects = SRight3Rects(++line);
                    if (GUI.Button(SFieldButtonRect(line, 1), "<", BDArmorySetup.BDGuiSkin.button))
                    {
                        spawnFields["diameter"].SetCurrentValue(spawnFields["diameter"].currentValue - movementIncrement);
                        if (spawnFields["diameter"].currentValue < 5) spawnFields["diameter"].SetCurrentValue(5);
                    }
                    spawnFields["diameter"].tryParseValue(GUI.TextField(rects[0], spawnFields["diameter"].possibleValue, 8, spawnFields["diameter"].style));
                    if (GUI.Button(SFieldButtonRect(line, 7), ">", BDArmorySetup.BDGuiSkin.button))
                    {
                        spawnFields["diameter"].SetCurrentValue(spawnFields["diameter"].currentValue + movementIncrement);
                        if (spawnFields["diameter"].currentValue > 1000) spawnFields["diameter"].SetCurrentValue(1000);
                    }

                    if (GUI.Button(SFieldButtonRect(line, 8.5f), "<", BDArmorySetup.BDGuiSkin.button))
                    {
                        if ((movementIncrement - 1) > 1)
                            spawnFields["increment"].SetCurrentValue(spawnFields["increment"].currentValue - 1);
                        else
                        {
                            if ((movementIncrement - 1) > 0.1)
                                spawnFields["increment"].SetCurrentValue(spawnFields["increment"].currentValue - 0.1); //there is almost certainly a more elegant way to do scaling, FIXME later
                            else
                                spawnFields["increment"].SetCurrentValue(spawnFields["increment"].currentValue - 0.01);
                        }
                        if (spawnFields["increment"].currentValue < 0.001f) spawnFields["increment"].SetCurrentValue(0.001f);
                    }
                    spawnFields["increment"].tryParseValue(GUI.TextField(rects[1], spawnFields["increment"].possibleValue, 8, spawnFields["increment"].style));
                    if (GUI.Button(SFieldButtonRect(line, 14.5f), ">", BDArmorySetup.BDGuiSkin.button))
                    {
                        spawnFields["increment"].SetCurrentValue(spawnFields["increment"].currentValue + 1);
                        if (spawnFields["increment"].currentValue > 1000) spawnFields["increment"].SetCurrentValue(1000);
                    }
                    movementIncrement = spawnFields["increment"].currentValue;
                    if (spawnFields["lat"].currentValue != currGate.location.x ||
                    spawnFields["lon"].currentValue != currGate.location.y ||
                    spawnFields["alt"].currentValue != currGate.location.z ||
                    spawnFields["diameter"].currentValue != currGate.scale)
                    {
                        currGate.location = new Vector3d(spawnFields["lat"].currentValue, spawnFields["lon"].currentValue, spawnFields["alt"].currentValue);
                        currGate.scale = (float)spawnFields["diameter"].currentValue;
                        WaypointField.Save(); //instead have a separate button and do saving manually?
                        loadedGates[selected_gate_index].UpdateWaypoint(currGate, selected_gate_index, WaypointCourses.CourseLocations[selected_index].waypoints);
                        SnapCameraToGate();
                    }
                }
                line += 0.3f;
            }

            line += 1.25f; // Bottom internal margin
            _windowHeight = (line * _lineHeight);
        }

        public void SetVisible(bool visible)
        {
            BDArmorySetup.showWPBuilderGUI = visible;
            GUIUtils.SetGUIRectVisible(_guiCheckIndex, visible);
        }
        void AddGate(string newName)
        {
            Vector3d gateCoords;
            if (!recording) FlightGlobals.currentMainBody.GetLatLonAlt(FlightCamera.fetch.transform.position + FlightCamera.fetch.Distance * FlightCamera.fetch.mainCamera.transform.forward, out gateCoords.x, out gateCoords.y, out gateCoords.z);
            else FlightGlobals.currentMainBody.GetLatLonAlt(FlightGlobals.ActiveVessel.CoM, out gateCoords.x, out gateCoords.y, out gateCoords.z);
            WaypointCourses.CourseLocations[selected_index].waypoints.Add(new Waypoint(newName, gateCoords, 500));
            WaypointField.Save();

            int wp = WaypointCourses.CourseLocations[selected_index].waypoints.Count - 1;
            float terrainAltitude = (float)FlightGlobals.currentMainBody.TerrainAltitude(WaypointCourses.CourseLocations[selected_index].waypoints[wp].location.x, WaypointCourses.CourseLocations[selected_index].waypoints[wp].location.y);
            Vector3d WorldCoords = VectorUtils.GetWorldSurfacePostion(new Vector3(WaypointCourses.CourseLocations[selected_index].waypoints[wp].location.x, WaypointCourses.CourseLocations[selected_index].waypoints[wp].location.y, (BDArmorySettings.WAYPOINTS_ALTITUDE == 0 ? WaypointCourses.CourseLocations[selected_index].waypoints[wp].location.z : BDArmorySettings.WAYPOINTS_ALTITUDE) + terrainAltitude), FlightGlobals.currentMainBody);
            Vector3d previousLocation = FlightGlobals.ActiveVessel.transform.position;
            if (wp > 0) previousLocation = VectorUtils.GetWorldSurfacePostion(new Vector3(WaypointCourses.CourseLocations[selected_index].waypoints[wp - 1].location.x, WaypointCourses.CourseLocations[selected_index].waypoints[wp - 1].location.y, (BDArmorySettings.WAYPOINTS_ALTITUDE == 0 ? WaypointCourses.CourseLocations[selected_index].waypoints[wp - 1].location.z : BDArmorySettings.WAYPOINTS_ALTITUDE) + terrainAltitude), FlightGlobals.currentMainBody);

            var direction = (WorldCoords - previousLocation).normalized;
            WayPointMarker.CreateWaypoint(WorldCoords, direction, "BDArmory/Models/WayPoint/Ring", WaypointCourses.CourseLocations[selected_index].waypoints[wp].scale);

            var location = string.Format("({0:##.###}, {1:##.###}, {2:####}", WaypointCourses.CourseLocations[selected_index].waypoints[wp].location.x, WaypointCourses.CourseLocations[selected_index].waypoints[wp].location.y, WaypointCourses.CourseLocations[selected_index].waypoints[wp].location.z);
            Debug.Log("[BDArmory.Waypoints]: Creating waypoint marker at  " + " " + location + " World: " + FlightGlobals.currentMainBody.flightGlobalsIndex + " scale: " + WaypointCourses.CourseLocations[selected_index].waypoints[wp].scale);
            if (!recording)
            {
                selected_gate_index = wp;
                SnapCameraToGate(); //this will need to change if adding ability to insert gates into middle of course, not at end

                var newGate = WaypointCourses.CourseLocations[selected_index].waypoints[wp];
                spawnFields["lat"].SetCurrentValue(newGate.location.x);
                spawnFields["lon"].SetCurrentValue(newGate.location.y);
                spawnFields["alt"].SetCurrentValue(newGate.location.z);
                spawnFields["diameter"].SetCurrentValue(500);
            }
        }
    }
}
