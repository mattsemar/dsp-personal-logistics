using System;
using PersonalLogistics.Shipping;
using PersonalLogistics.UI;
using PersonalLogistics.Util;
using UnityEngine;

namespace PersonalLogistics.UGUI
{
    public static class BufferStateWindow
    {
        private static Pager<InventoryItem> _pager;

        public static void WindowFunction(int id)
        {
            if (_pager == null)
            {
                _pager = new Pager<InventoryItem>(ShippingManager.GetInventoryItems(), 12);
            }
            else if (RequestWindow.bufferWindowDirty)
            {
                RequestWindow.bufferWindowDirty = false;
                _pager = new Pager<InventoryItem>(ShippingManager.GetInventoryItems(), 12);
            }

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
            {
                if (_pager == null || _pager.IsFirst() && _pager.IsEmpty())
                {
                    GUILayout.Label($"No items");
                }
                else if (_pager != null)
                {
                    DrawModeButton();
                    DrawCountLabel();
                    DrawPreviousButton();
                    var managedItems = _pager.GetPage();
                    foreach (var item in managedItems)
                    {
                        ItemProto itemProto = ItemUtil.GetItemProto(item.itemId);
                        var maxHeightSz = itemProto.iconSprite.rect.height / 2;
                        var maxHeight = GUILayout.MaxHeight(maxHeightSz);
                        GUILayout.BeginHorizontal(maxHeight);
                        var rect = GUILayoutUtility.GetRect(maxHeightSz, maxHeightSz);
                        GUI.Label(rect, new GUIContent(itemProto.iconSprite.texture, RequestWindow.GetItemIconTooltip(itemProto)));

                        DrawBufferedItemCount(item);
                        DrawAddToInventoryButton(item);
                        DrawReturnToLogisticsStationsButton(item);
                        GUILayout.EndHorizontal();
                    }

                    DrawNextButton();
                }
            }
            GUILayout.EndVertical();
            if (GUI.tooltip != null)
            {
                GUI.skin = null;
                var height = RequestWindow.toolTipStyle.CalcHeight(new GUIContent(GUI.tooltip), RequestWindow.windowRect.width) + 10;
                var rect = GUILayoutUtility.GetRect(RequestWindow.windowRect.width - 20, height * 1.25f);
                GUI.Box(rect, GUI.tooltip, RequestWindow.toolTipStyle);
            }

            RequestWindow.RestoreGuiSkinOptions();
        }


        private static void DrawModeButton()
        {
            var guiContent = new GUIContent("Requests", "Switch to showing requested/banned items");

            GUILayout.BeginVertical("Box");

            var clicked = GUILayout.Button(guiContent, GUILayout.ExpandWidth(false));

            if (clicked)
            {
                RequestWindow.mode = Mode.RequestWindow;
                RequestWindow.bufferWindowDirty = true;
            }

            GUILayout.EndVertical();
        }


        private static void DrawReturnToLogisticsStationsButton(InventoryItem item)
        {
            var guiContent = new GUIContent("Return to logistics", "Move buffered items back into logistics network");


            var clicked = GUILayout.Button(guiContent, GUILayout.ExpandWidth(false));

            if (clicked)
            {
                ShippingManager.Instance.MoveBufferedItemToLogisticsSystem(item);
                RequestWindow.bufferWindowDirty = true;
            }
        }

        private static void DrawAddToInventoryButton(InventoryItem item)
        {
            var guiContent = new GUIContent("Add to inventory", "Move buffered items into inventory");

            var clicked = GUILayout.Button(guiContent, GUILayout.ExpandWidth(false));

            if (clicked)
            {
                ShippingManager.Instance.MoveBufferedItemToInventory(item);
                RequestWindow.bufferWindowDirty = true;
            }
        }

        private static void DrawBufferedItemCount(InventoryItem item)
        {
            if (ShippingManager.Instance == null)
                return;
            GUILayout.Label(new GUIContent(item.count.ToString(), "Count in local buffer"));

            var lastUpdatedSeconds = item.AgeInSeconds;
            GUILayout.Label(new GUIContent($"Updated {lastUpdatedSeconds} seconds ago", "Number of game seconds since last state change for item"));
        }

        private static void DrawCountLabel()
        {
            GUILayout.BeginHorizontal();
            var (startIndex, endIndex) = _pager.GetIndexes();
            GUILayout.Label($"Items {startIndex}-{endIndex} of {_pager.Count}");

            GUILayout.EndHorizontal();
        }

        private static void DrawNextButton()
        {
            if (!_pager.HasNext())
                return;
            GUILayout.BeginHorizontal();
            var buttonPressed = GUILayout.Button(new GUIContent("Next", "Load next page of items"));

            if (buttonPressed)
            {
                _pager.Next();
            }

            GUILayout.EndHorizontal();
        }

        private static void DrawPreviousButton()
        {
            var disabled = _pager.IsFirst();
            if (disabled)
            {
                DrawNextButton();
                return;
            }

            GUILayout.BeginHorizontal();

            var buttonPressed = GUILayout.Button(new GUIContent("Previous", "Load previous page"));
            if (buttonPressed)
            {
                if (!_pager.IsFirst())
                    _pager.Previous();
            }


            GUILayout.EndHorizontal();
        }
    }
}