using Microsoft.Maui.Storage;

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
        await DisplayAlert("Gespeichert", $"Heimnetz-SSID gespeichert:\n{ssid}", "OK");

        await Navigation.PopModalAsync();
    }
}