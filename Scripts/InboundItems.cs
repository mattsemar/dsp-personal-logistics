using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace PersonalLogistics.Scripts
{
    public class InboundItems : MonoBehaviour
    {
        public int incomingItems = 41;

        public Text incomingItemsText;

        public void Update()
        {
            if (incomingItemsText == null)
            {
                return;
            }
            var itemLoadStates = ItemLoadState.GetLoadState();
            if (itemLoadStates != null)
            {
                var sb = new StringBuilder("Inbound: ");
                foreach (var loadState in itemLoadStates)
                {
                    sb.Append($"{loadState.itemName} {loadState.percentLoaded}%\r\n");
                }

                incomingItemsText.text = sb.ToString();
            }
            else
            {
                incomingItemsText.text = "Failed";
            }
        }
    }
}