using System;
using System.IO;
using PersonalLogistics.ModPlayer;
using PersonalLogistics.Scripts;
using PersonalLogistics.Util;

namespace PersonalLogistics.SerDe
{
    public class SerDeV1 : ISerDe
    {
        public void Import(BinaryReader r)
        {
            var plogPlayer = PlogPlayerRegistry.RegisterLocal(PlogPlayerId.ComputeLocalPlayerId());

            try
            {
                plogPlayer.personalLogisticManager.Import(r);
            }
            catch (Exception e)
            {
                Log.Warn($"Failed to import personal log manager in v1 import. {e.Message}\r\n{e.StackTrace}");
            }

            try
            {
                plogPlayer.shippingManager.Import(r);
            }
            catch (Exception e)
            {
                Log.Warn($"Failed to import personal log manager in v1 import. {e.Message}\r\n{e.StackTrace}");
            }

            // have to get desired inventory state from config  
            plogPlayer.inventoryManager.desiredInventoryState.TryLoadFromConfig();
            Log.Debug(
                $"loaded desired inv state from config property. {plogPlayer.inventoryManager.desiredInventoryState.BannedItems.Count}\t{plogPlayer.inventoryManager.desiredInventoryState.DesiredItems.Count}");
            RecycleWindow.InitOnLoad();
        }

        public void Export(BinaryWriter w)
        {
            w.Write(1);
            PlogPlayerRegistry.LocalPlayer().personalLogisticManager.ExportData(w);
            PlogPlayerRegistry.LocalPlayer().shippingManager.ExportData(w);
        }
    }
}