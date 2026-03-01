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
            catch (Exception ex)
            {
                // Log exception if needed during development
                Console.WriteLine($"Error retrieving speeds: {ex.Message}");
            }

            return supportedSpeeds;
        }

        /// <summary>
        /// Instantly applies the new speed setting via WMI and restarts the adapter to force renegotiation.
        /// </summary>
        /// <param name="adapterName">The exact name of the network adapter</param>
        /// <param name="registryValue">The raw registry value string to apply (e.g., "6" for 1Gbps)</param>
        public static void SetAdapterSpeedNative(string adapterName, string registryValue)
        {
            try
            {
                using CimSession session = CimSession.Create(null);

                // 1. Modify the *SpeedDuplex property value
                string queryConfig = $"SELECT * FROM MSFT_NetAdapterAdvancedPropertySettingData WHERE Name = '{adapterName}' AND RegistryKeyword = '*SpeedDuplex'";
                var configInstances = session.QueryInstances(@"root\StandardCimv2", "WQL", queryConfig);

                foreach (var instance in configInstances)
                {
                    instance.CimInstanceProperties["RegistryValue"].Value = registryValue;
                    session.ModifyInstance(@"root\StandardCimv2", instance);
                }

                // 2. Restart the adapter (Disable then Enable) to apply changes immediately
                string queryAdapter = $"SELECT * FROM MSFT_NetAdapter WHERE Name = '{adapterName}'";
                var adapterInstances = session.QueryInstances(@"root\StandardCimv2", "WQL", queryAdapter);

                foreach (var adapter in adapterInstances)
                {
                    session.InvokeMethod(adapter, "Disable", null);
                    session.InvokeMethod(adapter, "Enable", null);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to set adapter speed: {ex.Message}", ex);
            }
        }
    }
}
