using System.Collections.Generic;
using PersonalLogistics.Util;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

namespace PersonalLogistics.Scripts
{
    public static class SpriteSheetManager
    {
        private const int IconHeight = 90;
        public static uint[] spriteIndex;
        public static TMP_SpriteAsset iconsSpriteAsset;

        public static void Create(IconSet set, TMP_Text inboundItemStatus)
        {
            spriteIndex = new uint[60000];

            iconsSpriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            iconsSpriteAsset.version = "1.1.0";
            iconsSpriteAsset.hashCode = TMP_TextUtilities.GetSimpleHashCode(iconsSpriteAsset.name);
            iconsSpriteAsset.spriteSheet = set.texture;

            List<TMP_SpriteGlyph> spriteGlyphTable = new List<TMP_SpriteGlyph>();
            List<TMP_SpriteCharacter> spriteCharacterTable = new List<TMP_SpriteCharacter>();

            PopulateSpriteTables(set, ref spriteCharacterTable, ref spriteGlyphTable);

            iconsSpriteAsset.spriteCharacterTable = spriteCharacterTable;
            iconsSpriteAsset.spriteGlyphTable = spriteGlyphTable;

            // Add new default material for sprite asset.
            AddDefaultMaterial(iconsSpriteAsset);
            inboundItemStatus.spriteAsset.fallbackSpriteAssets = new List<TMP_SpriteAsset> { iconsSpriteAsset };
        }

        private static void PopulateSpriteTables(IconSet iconSet, ref List<TMP_SpriteCharacter> spriteCharacterTable,
            ref List<TMP_SpriteGlyph> spriteGlyphTable)
        {
            uint lastSpriteIndex = 0;

            foreach (ItemProto item in LDB.items.dataArray)
            {
                if (item.ID <= 0 || item.ID >= iconSet.itemIconIndex.Length) continue;

                uint spriteIndex = iconSet.itemIconIndex[item.ID];
                string spriteName = ItemUtil.GetItemName(item.ID);

                AddSprite(ref spriteCharacterTable, ref spriteGlyphTable, spriteIndex, lastSpriteIndex, spriteName);
                SpriteSheetManager.spriteIndex[item.ID] = lastSpriteIndex;
                lastSpriteIndex++;
            }
        }

        private static void AddSprite(ref List<TMP_SpriteCharacter> spriteCharacterTable, ref List<TMP_SpriteGlyph> spriteGlyphTable, uint spriteIndex, uint i, string spriteName)
        {
            int x = (int)(spriteIndex % 25U);
            int y = (int)(spriteIndex / 25U);
            Rect rect = new Rect(x * IconHeight, y * IconHeight, IconHeight, IconHeight);

            TMP_SpriteGlyph spriteGlyph = new TMP_SpriteGlyph
            {
                index = i,
                metrics = new GlyphMetrics(rect.width, rect.height, 0, 70, rect.width),
                glyphRect = new GlyphRect(rect),
                scale = 1.0f
            };

            spriteGlyphTable.Add(spriteGlyph);


            TMP_SpriteCharacter spriteCharacter = new TMP_SpriteCharacter(0, spriteGlyph)
            {
                name = spriteName,
                scale = 1.0f
            };

            spriteCharacterTable.Add(spriteCharacter);
        }

        private static void AddDefaultMaterial(TMP_SpriteAsset spriteAsset)
        {
            Shader shader = Asset.bundle.LoadAsset<Shader>("Assets/TextMesh Pro/Resources/Shaders/TMP_Sprite.shader");
            Material material = new Material(shader);
            material.SetTexture(ShaderUtilities.ID_MainTex, spriteAsset.spriteSheet);

            spriteAsset.material = material;
            material.hideFlags = HideFlags.HideInHierarchy;
        }
    }
}