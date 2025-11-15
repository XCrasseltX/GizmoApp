using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using GizmoApp.Service;

[assembly: Microsoft.Maui.Controls.Dependency(typeof(GizmoApp.Platforms.Windows.NetworkInfoProvider))]
namespace GizmoApp.Platforms.Windows
{
    public class NetworkInfoProvider : INetworkInfoProvider
    {
        public string? GetCurrentSsid()
        {
            try
            {
                uint negotiatedVersion;
                IntPtr clientHandle = IntPtr.Zero;
                uint result = WlanOpenHandle(2, IntPtr.Zero, out negotiatedVersion, out clientHandle);
                if (result != 0 || clientHandle == IntPtr.Zero)
                {
                    Debug.WriteLine($"[WindowsNetwork] WlanOpenHandle failed: {result}");
                    return null;
                }

                IntPtr ifaceListPtr = IntPtr.Zero;
                result = WlanEnumInterfaces(clientHandle, IntPtr.Zero, out ifaceListPtr);
                if (result != 0 || ifaceListPtr == IntPtr.Zero)
                {
                    Debug.WriteLine($"[WindowsNetwork] WlanEnumInterfaces failed: {result}");
                    WlanCloseHandle(clientHandle, IntPtr.Zero);
                    return null;
                }

                try
                {
                    var listHeader = Marshal.PtrToStructure<WLAN_INTERFACE_INFO_LIST>(ifaceListPtr);
                    if (listHeader.dwNumberOfItems == 0)
                    {
                        Debug.WriteLine("[WindowsNetwork] No WLAN interfaces found");
                        return null;
                    }

                    // Pointer arithmetic to first WLAN_INTERFACE_INFO
                    long current = ifaceListPtr.ToInt64() + Marshal.SizeOf<WLAN_INTERFACE_INFO_LIST>();
                    for (int i = 0; i < listHeader.dwNumberOfItems; i++)
                    {
                        var info = Marshal.PtrToStructure<WLAN_INTERFACE_INFO>(new IntPtr(current));
                        current += Marshal.SizeOf<WLAN_INTERFACE_INFO>();

                        Debug.WriteLine($"[WindowsNetwork] Interface: {info.strInterfaceDescription}, State: {info.isState}");

                        // Query current connection
                        IntPtr dataPtr = IntPtr.Zero;
                        uint dataSize;
                        WLAN_OPCODE_VALUE_TYPE opcode;
                        Guid guid = info.InterfaceGuid;
                        uint qres = WlanQueryInterface(clientHandle, ref guid, WLAN_INTF_OPCODE.wlan_intf_opcode_current_connection, IntPtr.Zero, out dataSize, out dataPtr, out opcode);
                        if (qres != 0 || dataPtr == IntPtr.Zero)
                        {
                            Debug.WriteLine($"[WindowsNetwork] WlanQueryInterface failed: {qres}");
                            continue;
                        }

                        try
                        {
                            var conn = Marshal.PtrToStructure<WLAN_CONNECTION_ATTRIBUTES>(dataPtr);
                            var ssid = Dot11SsidToString(conn.wlanAssociationAttributes.dot11Ssid);
                            Debug.WriteLine($"[WindowsNetwork] Found SSID: {ssid ?? "(null)"}");
                            if (!string.IsNullOrWhiteSpace(ssid))
                                return ssid;
                        }
                        finally
                        {
                            WlanFreeMemory(dataPtr);
                        }
                    }
                }
                finally
                {
                    WlanFreeMemory(ifaceListPtr);
                    WlanCloseHandle(clientHandle, IntPtr.Zero);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowsNetwork] Exception: {ex.GetType().Name}: {ex.Message}");
            }

            Debug.WriteLine("[WindowsNetwork] SSID not found");
            return null;
        }

        private static string? Dot11SsidToString(DOT11_SSID ssid)
        {
            try
            {
                if (ssid.uSSIDLength == 0 || ssid.ucSSID == null)
                    return null;
                return Encoding.UTF8.GetString(ssid.ucSSID, 0, (int)ssid.uSSIDLength);
            }
            catch { return null; }
        }

