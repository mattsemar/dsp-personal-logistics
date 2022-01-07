using System;
using System.IO;
using PersonalLogistics.Util;
using UnityEngine;

namespace PersonalLogistics.Model
{
    [Serializable]
    public class DesiredItem
    {
        private static readonly int VERSION = 1;
        public readonly int itemId;
        public int count;
        public int maxCount;
        public bool allowBuffering = true;
        public static DesiredItem bannedDesiredItem = new(0) { count = 0, maxCount = 0 };
        public static DesiredItem notRequestedDesiredItem = new(0) { count = 0, maxCount = Int32.MaxValue };
        private int stackSize;

        public DesiredItem(int newItemId)
        {
            itemId = newItemId;
            var itemProto = ItemUtil.GetItemProto(newItemId);
            if (itemProto != null)
                stackSize = itemProto.StackSize;
        }

        public bool IsBanned()
        {
            return maxCount == 0;
        }

        public bool IsNonRequested()
        {
            return count < 1;
        }

        public int RequestedStacks()
        {
            return stackSize > 0 ? Mathf.CeilToInt(count / (float)stackSize) : count;
        }

        public int RecycleMaxStacks()
        {
            if (!IsRecycle())
                return 300;
            return stackSize > 0 ? Mathf.CeilToInt(maxCount / (float)stackSize) : maxCount;
        }

        public bool IsRecycle()
        {
            if (maxCount < 0)
                return false;
            return stackSize > 0 ? maxCount / stackSize < 300 : maxCount < 1_000_000;
        }

        public bool IsNonManaged()
        {
            return IsNonRequested() && !IsRecycle();
        }

        public void Export(BinaryWriter binaryWriter)
        {
            binaryWriter.Write(VERSION);
            binaryWriter.Write(itemId);
            binaryWriter.Write(count);
            binaryWriter.Write(maxCount);
            binaryWriter.Write(allowBuffering);
        }

        public static DesiredItem Import(BinaryReader r)
        {
            var ver = r.ReadInt32();
            if (ver != VERSION)
            {
                Log.Debug($"reading an older version of desired item: {ver}, {VERSION}");
            }

            return new DesiredItem(r.ReadInt32())
            {
                count = r.ReadInt32(),
                maxCount = r.ReadInt32(),
                allowBuffering = r.ReadBoolean()
            };
        }

        public override string ToString() => $"DesiredItem: count={count}, max={maxCount}, stackSize={stackSize}";
    }
}