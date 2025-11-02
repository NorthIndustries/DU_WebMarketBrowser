using NQ;

namespace MarketBrowserMod.Utils
{
    /// <summary>
    /// Utility class for converting Dual Universe quantas (whole numbers) to decimal prices
    /// Dual Universe stores prices as whole numbers where the last 2 digits represent decimals
    /// Example: 10000 in database = 100.00 quantas
    /// </summary>
    public static class PriceConverter
    {
        /// <summary>
        /// Converts a quanta amount (stored as whole number) to decimal price
        /// Divides by 100 to get the actual decimal price
        /// </summary>
        public static double ToDecimalPrice(long quantaAmount)
        {
            return quantaAmount / 100.0;
        }

        /// <summary>
        /// Converts a quanta amount (as double) to decimal price
        /// Useful for averages and calculations that result in double values
        /// </summary>
        public static double ToDecimalPrice(double quantaAmount)
        {
            return quantaAmount / 100.0;
        }

        /// <summary>
        /// Converts a Currency object's amount to decimal price
        /// </summary>
        public static double ToDecimalPrice(Currency? currency)
        {
            if (currency == null) return 0.0;
            return ToDecimalPrice(currency.amount);
        }

        /// <summary>
        /// Converts a decimal price back to quantas (whole number) for database storage
        /// </summary>
        public static long ToQuantas(double decimalPrice)
        {
            return (long)(decimalPrice * 100);
        }
    }
}

