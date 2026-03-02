using System;
using System.Runtime.InteropServices;

namespace StreamTweak
{
    public static class DisplayHelper
    {
        #region P/Invoke

        [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Ansi, Size = 156)]
        private struct DEVMODE
        {
            [FieldOffset(36)] public short dmSize;
            [FieldOffset(108)] public int dmPelsWidth;
            [FieldOffset(112)] public int dmPelsHeight;
            [FieldOffset(120)] public int dmDisplayFrequency;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID { public uint LowPart; public int HighPart; }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_RATIONAL { public uint Numerator; public uint Denominator; }

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
            [MarshalAs(UnmanagedType.Bool)] public bool targetAvailable;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_INFO
        {
            public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
            public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
            public uint flags;
        }

        [StructLayout(LayoutKind.Sequential, Size = 88)]
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
        private const uint DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO = 9;
        private const uint DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE = 10;

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern bool EnumDisplaySettingsA(string? deviceName, int iModeNum, ref DEVMODE devMode);

        [DllImport("user32.dll")]
        private static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

        [DllImport("user32.dll")]
        private static extern int QueryDisplayConfig(uint flags, ref uint numPathArrayElements, [Out] DISPLAYCONFIG_PATH_INFO[] pathArray, ref uint numModeInfoArrayElements, [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray, IntPtr currentTopologyId);

        [DllImport("user32.dll")]
        private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO requestPacket);

        [DllImport("user32.dll")]
        private static extern int DisplayConfigSetDeviceInfo(ref DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE requestPacket);

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
                int ret = GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out uint pathCount, out uint modeCount);
                if (ret != 0) return HdrState.NotSupported;

                var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
                var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

                ret = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
                if (ret != 0) return HdrState.NotSupported;

                var colorInfo = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO();
                colorInfo.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO;
                colorInfo.header.size = (uint)Marshal.SizeOf(colorInfo);
                colorInfo.header.adapterId = paths[0].targetInfo.adapterId;
                colorInfo.header.id = paths[0].targetInfo.id;

                ret = DisplayConfigGetDeviceInfo(ref colorInfo);
                if (ret != 0) return HdrState.NotSupported;

                bool supported = (colorInfo.value & 1) != 0;
                bool enabled = (colorInfo.value & 2) != 0;

                if (!supported) return HdrState.NotSupported;
                return enabled ? HdrState.Enabled : HdrState.Disabled;
            }
            catch { return HdrState.NotSupported; }
        }

        public static bool SetHdrState(bool enable)
        {
            try
            {
                int ret = GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out uint pathCount, out uint modeCount);
                if (ret != 0) return false;

                var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
                var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

                ret = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
                if (ret != 0) return false;

                var setState = new DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE();
                setState.header.type = DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE;
                setState.header.size = (uint)Marshal.SizeOf(setState);
                setState.header.adapterId = paths[0].targetInfo.adapterId;
                setState.header.id = paths[0].targetInfo.id;
                setState.enableAdvancedColor = enable ? 1u : 0u;

                ret = DisplayConfigSetDeviceInfo(ref setState);
                return ret == 0;
            }
            catch { return false; }
        }
    }
}