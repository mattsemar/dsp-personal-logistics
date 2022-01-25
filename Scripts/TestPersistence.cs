using System;
using PersonalLogistics.Model;
using PersonalLogistics.ModPlayer;
using PersonalLogistics.Util;
using UnityEngine;

namespace PersonalLogistics.Scripts
{
    public class TestPersistence : MonoBehaviour
    {
#if DEBUG
        private void Update()
        {
            if (VFInput.control && Input.GetKeyDown(KeyCode.M))
            {
                // Test persistence by adding a bunch of random, well, not that random stuff and then forcing a game save
                // save this so we can restore after test is over
                var preTestPlayerId = PluginConfig.multiplayerUserId.Value;
                PlogPlayerRegistry.ClearLocal();
                PluginConfig.multiplayerUserId.Value = Guid.NewGuid().ToString();
                var preTestPlayer = (PlogLocalPlayer)PlogPlayerRegistry.RegisterLocal(PlogPlayerId.ComputeLocalPlayerId());
                try
                {
                    preTestPlayer.shippingManager.ClearBuffer();
                    var storageItemProtos = ItemUtil.GetAllItems().FindAll(i => ItemUtil.GetItemName(i.ID).ToLower().Contains("storage"));

                    for (int i = 0; i < storageItemProtos.Count; i++)
                    {
                        var storageItemProto = storageItemProtos[i];
                        preTestPlayer.shippingManager.UpsertBufferedItem(storageItemProto.ID, (i + 50) * 4,
                            GameMain.gameTick, (i + 50) * 2 + 6);
                        var itemRequest = new ItemRequest
                        {
                            ItemCount = i + 16,
                            ItemId = storageItemProto.ID,
                            RequestType = RequestType.Load,
                            ItemName = storageItemProto.Name.Translate(),
                            ProliferatorPoints = 4 / (i + 1),
                            ComputedCompletionTick = GameMain.gameTick + TimeUtil.GetGameTicksFromSeconds(120),
                            ComputedCompletionTime = DateTime.Now.AddSeconds(120),
                            State = RequestState.WaitingForShipping,
                        };
                        preTestPlayer.personalLogisticManager.GetRequests().Add(itemRequest);
                        preTestPlayer.shippingManager.AddTestRequest(itemRequest, new Cost
                        {
                            paid = true,
                            planetId = GameMain.localPlanet.id,
                            shippingToBufferCount = 5000,
                        });

                        preTestPlayer.inventoryManager.desiredInventoryState.AddDesiredItem(storageItemProto.ID, 5 + i);
                        preTestPlayer.inventoryManager.desiredInventoryState.AddBan(2208);
                        RecycleWindow.AddItemForTest(new GridItem
                        {
                            Count = i + 10,
                            Index = i,
                            ItemId = storageItemProto.ID,
                            ProliferatorPoints = (i + 1),
                        });
                    }


                    GameSave.SaveCurrentGame("persistence_test.dsv");
                }
                finally
                {
                    PlogPlayerRegistry.RestorePretestLocalPlayer(preTestPlayer);
                    PluginConfig.multiplayerUserId.Value = preTestPlayerId;
                }
            }
        }
#endif
    }
}