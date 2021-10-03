using System.Collections.Generic;
using PersonalLogistics.Model;
using PersonalLogistics.PlayerInventory;
using PersonalLogistics.Util;
using UnityEngine;

public class ItemLoadState
{
    public float percentLoaded;

    public string itemName;

    // public Texture itemImage;

    public static List<ItemLoadState> GetLoadState()
    {
        var mgr = PersonalLogisticManager.Instance;
        if (mgr == null)
        {
            return new List<ItemLoadState>();
        }

        var playerInventoryActions = mgr.GetInventoryActions(false);
        Debug.Log($"got {playerInventoryActions.Count} actions {JsonUtility.ToJson(playerInventoryActions)}");
        var itemLoadStates = new List<ItemLoadState>();
        foreach (var inventoryAction in playerInventoryActions)
        {
            if (inventoryAction.Request.RequestType == RequestType.Store)
                continue;
            if (inventoryAction.Request.PercentComplete() > 0.001f)
            {
                itemLoadStates.Add( new ItemLoadState
                {
                    percentLoaded = inventoryAction.Request.PercentComplete(),
                    itemName = ItemUtil.GetItemName(inventoryAction.ItemId)
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