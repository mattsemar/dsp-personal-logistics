﻿using System;
using System.Collections.Generic;
using System.Linq;
using PersonalLogistics.PlayerInventory;
using PersonalLogistics.Util;

namespace PersonalLogistics.Model
{
    public class ItemLoadState
    {
        public int count;
        public string itemName;
        public float percentLoaded;
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
                        percentLoaded = 0,
                        itemName = ItemUtil.GetItemName(itemRequest.ItemId),
                        secondsRemaining = (int)secondsRemaining,
                        count = itemRequest.ItemCount
                    });
                }
            }

            return itemLoadStates;
        }
    }
}