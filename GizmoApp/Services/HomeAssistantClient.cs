
using dotenv.net;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json; 
using System.Threading.Tasks;
using Websocket.Client;

namespace GizmoApp.Services
{
    public class HomeAssistantClient
    {
        private WebsocketClient? _client;
        private ChatManager _chatManager => ChatManager.Instance;

        private int _messageId = 1;

        private string? _url;
        private string? _token;

        private string _deviceId = "";

        public event Action<string>? ResponseReceived;
        public event Action<bool>? ConnectionStateChanged; // true = verbunden, false = getrennt

        public bool IsConnected => _client?.IsRunning == true;

        public HomeAssistantClient()
        {
            // Im Konstruktor NICHTS laden - nur initialisieren
            Debug.WriteLine("🔧 HomeAssistantClient wird initialisiert...");
        }
        public async Task InitializeAsync()
        {
#if DEBUG
        // 🔧 Debug-Modus: bequem von .env laden
        var envPath = Path.Combine(AppContext.BaseDirectory, ".env");
        if (File.Exists(envPath))
        {
            DotEnv.Load(new DotEnvOptions(envFilePaths: new[] { envPath }));
            _url = Environment.GetEnvironmentVariable("HA_BASE_URL") ?? "";
            _token = Environment.GetEnvironmentVariable("HA_TOKEN") ?? "";
        }
        else
        {
            Debug.WriteLine("⚠️ Keine .env gefunden – lade aus SecureStorage oder config.json");
            (_url, _token) = await LoadFromConfigOrStorageAsync();
        }
#else
            // 📦 Release: nur über config.json oder SecureStorage
            (_url, _token) = await LoadFromConfigOrStorageAsync();
#endif

            if (string.IsNullOrEmpty(_url) || string.IsNullOrEmpty(_token))
                throw new Exception("❌ HA configuration missing!");

            Debug.WriteLine($"✅ Loaded HA_URL = {_url}");


        }

        private async Task<(string, string)> LoadFromConfigOrStorageAsync()
        {
            string? url = null;
            string? token = null;

            // 📁 1️⃣ Zuerst prüfen, ob sie im AppData-Verzeichnis liegt
            string configPath = Path.Combine(FileSystem.AppDataDirectory, "config.json");
            if (File.Exists(configPath))
            {
                return await LoadAndCacheConfigAsync(configPath);
            }

            // 📁 2️⃣ Dann im BaseDirectory (nur Debug oder Desktop)
            var localConfig = Path.Combine(AppContext.BaseDirectory, "config.json");
            if (File.Exists(localConfig))
            {
                return await LoadAndCacheConfigAsync(localConfig);
            }

            // 📦 3️⃣ Fallback: Embedded Resource laden
            var assembly = typeof(HomeAssistantClient).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.Contains("config.json", StringComparison.OrdinalIgnoreCase));

            if (resourceName != null)
            {
                using Stream? stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    string json = reader.ReadToEnd();
                    var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
                    url = data.GetValueOrDefault("HA_BASE_URL", "");
                    token = data.GetValueOrDefault("HA_TOKEN", "");

                    if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(token))
                        throw new Exception("❌ config.json enthält keine gültigen Werte.");

                    try
                    {
                        await SecureStorage.SetAsync("HA_BASE_URL", url);
                        await SecureStorage.SetAsync("HA_TOKEN", token);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"⚠️ Konnte nicht in SecureStorage speichern: {ex.Message}");
                    }

