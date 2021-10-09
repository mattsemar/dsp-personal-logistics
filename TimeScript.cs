using System.Collections;
using System.Text;
using UnityEngine;

namespace PersonalLogistics
{
    public class TimeScript : MonoBehaviour
    {
        private StringBuilder timeText;
        private GUIStyle fontSize;
        private GUIStyle style;

        void Awake()
        {
            StartCoroutine(Loop());
            fontSize = new GUIStyle(GUI.skin.GetStyle("label"))
            {
                fontSize = 16
            };
            style = new GUIStyle
            {
                normal = new GUIStyleState { textColor = Color.white },
                wordWrap = false,
                // alignment = TextAnchor.UpperLeft,
                // stretchHeight = true,
                // stretchWidth = true
            };
        }

        public void OnGUI()
        {
            if (timeText == null || timeText.Length == 0)
            {
                return;
            }
            
            var height = style.CalcHeight(new GUIContent(this.timeText.ToString()), 600) + 10;
            var rect = GUILayoutUtility.GetRect(600, height * 1.25f);
            GUI.Label(new Rect(100, 100, rect.width, rect.height), this.timeText.ToString(), fontSize);
        }

        IEnumerator Loop()
        {
            while (true)
            {
                yield return new WaitForSeconds(2);
                var itemLoadStates = ItemLoadState.GetLoadState();
                if (itemLoadStates == null)
                {
                    continue;
                }
                if (itemLoadStates.Count > 0)
                {
                    timeText = new StringBuilder();
                    foreach (var loadState in itemLoadStates)
                    {
                        timeText.Append($"{loadState.itemName} arriving in {loadState.secondsRemaining + 5} seconds\r\n");
                    }
                }
                else
                {
                    timeText = null;
                }
            }
        }
    }
}