using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GizmoApp.Models
{
    public class ChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; }

        [JsonPropertyName("parts")]
        public List<ChatPart> Parts { get; set; } = new();

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Diese Property ist jetzt sowohl lesbar als auch schreibbar:
        [JsonIgnore]
        public string Text
        {
            get
            {
                if (Parts == null || Parts.Count == 0)
                    return string.Empty;

                // Spezialfall: Toolcalls besser darstellen
                if (Role == "tool")
                {
                    string toolName = "";
                    string text = "";

                    foreach (var p in Parts)
                    {
                        // Toolcalls haben oft kein .Text
                        if (!string.IsNullOrWhiteSpace(p.Text))
                            text += p.Text;

                        // Toolcall-ID extrahieren
                        if (p.ToolCallId != null)
                            toolName = p.ToolCallId;
                    }

                    if (!string.IsNullOrWhiteSpace(toolName) && !string.IsNullOrWhiteSpace(text))
                        return $"[🔧 Denke nach... (Rufe Tool {toolName}\n{text}auf)]";

                    if (!string.IsNullOrWhiteSpace(toolName))
                        return $"[🔧 Denke nach... (Rufe Tool {toolName}auf)]";

                    if (!string.IsNullOrWhiteSpace(text))
                        return text;

                    return "[🔧 Denke nach... (Toolcall ausgeführt)]";
                }

                // 🔥 WICHTIG: ALLE Teile zusammenführen
                return string.Concat(Parts.Select(p => p.Text));
            }
            set
            {
                if (Parts == null)
                    Parts = new List<ChatPart>();

                // Wenn du neuen Text setzt → wir ersetzen ALLE Parts durch 1 Part
                Parts.Clear();
                Parts.Add(new ChatPart { Text = value });
            }
        }
    }

    public class ChatPart
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("tool_call_id")]
        public string? ToolCallId { get; set; }
    }
}
