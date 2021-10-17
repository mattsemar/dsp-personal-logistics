﻿using System;
using System.Collections.Generic;
using System.Linq;
using PersonalLogistics.Model;
using PersonalLogistics.PlayerInventory;
using PersonalLogistics.Util;

namespace PersonalLogistics
{
    public class ItemLoadState
    {
        public float percentLoaded;
        public string itemName;
        public int secondsRemaining;

        public static List<ItemLoadState> GetLoadState()
        {
            var mgr = PersonalLogisticManager.Instance;
            if (mgr == null)
            {
                Log.Debug($"mgr instance is null {DateTime.Now}");
                return new List<ItemLoadState>();
            }

            var itemRequests = mgr.GetRequests()
                .Where(r => r.RequestType == RequestType.Load && r.State == RequestState.WaitingForShipping);
            var itemLoadStates = new List<ItemLoadState>();
            foreach (var itemRequest in itemRequests)
            {
                if (itemRequest.ComputedCompletionTick > GameMain.gameTick)
                {
                    var secondsRemaining = (itemRequest.ComputedCompletionTick - GameMain.gameTick) / GameMain.tickPerSec;
                    itemLoadStates.Add(new ItemLoadState
                    {
                        percentLoaded = itemRequest.PercentComplete(),
                        itemName = ItemUtil.GetItemName(itemRequest.ItemId),
                        secondsRemaining = (int)secondsRemaining
                    });
                }
                else
                {
                    Log.Debug($"Found item request for TScript {itemRequest.ItemName}, {itemRequest.State}, but completion tick is in the past {GameMain.gameTick} < {itemRequest.ComputedCompletionTick}");
                }
            }

            return itemLoadStates;
        }
    }
}