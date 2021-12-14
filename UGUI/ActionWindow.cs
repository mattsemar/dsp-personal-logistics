using PersonalLogistics.PlayerInventory;
using PersonalLogistics.Shipping;
using PersonalLogistics.Util;
using UnityEngine;

namespace PersonalLogistics.UGUI
{
    public static class ActionWindow
    {
        public static void WindowFunction(int id)
        {
            WindowFn();
            GUI.DragWindow();
        }

        private static void WindowFn()
        {
            RequestWindow.SaveCurrentGuiOptions();
            GUILayout.BeginArea(new Rect(RequestWindow.windowRect.width - 25f, 0f, 25f, 30f));

            if (GUILayout.Button("X"))
            {
                RequestWindow.OnClose();
                return;
            }

            GUILayout.EndArea();
            GUILayout.BeginVertical("Box");

            RequestWindow.DrawModeSelector();


            DrawRequestStateSection();
            DrawPluginActions();
            DrawInboundRequestActions();

            GUILayout.EndVertical();
            if (GUI.tooltip != null)
            {
                GUI.skin = null;
                var height = RequestWindow.toolTipStyle.CalcHeight(new GUIContent(GUI.tooltip), RequestWindow.windowRect.width) + 10;
                var rect = GUILayoutUtility.GetRect(RequestWindow.windowRect.width - 20, height * 1.25f);
                rect.y += 20;
                GUI.Box(rect, GUI.tooltip, RequestWindow.toolTipStyle);
            }

            RequestWindow.RestoreGuiSkinOptions();
        }

        private static void DrawPluginActions()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Plugin control"), GUI.skin.label, GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();
            DrawRefillBuffer();
            DrawClearBuffer();
            DrawPauseProcessing();
            GUILayout.EndHorizontal();
        }

        private static void DrawInboundRequestActions()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Inbound requests"), GUI.skin.label, GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();
            DrawCancelInboundRequests();
            GUILayout.EndHorizontal();
        }

        private static void DrawPauseProcessing()
        {
            var text = PluginConfig.inventoryManagementPaused.Value ? "Resume" : "Pause";
            var tip = PluginConfig.inventoryManagementPaused.Value ? "Resume personal logistics system" : "Pause personal logistics system";
            var guiContent = new GUIContent(text, tip);

            GUILayout.BeginVertical("Box");

            var currentlySelected = 0;
            var clicked = GUILayout.Button(guiContent, GUILayout.ExpandWidth(false));

            if (clicked)
            {
                PluginConfig.inventoryManagementPaused.Value = !PluginConfig.inventoryManagementPaused.Value;
            }

            GUILayout.EndVertical();
        }

        private static void DrawClearBuffer()
        {
            var text = "Clear Buffer";
            var tip = "Send all items in buffer back to Logistics Network stations";
            var guiContent = new GUIContent(text, tip);

            GUILayout.BeginVertical("Box");

            var clicked = GUILayout.Button(guiContent, GUILayout.ExpandWidth(false));

            if (clicked)
            {
                ShippingManager.Instance?.MoveAllBufferedItemsToLogisticsSystem();
                RequestWindow.bufferWindowDirty = true;
            }

            GUILayout.EndVertical();
        }

        private static void DrawRefillBuffer()
        {
            var text = "Refill Buffer";
            var tip =
                "Fetch items from logistics stations to ensure full buffer. Note that items being topped up will not show up in BufferStatus window until loading is complete";
            var guiContent = new GUIContent(text, tip);

            GUILayout.BeginVertical("Box");

            var clicked = GUILayout.Button(guiContent, GUILayout.ExpandWidth(false));

            if (clicked)
            {
                PersonalLogisticManager.FillBuffer();
                RequestWindow.bufferWindowDirty = true;
            }

            GUILayout.EndVertical();
        }

        private static void DrawCancelInboundRequests()
        {
            var text = "Cancel requests";
            var tip =
                "Cancel inbound items, sending items back to logistics network. Use this, if, for example, an item you've requested is now available on your local planet but you're still waiting for it to be fetched from a far away planet";
            var guiContent = new GUIContent(text, tip);

            GUILayout.BeginVertical("Box");

            var clicked = GUILayout.Button(guiContent, GUILayout.ExpandWidth(false));

            if (clicked)
            {
                var result = PersonalLogisticManager.Instance?.CancelInboundRequests();
                Log.LogAndPopupMessage($"Cancelled {result} inbound requests");
            }

            GUILayout.EndVertical();
        }

        private static void DrawRequestStateSection()
        {
            GUILayout.BeginHorizontal();
            var guiContent = new GUIContent("Desired inventory state", "Manage Requests/Bans");
            GUILayout.Label(guiContent, GUI.skin.label, GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();
            DrawSaveInventoryButton();
            DrawClearButton();
            GUILayout.EndHorizontal();
        }

        private static void DrawClearButton()
        {
            var guiContent = new GUIContent("Clear", "Clear all requests and bans");

            GUILayout.BeginVertical("Box");

            var currentlySelected = 0;
            var clicked = GUILayout.Button(guiContent, GUILayout.ExpandWidth(false));

            if (clicked)
            {
                InventoryManager.instance.Clear();
                RequestWindow.dirty = true;
            }

            GUILayout.EndVertical();
        }

        private static void DrawSaveInventoryButton()
        {
            var guiContent = new GUIContent("Copy Inventory", "Use current inventory amounts to set requested/banned items.");

            GUILayout.BeginVertical("Box");

            var currentlySelected = 0;
            var clicked = GUILayout.Button(guiContent, GUILayout.ExpandWidth(false));

            if (clicked)
            {
                InventoryManager.instance.SaveInventoryAsDesiredState();
                RequestWindow.dirty = true;
            }

            GUILayout.EndVertical();
        }
    }
}