using System;

namespace MarketBrowserMod.Utils
{
    /// <summary>
    /// Utility class for formatting distances in Dual Universe units
    /// 1 SU (Space Unit) = 200 km = 200,000 meters
    /// </summary>
    public static class DistanceFormatter
    {
        private const double SU_IN_METERS = 200000.0; // 1 SU = 200 km = 200,000 meters
        private const double KM_IN_METERS = 1000.0;   // 1 km = 1,000 meters

        /// <summary>
        /// Format distance in meters to human-readable string with appropriate units
        /// </summary>
        /// <param name="distanceInMeters">Distance in meters</param>
        /// <returns>Formatted distance string (e.g., "1.5 SU", "150 km", "500 m")</returns>
        public static string FormatDistance(double distanceInMeters)
        {
            if (distanceInMeters < 0)
                return "0 m";

            // Same planet or very close distance
            if (distanceInMeters < 1)
                return "Same location";

            // Use SU for distances >= 200 km (1 SU)
            if (distanceInMeters >= SU_IN_METERS)
            {
                var su = distanceInMeters / SU_IN_METERS;
                return su >= 10 
                    ? $"{su:F0} SU"  // No decimals for large SU values
                    : $"{su:F1} SU"; // One decimal for smaller SU values
            }

            // Use km for distances >= 1 km
            if (distanceInMeters >= KM_IN_METERS)
            {
                var km = distanceInMeters / KM_IN_METERS;
                return km >= 10 
                    ? $"{km:F0} km"  // No decimals for large km values
                    : $"{km:F1} km"; // One decimal for smaller km values
            }

            // Use meters for small distances
            return $"{distanceInMeters:F0} m";
        }

        /// <summary>
        /// Convert distance in meters to SU
        /// </summary>
        /// <param name="distanceInMeters">Distance in meters</param>
        /// <returns>Distance in SU</returns>
        public static double MetersToSU(double distanceInMeters)
        {
            return distanceInMeters / SU_IN_METERS;
        }

        /// <summary>
        /// Convert distance in SU to meters
        /// </summary>
        /// <param name="distanceInSU">Distance in SU</param>
        /// <returns>Distance in meters</returns>
        public static double SUToMeters(double distanceInSU)
        {
            return distanceInSU * SU_IN_METERS;
        }

        /// <summary>
        /// Get distance category for UI styling
        /// </summary>
        /// <param name="distanceInMeters">Distance in meters</param>
        /// <returns>Distance category string</returns>
        public static string GetDistanceCategory(double distanceInMeters)
        {
            if (distanceInMeters < 1)
                return "same-location";
            if (distanceInMeters < KM_IN_METERS)
                return "meters";
            if (distanceInMeters < SU_IN_METERS)
                return "kilometers";
            if (distanceInMeters < SU_IN_METERS * 10)
                return "short-su";
            return "long-su";
        }
    }
}