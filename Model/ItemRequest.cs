using System;
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
        public RequestType RequestType;
        public int ItemId;
        public int ItemCount;
        public RequestState State = RequestState.Created;
        public DateTime Created = DateTime.Now;
        public long CreatedTick = GameMain.gameTick;
        public DateTime ComputedCompletionTime;
        public long ComputedCompletionTick;
        public Guid guid = Guid.NewGuid();
        public string ItemName;
        public bool SkipBuffer;
        public bool bufferDebited;

        public override string ToString()
        {
            var completeTime = DateTime.Now + TimeSpan.FromSeconds((ComputedCompletionTick - GameMain.gameTick) / GameMain.tickPerSec);
            return $"{RequestType}, id={ItemId} name={ItemUtil.GetItemName(ItemId)}, count={ItemCount}, created={Created}, completion={completeTime}, state={Enum.GetName(typeof(RequestState), State)}";
        }

        public float PercentComplete()
        {
            if (ComputedCompletionTick == 0l)
            {
                return 0.0f;
            }

            if (State == RequestState.Failed)
                return 0.0f;
            var totalTaskTime = (float) ComputedCompletionTick - CreatedTick;
            var elapsed = GameMain.gameTick - CreatedTick;
            return elapsed / totalTaskTime;
        }
    }
    
    public enum PlayerInventoryActionType
    {
        Add, // Add item to inventory
        Remove, // Remove items from inventory
    }
    public class PlayerInventoryAction
    {
        public int ItemId;
        public int ItemCount;
        public PlayerInventoryActionType ActionType;
        public ItemRequest Request;

        public override string ToString()
        {
            return $"item={ItemId}, Count={ItemCount}, type={Enum.GetName(typeof(PlayerInventoryActionType), ActionType)}\t{Request}";
        }
    }
}