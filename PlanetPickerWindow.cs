using System.Collections.Generic;
using UnityEngine;

namespace NetworkManager
{
    public class PlanetPickerWindow
    {
        public bool visible;
        private Rect _windowRect = new Rect(600f, 550f, 200f, 600f);

        private Texture2D _tooltipBg;

        public PlanetInfo ChosenPlanet;
        public bool PlanetPicked;

        private Pager<PlanetInfo> _pager;

        public PlanetPickerWindow(IEnumerable<PlanetInfo> planetInfos)
        {
            _pager = new Pager<PlanetInfo>(new List<PlanetInfo>(planetInfos));
        }

        public void OnClose()
        {
            visible = false;
        }

        public void OnGUI()
        {
            _windowRect = GUILayout.Window(1297895666, _windowRect, WindowFnWrapper, "Planet picker");
        }

        private void WindowFnWrapper(int id)
        {
            // var savedSkin = GUI.skin;
            // GUI.skin = ScriptableObject.CreateInstance<GUISkin>();
            StationSelectionWindow.SaveCurrentGuiOptions();
            WindowFn(id);
            GUI.DragWindow();
            // GUI.skin = savedSkin;
            StationSelectionWindow.RestoreGuiSkinOptions();
        }

        private void WindowFn(int id)
        {
            GUILayout.BeginArea(new Rect(_windowRect.width - 25f, 0f, 25f, 30f));
            if (GUILayout.Button("X"))
            {
                OnClose();
                return;
            }

            GUILayout.EndArea();
            GUILayout.BeginVertical(GUI.skin.box);
            {
                if (_pager != null)
                {
                    DrawPreviousButton();
                    DrawPage();
                    DrawNextButton();
                }
            }
            GUILayout.EndVertical();
            if (GUI.tooltip != null)
            {
                GUI.skin = null;
                var style = new GUIStyle
                {
                    normal = new GUIStyleState { textColor = Color.white },
                    wordWrap = true,
                    alignment = TextAnchor.MiddleCenter,
                    stretchHeight = true,
                    stretchWidth = true
                };

                var height = style.CalcHeight(new GUIContent(GUI.tooltip), _windowRect.width) + 10;
                var y = (int)(_windowRect.height - height * 1.25);
                GUI.Box(new Rect(0, y, _windowRect.width, height * 1.25f), GUI.tooltip, style);
            }
        }

        private void DrawPage()
        {
            var planets = _pager.GetPage();
            GUILayout.BeginVertical();
            foreach (var planet in planets)
            {
                GUILayout.BeginHorizontal();
                var pushed = GUILayout.Button($"{planet.Name}");
                if (pushed)
                {
                    ChosenPlanet = planet;
                    PlanetPicked = true;
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
        }

        private void DrawNextButton()
        {
            if (!_pager.HasNext())
                return;
            GUILayout.BeginHorizontal();
            var buttonPressed = GUILayout.Button("Next");
            if (buttonPressed)
            {
                _pager.Next();
            }

            GUILayout.EndHorizontal();
        }

        private void DrawPreviousButton()
        {
            if (_pager.IsFirst())
                return;
            GUILayout.BeginHorizontal();
            var buttonPressed = GUILayout.Button("Previous");
            if (buttonPressed)
            {
                _pager.Previous();
            }

            GUILayout.EndHorizontal();
        }

        public void SetItems(List<PlanetInfo> items)
        {
            _pager = new Pager<PlanetInfo>(items);
        }
    }
}