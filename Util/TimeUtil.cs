using System;
using System.Collections.Generic;
using System.Linq;

namespace PersonalLogistics.Util
{
    public static class TimeUtil
    {
        public static string FormatEta(double seconds)
        {
            int s = (int)(seconds);
            int m = s / 60;
            int h = m / 60;
            s %= 60;
            m %= 60;
            if (h == 0 && m == 0)
            {
                return $"{s:00}s";
            }

            if (h == 0)
            {
                return $"{m:00}:{s:00}";
            }

            return $"{h:00}:{m:00}:{s:00}";
        }
    }
}