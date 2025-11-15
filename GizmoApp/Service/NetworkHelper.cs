using GizmoApp.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace GizmoApp.Service
{
    public static class NetworkHelper
    {
        private static string? Normalize(string? s) =>
            string.IsNullOrWhiteSpace(s) ? null : s.Trim().Trim('"').ToUpperInvariant();

        private static INetworkInfoProvider? ResolveProvider()
        {
            // 1) Versuch: DependencyService (legacy, falls Plattformattribute vorhanden)
            var provider = DependencyService.Get<INetworkInfoProvider>();
            if (provider != null)
                return provider;

            // 2) Versuch: DI über MauiContext.Services (zuverlässig, wenn in MauiProgram registriert)
            try
            {
                var services = Application.Current?.Handler?.MauiContext?.Services;
                if (services != null)
                {
                    provider = services.GetService<INetworkInfoProvider>();
                    if (provider != null)
                        return provider;
                }
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"[NetworkHelper] ResolveProvider DI failed: {ex.GetType().Name}: {ex.Message}");
            }

            Debug.WriteLine("[NetworkHelper] No INetworkInfoProvider registered");
            return null;
        }

        private static bool IsConnectedViaEthernet()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up)
                        continue;

                    // Ignoriere Loopback/Virtual-Adapter
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                        ni.Description.ToLowerInvariant().Contains("virtual") ||
                        ni.Name.ToLowerInvariant().Contains("virtual"))
                        continue;

                    // Ethernet-/Kabelfamilie erkennen
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet
                        || ni.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet
                        || ni.NetworkInterfaceType == NetworkInterfaceType.FastEthernetFx
                        || ni.NetworkInterfaceType == NetworkInterfaceType.FastEthernetT
                        || ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet3Megabit)
                    {
                        // Sicherstellen, dass eine IPv4-Adresse existiert
                        var props = ni.GetIPProperties();
                        if (props.UnicastAddresses.Any(a => a.Address.AddressFamily == AddressFamily.InterNetwork))
                        {
                            Debug.WriteLine($"[NetworkHelper] Ethernet detected: {ni.Name} / {ni.Description}");
                            return true;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"[NetworkHelper] IsConnectedViaEthernet failed: {ex.Message}");
            }

            return false;
        }

        public static bool IsInHomeNetwork()
        {
            var provider = ResolveProvider();
            string savedSsid = Preferences.Default.Get("HomeSsid", "");

            // Wenn Provider vorhanden: versuche SSID-abgleich
            if (provider != null)
            {
                string? currentSsid = provider.GetCurrentSsid();
                var cur = Normalize(currentSsid);
                var saved = Normalize(savedSsid);

                Debug.WriteLine($"[NetworkHelper] saved='{savedSsid}' normalized='{saved}', current='{currentSsid ?? "(null)"}' normalized='{cur}'");

                if (cur != null && saved != null && cur == saved)
                    return true;

                // Falls SSID nicht verfügbar (z.B. LAN) => prüfen wir auf Ethernet
                if (cur == null && IsConnectedViaEthernet())
                {
                    Debug.WriteLine("[NetworkHelper] Keine SSID, aber Ethernet-Verbindung erkannt → nehme Heimnetz an");
                    return true;
                }
            }
            else
            {
                Debug.WriteLine("[NetworkHelper] Kein SSID-Provider verfügbar, prüfe Ethernet-Fallback");
                if (IsConnectedViaEthernet())
                {
                    Debug.WriteLine("[NetworkHelper] Ethernet-Verbindung erkannt → nehme Heimnetz an");
                    return true;
                }
            }

            // Kein Treffer
            Debug.WriteLine("[NetworkHelper] Nicht im Heimnetz");
            return false;
        }
    }
}
