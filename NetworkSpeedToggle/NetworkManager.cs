using System;
using System.Collections.Generic;
using Microsoft.Management.Infrastructure;

namespace NetworkSpeedToggle
{
    public static class NetworkManager
    {
        /// <summary>
        /// Retrieves all supported speed values directly from the driver's *SpeedDuplex registry keyword.
        /// Bypasses localization issues (e.g., "Speed & Duplex" vs "Velocità").
        /// </summary>
        /// <param name="adapterName">The exact name of the network adapter (e.g., "Ethernet")</param>
        /// <returns>A dictionary containing the display name and its underlying registry value.</returns>
        public static Dictionary<string, string> GetSupportedSpeeds(string adapterName)
        {
            var supportedSpeeds = new Dictionary<string, string>();

            try
            {
                using CimSession session = CimSession.Create(null);

                // Query advanced properties targeting the universal *SpeedDuplex keyword
                string query = $"SELECT * FROM MSFT_NetAdapterAdvancedPropertySettingData WHERE Name = '{adapterName}' AND RegistryKeyword = '*SpeedDuplex'";
                var instances = session.QueryInstances(@"root\StandardCimv2", "WQL", query);

                foreach (var instance in instances)
                {
                    var validDisplayValues = instance.CimInstanceProperties["ValidDisplayValues"].Value as string[];
                    var validRegistryValues = instance.CimInstanceProperties["ValidRegistryValues"].Value as string[];

                    if (validDisplayValues != null && validRegistryValues != null && validDisplayValues.Length == validRegistryValues.Length)
                    {
                        for (int i = 0; i < validDisplayValues.Length; i++)
                        {
                            supportedSpeeds.Add(validDisplayValues[i], validRegistryValues[i]);
                        }
                    }
                    break;
                }
            }
            catch { }

            return supportedSpeeds;
        }
    }
}
