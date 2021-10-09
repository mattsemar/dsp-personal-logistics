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
        public DateTime ComputedCompletionTime;
        public Guid guid = Guid.NewGuid();
        public string ItemName;

        public bool IsInTerminalState()
        {
            return State == RequestState.Complete || State == RequestState.Failed;
        }

        public override string ToString()
        {
            return $"{RequestType}, id={ItemId} name={ItemUtil.GetItemName(ItemId)}, count={ItemCount}, created={Created}, completion={ComputedCompletionTime}, state={Enum.GetName(typeof(RequestState), State)}";
        }

        public float PercentComplete()
        {
            if (ComputedCompletionTime == null)
            {
                return 0.0f;
            }

            if (State == RequestState.Failed)
                return 0.0f;
            TimeSpan computedCompletionTime = ComputedCompletionTime - DateTime.Now;
            var overallTime = ComputedCompletionTime - Created;
            return overallTime.Milliseconds / (float)computedCompletionTime.Milliseconds;
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