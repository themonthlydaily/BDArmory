using UnityEngine;

namespace BDArmory.UI
{
    public class BDGUIComboBox
    {
        private static bool forceToUnShow = false;
        private static int useControlID = -1;
        private bool isClickedComboButton = false;
        public bool isOpen { get { return isClickedComboButton; } }
        private int selectedItemIndex = -1;

        Rect buttonRect;
        Rect listRect;
        GUIContent buttonContent;
        GUIContent[] listContent;
        float maxHeight;
        GUIStyle listStyle;
        int columns;
        bool persistant;

        bool isClickedComboButton = false;
        bool isOpen = false;
        int selectedItemIndex = -1;
        Vector2 scrollViewVector;
        Rect scrollViewRect;
        Rect scrollViewInnerRect;
        Rect selectionGridRect;
        RectOffset selectionGridRectOffset = new RectOffset(3, 3, 3, 3);
        float listHeight;
        float vScrollWidth = BDArmorySetup.BDGuiSkin.verticalScrollbar.fixedWidth + BDArmorySetup.BDGuiSkin.verticalScrollbar.margin.left;

        /// <summary>
        /// A drop-down combo-box.
        /// </summary>
        /// <param name="buttonRect">The rect for the button.</param>
        /// <param name="listRect">The rect defining the position and width of the selection grid. The height will be adjusted according to the contents.</param>
        /// <param name="buttonContent">The button content.</param>
        /// <param name="listContent">The selection grid contents.</param>
        /// <param name="maxHeight">The maximum height of the grid before scrolling is enabled.</param>
        /// <param name="listStyle">The GUIStyle to use for the selection grid.</param>
        /// <param name="columns">The number of columns in the selection grid.</param>
        /// <param name="persistant">Does the box remain open after clicking a selection</param>
        public BDGUIComboBox(Rect buttonRect, Rect listRect, GUIContent buttonContent, GUIContent[] listContent, float maxHeight, GUIStyle listStyle, int columns = 2, bool persistant = false)
		{
            this.rect = rect;
            this.buttonRect = buttonRect;
            this.buttonContent = buttonContent;
            this.listContent = listContent;
            this.listStyle = listStyle;
            this.listStyle.active.textColor = Color.black;
            this.listStyle.hover.textColor = Color.black;
            this.maxHeight = maxHeight;
            this.columns = columns;
            this.persistant = persistant;
            UpdateContent(listContent);
        }

        public int Show()
        {
            bool done = false;
            int controlID = GUIUtility.GetControlID(FocusType.Passive);

            if (forceToUnShow && !thisTriggeredForceClose) // Close all other comboboxes
            { isClickedComboButton = false; }
            if (forceCloseNow)
            {
                forceToUnShow = false;
                forceCloseNow = false;
            }
            if (thisTriggeredForceClose)
            {
                thisTriggeredForceClose = false;
                forceCloseNow = true;
            }
            switch (Event.current.GetTypeForControl(controlID))
            {
                case EventType.MouseUp:
                    if (isClickedComboButton)
                    { done = true; }
                    break;
            }

            if (GUI.Button(buttonRect, buttonContent, BDArmorySetup.BDGuiSkin.button))
            {
                if (useControlID == -1)
                { useControlID = controlID; }
                if (useControlID != controlID)
                {
                    if (isClickedComboButton)
                    {
                        forceToUnShow = true;
                        thisTriggeredForceClose = true;
                    }
                    useControlID = controlID;
                }
                isClickedComboButton = true;
            }

            if (isClickedComboButton)
            {
                float items_height = listStyle.CalcHeight(listContent[0], 1.0f) * (listContent.Length + 5);
                Rect listRect = new Rect(rect.x + 5, rect.y + listStyle.CalcHeight(listContent[0], 1.0f), rect.width - 20f, items_height);

                scrollViewVector = GUI.BeginScrollView(new Rect(rect.x, rect.y + rect.height, rect.width + 10f, comboxbox_height), scrollViewVector,
                                                        new Rect(rect.x, rect.y, rect.width - 10, items_height + rect.height), false, false, BDArmorySetup.BDGuiSkin.horizontalScrollbar, BDArmorySetup.BDGuiSkin.verticalScrollbar);

                GUI.Box(new Rect(rect.x, rect.y, rect.width - 10, items_height + rect.height), "", BDArmorySetup.BDGuiSkin.window);

                if (selectedItemIndex != (selectedItemIndex = GUI.SelectionGrid(listRect, selectedItemIndex, listContent, 2, listStyle)))
                {
                    if (selectedItemIndex > -1) buttonContent.text = listContent[selectedItemIndex].text;
                    if (!persistant) isClickedComboButton = false;

                }

                GUI.EndScrollView();
            }

            if (done)
            { isClickedComboButton = false; }

            return selectedItemIndex;
        }

        public void UpdateRect(Rect r)
        {
            if (r == rect) return;
            buttonRect.x += r.x - rect.x;
            buttonRect.y += r.y - rect.y;
            rect = r;
        }

        public int SelectedItemIndex
        {
            get
            {
                return selectedItemIndex;
            }
            set
            {
                selectedItemIndex = value;
            }
        }
    }
}
