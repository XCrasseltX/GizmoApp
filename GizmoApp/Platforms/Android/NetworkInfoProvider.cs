using Android.Content;
using Android.Net;
using Android.Net.Wifi;
using Android.App;
using GizmoApp.Service;
using System;
using System.Diagnostics;

[assembly: Microsoft.Maui.Controls.Dependency(typeof(GizmoApp.Platforms.Android.NetworkInfoProvider))]
namespace GizmoApp.Platforms.Android
{
    public class NetworkInfoProvider : INetworkInfoProvider
    {
        public string? GetCurrentSsid()
        {
            try
            {
                var context = global::Android.App.Application.Context;
                Debug.WriteLine("[AndroidNetwork] GetCurrentSsid start");

                var connectivityManager = (ConnectivityManager)context.GetSystemService(Context.ConnectivityService)!;
                var network = connectivityManager.ActiveNetwork;
                Debug.WriteLine($"[AndroidNetwork] ActiveNetwork: {(network == null ? "null" : network.ToString())}");
                if (network == null)
                {
                    Debug.WriteLine("[AndroidNetwork] No active network");
                    return null;
                }

                var networkCapabilities = connectivityManager.GetNetworkCapabilities(network);
                Debug.WriteLine($"[AndroidNetwork] NetworkCapabilities: {(networkCapabilities == null ? "null" : "available")}");
                if (networkCapabilities == null || !networkCapabilities.HasTransport(TransportType.Wifi))
                {
                    Debug.WriteLine("[AndroidNetwork] Active network is not WiFi");
                    return null;
                }

                var wifiManager = (WifiManager?)context.GetSystemService(Context.WifiService);
                if (wifiManager == null)
                {
                    Debug.WriteLine("[AndroidNetwork] WifiManager is null");
                    return null;
                }

                var connectionInfo = wifiManager.ConnectionInfo;
#pragma warning disable 618
                var rawSsid = connectionInfo?.SSID;
#pragma warning restore 618
                Debug.WriteLine($"[AndroidNetwork] raw SSID: {(rawSsid ?? "(null)")}");

                if (string.IsNullOrWhiteSpace(rawSsid) || rawSsid == "<unknown ssid>")
                {
                    Debug.WriteLine("[AndroidNetwork] SSID unknown or empty");
                    return null;
                }

                var ssid = rawSsid.Replace("\"", "").Trim();
                Debug.WriteLine($"[AndroidNetwork] parsed SSID: {ssid}");

                return string.IsNullOrWhiteSpace(ssid) ? null : ssid;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AndroidNetwork] Exception: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }
    }
}
