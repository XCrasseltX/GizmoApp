using GizmoApp.Services;
using Microsoft.Maui.Controls.PlatformConfiguration;
using System.Diagnostics;
using System.Windows.Input;

namespace GizmoApp.Views;

public partial class ChatView : ContentPage
{
    private HomeAssistantClient _haClient = new();
    private ChatManager _chatManager => ChatManager.Instance;

    public ICommand DeleteChatCommand { get; }

    public ChatView()
    {
        InitializeComponent();

        DeleteChatCommand = new Command<string>(OnDeleteChat);
        BindingContext = this;

        _haClient.ResponseReceived += OnResponse;
        _haClient.ConnectionStateChanged += OnConnectionStateChanged;
        _chatManager.ChatsChanged += OnChatsChanged;

        MessageEntry.Completed += (s, e) => OnSendClicked(s, e);
        MessageEntry.HandlerChanged += (s, e) =>
        {
#if ANDROID
            if (MessageEntry?.Handler?.PlatformView is Android.Widget.EditText nativeEditor)
            {
                nativeEditor.ImeOptions = Android.Views.InputMethods.ImeAction.Send;
                nativeEditor.SetImeActionLabel("Senden", Android.Views.InputMethods.ImeAction.Send);
                nativeEditor.EditorAction -= NativeEditor_EditorAction;
                nativeEditor.EditorAction += NativeEditor_EditorAction;
            }
#endif
        };

        // Initial Chat-Liste laden
        RefreshChatList();
    }

#if ANDROID
    private void NativeEditor_EditorAction(object sender, Android.Widget.TextView.EditorActionEventArgs e)
    {
        if (e.ActionId == Android.Views.InputMethods.ImeAction.Send)
        {
            OnSendClicked(sender, EventArgs.Empty);
            e.Handled = true;
        }
    }
#endif

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        // Zustand umschalten je nach Bildschirmbreite
        if (width < 700)
            VisualStateManager.GoToState(this, "Narrow"); // Handy
        else
            VisualStateManager.GoToState(this, "Wide");   // Tablet/Desktop
    }

    private void OnConnectionStateChanged(bool isConnected)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ButtonSend.IsEnabled = isConnected; // dein Button-Name
            ButtonSend.Opacity = isConnected ? 1.0 : 0.5;

            if (!isConnected)
                Debug.WriteLine(" Verbindung zu Home Assistant getrennt");
            else
                Debug.WriteLine(" Verbindung zu Home Assistant aktiv");
        });
    }

    //Wichtig, um "Memory Leaks" zu vermeiden
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Event-Abo wieder entfernen, wenn die Seite geschlossen wird
        _haClient.ResponseReceived -= OnResponse;
        _chatManager.ChatsChanged -= OnChatsChanged;
