using System.Collections;
using BDArmory.Core;
using BDArmory.Misc;
using BDArmory.Modules;
using UnityEngine;
using KSP.Localization;
using System.Collections.Generic;
using BDArmory.Bullets;

namespace BDArmory.UI
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class BDAmmoSelector : MonoBehaviour
    {
        public static BDAmmoSelector Instance;
        private Rect clientRect;

        const float width = 350;
        const float margin = 5;
        const float buttonHeight = 20;

        private int guiCheckIndex;
        private bool open = false;
        private bool save = false;
        private Rect window;
        private float height = 100;

        private string beltString = "";

        private Vector2 windowLocation;
        private ModuleWeapon selectedWeapon;

        public string SelectedAmmoType; //presumably Aubranium can use this to filter allowed/banned ammotypes

        public List<string> AList = new List<string>();
        public List<string> ammoDesc = new List<string>();
        private BulletInfo bulletInfo;
        public string guiAmmoTypeString = Localizer.Format("#LOC_BDArmory_Ammo_Slug");

        public void Open(ModuleWeapon weapon, Vector2 position, string bullets)
        {
            Debug.Log("[AMMOSELECT]: opening ammo selection submenu");
            open = true;
            selectedWeapon = weapon;
            windowLocation = position;
            AList = BDAcTools.ParseNames(bullets);
            
            for (int a = 0; a < AList.Count; a++)
            {
                bulletInfo = BulletInfo.bullets[AList[a].ToString()];
                guiAmmoTypeString = "";
                if (bulletInfo.subProjectileCount > 1)
                {
                    guiAmmoTypeString = Localizer.Format("#LOC_BDArmory_Ammo_Shot") + " ";
                }
                if (bulletInfo.apBulletMod >= 1.1)
                {
                    guiAmmoTypeString += Localizer.Format("#LOC_BDArmory_Ammo_AP") + " ";
                }
                if (bulletInfo.apBulletMod < 1.1 && bulletInfo.apBulletMod > 0.8f)
                {
                    guiAmmoTypeString += Localizer.Format("#LOC_BDArmory_Ammo_SAP") + " ";
                }
                if (bulletInfo.tntMass > 0)
                {
                    if (bulletInfo.fuzeType.ToLower() != "none")
                    {
                        guiAmmoTypeString += Localizer.Format("#LOC_BDArmory_Ammo_Flak") + " ";
                    }
                    else
                    {
                        guiAmmoTypeString += Localizer.Format("#LOC_BDArmory_Ammo_Explosive") + " ";
                    }
                }
                if (bulletInfo.incendiary)
                {
                    guiAmmoTypeString += Localizer.Format("#LOC_BDArmory_Ammo_Incendiary") + " ";
                }
                else
                {
                    guiAmmoTypeString += Localizer.Format("#LOC_BDArmory_Ammo_Slug");
                }
                ammoDesc.Add(guiAmmoTypeString);
            }
           
        }
        protected virtual void OnGUI()
        {
            if (save)
            {
                Debug.Log("[AMMOSELECT]: beginning save");
                save = false;
                List<Part>.Enumerator craftPart = EditorLogic.fetch.ship.parts.GetEnumerator();
                while (craftPart.MoveNext())
                {
                    if (craftPart.Current == null) continue;
                    if (craftPart.Current.name != selectedWeapon.name) continue;
                    List<ModuleWeapon>.Enumerator weapon = craftPart.Current.FindModulesImplementing<ModuleWeapon>().GetEnumerator();
                    while (weapon.MoveNext())
                    {
                        if (weapon.Current == null) continue;
                        if (beltString != "") ;
                        {
                            weapon.Current.ammoBelt = beltString;
                            weapon.Current.useCustomBelt = true;
                        }
                    }
                    weapon.Dispose();
                }
                craftPart.Dispose();
                Debug.Log("[AMMOSELECT]: save complete");
            }
            if (!open) return;

            var clientRect = new Rect(
                Mathf.Min(windowLocation.x, Screen.width - width),
                Mathf.Min(windowLocation.y, Screen.height - height),
                width,
                height);
            window = GUI.Window(10591029, clientRect, AmmoSelectorWindow, "", BDArmorySetup.BDGuiSkin.window);
            Misc.Misc.UpdateGUIRect(window, guiCheckIndex);

        }
        private void AmmoSelectorWindow(int id)
        {
            GUI.DragWindow(new Rect(0, 0, width-18, 20));
            int ammocounter = 0;
            int labelLines = 1;
            float line = 0.5f;
            GUIStyle labelStyle = BDArmorySetup.BDGuiSkin.label;
            GUI.Label(new Rect(margin, line * buttonHeight, width - 2 * margin, buttonHeight), Localizer.Format("Ammo Belt Config"), labelStyle);
            if (GUI.Button(new Rect(width - 18, 2, 16, 16), "X"))
            {
                open = false;
            }
            line ++;
            if (ammocounter > 4)
            {
                ammocounter = 0;
                labelLines++;
                Debug.Log("[ammobelt] increasing line count");
            }
            GUI.Label(new Rect(margin, line * buttonHeight, width - 2 * margin, buttonHeight * labelLines), Localizer.Format("Current Belt:"), labelStyle);
            line++;
            GUI.Label(new Rect(margin, line * buttonHeight, width - 2 * margin, buttonHeight), beltString, labelStyle);
            line++;
            float ammolines = line + 0.1f;
            for (int i = 0; i < AList.Count; i++)
            {
                //Rect buttonRect = new Rect(margin * 2, ammolines * buttonHeight, (width - 4 * margin), buttonHeight);
                string ammoname = AList[i];
                if (GUI.Button(new Rect(margin * 2, ammolines * buttonHeight, (width - 4 * margin), buttonHeight), ammoname, BDArmorySetup.BDGuiSkin.button))
                {
                    beltString += ammoname;
                    beltString += "; ";
                    ammocounter++;
                }
                ammolines++;
                if (ammoDesc[i] != null)
                {
                    GUI.Label(new Rect(margin * 4, ammolines * buttonHeight, (width - 8 * margin), buttonHeight), ammoDesc[i], labelStyle);
                    ammolines += 1.1f;
                }
            }
            line++;
            if (GUI.Button(new Rect(margin * 5, (line + ammolines) * buttonHeight, (width - (10 * margin))/2, buttonHeight), "Save Belt"))
            {
                save = true;
                open = false;
            }
            if (GUI.Button(new Rect(((margin * 5) + ((width - (10 * margin)) / 2)), (line + ammolines) * buttonHeight, width - (10 * margin), buttonHeight), "Clear"))
            {
                beltString = "";
            }
            line += 2;
            height = (line + ammolines) * buttonHeight;
            BDGUIUtils.RepositionWindow(ref clientRect);
        }

        private void Start()
        {
            guiCheckIndex = Misc.Misc.RegisterGUIRect(new Rect());
        }
        private void Awake()
        {
            if (Instance)
                Destroy(Instance);
            Instance = this;
            clientRect = new Rect(Mathf.Min(windowLocation.x, Screen.width - width), Mathf.Min(windowLocation.y, Screen.height - height), width, height);
        }
    }
}
