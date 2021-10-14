using System;
using PersonalLogistics.Scripts;
using PersonalLogistics.Util;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PersonalLogistics
{
    public class LoadFromFile
    {
        private static AssetBundle _bundle;
        private static Texture2D _logoTexture;
        private static Sprite _logoSprite;

        public static bool InitBundle()
        {
            if (_bundle != null)
                return true;
            var bundleLoadedResult = false;
            // if (_bundle != null)
            try
            {
                var path = FileUtil.GetBundleFilePath();
                _bundle = AssetBundle.LoadFromFile(path);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"exception while loading bundle {e.Message}\n{e.StackTrace}");
                return false;
            }
        }


        public static Sprite LoadIconSprite()
        {
            if (_logoSprite != null)
                return _logoSprite;
            if (!InitBundle())
            {
                return null;
            }

            if (_logoTexture == null)
            {
                _logoTexture = _bundle.LoadAsset<Texture2D>("Assets/Textures/wlogob.png");
            }

            if (_logoTexture == null)
            {
                Log.Warn($"Did not find wlogob.png trying other options");
                _logoTexture = _bundle.LoadAsset<Texture2D>("wlogo");
            }

            if (_logoTexture == null)
            {
                return null;
            }

            _logoSprite = Sprite.Create(_logoTexture, new Rect(0f, 0, _logoTexture.width, _logoTexture.height),
                new Vector2(_logoTexture.width, _logoTexture.height));
            return _logoSprite;
        }

        public static InboundItems AddInboundItems(GameObject gameObject, RectTransform rectTransform)
        {
            Console.WriteLine($"tryna load IO itms");
            if (gameObject == null)
            {
                Console.WriteLine($"game object null");
                return null;
            }

            if (!InitBundle())
            {
                return null;
            }

            var prefab = _bundle.LoadAsset<GameObject>("Assets/prefab/Incoming.prefab");

            if (prefab == null)
            {
                Console.WriteLine($"inbound items rect not found");
                return null;
            }

            var ioAreaGo = Object.Instantiate(prefab, rectTransform.transform, false);
            if (ioAreaGo == null)
            {
                Console.WriteLine($"io area go null");
                return null;
            }

            var rt = ioAreaGo.GetComponent<RectTransform>();
            // var copiedRectTransform = ioAreaGo.GetComponent<RectTransform>();
            // copiedRectTransform.pivot = new Vector2(0, 0.5f);
            // copiedRectTransform.anchorMin = new Vector2(0, 1);
            // copiedRectTransform.anchorMax = new Vector2(0, 1);

            // copiedRectTransform.anchoredPosition = new Vector2(100, 100);
            // var originalRectTransform = r
            rt.anchorMin = rectTransform.anchorMin;
            rt.anchorMax = rectTransform.anchorMax;
            rt.sizeDelta = rectTransform.sizeDelta;
            Transform panelTrans = ioAreaGo.transform.Find("Panel");
            if (panelTrans == null)
            {
                Log.Debug($"Panel transform not found");
                return null;
            }

            var textTrans = panelTrans.Find("Text");
            if (textTrans == null)
            {
                Log.Debug($"textTrans transform not found");
                return null;
            }

            return textTrans.gameObject.AddComponent<InboundItems>();
        }

        public static void UnloadAssetBundle()
        {
            if (_bundle != null)
                _bundle.Unload(true);
        }

        public static GameObject LoadPrefab(string path)
        {
            var prefab = _bundle.LoadAsset<GameObject>(path);

            return prefab;
        }
    }
}