using System.Text.Json;
using System.Diagnostics;

namespace GizmoApp.Services
{
    public class ChatManager
    {
        private static ChatManager? _instance;
        public static ChatManager Instance => _instance ??= new ChatManager();
        public string DeviceId { get; private set; }
        private readonly string _storagePath;
        private readonly string _deviceIdPath;
        private readonly Dictionary<string, ChatSession> _chats = new();
        private ChatSession? _activeChat;

        // Event, wenn sich die Chat-Liste ändert
        public event Action? ChatsChanged;

        private ChatManager()
        {
            _storagePath = Path.Combine(FileSystem.AppDataDirectory, "chats.json");
            _deviceIdPath = Path.Combine(FileSystem.AppDataDirectory, "device_id.txt");

            DeviceId = LoadOrCreateDeviceId();
            LoadChats();
        }

        public ChatSession? ActiveChat => _activeChat;
        public IReadOnlyDictionary<string, ChatSession> Chats => _chats;

        // Gibt Chats sortiert nach Datum zurück (neueste zuerst)
        public List<ChatSession> GetSortedChats()
        {
            return _chats.Values
                .OrderByDescending(c => c.LastMessageAt)
                .ToList();
        }

        public ChatSession StartNewChat()
        {
            string chatId = $"{DeviceId}-CHAT{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            var chat = new ChatSession { 
                ChatId = chatId, 
                MessageCounter = 1,
                CreatedAt = DateTime.Now,
                LastMessageAt = DateTime.Now
            };

            _chats[chatId] = chat;
            _activeChat = chat;
            SaveChats();
            ChatsChanged?.Invoke();

            Debug.WriteLine($"🆕 Neuer Chat gestartet: {chatId}");
            return chat;
        }

        public bool ActivateChat(string chatId)
        {
            if (_chats.TryGetValue(chatId, out var chat))
            {
                _activeChat = chat;
                Debug.WriteLine($"🔄 Chat wieder aktiviert: {chatId}");
                ChatsChanged?.Invoke();
                return true;
            }

            Debug.WriteLine($"⚠️ Chat-ID {chatId} nicht gefunden!");
            return false;
        }

        public void AddMessage(string text, bool isUser)
        {
            if (_activeChat == null) return;

            var message = new ChatMessage
            {
                Text = text,
                IsUser = isUser,
                Timestamp = DateTime.Now
            };

            _activeChat.Messages.Add(message);
            _activeChat.LastMessageAt = DateTime.Now;

            SaveChats();
            ChatsChanged?.Invoke();
        }

        public void IncrementMessageId()
        {
            if (_activeChat == null) return;
            _activeChat.MessageCounter++;
            SaveChats();
        }

        public void DeleteChat(string chatId)
        {
            if (_chats.Remove(chatId))
            {
                if (_activeChat?.ChatId == chatId)
                {
                    _activeChat = null;
                }
                SaveChats();
                ChatsChanged?.Invoke();
                Debug.WriteLine($"🗑️ Chat gelöscht: {chatId}");
            }
        }

        private void LoadChats()
        {
            if (!File.Exists(_storagePath)) return;

            try
            {
                var json = File.ReadAllText(_storagePath);
                var loaded = JsonSerializer.Deserialize<Dictionary<string, ChatSession>>(json);
                if (loaded != null)
                {
                    foreach (var c in loaded)
                        _chats[c.Key] = c.Value;
                }
                Debug.WriteLine($"✅ {_chats.Count} Chats geladen");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ Fehler beim Laden der Chats: {ex.Message}");
            }
        }

        private void SaveChats()
        {
            try
            {
                var json = JsonSerializer.Serialize(_chats, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_storagePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ Fehler beim Speichern der Chats: {ex.Message}");
            }
        }

        private string LoadOrCreateDeviceId()
        {
            try
            {
                if (File.Exists(_deviceIdPath))
                {
                    string existing = File.ReadAllText(_deviceIdPath).Trim();
                    if (!string.IsNullOrEmpty(existing))
                        return existing;
                }

                string newId = Guid.NewGuid().ToString();
                File.WriteAllText(_deviceIdPath, newId);
                Debug.WriteLine($"🆔 Neue DeviceID erzeugt: {newId}");
                return newId;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ Fehler beim Erzeugen der DeviceID: {ex.Message}");
                return "unknown-device";
            }
        }
    }
}
