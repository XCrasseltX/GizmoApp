
// für die Fenstergröße
using GizmoApp.Models;
using GizmoApp.Service;
using Microsoft.Maui.Controls;
using System;
using System.Diagnostics;

namespace GizmoApp.Views
{
    public partial class MainPage : ContentPage
    {
        //Variablen

        //Configuration
        private AppConfig _config;
        private HomeAssistService _ha;
        private ChatService _chat;

        //Statusmeldungen
        private CancellationTokenSource? _toastCts;

        private Border? _thinkingBubble;
        private bool _thinkingActive = false;

        public MainPage()
        {
            InitializeComponent();

            //Rezize holen
            SizeChanged += OnSizeChanged;
            //Config laden
            _ = LoadConfig();
            if(_config == null || _chat == null || _ha == null)
            {
                throw new Exception("[Error] _config, _ha oder _chat kann nicht geladen werden...");
            }
        }

        private void OnChatScrolled(object sender, ScrolledEventArgs e)
        {
            double offset = e.ScrollY * 0.05;
            InputEditor.TranslationY = -offset;
            SendButton.TranslationY = -offset;
        }

        private Border CreateThinkingBubble()
        {
            var gear = new Image
            {
                Source = "gear.png",
                WidthRequest = 30,
                HeightRequest = 30,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };

            var border = new Border
            {
                Style = (Style)Application.Current.Resources["ChatBubbleAssistantStyle"],
                Padding = 10,
                Content = gear
            };

            return border;
        }

        private async void StartThinkingBubble()
        {
            if (_thinkingActive) return;
            _thinkingActive = true;

            _thinkingBubble = CreateThinkingBubble();
            ChatStack.Children.Add(_thinkingBubble);

            await _thinkingBubble.FadeTo(1, 200);
            await AnimateGear(_thinkingBubble);
        }

        private async void StopThinkingBubble()
        {
            _thinkingActive = false;
            if (_thinkingBubble == null) return;

            await _thinkingBubble.FadeTo(0, 200);
            ChatStack.Children.Remove(_thinkingBubble);
            _thinkingBubble = null;
        }

        private async Task AnimateGear(Border bubble)
        {
            if (bubble.Content is not Image img) return;

            while (_thinkingActive)
            {
                await img.RotateTo(360, 1200, Easing.Linear);
                img.Rotation = 0;
                await img.ScaleTo(1.1, 200, Easing.CubicOut);
                await img.ScaleTo(1.0, 200, Easing.CubicIn);
            }
        }

