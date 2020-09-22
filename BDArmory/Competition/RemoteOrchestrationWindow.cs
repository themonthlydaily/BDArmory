using System.Collections;
using UnityEngine;
using BDArmory.Core;
using KSP.Localization;
using BDArmory.Competition;

namespace BDArmory.UI
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class RemoteOrchestrationWindow : MonoBehaviour
    {
        public static RemoteOrchestrationWindow Instance;
        private BDAScoreService service;
        private BDAScoreClient client;

        private int _guiCheckIndex;

        private readonly float _titleHeight = 30;
        private readonly float _margin = 5;

        private float _windowHeight; //auto adjusting

        private bool ready = false;

        private string status;
        private string competition;
        private string stage;
        private string heat;

        private void Awake()
        {
            if (Instance)
                Destroy(this);
            Instance = this;
        }

        private void Start()
        {
            ready = false;
            StartCoroutine(WaitForSetup());
        }

        private void Update()
        {
            if (!ready) return;

            competition = client.competition == null ? "-" : client.competition.name;
            status = client.competition == null ? "Offline" : service.Status();
            stage = client.activeHeat == null ? "-" : client.activeHeat.stage.ToString();
            heat = client.activeHeat == null ? "-" : client.activeHeat.order.ToString();
        }

        private void OnGUI()
        {
            if (!(ready && BDArmorySetup.GAME_UI_ENABLED && BDArmorySettings.REMOTE_LOGGING_ENABLED))
                return;

            SetNewHeight(_windowHeight);
            BDArmorySetup.WindowRectRemoteOrchestration = new Rect(
                BDArmorySetup.WindowRectRemoteOrchestration.x,
                BDArmorySetup.WindowRectRemoteOrchestration.y,
                BDArmorySettings.REMOTE_ORCHESTRATION_WINDOW_WIDTH,
                _windowHeight
            );
            BDArmorySetup.WindowRectRemoteOrchestration = GUI.Window(
                80085,
                BDArmorySetup.WindowRectRemoteOrchestration,
                WindowRemoteOrchestration,
                Localizer.Format("#LOC_BDArmory_BDARemoteOrchestration_Title"),//"BDA Remote Orchestration"
                BDArmorySetup.BDGuiSkin.window
            );
            Misc.Misc.UpdateGUIRect(BDArmorySetup.WindowRectRemoteOrchestration, _guiCheckIndex);
        }

        private void SetNewHeight(float windowHeight)
        {
            BDArmorySetup.WindowRectRemoteOrchestration.height = windowHeight;
        }

        private IEnumerator WaitForSetup()
        {
            while (BDArmorySetup.Instance == null || BDAScoreService.Instance == null || BDAScoreService.Instance.client == null)
            {
                yield return null;
            }
            service = BDAScoreService.Instance;
            client = BDAScoreService.Instance.client;
            ready = true;
            _guiCheckIndex = Misc.Misc.RegisterGUIRect(new Rect());
        }

        private void WindowRemoteOrchestration(int id)
        {
            GUI.DragWindow(new Rect(0, 0, BDArmorySettings.REMOTE_ORCHESTRATION_WINDOW_WIDTH, _titleHeight));

            float offset = _titleHeight + _margin;
            float width = BDArmorySettings.REMOTE_ORCHESTRATION_WINDOW_WIDTH;
            float half = width / 2.0f;
            GUI.Label(new Rect(_margin, offset, half, _titleHeight), "Competition: ");
            GUI.Label(new Rect(_margin + half, offset, half, _titleHeight), competition);
            offset += _titleHeight;
            GUI.Label(new Rect(_margin, offset, half, _titleHeight), "Status: ");
            string statusLine;
            switch (service.status)
            {
                case BDAScoreService.StatusType.Waiting:
                    statusLine = status + " " + (30 + service.retryFindStartedAt - Planetarium.GetUniversalTime()).ToString("0") + "s";
                    break;
                default:
                    statusLine = status;
                    break;
            }
            GUI.Label(new Rect(_margin + half, offset, half, _titleHeight), statusLine);
            offset += _titleHeight;
            GUI.Label(new Rect(_margin, offset, half, _titleHeight), "Stage: ");
            GUI.Label(new Rect(_margin + half, offset, half, _titleHeight), stage);
            offset += _titleHeight;
            GUI.Label(new Rect(_margin, offset, half, _titleHeight), "Heat: ");
            GUI.Label(new Rect(_margin + half, offset, half, _titleHeight), heat);
            offset += _titleHeight;
            string buttonText;
            switch (BDAScoreService.Instance.status)
            {
                case BDAScoreService.StatusType.Waiting:
                    buttonText = "Stop";
                    break;
                case BDAScoreService.StatusType.Stopped:
                case BDAScoreService.StatusType.Cancelled:
                    buttonText = "Next Heat";
                    break;
                default:
                    buttonText = "Cancel";
                    break;
            }
            if (GUI.Button(new Rect(_margin, offset, width - 2 * _margin, _titleHeight), buttonText, BDArmorySetup.BDGuiSkin.button))
            {
                switch (BDAScoreService.Instance.status)
                {
                    case BDAScoreService.StatusType.Stopped:
                    case BDAScoreService.StatusType.Cancelled:
                        service.Configure(service.vesselPath, BDArmorySettings.COMPETITION_HASH);
                        break;
                    default:
                        service.Cancel();
                        break;
                }
            }
            offset += _titleHeight + _margin;

            _windowHeight = offset;

            BDGUIUtils.RepositionWindow(ref BDArmorySetup.WindowRectRemoteOrchestration); // Prevent it from going off the screen edges.
        }


    }
}
