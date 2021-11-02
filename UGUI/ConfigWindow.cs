using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using BepInEx.Configuration;
using PersonalLogistics.Util;
using UnityEngine;

namespace PersonalLogistics.UGUI
{
    public static class ConfigWindow
    {
        private static Dictionary<string, int> previousSelections = new Dictionary<string, int>();

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
            var lastSection = "";

            foreach (var configDefinition in PluginConfig.configFile.Keys)
            {
                if (configDefinition.Section == "UIOnly" || configDefinition.Section == "Internal")
                {
                    continue;
                }

                var configEntry = PluginConfig.configFile[configDefinition];
                if (configEntry.Description.Tags.ToList().Contains("configEditOnly"))
                {
                    continue;
                }

                if (configDefinition.Section != lastSection)
                {
                    RequestWindow.DrawCenteredLabel($"{configDefinition.Section}");
                }

                lastSection = configDefinition.Section;

                DrawSetting(configEntry);
            }

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

        private static void DrawSetting(ConfigEntryBase configEntry)
        {
            GUILayout.BeginHorizontal();
            DrawSettingName(configEntry);
            var descriptionAdded = false;
            if (configEntry.SettingType.IsEnum)
            {
                descriptionAdded = AddPicker(configEntry, configEntry.SettingType, configEntry.BoxedValue);
            }

            if (!descriptionAdded && configEntry.SettingType == typeof(bool))
            {
                descriptionAdded = DrawBoolField(configEntry);
            }

            if (!descriptionAdded && configEntry.SettingType == typeof(int))
            {
                descriptionAdded = DrawRangeField(configEntry);
            }

            if (!descriptionAdded)
            {
                //something went wrong, default to text field
                GUILayout.Label(new GUIContent(configEntry.BoxedValue.ToString(), "Current setting"));
            }

            GUILayout.EndHorizontal();
        }

        private static bool DrawRangeField(ConfigEntryBase configEntry)
        {
            if (configEntry.Description.AcceptableValues.GetType() != typeof(AcceptableValueRange<int>))
                return false;

            GUILayout.BeginHorizontal();
            AcceptableValueRange<int> acceptableValues = (AcceptableValueRange<int>)configEntry.Description.AcceptableValues;
            var converted = (float)Convert.ToDouble(configEntry.BoxedValue, CultureInfo.InvariantCulture);
            var leftValue = (float)Convert.ToDouble(acceptableValues.MinValue, CultureInfo.InvariantCulture);
            var rightValue = (float)Convert.ToDouble(acceptableValues.MaxValue, CultureInfo.InvariantCulture);

            var result = GUILayout.HorizontalSlider(converted, leftValue, rightValue, GUILayout.MinWidth(200));
            if (Math.Abs(result - converted) > Mathf.Abs(rightValue - leftValue) / 1000)
            {
                var newValue = Convert.ChangeType(result, configEntry.SettingType, CultureInfo.InvariantCulture);
                configEntry.BoxedValue = newValue;
            }

            var strVal = configEntry.BoxedValue.ToString();
            var strResult = GUILayout.TextField(strVal, GUILayout.Width(50));

            GUILayout.EndHorizontal();
            if (strResult != strVal)
            {
                try
                {
                    var resultVal = (float)Convert.ToDouble(strResult, CultureInfo.InvariantCulture);
                    var clampedResultVal = Mathf.Clamp(resultVal, leftValue, rightValue);
                    configEntry.BoxedValue = (int)clampedResultVal;
                }
                catch (FormatException)
                {
                    // Ignore user typing in bad data
                }
            }

            return true;
        }

        private static bool DrawBoolField(ConfigEntryBase setting)
        {
            if (setting.SettingType != typeof(bool))
            {
                return false;
            }

            var boolVal = (bool)setting.BoxedValue;

            GUILayout.BeginHorizontal();
            var result = GUILayout.Toggle(boolVal, new GUIContent(boolVal ? "Enabled" : "Disabled", "Click to toggle"), GUI.skin.toggle,GUILayout.ExpandWidth(false));
            if (result != boolVal)
            {
                setting.BoxedValue = result;
            }

            GUILayout.EndHorizontal();
            return true;
        }

        private static void DrawSettingName(ConfigEntryBase setting)
        {
            var leftColumnWidth = Mathf.RoundToInt(RequestWindow.windowRect.width / 2.5f);
            var guiContent = new GUIContent(setting.Definition.Key, setting.Description.Description);
            GUILayout.Label(guiContent, GUI.skin.label ,GUILayout.ExpandWidth(true));
            // GUILayout.Label(guiContent, GUILayout.Width(leftColumnWidth), GUILayout.MaxWidth(leftColumnWidth));
            GUILayout.FlexibleSpace();
        }

        private static bool AddPicker(ConfigEntryBase entry, Type enumType, object currentValue)
        {
            if (!enumType.IsEnum)
            {
                Debug.LogWarning($"picker must only be used with enums");
                return false;
            }

            var names = Enum.GetNames(enumType);
            var selectedName = Enum.GetName(enumType, currentValue);
            var index = -1;
            for (var i = 0; i < names.Length; i++)
            {
                var name = names[i];
                if (selectedName == name)
                {
                    index = i;
                }
            }

            if (index == -1)
            {
                Console.WriteLine($"did not find index of {currentValue} for {enumType}");
                return false;
            }

            var guiContents = names.Select(n => GetGuiContent(enumType, n, entry.Description.Description, selectedName == n));
            GUILayout.BeginVertical("Box");
            if (!previousSelections.ContainsKey(entry.Definition.Key))
            {
                previousSelections[entry.Definition.Key] = index;
            }

            var previousSelection = previousSelections[entry.Definition.Key];
            index = GUILayout.Toolbar(previousSelection, guiContents.ToArray());

            if (previousSelections[entry.Definition.Key] != index)
            {
                var updatedEnumValue = Enum.Parse(enumType, names[index], true);
                entry.BoxedValue = updatedEnumValue;
                previousSelections[entry.Definition.Key] = index;
            }

            GUILayout.EndVertical();
            return true;
        }
        
        private static GUIContent GetGuiContent(Type enumType, string sourceValue, string parentDescription, bool currentlySelected)
        {
            var enumMember = enumType.GetMember(sourceValue).FirstOrDefault();
            var attr = enumMember?.GetCustomAttributes(typeof(DescriptionAttribute), false).Cast<DescriptionAttribute>().FirstOrDefault();
            var currentlySelectedIndicator = currentlySelected ? "<b>(selected)</b> " : "";
            var label = currentlySelected ? $"<b>{sourceValue}</b>" : sourceValue;
            if (attr != null)
            {
                return new GUIContent(label, $"<b>{parentDescription}</b>\n\n{currentlySelectedIndicator}{attr.Description}");
            }

            return new GUIContent(label);
        }
    }
}