#if ANDROID
        if (MessageEntry?.Handler?.PlatformView is Android.Widget.EditText nativeEditor)
        {
            nativeEditor.EditorAction -= NativeEditor_EditorAction;
        }
    #endif
    }

    protected override async void OnAppearing()
	{
		base.OnAppearing();
        // Verbindung zu Home Assistant herstellen
        await _haClient.InitializeAsync();
		await _haClient.ConnectAsync();

        // Wenn noch kein Chat aktiv ist, neuen starten
        if (_chatManager.ActiveChat == null)
        {
            _chatManager.StartNewChat();
            Debug.WriteLine(" Neuer Chat beim Seitenstart gestartet");
        }
        else
        {
            // Bestehenden Chat laden
            LoadActiveChat();
        }

        Debug.WriteLine("Neuer Chat gestartet beim Seitenstart");
    }

    private void OnChatsChanged()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            RefreshChatList();
        });
    }

    private void RefreshChatList()
    {
        var chats = _chatManager.GetSortedChats();
        ChatHistoryList.ItemsSource = chats;
        ChatHistoryListMobile.ItemsSource = chats;

        Debug.WriteLine($" Chat-Liste aktualisiert: {chats.Count} Chats");
    }

    private void OnNewChatClicked(object sender, EventArgs e)
    {
        StartNewChat();
    }

    private void OnNewChatClickedMobile(object sender, EventArgs e)
    {
        StartNewChat();
        ChatHistoryOverlay.IsVisible = false; // Overlay schließen
    }

    private void StartNewChat()
    {
        _chatManager.StartNewChat();
        MessagesLayout.Children.Clear();
        Debug.WriteLine(" Neuer Chat gestartet");
    }

    private void OnChatSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is ChatSession selected)
        {
            _chatManager.ActivateChat(selected.ChatId);
            LoadActiveChat();
            ChatHistoryList.SelectedItem = null; // Auswahl zurücksetzen
        }
    }

    private void OnChatSelectedMobile(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is ChatSession selected)
        {
            _chatManager.ActivateChat(selected.ChatId);
            LoadActiveChat();
            ChatHistoryListMobile.SelectedItem = null;
            ChatHistoryOverlay.IsVisible = false; // Overlay schließen
        }
    }

    private void OnDeleteChat(string chatId)
    {
        Debug.WriteLine($"DELETE aufgerufen für ChatId: {chatId}");

        if (string.IsNullOrEmpty(chatId))
        {
            Debug.WriteLine(" ChatId ist leer!");
            return;
        }

        _chatManager.DeleteChat(chatId);

        // Wenn der gelöschte Chat aktiv war, neuen Chat starten
        if (_chatManager.ActiveChat == null)
        {
            StartNewChat();
        }
    }

    private void OnToggleChatHistory(object sender, EventArgs e)
    {
        ChatHistoryOverlay.IsVisible = !ChatHistoryOverlay.IsVisible;
    }

    private void LoadActiveChat()
    {
        MessagesLayout.Children.Clear();

        if (_chatManager.ActiveChat == null) return;

        foreach (var message in _chatManager.ActiveChat.Messages)
        {
            AddMessageToUI(message.Text, message.IsUser);
        }

        Debug.WriteLine($" Chat geladen: {_chatManager.ActiveChat.Messages.Count} Nachrichten");
    }

    private void OnSendClicked(object sender, EventArgs e)
    {
        // Verbindungsprüfung
        if (!_haClient.IsConnected)
        {
            Debug.WriteLine(" Nachricht nicht gesendet - keine Verbindung zu Home Assistant");
            return;
        }

        string? message = MessageEntry.Text?.Trim();

        if (string.IsNullOrEmpty(message))
            return;

        // Nachricht in Chat speichern
        _chatManager.AddMessage(message, isUser: true);

        // UI aktualisieren
        AddMessageToUI(message, isUser: true);

        _haClient.SendText(message);

        MessageEntry.Text = string.Empty;
        (MessagesLayout.Parent as ScrollView)?.ScrollToAsync(0, double.MaxValue, true);
    }

    public void OnResponse(string response)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Nachricht speichern
            _chatManager.AddMessage(response, isUser: false);

            // UI aktualisieren
            AddMessageToUI(response, isUser: false);

            (MessagesLayout.Parent as ScrollView)?.ScrollToAsync(0, double.MaxValue, true);
        });
    }

    private void AddMessageToUI(string text, bool isUser)
    {
        string styleKey = isUser ? "UserChat" : "GizmoChat";

        if (Application.Current.Resources.TryGetValue(styleKey, out object styleObject)
            && styleObject is Style loadedStyle)
        {
            var editor = new Editor
            {
                Text = text,
                IsReadOnly = true,
                AutoSize = EditorAutoSizeOption.TextChanges,
                BackgroundColor = Colors.Transparent
            };

            if (!isUser && Application.Current.Resources.TryGetValue("PrimaryDarkText", out object textColorObj)
                && textColorObj is Color textColor)
            {
                editor.TextColor = textColor;
            }

            var border = new Border
            {
                Style = loadedStyle,
                Content = editor
            };

            MessagesLayout.Add(border);
        }
        else
        {
            Debug.WriteLine($" Style '{styleKey}' nicht gefunden!");
        }
    }
}