                    Debug.WriteLine($"✅ Loaded config from embedded resource: {resourceName}");
                    return (url, token);
                }
            }

            // 🔐 4️⃣ Als LETZTES: SecureStorage versuchen (falls UI schon bereit ist)
            try
            {
                url = await SecureStorage.GetAsync("HA_BASE_URL");
                token = await SecureStorage.GetAsync("HA_TOKEN");

                if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(token))
                {
                    Debug.WriteLine("✅ Loaded from SecureStorage");
                    return (url, token);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ SecureStorage Fehler: {ex.Message}");
            }

            Debug.WriteLine("📦 Resources found: " + string.Join(", ", assembly.GetManifestResourceNames()));


            throw new Exception("❌ Keine gültige HA-Konfiguration gefunden!");
        }

        private static async Task<(string, string)> LoadAndCacheConfigAsync(string path)
        {
            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
            var url = data["HA_BASE_URL"];
            var token = data["HA_TOKEN"];

            try
            {
                await SecureStorage.SetAsync("HA_BASE_URL", url);
                await SecureStorage.SetAsync("HA_TOKEN", token);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ Konnte nicht in SecureStorage speichern: {ex.Message}");
            }

            Debug.WriteLine($"✅ Loaded config from file: {path}");
            return (url, token);
        }

        public async Task ConnectAsync()
        {
            if (_client?.IsRunning == true)
                return; // Bereits verbunden

            _messageId = 1;

            var uri = new Uri($"{_url}/api/websocket");
            _client = new WebsocketClient(uri);
            _client.MessageReceived.Subscribe(msg =>
            {
                Debug.WriteLine($"HA -> {msg.Text}");

                try
                {
                    using var doc = JsonDocument.Parse(msg.Text);
                    var root = doc.RootElement;

                    // Wir interessieren uns nur für "event"-Nachrichten
                    if (root.TryGetProperty("event", out JsonElement eventElement))
                    {
                        // Typ des Events prüfen
                        if (eventElement.TryGetProperty("type", out JsonElement typeElement) &&
                            typeElement.GetString() == "intent-end")
                        {
                            // Hier steckt die eigentliche Antwort drin
                            // ✨ Conversation ID von HA holen und Antwort extrahieren
                            if (eventElement.TryGetProperty("data", out JsonElement data) &&
                                data.TryGetProperty("intent_output", out JsonElement intentOutput))
                            {
                                // Conversation ID speichern
                                if (intentOutput.TryGetProperty("conversation_id", out JsonElement convId))
                                {
                                    string haConvId = convId.GetString() ?? "";
                                    if (!string.IsNullOrEmpty(haConvId))
                                    {
                                        _chatManager.SetHaConversationId(haConvId);
                                    }
                                }

                                // Antwort extrahieren
                                if (intentOutput.TryGetProperty("response", out JsonElement response) &&
                                    response.TryGetProperty("speech", out JsonElement speech) &&
                                    speech.TryGetProperty("plain", out JsonElement plain) &&
                                    plain.TryGetProperty("speech", out JsonElement speechText))
                                {
                                    string answer = speechText.GetString() ?? "";
                                    Debug.WriteLine($"💬 Gizmo antwortet: {answer}");

                                    // Callback an deine ChatView
                                    ResponseReceived?.Invoke(answer);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[HA Parse Error] {ex.Message}");
                }
            });

            await _client.Start();

            // Authentifizierung
            var auth = new { type = "auth", access_token = _token };
            _client.Send(JsonSerializer.Serialize(auth));
            ConnectionStateChanged?.Invoke(true); // ✅ Verbunden

            _client.ReconnectionHappened.Subscribe(info =>
            {
                System.Diagnostics.Debug.WriteLine($"[HA] Reconnected: {info.Type}");
                ConnectionStateChanged?.Invoke(true); // ✅ Verbunden
            });

            _client.DisconnectionHappened.Subscribe(info =>
            {
                Debug.WriteLine($"[HA] Disconnected: {info.Type}, Reason: {info.CloseStatusDescription}");
                ConnectionStateChanged?.Invoke(false); // ❌ Getrennt
            });
        }
        public void SendText(string text)
        {
            if (_client == null || !_client.IsRunning)
            {
                Debug.WriteLine("⚠️ Not connected to Home Assistant.");
                return;
            }

            if (_chatManager.ActiveChat == null)
                _chatManager.StartNewChat();

            var chat = _chatManager.ActiveChat ?? _chatManager.StartNewChat();

            // ✨ WICHTIG: Nur conversation_id senden, wenn HA bereits eine vergeben hat
            object msg;

            if (string.IsNullOrEmpty(chat.HaConversationId))
            {
                // Erste Nachricht - OHNE conversation_id
                msg = new
                {
                    id = _messageId++,
                    type = "assist_pipeline/run",
                    start_stage = "intent",
                    end_stage = "intent",
                    input = new { text },
                    pipeline = "01hnnbz7n3mszayy67m7q9g90p"
                };
                Debug.WriteLine($"📤 Erste Nachricht (ohne conversation_id)");
            }
            else
            {
                // Folgenachrichten - MIT conversation_id von HA
                msg = new
                {
                    id = _messageId++,
                    type = "assist_pipeline/run",
                    start_stage = "intent",
                    end_stage = "intent",
                    input = new { text },
                    pipeline = "01hnnbz7n3mszayy67m7q9g90p",
                    conversation_id = chat.HaConversationId
                };
                Debug.WriteLine($"📤 Folgenachricht (mit conversation_id: {chat.HaConversationId})");
            }

            string json = JsonSerializer.Serialize(msg);
            Debug.WriteLine($"zu HA -> {json}");
            _client.Send(json);
        }

    }
}
