using System;
using System.Collections.Generic;
using System.Linq;
using PersonalLogistics.Logistics;
using PersonalLogistics.ModPlayer;
using PersonalLogistics.Util;

namespace PersonalLogistics.Model
{
    public class ItemLoadState
    {
        public RequestState requestState;
        public int count;
        public string itemName;
        public int secondsRemaining;
        public Cost cost;
        private const int testMultiplier = 3;

        public static List<ItemLoadState> GetLoadState(bool returnTestStates)
        {
            if (returnTestStates)
            {
                return GetTestStates();
            }

            var mgr = PlogPlayerRegistry.LocalPlayer().personalLogisticManager;
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
            itemRequests.Sort((ir1, ir2) =>
            {
                if (ir1.State != ir2.State)
                {
                    return ir1.State.CompareTo(ir2.State);
                }

                return ir2.CreatedTick.CompareTo(ir1.CreatedTick);
            });

            var itemLoadStates = new List<ItemLoadState>();
            foreach (var itemRequest in itemRequests)
            {
                Cost cost = null;
                var secondsRemaining = TimeUtil.GetSecondsFromGameTicks(itemRequest.ComputedCompletionTick - GameMain.gameTick);
                if (itemRequest.State != RequestState.WaitingForShipping)
                {
                    secondsRemaining = 0;
                }
                else
                {
                    cost = PlogPlayerRegistry.LocalPlayer().shippingManager.GetCostForRequest(itemRequest.guid);
                    if (cost == null)
                    {
                        Log.Warn($"failed to get cost for item request: {itemRequest}");
                    }
                }

                itemLoadStates.Add(new ItemLoadState
                {
                    itemName = ItemUtil.GetItemName(itemRequest.ItemId),
                    secondsRemaining = (int)secondsRemaining,
                    count = itemRequest.ItemCount,
                    requestState = itemRequest.State,
                    cost = cost
                });
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
                        count = 7 * (i + 1),
                        requestState = requestState,
                        cost = requestState == RequestState.WaitingForShipping ? BuildCost(allItems[i].ID, true) : null
                    });
                    if (requestState == RequestState.WaitingForShipping)
                    {
                        result.Add(new ItemLoadState
                        {
                            itemName = ItemUtil.GetItemName(allItems[i + 1].ID),
                            secondsRemaining = (i + 10) * 12,
                            count = 7 * (i + 1) * 4,
                            requestState = requestState,
                            cost = BuildCost(allItems[i + 1].ID, false, false, 100),
                        });
                        result.Add(new ItemLoadState
                        {
                            itemName = ItemUtil.GetItemName(allItems[i + 2].ID),
                            secondsRemaining = (i + 10) * 13,
                            count = 7 * (i + 1) * 6,
                            requestState = requestState,
                            cost = BuildCost(allItems[i + 2].ID, false, true),
                        });
                        result.Add(new ItemLoadState
                        {
                            itemName = ItemUtil.GetItemName(allItems[i + 3].ID),
                            secondsRemaining = (i + 10) * 14,
                            count = 7 * (i + 1) * 9,
                            requestState = requestState,
                            cost = BuildCost(allItems[i + 3].ID, false, true, 100),
                        });
                    }
                }
            }

            var x2 = new List<ItemLoadState>();
            for (int i = 0; i < testMultiplier; i++)
            {
                foreach (var itemLoadState in result)
                {
                    x2.Add(new ItemLoadState
                    {
                        cost = itemLoadState.cost,
                        itemName = itemLoadState.itemName + $"_{i+2}",
                        requestState = itemLoadState.requestState,
                        count = itemLoadState.count * (i+2),
                        secondsRemaining = itemLoadState.secondsRemaining * (i+2)
                    });
                }
            }

            result.AddRange(x2);

            return result;
        }

        private static Cost BuildCost(int itemId, bool paid, bool needWarper = false, long energyNeeded = 0)
        {
            var anyStationWithItem = StationInfo.GetAnyStationWithItem(itemId);
            if (anyStationWithItem == null)
                return null;
            return new Cost
            {
                paid = paid,
                planetId = anyStationWithItem.PlanetInfo.PlanetId,
                stationId = anyStationWithItem.StationId,
                energyCost = energyNeeded,
                needWarper = needWarper
            };
        }

        public override string ToString()
        {
            if (requestState == RequestState.WaitingForShipping)
                return $"ItemLoadState: {requestState}, {itemName}, x{count}, {secondsRemaining}";
            return $"ItemLoadState: {requestState}, {itemName}, x{count}";
        }
    }
}