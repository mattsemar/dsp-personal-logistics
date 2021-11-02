using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PersonalLogistics.Util
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
                return _bundle != null;
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

        public static void UnloadAssetBundle()
        {
            if (_bundle != null)
                _bundle.Unload(true);
        }

        public static T LoadPrefab<T>(string path) where T : Object
        {
            var prefab = _bundle.LoadAsset<T>(path);

            return prefab;
        }
    }
}