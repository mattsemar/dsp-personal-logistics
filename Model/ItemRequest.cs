using System;
using System.IO;
using PersonalLogistics.Util;

namespace PersonalLogistics.Model
{
    public enum RequestType
    {
        Load,   // get item from network
        Store   // add item to network
    }

    public enum RequestState
    {
        Created,
        WaitingForShipping,
        ReadyForInventoryUpdate,
        InventoryUpdated,
        Complete,
        Failed,
    }
    public class ItemRequest
    {
        private static readonly int VERSION = 1;
        public RequestType RequestType;
        public int ItemId;
        public int ItemCount;
        public RequestState State = RequestState.Created;
        public long CreatedTick = GameMain.gameTick;
        public DateTime ComputedCompletionTime;
        public long ComputedCompletionTick;
        public Guid guid = Guid.NewGuid();
        public string ItemName;
        public bool SkipBuffer;
        public bool bufferDebited;
        public bool fillBufferRequest;

        public override string ToString()
        {
            var completeTime = DateTime.Now + TimeSpan.FromSeconds((ComputedCompletionTick - GameMain.gameTick) / GameMain.tickPerSec);
            return $"{RequestType}, id={ItemId} name={ItemUtil.GetItemName(ItemId)}, count={ItemCount}, created={CreatedTick}, completion={completeTime}, state={Enum.GetName(typeof(RequestState), State)}";
        }
        
        public static ItemRequest Import(BinaryReader r)
        {
            var version = r.ReadInt32();
            if (version != VERSION)
            {
                Log.Warn($"version mismatch on ItemRequest {VERSION} vs {version}");
            }

            Enum.TryParse(r.ReadString(), true, out RequestType requestType);
            var result = new ItemRequest
            {
                RequestType = requestType,
                ItemId = r.ReadInt32(),
                ItemCount = r.ReadInt32()
            };
            RequestState requestState;
            Enum.TryParse(r.ReadString(), true, out requestState);
            result.State = requestState;
            
            result.CreatedTick = r.ReadInt64();
            result.ComputedCompletionTick = r.ReadInt64();
            result.guid = new Guid(r.ReadString());
            result.ItemName = ItemUtil.GetItemName(result.ItemId);
            result.bufferDebited = r.ReadBoolean();
            result.fillBufferRequest = r.ReadBoolean();
            

            return result;
        }

        public void Export(BinaryWriter binaryWriter)
        {
            binaryWriter.Write(VERSION);
            binaryWriter.Write(RequestType.ToString());
            binaryWriter.Write(ItemId);
            binaryWriter.Write(ItemCount);
            binaryWriter.Write(State.ToString());
            binaryWriter.Write(CreatedTick);
            binaryWriter.Write(ComputedCompletionTick);
            binaryWriter.Write(guid.ToString());
            binaryWriter.Write(bufferDebited);
            binaryWriter.Write(fillBufferRequest);
        }
    }
    
    public enum PlayerInventoryActionType
    {
        Add, // Add item to inventory
        Remove, // Remove items from inventory
    }
    public class PlayerInventoryAction
    {
        private static readonly int VERSION = 1; 
        public int ItemId;
        public int ItemCount;
        public PlayerInventoryActionType ActionType;
        public ItemRequest Request;

        public override string ToString()
        {
            return $"item={ItemId}, Count={ItemCount}, type={Enum.GetName(typeof(PlayerInventoryActionType), ActionType)}\t{Request}";
        }
        
        public static PlayerInventoryAction Import(BinaryReader r)
        {
            var version = r.ReadInt32();
            if (version != VERSION)
            {
                Log.Warn($"version mismatch on PlayerInventoryAction {VERSION} vs {version}");
            }

            Enum.TryParse(r.ReadString(), true, out RequestType requestType);
            var result = new PlayerInventoryAction
            {
               ItemId = r.ReadInt32(),
               ItemCount = r.ReadInt32(),
               ActionType = r.ReadChar() == 'a' ? PlayerInventoryActionType.Add : PlayerInventoryActionType.Remove,
               Request = ItemRequest.Import(r) 
            };

            return result;
        }

        public void Export(BinaryWriter binaryWriter)
        {
            binaryWriter.Write(VERSION);
            binaryWriter.Write(ItemId);
            binaryWriter.Write(ItemCount);
            binaryWriter.Write(ActionType == PlayerInventoryActionType.Add ? 'a' : 'r');
            Request.Export(binaryWriter);
        }
    }
}