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

        private GUIContent[] courseGUI;
        private GUIContent courseText;
        private BDGUIComboBox courseBox;
        private GUIContent[] gateGUI;
        private GUIContent gateText;
        private BDGUIComboBox gateBox;
        private int previous_index = 1;
        private bool wpCourseList = false;
        private bool wpGateList = false;
        int selected_index = 1;
        int selected_gate_index = 1;
        private int previous_gate_index = 1;
        public float SelectedGate = 0;
        public static string Gatepath;
        public string SelectedModel;
        double movementIncrement = 1;

        private bool ShowLoadMenu = false;
        private bool ShowNewCourse = false;
        private string newCourseName = "";
        private bool newNameEntered = false;
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

        List<Rect> SRight2Rects(float line)
        {
            var rectGap = _margin / 2;
            var rectWidth = ((_windowWidth - 2 * _margin) / 2 - 2 * rectGap) / 2;
            var rects = new List<Rect>();
            rects.Add(new Rect(_windowWidth / 2 + rectGap / 2, line * _lineHeight, rectWidth, _lineHeight));
            rects.Add(new Rect(_windowWidth / 2 + rectWidth + rectGap * 3 / 2, line * _lineHeight, rectWidth, _lineHeight));
            return rects;
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

            // Spawn fields
            spawnFields = new Dictionary<string, NumericInputField> {
                { "lat", gameObject.AddComponent<NumericInputField>().Initialise(0, FlightGlobals.currentMainBody.GetLatitudeAndLongitude(FlightGlobals.ActiveVessel.CoM).x, -90, 90) },
                { "lon", gameObject.AddComponent<NumericInputField>().Initialise(0, FlightGlobals.currentMainBody.GetLatitudeAndLongitude(FlightGlobals.ActiveVessel.CoM).y, -180, 180) },
                { "alt", gameObject.AddComponent<NumericInputField>().Initialise(0, BDArmorySettings.VESSEL_SPAWN_ALTITUDE) },
                { "increment", gameObject.AddComponent<NumericInputField>().Initialise(0.001f, movementIncrement) },
                { "diameter", gameObject.AddComponent<NumericInputField>().Initialise(0, BDArmorySettings.WAYPOINTS_SCALE) },
            };
            selected_index = FlightGlobals.currentMainBody != null ? FlightGlobals.currentMainBody.flightGlobalsIndex : 1;
        }

        private IEnumerator WaitForBdaSettings()
        {
            yield return new WaitUntil(() => BDArmorySetup.Instance is not null);

            BDArmorySetup.Instance.hasWPCourseSpawner = true;
            if (_guiCheckIndex < 0) _guiCheckIndex = GUIUtils.RegisterGUIRect(new Rect());
            _ready = true;
            SetVisible(BDArmorySetup.showWPBuilderGUI);
        }

        private void FillCourseList()
        {
            courseGUI = new GUIContent[WaypointCourses.CourseLocations.Count - 1];
            for (int i = 0; i < WaypointCourses.CourseLocations.Count - 1; i++)
            {
                GUIContent gui = new GUIContent(WaypointCourses.CourseLocations[i].name);
                courseGUI[i] = gui;
            }

            courseText = new GUIContent();
            //courseText.text = StringUtils.Localize("#LOC_BDArmory_Settings_Planet");//"Select Planet"
            courseText.text = "Select Course";
        }

        private void FillGateList()
        {
            gateGUI = new GUIContent[WaypointCourses.CourseLocations[BDArmorySettings.WAYPOINT_COURSE_INDEX].waypoints.Count -1];
            for (int i = 0; i < WaypointCourses.CourseLocations[BDArmorySettings.WAYPOINT_COURSE_INDEX].waypoints.Count - 1; i++)
            {
                GUIContent gui = new GUIContent(WaypointCourses.CourseLocations[BDArmorySettings.WAYPOINT_COURSE_INDEX].waypoints[i].name);
                gateGUI[i] = gui;
            }

            gateText = new GUIContent();
            gateText.text = "Select Gate";
        }

        void SnapCameraToGate(WayPointMarker gate)
        {
            var flightCamera = FlightCamera.fetch;
            var cameraHeading = FlightCamera.CamHdg;
            var cameraPitch = FlightCamera.CamPitch;
            var distance = flightCamera.Distance;
            if (FlightGlobals.ActiveVessel != null && FlightGlobals.ActiveVessel.PatchedConicsAttached) FlightGlobals.ActiveVessel.DetachPatchedConicsSolver();

            var terrainAltitude = FlightGlobals.currentMainBody.TerrainAltitude(gate.Position.x, gate.Position.y);
            var spawnPoint = FlightGlobals.currentMainBody.GetWorldSurfacePosition(gate.Position.x, gate.Position.y, terrainAltitude + gate.Position.z);
            FloatingOrigin.SetOffset(gate.Position); // This adjusts local coordinates, such that spawnPoint is (0,0,0).
            var radialUnitVector = -FlightGlobals.currentMainBody.transform.position.normalized;
            var cameraPosition = Vector3.RotateTowards(distance * radialUnitVector, Quaternion.AngleAxis(cameraHeading * Mathf.Rad2Deg, radialUnitVector) * -VectorUtils.GetNorthVector(gate.Position, FlightGlobals.currentMainBody), 70f * Mathf.Deg2Rad, 0);

            flightCamera.transform.parent = gate.transform;
            flightCamera.SetTarget(gate.transform);
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

        void ParseAllSpawnFieldsNow()
        {
            spawnFields["lat"].tryParseValueNow();
            spawnFields["lon"].tryParseValueNow();
            spawnFields["alt"].tryParseValueNow();
            spawnFields["increment"].tryParseValueNow();
            spawnFields["diameter"].tryParseValueNow();
            BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.x = spawnFields["lat"].currentValue;
            BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.y = spawnFields["lon"].currentValue;
            BDArmorySettings.VESSEL_SPAWN_WORLDINDEX = FlightGlobals.currentMainBody != null ? FlightGlobals.currentMainBody.flightGlobalsIndex : 1; //selected_index?
            BDArmorySettings.VESSEL_SPAWN_ALTITUDE = (float)spawnFields["alt"].currentValue;
            BDArmorySettings.WAYPOINTS_SCALE = (float)spawnFields["diameter"].currentValue;
            movementIncrement = spawnFields["increment"].currentValue;
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

            if (GUI.Button(SLeftButtonRect(++line), $"{StringUtils.Localize("#LOC_BDArmory_Settings_LoadCourse")}", ShowLoadMenu ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button))//Load Course
            {
                ShowLoadMenu = !ShowLoadMenu;
            }

            if (GUI.Button(SRightButtonRect(line), $"{StringUtils.Localize("#LOC_BDArmory_Settings_NewCourse")}", ShowNewCourse ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button))//Load Course
            {
                ShowNewCourse = !ShowNewCourse;
            }
            if (ShowLoadMenu)
            {
                line++;
                if (!wpCourseList)
                {
                    FillCourseList();
                    courseBox = new BDGUIComboBox(SLeftButtonRect(line), SLineRect(line), courseText, courseGUI, _lineHeight * 6, listStyle, 3);
                    wpCourseList = true;
                }
                courseBox.UpdateRect(SLeftButtonRect(line));
                selected_index = courseBox.Show();

                if (courseBox.IsOpen)
                {
                    line += courseBox.Height / _lineHeight;
                }
                if (selected_index != previous_index)
                {
                    if (selected_index != -1)
                    {
                        //clear previously loaded gates/course
                        BDArmorySettings.WAYPOINT_COURSE_INDEX = selected_index;
                    }
                    previous_index = selected_index;
                }
                if (selected_index == -1)
                {
                    selected_index = 1;
                    previous_index = 1;
                }
                ++line;
                int i = 0;
                foreach (var wpCourse in WaypointCourses.CourseLocations)
                {
                    if (GUI.Button(SQuarterRect(line, i++), wpCourse.name, BDArmorySetup.BDGuiSkin.button))
                    {
                        switch (Event.current.button)
                        {
                            case 1: // right click
                                WaypointCourses.CourseLocations.Remove(wpCourse);
                                WaypointField.Save();
                                break;
                            default:
                                //if loading an off-world course, warp to world
                                if (WaypointCourses.CourseLocations[BDArmorySettings.WAYPOINT_COURSE_INDEX].worldIndex != FlightGlobals.GetBodyIndex(FlightGlobals.currentMainBody))
                                    SpawnUtils.ShowSpawnPoint(selected_index, wpCourse.waypoints[1].location.x, wpCourse.waypoints[1].location.y, wpCourse.waypoints[1].location.z);
                                Vector3 previousLocation = FlightGlobals.ActiveVessel.transform.position;
                                //spawn in course gates
                                loadedGates.Clear();
                                for (int wp = 0; wp < wpCourse.waypoints.Count; wp++)
                                {
                                    float terrainAltitude = (float)FlightGlobals.currentMainBody.TerrainAltitude(wpCourse.waypoints[wp].location.x, wpCourse.waypoints[wp].location.y);
                                    Vector3d WorldCoords = VectorUtils.GetWorldSurfacePostion(new Vector3(wpCourse.waypoints[wp].location.x, wpCourse.waypoints[wp].location.y, (BDArmorySettings.WAYPOINTS_ALTITUDE == 0 ? wpCourse.waypoints[wp].location.z : BDArmorySettings.WAYPOINTS_ALTITUDE) + terrainAltitude), FlightGlobals.currentMainBody);
                                    var direction = (WorldCoords - previousLocation).normalized;
                                    WayPointMarker.CreateWaypoint(WorldCoords, direction, "BDArmory/Models/WayPoint/model", wpCourse.waypoints[wp].scale);

                                    previousLocation = WorldCoords;
                                    var location = string.Format("({0:##.###}, {1:##.###}, {2:####}", wpCourse.waypoints[wp].location.x, wpCourse.waypoints[wp].location.y, wpCourse.waypoints[wp].location.z);
                                    Debug.Log("[BDArmory.Waypoints]: Creating waypoint marker at  " + " " + location + " World: " + FlightGlobals.currentMainBody.flightGlobalsIndex + " scale: " + wpCourse.waypoints[wp].scale);
                                }
                                ShowLoadMenu = false;
                                showCourseWPsComboBox = true;
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
                    newNameEntered = GUI.Toggle(SRightButtonRect(line), newNameEntered, StringUtils.Localize("Create"));
                    {
                        if (newNameEntered)
                        {
                            Vector3d spawnCoords = VectorUtils.GetWorldSurfacePostion(FlightGlobals.ActiveVessel.transform.position, FlightGlobals.currentMainBody);
                            if (!WaypointCourses.CourseLocations.Select(l => l.name).ToList().Contains(newCourseName))
                                WaypointCourses.CourseLocations.Add(new WaypointCourse("newCourseName", FlightGlobals.GetBodyIndex(FlightGlobals.currentMainBody), new Vector2d(spawnCoords.x, spawnCoords.y), new List<GameModes.Waypoints.Waypoint>()));
                            newCourseName = "";
                            newNameEntered = false;
                            showCourseWPsComboBox = true;
                            loadedGates.Clear();
                            WaypointField.Save();
                        }
                    }
                }
            }

            if (showCourseWPsComboBox)
            {
                line++;
                if (!wpGateList)
                {
                    FillGateList();
                    gateBox = new BDGUIComboBox(SLeftButtonRect(line), SLineRect(line), gateText, gateGUI, _lineHeight * 6, listStyle, 3, true);
                    wpGateList = true;
                }
                gateBox.UpdateRect(SLeftButtonRect(line));
                selected_gate_index = gateBox.Show();

                if (gateBox.IsOpen)
                {
                    line += gateBox.Height / _lineHeight;
                }
                if (selected_gate_index != previous_gate_index)
                {
                    previous_gate_index = selected_gate_index;
                }
                if (selected_gate_index == -1)
                {
                    selected_gate_index = 1;
                    previous_gate_index = 1;
                }
                ++line;
                int i = 0;
                foreach (var gate in WaypointCourses.CourseLocations[selected_index].waypoints)
                {
                    if (WaypointCourses.CourseLocations[BDArmorySettings.WAYPOINT_COURSE_INDEX].worldIndex != FlightGlobals.GetBodyIndex(FlightGlobals.currentMainBody)) continue;
                    if (GUI.Button(SQuarterRect(line, i++), gate.name, BDArmorySetup.BDGuiSkin.button))
                    {
                        int wpmIndex = WaypointCourses.CourseLocations[BDArmorySettings.WAYPOINT_COURSE_INDEX].waypoints.IndexOf(gate);
                        switch (Event.current.button)
                        {
                            case 1: // right click, remove gate from course
                                loadedGates[wpmIndex].disabled = true;
                                loadedGates[wpmIndex].gameObject.SetActive(false);
                                WaypointCourses.CourseLocations[BDArmorySettings.WAYPOINT_COURSE_INDEX].waypoints.Remove(gate);
                                WaypointField.Save();
                                break;
                            default:
                                //snap camera to selected gate
                                SnapCameraToGate(loadedGates[wpmIndex]);
                                //show gate position buttons/location textboxes
                                showPositioningControls = true;

                                spawnFields["lat"].SetCurrentValue(gate.location.x);
                                spawnFields["lon"].SetCurrentValue(gate.location.y);
                                spawnFields["alt"].SetCurrentValue(gate.location.z);
                                spawnFields["scale"].SetCurrentValue(gate.scale);
                                break;
                        }
                    }
                }
                line += line / 2;
                txtName = GUI.TextField(SRightButtonRect(++line), txtName);
                if (GUI.Button(SLeftButtonRect(line), StringUtils.Localize("#Add Gate"), BDArmorySetup.BDGuiSkin.button))
                {
                    string newName = string.IsNullOrEmpty(txtName.Trim()) ? ("Waypoint " + WaypointCourses.CourseLocations[selected_index].waypoints.Count.ToString()) : txtName.Trim();

                    Vector3d gateCoords;
                    FlightGlobals.currentMainBody.GetLatLonAlt(FlightCamera.fetch.transform.position + FlightCamera.fetch.Distance * FlightCamera.fetch.mainCamera.transform.forward, out gateCoords.x, out gateCoords.y, out gateCoords.z);
                    WaypointCourses.CourseLocations[selected_index].waypoints.Add(new GameModes.Waypoints.Waypoint(newName, gateCoords, 500));
                    WaypointField.Save();

                    int wp = WaypointCourses.CourseLocations[selected_index].waypoints.Count - 1;
                    float terrainAltitude = (float)FlightGlobals.currentMainBody.TerrainAltitude(WaypointCourses.CourseLocations[selected_index].waypoints[wp].location.x, WaypointCourses.CourseLocations[selected_index].waypoints[wp].location.y);
                    Vector3d WorldCoords = VectorUtils.GetWorldSurfacePostion(new Vector3(WaypointCourses.CourseLocations[selected_index].waypoints[wp].location.x, WaypointCourses.CourseLocations[selected_index].waypoints[wp].location.y, (BDArmorySettings.WAYPOINTS_ALTITUDE == 0 ? WaypointCourses.CourseLocations[selected_index].waypoints[wp].location.z : BDArmorySettings.WAYPOINTS_ALTITUDE) + terrainAltitude), FlightGlobals.currentMainBody);
                    Vector3d previousLocation = FlightGlobals.ActiveVessel.transform.position;
                    if (wp > 0) previousLocation = VectorUtils.GetWorldSurfacePostion(new Vector3(WaypointCourses.CourseLocations[selected_index].waypoints[wp - 1].location.x, WaypointCourses.CourseLocations[selected_index].waypoints[wp - 1].location.y, (BDArmorySettings.WAYPOINTS_ALTITUDE == 0 ? WaypointCourses.CourseLocations[selected_index].waypoints[wp - 1].location.z : BDArmorySettings.WAYPOINTS_ALTITUDE) + terrainAltitude), FlightGlobals.currentMainBody);

                    var direction = (WorldCoords - previousLocation).normalized;
                    WayPointMarker.CreateWaypoint(WorldCoords, direction, "BDArmory/Models/WayPoint/model", WaypointCourses.CourseLocations[selected_index].waypoints[wp].scale);

                    var location = string.Format("({0:##.###}, {1:##.###}, {2:####}", WaypointCourses.CourseLocations[selected_index].waypoints[wp].location.x, WaypointCourses.CourseLocations[selected_index].waypoints[wp].location.y, WaypointCourses.CourseLocations[selected_index].waypoints[wp].location.z);
                    Debug.Log("[BDArmory.Waypoints]: Creating waypoint marker at  " + " " + location + " World: " + FlightGlobals.currentMainBody.flightGlobalsIndex + " scale: " + WaypointCourses.CourseLocations[selected_index].waypoints[wp].scale);
                    SnapCameraToGate(loadedGates[loadedGates.Count - 1]); //this will need to change if adding ability to insert gates into middle of course, not at end

                    var newGate = WaypointCourses.CourseLocations[selected_index].waypoints[WaypointCourses.CourseLocations[selected_index].waypoints.Count - 1];
                    selected_gate_index = loadedGates.Count - 1;
                    previous_gate_index = selected_gate_index;
                    spawnFields["lat"].SetCurrentValue(newGate.location.x);
                    spawnFields["lon"].SetCurrentValue(newGate.location.y);
                    spawnFields["alt"].SetCurrentValue(newGate.location.z);
                    spawnFields["scale"].SetCurrentValue(500);
                }

            }
            else showPositioningControls = false;

            if (showPositioningControls)
            {
                //need to get these setup and configured - TODO
                var currGate = WaypointCourses.CourseLocations[selected_index].waypoints[selected_gate_index];
                if (loadedGates[selected_gate_index] != null)
                {
                    rects = SRight3Rects(++line);
                    if (GUI.Button(SFieldButtonRect(line, 1), "<", BDArmorySetup.BDGuiSkin.button))
                    {
                        spawnFields["lat"].SetCurrentValue(spawnFields["lat"].currentValue - (movementIncrement / 100)); //having lat/long increase by 1 per frame while the button is held is going to cause gates to go *flying* across the continent
                        if (spawnFields["lat"].currentValue < -90) spawnFields["lat"].SetCurrentValue(spawnFields["lat"].currentValue + 180);
                    }
                    spawnFields["lat"].tryParseValue(GUI.TextField(rects[0], spawnFields["lat"].possibleValue, 8, spawnFields["lat"].style));
                    if (GUI.Button(SFieldButtonRect(line, 7), ">", BDArmorySetup.BDGuiSkin.button))
                    {
                        spawnFields["lat"].SetCurrentValue(spawnFields["lat"].currentValue + (movementIncrement / 100));
                        if (spawnFields["lat"].currentValue > 90) spawnFields["lat"].SetCurrentValue(spawnFields["lat"].currentValue - 180);
                    }

                    if (GUI.Button(SFieldButtonRect(line, 8.5f), "<", BDArmorySetup.BDGuiSkin.button))
                    {
                        spawnFields["lon"].SetCurrentValue(spawnFields["lon"].currentValue - (movementIncrement / 100));
                        if (spawnFields["lon"].currentValue < -180) spawnFields["lon"].SetCurrentValue(spawnFields["lon"].currentValue + 360);
                    }
                    spawnFields["lon"].tryParseValue(GUI.TextField(rects[1], spawnFields["lon"].possibleValue, 8, spawnFields["lon"].style));
                    if (GUI.Button(SFieldButtonRect(line, 14.5f), ">", BDArmorySetup.BDGuiSkin.button))
                    {
                        spawnFields["lon"].SetCurrentValue(spawnFields["lon"].currentValue + (movementIncrement / 100));
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

                    rects = SRight3Rects(line++);
                    if (GUI.Button(SFieldButtonRect(line, 1), "<", BDArmorySetup.BDGuiSkin.button))
                    {
                        spawnFields["diameter"].SetCurrentValue(spawnFields["diameter"].currentValue + movementIncrement);
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
                        if (movementIncrement - 1 > 1)
                            spawnFields["increment"].SetCurrentValue(spawnFields["increment"].currentValue - 1);
                        else
                        {
                            if (movementIncrement - 1 > 0.1)
                                spawnFields["increment"].SetCurrentValue(spawnFields["increment"].currentValue - 0.1); //there is almost certainly a more elegant way to do scaling, FIXME later
                            else
                                spawnFields["increment"].SetCurrentValue(spawnFields["increment"].currentValue - 0.01);
                        }
                        if (spawnFields["increment"].currentValue < 0.001f) spawnFields["increment"].SetCurrentValue(0.001f);
                    }
                    spawnFields["increment"].tryParseValue(GUI.TextField(rects[1], spawnFields["increment"].possibleValue, 8, spawnFields["increment"].style));
                    if (GUI.Button(SFieldButtonRect(line, 14.5f), ">", BDArmorySetup.BDGuiSkin.button))
                    {
                        spawnFields["increment"].SetCurrentValue(spawnFields["increment"].currentValue + movementIncrement);
                        if (spawnFields["increment"].currentValue > 180) spawnFields["increment"].SetCurrentValue(spawnFields["long"].currentValue - 360);
                    }

                    if (spawnFields["lat"].currentValue != currGate.location.x ||
                    spawnFields["lon"].currentValue != currGate.location.y ||
                    spawnFields["alt"].currentValue != currGate.location.z ||
                    spawnFields["diameter"].currentValue != currGate.scale)
                    {
                        currGate.location = new Vector3d(spawnFields["lat"].currentValue, spawnFields["lon"].currentValue, spawnFields["alt"].currentValue);
                        currGate.scale = (float)spawnFields["diameter"].currentValue;
                        loadedGates[selected_gate_index].UpdateWaypoint(currGate, selected_gate_index, WaypointCourses.CourseLocations[selected_index].waypoints);
                        WaypointField.Save(); //instead have a separate button and do saving manually?
                        SnapCameraToGate(loadedGates[selected_gate_index]);
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
            if (!visible) ParseAllSpawnFieldsNow();
        }
    }
}
