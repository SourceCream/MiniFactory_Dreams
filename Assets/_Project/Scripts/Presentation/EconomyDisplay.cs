using System;
using System.Globalization;

namespace MiniFactory.Presentation
{
    public static class EconomyDisplay
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        public static string Coins(double value)
        {
            return Compact(Math.Floor(value), "N0");
        }

        public static string Cost(double value)
        {
            return Compact(Math.Ceiling(value), "N0");
        }

        public static string Rate(double value)
        {
            return Compact(value, "0.##");
        }

        private static string Compact(double value, string format)
        {
            if (value >= 1e12) return (value / 1e12).ToString("0.##", Culture) + "T";
            if (value >= 1e9) return (value / 1e9).ToString("0.##", Culture) + "B";
            if (value >= 1e6) return (value / 1e6).ToString("0.##", Culture) + "M";
            return value.ToString(format, Culture).Replace(',', ' ');
        }
    }
}
