using System;
using System.Collections.Generic;
using System.Linq;
using PersonalLogistics.PlayerInventory;
using PersonalLogistics.Util;

namespace PersonalLogistics.Model
{
    public class ItemLoadState
    {
        public RequestState requestState;
        public int count;
        public string itemName;
        public int secondsRemaining;

        public static List<ItemLoadState> GetLoadState(bool returnTestStates)
        {
            if (returnTestStates)
            {
                return GetTestStates();
            }

            var mgr = PersonalLogisticManager.Instance;
            if (mgr == null)
            {
                Log.Debug($"mgr instance is null {DateTime.Now}");
                return new List<ItemLoadState>();
            }

            var itemRequests = mgr.GetRequests()
                .Where(r => r.RequestType == RequestType.Load)
                .Where(r => r.State != RequestState.InventoryUpdated)
                .Where(r => r.State != RequestState.Complete)
                .ToList();
            itemRequests.Sort((ir1, ir2) => ir1.State.CompareTo(ir2.State));

            var itemLoadStates = new List<ItemLoadState>();
            foreach (var itemRequest in itemRequests)
            {
                if (itemRequest.ComputedCompletionTick > GameMain.gameTick || itemRequest.State != RequestState.WaitingForShipping)
                {
                    var secondsRemaining = (itemRequest.ComputedCompletionTick - GameMain.gameTick) / GameMain.tickPerSec;
                    if (itemRequest.State != RequestState.WaitingForShipping)
                    {
                        secondsRemaining = 100;
                    }

                    itemLoadStates.Add(new ItemLoadState
                    {
                        itemName = ItemUtil.GetItemName(itemRequest.ItemId),
                        secondsRemaining = (int)secondsRemaining,
                        count = itemRequest.ItemCount,
                        requestState = itemRequest.State
                    });
                }
            }

            return itemLoadStates;
        }

        private static List<ItemLoadState> GetTestStates()
        {
            var result = new List<ItemLoadState>();
            var allItems = ItemUtil.GetAllItems();
            var stateValues = Enum.GetNames(typeof(RequestState));
            for (int i = 0; i < stateValues.Length; i++)
            {
                var stateName = stateValues[i];
                if (stateName == RequestState.InventoryUpdated.ToString() || stateName == RequestState.Complete.ToString())
                    continue;
                if (Enum.TryParse(stateName, true, out RequestState requestState))
                {
                    result.Add(new ItemLoadState
                    {
                        itemName = ItemUtil.GetItemName(allItems[i].ID),
                        secondsRemaining = (i + 10) * 11,
                        count = 7 * i,
                        requestState = requestState
                    });
                }
            }

            return result;
        }

        public override string ToString()
        {
            if (requestState == RequestState.WaitingForShipping)
                return $"ItemLoadState: {requestState}, {itemName}, x{count}, {secondsRemaining}";
            return $"ItemLoadState: {requestState}, {itemName}, x{count}";
        }
    }
}