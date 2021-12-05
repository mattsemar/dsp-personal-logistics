using UnityEngine;

namespace PersonalLogistics.UI
{
    public static class UiScaler
    {
        private static readonly Resolution defaultRes = new Resolution { width = 1920, height = 1080 };

        public static int ScaleToDefault(int input, bool horizontal = true)
        {
            if (Screen.currentResolution.Equals(defaultRes))
            {
                return input;
            }

            float ratio;
            if (horizontal)
            {
                ratio = (float)Screen.currentResolution.width / defaultRes.width;
            }
            else
            {
                ratio = (float)DSPGame.globalOption.uiLayoutHeight / defaultRes.height;
                return (int)(input * ratio);
            }

            return (int)(input * ratio);
        }

        public static Vector2 ScaleXYToDefault(Vector2 input) => new Vector2(ScaleToDefault((int)input.x), ScaleToDefault((int)input.y));

        public static Rect ScaleRectToDefault(float x, float y, float width, float height)
        {
            var xy = ScaleXYToDefault(new Vector2(x, y));
            var widHigh = ScaleXYToDefault(new Vector2(width, height));
            return new Rect(xy, widHigh);
        }
    }
}