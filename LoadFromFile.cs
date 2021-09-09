using System;
using PersonalLogistics.Util;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PersonalLogistics
{
    public class LoadFromFile
    {
        private static AssetBundle _bundle;
        private static Texture2D _logoTexture;
        private static Sprite _logoSprite;

        private static bool InitBundle()
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

            // return bundleLoadedResult;
            // var sceneLoadedResult = false;
            // if (_sceneBundle != null)
            //     try
            //     {
            //         var path = FileUtil.GetBundleFilePath("ioareascene");
            //         _sceneBundle = AssetBundle.LoadFromFile(path);
            //         sceneLoadedResult = true;
            //     }
            //     catch (Exception e)
            //     {
            //         Console.WriteLine($"exception while loading scene bundle {e.Message}\n{e.StackTrace}");
            //         sceneLoadedResult = false;
            //     }
            //
            // return sceneLoadedResult && bundleLoadedResult;
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
                _logoTexture = _bundle.LoadAsset<Texture2D>("wlogo");
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

            var prefab = _bundle.LoadAsset<GameObject>("Assets/UI/Canvas.prefab");

            if (prefab == null)
            {
                Console.WriteLine($"inbound items rect not found");
                return null;
            }

            var ioAreaGo = Object.Instantiate(prefab, rectTransform.transform, true);
            var copiedRectTransform = ioAreaGo.GetComponent<RectTransform>();
            // copiedRectTransform.pivot = new Vector2(0, 0.5f);
            // copiedRectTransform.anchorMin = new Vector2(0, 1);
            // copiedRectTransform.anchorMax = new Vector2(0, 1);
            
            // copiedRectTransform.anchoredPosition = new Vector2(100, 100);
            // var originalRectTransform = r
            copiedRectTransform.anchorMin = rectTransform.anchorMin;
            copiedRectTransform.anchorMax = rectTransform.anchorMax;
            copiedRectTransform.sizeDelta = rectTransform.sizeDelta;
            
            if (ioAreaGo == null)
            {
                Console.WriteLine($"io area go");
                return null;
            }

            Console.WriteLine($"got a io area ${ioAreaGo.transform.position}");

            var addComponent = ioAreaGo.AddComponent<InboundItems>();
            if (addComponent != null && addComponent.incomingItemsText == null)
            {
                addComponent.incomingItemsText = addComponent.gameObject.AddComponent<Text>();
                addComponent.incomingItemsText.text = "setting to default";
            }
            Console.WriteLine($"add comp ${addComponent} ${addComponent?.incomingItemsText.text} ${addComponent?.enabled}");
            return addComponent;
        }

        // public static ProgressBar Load(int itemId)
        // {
        //     if (!InitBundle())
        //     {
        //         return null;
        //     }
        //
        //     var prefab = _bundle.LoadAsset<GameObject>("Bar");
        //     if (prefab == null)
        //     {
        //         Console.WriteLine($"prefab not loaded");
        //         return null;
        //     }
        //     var go = Object.Instantiate(prefab);
        //     if (go == null)
        //     {
        //         Console.WriteLine($"game object not loaded");
        //         return null;
        //     }
        //
        //     var progressBar = go.transform.GetComponentInChildren<ProgressBar>();
        //     if (progressBar == null)
        //     {
        //         Console.WriteLine($"bar not loaded");
        //         return null;
        //     }
        //     progressBar.itemId = itemId;
        //     progressBar.mask.fillAmount = 0;
        //     return progressBar;
        // }

        public static void UnloadAssetBundle()
        {
            // if (_bundle == null )
            // {
            //     return;
            // }

            if (_bundle != null)
                _bundle.Unload(true);
            // if (_sceneBundle != null)
            //     _sceneBundle.Unload(true);
        }
    }
}