using System;
using System.IO;
using PersonalLogistics.Model;
using PersonalLogistics.ModPlayer;
using PersonalLogistics.Scripts;
using PersonalLogistics.Util;

namespace PersonalLogistics.SerDe
{
    public class SerDeV2 : ISerDe
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
                Log.Warn($"Failed to import personal log manager in v2 import. {e.Message}\r\n{e.StackTrace}");
            }

            try
            {
                plogPlayer.shippingManager.Import(r);
            }
            catch (Exception e)
            {
                Log.Warn($"Failed to import shipping mgr v2 import. {e.Message}\r\n{e.StackTrace}");
            }

            try
            {
                plogPlayer.inventoryManager.desiredInventoryState = DesiredInventoryState.Import(r);
            }
            catch (Exception e)
            {
                Log.Warn($"Failed to import desired inv state for v2. {e.Message}\r\n{e.StackTrace}");
            }

            try
            {
                RecycleWindow.Import(r);
            }
            catch (Exception e)
            {
                RecycleWindow.InitOnLoad();
            }
        }

        public void Export(BinaryWriter w)
        {
            var localPlayer = PlogPlayerRegistry.LocalPlayer();
            w.Write(2);
            localPlayer.personalLogisticManager.ExportData(w);
            localPlayer.shippingManager.ExportData(w);
            localPlayer.inventoryManager.ExportData(w);
            RecycleWindow.Export(w);
        }
    }
}