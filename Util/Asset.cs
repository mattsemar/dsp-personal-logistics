using System.IO;
using System.Reflection;
using CommonAPI.Systems;
using UnityEngine;

namespace PersonalLogistics.Util
{
    public static class Asset
    {
        private static ResourceData resources;
        private static Texture2D _logoTexture;
        private static Sprite _logoSprite;

        public static AssetBundle bundle => resources.bundle;

        public static void Init(string pluginGuid, string key)
        {
            string pluginfolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            resources = new ResourceData(pluginGuid, key, pluginfolder);
            resources.LoadAssetBundle(key);
            ProtoRegistry.AddResource(resources);
        }


        public static Sprite LoadIconSprite()
        {
            if (_logoTexture == null)
            {
                _logoTexture = resources.bundle.LoadAsset<Texture2D>("Assets/Textures/blogob.png");
            }

            if (_logoTexture == null)
            {
                Log.Warn("Did not find blogob.png trying other options");
                _logoTexture = resources.bundle.LoadAsset<Texture2D>("wlogo");
            }

            if (_logoTexture == null)
            {
                return null;
            }

            _logoSprite = Sprite.Create(_logoTexture, new Rect(0f, 0, _logoTexture.width, _logoTexture.height),
                new Vector2(_logoTexture.width, _logoTexture.height));
            return _logoSprite;
        }
    }
}