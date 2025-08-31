using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VesselAutoRenamer
{
    public static class DatePlaceholders
    {
        public static string ReplaceDatePlaceholders(string template)
        {
            if (string.IsNullOrEmpty(template))
                return template;

            double ut = Planetarium.GetUniversalTime();
            int days = (int)(ut / 21600);

            int year = days / 426 + 1;
            int dayOfYear = (days % 426) + 1;

            return template
                .Replace("%Y", year.ToString())
                .Replace("%D", dayOfYear.ToString());
        }
    }
}
