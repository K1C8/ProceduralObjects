﻿// The following code is borrowed from Traffic Manager President Edition mod
// https://github.com/CitiesSkylinesMods/TMPE/blob/02e8fe2a9152c52413b1391245c88b835dbf81ce/TLM/TLM/UI/Helpers/ExtUITabStrip.cs
// itself borrowed from RushHour mod and Advanced toolbar mod
// https://github.com/PropaneDragon/RushHour/blob/release/RushHour/Options/OptionHandler.cs
// https://github.com/CWMlolzlz/CS-AdvancedToolbar/blob/master/Source/ExpandableToolbar.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using ColossalFramework.UI;

namespace ProceduralObjects.UI
{
    public sealed class ExtUITabstrip : UITabstrip
    {

        public const float V_SCROLLBAR_WIDTH = 16f;
        public const float TAB_STRIP_HEIGHT = 40f;

        private UIScrollbar CreateVerticalScrollbar(UIPanel panel, UIScrollablePanel scrollablePanel)
        {
            UIScrollbar verticalScrollbar = panel.AddUIComponent<UIScrollbar>();
            verticalScrollbar.name = "VerticalScrollbar";
            verticalScrollbar.width = V_SCROLLBAR_WIDTH;
            verticalScrollbar.height = tabPages.height;
            verticalScrollbar.orientation = UIOrientation.Vertical;
            verticalScrollbar.pivot = UIPivotPoint.TopLeft;
            verticalScrollbar.AlignTo(panel, UIAlignAnchor.TopRight);
            verticalScrollbar.minValue = 0;
            verticalScrollbar.value = 0;
            verticalScrollbar.incrementAmount = 50;
            verticalScrollbar.autoHide = true;

            UISlicedSprite trackSprite = verticalScrollbar.AddUIComponent<UISlicedSprite>();
            trackSprite.relativePosition = Vector2.zero;
            trackSprite.autoSize = true;
            trackSprite.size = trackSprite.parent.size;
            trackSprite.fillDirection = UIFillDirection.Vertical;
            trackSprite.spriteName = "ScrollbarTrack";
            verticalScrollbar.trackObject = trackSprite;

            UISlicedSprite thumbSprite = trackSprite.AddUIComponent<UISlicedSprite>();
            thumbSprite.relativePosition = Vector2.zero;
            thumbSprite.fillDirection = UIFillDirection.Vertical;
            thumbSprite.autoSize = true;
            thumbSprite.width = thumbSprite.parent.width;
            thumbSprite.spriteName = "ScrollbarThumb";
            verticalScrollbar.thumbObject = thumbSprite;

            verticalScrollbar.eventValueChanged += (component, value) =>
            {
                scrollablePanel.scrollPosition = new Vector2(0, value);
            };

            panel.eventMouseWheel += (component, eventParam) =>
            {
                verticalScrollbar.value -= (int)eventParam.wheelDelta * verticalScrollbar.incrementAmount;
            };

            scrollablePanel.eventMouseWheel += (component, eventParam) =>
            {
                verticalScrollbar.value -= (int)eventParam.wheelDelta * verticalScrollbar.incrementAmount;
            };

            scrollablePanel.verticalScrollbar = verticalScrollbar;

            return verticalScrollbar;
        }

        private UIScrollablePanel CreateScrollablePanel(UIPanel panel)
        {
            panel.autoLayout = true;
            panel.autoLayoutDirection = LayoutDirection.Horizontal;

            UIScrollablePanel scrollablePanel = panel.AddUIComponent<UIScrollablePanel>();
            scrollablePanel.autoLayout = true;
            scrollablePanel.autoLayoutPadding = new RectOffset(10, 10, 0, 16);
            scrollablePanel.autoLayoutStart = LayoutStart.TopLeft;
            scrollablePanel.wrapLayout = true;
            scrollablePanel.size = new Vector2(panel.size.x - V_SCROLLBAR_WIDTH, panel.size.y);
            scrollablePanel.autoLayoutDirection = LayoutDirection.Horizontal; //Vertical does not work but why?

            UIScrollbar verticalScrollbar = CreateVerticalScrollbar(panel, scrollablePanel);
            verticalScrollbar.Show();
            verticalScrollbar.Invalidate();
            scrollablePanel.Invalidate();

            return scrollablePanel;
        }

        public UIHelper AddTabPage(string name, out UIButton tabButton, bool scrollBars = true)
        {
            tabButton = base.AddTab(name);
            tabButton.normalBgSprite = "SubBarButtonBase";
            tabButton.disabledBgSprite = "SubBarButtonBaseDisabled";
            tabButton.focusedBgSprite = "SubBarButtonBaseFocused";
            tabButton.hoveredBgSprite = "SubBarButtonBaseHovered";
            tabButton.pressedBgSprite = "SubBarButtonBasePressed";
            tabButton.textPadding = new RectOffset(10, 10, 10, 6);
            tabButton.autoSize = true;
            tabButton.atlas = Ingame;

            selectedIndex = tabCount - 1;
            UIPanel currentPanel = tabContainer.components[selectedIndex] as UIPanel;
            currentPanel.autoLayout = true;

            UIHelper panelHelper;
            if (scrollBars)
            {
                UIScrollablePanel scrollablePanel = CreateScrollablePanel(currentPanel);
                panelHelper = new UIHelper(scrollablePanel);
            }
            else
            {
                currentPanel.autoLayoutDirection = LayoutDirection.Vertical;
                panelHelper = new UIHelper(currentPanel);
            }
            return panelHelper;
        }

        public static ExtUITabstrip Create(UIHelper helper)
        {
            UIComponent optionsContainer = helper.self as UIComponent;
            float orgOptsContainerWidth = optionsContainer.height;
            float orgOptsContainerHeight = optionsContainer.width;

            int paddingRight = 10; //Options container is Scrollable panel itself(reserves space for scroll - which we don't use)
            optionsContainer.size = new Vector2(orgOptsContainerWidth + paddingRight, orgOptsContainerHeight);

            ExtUITabstrip tabStrip = optionsContainer.AddUIComponent<ExtUITabstrip>();
            tabStrip.relativePosition = new Vector3(0, 0);
            tabStrip.size = new Vector2(orgOptsContainerWidth, TAB_STRIP_HEIGHT);

            UITabContainer tabContainer = optionsContainer.AddUIComponent<UITabContainer>();
            tabContainer.relativePosition = new Vector3(0, TAB_STRIP_HEIGHT);
            tabContainer.width = (orgOptsContainerWidth + paddingRight) - V_SCROLLBAR_WIDTH;
            tabContainer.height = optionsContainer.height - (tabStrip.relativePosition.y + tabContainer.relativePosition.y);
            tabStrip.tabPages = tabContainer;

            return tabStrip;
        }

        public static UITextureAtlas FindAtlasOrNull(string name)
        {
            UITextureAtlas[] atlases = Resources.FindObjectsOfTypeAll<UITextureAtlas>();
            for (int i = 0; i < atlases.Length; i++)
            {
                if (atlases[i].name == name)
                {
                    return atlases[i];
                }
            }

            return null;
        }
        
        public static UITextureAtlas Ingame 
        {
            get {
                var a = FindAtlasOrNull("Ingame");
                return a == null ? UIView.GetAView().defaultAtlas : a;
            }
        }
    }
}
