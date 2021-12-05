using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PersonalLogistics.Util
{
    public class LoadFromFile
    {
        private static Dictionary<string, AssetBundle> _bundles = new Dictionary<string, AssetBundle>();
        private static Texture2D _logoTexture;
        private static Sprite _logoSprite;

        public static bool InitBundle(string key)
        {
            if (_bundles.ContainsKey(key))
                return true;
            var bundleLoadedResult = false;
            try
            {
                var path = FileUtil.GetBundleFilePath(key);
                var asset = AssetBundle.LoadFromFile(path);
                if (asset == null)
                    return false;
                _bundles.Add(key, asset);
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
            if (!InitBundle("pls"))
            {
                return null;
            }

            if (_logoTexture == null)
            {
                _logoTexture = _bundles["pls"].LoadAsset<Texture2D>("Assets/Textures/wlogob.png");
            }

            if (_logoTexture == null)
            {
                Log.Warn($"Did not find wlogob.png trying other options");
                _logoTexture = _bundles["pls"].LoadAsset<Texture2D>("wlogo");
            }

            if (_logoTexture == null)
            {
                return null;
            }

            _logoSprite = Sprite.Create(_logoTexture, new Rect(0f, 0, _logoTexture.width, _logoTexture.height),
                new Vector2(_logoTexture.width, _logoTexture.height));
            return _logoSprite;
        }

        public static void UnloadAssetBundle(string key)
        {
            if (_bundles.ContainsKey(key) && _bundles[key] != null)
                _bundles[key].Unload(true);
        }

        public static T LoadPrefab<T>(string key, string path) where T : Object
        {
            if (!InitBundle(key))
            {
                throw new Exception($"Failed to init bundle for key {key}");
            }
            var prefab = _bundles[key].LoadAsset<T>(path);

            return prefab;
        }
    }
}