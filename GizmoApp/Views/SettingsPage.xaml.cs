using Microsoft.Maui.Storage;
using Microsoft.Maui.ApplicationModel;
using System.Diagnostics;
using GizmoApp.Service;

namespace GizmoApp.Views;

public partial class SettingsPage : ContentPage
{
    private const string SsidKey = "HomeSsid";

    public SettingsPage()
    {
        InitializeComponent();

        // SSID beim Laden einfügen
        SsidEntry.Text = Preferences.Default.Get(SsidKey, "");
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        var ssid = SsidEntry.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(ssid))
        {
            await DisplayAlert("Fehler", "Bitte gib eine gültige SSID ein.", "OK");
            return;
        }

        Preferences.Default.Set(SsidKey, ssid);

        // Runtime-Permission anfragen (auf Android erforderlich; Aufrufen ist plattformübergreifend sicher)
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Permission-Check fehlgeschlagen (erwartet auf manchen Plattformen): {ex.Message}");
        }

        // Aktuelle SSID prüfen
        var provider = DependencyService.Get<INetworkInfoProvider>();
        string? currentSsid = provider?.GetCurrentSsid();
        bool connected = NetworkHelper.IsInHomeNetwork();

        Debug.WriteLine($"Saved SSID: {ssid}, Current SSID: {currentSsid ?? "(unbekannt)"}, Im Heimnetz: {connected}");

        await DisplayAlert("Gespeichert", $"Heimnetz-SSID gespeichert:\n{ssid}\nAktuelle SSID: {currentSsid ?? "(unbekannt)"}\nIm Heimnetz: {connected}", "OK");

        await Navigation.PopModalAsync();
    }
}