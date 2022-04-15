using System.IO;
using PersonalLogistics.Logistics;
using PersonalLogistics.Util;
using UnityEngine;

namespace PersonalLogistics.Nebula.Packets
{
    public class ItemSummaryUpdate
    {
        public int itemId { get; set; }
        public int availableItems { get; set; }
        public int requesters { get; set; }
        public int suppliedItems { get; set; }
        public int suppliers { get; set; }
        public int suppliedLocally { get; set; }
        public int proliferatorPoints { get; set; }

        public ItemSummaryUpdate()
        {
        }

        public ItemSummaryUpdate(int itemId, ByItemSummary itemSummary)
        {
            this.itemId = itemId;
            availableItems = itemSummary.AvailableItems;
            requesters = itemSummary.Requesters;
            suppliedItems = itemSummary.SuppliedItems;
            suppliers = itemSummary.Suppliers;
            suppliedLocally = itemSummary.SuppliedLocally;
            proliferatorPoints = itemSummary.ProliferatorPoints;
        }

        public ByItemSummary ToByItemSummary()
        {
            return new ByItemSummary
            {
                AvailableItems = availableItems,
                Requesters = requesters,
                Suppliers = suppliers,
                ProliferatorPoints = proliferatorPoints,
                SuppliedItems = suppliedItems,
                SuppliedLocally = suppliedLocally
            };
        }
    }
}