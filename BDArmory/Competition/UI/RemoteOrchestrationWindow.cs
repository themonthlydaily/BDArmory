using System.Collections;
using UnityEngine;
using BDArmory.Core;
using KSP.Localization;

namespace BDArmory.UI
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class RemoteOrchestrationWindow : MonoBehaviour
    {
        public RemoteOrchestrationWindow Instance;

        private int _guiCheckIndex;

        private float _windowHeight; //auto adjusting

        private bool ready = false;

        private string status = "Offline";

        private void Awake()
        {
            if (Instance)
                Destroy(this);
            else
                Instance = this;
        }

        private void Start()
        {
            ready = false;
            StartCoroutine(WaitForSetup());
        }

        private void OnGUI()
        {
            if (!ready) return;
            if (!BDArmorySetup.GAME_UI_ENABLED) return;

            BDArmorySetup.WindowRectRemoteOrchestration = new Rect(
                BDArmorySetup.WindowRectRemoteOrchestration.x, 
                BDArmorySetup.WindowRectRemoteOrchestration.y, 
                BDArmorySettings.REMOTE_ORCHESTRATION_WINDOW_WIDTH,
                _windowHeight
                );
            BDArmorySetup.WindowRectRemoteOrchestration = GUI.Window(
                10283444, 
                BDArmorySetup.WindowRectRemoteOrchestration, 
                WindowRemoteOrchestration,
                Localizer.Format("#LOC_BDArmory_BDARemoteOrchestration_Title"),//"BDA Remote Orchestration"
                BDArmorySetup.BDGuiSkin.window
                );
            Misc.Misc.UpdateGUIRect(BDArmorySetup.WindowRectRemoteOrchestration, _guiCheckIndex);

        }

        private IEnumerator WaitForSetup()
        {
            while (BDArmorySetup.Instance == null)
            {
                yield return null;
            }
            ready = true;
        }

        private void WindowRemoteOrchestration(int id)
        {

        }
    }
}
