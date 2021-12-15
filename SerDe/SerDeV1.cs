using System;
using System.IO;
using PersonalLogistics.Model;
using PersonalLogistics.PlayerInventory;
using PersonalLogistics.Scripts;
using PersonalLogistics.Shipping;
using PersonalLogistics.Util;

namespace PersonalLogistics.SerDe
{
    public class SerDeV1 : ISerDe
    {
        public void Import(BinaryReader r)
        {
            try
            {
                PersonalLogisticManager.Import(r);
            }
            catch (Exception e)
            {
                PersonalLogisticManager.InitOnLoad();
            }

            try
            {
                ShippingManager.Import(r);
            }
            catch (Exception e)
            {
                ShippingManager.InitOnLoad();
            }

            // have to get desired inventory state from config  
            var desiredInventoryState = DesiredInventoryState.instance;
            Log.Debug($"loaded desired inv state from config property. {desiredInventoryState.BannedItems.Count}\t{desiredInventoryState.DesiredItems.Count}");
            RecycleWindow.InitOnLoad();
        }

        public void Export(BinaryWriter w)
        {
            w.Write(1);
            PersonalLogisticManager.Export(w);
            ShippingManager.Export(w);
        }
    }
}