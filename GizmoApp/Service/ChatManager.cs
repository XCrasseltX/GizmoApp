using GizmoApp.Models;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GizmoApp.Service
{
    public static class ChatManager
    {
        private static List<Chat> _chats = new();
        private static readonly object _chatsLock = new();

        private static Chat? _activeChat;

        private const int RedisPort = 5003;
        private const string RedisHost = "192.168.178.71"; // oder lokal IP deines Pi
        private const string RedisPrefix = "gizmo:conv:";

        // ConnectionMultiplexer reuse
        private static ConnectionMultiplexer? _redis;
        private static readonly SemaphoreSlim _redisLock = new(1, 1);

        private static readonly string _localFilePath =
            Path.Combine(FileSystem.AppDataDirectory, "chats.json");

        private static async Task<ConnectionMultiplexer> GetRedisAsync()
        {
            if (_redis != null && _redis.IsConnected)
                return _redis;

            await _redisLock.WaitAsync();
            try
            {
                if (_redis != null && _redis.IsConnected)
                    return _redis;

                var config = $"{RedisHost}:{RedisPort}";
                _redis = await ConnectionMultiplexer.ConnectAsync(config);
                return _redis!;
            }
            finally
            {
                _redisLock.Release();
            }
        }
        //nurnoch anpassen das die geladenen chats auch angezeigt werden und die conversation id passt und funktioniert
        public static async Task<bool> LoadFromRedisAsync()
        {
            try
            {
                var redis = await GetRedisAsync();
                var db = redis.GetDatabase();

                var endpoints = redis.GetEndPoints();
                System.Diagnostics.Debug.WriteLine($"Endpoints: {string.Join(", ", endpoints.Select(e => e.ToString()))}");
                if (endpoints == null || endpoints.Length == 0)
                {
                    System.Diagnostics.Debug.WriteLine("❌ Redis: Keine Endpoints gefunden");
                    return false;
                }

                var server = redis.GetServer(endpoints[0]);
                System.Diagnostics.Debug.WriteLine($"Verbunden mit Server: {server.EndPoint}");

                // Clear existing chats to avoid duplicates
                lock (_chatsLock)
                {
                    _chats.Clear();
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var keys = server.Keys(pattern: $"{RedisPrefix}*").ToList();
                System.Diagnostics.Debug.WriteLine($"Gefundene Keys: {keys.Count}");

                // Iterate keys (be cautious: KEYS can be expensive on large DBs)
                foreach (var key in server.Keys(pattern: $"{RedisPrefix}*"))
                {
                    string id = key.ToString().Replace(RedisPrefix, "");
                    string json = await db.StringGetAsync(key);

                    if (string.IsNullOrWhiteSpace(json))
                        continue;

                    try
                    {
                        Chat? loadedChat = null;

                        try
                        {
                            // Erst versuchen, das volle Chat-Objekt zu lesen
                            loadedChat = JsonSerializer.Deserialize<Chat>(json, options);
                        }
                        catch (JsonException)
                        {
                            // Ignorieren – wir versuchen gleich das Array-Format
                        }

                        // Wenn das fehlschlägt, versuchen wir ein Message-Array zu parsen
                        // Versuchen, dass es sicher ein JSON-Array von Nachrichten ist
                        if (loadedChat == null)
                        {
                            if (json.TrimStart().StartsWith("["))
                            {
                                try
                                {
                                    var msgs = JsonSerializer.Deserialize<List<ChatMessage>>(json, options);
                                    if (msgs != null)
                                    {
                                        loadedChat = new Chat
                                        {
                                            ChatId = id,
                                            Title = $"🗨️ {id.Substring(0, 6)}",
                                            Messages = msgs,
                                            LastUsed = msgs.LastOrDefault()?.Timestamp ?? DateTime.UtcNow
                                        };
                                        Debug.WriteLine($"✔ Parsed as message list: {id} ({msgs.Count} msgs)");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"⚠️ Failed to parse message array for {id}: {ex.Message}");
                                }
                            }
                            else
                            {
                                Debug.WriteLine($"⚠️ Key {id} does not contain a message array.");
                            }
                        }

                        // 🔥 ConversationId per METADATA aus Redis nachladen
                        if (loadedChat != null)
                        {
                            string metaKey = $"gizmo:convmeta:{id}";
                            string metaJson = await db.StringGetAsync(metaKey);

                            // ❌ Keine Metadaten → Chat überspringen
                            if (string.IsNullOrWhiteSpace(metaJson))
                            {
                                Debug.WriteLine($"❌ No metadata found for {id}, skipping chat.");
                                continue;
                            }

                            try
                            {
                                var metaObj = JsonSerializer.Deserialize<Dictionary<string, object>>(metaJson);

                                if (metaObj == null ||
                                    !metaObj.TryGetValue("conversation_id", out var cidObj))
                                {
                                    Debug.WriteLine($"❌ Metadata for {id} missing conversation_id, skipping");
                                    continue;
                                }

                                string convId = cidObj?.ToString();

                                if (string.IsNullOrWhiteSpace(convId))
                                {
                                    Debug.WriteLine($"❌ Metadata for {id} has empty conversation_id, skipping");
                                    continue;
                                }

                                // 💥 HIER: ConversationId dem Chat zuweisen
                                loadedChat.ConversationId = convId;

                                Debug.WriteLine($"🔗 ConversationId set from META: {convId}");
                            }
                            catch (Exception exMeta)
                            {
                                Debug.WriteLine($"⚠️ Meta parsing failed for {metaKey}: {exMeta.Message}");
                                continue; // Fehler → Chat wird ignoriert
                            }
                        }


                        if (loadedChat != null)
                        {
                            lock (_chatsLock)
                                _chats.Add(loadedChat);
                        }
                    }
                    catch (Exception exKey)
                    {
                        Debug.WriteLine($"❌ Fehler beim Laden von {key}: {exKey.Message}");
                    }
                }

                System.Diagnostics.Debug.WriteLine("✅ Redis erfolgreich synchronisiert");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Redis Sync fehlgeschlagen: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        public static IEnumerable<Chat> GetAllChats()
        {
            lock (_chatsLock)
            {
                return _chats.OrderByDescending(c => c.LastUsed).ToList();
            }
        }

        public static void SetActiveChat(string chatId)
        {
            lock (_chatsLock)
            {
                var chat = _chats.FirstOrDefault(c => c.ChatId == chatId);
                if (chat != null)
                {
                    _activeChat = chat;
                    chat.LastUsed = DateTime.UtcNow;
                }
            }
        }

        public static Chat? GetActiveChat()
        {
            lock (_chatsLock)
            {
                return _activeChat ?? _chats.OrderByDescending(c => c.LastUsed).FirstOrDefault();
            }
        }

        public static string? GetActiveChatId()
        {
            return GetActiveChat()?.ChatId;
        }

        public static void ClearConversationId()
        {
            var a = GetActiveChat();
            if (a != null) a.ConversationId = null;
        }

        public static Chat EnsureActiveChat()
        {
            lock (_chatsLock)
            {
                // Wenn schon ein aktiver Chat da ist → den nehmen
                if (_activeChat != null)
                    return _activeChat;

                // Falls es schon Chats gibt, nimm den zuletzt verwendeten
                var existing = _chats.OrderByDescending(c => c.LastUsed).FirstOrDefault();
                if (existing != null)
                {
                    _activeChat = existing;
                    return existing;
                }

                // Sonst: NEUEN Chat anlegen
                var chat = new Chat
                {
                    ChatId = Guid.NewGuid().ToString(),
                    Title = "Neuer Chat",
                    LastUsed = DateTime.UtcNow,
                    Messages = new List<ChatMessage>()
                };

                _chats.Add(chat);
                _activeChat = chat;
                return chat;
            }
        }

        public static async Task SaveToLocalAsync()
        {
            try
            {
                var dir = Path.GetDirectoryName(_localFilePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(_chats);

                using (var stream = File.Open(_localFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream))
                {
                    await writer.WriteAsync(json);
                    await writer.FlushAsync();   // 🔥 wichtig für Android
                }

                System.Diagnostics.Debug.WriteLine("✅ Chats lokal gespeichert.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Fehler beim Speichern lokaler Chats: {ex.Message}");
            }
        }

        public static async Task LoadFromLocalAsync()
        {
            try
            {
                if (!File.Exists(_localFilePath))
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ Lokale Datei existiert noch nicht.");
                    return;
                }

                string json;

                using (var stream = File.Open(_localFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                {
                    json = await reader.ReadToEndAsync();
                }

                if (string.IsNullOrWhiteSpace(json))
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ Lokale Datei ist leer.");
                    return;
                }

                var loaded = JsonSerializer.Deserialize<List<Chat>>(json);
                if (loaded != null)
                {
                    _chats.Clear();
                    _chats.AddRange(loaded);
                }

                System.Diagnostics.Debug.WriteLine($"📥 Chats geladen: {_chats.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Fehler beim Laden lokaler Chats: {ex.Message}");
            }
        }
    }
}