        // Configuration laden
        private async Task LoadConfig()
        {
            try
            {
                _config = await ConfigService.LoadConfigAsync();
                _ha = new HomeAssistService(_config);
                _chat = new ChatService(_ha);

                // Ereignisse abonnieren
                _ha.OnStatusChanged += (status) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[Status] {status}");

                    bool connected = status.Contains("✅") || status.Contains("Verbunden");
                    
                    MainThread.BeginInvokeOnMainThread(() =>
                    { 
                        ShowToast(status, false); // statt AddMassageToChat
                    });    
                };

                _ha.OnError += (err) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[Fehler] {err}");
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        ShowToast($"Fehler: {err}", true); // true = rot
                    });
                        
                };

                _chat.OnChatMessage += (msg, isUser) =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        AddMassageToChat(msg, isUser);
                        if (!isUser)
                            StopThinkingBubble();
                    });
                    
                };

                _chat.OnStopThinking += () =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        StopThinkingBubble();
                    });
                };

                _chat.OnToast += (message) =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        ShowToast(message);
                    });
                };

                await _ha.ConnectAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Fehler", $"Verbindung fehlgeschlagen: {ex.Message}", "OK");
            }
        }
        //Status und error benachrichtigungen
        private async void ShowToast(string message, bool isError = false)
        {
            _toastCts?.Cancel(); // wenn noch einer läuft, abbrechen
            _toastCts = new();

            ToastLabel.Text = message;
            //ToastBorder.BackgroundColor = isError ? Colors.DarkRed : Color.FromArgb("#333333");

            // sanft einblenden
            await BotImage.FadeTo(0, 250, Easing.CubicIn);
            await ToastBorder.FadeTo(1, 250, Easing.CubicOut);
            

            try
            {
                await Task.Delay(3000, _toastCts.Token); // 3 Sekunden sichtbar
            }
            catch (TaskCanceledException) { }

            // sanft ausblenden
            await ToastBorder.FadeTo(0, 250, Easing.CubicIn);
            await BotImage.FadeTo(1, 250, Easing.CubicOut);
        }

        //Nachricht senden
        private async void OnSendMessage(object sender, EventArgs e)
        {
            if (_ha == null || !_ha.IsConnected)
            {
                ShowToast("❌ Nicht verbunden mit Home Assistant", true);
                return;
            }

            string? text = InputEditor.Text?.Trim();
            if (string.IsNullOrEmpty(text))
                return;

            InputEditor.Text = "";
            System.Diagnostics.Debug.WriteLine($"Send Message: {text}");

            // 🚀 Denkblase starten
            StartThinkingBubble();

            await _chat.SendUserMessage(text);
        }

        private async void AddMassageToChat(string message, bool isUser)
        {
            var userstyle = (Style)Application.Current.Resources["ChatBubbleUserStyle"];
            var AssistStyle = (Style)Application.Current.Resources["ChatBubbleAssistantStyle"];
            var ChatStyle = (Style)Application.Current.Resources["EditorMessageStyle"];

            if (userstyle == null || AssistStyle == null || ChatStyle == null)
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Styles nicht gefunden!");
                return;
            }

            
            double max = Math.Max(0, ChatScroll.Width * 0.8);   // 80% des Chatbereichs
            //Hier Logik zum Hinzufügen der Nachricht zum Chatverlauf
            var bubble = new Border()
            {
                Style = isUser ? userstyle : AssistStyle,
                Content = new Editor
                {
                    Text = message,
                    Style = ChatStyle
                }

            };

            bubble.MaximumWidthRequest = max;
            ChatStack.Children.Add(bubble);

            // 👇 automatisch ans Ende scrollen (unterste Nachricht im Sichtfeld)
            await Task.Delay(50); // kurz warten, bis Layout aktualisiert ist
            await ChatScroll.ScrollToAsync(ChatStack, ScrollToPosition.End, true);
        }
        private double _lastWidth;

        // Visual State Setter Wichtig!!!
        private void OnSizeChanged(object? sender, EventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"OnSizeChanged: Width={Width}, Height={Height}");

            //Bei jedem Rezize Prüfen
            if (Width > 700)
            {
                //System.Diagnostics.Debug.WriteLine("→ Wechsel auf DESKTOP");
                VisualStateManager.GoToState(BaseGrid, "Desktop");
                
            }
            else
            {
                //System.Diagnostics.Debug.WriteLine("→ Wechsel auf MOBILE");
                VisualStateManager.GoToState(BaseGrid, "Mobile");
            }

            if (Math.Abs(Width - _lastWidth) > 0.5)
            {
                //System.Diagnostics.Debug.WriteLine($"OnSizeChanged: Width={Width}, Height={Height}");
                var max = Math.Max(0, ChatScroll.Width * 0.8);
                /*
                foreach (var b in ChatStack.Children.OfType<Border>())
                {
                    b.MinimumWidthRequest = max;
                }

                // optional „sanft“ neu messen:
                ChatStack.InvalidateMeasure();
                ChatScroll.InvalidateMeasure();
                */
            }
            _lastWidth = Width;

        }

        private async void OnChatButtonClicked(object sender, EventArgs e)
        {
            await Navigation.PushModalAsync(new ChatSelectorPage());
        }
    }
}
