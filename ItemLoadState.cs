using System;
using System.Collections.Generic;
using System.Linq;
using PersonalLogistics;
using PersonalLogistics.Model;
using PersonalLogistics.PlayerInventory;
using PersonalLogistics.Util;
using UnityEngine;

public class ItemLoadState
{
    public float percentLoaded;
    public string itemName;
    public int secondsRemaining;

    // public Texture itemImage;

    public static List<ItemLoadState> GetLoadState()
    {
        var mgr = PersonalLogisticManager.Instance;
        if (mgr == null)
        {
            Log.Debug($"mgr instance is null {DateTime.Now}");
            return new List<ItemLoadState>();
        }

        var itemRequests = mgr.GetRequests().Where(r => r.RequestType == RequestType.Load);
        var itemLoadStates = new List<ItemLoadState>();
        foreach (var itemRequest in itemRequests)
        {
            if (itemRequest.ComputedCompletionTime > DateTime.Now)
            {
                itemLoadStates.Add(new ItemLoadState
                {
                    percentLoaded = itemRequest.PercentComplete(),
                    itemName = ItemUtil.GetItemName(itemRequest.ItemId),
                    secondsRemaining = (int)new TimeSpan(itemRequest.ComputedCompletionTime.Ticks - DateTime.Now.Ticks).TotalSeconds
                });
            }
        }

        return itemLoadStates;
    }

    public static ItemLoadState GetLoadStateForItem(int itemId)
    {
        var mgr = PersonalLogisticManager.Instance;
        if (mgr == null)
        {
            return new ItemLoadState
            {
                percentLoaded = 100,
                itemName = ItemUtil.GetItemName(itemId)
            };
        }

        var playerInventoryActions = mgr.GetInventoryActions(false);
        var result = playerInventoryActions.Find(pia => pia.Request.PercentComplete() > 0.001f && pia.Request.ItemId == itemId);
        if (result == null)
        {
            return new ItemLoadState
            {
                percentLoaded = 100,
                itemName = ItemUtil.GetItemName(itemId)
            };
        }

        return new ItemLoadState
        {
            percentLoaded = result.Request.PercentComplete(),
            itemName = ItemUtil.GetItemName(itemId)
        };
    }
}