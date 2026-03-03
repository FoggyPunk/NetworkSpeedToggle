using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace StreamTweak
{
    public static class DisplayHelper
    {
        #region P/Invoke structs and constants

        [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Ansi, Size = 156)]
        private struct DEVMODE
        {
            [FieldOffset(36)] public short dmSize;
            [FieldOffset(108)] public int dmPelsWidth;
            [FieldOffset(112)] public int dmPelsHeight;
            [FieldOffset(120)] public int dmDisplayFrequency;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_RATIONAL
        {
            public uint Numerator;
            public uint Denominator;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_SOURCE_INFO
        {
            public LUID adapterId;
            public uint id;
            public uint modeInfoIdx;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_TARGET_INFO
        {
            public LUID adapterId;
            public uint id;
            public uint modeInfoIdx;
            public uint outputTechnology;
            public uint rotation;
            public uint scaling;
            public DISPLAYCONFIG_RATIONAL refreshRate;
            public uint scanLineOrdering;
            public uint targetAvailable;   // kept as uint to avoid Bool marshaling issues
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_INFO
        {
            public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
            public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
            public uint flags;
        }

        [StructLayout(LayoutKind.Sequential, Size = 64)]
        private struct DISPLAYCONFIG_MODE_INFO { }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_DEVICE_INFO_HEADER
        {
            public uint type;
            public uint size;
            public LUID adapterId;
            public uint id;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public uint value;
            public uint colorEncoding;
            public uint bitsPerColorChannel;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public uint enableAdvancedColor;
        }

        private const uint QDC_ONLY_ACTIVE_PATHS = 2;
        private const uint SDC_APPLY = 0x00000004;
        private const uint SDC_USE_SUPPLIED_DISPLAY_CONFIG = 0x00000020;
        private const uint DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO = 9;
        private const uint DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE = 10;

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern bool EnumDisplaySettingsA(string? deviceName, int iModeNum, ref DEVMODE devMode);

        [DllImport("user32.dll")]
        private static extern int GetDisplayConfigBufferSizes(
            uint flags,
            out uint numPathArrayElements,
            out uint numModeInfoArrayElements);

        [DllImport("user32.dll")]
        private static extern int QueryDisplayConfig(
            uint flags,
            ref uint numPathArrayElements,
            [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
            ref uint numModeInfoArrayElements,
            [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
            IntPtr currentTopologyId);

        [DllImport("user32.dll")]
        private static extern int DisplayConfigGetDeviceInfo(
            ref DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO requestPacket);

        [DllImport("user32.dll")]
        private static extern int DisplayConfigSetDeviceInfo(
            ref DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE requestPacket);

        [DllImport("user32.dll")]
        private static extern int SetDisplayConfig(
            uint numPathArrayElements,
            [In] DISPLAYCONFIG_PATH_INFO[] pathArray,
            uint numModeInfoArrayElements,
            [In] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
            uint flags);

        #endregion

        public static (int width, int height, int refreshRate) GetPrimaryDisplayInfo()
        {
            try
            {
                var devMode = new DEVMODE();
                devMode.dmSize = 156;
                if (EnumDisplaySettingsA(null, -1, ref devMode))
                    return (devMode.dmPelsWidth, devMode.dmPelsHeight, devMode.dmDisplayFrequency);
            }
            catch { }
            return (0, 0, 0);
        }

        public enum HdrState { NotSupported, Enabled, Disabled }

        public static HdrState GetHdrState()
        {
            try
            {
                // First try: Read from Windows registry (the authoritative source for HDR state)
                try
                {
                    using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                        @"Software\Microsoft\Windows\CurrentVersion\VideoSettings");

                    if (key != null)
                    {
                        var value = key.GetValue("EnableHDROutput");
                        if (value != null)
                        {
                            int hdrValue = Convert.ToInt32(value);
                            if (hdrValue == 1)
                                return HdrState.Enabled;
                            else if (hdrValue == 0)
                                return HdrState.Disabled;
                        }
                    }
                }
                catch { }

                // Second try: Use DisplayConfig API to detect HDR support and state
                if (GetPathsAndModes(out var paths, out var modes, out uint pathCount))
                {
                    if (pathCount > 0)
                    {
                        // Query the first active target to get HDR state
                        for (int i = 0; i < (int)pathCount; i++)
                        {
                            var getColorInfo = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO();
                            getColorInfo.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO;
                            getColorInfo.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>();
                            getColorInfo.header.adapterId = paths[i].targetInfo.adapterId;
                            getColorInfo.header.id = paths[i].targetInfo.id;

                            int rc = DisplayConfigGetDeviceInfo(ref getColorInfo);
                            if (rc == 0)
                            {
                                // Check if display supports HDR and its current state
                                // value == 1 means HDR is enabled
                                if (getColorInfo.value == 1)
                                    return HdrState.Enabled;
                                else if (getColorInfo.value == 0)
                                    return HdrState.Disabled;
                            }
                        }
                    }
                }

                return HdrState.NotSupported;
            }
            catch { return HdrState.NotSupported; }
        }

        // Added missing GetPaths method to query active display paths
        private static bool GetPaths(out DISPLAYCONFIG_PATH_INFO[] paths, out uint pathCount)
        {
            paths = Array.Empty<DISPLAYCONFIG_PATH_INFO>();
            pathCount = 0;

            try
            {
                int rc = GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out uint numPathArrayElements, out uint numModeInfoArrayElements);
                if (rc != 0 || numPathArrayElements == 0)
                    return false;

                var pathArray = new DISPLAYCONFIG_PATH_INFO[numPathArrayElements];
                var modeInfoArray = new DISPLAYCONFIG_MODE_INFO[numModeInfoArrayElements];

                uint pathElements = numPathArrayElements;
                uint modeElements = numModeInfoArrayElements;

                rc = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathElements, pathArray, ref modeElements, modeInfoArray, IntPtr.Zero);
                if (rc != 0)
                    return false;

                paths = new DISPLAYCONFIG_PATH_INFO[(int)pathElements];
                Array.Copy(pathArray, paths, (int)pathElements);
                pathCount = pathElements;

                return true;
            }
            catch
            {
                paths = Array.Empty<DISPLAYCONFIG_PATH_INFO>();
                pathCount = 0;
                return false;
            }
        }

        // Get both paths and mode info for applying configurations
        private static bool GetPathsAndModes(out DISPLAYCONFIG_PATH_INFO[] paths, out DISPLAYCONFIG_MODE_INFO[] modes, out uint pathCount)
        {
            paths = Array.Empty<DISPLAYCONFIG_PATH_INFO>();
            modes = Array.Empty<DISPLAYCONFIG_MODE_INFO>();
            pathCount = 0;

            try
            {
                int rc = GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out uint numPathArrayElements, out uint numModeInfoArrayElements);
                if (rc != 0 || numPathArrayElements == 0)
                    return false;

                var pathArray = new DISPLAYCONFIG_PATH_INFO[numPathArrayElements];
                var modeInfoArray = new DISPLAYCONFIG_MODE_INFO[numModeInfoArrayElements];

                uint pathElements = numPathArrayElements;
                uint modeElements = numModeInfoArrayElements;

                rc = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathElements, pathArray, ref modeElements, modeInfoArray, IntPtr.Zero);
                if (rc != 0)
                    return false;

                paths = new DISPLAYCONFIG_PATH_INFO[(int)pathElements];
                Array.Copy(pathArray, paths, (int)pathElements);

                modes = new DISPLAYCONFIG_MODE_INFO[(int)modeElements];
                Array.Copy(modeInfoArray, modes, (int)modeElements);

                pathCount = pathElements;

                return true;
            }
            catch
            {
                paths = Array.Empty<DISPLAYCONFIG_PATH_INFO>();
                modes = Array.Empty<DISPLAYCONFIG_MODE_INFO>();
                pathCount = 0;
                return false;
            }
        }

        public static bool SetHdrState(bool enable)
        {
            try
            {
                if (!GetPathsAndModes(out var paths, out var modes, out uint pathCount))
                    return false;

                if (pathCount == 0 || paths.Length == 0)
                    return false;

                bool anySuccess = false;

                // Set the HDR state for each active display
                for (int i = 0; i < (int)pathCount; i++)
                {
                    var setState = new DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE();
                    setState.header.type = DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE;
                    setState.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE>();
                    setState.header.adapterId = paths[i].targetInfo.adapterId;
                    setState.header.id = paths[i].targetInfo.id;
                    setState.enableAdvancedColor = enable ? 1u : 0u;

                    int rc = DisplayConfigSetDeviceInfo(ref setState);
                    if (rc == 0)
                        anySuccess = true;
                }

                // Apply the configuration changes to the system using both flags for maximum compatibility
                if (anySuccess && paths.Length > 0)
                {
                    int rc = SetDisplayConfig((uint)paths.Length, paths, (uint)modes.Length, modes, 
                        SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_APPLY);
                    if (rc == 0)
                        return true;
                }

                return anySuccess;
            }
            catch { return false; }
        }
    }
}