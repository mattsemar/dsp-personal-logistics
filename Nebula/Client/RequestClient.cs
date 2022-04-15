using NebulaAPI;
using PersonalLogistics.Logistics;
using PersonalLogistics.Model;
using PersonalLogistics.ModPlayer;
using PersonalLogistics.Nebula.Packets;

namespace PersonalLogistics.Nebula.Client
{
    public static class RequestClient
    {
        public static void RequestStateFromHost()
        {
            NebulaModAPI.MultiplayerSession.Network.SendPacket(new ClientStateRequest(PlogPlayerId.ComputeLocalPlayerId()));
        }

        public static void SendDesiredItemUpdate(int itemID, int requestMin, int recycleMax)
        {
            NebulaModAPI.MultiplayerSession.Network.SendPacket(new DesiredItemUpdate(PlogPlayerRegistry.LocalPlayer().playerId,
                itemID, requestMin, recycleMax));
        }

        public static void NotifyBufferUpsert(int itemId, ItemStack stack, long gameTick)
        {
            NebulaModAPI.MultiplayerSession.Network.SendPacket(new BufferedItemUpsert(PlogPlayerRegistry.LocalPlayer().playerId, itemId,
                stack.ItemCount, stack.ProliferatorPoints, gameTick));
        }

        public static void NotifyStationInfo(StationInfo stationInfo)
        {
            NebulaModAPI.MultiplayerSession.Network.SendPacket(
                new StationInfoUpdate(stationInfo));
        }

        public static void SendByItemUpdate(int itemId, ByItemSummary itemSummaryUpdate)
        {
            NebulaModAPI.MultiplayerSession.Network.SendPacket(
                new ItemSummaryUpdate(itemId, itemSummaryUpdate));
        }

        public static void SendRemoteAddItemRequest(VectorLF3 playerUPosition, int itemId, ItemStack amountToAdd)
        {
            NebulaModAPI.MultiplayerSession.Network.SendPacket(
                new AddToNetworkRequest(PlogPlayerId.ComputeLocalPlayerId(), playerUPosition, itemId, amountToAdd));

        }
    }
}