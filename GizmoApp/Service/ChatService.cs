using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;

namespace GizmoApp.Service
{
    public class ChatService
    {
        private readonly HomeAssistService _ha;
        private int _msgId = 1;

        public event Action<string, bool>? OnChatMessage;
        public event Action? OnStopThinking;
        public event Action<string>? OnToast;

        public ChatService(HomeAssistService ha)
        {
            _ha = ha;
            _ha.OnMessageReceived += HandleIncoming;
        }

        public async Task SendUserMessage(string text)
        {
            await _ha.EnsureConnectedAsync();
            OnChatMessage?.Invoke(text, true);

            var payload = new
            {
                id = _msgId++,
                type = "assist_pipeline/run",
                start_stage ="intent",
                end_stage = "intent",
                input = new
                {
                    text = text
                },
                pipeline = "01hnnbz7n3mszayy67m7q9g90p"
            };

            string json = JsonSerializer.Serialize(payload);
            await _ha.SendAsync(json);
        }

        private void HandleIncoming(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                System.Diagnostics.Debug.WriteLine("Received JSON: " + json);

                // 🟥 Fehlerhafte Resultate erkennen
                if (root.TryGetProperty("type", out var typeProp) &&
                    typeProp.GetString() == "result")
                {
                    bool success = root.TryGetProperty("success", out var successProp) && successProp.GetBoolean();

                    if (!success && root.TryGetProperty("error", out var errorProp))
                    {
                        string code = errorProp.TryGetProperty("code", out var codeProp)
                            ? codeProp.GetString() ?? "unknown"
                            : "unknown";
                        string message = errorProp.TryGetProperty("message", out var msgProp)
                            ? msgProp.GetString() ?? "Unbekannter Fehler"
                            : "Unbekannter Fehler";

                        System.Diagnostics.Debug.WriteLine($"❌ Assist-Fehler: {message}");

                        // 🧠 Animation stoppen & Toast anzeigen
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            OnStopThinking?.Invoke();
                            OnToast?.Invoke($"Fehler: {message}");
                        });

                        // optional: bei intent-not-supported gezielt reagieren
                        if (code == "intent-not-supported")
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                OnToast?.Invoke("⚠️ Intent Engine nicht gefunden – bitte Home Assistant prüfen.");
                            });
                        }

                        return; // abbrechen, kein Intent-End verarbeiten
                    }
                }

                // 🟩 Erfolgreiche Intent-Antwort verarbeiten
                if (root.TryGetProperty("type", out typeProp) &&
                    typeProp.GetString() == "event" &&
                    root.TryGetProperty("event", out var eventProp) &&
                    eventProp.TryGetProperty("type", out var evtType) &&
                    evtType.GetString() == "intent-end")
                {
                    if (eventProp.TryGetProperty("data", out var data) &&
                        data.TryGetProperty("intent_output", out var intentOutput) &&
                        intentOutput.TryGetProperty("response", out var response) &&
                        response.TryGetProperty("speech", out var speech) &&
                        speech.TryGetProperty("plain", out var plain) &&
                        plain.TryGetProperty("speech", out var spoken))
                    {
                        string msg = spoken.GetString() ?? "";
                        OnChatMessage?.Invoke(msg, false);

                        // 🧠 Animation stoppen, Erfolg anzeigen
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            OnStopThinking?.Invoke();
                            OnToast?.Invoke("✅ Antwort empfangen");
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error handling incoming message: " + ex.Message);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    OnStopThinking?.Invoke();
                    OnToast?.Invoke($"⚠️ Fehler beim Verarbeiten: {ex.Message}");
                });
            }
        }
    }
}
