using UnityEngine;
using UnityEngine.UI;

namespace PersonalLogistics.Scripts
{
    [ExecuteInEditMode]
    public class RequestedAmountSelector : MonoBehaviour
    {
        public const int MAX = 120;
        public const int MIN = 0;
        public int min;
        public int max;
        public Image mask;
        private void Update()
        {
            GetFillAmount();
        }

        private void GetFillAmount()
        {
            mask.fillAmount = min / (float)max;
        }
    }
}