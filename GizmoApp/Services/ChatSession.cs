using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GizmoApp.Services
{
    public class ChatSession
    {
        public string ChatId { get; set; } = string.Empty;
        // HA Conversation ID (wird von Home Assistant vergeben)
        public string? HaConversationId { get; set; } = null;
        public int MessageCounter { get; set; } = 1;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastMessageAt { get; set; } = DateTime.Now;

        // Speichert alle Nachrichten dieses Chats
        public List<ChatMessage> Messages { get; set; } = new();

        // Für die Anzeige in der Liste - zeigt erste User-Nachricht oder "Neuer Chat"
        public string PreviewText => Messages.Count > 0
            ? Messages[0].Text
            : "Neuer Chat";
    }

    public class ChatMessage
    {
        public string Text { get; set; } = string.Empty;
        public bool IsUser { get; set; } // true = User, false = Gizmo
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