        #region Native WLAN API

        private const string WLAN_API = "wlanapi.dll";

        [DllImport(WLAN_API, SetLastError = true)]
        private static extern uint WlanOpenHandle(
            uint dwClientVersion,
            IntPtr pReserved,
            out uint pdwNegotiatedVersion,
            out IntPtr phClientHandle);

        [DllImport(WLAN_API, SetLastError = true)]
        private static extern uint WlanCloseHandle(
            IntPtr hClientHandle,
            IntPtr pReserved);

        [DllImport(WLAN_API, SetLastError = true)]
        private static extern uint WlanEnumInterfaces(
            IntPtr hClientHandle,
            IntPtr pReserved,
            out IntPtr ppInterfaceList);

        [DllImport(WLAN_API)]
        private static extern void WlanFreeMemory(IntPtr pMemory);

        [DllImport(WLAN_API, SetLastError = true)]
        private static extern uint WlanQueryInterface(
            IntPtr hClientHandle,
            ref Guid pInterfaceGuid,
            WLAN_INTF_OPCODE OpCode,
            IntPtr pReserved,
            out uint pdwDataSize,
            out IntPtr ppData,
            out WLAN_OPCODE_VALUE_TYPE pWlanOpcodeValueType);

        private enum WLAN_OPCODE_VALUE_TYPE
        {
            wlan_opcode_value_type_query_only = 0,
            wlan_opcode_value_type_set_by_group_policy,
            wlan_opcode_value_type_set_by_user,
            wlan_opcode_value_type_invalid
        }

        private enum WLAN_INTF_OPCODE
        {
            wlan_intf_opcode_autoconf_enabled = 1,
            wlan_intf_opcode_background_scan_enabled,
            wlan_intf_opcode_media_streaming_mode,
            wlan_intf_opcode_radio_state,
            wlan_intf_opcode_bss_type,
            wlan_intf_opcode_interface_state,
            wlan_intf_opcode_current_connection = 7,
            // ...
        }

        private enum WLAN_INTERFACE_STATE
        {
            wlan_interface_state_not_ready = 0,
            wlan_interface_state_connected = 1,
            wlan_interface_state_ad_hoc_network_formed = 2,
            wlan_interface_state_disconnecting = 3,
            wlan_interface_state_disconnected = 4,
            wlan_interface_state_associating = 5,
            wlan_interface_state_discovering = 6,
            wlan_interface_state_authenticating = 7
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WLAN_INTERFACE_INFO_LIST
        {
            public int dwNumberOfItems;
            public int dwIndex;
            // followed by WLAN_INTERFACE_INFO[dwNumberOfItems]
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WLAN_INTERFACE_INFO
        {
            public Guid InterfaceGuid;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strInterfaceDescription;

            public WLAN_INTERFACE_STATE isState;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DOT11_SSID
        {
            public uint uSSIDLength;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] ucSSID;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WLAN_ASSOCIATION_ATTRIBUTES
        {
            public DOT11_SSID dot11Ssid;
            public uint dot11BssType;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            public byte[] dot11Bssid;
            public uint uPhyId;
            public uint wlanSignalQuality;
            public uint ulRxRate;
            public uint ulTxRate;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WLAN_SECURITY_ATTRIBUTES
        {
            [MarshalAs(UnmanagedType.Bool)]
            public bool bSecurityEnabled;
            [MarshalAs(UnmanagedType.Bool)]
            public bool bOneXEnabled;
            public uint dot11AuthAlgorithm;
            public uint dot11CipherAlgorithm;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WLAN_CONNECTION_ATTRIBUTES
        {
            public WLAN_INTERFACE_STATE isState;
            public uint wlanConnectionMode;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strProfileName;
            public WLAN_ASSOCIATION_ATTRIBUTES wlanAssociationAttributes;
            public WLAN_SECURITY_ATTRIBUTES wlanSecurityAttributes;
        }

        #endregion
    }
}
